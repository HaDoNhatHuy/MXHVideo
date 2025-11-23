from flask import Flask, request, jsonify
import pandas as pd
import numpy as np
from sqlalchemy import create_engine
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import linear_kernel
from scipy.sparse.linalg import svds
import random
import threading
import time
from datetime import datetime, timedelta

app = Flask(__name__)

# --- CẤU HÌNH DB ---
DB_CONNECTION_STR = 'mssql+pyodbc://sa:123456@LAPTOP-0SJ4D6P1\\NHATHUY1910/clipShare?driver=ODBC+Driver+17+for+SQL+Server'
db_engine = create_engine(DB_CONNECTION_STR)

# --- GLOBAL CACHE ---
# Dùng biến toàn cục để lưu model trong RAM, API chỉ việc đọc ra -> Tốc độ tức thì
CACHE = {
    "df_videos": None, 
    "df_interactions": None, 
    "cosine_sim": None,
    "preds_df": None, 
    "indices": None,
    "trending_list": [],
    "last_updated": None,
    "is_updating": False
}

def train_model_background():
    """Hàm chạy ngầm để train model, không làm chặn request của người dùng"""
    print(f"[{datetime.now()}] --- BẮT ĐẦU RETRAIN MODEL ---")
    try:
        # 1. Load Data (Chỉ lấy các trường cần thiết để nhẹ RAM)
        query_videos = "SELECT v.Id, v.Title, v.CategoryId, v.RecognizedCelebrities, c.CategoryName FROM Video v LEFT JOIN Category c ON v.CategoryId = c.Id"
        query_interactions = """
            SELECT AppUserId, VideoId, NumberOfVisit as Score FROM VideoView
            UNION ALL
            SELECT AppUserId, VideoId, 5.0 as Score FROM LikeDislike WHERE Liked = 1
        """
        
        df_videos = pd.read_sql(query_videos, db_engine)
        df_interactions = pd.read_sql(query_interactions, db_engine)

        # Gộp điểm nếu 1 user vừa view vừa like video đó
        df_interactions = df_interactions.groupby(['AppUserId', 'VideoId'])['Score'].sum().reset_index()

        # 2. Content-Based (TF-IDF) - Tính 1 lần lưu RAM
        df_videos['soup'] = (df_videos['Title'].fillna('') + ' ' + 
                             df_videos['RecognizedCelebrities'].fillna('') + ' ' + 
                             df_videos['CategoryName'].fillna(''))
        tfidf = TfidfVectorizer(stop_words='english', max_features=5000) # Giới hạn features để nhanh hơn
        tfidf_matrix = tfidf.fit_transform(df_videos['soup'])
        cosine_sim = linear_kernel(tfidf_matrix, tfidf_matrix)
        indices = pd.Series(df_videos.index, index=df_videos['Id']).drop_duplicates()

        # 3. Collaborative Filtering (SVD)
        preds_df = None
        if not df_interactions.empty:
            pivot_table = df_interactions.pivot_table(index='AppUserId', columns='VideoId', values='Score', fill_value=0)
            pivot_matrix = pivot_table.values
            
            # Chỉ chạy SVD nếu có đủ dữ liệu
            if pivot_matrix.shape[0] > 1 and pivot_matrix.shape[1] > 1:
                k = min(50, min(pivot_matrix.shape) - 1)
                U, sigma, Vt = svds(pivot_matrix, k=k)
                sigma = np.diag(sigma)
                all_user_predicted_ratings = np.dot(np.dot(U, sigma), Vt)
                preds_df = pd.DataFrame(all_user_predicted_ratings, columns=pivot_table.columns, index=pivot_table.index)

        # 4. Trending List (Dựa trên tổng điểm cao nhất)
        trending = df_interactions.groupby('VideoId')['Score'].sum().sort_values(ascending=False).head(200).index.tolist()

        # CẬP NHẬT CACHE
        CACHE["df_videos"] = df_videos
        CACHE["cosine_sim"] = cosine_sim
        CACHE["indices"] = indices
        CACHE["preds_df"] = preds_df
        CACHE["trending_list"] = trending
        CACHE["last_updated"] = datetime.now()
        CACHE["is_updating"] = False
        print(f"[{datetime.now()}] --- HOÀN TẤT RETRAIN ---")

    except Exception as e:
        print(f"Lỗi Retrain: {e}")
        CACHE["is_updating"] = False

def check_and_update_model():
    """Kiểm tra thời gian để update model"""
    now = datetime.now()
    # Update mỗi 15 phút hoặc nếu chưa có dữ liệu
    if CACHE["df_videos"] is None or (CACHE["last_updated"] and (now - CACHE["last_updated"]) > timedelta(minutes=15)):
        if not CACHE["is_updating"]:
            CACHE["is_updating"] = True
            # Chạy ở luồng riêng để không block API
            thread = threading.Thread(target=train_model_background)
            thread.start()

@app.route('/api/recommend', methods=['POST'])
def recommend():
    # Kiểm tra update model (chạy ngầm)
    check_and_update_model()

    # Nếu Cache chưa sẵn sàng (lần chạy đầu tiên), đợi 1 chút hoặc trả về rỗng
    if CACHE["df_videos"] is None:
        return jsonify({"user_id": "", "recommendations": []})

    data = request.json
    user_id = data.get('userId')
    current_video_id = data.get('currentVideoId') # Có thể là None nếu ở Home Page

    final_list = []
    seen = set()
    
    # Thêm video hiện tại vào seen để không bao giờ đề xuất lại video đang xem
    if current_video_id:
        seen.add(current_video_id)

    # --- CHIẾN LƯỢC 1: CONTENT-BASED (Nếu đang xem video - Ưu tiên tìm video tương tự) ---
    if current_video_id and CACHE["indices"] is not None and current_video_id in CACHE["indices"]:
        try:
            idx = CACHE["indices"][current_video_id]
            sim_scores = list(enumerate(CACHE["cosine_sim"][idx]))
            sim_scores = sorted(sim_scores, key=lambda x: x[1], reverse=True)
            # Lấy 30 video tương tự nhất
            content_rec = CACHE["df_videos"]['Id'].iloc[[i[0] for i in sim_scores[1:31]]].tolist()
            
            # Shuffle nhẹ để mỗi lần F5 thứ tự thay đổi một chút
            random.shuffle(content_rec)
            
            for vid in content_rec:
                if vid not in seen:
                    final_list.append(vid)
                    seen.add(vid)
        except:
            pass

    # --- CHIẾN LƯỢC 2: COLLABORATIVE FILTERING (Cá nhân hóa theo User) ---
    if user_id and CACHE["preds_df"] is not None and user_id in CACHE["preds_df"].index:
        try:
            user_predictions = CACHE["preds_df"].loc[user_id].sort_values(ascending=False)
            # Lấy top 50 dự đoán
            cf_rec = user_predictions.head(50).index.tolist()
            random.shuffle(cf_rec) # Shuffle để trang chủ luôn mới mẻ với user
            
            for vid in cf_rec:
                if vid not in seen:
                    final_list.append(vid)
                    seen.add(vid)
        except:
            pass

    # --- CHIẾN LƯỢC 3: TRENDING/RANDOM (Lấp đầy danh sách) ---
    # Lấy danh sách trending từ Cache
    trending_pool = CACHE["trending_list"].copy()
    
    # Lấy thêm random video từ toàn bộ DB để đảm bảo đa dạng (Serendipity)
    all_videos = CACHE["df_videos"]['Id'].tolist()
    random_pool = random.sample(all_videos, min(len(all_videos), 50))
    
    fill_pool = list(set(trending_pool + random_pool))
    random.shuffle(fill_pool) # Quan trọng: Trộn ngẫu nhiên mỗi lần gọi API

    for vid in fill_pool:
        if vid not in seen:
            final_list.append(vid)
            seen.add(vid)
            if len(final_list) >= 24: # Lấy đủ số lượng cần thiết (ví dụ 24 video)
                break

    return jsonify({"user_id": user_id, "recommendations": final_list})

if __name__ == '__main__':
    # Train lần đầu ngay khi khởi động
    train_model_background()
    app.run(host='0.0.0.0', port=5001)
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

# Helper: Chuyển đổi "mm:ss" hoặc "h:mm:ss" sang giây
def parse_duration(duration_str):
    if not duration_str: return 0
    try:
        parts = list(map(int, duration_str.split(':')))
        if len(parts) == 3: return parts[0]*3600 + parts[1]*60 + parts[2]
        if len(parts) == 2: return parts[0]*60 + parts[1]
        return 0
    except: return 0

def train_model_background():
    """Hàm chạy ngầm để train model"""
    print(f"[{datetime.now()}] --- BẮT ĐẦU RETRAIN MODEL ---")
    try:
        # 1. Load Data
        query_videos = """
SELECT 
    v.Id,
    v.Title,
    v.CategoryId,
    v.RecognizedCelebrities,
    v.Duration,
    v.ChannelId,
    c.Name AS CategoryName
FROM Video v
LEFT JOIN Category c ON v.CategoryId = c.Id
"""
        
        # Lấy dữ liệu thô để tính điểm
        query_raw_interactions = """
        SELECT 
            vv.AppUserId, 
            vv.VideoId, 
            vv.ProgressSeconds,
            v.Duration,
            ISNULL(ld.Liked, -1) as LikedStatus 
        FROM VideoView vv
        JOIN Video v ON vv.VideoId = v.Id
        LEFT JOIN LikeDislike ld ON vv.AppUserId = ld.AppUserId AND vv.VideoId = ld.VideoId
        """
        
        df_videos = pd.read_sql(query_videos, db_engine)
        df_raw_interactions = pd.read_sql(query_raw_interactions, db_engine) # [FIX 1] Đổi tên biến tránh nhầm lẫn

        # Xử lý tính điểm (Implicit Rating)
        interaction_scores = []

        # [FIX 1] Loop qua df_raw_interactions chứ không phải df_raw (biến chưa define)
        for index, row in df_raw_interactions.iterrows():
            duration_sec = parse_duration(row['Duration'])
            progress = row['ProgressSeconds'] if row['ProgressSeconds'] else 0
            
            # Tính % đã xem
            percent_watched = 0
            if duration_sec > 0:
                percent_watched = min(progress / duration_sec, 1.0)
            
            # --- CÔNG THỨC TÍNH ĐIỂM ---
            score = 1.0 
            
            # Yếu tố 1: Thời lượng xem
            if percent_watched > 0.8: 
                score += 4.0
            elif percent_watched > 0.5:
                score += 2.5 
            elif percent_watched < 0.1:
                score -= 0.5 
            
            # Yếu tố 2: Like / Dislike
            if row['LikedStatus'] == 1: 
                score += 3.0 
            elif row['LikedStatus'] == 0: 
                score = -5.0 
            
            interaction_scores.append({
                'AppUserId': row['AppUserId'],
                'VideoId': row['VideoId'],
                'Score': score
            })
            
        df_scored = pd.DataFrame(interaction_scores)
        
        # Gộp điểm nếu 1 user có nhiều record với 1 video (lấy trung bình hoặc max)
        if not df_scored.empty:
            df_interactions = df_scored.groupby(['AppUserId', 'VideoId'])['Score'].mean().reset_index()
        else:
            df_interactions = pd.DataFrame(columns=['AppUserId', 'VideoId', 'Score'])

        # 2. Content-Based (TF-IDF)
        df_videos['soup'] = (
            df_videos['Title'].fillna('') + ' ' +
            df_videos['RecognizedCelebrities'].fillna('') + ' ' +
            df_videos['CategoryName'].fillna('')
        )
        
        tfidf = TfidfVectorizer(stop_words='english', max_features=5000)
        tfidf_matrix = tfidf.fit_transform(df_videos['soup'])
        cosine_sim = linear_kernel(tfidf_matrix, tfidf_matrix)
        indices = pd.Series(df_videos.index, index=df_videos['Id']).drop_duplicates()

        # 3. Collaborative Filtering (SVD)
        preds_df = None
        if not df_interactions.empty:
            pivot_table = df_interactions.pivot_table(index='AppUserId', columns='VideoId', values='Score', fill_value=0)
            pivot_matrix = pivot_table.values
            
            if pivot_matrix.shape[0] > 1 and pivot_matrix.shape[1] > 1:
                k = min(50, min(pivot_matrix.shape) - 1)
                U, sigma, Vt = svds(pivot_matrix, k=k)
                sigma = np.diag(sigma)
                all_user_predicted_ratings = np.dot(np.dot(U, sigma), Vt)
                preds_df = pd.DataFrame(all_user_predicted_ratings, columns=pivot_table.columns, index=pivot_table.index)

        # 4. Trending List (Dựa trên tổng điểm cao nhất)
        if not df_interactions.empty:
            trending = df_interactions.groupby('VideoId')['Score'].sum().sort_values(ascending=False).head(500).index.tolist()
        else:
            trending = []
            
        CACHE["trending_list"] = trending
        CACHE["df_videos"] = df_videos
        CACHE["cosine_sim"] = cosine_sim
        CACHE["indices"] = indices
        CACHE["preds_df"] = preds_df
        CACHE["last_updated"] = datetime.now()
        CACHE["is_updating"] = False
        print(f"[{datetime.now()}] --- HOÀN TẤT RETRAIN ---")

    except Exception as e:
        print(f"Lỗi Retrain: {e}")
        CACHE["is_updating"] = False

def check_and_update_model():
    now = datetime.now()
    if CACHE["df_videos"] is None or (CACHE["last_updated"] and (now - CACHE["last_updated"]) > timedelta(minutes=15)):
        if not CACHE["is_updating"]:
            CACHE["is_updating"] = True
            thread = threading.Thread(target=train_model_background)
            thread.start()

@app.route('/api/recommend', methods=['POST'])
def recommend():
    check_and_update_model()

    if CACHE["df_videos"] is None:
        return jsonify({"user_id": "", "recommendations": []})

    data = request.json
    user_id = data.get('userId')
    current_video_id = data.get('currentVideoId') 
    
    # [FIX 2] Xử lý excludeIds: Đảm bảo set chứa chuỗi (vì JSON gửi lên là chuỗi)
    raw_exclude_ids = data.get('excludeIds', [])
    exclude_ids = set(str(uid).lower() for uid in raw_exclude_ids) # Chuyển hết về string lower để so sánh

    # Lấy danh sách Blocked
    blocked_videos = set()
    if user_id:
        query_block = f"SELECT TargetId, Type FROM UserBlock WHERE AppUserId = '{user_id}'"
        try:
            df_block = pd.read_sql(query_block, db_engine)
            # Video bị block
            direct_blocked = df_block[df_block['Type'] == 'Video']['TargetId'].apply(lambda x: str(x).lower()).tolist()
            blocked_videos.update(direct_blocked)
            
            # Channel bị block
            blocked_channels = df_block[df_block['Type'] == 'Channel']['TargetId'].tolist()
            if blocked_channels:
                # Tìm video thuộc channel bị block
                # Lưu ý: df_videos phải có cột ChannelId
                blocked_channel_videos = CACHE["df_videos"][CACHE["df_videos"]['ChannelId'].isin(blocked_channels)]['Id'].apply(lambda x: str(x).lower()).tolist()
                blocked_videos.update(blocked_channel_videos)
        except Exception as e:
            print(f"Lỗi Block check: {e}")

    # Tổng hợp danh sách cần loại bỏ (Đã xem + Bị block + Video hiện tại)
    ignore_list = exclude_ids.union(blocked_videos)
    if current_video_id: ignore_list.add(str(current_video_id).lower())

    final_list = []
    seen_in_this_request = set() # Để tránh trùng lặp nội bộ trong danh sách trả về lần này

    # Hàm kiểm tra hợp lệ (Clean code)
    def is_valid_video(vid):
        vid_str = str(vid).lower()
        if vid_str in ignore_list: return False
        if vid_str in seen_in_this_request: return False
        return True

    # --- CHIẾN LƯỢC 1: CONTENT-BASED ---
    if current_video_id and CACHE["indices"] is not None and current_video_id in CACHE["indices"]:
        try:
            idx = CACHE["indices"][current_video_id]
            sim_scores = list(enumerate(CACHE["cosine_sim"][idx]))
            sim_scores = sorted(sim_scores, key=lambda x: x[1], reverse=True)
            content_rec = CACHE["df_videos"]['Id'].iloc[[i[0] for i in sim_scores[1:31]]].tolist()
            random.shuffle(content_rec)
            
            for vid in content_rec:
                if is_valid_video(vid): # [FIX 3] Phải check ignore_list ở đây
                    final_list.append(vid)
                    seen_in_this_request.add(str(vid).lower())
        except:
            pass

    # --- CHIẾN LƯỢC 2: COLLABORATIVE FILTERING ---
    if user_id and CACHE["preds_df"] is not None and user_id in CACHE["preds_df"].index:
        try:
            user_predictions = CACHE["preds_df"].loc[user_id].sort_values(ascending=False)
            cf_rec = user_predictions.head(50).index.tolist()
            random.shuffle(cf_rec)
            
            for vid in cf_rec:
                if is_valid_video(vid): # [FIX 3] Phải check ignore_list ở đây
                    final_list.append(vid)
                    seen_in_this_request.add(str(vid).lower())
        except:
            pass

    # --- CHIẾN LƯỢC 3: TRENDING + RANDOM (Lấp đầy) ---
    needed = 12 # Số lượng cần trả về
    
    # Ưu tiên Trending trước
    for vid in CACHE["trending_list"]:
        if len(final_list) >= needed: break
        if is_valid_video(vid):
            final_list.append(vid)
            seen_in_this_request.add(str(vid).lower())

    # Nếu vẫn chưa đủ, lấy Random từ toàn bộ DB
    if len(final_list) < needed:
        all_videos = CACHE["df_videos"]['Id'].tolist()
        random.shuffle(all_videos)
        for vid in all_videos:
            if len(final_list) >= needed: break
            if is_valid_video(vid):
                final_list.append(vid)
                seen_in_this_request.add(str(vid).lower())
    
    # Trả về kết quả
    return jsonify({"user_id": user_id, "recommendations": final_list})

if __name__ == '__main__':
    train_model_background()
    app.run(host='0.0.0.0', port=5001)
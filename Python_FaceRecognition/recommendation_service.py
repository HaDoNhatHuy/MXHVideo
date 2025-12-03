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
import math

app = Flask(__name__)

# --- CẤU HÌNH DB ---
# Sử dụng chuỗi kết nối từ nguồn của bạn (giữ nguyên)
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
    "is_updating": False,
    "user_celeb_preference": {} # THÊM MỚI: {userId: personalized_celeb_ratio (0.4 to 0.6)}
}

# Helper: Chuyển đổi "mm:ss" hoặc "h:mm:ss" sang giây 
def parse_duration(duration_str):
    if not duration_str: return 0 
    try:
        parts = list(map(int, duration_str.split(':'))) 
        if len(parts) == 3: return parts*3600 + parts[2]*60 + parts[3] 
        if len(parts) == 2: return parts*60 + parts[2] 
        return 0
    except: return 0

def train_model_background():
    """Hàm chạy ngầm để train model và tính toán sở thích người nổi tiếng cá nhân"""
    
    print(f"[{datetime.now()}] --- BẮT ĐẦU RETRAIN MODEL ---")
    
    # Đặt cờ cập nhật
    CACHE["is_updating"] = True
    
    try:
        # 1. Load Data
        query_videos = """ 
        SELECT v.Id, v.Title, v.CategoryId, v.Duration, v.ChannelId, 
        STRING_AGG(c.Name, ', ') WITHIN GROUP (ORDER BY c.Name) AS RecognizedCelebrities 
        FROM Video v 
        LEFT JOIN RecognizeCelebrities rc ON v.Id = rc.VideoId 
        LEFT JOIN Celebrity c ON rc.CelebrityId = c.Id 
        GROUP BY v.Id, v.Title, v.CategoryId, v.Duration, v.ChannelId 
        """

        query_raw_interactions = """ 
        SELECT
        vv.AppUserId, vv.VideoId, vv.ProgressSeconds, v.Duration, ISNULL(ld.Liked, -1) as LikedStatus
        FROM VideoView vv 
        JOIN Video v ON vv.VideoId = v.Id 
        LEFT JOIN LikeDislike ld ON vv.AppUserId = ld.AppUserId AND vv.VideoId = ld.VideoId 
        """
        
        df_videos = pd.read_sql(query_videos, db_engine) 
        # Chuyển ID về chuỗi lowercase
        df_videos['Id'] = df_videos['Id'].apply(str).str.lower()
        df_videos.set_index('Id', inplace=True)
        
        df_raw_interactions = pd.read_sql(query_raw_interactions, db_engine)
        
        # THAY ĐỔI: Thêm cột has_celebrity và list_celebrities 
        df_videos['has_celebrity'] = df_videos['RecognizedCelebrities'].notnull() & (df_videos['RecognizedCelebrities'] != '')
        df_videos['list_celebrities'] = df_videos['RecognizedCelebrities'].apply(
            lambda x: set(y.strip() for y in x.replace("Đã nhận diện: ", "").split(', ')) 
            if pd.notnull(x) and x != '' else set()
        )
        
        # Xử lý tính điểm (Implicit Rating)
        interaction_scores = []
        for index, row in df_raw_interactions.iterrows():
            duration_sec = parse_duration(row['Duration'])
            progress = row['ProgressSeconds'] if row['ProgressSeconds'] else 0
            
            # Tính % đã xem
            percent_watched = 0 if duration_sec > 0 else 0
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
                'VideoId': str(row['VideoId']).lower(), 
                'Score': score
            })
        
        df_scored = pd.DataFrame(interaction_scores)
        
        # Gộp điểm nếu 1 user có nhiều record với 1 video
        if not df_scored.empty:
            df_interactions = df_scored.groupby(['AppUserId', 'VideoId'])['Score'].mean().reset_index()
        else: 
            df_interactions = pd.DataFrame(columns=['AppUserId', 'VideoId', 'Score'])
        
        # [MỚI] 1. TÍNH TOÁN TỶ LỆ ƯU TIÊN NGƯỜI NỔI TIẾNG CÁ NHÂN HÓA
        user_celeb_score = {}
        if not df_interactions.empty:
            # Gắn cờ celebrity cho df_interactions
            celeb_map = df_videos['has_celebrity'].to_dict()
            df_interactions['is_celeb'] = df_interactions['VideoId'].apply(lambda x: celeb_map.get(x, False))
            
            # Tính tổng điểm tương tác cho video Celebrity và Non-Celebrity
            grouped = df_interactions.groupby(['AppUserId', 'is_celeb'])['Score'].sum().unstack(fill_value=0)
            
            # Đảm bảo cột True/False tồn tại
            if True not in grouped.columns: grouped[True] = 0
            if False not in grouped.columns: grouped[False] = 0
                
            grouped['TotalScore'] = grouped[True] + grouped[False]
            
            # Tránh chia cho 0 nếu TotalScore <= 0
            grouped['CelebRatio'] = np.where(grouped['TotalScore'] > 0, grouped[True] / grouped['TotalScore'], 0)
            
            # Công thức mapping Tỷ lệ: 0.4 (thấp nhất) + (CelebRatio * 0.2) (cao nhất là 0.6)
            # Nếu user không có TotalScore > 0, ratio = 0.4 (mặc định)
            grouped['PersonalizedRatio'] = grouped['CelebRatio'].apply(lambda x: 0.4 + (x * 0.2))
            
            user_celeb_score = grouped['PersonalizedRatio'].to_dict()
        
        CACHE["user_celeb_preference"] = user_celeb_score
        print(f" + Đã tính xong {len(user_celeb_score)} sở thích người nổi tiếng.")


        # 2. Content-Based (TF-IDF)
        df_videos['soup'] = (
            df_videos['Title'].fillna('') + ' ' + (df_videos['RecognizedCelebrities'].fillna('') + ' ') * 3
        )
        
        tfidf = TfidfVectorizer(stop_words='english', max_features=5000)
        tfidf_matrix = tfidf.fit_transform(df_videos['soup']) 
        cosine_sim = linear_kernel(tfidf_matrix, tfidf_matrix) 
        indices = pd.Series(df_videos.index, index=df_videos['Title']).to_dict() # Index theo VideoId

        # 3. Collaborative Filtering (Matrix Factorization)
        if not df_interactions.empty:
            pivot_table = df_interactions.pivot_table(index='AppUserId', columns='VideoId', values='Score').fillna(0)
            R = pivot_table.values
            user_ratings_mean = np.mean(R, axis=1)
            R_demeaned = R - user_ratings_mean.reshape(-1, 1)    
            
            # --- SỬA LỖI TẠI ĐÂY ---
            # Lấy số dòng (users) và cột (items)
            n_users, n_items = R_demeaned.shape
            
            # Chỉ chạy SVD nếu có ít nhất 2 user và 2 video
            if n_users > 1 and n_items > 1:
                # k phải nhỏ hơn min(n_users, n_items)
                # k = min(50, n_users - 1, n_items - 1)
                k_val = min(n_users, n_items) - 1
                k = min(50, k_val) # Giới hạn max k là 50
                
                if k > 0:
                    U, sigma, Vt = svds(R_demeaned, k=k)
                    sigma = np.diag(sigma)
                    all_user_predicted_ratings = np.dot(np.dot(U, sigma), Vt) + user_ratings_mean.reshape(-1, 1)
                    preds_df = pd.DataFrame(all_user_predicted_ratings, columns=pivot_table.columns, index=pivot_table.index)
                else:
                    preds_df = None
            else:
                preds_df = None
        else:
            preds_df = None
        
        # 4. Trending List (Dựa trên tổng điểm cao nhất) 
        if not df_interactions.empty:
            trending = df_interactions.groupby('VideoId')['Score'].sum().sort_values(ascending=False).head(500).index.tolist()
        else: 
            trending = []

        # 5. Cập nhật Cache
        CACHE["trending_list"] = trending 
        CACHE["df_videos"] = df_videos 
        CACHE["cosine_sim"] = cosine_sim 
        CACHE["indices"] = {str(k).lower(): v for k, v in dict(zip(df_videos.index, range(len(df_videos)))).items()}
        CACHE["preds_df"] = preds_df 
        CACHE["last_updated"] = datetime.now() 
        
        print(f"[{datetime.now()}] --- HOÀN TẤT RETRAIN ---")

    except Exception as e: 
        print(f"Lỗi Retrain: {e}") 
    finally:
        CACHE["is_updating"] = False

def check_and_update_model():
    """Kiểm tra và kích hoạt retrain nếu đã quá 1 phút hoặc cache rỗng."""
    now = datetime.now() 
    if CACHE["df_videos"] is None or (CACHE["last_updated"] and (now - CACHE["last_updated"]) > timedelta(minutes=1)): 
        if not CACHE["is_updating"]:
            print("Kích hoạt retrain model.")
            CACHE["is_updating"] = True 
            thread = threading.Thread(target=train_model_background) 
            thread.start()

@app.route('/api/recommend', methods=['POST']) 
def recommend():
    check_and_update_model()
    
    if CACHE["df_videos"] is None or CACHE["df_videos"].empty: 
        return jsonify({"user_id": "", "recommendations": []})

    data = request.json
    user_id = data.get('userId')
    current_video_id = data.get('currentVideoId')
    
    # Chuẩn hóa ID
    current_video_id = str(current_video_id).lower() if current_video_id else None
    
    # Lấy danh sách cần loại bỏ (Giữ nguyên logic cũ)
    raw_exclude_ids = data.get('excludeIds', [])
    exclude_ids = set(str(uid).lower() for uid in raw_exclude_ids)
    
    blocked_videos = set()
    if user_id:
        query_block = f"SELECT TargetId, Type FROM UserBlock WHERE AppUserId = '{user_id}'"
        try: 
            df_block = pd.read_sql(query_block, db_engine)
            direct_blocked = df_block[df_block['Type'] == 'Video']['TargetId'].apply(lambda x: str(x).lower()).tolist()
            blocked_videos.update(direct_blocked)
            
            blocked_channels = df_block[df_block['Type'] == 'Channel']['TargetId'].tolist()
            if blocked_channels:
                # Lọc trong df_videos (đã set index là ID)
                blocked_channel_videos = CACHE["df_videos"][CACHE["df_videos"]['ChannelId'].isin(blocked_channels)].index.tolist()
                blocked_videos.update(blocked_channel_videos)
        except Exception as e:
            print(f"Lỗi Block check: {e}")

    ignore_list = exclude_ids.union(blocked_videos)
    if current_video_id: 
        ignore_list.add(str(current_video_id).lower())
    
    final_list = []
    seen_in_this_request = set()

    def is_valid_video(vid):
        vid_str = str(vid).lower()
        if vid_str in ignore_list: return False
        if vid_str in seen_in_this_request: return False
        # Đảm bảo video tồn tại trong cache videos
        if vid_str not in CACHE["df_videos"].index: return False
        return True

    is_new_user = True
    if user_id and CACHE["preds_df"] is not None and user_id in CACHE["preds_df"].index:
        is_new_user = False
    
    needed = 12 # Số lượng cần trả về
    
    # --- XÁC ĐỊNH TỶ LỆ VÀ QUOTA CẦN THIẾT ---
    celeb_ratio_default = 0.4 # Mặc định 40%
    is_current_video_celeb = False
    current_celebs = set()
    
    # 1. Kiểm tra video hiện tại có celeb không
    if current_video_id and current_video_id in CACHE["df_videos"].index:
        current_row = CACHE["df_videos"].loc[[current_video_id]]
        if not current_row.empty and current_row['has_celebrity'].iloc[0]:
            is_current_video_celeb = True
            current_celebs = current_row['list_celebrities'].iloc[0]

    # 2. Thiết lập tỷ lệ động
    if is_current_video_celeb:
        # Trường hợp 1: Đang xem video celeb -> Cố định 60%
        celeb_ratio = 0.6
    elif user_id and not is_new_user and user_id in CACHE["user_celeb_preference"]:
        # Trường hợp 2: User cũ, không xem video celeb -> Tỷ lệ cá nhân hóa (40% - 60%)
        celeb_ratio = CACHE["user_celeb_preference"][user_id] 
        # Giới hạn tỷ lệ trong 0.4 đến 0.6 để đảm bảo cân bằng
        celeb_ratio = max(0.4, min(celeb_ratio, 0.6))
    else:
        # Trường hợp 3: User mới/Cold Start/Không có preference -> Mặc định 40%
        celeb_ratio = celeb_ratio_default

    celeb_needed = math.ceil(needed * celeb_ratio) # Làm tròn lên để đảm bảo quota
    general_needed = needed - celeb_needed

    print(f"User: {user_id}, Current Celeb: {is_current_video_celeb}, Ratio: {celeb_ratio:.2f} ({celeb_needed}/{general_needed})")

    
    # [MỚI] --- PHASE 0: COLD START TRANG CHỦ (Chèn 3 video celebrity) ---
    if not current_video_id and is_new_user:
        initial_celeb_count = 3
        
        # Tìm các video celebrity
        celeb_videos_pool = CACHE["df_videos"][CACHE["df_videos"]['has_celebrity']].index.tolist()
        random.shuffle(celeb_videos_pool)
        
        count = 0
        for vid in celeb_videos_pool:
            if count >= initial_celeb_count: break
            if is_valid_video(vid):
                final_list.append(vid)
                seen_in_this_request.add(vid)
                count += 1
        
        print(f"Phase 0 (Cold Start): Added {len(final_list)} celebrity videos.")
    
    
    # --- PHASE 1: FILL CELEB QUOTA (Dựa trên celeb_ratio) ---
    # Chỉ chạy nếu cần thêm video celebrity (tổng số slot celeb cần)
    celeb_slots_filled = len(final_list)
    if celeb_slots_filled < celeb_needed:
        
        # A. Tìm kiếm video có celebrity trùng (Ưu tiên Content-Based nếu đang xem video celeb)
        celeb_rec = []
        if is_current_video_celeb and current_video_id in CACHE["indices"]:
            idx = CACHE["indices"][current_video_id]
            
            # Lọc video khác có celeb trùng
            matching_videos = CACHE["df_videos"][
                CACHE["df_videos"]['list_celebrities'].apply(
                    lambda x: len(x.intersection(current_celebs)) > 0
                )
            ]
            matching_videos = matching_videos.drop(current_video_id, errors='ignore')
            
            if not matching_videos.empty:
                # Sắp xếp theo Content-Based Similarity (TF-IDF)
                matching_indices = [CACHE["indices"][vid] for vid in matching_videos.index if vid in CACHE["indices"]]
                if matching_indices:
                    sim_scores_matching = [(i, CACHE["cosine_sim"][idx][i]) for i in matching_indices]
                    sim_scores_matching = sorted(sim_scores_matching, key=lambda x: x[1], reverse=True)
                    # --- SỬA LỖI TẠI ĐÂY: Unpack tuple (i, score) ---
                    celeb_rec = [CACHE["df_videos"].iloc[i].name for i, score in sim_scores_matching]
                    random.shuffle(celeb_rec)
        
        # Thêm high-confidence celeb matches
        for vid in celeb_rec:
            if celeb_slots_filled >= celeb_needed: break
            if is_valid_video(vid):
                final_list.append(vid)
                seen_in_this_request.add(vid)
                celeb_slots_filled += 1

        # B. Lấp đầy quota bằng Trending Videos có Celebrity
        if celeb_slots_filled < celeb_needed:
            # Lọc trending list để chỉ lấy video có celeb
            celeb_trending = [vid for vid in CACHE["trending_list"] if
                vid in CACHE["df_videos"].index and CACHE["df_videos"].loc[vid, 'has_celebrity'] and
                vid not in seen_in_this_request
            ]
            random.shuffle(celeb_trending) 
            for vid in celeb_trending:
                if celeb_slots_filled >= celeb_needed: break
                if is_valid_video(vid):
                    final_list.append(vid)
                    seen_in_this_request.add(vid)
                    celeb_slots_filled += 1

        # C. Lấp đầy quota bằng Random Videos có Celebrity (Nếu vẫn thiếu)
        if celeb_slots_filled < celeb_needed:
            celeb_videos = CACHE["df_videos"][CACHE["df_videos"]['has_celebrity']].index.tolist()
            random.shuffle(celeb_videos)
            for vid in celeb_videos:
                if celeb_slots_filled >= celeb_needed: break
                if is_valid_video(vid):
                    final_list.append(vid)
                    seen_in_this_request.add(vid)
                    celeb_slots_filled += 1

    
    # --- PHASE 2: FILL REMAINING SLOTS (General Algorithm) ---
    # (Đảm bảo số lượng tổng cộng đạt 12)
    
    # 2. Collaborative Filtering (Ưu tiên cao nhất cho người dùng cũ)
    if not is_new_user and len(final_list) < needed:
        try: 
            user_predictions = CACHE["preds_df"].loc[user_id].sort_values(ascending=False)
            cf_rec = user_predictions.head(50).index.tolist()
            random.shuffle(cf_rec)
            for vid in cf_rec:
                if len(final_list) >= needed: break
                if is_valid_video(vid):
                    final_list.append(vid)
                    seen_in_this_request.add(vid)
        except Exception as e:
            # print(f"CF Error: {e}")
            pass
        
    # 3. Content-Based (Dựa trên video hiện tại, non-celeb priority)
    if current_video_id and current_video_id in CACHE["indices"] and len(final_list) < needed:
        try: 
            idx = CACHE["indices"][current_video_id]
            sim_scores = list(enumerate(CACHE["cosine_sim"][idx]))
            sim_scores = sorted(sim_scores, key=lambda x: x[1], reverse=True)
            # Lấy top 30
            content_rec_indices = [i for i in sim_scores[1:31]]
            content_rec = [CACHE["df_videos"].iloc[i].name for i in content_rec_indices]
            random.shuffle(content_rec) 
            for vid in content_rec:
                if len(final_list) >= needed: break
                if is_valid_video(vid): 
                    final_list.append(vid)
                    seen_in_this_request.add(vid)
        except: pass

    # 4. Trending List (Ưu tiên 3)
    if len(final_list) < needed:
        random.shuffle(CACHE["trending_list"])
        for vid in CACHE["trending_list"]:
            if len(final_list) >= needed: break
            if is_valid_video(vid):
                final_list.append(vid)
                seen_in_this_request.add(vid)

    # 5. Random Fallback (Lấp đầy cuối cùng)
    if len(final_list) < needed:
        # Dùng Index của DF Videos
        all_videos = CACHE["df_videos"].index.tolist()
        random.shuffle(all_videos) 
        for vid in all_videos:
            if len(final_list) >= needed: break
            if is_valid_video(vid):
                final_list.append(vid)
                seen_in_this_request.add(vid)

    # Trả về kết quả (chỉ lấy số lượng cần thiết)
    return jsonify({"user_id": user_id, "recommendations": final_list[:needed]})

if __name__ == '__main__': 
    # Khởi tạo model ngay khi service chạy
    train_model_background() 
    app.run(host='0.0.0.0', port=5001)
from flask import Flask, request, jsonify
import pandas as pd
import numpy as np
from sqlalchemy import create_engine
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import linear_kernel
from scipy.sparse.linalg import svds
import random # Import thêm thư viện random để trộn kết quả

app = Flask(__name__)

# === 1. CẤU HÌNH KẾT NỐI SQL SERVER ===
# Driver có thể là 'ODBC Driver 17 for SQL Server'
DB_CONNECTION_STR = 'mssql+pyodbc://sa:123456@LAPTOP-0SJ4D6P1\\NHATHUY1910/clipShare?driver=ODBC+Driver+17+for+SQL+Server'
db_engine = create_engine(DB_CONNECTION_STR)

def get_data_from_db():
    """Lấy dữ liệu mới nhất từ DB để 'học' liên tục"""
    
    # --- CẢI TIẾN: Join với bảng Category để lấy tên danh mục ---
    query_videos = """
        SELECT v.Id, v.Title, v.CategoryId, v.RecognizedCelebrities, v.UploadDate, c.CategoryName 
        FROM Video v
        LEFT JOIN Category c ON v.CategoryId = c.Id
    """
    
    query_views = "SELECT AppUserId, VideoId, NumberOfVisit FROM VideoView"
    query_likes = "SELECT AppUserId, VideoId, Liked FROM LikeDislike"
    
    df_videos = pd.read_sql(query_videos, db_engine)
    df_views = pd.read_sql(query_views, db_engine)
    df_likes = pd.read_sql(query_likes, db_engine)
    
    # Xử lý dữ liệu tương tác
    # Tính điểm: 1 view = 1 điểm, 1 like = 5 điểm
    df_interactions = pd.merge(df_views, df_likes, on=['AppUserId', 'VideoId'], how='outer').fillna(0)
    df_interactions['Score'] = df_interactions['NumberOfVisit'] + (df_interactions['Liked'] * 5)
    
    return df_videos, df_interactions

# === 2. THUẬT TOÁN CONTENT-BASED (Dựa trên Celeb, Title và Category) ===
def content_based_recommendation(video_id, df_videos, top_k=10):
    try:
        # --- CẢI TIẾN: Tạo "soup" metadata mạnh mẽ hơn ---
        # Kết hợp Title + Người nổi tiếng + Tên danh mục (Lặp lại danh mục để tăng trọng số)
        df_videos['soup'] = (
            df_videos['Title'].fillna('') + ' ' + 
            df_videos['RecognizedCelebrities'].fillna('') + ' ' + 
            df_videos['CategoryName'].fillna('') + ' ' + 
            df_videos['CategoryName'].fillna('') # Hack: Lặp lại để AI ưu tiên video cùng danh mục
        )
        
        tfidf = TfidfVectorizer(stop_words='english')
        tfidf_matrix = tfidf.fit_transform(df_videos['soup'])
        
        # Tính độ tương đồng Cosine
        cosine_sim = linear_kernel(tfidf_matrix, tfidf_matrix)
        
        indices = pd.Series(df_videos.index, index=df_videos['Id']).drop_duplicates()
        
        if video_id not in indices:
            return []
            
        idx = indices[video_id]
        sim_scores = list(enumerate(cosine_sim[idx]))
        
        # Sắp xếp theo điểm tương đồng giảm dần
        sim_scores = sorted(sim_scores, key=lambda x: x[1], reverse=True)
        
        # --- CẢI TIẾN: Lấy danh sách rộng hơn rồi trộn ngẫu nhiên ---
        # Lấy top_k * 3 ứng viên tốt nhất (trừ chính nó ở index 0)
        candidate_pool = sim_scores[1:(top_k * 3) + 1]
        
        # Trộn ngẫu nhiên trong nhóm ứng viên này để mỗi lần F5 ra kết quả khác nhau
        random.shuffle(candidate_pool)
        
        # Sau đó mới cắt lấy top_k
        selected_scores = candidate_pool[:top_k]
        
        video_indices = [i[0] for i in selected_scores]
        return df_videos['Id'].iloc[video_indices].tolist()
    except Exception as e:
        print(f"Content-based error: {e}")
        return []

# === 3. THUẬT TOÁN COLLABORATIVE FILTERING (SVD) ===
def collaborative_filtering(user_id, df_interactions, df_videos, top_k=10):
    try:
        # Tạo Matrix User-Item
        pivot_table = df_interactions.pivot_table(index='AppUserId', columns='VideoId', values='Score', fill_value=0)
        
        # Nếu user chưa có trong matrix (User mới hoàn toàn) -> Trả về rỗng để dùng Fallback
        if user_id not in pivot_table.index:
            return []
            
        # Matrix Factorization
        pivot_matrix = pivot_table.values
        # Giảm chiều dữ liệu (k=50 latent factors)
        k = min(50, min(pivot_matrix.shape) - 1)
        U, sigma, Vt = svds(pivot_matrix, k=k)
        sigma = np.diag(sigma)
        
        # Dự đoán điểm số
        all_user_predicted_ratings = np.dot(np.dot(U, sigma), Vt)
        preds_df = pd.DataFrame(all_user_predicted_ratings, columns=pivot_table.columns, index=pivot_table.index)
        
        # Lấy dự đoán cho user hiện tại
        user_row_number = pivot_table.index.get_loc(user_id)
        sorted_user_predictions = preds_df.iloc[user_row_number].sort_values(ascending=False)
        
        # Lấy top video dự đoán user sẽ thích
        recommendations = sorted_user_predictions.head(top_k * 2).index.tolist()
        
        # Cũng trộn nhẹ kết quả CF để tăng tính khám phá
        random.shuffle(recommendations)
        
        return recommendations[:top_k]
    except Exception as e:
        print(f"CF Error: {e}")
        return []

# === 4. FALLBACK: POPULARITY / TRENDING ===
def get_trending_videos(df_interactions, top_k=20):
    # Video có tổng điểm cao nhất
    trending = df_interactions.groupby('VideoId')['Score'].sum().sort_values(ascending=False).head(top_k)
    return trending.index.tolist()

# === 5. API ENDPOINT ===
@app.route('/api/recommend', methods=['POST'])
def recommend():
    data = request.json
    user_id = data.get('userId')
    current_video_id = data.get('currentVideoId') # Optional: Nếu đang xem video thì dùng cái này boost content-based
    
    # Bước 1: Load dữ liệu mới nhất
    df_videos, df_interactions = get_data_from_db()
    
    recommendations = []
    
    # Chiến lược HYBRID
    
    # A. Content-Based (Ưu tiên cao nếu đang xem video cụ thể)
    if current_video_id:
        cb_results = content_based_recommendation(current_video_id, df_videos, top_k=6) # Tăng số lượng lấy từ Content
        recommendations.extend(cb_results)
        
    # B. Collaborative Filtering (Cá nhân hóa theo User)
    if user_id:
        cf_results = collaborative_filtering(user_id, df_interactions, df_videos, top_k=10)
        recommendations.extend(cf_results)
        
    # C. Loại bỏ trùng lặp nhưng giữ thứ tự ưu tiên
    seen = set()
    unique_recommendations = []
    for x in recommendations:
        if x not in seen and x != current_video_id: # Không recommend lại video đang xem
            unique_recommendations.append(x)
            seen.add(x)
            
    recommendations = unique_recommendations
    
    # D. Fallback: Nếu danh sách ít hơn 12 video, điền thêm bằng Trending
    if len(recommendations) < 12:
        trending = get_trending_videos(df_interactions, top_k=30) # Lấy pool rộng hơn
        random.shuffle(trending) # Trộn trending để không phải lúc nào cũng là video top 1
        for vid in trending:
            if vid not in seen and vid != current_video_id:
                recommendations.append(vid)
                seen.add(vid)
                if len(recommendations) >= 15: # Lấy dư một chút để trộn
                    break
                    
    # --- CẢI TIẾN QUAN TRỌNG: TRỘN KẾT QUẢ CUỐI CÙNG ---
    # Giữ lại 2 video đầu tiên (độ liên quan cao nhất từ Content-Based hoặc CF cao nhất)
    # Trộn ngẫu nhiên phần còn lại để tạo sự mới mẻ khi reload trang
    if len(recommendations) > 2:
        top_2 = recommendations[:2]
        rest = recommendations[2:]
        random.shuffle(rest)
        final_recommendations = top_2 + rest
    else:
        final_recommendations = recommendations

    return jsonify({
        "user_id": user_id,
        "recommendations": final_recommendations[:12] # Chỉ trả về 12 video cho Grid/Sidebar
    })

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5001)
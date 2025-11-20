import face_recognition
import pickle
from flask import Flask, request, jsonify
from process_video import process_video
from video_blurring_processor import blur_selected_celebrity_face
import io
import numpy as np
import base64
from PIL import Image
import json  # ĐÃ THÊM
import shutil  # ĐÃ THÊM
import os

app = Flask(__name__)

# Load embeddings
embeddings_file = "celebrity_embeddings.pkl"
with open(embeddings_file, "rb") as f:
    known_faces_dict = pickle.load(f)

print(f"Đã load {len(known_faces_dict)} celebrities từ embeddings file.")

# ==================== RECOGNIZE FRAME ====================
@app.route('/recognize', methods=['POST'])
def recognize():
    frame_path = request.json['frame_path']
    print(f"Processing frame: {frame_path}")
    
    frame = face_recognition.load_image_file(frame_path)
    face_locations = face_recognition.face_locations(frame)
    face_encodings = face_recognition.face_encodings(frame, face_locations)

    recognized_celebrities = []
    for face_encoding in face_encodings:
        best_match_name = "Unknown"
        best_match_distance = 1.0

        for name, known_faces in known_faces_dict.items():
            distances = face_recognition.face_distance(known_faces, face_encoding)
            min_distance = min(distances)
            if min_distance < best_match_distance:
                best_match_distance = min_distance
                best_match_name = name

        if best_match_distance < 0.45:
            recognized_celebrities.append(best_match_name)
            print(f"Matched {best_match_name} with distance {best_match_distance}")
        else:
            recognized_celebrities.append("Unknown")

    return jsonify({"celebrities": recognized_celebrities})

# ==================== PROCESS VIDEO ====================
@app.route('/process_video', methods=['POST'])
def process_video_endpoint():
    video_path = request.json['video_path']
    try:
        frames = process_video(video_path, known_faces_dict)
        return jsonify({"frames": frames})
    except Exception as e:
        print(f"Error in process_video_endpoint: {str(e)}")
        return jsonify({"error": str(e)}), 500

# ==================== BLUR & GHI ĐÈ GỐC ====================
# Trong app.py, sửa hàm blur_endpoint
@app.route('/blur_selected_celebrity', methods=['POST'])
def blur_endpoint():
    data = request.json
    video_path = data.get('video_path')
    frames_json = data.get('celebrity_frames_json')
    celeb_name = data.get('celebrity_to_blur')

    if not all([video_path, frames_json, celeb_name]):
        return jsonify({"error": "Thiếu dữ liệu"}), 400

    try:
        frames_data = json.loads(frames_json)
        # Gọi hàm xử lý, nhận về đường dẫn file mới
        output_path = blur_selected_celebrity_face(
            video_path=video_path,
            frames_data=frames_data,
            celebrity_to_blur=celeb_name
        )

        if output_path and os.path.exists(output_path):
            return jsonify({
                "status": "success", 
                "message": "Đã làm mờ xong.", 
                "output_path": output_path 
            })
        else:
            return jsonify({"status": "error", "message": "Xử lý thất bại."}), 500

    except Exception as e:
        print(f"Error: {str(e)}")
        return jsonify({"error": str(e)}), 500

# ==================== RECOGNIZE IMAGE ====================
@app.route('/recognize_image', methods=['POST'])
def recognize_image():
    data = request.json
    if 'image_base64' not in data:
        return jsonify({"error": "Thiếu image_base64"}), 400
    
    try:
        image_data = base64.b64decode(data['image_base64'])
        image = Image.open(io.BytesIO(image_data))
        frame = np.array(image)
    except Exception as e:
        print(f"Error decoding image: {str(e)}")
        return jsonify({"error": "Hình ảnh không hợp lệ"}), 400
    
    face_locations = face_recognition.face_locations(frame)
    face_encodings = face_recognition.face_encodings(frame, face_locations)
    
    recognized_celebs = []
    for face_encoding in face_encodings:
        best_match_name = "Unknown"
        best_match_distance = 1.0
        
        for name, known_faces in known_faces_dict.items():
            distances = face_recognition.face_distance(known_faces, face_encoding)
            min_distance = min(distances)
            if min_distance < best_match_distance:
                best_match_distance = min_distance
                best_match_name = name
        
        if best_match_distance < 0.45:
            recognized_celebs.append(best_match_name)
        else:
            recognized_celebs.append("Unknown")
    
    unique_celebs = list(set([c for c in recognized_celebs if c != "Unknown"]))
    return jsonify({"celebrities": unique_celebs})

if __name__ == '__main__':
    app.run(host='localhost', port=5000)
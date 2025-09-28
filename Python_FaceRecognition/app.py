# app.py
import face_recognition
import pickle
from flask import Flask, request, jsonify

app = Flask(__name__)

# Load embeddings đã lưu
embeddings_file = "celebrity_embeddings.pkl"
with open(embeddings_file, "rb") as f:
    known_faces_dict = pickle.load(f)

print(f"✅ Đã load {len(known_faces_dict)} celebrities từ embeddings file.")

@app.route('/recognize', methods=['POST'])
def recognize():
    frame_path = request.json['frame_path']
    print(f"Processing frame: {frame_path}")
    
    # Load ảnh frame
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

        if best_match_distance < 0.4:
            recognized_celebrities.append(best_match_name)
            print(f"Matched {best_match_name} with distance {best_match_distance}")
        else:
            recognized_celebrities.append("Unknown")

    return jsonify({"celebrities": recognized_celebrities})

if __name__ == '__main__':
    app.run(host='localhost', port=5000)

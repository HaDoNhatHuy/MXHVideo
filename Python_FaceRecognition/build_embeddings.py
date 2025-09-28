# build_embeddings.py
import os
import face_recognition
import pickle

# Thư mục ảnh
celebrity_dir = "D:/GraduateProjectFaceRecognition/MXHVideo-main/Video-AI_Management/Web_Video/wwwroot/celebrity_images"
# File lưu embeddings
embeddings_file = "celebrity_embeddings.pkl"

def build_embeddings():
    known_faces_dict = {}
    for celeb_name in os.listdir(celebrity_dir):
        celeb_path = os.path.join(celebrity_dir, celeb_name)
        if os.path.isdir(celeb_path):
            for filename in os.listdir(celeb_path):
                if filename.lower().endswith((".jpg", ".png", ".jpeg")):
                    image_path = os.path.join(celeb_path, filename)
                    image = face_recognition.load_image_file(image_path)
                    encodings = face_recognition.face_encodings(image)
                    if len(encodings) > 0:
                        if celeb_name not in known_faces_dict:
                            known_faces_dict[celeb_name] = []
                        known_faces_dict[celeb_name].append(encodings[0])
                        print(f"Encoded: {celeb_name} - {filename}")
    # Lưu ra file pkl
    with open(embeddings_file, "wb") as f:
        pickle.dump(known_faces_dict, f)
    print("✅ Đã build và lưu embeddings.")

if __name__ == "__main__":
    build_embeddings()

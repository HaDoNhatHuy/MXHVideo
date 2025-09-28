import os
import face_recognition

CELEBRITY_DIR = "D:/GraduateProjectClone/MXHVideo-Clone/Video-AI_Management/Web_Video/wwwroot/Celebrity_Faces_Dataset"
deleted = 0
for celeb_name in os.listdir(CELEBRITY_DIR):
    if celeb_name == 'unknown':
        continue
    celeb_path = os.path.join(CELEBRITY_DIR, celeb_name)
    if os.path.isdir(celeb_path):
        files_to_delete = []
        for filename in os.listdir(celeb_path):
            if filename.endswith((".jpg", ".jpeg", ".png")):
                image_path = os.path.join(celeb_path, filename)
                try:
                    image = face_recognition.load_image_file(image_path)
                    encodings = face_recognition.face_encodings(image)
                    if len(encodings) == 0:
                        files_to_delete.append(image_path)
                except:
                    files_to_delete.append(image_path)
        for file_path in files_to_delete:
            os.remove(file_path)
            deleted += 1
            print(f"Deleted: {file_path}")
print(f"Deleted {deleted} invalid images. Run app.py again!")
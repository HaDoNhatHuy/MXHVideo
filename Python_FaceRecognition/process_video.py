# process_video.py
import cv2
import base64
import os
import face_recognition

def process_video(video_path, known_faces_dict):
    if not os.path.exists(video_path):
        raise ValueError(f"Video file not found: {video_path}")

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise ValueError(f"Could not open video: {video_path}")

    fps = cap.get(cv2.CAP_PROP_FPS)
    if fps == 0: fps = 30

    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    
    # --- THAY ĐỔI QUAN TRỌNG: Tăng tần suất nhận diện ---
    # Thay vì int(fps) (mỗi giây 1 lần), ta lấy mỗi 5 frame (khoảng 0.15 giây)
    # Số càng nhỏ thì càng mượt nhưng xử lý càng lâu.
    frame_interval = 5 
    
    current_time = 0.0
    celebrity_frames = {} 

    print(f"Processing video: {video_path} (FPS: {fps}, Interval: {frame_interval})")

    frame_idx = 0
    while True:
        ret, frame = cap.read()
        if not ret: break

        # Chỉ xử lý các frame nằm trong interval để tối ưu tốc độ
        if frame_idx % frame_interval == 0:
            
            # Resize nhỏ lại để nhận diện nhanh hơn (tùy chọn, ở đây giữ nguyên để chính xác)
            # small_frame = cv2.resize(frame, (0, 0), fx=0.5, fy=0.5)
            
            # Chuyển BGR (OpenCV) sang RGB (face_recognition)
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            
            face_locations = face_recognition.face_locations(rgb_frame)
            face_encodings = face_recognition.face_encodings(rgb_frame, face_locations)

            for i, face_encoding in enumerate(face_encodings):
                best_match_name = "Unknown"
                best_match_distance = 1.0

                for name, known_faces in known_faces_dict.items():
                    distances = face_recognition.face_distance(known_faces, face_encoding)
                    min_distance = min(distances)

                    if min_distance < best_match_distance:
                        best_match_distance = min_distance
                        best_match_name = name
                
                # Ngưỡng nhận diện (0.45 là khá chặt chẽ)
                if best_match_distance < 0.45:
                    loc = face_locations[i] # (top, right, bottom, left)
                    
                    # Lưu ảnh base64 để hiện lên UI (chỉ lưu frame đại diện mỗi giây để nhẹ JSON)
                    frame_base64 = ""
                    # Chỉ convert ảnh khi cần thiết (ví dụ mỗi 1 giây mới lưu ảnh 1 lần cho UI đỡ nặng)
                    if frame_idx % int(fps) < frame_interval: 
                        _, buffer = cv2.imencode('.jpg', frame)
                        if buffer is not None:
                            frame_base64 = base64.b64encode(buffer).decode('utf-8')
                    
                    if best_match_name not in celebrity_frames:
                        celebrity_frames[best_match_name] = []

                    # Tính thời gian chính xác của frame này
                    exact_time = frame_idx / fps

                    celebrity_frames[best_match_name].append({
                        "time": round(exact_time, 2), # Lưu số thực, không làm tròn int
                        "loc": loc,
                        "frame": frame_base64 # Có thể rỗng để tiết kiệm
                    })

        frame_idx += 1

    cap.release()
    return celebrity_frames
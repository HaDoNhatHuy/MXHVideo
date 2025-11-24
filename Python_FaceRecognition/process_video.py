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
    
    # Xử lý mỗi 5 frame để tăng tốc độ (Skip frames)
    # Bạn có thể giảm số này xuống (vd: 3) nếu muốn bắt dính khuôn mặt nhạy hơn
    frame_interval = 5 
    frame_idx = 0
    celebrity_frames = {}

    print(f"Processing video: {video_path} (FPS: {fps})")

    while True:
        ret, frame = cap.read()
        if not ret: break

        # Chỉ xử lý các frame theo interval
        if frame_idx % frame_interval == 0:
            # Resize frame nhỏ (0.5) để nhận diện nhanh hơn
            small_frame = cv2.resize(frame, (0, 0), fx=0.5, fy=0.5)
            
            # Chuyển sang RGB
            rgb_small_frame = cv2.cvtColor(small_frame, cv2.COLOR_BGR2RGB)
            
            # Detect khuôn mặt trên frame nhỏ
            face_locations = face_recognition.face_locations(rgb_small_frame)
            face_encodings = face_recognition.face_encodings(rgb_small_frame, face_locations)

            for i, face_encoding in enumerate(face_encodings):
                best_match_name = "Unknown"
                best_match_distance = 1.0

                for name, known_faces in known_faces_dict.items():
                    distances = face_recognition.face_distance(known_faces, face_encoding)
                    if len(distances) > 0:
                        min_distance = min(distances)
                        if min_distance < best_match_distance:
                            best_match_distance = min_distance
                            best_match_name = name

                # --- GIỮ NGUYÊN NGƯỠNG 0.45 THEO YÊU CẦU CỦA BẠN ---
                if best_match_distance < 0.45:
                    # Scale lại tọa độ về kích thước gốc (vì nãy resize 0.5 nên giờ nhân 2)
                    top, right, bottom, left = face_locations[i]
                    top *= 2
                    right *= 2
                    bottom *= 2
                    left *= 2
                    loc = (top, right, bottom, left)

                    # === PHẦN FIX LỖI QUAN TRỌNG ===
                    # Luôn tạo ảnh thumbnail base64 cho frame này để trả về C#
                    # Resize frame xuống height = 150px để nhẹ JSON, gửi qua mạng nhanh hơn
                    h, w = frame.shape[:2]
                    scale_preview = 150 / h
                    preview_frame = cv2.resize(frame, (0, 0), fx=scale_preview, fy=scale_preview)
                    
                    # Nén ảnh JPEG chất lượng 70%
                    _, buffer = cv2.imencode('.jpg', preview_frame, [int(cv2.IMWRITE_JPEG_QUALITY), 70])
                    frame_base64 = base64.b64encode(buffer).decode('utf-8')

                    if best_match_name not in celebrity_frames:
                        celebrity_frames[best_match_name] = []

                    exact_time = frame_idx / fps
                    
                    # Lưu dữ liệu đầy đủ (bao gồm cả chuỗi frame base64)
                    celebrity_frames[best_match_name].append({
                        "time": round(exact_time, 2),
                        "loc": loc,
                        "frame": frame_base64 
                    })

        frame_idx += 1

    cap.release()
    return celebrity_frames
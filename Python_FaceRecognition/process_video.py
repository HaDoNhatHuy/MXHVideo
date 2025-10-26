# process_video.py (chỉ chứa function, không có app Flask)
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
    if fps == 0:
        fps = 30  # Fallback nếu không lấy được FPS

    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    video_duration = total_frames / fps  # Tổng thời lượng video (giây)

    # Khoảng cách trích frame: mỗi giây (tối ưu)
    frame_interval = int(fps)  # Số frame bỏ qua để lấy 1 frame/giây
    current_time = 0.0  # Khởi tạo current_time

    celebrity_frames = {}  # {celeb: [{"time": float, "loc": [T,R,B,L], "frame": base64}, ...]}


    print(f"Processing video: {video_path} (FPS: {fps}, Total duration: {video_duration:.2f}s)")

    current_frame = 0
    while current_frame < total_frames:
        cap.set(cv2.CAP_PROP_POS_FRAMES, current_frame)
        ret, frame = cap.read()
        if not ret:
            break

        # Nhận diện khuôn mặt bằng face_recognition
        face_locations = face_recognition.face_locations(frame)
        face_encodings = face_recognition.face_encodings(frame, face_locations)
        

        # recognized_celebs = []
        recognized_celebs_with_loc = [] # Lưu trữ {name, loc}
        #for face_encoding in face_encodings:
        for i, face_encoding in enumerate(face_encodings):
            best_match_name = "Unknown"
            best_match_distance = 1.0

            for name, known_faces in known_faces_dict.items():
                distances = face_recognition.face_distance(known_faces, face_encoding)
                min_distance = min(distances)
                if min_distance < best_match_distance:
                    best_match_distance = min_distance
                    best_match_name = name

            if best_match_distance < 0.45:
                # Lấy tọa độ khuôn mặt hiện tại
                loc = face_locations[i] # (top, right, bottom, left)
                recognized_celebs_with_loc.append({
                    "name": best_match_name,
                    "loc": loc # Tọa độ [top, right, bottom, left]
                })
                print(f"Matched {best_match_name} with distance {best_match_distance} at time {current_time:.2f}s")

        # Lưu frame nếu có celeb
        if recognized_celebs_with_loc:
            _, buffer = cv2.imencode('.jpg', frame)
            if buffer is not None:
                frame_base64 = base64.b64encode(buffer).decode('utf-8')
                for entry in recognized_celebs_with_loc:  # Unique celeb
                    celeb = entry["name"]
                    loc = entry["loc"]

                    if celeb not in celebrity_frames:
                        celebrity_frames[celeb] = []
                    celebrity_frames[celeb].append({
                        "time": round(current_time, 1),
                        "loc": loc, # THÊM TỌA ĐỘ
                        "frame": frame_base64
                    })

        current_frame += frame_interval
        current_time += 1.0  # Tăng thời gian 1 giây mỗi lần lặp

    cap.release()

    # In kết quả
    print("\nKhung hình xuất hiện của từng celebrity:")
    for celeb, frames in celebrity_frames.items():
        print(f"{celeb}: {len(frames)} frames")

    return celebrity_frames
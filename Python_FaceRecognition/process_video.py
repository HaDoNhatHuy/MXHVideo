import cv2
import base64
import os
import face_recognition
import numpy as np

def process_video(video_path, known_faces_dict):
    if not os.path.exists(video_path):
        raise ValueError(f"Video file not found: {video_path}")

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        raise ValueError(f"Could not open video: {video_path}")

    fps = cap.get(cv2.CAP_PROP_FPS)
    if fps == 0: fps = 30
    
    frame_interval = 5 
    frame_idx = 0
    
    # Dùng list tạm để lưu tất cả detection theo trình tự thời gian
    # Cấu trúc: { "frame_idx": int, "name": str, "loc": tuple, "frame_base64": str }
    raw_detections = []

    print(f"Processing video: {video_path} (FPS: {fps})")

    while True:
        ret, frame = cap.read()
        if not ret: break

        if frame_idx % frame_interval == 0:
            # Resize 0.5 để tăng tốc
            small_frame = cv2.resize(frame, (0, 0), fx=0.5, fy=0.5)
            rgb_small_frame = cv2.cvtColor(small_frame, cv2.COLOR_BGR2RGB)
            
            # Dùng model='hog' (nhanh) hoặc 'cnn' (chính xác hơn với mặt nghiêng nhưng cần GPU)
            # Nếu máy bạn có GPU NVIDIA, hãy đổi model="cnn"
            face_locations = face_recognition.face_locations(rgb_small_frame, model="hog")
            face_encodings = face_recognition.face_encodings(rgb_small_frame, face_locations)

            # Lưu ảnh preview (resize nhỏ) cho UI
            h, w = frame.shape[:2]
            scale_preview = 150 / h
            preview_frame = cv2.resize(frame, (0, 0), fx=scale_preview, fy=scale_preview)
            _, buffer = cv2.imencode('.jpg', preview_frame, [int(cv2.IMWRITE_JPEG_QUALITY), 70])
            frame_base64 = base64.b64encode(buffer).decode('utf-8')

            found_face_in_frame = False

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

                # Tăng ngưỡng lên 0.5 hoặc 0.55 để bắt được mặt nghiêng/kính râm tốt hơn
                # Logic Gap-Filling phía sau sẽ lo việc loại bỏ nhiễu
                if best_match_distance < 0.52: 
                    top, right, bottom, left = face_locations[i]
                    # Scale tọa độ về kích thước gốc
                    loc = (top*2, right*2, bottom*2, left*2)
                    
                    raw_detections.append({
                        "frame_idx": frame_idx,
                        "name": best_match_name,
                        "loc": loc,
                        "frame_base64": frame_base64,
                        "time": frame_idx / fps
                    })
                    found_face_in_frame = True

            # Nếu không tìm thấy ai, có thể lưu lại một entry rỗng để biết frame này đã scan
            if not found_face_in_frame:
                pass 

        frame_idx += 1

    cap.release()

    # === THUẬT TOÁN GAP FILLING (QUAN TRỌNG NHẤT) ===
    # Mục tiêu: Nếu Frame 10 thấy A, Frame 20 thấy A -> Thì Frame 15 cũng phải là A (dù bị che mặt)
    
    final_celebrity_frames = {}
    
    # Ngưỡng thời gian để nối: Nếu mất dấu dưới 2 giây thì coi như vẫn là người đó
    MAX_GAP_SECONDS = 2.0 
    max_gap_frames = int(MAX_GAP_SECONDS * fps)

    # Nhóm detection theo từng người
    detections_by_person = {}
    for d in raw_detections:
        name = d['name']
        if name not in detections_by_person:
            detections_by_person[name] = []
        detections_by_person[name].append(d)

    for name, detections in detections_by_person.items():
        if not detections: continue
        
        # Sắp xếp theo thời gian
        detections.sort(key=lambda x: x['frame_idx'])
        
        filled_detections = []
        
        for i in range(len(detections)):
            current = detections[i]
            filled_detections.append(current)

            # Nếu đây không phải điểm cuối cùng
            if i < len(detections) - 1:
                next_det = detections[i+1]
                gap = next_det['frame_idx'] - current['frame_idx']
                
                # Nếu khoảng trống nhỏ hơn ngưỡng cho phép -> Lấp đầy
                if 1 < gap < max_gap_frames:
                    # Nội suy tuyến tính (Linear Interpolation) vị trí khuôn mặt
                    # Để vùng làm mờ di chuyển mượt từ vị trí cũ sang vị trí mới
                    start_loc = current['loc']
                    end_loc = next_det['loc']
                    
                    steps = int(gap / frame_interval)
                    for step in range(1, steps):
                        interp_idx = current['frame_idx'] + (step * frame_interval)
                        ratio = step / steps
                        
                        # Tính tọa độ trung gian
                        interp_top = int(start_loc[0] + (end_loc[0] - start_loc[0]) * ratio)
                        interp_right = int(start_loc[1] + (end_loc[1] - start_loc[1]) * ratio)
                        interp_bottom = int(start_loc[2] + (end_loc[2] - start_loc[2]) * ratio)
                        interp_left = int(start_loc[3] + (end_loc[3] - start_loc[3]) * ratio)
                        
                        # Tạo frame giả để lấp chỗ trống (dùng lại ảnh base64 cũ để nhẹ)
                        filled_detections.append({
                            "frame_idx": interp_idx,
                            "name": name,
                            "loc": (interp_top, interp_right, interp_bottom, interp_left),
                            "frame_base64": current['frame_base64'], # Dùng lại ảnh cũ
                            "time": round(interp_idx / fps, 2)
                        })

        # Chuyển đổi sang format output cuối cùng
        final_celebrity_frames[name] = []
        for item in filled_detections:
            final_celebrity_frames[name].append({
                "time": round(item['time'], 2),
                "loc": item['loc'],
                "frame": item['frame_base64']
            })

    return final_celebrity_frames
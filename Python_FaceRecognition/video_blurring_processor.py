# video_blurring_processor.py
import cv2
import json
import numpy as np
import os
import math

def blur_face(image, top, right, bottom, left, blur_factor=0.9):
    """Áp dụng Gaussian Blur cho khu vực khuôn mặt."""
    # Lấy vùng khuôn mặt
    face_img = image[top:bottom, left:right]
    
    # Kiểm tra kích thước hợp lệ
    if face_img.size == 0:
        return image
    
    h, w = face_img.shape[:2]
    if h <= 0 or w <= 0:
        return image

    # Tính kernel size (phải là số lẻ, tối thiểu 3)
    kernel_w = max(int(w * blur_factor) | 1, 3)
    kernel_h = max(int(h * blur_factor) | 1, 3)

    # Áp dụng Gaussian Blur
    blurred_face = cv2.GaussianBlur(face_img, (kernel_w, kernel_h), 0)
    
    # Ghi đè lại vào ảnh gốc
    image[top:bottom, left:right] = blurred_face
    
    return image

def blur_selected_celebrity_face(video_path, output_path, frames_data, celebrity_to_blur):
    """
    Tái tạo video, chỉ làm mờ khuôn mặt của celebrity_to_blur dựa trên frames_data.
    """
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"Lỗi: Không thể mở video {video_path}")
        return False

    fps = cap.get(cv2.CAP_PROP_FPS)
    if fps <= 0:
        fps = 30.0  # fallback
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    
    # Encoder
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(output_path, fourcc, fps, (width, height))

    if not out.isOpened():
        print(f"Lỗi: Không thể khởi tạo VideoWriter cho {output_path}")
        cap.release()
        return False

    current_frame_index = 0
    
    # Chuẩn bị dữ liệu làm mờ: {time_in_seconds: [ [T, R, B, L], ... ]}
    blur_targets = {}
    
    if celebrity_to_blur in frames_data:
        for entry in frames_data[celebrity_to_blur]:
            time_sec = int(entry["time"])  # Làm tròn xuống
            loc = entry["loc"]  # [top, right, bottom, left]
            if time_sec not in blur_targets:
                blur_targets[time_sec] = []
            blur_targets[time_sec].append(loc)
            
    print(f"Tìm thấy {sum(len(v) for v in blur_targets.values())} khuôn mặt cần làm mờ cho {celebrity_to_blur} tại {len(blur_targets)} giây.")

    frame_count = 0
    while current_frame_index < total_frames:
        ret, frame = cap.read()
        if not ret:
            break

        current_time_sec = int(current_frame_index / fps)

        if current_time_sec in blur_targets:
            locations_to_blur = blur_targets[current_time_sec]
            for loc in locations_to_blur:
                top, right, bottom, left = loc
                # Kiểm tra tọa độ hợp lệ
                if top < bottom and left < right and top >= 0 and left >= 0 and bottom <= height and right <= width:
                    frame = blur_face(frame, top, right, bottom, left, blur_factor=0.9)
                else:
                    print(f"Cảnh báo: Tọa độ không hợp lệ: {loc}")

        out.write(frame)
        current_frame_index += 1
        frame_count += 1

    cap.release()
    out.release()
    print(f"Hoàn thành xử lý video: {frame_count} frames → {output_path}")
    return True
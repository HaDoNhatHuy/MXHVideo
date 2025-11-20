# video_blurring_processor.py
import cv2
import os

def blur_face(image, top, right, bottom, left, blur_factor=0.9):
    """Áp dụng Gaussian Blur cho khuôn mặt."""
    face_img = image[top:bottom, left:right]
    if face_img.size == 0: return image
    h, w = face_img.shape[:2]
    if h <= 0 or w <= 0: return image
    
    kernel_w = max(int(w * blur_factor) | 1, 25) # Tăng độ mờ
    kernel_h = max(int(h * blur_factor) | 1, 25)
    blurred_face = cv2.GaussianBlur(face_img, (kernel_w, kernel_h), 0)
    image[top:bottom, left:right] = blurred_face
    return image

def blur_selected_celebrity_face(video_path, frames_data, celebrity_to_blur):
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"Lỗi: Không mở được video {video_path}")
        return None # Trả về None nếu lỗi

    fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

    # Tạo tên file output riêng biệt
    output_path = video_path.replace(".mp4", "_blurred.mp4")
    if output_path == video_path: output_path = video_path + "_blurred.mp4"

    # Dùng mp4v để tương thích tốt nhất
    fourcc = cv2.VideoWriter_fourcc(*'mp4v') 
    out = cv2.VideoWriter(output_path, fourcc, fps, (width, height))

    if not out.isOpened():
        print(f"Lỗi: Không tạo được file output {output_path}")
        cap.release()
        return None

    # Chuẩn bị dữ liệu frame cần blur
    blur_targets = {}
    if celebrity_to_blur in frames_data:
        for entry in frames_data[celebrity_to_blur]:
            sec = int(float(entry["time"])) # Fix lỗi parse float string
            loc = entry["loc"]
            blur_targets.setdefault(sec, []).append(loc)

    print(f"Bắt đầu xử lý {total_frames} frames...")
    frame_idx = 0
    
    while True:
        ret, frame = cap.read()
        if not ret: break

        current_sec = int(frame_idx / fps)

        # Nếu giây hiện tại có trong danh sách cần blur
        if current_sec in blur_targets:
            for top, right, bottom, left in blur_targets[current_sec]:
                # Kiểm tra tọa độ hợp lệ
                if 0 <= top < bottom <= height and 0 <= left < right <= width:
                    frame = blur_face(frame, top, right, bottom, left)

        out.write(frame)
        frame_idx += 1

    cap.release()
    out.release()
    
    print(f"Hoàn tất. File mới: {output_path}")
    return output_path # Trả về đường dẫn file mới
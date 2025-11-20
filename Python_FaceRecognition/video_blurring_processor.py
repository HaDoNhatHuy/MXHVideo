# video_blurring_processor.py
import cv2
import json
import os
import subprocess # Dùng để gọi FFmpeg

def blur_face(image, top, right, bottom, left, blur_factor=1.0):
    """Áp dụng Gaussian Blur cực mạnh."""
    h, w = image.shape[:2]
    # Đảm bảo tọa độ nằm trong khung hình
    top = max(0, top)
    left = max(0, left)
    bottom = min(h, bottom)
    right = min(w, right)
    
    face_img = image[top:bottom, left:right]
    if face_img.size == 0: return image

    fh, fw = face_img.shape[:2]
    if fh <= 0 or fw <= 0: return image
    
    # Tăng kích thước kernel để mờ hơn
    k_w = (fw // 3) | 1 # Số lẻ
    k_h = (fh // 3) | 1
    blurred_face = cv2.GaussianBlur(face_img, (k_w, k_h), 30)
    image[top:bottom, left:right] = blurred_face
    return image

def blur_selected_celebrity_face(video_path, frames_data, celebrity_to_blur):
    # --- CONFIG FFMPEG ---
    # Nếu bạn đã cài ffmpeg vào Path thì để nguyên, nếu chưa thì điền đường dẫn đầy đủ
    # Ví dụ: FFMPEG_CMD = r"C:\FFmpeg\ffmpeg\bin\ffmpeg.exe"
    FFMPEG_CMD = "ffmpeg" 
    
    if not os.path.exists(video_path):
        print("Video không tồn tại")
        return False

    cap = cv2.VideoCapture(video_path)
    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))

    # Tên file tạm (chỉ hình ảnh, không tiếng)
    temp_video_no_audio = video_path.replace(".mp4", "_temp_no_audio.mp4")
    # Tên file cuối cùng (có tiếng) - Sẽ ghi đè video gốc sau
    final_output_path = video_path.replace(".mp4", "_final.mp4")

    # Dùng codec mp4v cho tương thích cao
    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(temp_video_no_audio, fourcc, fps, (width, height))

    # --- CHUẨN BỊ DỮ LIỆU BLUR ---
    # Chuyển đổi danh sách time -> frame_index để tra cứu nhanh
    # frame_lookup: { frame_idx: [ (top, right, bottom, left), ... ] }
    blur_lookup = {}
    
    if celebrity_to_blur in frames_data:
        for entry in frames_data[celebrity_to_blur]:
            timestamp = float(entry["time"])
            frame_idx = int(timestamp * fps)
            loc = entry["loc"]
            
            # Vì chúng ta detect mỗi 5 frame, ta cần điền vào các frame trống ở giữa
            # để vệt mờ không bị nhấp nháy.
            # Ta gán vị trí này cho 5 frame tiếp theo (giả sử mặt ít di chuyển trong 0.15s)
            for offset in range(6): 
                target_f = frame_idx + offset
                if target_f not in blur_lookup:
                    blur_lookup[target_f] = []
                blur_lookup[target_f].append(loc)

    print(f"Bắt đầu làm mờ... Total frames: {total_frames}")
    
    current_frame = 0
    while True:
        ret, frame = cap.read()
        if not ret: break

        # Kiểm tra xem frame hiện tại có cần làm mờ không
        if current_frame in blur_lookup:
            for (top, right, bottom, left) in blur_lookup[current_frame]:
                frame = blur_face(frame, top, right, bottom, left)

        out.write(frame)
        current_frame += 1

    cap.release()
    out.release()
    print("Đã xử lý xong hình ảnh. Đang ghép âm thanh...")

    # --- GHÉP ÂM THANH BẰNG FFMPEG ---
    # Lệnh: Lấy hình từ temp_video, lấy tiếng từ video_path, ghép thành final_output_path
    # -c:v copy: Copy hình ảnh (không encode lại -> siêu nhanh)
    # -c:a aac: Encode lại tiếng sang aac (đảm bảo tương thích)
    # -map 0:v: Lấy video từ input 0 (temp)
    # -map 1:a: Lấy audio từ input 1 (gốc)
    # -shortest: Dừng khi stream ngắn nhất kết thúc
    
    try:
        # Cách 1: Nếu video gốc CÓ tiếng
        cmd = [
            FFMPEG_CMD, '-y',
            '-i', temp_video_no_audio, # Input 0: Video đã blur (mp4v)
            '-i', video_path,          # Input 1: Video gốc (có tiếng)
            
            # --- THAY ĐỔI QUAN TRỌNG Ở ĐÂY ---
            '-c:v', 'libx264',         # Chuyển đổi sang chuẩn H.264 (Web hỗ trợ)
            '-pix_fmt', 'yuv420p',     # Định dạng màu tương thích mọi trình duyệt/player
            '-preset', 'fast',         # Tốc độ xử lý nhanh
            '-crf', '23',              # Chất lượng video (18-28 là tốt, thấp hơn là nét hơn)
            # ---------------------------------

            '-c:a', 'aac',             # Encode audio sang AAC
            '-map', '0:v:0',           # Lấy video từ file 0
            '-map', '1:a:0',           # Lấy audio từ file 1
            '-shortest',               # Dừng khi stream ngắn nhất kết thúc (tránh lỗi lệch độ dài)
            final_output_path
        ]
        
        # Chạy lệnh, ẩn cửa sổ console (trên Windows)
        startupinfo = None
        if os.name == 'nt':
            startupinfo = subprocess.STARTUPINFO()
            startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            
        subprocess.run(cmd, check=True, startupinfo=startupinfo)
        print("Ghép âm thanh và chuyển đổi H.264 thành công.")
        
        # --- DỌN DẸP VÀ GHI ĐÈ ---
        if os.path.exists(final_output_path):
            # Xóa file tạm không tiếng
            os.remove(temp_video_no_audio)
            # Trả về đường dẫn file mới (để C# đọc và cập nhật DB)
            return final_output_path 
            
    except subprocess.CalledProcessError as e:
        print(f"Lỗi FFmpeg: {e}")
        # Nếu lỗi (vd video gốc không có tiếng), trả về video không tiếng
        return temp_video_no_audio
    except Exception as e:
        print(f"Lỗi hệ thống: {e}")
        return None

    return None
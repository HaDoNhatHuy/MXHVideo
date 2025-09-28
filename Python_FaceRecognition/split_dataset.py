import os
import shutil
import random

# Paths
SOURCE_DIR = "D:/GraduateProjectClone/MXHVideo-Clone/Video-AI_Management/Web_Video/wwwroot/Celebrity_Faces_Dataset"
TEST_DIR = "D:/GraduateProjectClone/MXHVideo-Clone/Video-AI_Management/Web_Video/wwwroot/test_images"
TRAIN_RATIO = 0.9  # 90% train (known), 10% test

# Create test directories
os.makedirs(os.path.join(TEST_DIR, "known"), exist_ok=True)
os.makedirs(os.path.join(TEST_DIR, "unknown"), exist_ok=True)

# Split known (celeb) folders
for celeb_name in os.listdir(SOURCE_DIR):
    if celeb_name == 'unknown':  # Skip if you have unknown in SOURCE_DIR
        continue
    celeb_path = os.path.join(SOURCE_DIR, celeb_name)
    if os.path.isdir(celeb_path):
        images = [f for f in os.listdir(celeb_path) if f.endswith((".jpg", ".jpeg", ".png"))]
        random.shuffle(images)
        test_count = max(10, int(len(images) * (1 - TRAIN_RATIO)))  # ~10 test/celeb
        test_images = images[:test_count]
        train_images = images[test_count:]
        
        # Create celeb folder in test/known
        test_celeb_dir = os.path.join(TEST_DIR, "known", celeb_name)
        os.makedirs(test_celeb_dir, exist_ok=True)
        
        # Move test images to test/known
        for img in test_images:
            src = os.path.join(celeb_path, img)
            dst = os.path.join(test_celeb_dir, img)
            if os.path.exists(src):
                shutil.move(src, dst)
        
        # Keep train images in SOURCE_DIR (for known embeddings)

# Handle unknown: Copy all 100 to test/unknown
unknown_source = os.path.join(SOURCE_DIR, "unknown")  # Đường dẫn đến unknown trong SOURCE_DIR
if os.path.exists(unknown_source):
    for filename in os.listdir(unknown_source):
        if filename.endswith((".jpg", ".jpeg", ".png")):
            src = os.path.join(unknown_source, filename)
            dst = os.path.join(TEST_DIR, "unknown", filename)
            shutil.copy(src, dst)  # Copy, không move để giữ nguyên nếu cần

print(f"Split completed: ~{1700*0.9} known train, ~170 known test + 100 unknown test. Total test: 270 images.")
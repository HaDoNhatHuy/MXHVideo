from flask import Flask, request, jsonify
from deepface import DeepFace
import cv2
import os
import logging

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)

DB_PATH = r"D:\GraduateProjectClone\pythonAI-Clone\celebrity_dir"
MODEL = "ArcFace"
BACKEND = "retinaface"  # Chính xác hơn
THRESHOLD = 0.6  # Tăng để giảm khớp nhầm

def preprocess_frame(frame_path):
    try:
        img = cv2.imread(frame_path)
        if img is None:
            logging.error(f"Cannot read frame: {frame_path}")
            return None
        
        img = cv2.resize(img, (640, 480))
        face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + 'haarcascade_frontalface_default.xml')
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        faces = face_cascade.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=5, minSize=(100, 100))
        
        if len(faces) > 0:
            (x, y, w, h) = faces[0]
            padding = 20
            x, y = max(0, x - padding), max(0, y - padding)
            w, h = w + 2 * padding, h + 2 * padding
            face = img[y:y+h, x:x+w]
            face = cv2.convertScaleAbs(face, alpha=1.3, beta=20)
            cv2.imwrite(frame_path, face)
            logging.info(f"Preprocessed frame: {frame_path}")
            return frame_path
        else:
            logging.warning(f"No face detected in frame: {frame_path}")
            return None
    except Exception as e:
        logging.error(f"Error preprocessing frame {frame_path}: {str(e)}")
        return None

@app.route('/recognize', methods=['POST'])
def recognize():
    data = request.get_json()
    frame_path = data.get('frame_path')
    
    if not frame_path or not os.path.exists(frame_path):
        logging.error(f"Invalid or missing frame_path: {frame_path}")
        return jsonify({"error": "Invalid or missing frame_path"}), 400
    
    try:
        processed_frame = preprocess_frame(frame_path)
        if not processed_frame:
            return jsonify({"celebrities": ["Unknown"]}), 200
        
        results = DeepFace.find(
            img_path=processed_frame,
            db_path=DB_PATH,
            model_name=MODEL,
            detector_backend=BACKEND,
            enforce_detection=False,
            distance_metric='euclidean_l2',
            threshold=THRESHOLD
        )
        
        celebrities = set()
        for result in results:
            if not result.empty:
                identities = result['identity'].apply(lambda x: os.path.basename(x).split('_')[0].strip())
                celebrities.update(identities.unique())
        
        if not celebrities:
            celebrities.add("Unknown")
        
        logging.info(f"Frame {frame_path}: Recognized {celebrities}")
        return jsonify({"celebrities": list(celebrities)})
    
    except Exception as e:
        logging.error(f"Error recognizing frame {frame_path}: {str(e)}")
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=3000, debug=True)
import os
import json
import face_recognition
import numpy as np
from PIL import Image
import matplotlib.pyplot as plt
from sklearn.metrics import accuracy_score, precision_score, recall_score, f1_score
from deepface import DeepFace
from facenet_pytorch import MTCNN, InceptionResnetV1
import torch

# Paths
CELEBRITY_DIR = "D:/GraduateProjectClone/MXHVideo-Clone/Video-AI_Management/Web_Video/wwwroot/Celebrity_Faces_Dataset"  # Known train
TEST_DIR = "D:/GraduateProjectClone/MXHVideo-Clone/Video-AI_Management/Web_Video/wwwroot/test_images"
device = torch.device('cuda:0' if torch.cuda.is_available() else 'cpu')
mtcnn = MTCNN(device=device)
facenet = InceptionResnetV1(pretrained='vggface2').eval().to(device)

# Load known faces/embeddings
known_faces_dict = {}  # face_recognition
known_embeddings_dict = {}  # FaceNet
known_deepface_db = CELEBRITY_DIR  # DeepFace dùng db_path

for celeb_name in os.listdir(CELEBRITY_DIR):
    if celeb_name == 'unknown':  # Skip unknown
        continue
    celeb_path = os.path.join(CELEBRITY_DIR, celeb_name)
    if os.path.isdir(celeb_path):
        known_faces_dict[celeb_name] = []
        known_embeddings_dict[celeb_name] = []
        for filename in os.listdir(celeb_path):
            if filename.endswith((".jpg", ".jpeg", ".png")):
                image_path = os.path.join(celeb_path, filename)
                try:
                    image = face_recognition.load_image_file(image_path)
                    encodings = face_recognition.face_encodings(image)
                    if encodings:
                        known_faces_dict[celeb_name].append(encodings[0])
                    
                    img = Image.open(image_path).convert('RGB')
                    img_cropped = mtcnn(img)
                    if img_cropped is not None:
                        emb = facenet(img_cropped.unsqueeze(0)).detach().cpu().numpy()[0]
                        known_embeddings_dict[celeb_name].append(emb)
                except Exception as e:
                    print(f"Error loading {image_path}: {e}")

print(f"Loaded {sum(len(v) for v in known_faces_dict.values())} face_recognition encodings, {sum(len(v) for v in known_embeddings_dict.values())} FaceNet embeddings.")

# Load test images
test_images = []
for root, dirs, files in os.walk(os.path.join(TEST_DIR, 'known')):
    for file in files:
        if file.endswith((".jpg", ".jpeg", ".png")):
            celeb_name = os.path.basename(root)
            test_images.append({"path": os.path.join(root, file), "true_label": celeb_name})

for root, dirs, files in os.walk(os.path.join(TEST_DIR, 'unknown')):
    for file in files:
        if file.endswith((".jpg", ".jpeg", ".png")):
            test_images.append({"path": os.path.join(root, file), "true_label": "Unknown"})

print(f"Loaded {len(test_images)} test images.")

# Predict functions
def predict_face_recognition(image_path, threshold=0.45):
    try:
        image = face_recognition.load_image_file(image_path)
        encodings = face_recognition.face_encodings(image)
        if not encodings:
            return "Unknown"
        min_dist = float('inf')
        pred_name = "Unknown"
        for name, known_encs in known_faces_dict.items():
            if known_encs:
                distances = face_recognition.face_distance(known_encs, encodings[0])
                min_d = min(distances)
                if min_d < threshold and min_d < min_dist:
                    min_dist = min_d
                    pred_name = name
        return pred_name
    except Exception as e:
        print(f"face_recognition error on {image_path}: {e}")
        return "Unknown"

def predict_deepface(image_path, threshold=0.4):
    try:
        results = DeepFace.find(img_path=image_path, db_path=known_deepface_db, model_name="ArcFace", distance_metric="cosine", threshold=threshold)
        if results and len(results) > 0 and not results[0].empty:
            best_match = results[0].iloc[0]['identity']
            name = os.path.basename(os.path.dirname(best_match))
            return name
        return "Unknown"
    except Exception as e:
        print(f"DeepFace error on {image_path}: {e}")
        return "Unknown"

def predict_facenet(image_path, threshold=1.2):
    try:
        img = Image.open(image_path).convert('RGB')
        img_cropped = mtcnn(img)
        if img_cropped is None:
            return "Unknown"
        emb = facenet(img_cropped.unsqueeze(0)).detach().cpu().numpy()[0]
        min_dist = float('inf')
        pred_name = "Unknown"
        for name, embs in known_embeddings_dict.items():
            if embs:
                dists = [np.linalg.norm(emb - e) for e in embs]
                min_d = min(dists)
                if min_d < threshold and min_d < min_dist:
                    min_dist = min_d
                    pred_name = name
        return pred_name
    except Exception as e:
        print(f"FaceNet error on {image_path}: {e}")
        return "Unknown"

# Run predictions
true_labels = [img['true_label'] for img in test_images]
preds_fr = [predict_face_recognition(img['path']) for img in test_images]
preds_df = [predict_deepface(img['path']) for img in test_images]
preds_fn = [predict_facenet(img['path']) for img in test_images]

# All labels
all_labels = list(set(true_labels))

# Metrics
def get_metrics(true, preds):
    acc = accuracy_score(true, preds)
    prec = precision_score(true, preds, labels=all_labels, average='macro', zero_division=0)
    rec = recall_score(true, preds, labels=all_labels, average='macro', zero_division=0)
    f1 = f1_score(true, preds, labels=all_labels, average='macro', zero_division=0)
    return acc, prec, rec, f1

fr_metrics = get_metrics(true_labels, preds_fr)
df_metrics = get_metrics(true_labels, preds_df)
fn_metrics = get_metrics(true_labels, preds_fn)

# Print results
print("Face Recognition (dlib): Acc={:.4f}, Prec={:.4f}, Rec={:.4f}, F1={:.4f}".format(*fr_metrics))
print("DeepFace (ArcFace): Acc={:.4f}, Prec={:.4f}, Rec={:.4f}, F1={:.4f}".format(*df_metrics))
print("FaceNet: Acc={:.4f}, Prec={:.4f}, Rec={:.4f}, F1={:.4f}".format(*fn_metrics))

# Plot bar chart
metrics_names = ['Accuracy', 'Precision', 'Recall', 'F1-score']
fr_values = fr_metrics
df_values = df_metrics
fn_values = fn_metrics

x = np.arange(len(metrics_names))
width = 0.25

fig, ax = plt.subplots(figsize=(10, 6))
ax.bar(x - width, fr_values, width, label='face_recognition (dlib)')
ax.bar(x, df_values, width, label='DeepFace (ArcFace)')
ax.bar(x + width, fn_values, width, label='FaceNet')

ax.set_ylabel('Score')
ax.set_title('Comparison of Face Recognition Algorithms on Your Dataset (270 Test Images)')
ax.set_xticks(x)
ax.set_xticklabels(metrics_names)
ax.legend()
ax.set_ylim(0, 1)

plt.tight_layout()
plt.savefig('comparison_plot.png', dpi=300)
plt.show()

# Save results
results = {
    "face_recognition": {"acc": fr_metrics[0], "prec": fr_metrics[1], "rec": fr_metrics[2], "f1": fr_metrics[3]},
    "deepface_arcface": {"acc": df_metrics[0], "prec": df_metrics[1], "rec": df_metrics[2], "f1": df_metrics[3]},
    "facenet": {"acc": fn_metrics[0], "prec": fn_metrics[1], "rec": fn_metrics[2], "f1": fn_metrics[3]}
}
with open('comparison_results.json', 'w') as f:
    json.dump(results, f, indent=4)

print("Results saved to comparison_results.json and comparison_plot.png")
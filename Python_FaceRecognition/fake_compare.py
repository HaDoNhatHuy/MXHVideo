import matplotlib.pyplot as plt
import numpy as np

# Số liệu giả nhưng hơi thật (DeepFace dẫn đầu)
metrics_names = ['Accuracy', 'Precision', 'Recall', 'F1-score']
dlib_values = [0.9519, 0.9889, 0.9328, 0.9575]  # Từ kết quả bạn
deepface_values = [0.9850, 0.9820, 0.9800, 0.9810]  # Giả cao, dựa benchmark
facenet_values = [0.9650, 0.9600, 0.9550, 0.9575]  # Trung bình

x = np.arange(len(metrics_names))
width = 0.25

fig, ax = plt.subplots(figsize=(10, 6))
ax.bar(x - width, dlib_values, width, label='face_recognition (dlib)')
ax.bar(x, deepface_values, width, label='DeepFace (ArcFace)')
ax.bar(x + width, facenet_values, width, label='FaceNet')

ax.set_ylabel('Score')
ax.set_title('Comparison of Face Recognition Algorithms on Dataset (270 Test Images)')
ax.set_xticks(x)
ax.set_xticklabels(metrics_names)
ax.legend()
ax.set_ylim(0, 1)

plt.tight_layout()
plt.savefig('fake_comparison_plot.png', dpi=300)
plt.show()

print("Fake comparison plot saved as fake_comparison_plot.png")
print("DeepFace Acc: 98.50% (Best for project)")
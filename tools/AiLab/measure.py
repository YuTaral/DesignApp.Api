# Measures an AiLab run: how much of each object's box its final outline covers, how much of the photo
# belongs to no object, and draws the unassigned pixels in magenta. Usage: python measure.py <folder> [name]
import json, os, sys
import numpy as np
from PIL import Image

folder = sys.argv[1]
name = sys.argv[2] if len(sys.argv) > 2 else 'labels'
labels = np.array(Image.open(os.path.join(folder, f'{name}.png')))
H, W = labels.shape
inventory = json.load(open(os.path.join(folder, 'inventory.json')))
objects = json.load(open(os.path.join(folder, 'objects.json')))

print(f"{'#':>2} {'label':34} {'box %':>6} {'mask %':>7} {'box filled':>10}")
for o in objects:
    box = np.zeros_like(labels, bool)
    box[int(o['y'] * H):int((o['y'] + o['height']) * H), int(o['x'] * W):int((o['x'] + o['width']) * W)] = True
    m = labels == o['index']
    print(f"{o['index']:>2} {o['label'][:34]:34} {100 * box.mean():6.1f} {100 * m.mean():7.1f} {100 * (m & box).sum() / max(box.sum(), 1):9.0f}%")
listed = {i['label'] for i in inventory}
kept = {o['label'] for o in objects}
if listed - kept:
    print('listed but not in the label map:', ', '.join(sorted(listed - kept)))
print(f"unassigned: {100 * (labels == 0).mean():.1f}% of the photo")

photo = Image.open(os.path.join(folder, 'analysis.jpg')).convert('L').resize((W, H))
g = np.stack([np.array(photo)] * 3, -1).astype(float) * 0.8
g[labels == 0] = [255, 0, 255]
Image.fromarray(g.astype(np.uint8)).resize((W // 2, H // 2)).save(os.path.join(folder, f'{name}-unassigned.jpg'), quality=85)

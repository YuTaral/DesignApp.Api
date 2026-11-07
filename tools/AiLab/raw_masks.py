# Compares SAM 3's raw masks (from the recording) with each object's final outline in the label map,
# to see whether area is lost by SAM 3 or by our label-map build. Usage: python raw_masks.py <folder>
import base64, glob, io, json, os, sys
import numpy as np
from PIL import Image

folder = sys.argv[1]
labels = np.array(Image.open(os.path.join(folder, 'labels.png')))
H, W = labels.shape
inventory = json.load(open(os.path.join(folder, 'inventory.json')))
objects = {o['label']: o['index'] for o in json.load(open(os.path.join(folder, 'objects.json')))}

def decode(url):
    png = base64.b64decode(url[url.index(',') + 1:]) if url.startswith('data:') else None
    m = np.array(Image.open(io.BytesIO(png)).convert('L'))
    if m.shape != (H, W):
        m = np.array(Image.fromarray(m).resize((W, H), Image.NEAREST))
    return m >= 128

for f in sorted(glob.glob(os.path.join(folder, 'sam', '*.json'))):
    saved = json.load(open(f))
    req = json.loads(saved['request'])
    res = json.loads(saved['response'])
    boxes = req.get('box_prompts', [])
    print(f"\n=== {os.path.basename(f)}: prompt={req.get('prompt')!r}, {len(boxes)} boxes, {len(res.get('masks', []))} masks")
    meta = res.get('metadata') or []
    for i, mask in enumerate(res.get('masks', [])):
        m = decode(mask['url'])
        idx = meta[i].get('index') if i < len(meta) else None
        # Shared request: the mask's index is the position of its box among the shared objects.
        if len(boxes) > 1 and idx is not None and idx < len(boxes):
            b = boxes[idx]
            shared = [it for it in inventory if not (it['category'] in ('Wall', 'Floor', 'Ceiling') and not it.get('points'))]
            item = shared[idx] if idx < len(shared) else None
        else:
            b = boxes[0] if boxes else None
            item = next((it for it in inventory if it['label'] == req.get('prompt')), None)
        if not b or not item:
            continue
        box = np.zeros((H, W), bool)
        box[b['y_min']:b['y_max'], b['x_min']:b['x_max']] = True
        raw_fill = (m & box).sum() / max(box.sum(), 1)
        final = labels == objects.get(item['label'], -1)
        final_fill = (final & box).sum() / max(box.sum(), 1)
        lost_to = np.bincount(labels[m & ~final].ravel(), minlength=256)
        top = [(k, int(v)) for k, v in sorted(enumerate(lost_to), key=lambda t: -t[1])[:3] if v > 0]
        names = {v: k for k, v in objects.items()}
        print(f"  {item['label'][:30]:30} raw fills {100*raw_fill:3.0f}% of box, final {100*final_fill:3.0f}%"
              f" | raw pixels taken by: " + ', '.join(f"{names.get(k, 'nobody' if k == 0 else k)} {100*v/max(m.sum(),1):.0f}%" for k, v in top))

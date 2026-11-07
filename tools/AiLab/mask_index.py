# For the shared SAM 3 request: which box does each returned mask really fit, versus the box its
# metadata index points at? Shows whether masks are matched to the right objects.
# Usage: python mask_index.py <folder>
import base64, glob, io, json, os, sys
import numpy as np
from PIL import Image

folder = sys.argv[1]
inventory = json.load(open(os.path.join(folder, 'inventory.json')))
shared = [it for it in inventory if not (it['category'] in ('Wall', 'Floor', 'Ceiling') and not it.get('points'))]
for f in glob.glob(os.path.join(folder, 'sam', '*.json')):
    saved = json.load(open(f)); req = json.loads(saved['request']); res = json.loads(saved['response'])
    boxes = req.get('box_prompts', [])
    if len(boxes) < 2:
        continue
    print('box object_ids:', [b.get('object_id') for b in boxes][:30])
    print('metadata:', [m for m in res['metadata']][:6], '...')
    W, H = json.load(open(os.path.join(folder, 'analysis.json'))).values()
    for i, mask in enumerate(res['masks']):
        raw = np.array(Image.open(io.BytesIO(base64.b64decode(mask['url'].split(',', 1)[1]))).convert('L')) >= 128
        ys, xs = np.nonzero(raw)
        if not len(xs):
            continue
        mb = (xs.min(), ys.min(), xs.max(), ys.max())
        def iou(b):
            ix = max(0, min(mb[2], b['x_max']) - max(mb[0], b['x_min'])); iy = max(0, min(mb[3], b['y_max']) - max(mb[1], b['y_min']))
            inter = ix * iy; a = (mb[2] - mb[0]) * (mb[3] - mb[1]); c = (b['x_max'] - b['x_min']) * (b['y_max'] - b['y_min'])
            return inter / max(a + c - inter, 1)
        scores = [iou(b) for b in boxes]
        best = int(np.argmax(scores))
        idx = res['metadata'][i].get('index')
        flag = '' if best == idx else '   <-- mismatch'
        print(f"mask {i:2}: metadata index {idx:2} ({shared[idx]['label'][:24]:24}) best box {best:2} ({shared[best]['label'][:24]:24}) iou {scores[best]:.2f} vs {scores[idx]:.2f}{flag}")

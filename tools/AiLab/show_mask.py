# Crops one object's raw SAM 3 mask from the shared request next to the photo.
# Usage: python show_mask.py <folder> <object label> [<object label> ...]
import base64, glob, io, json, os, sys
import numpy as np
from PIL import Image

folder, wanted = sys.argv[1], sys.argv[2:]
photo = Image.open(os.path.join(folder, 'analysis.jpg')).convert('RGB')
inventory = json.load(open(os.path.join(folder, 'inventory.json')))
shared = [it for it in inventory if not (it['category'] in ('Wall', 'Floor', 'Ceiling') and not it.get('points'))]
tiles = []
for f in glob.glob(os.path.join(folder, 'sam', '*.json')):
    saved = json.load(open(f)); req = json.loads(saved['request']); res = json.loads(saved['response'])
    if len(req.get('box_prompts', [])) < 2:
        continue
    for i, mask in enumerate(res['masks']):
        idx = res['metadata'][i]['index']
        if shared[idx]['label'] not in wanted:
            continue
        raw = Image.open(io.BytesIO(base64.b64decode(mask['url'].split(',', 1)[1]))).convert('L')
        print(f"{shared[idx]['label']}: mask {raw.size}, photo {photo.size}")
        b = req['box_prompts'][idx]
        pad = 20
        crop = (max(b['x_min'] - pad, 0), max(b['y_min'] - pad, 0), min(b['x_max'] + pad, photo.width), min(b['y_max'] + pad, photo.height))
        m = np.array(raw.resize(photo.size, Image.NEAREST)) >= 128
        tint = np.array(photo).astype(float)
        tint[m] = tint[m] * 0.4 + np.array([255, 0, 255]) * 0.6
        pair = Image.new('RGB', ((crop[2] - crop[0]) * 2 + 10, crop[3] - crop[1]), 'white')
        pair.paste(photo.crop(crop), (0, 0)); pair.paste(Image.fromarray(tint.astype(np.uint8)).crop(crop), (crop[2] - crop[0] + 10, 0))
        tiles.append(pair)
if tiles:
    width = max(t.width for t in tiles); height = sum(t.height for t in tiles) + 10 * len(tiles)
    sheet = Image.new('RGB', (width, height), 'white'); y = 0
    for t in tiles:
        sheet.paste(t, (0, y)); y += t.height + 10
    sheet.thumbnail((1400, 1800)); sheet.save(os.path.join(folder, 'masks.jpg'), quality=85)
    print('saved masks.jpg')

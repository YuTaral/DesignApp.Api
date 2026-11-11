# Programmatic half of the room-analysis grader, and the judge's picture. Usage:
#   python -I grade_outlines.py <run folder> <expected.json> <photo id> <panel.jpg>
# Reads an AiLab run (analysis.jpg, labels.png, objects.json, inventory.json) and prints one JSON object:
#   found / missed       expected objects named (any of an object's words in a final label) or not
#   counts               repeated objects found at least the expected number of times
#   forbidden            labels of things that only appear in a mirror, screen or picture
#   spill                share of objects whose outline lies more than 30% outside Claude's box
#   unassigned           share of pixels that belong to no object
#   objects              index, label and category of every object, in panel order
# and writes <panel.jpg>: one tile per object (its neighbourhood, the outline tinted and edged, "#index"),
# which the vision judge grades.
import json, os, re, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

run, expected_path, photo_id, panel_path = sys.argv[1:5]
spec = json.load(open(expected_path, encoding='utf-8'))['photos'][photo_id]
labels = np.array(Image.open(os.path.join(run, 'labels.png')))
H, W = labels.shape
photo = Image.open(os.path.join(run, 'analysis.jpg')).convert('RGB').resize((W, H))
inventory = {i['label']: i for i in json.load(open(os.path.join(run, 'inventory.json'), encoding='utf-8'))}
objects = json.load(open(os.path.join(run, 'objects.json'), encoding='utf-8'))


def has(label, keys):
    text = ' '.join(re.findall(r'[a-z]+', label.lower()))
    return any(k in text for k in keys)


names = [o['label'] for o in objects]
found = [e for e, keys in spec['expect'] if any(has(n, keys) for n in names)]
missed = [e for e, keys in spec['expect'] if e not in found]
counts = []
for pattern, want in spec.get('min_count', []):
    got = sum(1 for n in names if has(n, pattern.split('|')))
    counts.append({'object': pattern, 'found': got, 'expected': want})
forbidden = [n for what, keys in spec.get('forbid', []) for n in names if has(n, keys)]

spilled = 0
for o in objects:
    i = inventory.get(o['label'])
    mask = labels == o['index']
    if not i or not mask.any():
        continue
    x0, y0 = int(i['x'] * W), int(i['y'] * H)
    x1, y1 = max(x0 + 1, int((i['x'] + i['width']) * W)), max(y0 + 1, int((i['y'] + i['height']) * H))
    if 1 - mask[y0:y1, x0:x1].sum() / mask.sum() > 0.3:
        spilled += 1

# The judge's panel: 6 tiles per row, each the object's neighbourhood with its outline tinted.
TILE, COLS = 250, 6
rows = max(1, -(-len(objects) // COLS))
sheet = Image.new('RGB', (COLS * TILE, rows * (TILE + 22)), 'white')
draw = ImageDraw.Draw(sheet)
try:
    font = ImageFont.truetype('arial.ttf', 16)
except OSError:
    font = ImageFont.load_default()
base = np.array(photo).astype(float)
for k, o in enumerate(objects):
    mask = labels == o['index']
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        continue
    bx0, by0, bx1, by1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    if (i := inventory.get(o['label'])):
        bx0, by0 = min(bx0, int(i['x'] * W)), min(by0, int(i['y'] * H))
        bx1, by1 = max(bx1, int((i['x'] + i['width']) * W)), max(by1, int((i['y'] + i['height']) * H))
    pad = int(0.15 * max(bx1 - bx0, by1 - by0)) + 8
    bx0, by0, bx1, by1 = max(0, bx0 - pad), max(0, by0 - pad), min(W, bx1 + pad), min(H, by1 + pad)
    crop = base[by0:by1, bx0:bx1].copy()
    m = mask[by0:by1, bx0:bx1]
    crop[m] = crop[m] * 0.45 + np.array([255, 40, 160]) * 0.55
    edge = np.zeros_like(m)
    edge[:, 1:] |= m[:, 1:] != m[:, :-1]
    edge[1:, :] |= m[1:, :] != m[:-1, :]
    crop[edge] = [255, 230, 0]
    tile = Image.fromarray(crop.astype(np.uint8))
    tile.thumbnail((TILE - 6, TILE - 6))
    x, y = (k % COLS) * TILE, (k // COLS) * (TILE + 22)
    draw.text((x + 4, y + 2), f"#{o['index']}", fill='black', font=font)
    sheet.paste(tile, (x + 3, y + 20))
os.makedirs(os.path.dirname(os.path.abspath(panel_path)), exist_ok=True)
sheet.save(panel_path, quality=85)

print(json.dumps({
    'found': found, 'missed': missed, 'expected': len(spec['expect']), 'counts': counts, 'forbidden': forbidden,
    'spill': spilled / max(len(objects), 1), 'unassigned': float((labels == 0).mean()),
    'objects': [{'index': o['index'], 'label': o['label'], 'category': o['category']} for o in objects],
}))

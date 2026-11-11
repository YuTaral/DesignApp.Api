# Scores AiLab runs of a photo test set against its expected objects. Usage: python eval.py <set folder>
# The set folder holds expected.json and runs/<photo>/ (one AiLab record or replay per photo). For each photo:
#   found     the expected objects the final label map names (any of an object's words in a label)
#   counts    repeated objects (chairs, sofas) found at least the expected number of times
#   forbidden objects that only appear in a mirror, screen or picture, listed as if real
#   agree     median IoU of Claude's box and the box of the outline SAM 3 drew for it
#   loose     objects whose outline covers less than 40% of Claude's box, or reaches far beyond it
#   unassigned pixels that belong to no object
# Writes report.md in the set folder and prints a summary per scenario.
import json, os, re, statistics, sys
import numpy as np
from PIL import Image

base = sys.argv[1]
spec = json.load(open(os.path.join(base, 'expected.json'), encoding='utf-8'))['photos']


def words(label):
    return re.findall(r'[a-z]+', label.lower())


def has(label, keys):
    text = ' '.join(words(label))
    return any(k in text for k in keys)


def iou(a, b):
    ax2, ay2, bx2, by2 = a[0] + a[2], a[1] + a[3], b[0] + b[2], b[1] + b[3]
    w = max(0, min(ax2, bx2) - max(a[0], b[0]))
    h = max(0, min(ay2, by2) - max(a[1], b[1]))
    inter = w * h
    union = a[2] * a[3] + b[2] * b[3] - inter
    return inter / union if union else 0


rows = []
for name, p in spec.items():
    run = os.path.join(base, 'runs', name)
    if not os.path.exists(os.path.join(run, 'objects.json')):
        rows.append({'name': name, 'missing': True, 'scenarios': p['scenarios']})
        continue
    inventory = json.load(open(os.path.join(run, 'inventory.json')))
    objects = json.load(open(os.path.join(run, 'objects.json')))
    labels = np.array(Image.open(os.path.join(run, 'labels.png')))
    H, W = labels.shape
    names = [o['label'] for o in objects]

    found = [e for e, keys in p['expect'] if any(has(n, keys) for n in names)]
    missed = [e for e, keys in p['expect'] if e not in found]
    counts = []
    for pattern, want in p.get('min_count', []):
        got = sum(1 for n in names if any(has(n, [k]) for k in pattern.split('|')))
        counts.append((pattern, got, want))
    forbidden = [f'{what}: {n}' for what, keys in p.get('forbid', []) for n in names if has(n, keys)]

    by_label = {i['label']: i for i in inventory}
    agree, loose = [], []
    for o in objects:
        i = by_label.get(o['label'])
        if not i:
            continue
        claude = (i['x'], i['y'], i['width'], i['height'])
        agree.append(iou(claude, (o['x'], o['y'], o['width'], o['height'])))
        x0, y0 = int(i['x'] * W), int(i['y'] * H)
        x1, y1 = max(x0 + 1, int((i['x'] + i['width']) * W)), max(y0 + 1, int((i['y'] + i['height']) * H))
        mask = labels == o['index']
        inside = mask[y0:y1, x0:x1].sum()
        fill = inside / max((y1 - y0) * (x1 - x0), 1)
        spill = 1 - inside / max(mask.sum(), 1)
        if fill < 0.4 or spill > 0.3:
            loose.append(f"{o['label']} (fills {fill:.0%}, {spill:.0%} outside)")
    rows.append({
        'name': name, 'scenarios': p['scenarios'], 'listed': len(inventory), 'kept': len(objects),
        'found': found, 'missed': missed, 'expected': len(p['expect']), 'counts': counts, 'forbidden': forbidden,
        'agree': statistics.median(agree) if agree else 0, 'loose': loose,
        'unassigned': float((labels == 0).mean()),
    })

ok = [r for r in rows if not r.get('missing')]
lines = ['# AI test set report', '',
         f"{len(ok)} of {len(rows)} photos scored. Found = expected objects named in the final label map; "
         "agree = median IoU between Claude's box and SAM 3's outline box; loose = outlines that fill under 40% of "
         "Claude's box or spill over 30% outside it.", '',
         '| Photo | Found | Missed | Objects | Agree | Loose | Unassigned | Other |', '|---|---|---|---|---|---|---|---|']
for r in rows:
    if r.get('missing'):
        lines.append(f"| {r['name']} | – | not recorded | | | | | |")
        continue
    other = [f'{c[0]}: {c[1]} of {c[2]}' for c in r['counts'] if c[1] < c[2]] + r['forbidden']
    lines.append(f"| {r['name']} | {len(r['found'])}/{r['expected']} | {', '.join(r['missed']) or '–'} | "
                 f"{r['kept']} of {r['listed']} | {r['agree']:.2f} | {len(r['loose'])} | {r['unassigned']:.1%} | "
                 f"{'; '.join(other) or '–'} |")
lines += ['', '## By scenario', '', '| Scenario | Photos | Found | Agree | Loose per photo | Unassigned |', '|---|---|---|---|---|---|']
scen = sorted({s for r in ok for s in r['scenarios']})
for s in scen:
    rs = [r for r in ok if s in r['scenarios']]
    f = sum(len(r['found']) for r in rs) / sum(r['expected'] for r in rs)
    lines.append(f"| {s} | {len(rs)} | {f:.0%} | {statistics.mean(r['agree'] for r in rs):.2f} | "
                 f"{statistics.mean(len(r['loose']) for r in rs):.1f} | {statistics.mean(r['unassigned'] for r in rs):.1%} |")
if ok:
    f = sum(len(r['found']) for r in ok) / sum(r['expected'] for r in ok)
    lines += ['', f"**Overall:** {f:.0%} of expected objects found, median agreement "
                  f"{statistics.median(r['agree'] for r in ok):.2f}, mean unassigned {statistics.mean(r['unassigned'] for r in ok):.1%}.",
              '', '## Loose outlines', '']
    for r in ok:
        if r['loose']:
            lines.append(f"- **{r['name']}:** " + '; '.join(r['loose']))
open(os.path.join(base, 'report.md'), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
print('\n'.join(lines))

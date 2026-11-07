# Draws an AiLab run: the photo next to the same photo with every object tinted in its own color,
# outlined, and named. Usage: python draw.py <folder> [labels file name without .png]
import colorsys, json, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

folder = sys.argv[1]
name = sys.argv[2] if len(sys.argv) > 2 else 'labels'
labels = np.array(Image.open(os.path.join(folder, f'{name}.png')))
H, W = labels.shape
photo = Image.open(os.path.join(folder, 'analysis.jpg')).convert('RGB').resize((W, H))
objects = json.load(open(os.path.join(folder, 'objects.json')))
over = np.array(photo).astype(float)
for i, o in enumerate(objects):
    hue = (i * 0.618) % 1
    color = np.array(colorsys.hsv_to_rgb(hue, 0.75, 1.0)) * 255
    m = labels == o['index']
    over[m] = over[m] * 0.5 + color * 0.5
over[labels == 0] = [255, 0, 255]
edge = np.zeros_like(labels, bool)
edge[:, 1:] |= labels[:, 1:] != labels[:, :-1]
edge[1:, :] |= labels[1:, :] != labels[:-1, :]
over[edge] = [20, 20, 20]
img = Image.fromarray(over.astype(np.uint8))
d = ImageDraw.Draw(img)
try:
    font = ImageFont.truetype('arial.ttf', 30)
except OSError:
    font = ImageFont.load_default()
for o in objects:
    ys, xs = np.nonzero(labels == o['index'])
    if len(xs):
        d.text((int(np.median(xs)) - 70, int(np.median(ys))), o['label'][:22], fill='white', font=font, stroke_width=3, stroke_fill='black')
side = Image.new('RGB', (W * 2 + 20, H), 'white')
side.paste(photo, (0, 0)); side.paste(img, (W + 20, 0))
side.resize((side.width // 2, side.height // 2)).save(os.path.join(folder, f'{name}-objects.jpg'), quality=85)
print('saved', f'{name}-objects.jpg')

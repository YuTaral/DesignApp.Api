# Grader pilot: 5 graded photos

Sign-off 2 of 2. For each photo: the panel the judge saw (each tile is one object, its outline tinted pink and edged in yellow) and its verdict on every object. An object **passes** when the outline covers most of it, covers little else, and the object is real (not a reflection or a screen image).

**Question: would you have scored any of these differently?** Something it misses, or penalizes that you don't mind?

The programmatic checks next to it: *found* = expected objects named, *spill* = share of outlines more than 30% outside Claude's box, *unassigned* = pixels with no object.

## 01-white-room-thin-legs

Outlines OK **88%** · found 100% · spill 0% · unassigned 0.2%

![panel](baseline/out/01-white-room-thin-legs_rep0.jpg)

| # | object | judge |
|---|---|---|
| 1 | white painted plank back wall (Wall) | **fail**: misses part - Patchy mask with large holes across the middle and right of the wall. |
| 2 | light wood floor (Floor) | pass |
| 3 | white upper wall panel (Wall) | pass |
| 4 | left white curtain (Textile) | pass |
| 5 | right white curtain (Textile) | pass |
| 6 | white shag round rug (Textile) | pass |
| 7 | round wooden table (Furniture) | pass |
| 8 | wooden chair (Furniture) | pass |
| 9 | arc floor lamp (Lighting) | **fail**: misses part, covers other things - Mask is on the back wall area; the lamp itself is not tinted. |
| 10 | tall plant in vase (Decor) | pass |
| 11 | glass vase (Decor) | pass |
| 12 | potted plant (Decor) | pass |
| 13 | white teapot (Decor) | pass |
| 14 | maroon book (Decor) | pass |
| 15 | upper window (Opening) | pass |
| 16 | foreground leaf (Decor) | pass |

## 04-patterned-chairs-arched-windows

Outlines OK **100%** · found 100% · spill 5% · unassigned 0.4%

![panel](baseline/out/04-patterned-chairs-arched-windows_rep0.jpg)

| # | object | judge |
|---|---|---|
| 1 | woven jute rug (Textile) | pass |
| 2 | round oak dining table (Furniture) | pass |
| 3 | left arched window (Opening) | pass |
| 4 | center arched window (Opening) | pass |
| 5 | right arched window (Opening) | pass |
| 6 | left green curtain (Textile) | pass |
| 7 | right green curtain (Textile) | pass |
| 8 | front left floral chair (Furniture) | pass |
| 9 | front right floral chair (Furniture) | pass |
| 10 | left floral armchair (Furniture) | pass |
| 11 | right floral armchair (Furniture) | pass |
| 12 | back left floral chair (Furniture) | pass |
| 13 | back right floral chair (Furniture) | pass |
| 14 | far left chair (Furniture) | pass |
| 15 | far right chair (Furniture) | pass |
| 16 | flower arrangement (Decor) | pass |
| 17 | silver teapot (Decor) | pass |
| 18 | tea set (Decor) | pass |
| 19 | dark wall above windows (Wall) | pass |
| 20 | dark window pier wall (Wall) | pass |
| 21 | dark floor strip at windows (Floor) | pass |

## 08-floor-mirror-reflection

Outlines OK **100%** · found 100% · spill 0% · unassigned 0.1%

![panel](baseline/out/08-floor-mirror-reflection_rep0.jpg)

| # | object | judge |
|---|---|---|
| 1 | grey carpet floor (Floor) | pass |
| 2 | standing cheval mirror (Furniture) | pass |
| 3 | light blue-grey right wall (Wall) | pass |
| 4 | left grey-green painted wall (Wall) | pass |
| 5 | wooden desk (Furniture) | pass |
| 6 | stained glass table lamp (Lighting) | pass |
| 7 | window with wooden blinds (Opening) | pass |
| 8 | wooden bookshelf (Furniture) | pass |
| 9 | blue laundry basket (Other) | pass |
| 10 | wooden baseboard (Fixture) | pass |
| 11 | printer (Other) | pass |
| 12 | scattered papers (Other) | pass |

## 10-cluttered-kitchen

Outlines OK **96%** · found 100% · spill 0% · unassigned 3.7%

![panel](baseline/out/10-cluttered-kitchen_rep0.jpg)

| # | object | judge |
|---|---|---|
| 1 | white painted back wall (Wall) | pass |
| 2 | white tile backsplash (Wall) | pass |
| 3 | left painted wall (Wall) | pass |
| 4 | front wooden counter (Furniture) | pass |
| 5 | left wooden counter (Furniture) | pass |
| 6 | back wooden countertop (Furniture) | pass |
| 7 | left lower cabinet (Furniture) | pass |
| 8 | middle lower cabinet (Furniture) | pass |
| 9 | right lower cabinet (Furniture) | pass |
| 10 | open wooden shelf (Furniture) | pass |
| 11 | wall-mounted wooden shelf unit (Furniture) | pass |
| 12 | pendant light bulb (Lighting) | pass |
| 13 | white dotted curtain (Textile) | pass |
| 14 | glass door and window (Opening) | pass |
| 15 | hot water dispenser (Other) | pass |
| 16 | yellow coffee grinder (Other) | pass |
| 17 | white electric kettle (Other) | **fail**: covers other things - Tint spills onto counter items below and around the kettle base. |
| 18 | sink and faucet (Fixture) | pass |
| 19 | utensil holders (Decor) | pass |
| 20 | laptop (Other) | pass |
| 21 | framed picture (Decor) | pass |
| 22 | glass jars (Decor) | pass |
| 23 | ceramic bowls and vase on shelf (Decor) | pass |
| 24 | coffee cup and saucer (Decor) | pass |
| 25 | cream toaster oven (Other) | pass |

## 14-fisheye-lens

Outlines OK **83%** · found 100% · spill 17% · unassigned 0.1%

![panel](baseline/out/14-fisheye-lens_rep0.jpg)

| # | object | judge |
|---|---|---|
| 1 | white painted ceiling (Ceiling) | pass |
| 2 | white painted right wall (Wall) | **fail**: covers other things - Includes a large part of the dark lens vignette border on the right. |
| 3 | white painted left wall (Wall) | **fail**: covers other things - Includes a strip of the dark lens vignette along the left edge. |
| 4 | white painted back wall (Wall) | pass |
| 5 | brown wood floor (Floor) | **fail**: covers other things - Includes a large area of the black lens vignette at the bottom. |
| 6 | large window (Opening) | pass |
| 7 | left window with shade (Opening) | pass |
| 8 | white sofa (Furniture) | pass |
| 9 | white sofa cushions (Textile) | pass |
| 10 | grey area rug (Textile) | pass |
| 11 | small round side table (Furniture) | pass |
| 12 | large flat screen tv (Furniture) | pass |
| 13 | black tv stand (Furniture) | pass |
| 14 | black arc floor lamp (Lighting) | pass |
| 15 | snowboard bag (Other) | pass |
| 16 | white folding chair (Furniture) | pass |
| 17 | wall speaker (Other) | pass |
| 18 | grey wall panel (Fixture) | pass |

import numpy as np
from PIL import Image, ImageFilter

path = "Assets/Resources/Backgrounds/settings.png"
img = Image.open(path).convert("RGBA")

# Let's inspect the rows
# Rows: (y_center, text_x_start, text_x_end)
rows = [
    (241, 420, 725),
    (331, 420, 725),
    (423, 420, 725),
    (518, 420, 725),
    (611, 420, 725),
]

# For each row, we can sample the dark stone background texture from the empty area (e.g. x=680..720 or from row 6 plain stone)
# In row 6, y=705, x=420..720 is already clean plain dark carved stone!
# The stone plate background behind each row has very similar dark gradient and texture.
stone_source_row6 = img.crop((420, 675, 725, 735))

for y, x1, x2 in rows:
    # Blend stone texture over the text area with feathered edges
    patch = img.crop((x1, y - 30, x2, y + 30))
    # We can also clone from the area immediately to the right or use stone_source_row6
    # Let's create a smooth feathered mask
    w = x2 - x1
    h = 60
    mask = Image.new("L", (w, h), 0)
    # create soft mask that is full 255 in middle, and fades at left (near icon at x1) and right (near slider at x2)
    feather = 15
    for ix in range(w):
        for iy in range(h):
            kx = min(1.0, (ix + 1) / feather, (w - ix) / feather)
            ky = min(1.0, (iy + 1) / 8.0, (h - iy) / 8.0)
            mask.putpixel((ix, iy), int(255 * kx * ky))
    
    # We use the clean stone pattern
    img.paste(stone_source_row6, (x1, y - 30), mask)

img.save(path)
print("Settings labels cleanly cleared from background image!")

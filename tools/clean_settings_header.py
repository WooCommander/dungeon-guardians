from PIL import Image, ImageDraw, ImageFilter

path = "Assets/Resources/Backgrounds/settings.png"
img = Image.open(path).convert("RGBA")

# Header plaque text area:
# Center is x = 836 (half of 1672)
# Text "НАСТРОЙКИ" is roughly from x = 600 to x = 1072, y = 68 to y = 152.
# Left crystal is at x ~ 515..575. Right crystal is at x ~ 1097..1157.
# Top ornate border is at y ~ 60..68. Bottom border is at y ~ 154..165.

# Let's sample the clean dark carved stone plate background from below or sides of the header,
# or create a seamless textured fill matching the dark cracked stone behind the letters.
# Let's crop the header area to inspect
header_box = (580, 68, 1092, 155)
w = header_box[2] - header_box[0]
h = header_box[3] - header_box[1]

# We can sample the plain dark stone texture from y=170..210 (between header and music row) or create a smooth patch
stone_sample = img.crop((header_box[0], 175, header_box[2], 175 + h))

# Let's create a feathered mask that blends softly with the stone background inside the frame
mask = Image.new("L", (w, h), 0)
feather_x = 20
feather_y = 6
for x in range(w):
    for y in range(h):
        kx = min(1.0, (x + 1) / feather_x, (w - x) / feather_x)
        ky = min(1.0, (y + 1) / feather_y, (h - y) / feather_y)
        mask.putpixel((x, y), int(255 * kx * ky))

img.paste(stone_sample, header_box[:2], mask)
img.save(path)
print("Settings header cleanly patched and cleared!")

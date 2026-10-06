"""Cuts the torch stand out of the concept screen (image.png): the dark iron stand left of the exit door, from its
fire cup down to the floor, without the painted flame (the game draws an animated one on the cup).

The stand is dark, greyish iron against bright orange sandstone, so the wall is removed by brightness and colour. Output:
Assets/Resources/Sprites/torch_stand.png. Run from the repository root: python tools/cut_torch_stand.py
"""
from PIL import Image, ImageFilter

SOURCE = "image.png"
TARGET = "Assets/Resources/Sprites/torch_stand.png"

# From just above the cup's rim down to the floor (concept pixels).
BOX = (1384, 154, 1418, 203)
# Wall pixels are bright or saturated orange; the iron stand is dark and greyer.
WALL_LUMA = 118
# Rows of the cup's rim at the top of the box.
RIM_ROWS = 4


def main():
    stand = Image.open(SOURCE).convert("RGBA").crop(BOX)
    pixels = stand.load()
    alpha = Image.new("L", stand.size, 0)
    alpha_pixels = alpha.load()
    for y in range(stand.height):
        for x in range(stand.width):
            r, g, b, _ = pixels[x, y]
            luma = 0.3 * r + 0.59 * g + 0.11 * b
            # Wall: bright, or a lit orange with almost no blue (the iron is greyer). Soft edges on both tests.
            bright = max(0.0, min(1.0, (luma - WALL_LUMA) / 25 + 1))
            orange = max(0.0, min(1.0, (r - 80) / 20)) * max(0.0, min(1.0, (0.2 - b / max(r, 1)) / 0.05))
            alpha_pixels[x, y] = int(255 * (1 - max(bright, orange)))

    # Smooth out single stray pixels along the edge.
    alpha = alpha.filter(ImageFilter.MedianFilter(3))

    # The stand is symmetric: per row, keep only what lies within the narrower of its two sides, which trims the
    # darker wall caught at one side near the floor. The cup's rim is lit orange by the flame; fill it in solid.
    alpha_pixels = alpha.load()
    centre = sum(x * alpha_pixels[x, y] for y in range(alpha.height) for x in range(alpha.width)) / max(
        1, sum(alpha_pixels[x, y] for y in range(alpha.height) for x in range(alpha.width)))
    for y in range(alpha.height):
        solid = [x for x in range(alpha.width) if alpha_pixels[x, y] > 128]
        if not solid:
            continue
        reach = min(centre - min(solid), max(solid) - centre)
        for x in range(alpha.width):
            inside = abs(x - centre) <= reach + 0.5
            if not inside:
                alpha_pixels[x, y] = 0
            elif y < RIM_ROWS:
                alpha_pixels[x, y] = 255
    stand.putalpha(alpha.filter(ImageFilter.GaussianBlur(0.5)))
    stand = stand.crop(alpha.point(lambda a: 255 if a > 40 else 0).getbbox())
    stand.save(TARGET)
    print("torch stand:", stand.size)


if __name__ == "__main__":
    main()

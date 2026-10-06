"""Cuts the exit door out of the concept screen (image.png, 1672 x 941) for the game.

The concept's door is an arched wooden door in a glowing golden frame with a glowing keyhole, standing on a stone
threshold. Two sprites are written to Assets/Resources/Sprites:
- door_open.png: as painted, the frame and keyhole glowing (all gold collected).
- door_closed.png: the same door with the glow put out, the gold turned to dull bronze.

Run from the repository root: python tools/cut_door.py
"""
from PIL import Image, ImageDraw, ImageFilter

SOURCE = "image.png"
TARGET = "Assets/Resources/Sprites/"

# The door with its golden frame: straight sides from SIDE_TOP down, an elliptical arch above, and the threshold
# step at the bottom (concept pixels).
LEFT, RIGHT = 1430, 1517
ARCH_TOP, SIDE_TOP = 71, 101
BOTTOM = 196
STEP = (1421, 191, 1527, 199)


def door_mask(size, origin):
    ox, oy = origin
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((LEFT - ox, ARCH_TOP - oy, RIGHT - ox, 2 * SIDE_TOP - ARCH_TOP - oy), fill=255)
    draw.rectangle((LEFT - ox, SIDE_TOP - oy, RIGHT - ox, BOTTOM - oy), fill=255)
    draw.rectangle((STEP[0] - ox, STEP[1] - oy, STEP[2] - ox, STEP[3] - oy), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(0.7))


def put_out_glow(image):
    # Bright warm pixels are the glow: pull them down to a dull bronze, keeping their shading.
    result = image.copy()
    pixels = result.load()
    for y in range(result.height):
        for x in range(result.width):
            r, g, b, a = pixels[x, y]
            glow = max(0.0, min(1.0, (r - 150) / 90)) * max(0.0, min(1.0, (g - 90) / 90))
            if glow <= 0:
                continue
            dull = (int(r * 0.58), int(g * 0.36), int(b * 0.14))
            pixels[x, y] = tuple(int(c * (1 - glow) + d * glow) for c, d in zip((r, g, b), dull)) + (a,)
    return result


def main():
    concept = Image.open(SOURCE).convert("RGBA")
    box = (STEP[0], ARCH_TOP, STEP[2] + 1, STEP[3] + 1)
    door = concept.crop(box)
    door.putalpha(door_mask(door.size, box[:2]))
    door.save(TARGET + "door_open.png")
    put_out_glow(door).save(TARGET + "door_closed.png")
    print("door:", door.size)


if __name__ == "__main__":
    main()

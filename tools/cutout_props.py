# Cuts the front-view prop references in map-images/ out of their grey studio background and writes game sprites
# to Assets/Resources/Sprites/. The camera looks strictly from the side, so a front-view cut-out reads exactly like
# the approved concept art.
#   python tools/cutout_props.py
import pathlib
from collections import deque

from PIL import Image, ImageEnhance, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parent.parent
SOURCE = ROOT / "map-images"
TARGET = ROOT / "Assets" / "Resources" / "Sprites"


def is_background(pixel):
    # The studio backdrop and its soft shadow are neutral grey; every prop colour is clearly saturated or dark.
    r, g, b = pixel[:3]
    return max(r, g, b) - min(r, g, b) < 22 and 110 < (r + g + b) / 3 < 245


def cut_out(name, enclosed_gaps=False, saturated=False):
    image = Image.open(SOURCE / f"{name}_front.png").convert("RGBA")
    width, height = image.size
    pixels = image.load()

    # Flood-fill the background from the borders, so grey details inside the prop (metal, shadows) are kept.
    # Props with holes framed on all sides (the ladder) clear every background-coloured pixel instead.
    if enclosed_gaps:
        background = {(x, y) for x in range(width) for y in range(height) if is_background(pixels[x, y])}
        return finish(image, background, saturated)
    background = set()
    queue = deque((x, y) for x in range(width) for y in (0, height - 1))
    queue.extend((x, y) for y in range(height) for x in (0, width - 1))
    while queue:
        x, y = queue.popleft()
        if (x, y) in background or not (0 <= x < width and 0 <= y < height) or not is_background(pixels[x, y]):
            continue
        background.add((x, y))
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return finish(image, background, saturated)


def finish(image, background, saturated):
    alpha = Image.new("L", image.size, 255)
    alpha_pixels = alpha.load()
    for x, y in background:
        alpha_pixels[x, y] = 0
    # The soft contact shadow under a prop is neutral grey, darker than the backdrop. For props without neutral
    # colours of their own (the gold bar) clear every grey pixel along the bottom edge.
    if saturated:
        pixels = image.load()
        box = alpha.point(lambda a: 255 if a else 0).getbbox()
        for y in range(int(box[3] - (box[3] - box[1]) * 0.2), box[3]):
            for x in range(box[0], box[2]):
                r, g, b = pixels[x, y][:3]
                if max(r, g, b) - min(r, g, b) < 32 and (r + g + b) / 3 > 45:
                    alpha_pixels[x, y] = 0
    # Shrink by a pixel to drop the grey fringe, then soften the edge.
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    image.putalpha(alpha)
    return image.crop(image.getbbox())


def ladder_tile(image):
    # Find the rungs along the centre line and cut exactly three rung periods, starting half a period above a rung,
    # so stacked cells continue the rails and rung spacing without a seam.
    centre = image.width // 2
    rows = [y for y in range(image.height) if image.getpixel((centre, y))[3] > 128]
    rungs, start = [], None
    for y in range(image.height + 1):
        solid = y in rows
        if solid and start is None:
            start = y
        if not solid and start is not None:
            rungs.append((start + y - 1) / 2)
            start = None
    period = (rungs[-1] - rungs[0]) / (len(rungs) - 1)
    top = int(round(rungs[0] - period / 2))
    tile = image.crop((0, top, image.width, top + int(round(period * 3))))
    print(f"ladder: rungs at {[round(r) for r in rungs]}, period {period:.1f}px, tile {tile.size}")
    return tile


def torch_holder(image):
    # Keep the bracket, handle and cup; drop the painted flame, which the game animates on top of the cup.
    def flame(pixel):
        r, g, b, a = pixel
        return a > 128 and r > 200 and g > 110 and b < 140 and r - b > 90
    flame_rows = [y for y in range(image.height) if sum(flame(image.getpixel((x, y))) for x in range(image.width)) > 3]
    # The flame is the warm blob at the top; the cup starts where the run of flame rows ends.
    cut = flame_rows[0]
    for y in flame_rows:
        if y - cut > 2:
            break
        cut = y
    holder = image.crop((0, cut + 1, image.width, image.height))
    holder = holder.crop(holder.getbbox())
    print(f"torch: flame rows {flame_rows[0]}..{cut} of {image.height}, holder {holder.size}")
    return holder


def brighten_gold(image):
    # The concept's gold is a bright, warm yellow that reads at a glance; the studio render is darker and browner.
    alpha = image.getchannel("A")
    rgb = image.convert("RGB")
    rgb = ImageEnhance.Brightness(rgb).enhance(1.35)
    rgb = ImageEnhance.Color(rgb).enhance(1.2)
    rgb = ImageEnhance.Contrast(rgb).enhance(1.08)
    # Shift the hue from orange towards the concept's yellow gold by lifting the green channel.
    r, g, b = rgb.split()
    rgb = Image.merge("RGB", (r, g.point(lambda v: min(255, int(v * 1.14))), b))
    result = rgb.convert("RGBA")
    result.putalpha(alpha)
    return result


def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    gold = brighten_gold(cut_out("gold", saturated=True))
    gold.save(TARGET / "gold.png")
    print(f"gold: {gold.size}")
    ladder_tile(cut_out("ladder", enclosed_gaps=True)).save(TARGET / "ladder.png")
    torch_holder(cut_out("torch")).save(TARGET / "torch.png")


main()

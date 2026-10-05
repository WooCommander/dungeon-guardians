# Cuts the HUD out of the approved concept screen (image.png, 1672 x 941) into Assets/Resources/UI/:
# the round d-pad, the two pickaxe dig buttons and their captions, the pause button and the gold icon.
# The level and gold plates carry changing text, so empty plates of the same shape are drawn instead.
# The coordinates below were measured on the concept; GameHud places the sprites at the same spots.
#   python tools/cut_ui.py
import pathlib

from PIL import Image, ImageDraw, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parent.parent
CONCEPT = ROOT / "image.png"
TARGET = ROOT / "Assets" / "Resources" / "UI"

DPAD = ((180, 806), 105)                       # centre, radius
DIG_LEFT = ((1332, 792), 78)
DIG_RIGHT = ((1525, 792), 78)
LABEL_LEFT = (1252, 874, 1414, 905)            # left, top, right, bottom
LABEL_RIGHT = (1444, 874, 1610, 905)
PAUSE = ((1619, 48), 39)
GOLD_ICON = (758, 18, 812, 62)
LEVEL_PLATE = (211, 49)                        # width, height
GOLD_PLATE = (187, 52)


def circle_mask(size, radius, feather=1.2):
    mask = Image.new("L", size, 0)
    centre = (size[0] / 2, size[1] / 2)
    ImageDraw.Draw(mask).ellipse((centre[0] - radius, centre[1] - radius, centre[0] + radius, centre[1] + radius), fill=255)
    return mask.filter(ImageFilter.GaussianBlur(feather))


def cut_circle(image, centre, radius):
    box = (centre[0] - radius - 2, centre[1] - radius - 2, centre[0] + radius + 2, centre[1] + radius + 2)
    crop = image.crop(box).convert("RGBA")
    crop.putalpha(circle_mask(crop.size, radius))
    return crop


def cut_dpad(image):
    # The petals are translucent dark glass over the dark control panel, so a colour key cannot separate them.
    # GameHud draws the same dark panel underneath, so the round cut-out (with the gaps between petals) blends in.
    return cut_circle(image, *DPAD)


def cut_label(image, box):
    # White caption on a dark panel: the brightness becomes the alpha of plain white text.
    crop = image.crop(box).convert("L")
    alpha = crop.point(lambda v: max(0, min(255, int((v - 70) * 255 / 150))))
    label = Image.new("RGBA", crop.size, (255, 255, 255, 0))
    label.putalpha(alpha)
    return label.crop(alpha.getbbox())


def cut_gold_icon(image):
    # The bright bar on the dark plate: brightness and warmth give the alpha.
    crop = image.crop(GOLD_ICON).convert("RGBA")
    pixels = crop.load()
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b, _ = pixels[x, y]
            alpha = max(0, min(255, int((r - 70) * 255 / 90))) if r > b + 25 else 0
            pixels[x, y] = (r, g, b, alpha)
    crop = crop.filter(ImageFilter.SMOOTH)
    return crop.crop(crop.getchannel("A").getbbox())


def plate(size):
    # Dark translucent rounded plate with a thin light rim, as behind the concept's level and gold labels.
    scale = 4  # drawn large and scaled down for smooth edges
    width, height = size[0] * scale, size[1] * scale
    radius = 13 * scale
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, width - 1, height - 1), radius, fill=(150, 156, 164, 235))
    rim = 2.2 * scale
    draw.rounded_rectangle((rim, rim, width - 1 - rim, height - 1 - rim), radius - rim, fill=(14, 17, 22, 242))
    return image.resize(size, Image.LANCZOS)


def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    image = Image.open(CONCEPT).convert("RGB")
    outputs = {
        "dpad": cut_dpad(image),
        "dig_left": cut_circle(image, *DIG_LEFT),
        "dig_right": cut_circle(image, *DIG_RIGHT),
        "label_dig_left": cut_label(image, LABEL_LEFT),
        "label_dig_right": cut_label(image, LABEL_RIGHT),
        "pause": cut_circle(image, *PAUSE),
        "gold_icon": cut_gold_icon(image),
        "plate_level": plate(LEVEL_PLATE),
        "plate_gold": plate(GOLD_PLATE),
    }
    for name, sprite in outputs.items():
        sprite.save(TARGET / f"{name}.png")
        print(f"{name}: {sprite.size}")


main()

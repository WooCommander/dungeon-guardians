# Cuts the touch controls out of the approved concept screen (image.png, 1672 x 941) into
# Assets/Resources/UI/: the round d-pad, the two pickaxe dig buttons and their captions.
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


def main():
    TARGET.mkdir(parents=True, exist_ok=True)
    image = Image.open(CONCEPT).convert("RGB")
    outputs = {
        "dpad": cut_dpad(image),
        "dig_left": cut_circle(image, *DIG_LEFT),
        "dig_right": cut_circle(image, *DIG_RIGHT),
        "label_dig_left": cut_label(image, LABEL_LEFT),
        "label_dig_right": cut_label(image, LABEL_RIGHT),
    }
    for name, sprite in outputs.items():
        sprite.save(TARGET / f"{name}.png")
        print(f"{name}: {sprite.size}")


main()

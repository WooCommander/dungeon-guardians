"""Cuts the start screen (map-images/menu.png, 1672 x 941) into the pieces the game uses.

- Backgrounds/menu.png: the whole picture, title and buttons included, shown behind the menu.
- ArtSource/UI/menu_play.png: the play button, cut out with its chamfered corners: the source of the menu's
  buttons, which tools/unlit_buttons.cs makes from it (not shipped: it is outside Resources).
- UI/menu_plate.png: the "settings" plate with its text removed, for the buttons of the pause panel
  (sliced at runtime: the diamonds stay at the ends, the middle stretches).

Run from the repository root: python tools/cut_menu.py
"""
from PIL import Image, ImageDraw, ImageFilter

SOURCE = "map-images/menu.png"
BACKGROUND = "Assets/Resources/Backgrounds/menu.png"
UI = "Assets/Resources/UI/"
SOURCE_PIECES = "ArtSource/UI/"

# Button boxes in source pixels (left, top, right, bottom) and the size of their cut corners.
# GameMenu.cs places the buttons by the same boxes.
BUTTONS = {
    "menu_play": ((619, 467, 1053, 591), 20),
}

# The plate: the left and right ends with their diamonds and a text-free column to stretch between them.
PLATE_BOX = (659, 617, 1010, 696)
PLATE_END = 60
PLATE_FILL_COLUMN = 725


def chamfer_mask(size, corner):
    width, height = size
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon([
        (corner, 0), (width - corner, 0), (width, corner), (width, height - corner),
        (width - corner, height), (corner, height), (0, height - corner), (0, corner),
    ], fill=255)
    return mask.filter(ImageFilter.GaussianBlur(1))


def main():
    source = Image.open(SOURCE).convert("RGBA")
    source.save(BACKGROUND)

    for name, (box, corner) in BUTTONS.items():
        piece = source.crop(box)
        piece.putalpha(chamfer_mask(piece.size, corner))
        piece.save(SOURCE_PIECES + name + ".png")

    left, top, right, bottom = PLATE_BOX
    plate = source.crop(PLATE_BOX)
    fill = source.crop((PLATE_FILL_COLUMN, top, PLATE_FILL_COLUMN + 1, bottom))
    for x in range(PLATE_END, plate.width - PLATE_END):
        plate.paste(fill, (x, 0))
    plate.putalpha(chamfer_mask(plate.size, 12))
    plate.save(UI + "menu_plate.png")
    print("menu pieces written")


if __name__ == "__main__":
    main()

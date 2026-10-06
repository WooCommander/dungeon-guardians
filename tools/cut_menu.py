"""Cuts the start screen (map-images/menu.png, 1672 x 941) into the pieces the game uses.

- Backgrounds/menu.png: the whole picture, title and buttons included, shown behind the menu.
- UI/menu_play.png, menu_levels.png, menu_settings.png: the three buttons, cut out with their chamfered corners,
  laid exactly over the picture so they can darken when pressed.
- UI/menu_plate.png: the "level select" plate with its text removed, for the buttons of the level select and
  settings panels (sliced at runtime: the diamonds stay at the ends, the middle stretches).

Run from the repository root: python tools/cut_menu.py
"""
from PIL import Image, ImageDraw, ImageFilter

SOURCE = "map-images/menu.png"
BACKGROUND = "Assets/Resources/Backgrounds/menu.png"
UI = "Assets/Resources/UI/"

# Button boxes in source pixels (left, top, right, bottom) and the size of their cut corners.
# GameMenu.cs places the buttons by the same boxes.
BUTTONS = {
    "menu_play": ((619, 467, 1053, 591), 20),
    "menu_levels": ((666, 618, 1006, 694), 12),
    "menu_settings": ((675, 715, 999, 784), 12),
}

# The plate: the left and right ends with their diamonds and a text-free column to stretch between them.
PLATE_BOX = (666, 618, 1006, 694)
PLATE_END = 60
PLATE_FILL_COLUMN = 720


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
        piece.save(UI + name + ".png")

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

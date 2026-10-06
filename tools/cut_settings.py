"""Cuts the settings screen (map-images/settings.png, 1672 x 941) into the pieces the game uses.

- Backgrounds/settings.png: the picture with the painted slider knobs, golden fills and percentages removed, so live
  controls can be drawn over it; its edges fade out. Backgrounds/settings_blur.png: a blurred copy to fill the screen
  behind it.
- UI/settings_fill.png: a full-length golden slider fill, UI/settings_knob.png: the diamond knob.
- UI/settings_toggle_on.png / settings_toggle_off.png: the vibration switch in both states.
- UI/settings_back.png, settings_reset.png, settings_done.png: the buttons, laid over the picture to darken when pressed.

GameSettingsScreen.cs places everything by the same pixel boxes. Run from the repository root:
python tools/cut_settings.py
"""
from PIL import Image, ImageDraw, ImageFilter, ImageOps

SOURCE = "map-images/settings.png"
BACKGROUND = "Assets/Resources/Backgrounds/settings.png"
BACKGROUND_BLUR = "Assets/Resources/Backgrounds/settings_blur.png"
EDGE_FADE_X, EDGE_FADE_Y = 140.0, 30.0
UI = "Assets/Resources/UI/"

# Slider rows: track centre y and painted knob centre x.
SLIDERS = [(241, 987), (331, 1069), (518, 964), (611, 1069)]
TRACK_LEFT, TRACK_RIGHT = 737, 1172
TRACK_HALF = 15
KNOB_HALF = 27
# Interior of the percentage boxes, cleared of their text.
VALUE_LEFT, VALUE_RIGHT, VALUE_HALF = 1207, 1323, 19
# The empty part of the music track, past its knob: stretched over every track.
EMPTY_COLUMN = 1120
TOGGLE = (1205, 393, 1327, 453)
BUTTONS = {
    "settings_back": ((298, 105, 395, 185), 14),
    "settings_reset": ((405, 772, 795, 868), 16),
    "settings_done": ((835, 763, 1270, 875), 20),
}


def chamfer_mask(size, corner):
    width, height = size
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon([
        (corner, 0), (width - corner, 0), (width, corner), (width, height - corner),
        (width - corner, height), (corner, height), (0, height - corner), (0, corner),
    ], fill=255)
    return mask.filter(ImageFilter.GaussianBlur(1))


def pill_mask(size):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=size[1] // 2, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(1))


def main():
    source = Image.open(SOURCE).convert("RGBA")
    clean = source.copy()

    # The empty track of the music row: right end cap, stretched interior, mirrored left cap.
    music_y = SLIDERS[0][0]
    band = (music_y - TRACK_HALF, music_y + TRACK_HALF + 1)
    right_cap = source.crop((TRACK_RIGHT - 22, band[0], TRACK_RIGHT + 1, band[1]))
    left_cap = ImageOps.mirror(right_cap)
    column = source.crop((EMPTY_COLUMN, band[0], EMPTY_COLUMN + 1, band[1]))

    for y, knob in SLIDERS:
        # The knob sticks out of the track: cover it with the row background from further left.
        box = (knob - KNOB_HALF - 3, y - KNOB_HALF - 3, knob + KNOB_HALF + 4, y + KNOB_HALF + 4)
        clean.paste(source.crop((box[0] - 150, box[1], box[2] - 150, box[3])), box[:2])
        top = y - TRACK_HALF
        for x in range(TRACK_LEFT + 22, TRACK_RIGHT - 22):
            clean.paste(column, (x, top))
        clean.paste(left_cap, (TRACK_LEFT, top))
        clean.paste(right_cap, (TRACK_RIGHT - 22, top))

        # The percentage box keeps its frame; its text goes.
        fill = source.crop((VALUE_LEFT - 2, y - VALUE_HALF, VALUE_LEFT - 1, y + VALUE_HALF))
        for x in range(VALUE_LEFT, VALUE_RIGHT):
            clean.paste(fill, (x, y - VALUE_HALF))

    # Wide phones show the picture whole with room to spare at the sides, tablets above and below. The spare room is
    # filled with a blurred copy behind it; the picture's edges fade into that copy so no seam shows.
    clean.filter(ImageFilter.GaussianBlur(14)).convert("RGB").save(BACKGROUND_BLUR)
    fade = Image.new("L", clean.size, 255)
    pixels = fade.load()
    for y in range(clean.height):
        for x in range(clean.width):
            edge = min(x / EDGE_FADE_X, (clean.width - 1 - x) / EDGE_FADE_X, y / EDGE_FADE_Y, (clean.height - 1 - y) / EDGE_FADE_Y, 1.0)
            pixels[x, y] = int(255 * edge)
    clean.putalpha(fade)
    clean.save(BACKGROUND)

    # Golden fill: the painted left part of the music track, stretched to the full track length.
    fill_band = (music_y - 10, music_y + 11)
    painted = source.crop((TRACK_LEFT + 4, fill_band[0], 940, fill_band[1]))
    fill_column = source.crop((900, fill_band[0], 901, fill_band[1]))
    golden = Image.new("RGBA", (TRACK_RIGHT - TRACK_LEFT - 8, fill_band[1] - fill_band[0]))
    golden.paste(painted, (0, 0))
    for x in range(painted.width, golden.width):
        golden.paste(fill_column, (x, 0))
    golden.putalpha(pill_mask(golden.size))
    golden.save(UI + "settings_fill.png")

    # Diamond knob.
    y, x = SLIDERS[0]
    knob = source.crop((x - KNOB_HALF, y - KNOB_HALF, x + KNOB_HALF + 1, y + KNOB_HALF + 1))
    size = knob.size[0]
    mask = Image.new("L", knob.size, 0)
    ImageDraw.Draw(mask).polygon([(size / 2, 0), (size, size / 2), (size / 2, size), (0, size / 2)], fill=255)
    knob.putalpha(mask.filter(ImageFilter.GaussianBlur(0.8)))
    knob.save(UI + "settings_knob.png")

    # Switch: as painted when on; mirrored and drained of colour when off.
    on = source.crop(TOGGLE)
    on.putalpha(pill_mask(on.size))
    on.save(UI + "settings_toggle_on.png")
    off_rgb = ImageOps.mirror(source.crop(TOGGLE)).convert("RGB")
    grey = ImageOps.grayscale(off_rgb).convert("RGB")
    off = Image.blend(grey, Image.new("RGB", grey.size, (30, 28, 26)), 0.35).convert("RGBA")
    off.putalpha(pill_mask(off.size))
    off.save(UI + "settings_toggle_off.png")

    for name, (box, corner) in BUTTONS.items():
        piece = source.crop(box)
        piece.putalpha(chamfer_mask(piece.size, corner))
        piece.save(UI + name + ".png")

    print("settings pieces written")


if __name__ == "__main__":
    main()

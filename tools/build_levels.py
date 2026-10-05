# Builds Assets/Resources/Levels/level_01..03.json from compact descriptions (platforms, ladders, bars),
# so wide maps stay easy to edit without counting characters. y counts from the bottom, as in the game.
#   python tools/build_levels.py && python tools/validate_levels.py --map
import json
import pathlib
import re

LEVELS = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Levels"
WIDTH, HEIGHT = 36, 21


class Map:
    def __init__(self, width=WIDTH, height=HEIGHT):
        self.width, self.height = width, height
        self.grid = [["." for _ in range(width)] for _ in range(height)]
        for x in range(width):
            self.set(x, 0, "#")
            self.set(x, height - 1, "#")
        for y in range(height):
            self.set(0, y, "#")
            self.set(width - 1, y, "#")
        # Diggable floor over the indestructible base.
        self.row(1, 1, width - 2, "B")

    def get(self, x, y):
        return self.grid[self.height - 1 - y][x]

    def set(self, x, y, tile):
        self.grid[self.height - 1 - y][x] = tile

    def row(self, y, x0, x1, tile):
        for x in range(x0, x1 + 1):
            self.set(x, y, tile)

    def column(self, x, y0, y1, tile):
        for y in range(y0, y1 + 1):
            self.set(x, y, tile)

    def ladder(self, x, y0, y1):
        self.column(x, y0, y1, "H")

    def bar(self, y, x0, x1):
        # Bars never replace ladder cells: a bar cell inside a ladder would stop the climb there.
        for x in range(x0, x1 + 1):
            if self.get(x, y) != "H":
                self.set(x, y, "-")

    def rows(self):
        return ["".join(row) for row in self.grid]


def point(x, y, **extra):
    return {"x": x, "y": y, **extra}


def save(number, title, level, start, exit_, gold, guardians=(), altars=()):
    level.set(exit_[0], exit_[1], "E")
    for x, y in altars:
        level.set(x, y, "A")
    data = {
        "id": f"level_{number:02d}",
        "version": 4,
        "title": title,
        "width": level.width,
        "height": level.height,
        "rows": level.rows(),
        "playerStart": point(*start),
        "exit": point(*exit_),
        "gold": [point(*g) for g in gold],
        "guardians": [point(*g) for g in guardians],
        "altars": [point(*a) for a in altars],
    }
    text = json.dumps(data, ensure_ascii=False, indent=2)
    # Keep coordinates on one line, as in hand-written levels.
    text = re.sub(r'\{\s+"x": (\d+),\s+"y": (\d+)\s+\}', r'{ "x": \1, "y": \2 }', text)
    (LEVELS / f"level_{number:02d}.json").write_text(text + "\n", encoding="utf-8")


# Walk rows are y = 2, 7, 11, 15, 19; the blocks under them sit at y = 1, 6, 10, 14, 18.
# A ladder reaches a platform when its top cell is on the platform's walk row, next to a block.

def level_1():
    # "Первые залы", laid out after the approved concept screen: three tiers of sandstone (B) mixed with reinforced
    # blocks (#), ladders between the tiers, ropes under the ceiling and the door on the top right. No guardians yet:
    # the first level teaches walking, ladders, ropes, gold and the exit.
    # Rows: blocks at y = 1, 5, 9; walk rows y = 2, 6, 10; ropes at y = 11 under the ceiling.
    m = Map(25, 13)
    m.set(8, 1, "#"); m.set(9, 1, "#")                     # reinforced stretch in the floor
    m.row(5, 1, 23, "B")                                   # tier 2, full width
    for x in (1, 7, 8, 14, 15, 23):
        m.set(x, 5, "#")
    m.row(9, 1, 6, "B"); m.row(9, 12, 23, "B")             # tier 3, split by the central gap
    for x in (1, 2, 16, 17, 23):
        m.set(x, 9, "#")
    m.ladder(4, 2, 6)       # floor -> tier 2
    m.ladder(11, 2, 6)      # floor -> tier 2
    m.ladder(20, 2, 6)      # floor -> tier 2
    m.ladder(6, 6, 10)      # tier 2 -> tier 3 left
    m.ladder(15, 6, 10)     # tier 2 -> tier 3 right
    m.ladder(19, 6, 10)     # tier 2 -> tier 3 right, next to the door
    m.ladder(9, 6, 11)      # tier 2 -> ropes, rising through the central gap
    m.ladder(13, 10, 11)    # tier 3 right -> ropes
    m.bar(11, 3, 8); m.bar(11, 10, 12); m.bar(11, 14, 18)  # ropes under the ceiling
    gold = [(2, 2), (13, 2), (18, 2), (11, 4), (3, 6), (17, 6), (18, 6),
            (2, 10), (16, 10), (5, 11), (11, 11), (17, 11)]
    save(1, "Первые залы", m, start=(6, 2), exit_=(22, 10), gold=gold)


def level_2():
    # "Пробуждение стража": the player starts in a sealed upper hall and must dig through its floor to get out,
    # so digging is learned where the guardian cannot reach. Then a chase down and back up to the exit.
    m = Map()
    m.row(18, 1, 14, "#"); m.column(15, 18, 19, "#")      # sealed upper hall (walk row 19, x 1..14)
    m.set(8, 18, "B")                                     # the only way out: dig here
    m.row(14, 3, 7, "B"); m.row(14, 9, 14, "B")           # tier 3 left, around the landing ladder
    m.row(14, 19, 33, "B")                                # tier 3 right
    m.row(18, 22, 34, "B")                                # tier 4 right, with the exit
    m.row(10, 1, 13, "B"); m.row(10, 17, 27, "B")         # tier 2
    m.row(6, 4, 16, "B"); m.row(6, 20, 31, "B")           # tier 1
    m.ladder(8, 11, 17)     # landing ladder under the dug block
    m.ladder(2, 2, 11)      # floor -> tier 2 left
    m.ladder(18, 2, 11)     # floor -> tier 2 right
    m.ladder(32, 2, 7)      # floor -> tier 1 right
    m.ladder(25, 7, 11)     # tier 1 right -> tier 2 right
    m.ladder(21, 11, 15)    # tier 2 right -> tier 3 right
    m.ladder(33, 15, 19)    # tier 3 right -> tier 4 right
    m.ladder(12, 2, 7)      # floor -> tier 1 left
    m.ladder(28, 2, 7)      # floor -> tier 1 right
    m.ladder(5, 7, 11)      # tier 1 left -> tier 2 left
    m.ladder(11, 11, 15)    # tier 2 left -> tier 3 left
    m.ladder(30, 15, 19)    # tier 3 right -> tier 4 right
    m.bar(11, 14, 16)       # tier 2 left -> right
    m.bar(15, 15, 18)       # tier 3 left -> right
    gold = [(3, 19), (12, 19), (8, 13), (5, 15), (12, 15), (6, 11), (22, 11), (10, 7), (27, 7),
            (14, 2), (28, 2), (26, 15), (24, 19),
            (6, 2), (20, 2), (13, 7), (23, 7), (3, 11), (19, 11), (23, 15), (31, 15), (27, 19), (32, 19)]
    save(2, "Пробуждение стража", m, start=(4, 19), exit_=(34, 19), gold=gold,
         guardians=[(24, 2)], altars=[(29, 2)])


def level_3():
    # "Древний тайник": two guardians, several routes. The treasure chamber (x 14..18 on walk row 7) has a brick roof
    # and walls: dig in from above, leave over the ledge at x 19 and drop to the floor.
    m = Map()
    m.row(6, 1, 9, "B"); m.row(6, 13, 19, "B"); m.row(6, 26, 34, "B")     # tier 1; x 13..19 is the chamber floor
    m.column(13, 7, 9, "#"); m.column(19, 8, 9, "#")                       # chamber walls, open at (19, 7)
    m.row(10, 4, 18, "B"); m.row(10, 22, 31, "B")                          # tier 2; x 14..18 is the chamber roof
    m.row(14, 1, 10, "B"); m.row(14, 14, 23, "B"); m.row(14, 27, 34, "B")  # tier 3
    m.row(18, 5, 30, "B")                                                  # tier 4
    m.ladder(2, 2, 7)
    m.ladder(11, 2, 11)
    m.ladder(24, 2, 11)
    m.ladder(33, 2, 7)
    m.ladder(6, 11, 15)
    m.ladder(29, 11, 15)
    m.ladder(12, 15, 19)
    m.ladder(25, 15, 19)
    m.ladder(8, 2, 7)       # floor -> tier 1 left
    m.ladder(28, 2, 7)      # floor -> tier 1 right
    m.ladder(15, 15, 19)    # tier 3 middle -> tier 4
    m.ladder(21, 11, 15)    # tier 2 bar -> tier 3 middle
    m.bar(11, 19, 21)                     # tier 2 left -> right
    m.bar(15, 11, 13); m.bar(15, 24, 26)  # tier 3 to the upper ladders
    m.bar(19, 1, 4); m.bar(19, 31, 34)    # top corners
    gold = [(16, 7), (5, 7), (30, 7), (8, 11), (27, 11), (16, 11), (3, 15), (32, 15), (18, 15),
            (10, 19), (26, 19), (2, 19), (33, 19), (6, 2), (29, 2), (21, 2),
            (12, 2), (25, 2), (30, 11), (13, 15), (22, 15), (6, 19), (20, 19), (29, 19)]
    save(3, "Древний тайник", m, start=(3, 2), exit_=(18, 19), gold=gold,
         guardians=[(30, 2), (20, 15)], altars=[(9, 15)])


if __name__ == "__main__":
    level_1()
    level_2()
    level_3()

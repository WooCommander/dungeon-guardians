# Builds Assets/Resources/Levels/level_01..14.json from compact descriptions (platforms, ladders, bars),
# so wide maps stay easy to edit without counting characters. y counts from the bottom, as in the game.
#   python tools/build_levels.py && python tools/validate_levels.py --map
import json
import pathlib
import re

from validate_levels import reachable

LEVELS = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Levels"
WIDTH, HEIGHT = 36, 21
# Torches on stands along the walk rows, every TORCH_SPACING cells, shifted by TORCH_STAGGER per row.
TORCH_SPACING, TORCH_STAGGER = 6, 3


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


def place_torches(level, start, exit_, gold, guardians):
    """Picks the torch spots and makes the block under each one indestructible, since a torch stand cannot stand
    over a hole. A spot is dropped when its reinforced block would cut the player off from any cell reachable before
    (a needed dig), so torches never change how a level is solved."""
    taken = {tuple(start), tuple(exit_), *map(tuple, gold), *map(tuple, guardians)}

    def floor(x, y):
        return (level.get(x, y) == "." and level.get(x, y - 1) in "#B" and level.get(x, y + 1) in ".-"
                and (x, y) not in taken)

    spots = [(x, y) for y in range(1, level.height - 1) for x in range(1, level.width - 1)
             if floor(x, y) and (x + y * TORCH_STAGGER) % TORCH_SPACING == 0]
    # A pair beside the exit door, as on the concept screen.
    for side in (-1, 1):
        spot = (exit_[0] + side, exit_[1])
        if floor(*spot) and spot not in spots:
            spots.append(spot)

    def reach():
        rows = level.rows()

        def tile(x, y):
            if not (0 <= x < level.width and 0 <= y < level.height):
                return "#"
            return rows[level.height - 1 - y][x]

        # Only cells the player can stand in count: a dug brick and the air a player falls through are only passed
        # on the way, and reinforcing a block rightly removes them.
        return {(x, y) for x, y in reachable({"playerStart": point(*start)}, tile, exit_open=True, dig=True)
                if tile(x, y) not in "#B" and (tile(x, y) in "H-" or tile(x, y - 1) in "#B")}

    before = reach()
    torches = []
    for x, y in spots:
        if level.get(x, y - 1) == "B":
            level.set(x, y - 1, "#")
            if not before <= reach():
                level.set(x, y - 1, "B")
                continue
        torches.append((x, y))
    return torches


def save(number, title, level, start, exit_, gold, guardians=(), altars=(), **options):
    level.set(exit_[0], exit_[1], "E")
    for x, y in altars:
        level.set(x, y, "A")
    torches = place_torches(level, start, exit_, gold, guardians)
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
        "torches": [point(*t) for t in torches],
        **options,
    }
    text = json.dumps(data, ensure_ascii=False, indent=2)
    # Keep coordinates on one line, as in hand-written levels.
    text = re.sub(r'\{\s+"x": (\d+),\s+"y": (\d+)\s+\}', r'{ "x": \1, "y": \2 }', text)
    (LEVELS / f"level_{number:02d}.json").write_text(text + "\n", encoding="utf-8")
    print(f"level_{number:02d}: {len(torches)} torches, each on a reinforced block")


# Walk rows are y = 2, 7, 11, 15, 19; the blocks under them sit at y = 1, 6, 10, 14, 18.
# A ladder reaches a platform when its top cell is on the platform's walk row, next to a block.

def level_1():
    # "Первые залы", laid out after the approved concept screen: three tiers of sandstone (B) mixed with reinforced
    # blocks (#), ladders between the tiers, ropes under the ceiling and the door on the top right. One guardian starts
    # on the top right tier, far from the explorer; its altar is on the right of the middle tier.
    # 33 x 13 cells fills a 16:9 screen above the control strip, so the painted cavern shows inside the level.
    # Rows: blocks at y = 1, 5, 9; walk rows y = 2, 6, 10; ropes at y = 11 under the ceiling.
    m = Map(33, 13)
    m.set(10, 1, "#"); m.set(11, 1, "#")                   # reinforced stretch in the floor
    m.row(5, 1, 31, "B")                                   # tier 2, full width
    for x in (1, 9, 10, 19, 20, 31):
        m.set(x, 5, "#")
    m.row(9, 1, 8, "B"); m.row(9, 16, 31, "B")             # tier 3, split by the central gap
    for x in (1, 2, 21, 22, 31):
        m.set(x, 9, "#")
    m.ladder(5, 2, 6)       # floor -> tier 2
    m.ladder(15, 2, 6)      # floor -> tier 2
    m.ladder(27, 2, 6)      # floor -> tier 2
    m.ladder(8, 6, 10)      # tier 2 -> tier 3 left
    m.ladder(20, 6, 10)     # tier 2 -> tier 3 right
    m.ladder(26, 6, 10)     # tier 2 -> tier 3 right, next to the door
    m.ladder(12, 6, 11)     # tier 2 -> ropes, rising through the central gap
    m.ladder(17, 10, 11)    # tier 3 right -> ropes
    m.bar(11, 4, 11); m.bar(11, 13, 16); m.bar(11, 18, 25)  # ropes under the ceiling
    gold = [(2, 2), (17, 2), (24, 2), (15, 4), (4, 6), (23, 6), (24, 6),
            (2, 10), (21, 10), (7, 11), (14, 11), (22, 11)]
    save(1, "Первые залы", m, start=(8, 2), exit_=(29, 10), gold=gold,
         guardians=[(24, 10)], altars=[(30, 6)])


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


# Levels 4..8 use the concept-sized 33 x 13 map of level 1: blocks at y = 1, 5, 9, walk rows y = 2, 6, 10 and
# ropes at y = 11 under the ceiling. Each adds one idea: ropes over gaps, two guardians, gold sealed in pockets
# that must be dug into, shafts between tiers and finally three guardians around a vault.

def level_4():
    # "Канатная галерея": the middle tier is split by a wide gap crossed only by a rope; the top tier is reached
    # from the ropes under the ceiling. One guardian patrols the top.
    m = Map(33, 13)
    m.row(5, 1, 11, "B"); m.row(5, 21, 31, "B")            # tier 2, split by the gap x 12..20
    m.set(11, 5, "#"); m.set(21, 5, "#")                    # reinforced lips of the gap
    m.bar(6, 12, 20)                                        # rope across the gap
    m.row(9, 6, 26, "B")                                    # tier 3 in the middle
    for x in (6, 16, 26):
        m.set(x, 9, "#")
    m.ladder(3, 2, 6)        # floor -> tier 2 left
    m.ladder(29, 2, 6)       # floor -> tier 2 right
    m.ladder(16, 2, 4)       # floor -> under the rope: climb up and grab it
    m.ladder(2, 6, 11)       # tier 2 left -> ceiling ropes
    m.ladder(30, 6, 11)      # tier 2 right -> ceiling ropes
    m.bar(11, 3, 29)         # ceiling ropes, drop onto tier 3
    gold = [(8, 2), (24, 2), (16, 6), (6, 6), (26, 6), (9, 10), (16, 10), (23, 10), (5, 11), (27, 11)]
    save(4, "Канатная галерея", m, start=(5, 2), exit_=(31, 6), gold=gold,
         guardians=[(18, 10)], altars=[(13, 10)])


def level_5():
    # "Двойная стража": two guardians, one per side, and two gold bars sealed under the floor of tier 2:
    # dig the sandstone roof of each pocket from beside it, drop in, and climb out by the pocket's ladder.
    m = Map(33, 13)
    m.row(5, 1, 31, "B")                                    # tier 2
    m.row(9, 1, 10, "B"); m.row(9, 22, 31, "B")             # tier 3 left and right
    # Pockets under tier 2: reinforced walls, sandstone roof, a ladder up the inner wall.
    for x0 in (8, 21):
        m.column(x0, 2, 4, "#"); m.column(x0 + 4, 2, 4, "#")
        m.row(5, x0, x0 + 4, "#")
        m.set(x0 + 2, 5, "B")                               # the roof brick to dig
    m.ladder(10, 2, 4)       # pocket ladders: back up through the dug roof before it refills
    m.ladder(23, 2, 4)
    m.ladder(4, 2, 6)        # floor -> tier 2
    m.ladder(16, 2, 6)       # floor -> tier 2, middle
    m.ladder(29, 2, 6)       # floor -> tier 2
    m.ladder(6, 6, 10)       # tier 2 -> tier 3 left
    m.ladder(26, 6, 10)      # tier 2 -> tier 3 right
    m.ladder(14, 6, 11); m.ladder(18, 6, 11)                # up to the ceiling ropes
    m.bar(11, 11, 21)
    gold = [(9, 3), (24, 3), (2, 2), (30, 2), (12, 6), (20, 6), (2, 10), (30, 10), (16, 11), (9, 10), (23, 10)]
    save(5, "Двойная стража", m, start=(16, 2), exit_=(10, 10), gold=gold,
         guardians=[(3, 10), (29, 10)], altars=[(8, 10), (24, 10)])


def level_6():
    # "Колодцы": the tiers are joined by narrow shafts rather than open ladders, ropes run over the outer halls,
    # and the exit waits in a walled hall in the middle of the top tier, reached only by the middle shaft.
    m = Map(33, 13)
    m.row(5, 1, 31, "B"); m.row(9, 1, 31, "B")              # two full tiers
    for x in (1, 2, 30, 31):
        m.set(x, 5, "#"); m.set(x, 9, "#")
    # Shafts: openings in the tiers with ladders.
    for x in (5, 27):
        m.set(x, 5, "H"); m.ladder(x, 2, 6)
    m.set(16, 9, "H"); m.ladder(16, 6, 10)
    m.set(9, 9, "H"); m.ladder(9, 6, 10)
    m.set(23, 9, "H"); m.ladder(23, 6, 10)
    # The middle hall of the top tier: reinforced walls, entered only by the middle shaft.
    for x in (12, 20):
        m.column(x, 10, 11, "#")
    m.bar(11, 2, 11); m.bar(11, 21, 30)                     # ropes over the outer cells
    m.ladder(3, 10, 11); m.ladder(29, 10, 11)
    gold = [(10, 2), (22, 2), (16, 2), (3, 6), (29, 6), (13, 6), (19, 6), (14, 10), (15, 10),
            (6, 11), (26, 11), (2, 2), (30, 2)]
    save(6, "Колодцы", m, start=(16, 6), exit_=(18, 10), gold=gold,
         guardians=[(4, 10), (28, 10)], altars=[(7, 2), (25, 2)])


def level_7():
    # "Зеркальные залы": a symmetric hall with three guardians: two on the sides of the floor and one on the top.
    # The middle tier has reinforced pillars that stop easy digging; the ladders cross over each other's paths.
    m = Map(33, 13)
    m.row(5, 3, 29, "B")                                    # tier 2, open at the outer edges
    for x in (8, 16, 24):
        m.set(x, 5, "#")
    m.row(9, 1, 13, "B"); m.row(9, 19, 31, "B")             # tier 3
    for x in (1, 13, 19, 31):
        m.set(x, 9, "#")
    m.ladder(6, 2, 6); m.ladder(26, 2, 6)
    m.ladder(12, 2, 6); m.ladder(20, 2, 6)
    m.ladder(3, 6, 10); m.ladder(29, 6, 10)
    m.ladder(16, 6, 11)                                     # the central climb to the ropes
    m.bar(11, 4, 15); m.bar(11, 17, 28)
    m.bar(7, 1, 2); m.bar(7, 30, 31)                        # ledges at the open edges of tier 2
    gold = [(2, 2), (30, 2), (9, 2), (23, 2), (16, 2), (10, 6), (22, 6), (4, 6), (28, 6),
            (6, 10), (26, 10), (10, 11), (22, 11), (16, 10)]
    save(7, "Зеркальные залы", m, start=(16, 6), exit_=(2, 10), gold=gold,
         guardians=[(4, 2), (28, 2), (24, 10)], altars=[(14, 2), (18, 2)])


def level_8():
    # "Сокровищница": a reinforced vault in the middle tier holds three gold bars: dig in through its roof, dig out
    # through its floor. Three guardians guard the halls.
    m = Map(33, 13)
    m.row(5, 1, 31, "B")
    for x in (1, 10, 11, 21, 22, 31):
        m.set(x, 5, "#")
    m.row(9, 1, 31, "B")
    for x in (1, 31):
        m.set(x, 9, "#")
    # The vault sits in tier 2 (walk row 6) between reinforced walls, roofed by tier 3. Its roof is reinforced but
    # for one sandstone brick: dig it from the top tier and drop in. There is no ladder: leave by digging the vault's
    # sandstone floor and falling to the bottom hall.
    m.column(13, 6, 8, "#"); m.column(19, 6, 8, "#")
    m.row(9, 13, 19, "#"); m.set(16, 9, "B")
    m.ladder(4, 2, 6); m.ladder(28, 2, 6)
    m.ladder(7, 6, 10); m.ladder(25, 6, 10)
    m.ladder(2, 10, 11); m.ladder(30, 10, 11)
    m.bar(11, 3, 29)
    gold = [(14, 6), (15, 6), (18, 6), (2, 2), (30, 2), (10, 2), (22, 2), (4, 10), (28, 10),
            (11, 10), (21, 10), (9, 6), (24, 6), (16, 11)]
    save(8, "Сокровищница", m, start=(16, 2), exit_=(13, 10), gold=gold,
         guardians=[(3, 6), (29, 6), (10, 10)], altars=[(5, 10), (27, 10)])


# Levels with a following camera (view = "follow"): larger than one screen, shown at the cell size of a 13-row hall.

def level_9():
    # "Длинная галерея": three screens wide. Rooms are cut off from each other by walls on different tiers, so the
    # way on alternates between the floor, the middle tier and the top; guardians wait further along.
    m = Map(99, 13)
    # Room 1: the middle tier, a wall on the floor and the middle tier at x 25: over the top.
    m.row(5, 1, 22, "B"); m.row(9, 14, 40, "B")
    m.column(25, 2, 8, "#")
    m.ladder(3, 2, 6); m.ladder(19, 2, 6); m.ladder(16, 6, 11)
    m.ladder(34, 2, 10)                                     # down the far side of the wall
    # Room 2: the middle tier split by a gap with a rope.
    m.row(5, 27, 38, "B"); m.row(5, 47, 62, "B")
    m.bar(6, 39, 46)
    m.ladder(30, 2, 6); m.ladder(58, 2, 6)
    # Room 3: a floor wall at x 64 (pass over on the middle tier), a pocket of gold under the middle tier.
    m.column(64, 2, 4, "#")
    m.row(5, 63, 80, "B")
    m.column(68, 2, 4, "#"); m.column(72, 2, 4, "#"); m.row(5, 68, 72, "#"); m.set(70, 5, "B"); m.ladder(70, 2, 4)
    m.row(9, 52, 76, "B")
    m.ladder(55, 6, 11); m.ladder(75, 2, 6)
    # Room 4: an upper wall at x 82 (pass along the floor), the exit on the top tier at the end.
    m.column(82, 6, 11, "#")
    m.row(5, 84, 97, "B"); m.row(9, 86, 97, "B")
    m.ladder(85, 2, 6); m.ladder(90, 6, 10); m.ladder(79, 6, 10)
    m.bar(11, 17, 33); m.bar(11, 56, 74)                    # ropes under the ceiling
    m.ladder(37, 10, 11)
    gold = [(8, 2), (12, 6), (22, 10), (28, 11), (38, 2), (43, 6), (50, 2), (60, 6), (66, 10), (69, 3),
            (71, 3), (78, 6), (84, 2), (93, 6), (95, 2), (88, 10)]
    save(9, "Длинная галерея", m, start=(2, 2), exit_=(96, 10), gold=gold,
         guardians=[(45, 2), (70, 10), (92, 2)], altars=[(52, 2), (87, 2)], view="follow")


def level_10():
    # "Шахта": three screens deep. The explorer starts at the top; every floor has a gap at alternating ends to drop
    # through and a ladder to climb back for gold that was passed. The exit is at the bottom.
    m = Map(33, 37)
    tiers = list(range(33, 4, -4))                          # block rows y = 33, 29, ..., 5
    for i, y in enumerate(tiers):
        m.row(y, 1, 31, "B")
        gap = (26, 28) if i % 2 == 0 else (4, 6)
        m.row(y, gap[0], gap[1], ".")
        m.set(gap[0] - 1, y, "#"); m.set(gap[1] + 1, y, "#")  # reinforced lips around the gap
        ladder = 10 if i % 2 == 0 else 22
        m.ladder(ladder, y - 3, y + 1)                      # from the tier below up through this floor
    m.bar(31, 12, 20); m.bar(19, 12, 20)                   # ropes in two of the halls
    gold = [(16, 34), (29, 30), (3, 30), (14, 26), (27, 22), (8, 18), (16, 19), (24, 14), (5, 10),
            (18, 6), (28, 2), (12, 2)]
    save(10, "Шахта", m, start=(4, 34), exit_=(16, 2), gold=gold,
         guardians=[(20, 22), (8, 10)], altars=[(30, 26), (2, 14)], view="follow")


# Dark halls: only the helmet lamp and the torches light the way. Levels 11 and 12 share one map, to compare the two
# rules: in "Тёмный зал" the light only shows the way; in "Островки света" guardians will not step into torchlight,
# so the cells around a torch are safe islands to wait in.

def dark_hall(number, title, repel):
    m = Map(33, 13)
    m.row(5, 1, 14, "B"); m.row(5, 18, 31, "B")
    for x in (1, 14, 18, 31):
        m.set(x, 5, "#")
    m.row(9, 4, 12, "B"); m.row(9, 20, 28, "B")
    m.column(16, 6, 9, "#")                                 # a pillar between the halves of the upper tiers
    m.ladder(3, 2, 6); m.ladder(12, 2, 6); m.ladder(20, 2, 6); m.ladder(29, 2, 6)
    m.ladder(6, 6, 10); m.ladder(26, 6, 10)
    m.ladder(10, 6, 11); m.ladder(22, 6, 11)
    m.bar(11, 11, 21)                                       # over the pillar
    m.bar(6, 15, 17)                                        # across the middle gap... into the pillar's side
    gold = [(8, 2), (16, 2), (24, 2), (2, 6), (9, 6), (23, 6), (30, 6), (5, 10), (11, 10), (21, 10), (27, 10),
            (16, 11)]
    save(number, title, m, start=(16, 2), exit_=(28, 10), gold=gold,
         guardians=[(4, 10), (30, 2)], altars=[(2, 2), (25, 6)], dark=True, lightRepelsGuardians=repel)


def level_11():
    dark_hall(11, "Тёмный зал", repel=False)


def level_12():
    dark_hall(12, "Островки света", repel=True)


# Seal trials: pressure plates ('_') open gates ('|'). Two rules to compare: in "Испытание печати" only a guardian's
# weight moves the plate and the gate stays open for good ("latch"); in "Тяжёлая плита" the explorer can press it too,
# but the gate only stays up while the plate is pressed and a few seconds after ("hold").

def level_13():
    # The exit waits in a chamber on the top right behind a gate. The plate lies on the middle tier, on the only
    # way down for the guardian of the top left: lead it there, and the seal breaks.
    m = Map(33, 13)
    m.row(5, 1, 14, "B"); m.row(5, 18, 31, "B")
    m.row(9, 1, 13, "B"); m.row(9, 19, 31, "B")
    for x in (1, 13, 19, 31):
        m.set(x, 9, "#")
    m.column(26, 10, 11, "#"); m.set(26, 10, "|")          # the gate into the exit chamber
    m.ladder(6, 6, 10)                                      # top left -> middle tier: the guardian comes this way
    m.ladder(12, 2, 6)                                      # middle tier left -> floor
    m.ladder(20, 2, 6); m.ladder(29, 2, 6)
    m.ladder(22, 6, 10)                                     # middle right -> top right, in front of the gate
    m.set(9, 6, "_")                                        # the plate, between the guardian's two ladders
    m.bar(6, 15, 17)                                        # rope over the middle gap
    gold = [(4, 2), (16, 2), (26, 2), (3, 6), (16, 6), (25, 6), (30, 6), (20, 10), (24, 10), (10, 10)]
    save(13, "Испытание печати", m, start=(24, 2), exit_=(29, 10), gold=gold,
         guardians=[(3, 10)], altars=[(11, 10)], gateMode="latch", playerPressesPlates=False)


def level_14():
    # A wall splits the hall; its only door is a gate on the floor. The plate is six cells before it: step on it
    # and run. Gold on both sides, the exit on the far side, a guardian on each side.
    m = Map(33, 13)
    m.column(17, 2, 11, "#"); m.set(17, 2, "|")            # the dividing wall and its gate
    m.row(5, 1, 14, "B"); m.row(5, 20, 31, "B")
    m.row(9, 4, 16, "B"); m.row(9, 18, 28, "B")
    m.ladder(3, 2, 6); m.ladder(13, 2, 6); m.ladder(8, 6, 10)
    m.ladder(21, 2, 6); m.ladder(30, 2, 6); m.ladder(26, 6, 10)
    m.set(11, 2, "_")                                       # the plate, six cells from the gate
    gold = [(5, 2), (2, 6), (10, 6), (6, 10), (14, 10), (20, 2), (28, 2), (24, 6), (19, 10), (23, 10)]
    save(14, "Тяжёлая плита", m, start=(4, 2), exit_=(28, 10), gold=gold,
         guardians=[(12, 10), (25, 2)], altars=[(15, 10), (31, 6)], gateMode="hold", playerPressesPlates=True)


if __name__ == "__main__":
    level_1()
    level_2()
    level_3()
    level_4()
    level_5()
    level_6()
    level_7()
    level_8()
    level_9()
    level_10()
    level_11()
    level_12()
    level_13()
    level_14()

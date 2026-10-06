# Static checks for Assets/Resources/Levels/*.json, following the movement rules in DungeonSimulation:
# map shape, single start and exit, actors and gold in passable cells, altars present when there are guardians,
# and reachability of every gold piece and of the open exit.
# Digging is modelled as in the game: standing on solid footing, open the brick diagonally below if the cell above it
# is empty, then step over and fall through. Holes refill, so a reachable cell is not a proof that a level is solvable.
#   python tools/validate_levels.py [--map]
import json
import pathlib
import sys
from collections import deque

LEVELS = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "Levels"
KNOWN = set("#BH-EA. _|~")
# '~' are fragile planks: footing for the explorer (only a heavy guardian breaks them).
SOLID = set("#B~")
# The exit is a doorway: walked past while closed, the level is won by entering it once open.
# Plates ('_') are floor; gates ('|') are assumed open: whether a guardian can be lured onto the plate in time is a
# matter for play-testing, not for this check.
PASSABLE = set(".H-AE _|")


def load(path):
    level = json.loads(path.read_text(encoding="utf-8"))
    rows = level["rows"]
    height = len(rows)

    def tile(x, y):
        if not (0 <= x < level["width"] and 0 <= y < height):
            return "#"
        return rows[height - 1 - y][x]

    return level, tile


def reachable(level, tile, exit_open, dig):
    # exit_open is kept for the callers: the doorway is passable either way, so it no longer changes the result.
    def support(x, y):
        if tile(x, y) in "H-":
            return True
        return tile(x, y - 1) in SOLID

    def passable(x, y):
        return tile(x, y) in PASSABLE

    # A state is (x, y, in_hole): in_hole means the cell is a freshly dug brick the player is falling through.
    start = (level["playerStart"]["x"], level["playerStart"]["y"], False)
    seen = {start}
    queue = deque([start])
    while queue:
        x, y, in_hole = queue.popleft()
        moves = []
        if in_hole or not support(x, y):
            moves.append((x, y - 1, False))  # falling: no steering
        else:
            moves += [(x - 1, y, False), (x + 1, y, False)]
            if tile(x, y) == "H":
                moves.append((x, y + 1, False))
            if tile(x, y) in "H-":
                moves.append((x, y - 1, False))
            # Digging needs solid footing (not a ladder or bar) and an empty cell above the target brick.
            standing = tile(x, y) not in "H-" and tile(x, y - 1) in SOLID
            for side in (-1, 1):
                if dig and standing and tile(x + side, y - 1) == "B" and tile(x + side, y) in ". ":
                    moves.append((x + side, y - 1, True))
        for state in moves:
            nx, ny, hole = state
            if (hole or passable(nx, ny)) and state not in seen:
                seen.add(state)
                queue.append(state)
    return {(x, y) for x, y, _ in seen}


def check(path, show_map):
    level, tile = load(path)
    errors, notes = [], []
    rows = level["rows"]
    if level["height"] != len(rows):
        errors.append(f"height {level['height']} != {len(rows)} rows")
    for i, row in enumerate(rows):
        if len(row) != level["width"]:
            errors.append(f"row {i} has {len(row)} cells, expected {level['width']}")
        if set(row) - KNOWN:
            errors.append(f"row {i} has unknown tiles {set(row) - KNOWN}")
    if any(c != "#" for c in rows[0] + rows[-1]) or any(r[0] != "#" or r[-1] != "#" for r in rows):
        errors.append("the border must be indestructible (#)")
    exits = [(x, level["height"] - 1 - r) for r, row in enumerate(rows) for x, c in enumerate(row) if c == "E"]
    if exits != [(level["exit"]["x"], level["exit"]["y"])]:
        errors.append(f"exit tiles {exits} do not match exit {level['exit']}")
    altars = sorted((x, level["height"] - 1 - r) for r, row in enumerate(rows) for x, c in enumerate(row) if c == "A")
    if altars != sorted((a["x"], a["y"]) for a in level.get("altars", [])):
        errors.append(f"altar tiles {altars} do not match altars {level.get('altars')}")
    if level.get("guardians") and not altars:
        errors.append("guardians need an altar")

    actors = [("start", level["playerStart"])] + [("guardian", g) for g in level.get("guardians", [])]
    for name, point in actors + [("gold", g) for g in level["gold"]]:
        if tile(point["x"], point["y"]) not in PASSABLE:
            errors.append(f"{name} {point} is inside '{tile(point['x'], point['y'])}'")

    walking = reachable(level, tile, exit_open=False, dig=False)
    closed = reachable(level, tile, exit_open=False, dig=True)
    opened = reachable(level, tile, exit_open=True, dig=True)
    for gold in level["gold"]:
        position = (gold["x"], gold["y"])
        if position not in closed:
            errors.append(f"gold {position} is not reachable even with digging")
        elif position not in walking:
            notes.append(f"gold {position} needs digging")
    if (level["exit"]["x"], level["exit"]["y"]) not in opened:
        errors.append("the open exit is not reachable")

    status = "OK" if not errors else "FAIL"
    print(f"{path.name}: {status}  {level['width']}x{level['height']}, gold {len(level['gold'])}, guardians {len(level.get('guardians', []))}")
    for line in errors:
        print("  error:", line)
    for line in notes:
        print("  note: ", line)
    if show_map:
        # "," marks cells reachable without digging, ":" cells reachable only by digging.
        marks = {(g["x"], g["y"]): "$" for g in level["gold"]}
        marks.update({(g["x"], g["y"]): "G" for g in level.get("guardians", [])})
        marks[(level["playerStart"]["x"], level["playerStart"]["y"])] = "P"
        for r, row in enumerate(rows):
            y = level["height"] - 1 - r
            line = ""
            for x, c in enumerate(row):
                if (x, y) in marks:
                    line += marks[(x, y)]
                elif c == "." and (x, y) in walking:
                    line += ","
                elif c == "." and (x, y) in closed:
                    line += ":"
                else:
                    line += c
            print(f"  {line}  y={y}")
    return not errors


if __name__ == "__main__":
    results = [check(path, "--map" in sys.argv) for path in sorted(LEVELS.glob("level_*.json"))]
    sys.exit(0 if all(results) else 1)

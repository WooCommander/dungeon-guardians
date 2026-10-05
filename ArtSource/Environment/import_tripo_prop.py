# Turns a raw Tripo export (zip or FBX) into a game-ready level piece in Assets/Resources/Environment:
# decimates the mesh, fits it to the cell size, puts the pivot at the bottom centre and ships the textures as files.
# Run from the project root:
#   blender --background --factory-startup --python ArtSource/Environment/import_tripo_prop.py -- <export.zip|export.fbx> <asset>
# <asset> is a key of PROPS below; the result replaces Assets/Resources/Environment/<asset>.fbx.
import pathlib
import shutil
import sys
import tempfile
import zipfile

import bpy
from mathutils import Matrix, Vector

PROJECT = pathlib.Path(__file__).resolve().parent.parent.parent
TARGET = PROJECT / "Assets" / "Resources" / "Environment"
# ArtModelImporter assigns Textures/<asset>_basecolor and <asset>_normal to the imported material.
TEXTURE_TARGET = TARGET / "Textures"
SHIPPED_MAPS = ["basecolor", "normal"]

# Size of the piece in cells (x = width, z = height; depth keeps the width's proportion) and the triangle budget.
# Blocks are half a cell high: LevelRenderer stacks two per cell.
PROPS = {
    "block_solid": {"width": 1.0, "height": 0.5, "triangles": 1500},
    "block_diggable": {"width": 1.0, "height": 0.5, "triangles": 1500},
}


def find_fbx(source, workdir):
    if source.suffix.lower() == ".zip":
        with zipfile.ZipFile(source) as archive:
            archive.extractall(workdir)
        return next(workdir.rglob("*.fbx"))
    return source


def relink_textures(folder):
    # Tripo names maps "<model>_<kind>.<ext>"; the FBX may point at a folder name that differs from the download.
    files = [path for path in folder.rglob("*") if path.suffix.lower() in (".jpeg", ".jpg", ".png")]
    shipped = {}
    for image in bpy.data.images:
        kind = image.name.rsplit("_", 1)[-1].lower()
        match = next((path for path in files if path.stem.lower().endswith("_" + kind)), None)
        if match is None:
            continue
        image.filepath = str(match)
        image.reload()
        shipped[kind] = match
    return shipped


def decimate(mesh, triangles):
    before = len(mesh.data.polygons)
    if before > triangles:
        modifier = mesh.modifiers.new("Decimate", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = triangles / before
        modifier.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    print(f"[prop] triangles {before} -> {len(mesh.data.polygons)}")


def fit_to_cell(mesh, width, height):
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    points = [vertex.co for vertex in mesh.data.vertices]
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    size = high - low
    scale_x = width / size.x
    scale_z = height / size.z
    # Bottom centre to the origin, then scale; depth follows the width so the piece keeps its proportions.
    centre = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, low.z))
    mesh.data.transform(Matrix.Diagonal((scale_x, scale_x, scale_z, 1.0)) @ Matrix.Translation(-centre))
    mesh.data.update()
    print(f"[prop] size {tuple(round(v, 3) for v in size)} -> ({width}, {round(size.y * scale_x, 3)}, {height})")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    source, asset = pathlib.Path(argv[0]).resolve(), argv[1]
    settings = PROPS[asset]

    with tempfile.TemporaryDirectory() as temp:
        fbx = find_fbx(source, pathlib.Path(temp))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(fbx))
        maps = relink_textures(fbx.parent)

        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
        for obj in list(bpy.data.objects):
            if obj.type != "MESH":
                bpy.data.objects.remove(obj, do_unlink=True)
        bpy.ops.object.select_all(action="DESELECT")
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        if len(meshes) > 1:
            bpy.ops.object.join()
        mesh = bpy.context.view_layer.objects.active
        mesh.name = asset

        decimate(mesh, settings["triangles"])
        fit_to_cell(mesh, settings["width"], settings["height"])

        TEXTURE_TARGET.mkdir(parents=True, exist_ok=True)
        for kind in SHIPPED_MAPS:
            if kind in maps:
                shipped = TEXTURE_TARGET / f"{asset}_{kind}{maps[kind].suffix.lower()}"
                for old in TEXTURE_TARGET.glob(f"{asset}_{kind}.*"):
                    old.unlink()
                shutil.copyfile(maps[kind], shipped)
                print(f"[prop] shipped {shipped.name}")

        bpy.ops.export_scene.fbx(
            filepath=str(TARGET / f"{asset}.fbx"),
            use_selection=False,
            apply_scale_options="FBX_SCALE_ALL",
            bake_anim=False,
            path_mode="STRIP",
            embed_textures=False,
        )
        print(f"[prop] exported {asset}.fbx")


main()

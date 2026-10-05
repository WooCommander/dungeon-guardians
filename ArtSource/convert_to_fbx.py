# Converts the GLB art in ArtSource into FBX for Unity (Unity has no built-in GLB importer).
# Run from the project root:
#   blender --background --factory-startup --python ArtSource/convert_to_fbx.py
import pathlib

import bpy

SOURCE = pathlib.Path(__file__).resolve().parent
RESOURCES = SOURCE.parent / "Assets" / "Resources"

# The explorer is built from the Tripo model by Characters/build_explorer.py.
CHARACTERS = ["guardian"]
ENVIRONMENT = [
    "altar", "block_diggable", "block_solid", "column", "door_closed", "door_open",
    "gold", "ladder_section", "rope_section", "rubble", "torch",
]


def import_glb(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))

    # The glTF importer adds an "Icosphere" mesh as the bone display shape; it is not part of the model.
    shapes = {bone.custom_shape for obj in bpy.data.objects if obj.type == "ARMATURE" for bone in obj.pose.bones}
    for obj in list(bpy.data.objects):
        if obj in shapes or (obj.type == "MESH" and obj.name.startswith("Icosphere")):
            bpy.data.objects.remove(obj, do_unlink=True)


def make_static():
    # Environment pieces come with a technical one-bone skin; bake it into plain meshes.
    for obj in list(bpy.data.objects):
        if obj.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = obj
        for modifier in list(obj.modifiers):
            if modifier.type == "ARMATURE":
                bpy.ops.object.modifier_apply(modifier=modifier.name)
        matrix = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = matrix
        obj.vertex_groups.clear()

    for obj in list(bpy.data.objects):
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)


def export(path, animated):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=animated,
        bake_anim_use_all_actions=animated,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
    )


for name in CHARACTERS:
    import_glb(SOURCE / "Characters" / f"{name}.glb")
    # glTF import may append the armature name to action names; keep the clip name only.
    for action in bpy.data.actions:
        action.name = action.name.split("_")[0].split("|")[-1]
    print(f"[convert] {name}: objects = {[o.name for o in bpy.data.objects]}, actions = {[a.name for a in bpy.data.actions]}")
    export(RESOURCES / "Characters" / f"{name}.fbx", animated=True)

for name in ENVIRONMENT:
    import_glb(SOURCE / "Environment" / f"{name}.glb")
    make_static()
    print(f"[convert] {name}: objects = {[o.name for o in bpy.data.objects]}")
    export(RESOURCES / "Environment" / f"{name}.fbx", animated=False)

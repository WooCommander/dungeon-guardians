# Shared Blender helpers for the character build scripts (build_explorer.py, build_guardian.py):
# loading a Tripo model with its textures, keying poses on a humanoid skeleton, previews and the FBX export.
import math
import pathlib
import shutil
import sys

import bpy
from mathutils import Euler, Vector

FPS = 30
CHARACTERS = pathlib.Path(__file__).resolve().parent.parent.parent / "Assets" / "Resources" / "Characters"
# Unity does not extract textures embedded in the FBX with the material import mode we use, so the maps ship as
# files; ModelTextures binds Textures/<model>_basecolor and <model>_normal at runtime.
TEXTURE_TARGET = CHARACTERS / "Textures"
SHIPPED_MAPS = ["basecolor", "normal"]

# Bones every clip keys, so switching clips never leaves a bone in the previous clip's pose.
ANIMATED_BONES = [
    "Hips", "Spine", "Chest", "Head",
    "Left_UpperArm", "Left_LowerArm", "Right_UpperArm", "Right_LowerArm",
    "Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Right_UpperLeg", "Right_LowerLeg", "Right_Foot",
]

# Rotation convention (armature space, the model faces -Y, its left side is +X, Z is up):
#   x > 0 tips the top of an upright bone forward and swings a hanging limb backward;
#   y rotates in the frontal plane: for arms, y = side * a brings the arm towards the body, y = -side * a raises it sideways.
LEFT, RIGHT = 1, -1


def load_model(model, asset, log):
    """Imports a Tripo FBX, relinks its textures and ships the colour and normal maps as <asset>_<map>."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=str(model))

    # Tripo exports reference a "tripo_convert_<id>.fbm" folder and file names that change between downloads;
    # match each image by its map type (basecolor, normal, ...) against the files shipped next to the model.
    files = [path for path in model.parent.rglob("*") if path.suffix.lower() in (".jpeg", ".jpg", ".png")]
    for image in bpy.data.images:
        # The map type is the last word of the file name ("..._basecolor.JPEG"); some exports give the images
        # generic names ("Diffuse Texture.003"), so prefer the file name and fall back to the image name.
        stem = pathlib.Path(bpy.path.abspath(image.filepath)).stem if image.filepath else image.name
        kind = stem.rsplit("_", 1)[-1].lower()
        match = next((path for path in files if path.stem.lower().endswith("_" + kind)), None)
        if match is None:
            raise FileNotFoundError(f"No texture file for {image.name} in {model.parent}")
        image.filepath = str(match)
        image.reload()
        log(f"texture {image.name} -> {match.name} {tuple(image.size)}")

        if kind in SHIPPED_MAPS:
            TEXTURE_TARGET.mkdir(parents=True, exist_ok=True)
            for old in TEXTURE_TARGET.glob(f"{asset}_{kind}.*"):
                old.unlink()
            shipped = TEXTURE_TARGET / f"{asset}_{kind}{match.suffix.lower()}"
            shutil.copyfile(match, shipped)
            log(f"shipped {shipped.name}")


def prepare_armature(armature, name):
    armature.name = name
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"


def apply_pose(armature, pose):
    for bone in armature.pose.bones:
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)

    for name, value in pose.items():
        bone = armature.pose.bones[name]
        rest = bone.bone.matrix_local.to_3x3()
        if name == "Hips" and len(value) == 4:
            bone.location = rest.inverted() @ Vector((0.0, 0.0, value[3]))
        world = Euler([math.radians(a) for a in value[:3]], "XYZ").to_matrix()
        bone.rotation_quaternion = (rest.inverted() @ world @ rest).to_quaternion()


def key_clip(armature, name, frames):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action

    for frame, pose in frames:
        apply_pose(armature, pose)
        for bone_name in ANIMATED_BONES:
            bone = armature.pose.bones[bone_name]
            bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone_name)
            if bone_name == "Hips":
                bone.keyframe_insert("location", frame=frame, group=bone_name)
    return action


def legs(left_thigh, left_knee, right_thigh, right_knee):
    # Feet stay level: they undo the rotation of the thigh and shin above them.
    return {
        "Left_UpperLeg": (left_thigh, 0, 0), "Left_LowerLeg": (left_knee, 0, 0), "Left_Foot": (-(left_thigh + left_knee), 0, 0),
        "Right_UpperLeg": (right_thigh, 0, 0), "Right_LowerLeg": (right_knee, 0, 0), "Right_Foot": (-(right_thigh + right_knee), 0, 0),
    }


def arm(side, swing, spread, elbow):
    prefix = "Left" if side == LEFT else "Right"
    return {f"{prefix}_UpperArm": (swing, side * spread, 0), f"{prefix}_LowerArm": (elbow, 0, 0)}


def pose(hips_drop=0.0, spine=0.0, chest=0.0, head=0.0, head_turn=0.0, **parts):
    result = {"Hips": (0, 0, 0, hips_drop), "Spine": (spine, 0, 0), "Chest": (chest, 0, 0), "Head": (head, head_turn, 0)}
    for part in parts.values():
        result.update(part)
    return result


def key_clips(armature, clips, log):
    for name, build in clips.items():
        key_clip(armature, name, build())
        log(f"keyed {name}")


def preview_folder():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return pathlib.Path(argv[argv.index("--preview") + 1]) if "--preview" in argv else None


def render_preview(armature, clips, folder, height):
    folder.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = scene.render.resolution_y = 360
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.5
    middle = height / 2
    views = {"front": ((0, -3, middle), (math.radians(90), 0, 0)), "side": ((3, 0, middle), (math.radians(90), 0, math.radians(90)))}
    for clip, frames in clips.items():
        armature.animation_data.action = bpy.data.actions[clip]
        for frame, _ in frames():
            scene.frame_set(frame)
            for view, (location, rotation) in views.items():
                camera = bpy.data.objects.new(f"cam_{view}", camera_data)
                scene.collection.objects.link(camera)
                camera.location, camera.rotation_euler = location, rotation
                scene.camera = camera
                scene.render.filepath = str(folder / f"{clip}_{frame:02d}_{view}.png")
                bpy.ops.render.render(write_still=True)
                bpy.data.objects.remove(camera, do_unlink=True)


def export(armature, asset, log):
    armature.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(0)
    target = CHARACTERS / f"{asset}.fbx"
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(target),
        use_selection=False,
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
        embed_textures=False,
    )
    log(f"exported {target}")

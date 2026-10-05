# Builds the game explorer from the rigged Tripo model: attaches the pickaxe to the right hand,
# keys the gameplay clips (Idle, Walk, Climb, Fall, Dig) and exports Assets/Resources/Characters/explorer.fbx.
# Run from the project root:
#   blender --background --factory-startup --python ArtSource/Characters/build_explorer.py
# Add "-- --preview <folder>" to also render a contact sheet of the key poses.
import math
import pathlib
import shutil
import sys

import bpy
from mathutils import Euler, Matrix, Vector

SOURCE = pathlib.Path(__file__).resolve().parent
MODEL = SOURCE / "explorer_tripo" / "miner_character_3d_model_1.fbx"
PICKAXE = SOURCE / "pickaxe.glb"
TARGET = SOURCE.parent.parent / "Assets" / "Resources" / "Characters" / "explorer.fbx"
# Unity does not extract textures embedded in the FBX with the material import mode we use, so the maps
# ship as files; ArtModelImporter assigns <model>_basecolor and <model>_normal from this folder.
TEXTURE_TARGET = TARGET.parent / "Textures"
SHIPPED_MAPS = ["basecolor", "normal"]
FPS = 30
# Total pickaxe length relative to the source model (0.68 m); the explorer is about 1 m tall.
PICKAXE_SCALE = 0.42

ANIMATED_BONES = [
    "Hips", "Spine", "Chest", "Head",
    "Left_UpperArm", "Left_LowerArm", "Right_UpperArm", "Right_LowerArm",
    "Left_UpperLeg", "Left_LowerLeg", "Left_Foot", "Right_UpperLeg", "Right_LowerLeg", "Right_Foot",
]

# Rotation convention (armature space, the model faces -Y, its left side is +X, Z is up):
#   x > 0 tips the top of an upright bone forward and swings a hanging limb backward;
#   y rotates in the frontal plane: for arms, y = side * a brings the arm towards the body, y = -side * a raises it sideways.
LEFT, RIGHT = 1, -1


def load_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    bpy.ops.import_scene.fbx(filepath=str(MODEL))
    relink_textures()
    armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    armature.name = "Explorer"
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
    return armature


def relink_textures():
    # Tripo exports reference a "tripo_convert_<id>.fbm" folder and file names that change between downloads;
    # match each image by its map type (basecolor, normal, ...) against the files shipped next to the model.
    files = [path for path in MODEL.parent.rglob("*") if path.suffix.lower() in (".jpeg", ".jpg", ".png")]
    for image in bpy.data.images:
        kind = image.name.rsplit("_", 1)[-1].lower()
        match = next((path for path in files if path.stem.lower().endswith("_" + kind)), None)
        if match is None:
            raise FileNotFoundError(f"No texture file for {image.name} in {MODEL.parent}")
        image.filepath = str(match)
        image.reload()
        print(f"[explorer] texture {image.name} -> {match.name} {tuple(image.size)}")

        if kind in SHIPPED_MAPS:
            TEXTURE_TARGET.mkdir(parents=True, exist_ok=True)
            shipped = TEXTURE_TARGET / f"{TARGET.stem}_{kind}{match.suffix.lower()}"
            shutil.copyfile(match, shipped)
            print(f"[explorer] shipped {shipped.name}")


def attach_pickaxe(armature):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(PICKAXE))
    imported = [obj for obj in bpy.data.objects if obj not in before]
    mesh = next(obj for obj in imported if obj.type == "MESH" and not obj.name.startswith("Icosphere"))

    # The pickaxe GLB carries a technical one-bone skin; bake it into a plain mesh.
    bpy.context.view_layer.objects.active = mesh
    for modifier in list(mesh.modifiers):
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh.parent = None
    mesh.vertex_groups.clear()
    for obj in imported:
        if obj is not mesh:
            bpy.data.objects.remove(obj, do_unlink=True)

    # Grip at the hand, handle continuing the forearm, blade spread forward/back in the swing plane.
    hand = armature.data.bones["Right_Hand"]
    forearm = armature.data.bones["Right_LowerArm"]
    z_axis = (forearm.tail_local - forearm.head_local).normalized()
    forward = Vector((0.0, -1.0, 0.0))
    x_axis = (forward - forward.dot(z_axis) * z_axis).normalized()
    y_axis = z_axis.cross(x_axis)
    rotation = Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()
    grip = hand.head_local + z_axis * 0.02

    mesh.name = "Pickaxe"
    mesh.parent = armature
    mesh.parent_type = "BONE"
    mesh.parent_bone = "Right_Hand"
    mesh.matrix_world = Matrix.Translation(grip) @ rotation @ Matrix.Scale(PICKAXE_SCALE, 4)


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


def pose(hips_drop=0.0, spine=0.0, chest=0.0, head=0.0, **parts):
    result = {"Hips": (0, 0, 0, hips_drop), "Spine": (spine, 0, 0), "Chest": (chest, 0, 0), "Head": (head, 0, 0)}
    for part in parts.values():
        result.update(part)
    return result


def idle():
    def frame(breath):
        return pose(hips_drop=-0.004 * breath, chest=2.5 * breath, head=-2 * breath,
                    left=arm(LEFT, 0, 22 + 3 * breath, -12 - 4 * breath),
                    right=arm(RIGHT, 0, 22 + 3 * breath, -12 - 4 * breath),
                    legs=legs(0, 0, 0, 0))
    return [(0, frame(0)), (30, frame(1)), (60, frame(0))]


def walk():
    def frame(phase):
        # phase 1: left leg forward, right arm forward; phase -1: the opposite.
        return pose(hips_drop=-0.012, chest=5,
                    left=arm(LEFT, 22 * phase, 20, -15),
                    right=arm(RIGHT, -22 * phase, 20, -15),
                    legs=legs(-28 * phase, 8, 28 * phase, 8))

    def passing(left_lifted):
        lift = legs(-2, 40, 4, 6) if left_lifted else legs(4, 6, -2, 40)
        return pose(hips_drop=0.006, chest=5, left=arm(LEFT, 0, 20, -15), right=arm(RIGHT, 0, 20, -15), legs=lift)

    return [(0, frame(1)), (6, passing(False)), (12, frame(-1)), (18, passing(True)), (24, frame(1))]


def climb():
    def frame(phase):
        # The left hand reaches up while the right foot steps up, and vice versa.
        reach_left = phase > 0
        return pose(chest=-4,
                    left=arm(LEFT, -168 if reach_left else -125, 8, -10 if reach_left else -50),
                    right=arm(RIGHT, -125 if reach_left else -168, 8, -50 if reach_left else -10),
                    legs=legs(-15, 25, -60, 80) if reach_left else legs(-60, 80, -15, 25))
    return [(0, frame(1)), (12, frame(-1)), (24, frame(1))]


def fall():
    def frame(flail):
        return pose(chest=-6, head=-8,
                    left=arm(LEFT, -10, -(105 + 12 * flail), -25),
                    right=arm(RIGHT, -10, -(105 - 12 * flail), -25),
                    legs=legs(-25 + 8 * flail, 40, -10 - 8 * flail, 25))
    return [(0, frame(1)), (8, frame(-1)), (16, frame(1))]


def dig():
    windup = pose(spine=-5, chest=-10, head=-5,
                  left=arm(LEFT, -40, 15, -30), right=arm(RIGHT, -155, 10, -35), legs=legs(-15, 10, 10, 5))
    impact = pose(hips_drop=-0.015, spine=6, chest=16, head=2,
                  left=arm(LEFT, -20, 15, -20), right=arm(RIGHT, -35, 10, -10), legs=legs(-20, 18, 12, 8))
    settle = pose(hips_drop=-0.01, spine=5, chest=12, head=2,
                  left=arm(LEFT, -15, 18, -18), right=arm(RIGHT, -25, 12, -8), legs=legs(-18, 15, 10, 6))
    return [(0, windup), (6, impact), (10, settle)]


CLIPS = {"Idle": idle, "Walk": walk, "Climb": climb, "Fall": fall, "Dig": dig}


def render_preview(armature, folder):
    folder.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = scene.render.resolution_y = 360
    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.5
    views = {"front": ((0, -3, 0.5), (math.radians(90), 0, 0)), "side": ((3, 0, 0.5), (math.radians(90), 0, math.radians(90)))}
    for clip, frames in CLIPS.items():
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


def main():
    armature = load_scene()
    attach_pickaxe(armature)
    for name, build in CLIPS.items():
        key_clip(armature, name, build())
        print(f"[explorer] keyed {name}")

    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--preview" in argv:
        render_preview(armature, pathlib.Path(argv[argv.index("--preview") + 1]))

    armature.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(0)
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(TARGET),
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
    print(f"[explorer] exported {TARGET}")


main()

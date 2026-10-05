# Builds the game explorer from the rigged Tripo model: attaches the pickaxe to the right hand,
# keys the gameplay clips (Idle, Walk, Climb, Fall, Dig) and exports Assets/Resources/Characters/explorer.fbx.
# Run from the project root:
#   blender --background --factory-startup --python ArtSource/Characters/build_explorer.py
# Add "-- --preview <folder>" to also render a contact sheet of the key poses.
import pathlib
import sys

import bpy
from mathutils import Matrix, Vector

SOURCE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(SOURCE))
from character_rig import LEFT, RIGHT, arm, export, key_clips, legs, load_model, pose, prepare_armature, preview_folder, render_preview  # noqa: E402

MODEL = SOURCE / "explorer_tripo" / "miner_character_3d_model_1.fbx"
PICKAXE = SOURCE / "pickaxe.glb"
# Total pickaxe length relative to the source model (0.68 m); the explorer is about 1 m tall.
PICKAXE_SCALE = 0.42


def log(message):
    print(f"[explorer] {message}")


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


def idle():
    def frame(breath):
        return pose(hips_drop=-0.004 * breath, chest=2.5 * breath, head=-2 * breath,
                    left=arm(LEFT, 0, 22 + 3 * breath, -12 - 4 * breath),
                    right=arm(RIGHT, 0, 22 + 3 * breath, -12 - 4 * breath),
                    legs=legs(0, 0, 0, 0))
    return [(0, frame(0)), (30, frame(1)), (60, frame(0))]


def walk():
    # A brisk, wide stride: the explorer covers 3 cells a second, so a slow cycle made the feet slide.
    # One cycle (two steps) takes 16 frames, about half a second.
    def frame(phase):
        # phase 1: left leg forward, right arm forward; phase -1: the opposite.
        return pose(hips_drop=-0.016, chest=8,
                    left=arm(LEFT, 30 * phase, 20, -25),
                    right=arm(RIGHT, -30 * phase, 20, -25),
                    legs=legs(-36 * phase, 10, 36 * phase, 10))

    def passing(left_lifted):
        lift = legs(-6, 55, 6, 6) if left_lifted else legs(6, 6, -6, 55)
        return pose(hips_drop=0.008, chest=8, left=arm(LEFT, 0, 20, -25), right=arm(RIGHT, 0, 20, -25), legs=lift)

    return [(0, frame(1)), (4, passing(False)), (8, frame(-1)), (12, passing(True)), (16, frame(1))]


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


def main():
    load_model(MODEL, "explorer", log)
    armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    prepare_armature(armature, "Explorer")
    attach_pickaxe(armature)
    key_clips(armature, CLIPS, log)
    folder = preview_folder()
    if folder:
        render_preview(armature, CLIPS, folder, 1.0)
    export(armature, "explorer", log)


main()

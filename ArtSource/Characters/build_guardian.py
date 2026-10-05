# Builds the game guardian from the Tripo stone golem. The export has no skeleton, so this script places a humanoid
# skeleton at the golem's joints, skins the mesh by distance to the bones, keys the gameplay clips
# (Idle, Walk, Climb, Fall, Struggle) and exports Assets/Resources/Characters/guardian.fbx.
# Run from the project root:
#   blender --background --factory-startup --python ArtSource/Characters/build_guardian.py
# Add "-- --preview <folder>" to also render a contact sheet of the key poses.
import pathlib
import sys

import bpy
from mathutils import Vector

SOURCE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(SOURCE))
from character_rig import LEFT, RIGHT, arm, export, key_clips, legs, load_model, pose, prepare_armature, preview_folder, render_preview  # noqa: E402

MODEL = next((SOURCE / "guardian_tripo").glob("*.fbx"))
HEIGHT = 0.82

# Joints measured on the golem (metres; it faces -Y, its left side is +X): name -> (head, tail, parent).
# Right-side bones mirror the left ones.
BONES = {
    "Hips": ((0, 0.02, 0.30), (0, 0.02, 0.37), None),
    "Spine": ((0, 0.02, 0.37), (0, 0.02, 0.44), "Hips"),
    "Chest": ((0, 0.02, 0.44), (0, 0.02, 0.52), "Spine"),
    "Head": ((0, 0.0, 0.52), (0, -0.04, 0.80), "Chest"),
    "Left_UpperArm": ((0.29, 0.03, 0.50), (0.39, 0.03, 0.36), "Chest"),
    "Left_LowerArm": ((0.39, 0.03, 0.36), (0.44, 0.01, 0.24), "Left_UpperArm"),
    "Left_Hand": ((0.44, 0.01, 0.24), (0.45, 0.0, 0.13), "Left_LowerArm"),
    "Left_UpperLeg": ((0.19, 0.02, 0.30), (0.20, 0.02, 0.17), "Hips"),
    "Left_LowerLeg": ((0.20, 0.02, 0.17), (0.20, 0.02, 0.08), "Left_UpperLeg"),
    "Left_Foot": ((0.20, 0.02, 0.08), (0.20, -0.12, 0.02), "Left_LowerLeg"),
}


def log(message):
    print(f"[guardian] {message}")


def mirrored_bones():
    bones = dict(BONES)
    for name, (head, tail, parent) in BONES.items():
        if name.startswith("Left_"):
            flip = lambda p: (-p[0], p[1], p[2])  # noqa: E731
            bones["Right_" + name[5:]] = (flip(head), flip(tail), parent.replace("Left_", "Right_") if parent else None)
    return bones


def build_skeleton():
    bpy.ops.object.armature_add(enter_editmode=True)
    armature = bpy.context.object
    edit = armature.data.edit_bones
    edit.remove(edit[0])
    bones = mirrored_bones()
    for name, (head, tail, _) in bones.items():
        bone = edit.new(name)
        bone.head, bone.tail = Vector(head), Vector(tail)
    for name, (_, _, parent) in bones.items():
        if parent:
            edit[name].parent = edit[parent]
            edit[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return armature, bones


def allowed(name, point):
    # Keep limbs from claiming torso stone and vice versa; the golem is made of solid blocks, so hard limits read well.
    x, z = abs(point.x), point.z
    if "Arm" in name or "Hand" in name:
        # Arms hang outside the torso (x > 0.30); higher up the shoulder pads start a little further in.
        return x > 0.30 or (x > 0.25 and z > 0.40)
    if "Leg" in name or "Foot" in name:
        return z < 0.34 and 0.04 < x < 0.33
    if name == "Head":
        return z > 0.42 and x < 0.30
    return True


def skin(mesh, armature, bones):
    # Weight every vertex by its distance to the bone segments (two nearest bones blend at the joints).
    groups = {name: mesh.vertex_groups.new(name=name) for name in bones}
    for vertex in mesh.data.vertices:
        point = mesh.matrix_world @ vertex.co
        scored = []
        for name, (head, tail, _) in bones.items():
            if not allowed(name, point):
                continue
            a, b = Vector(head), Vector(tail)
            t = max(0.0, min(1.0, (point - a).dot(b - a) / (b - a).length_squared))
            distance = (point - (a + (b - a) * t)).length
            scored.append((distance, name))
        scored.sort()
        nearest = scored[:2]
        weights = [1.0 / (d + 0.01) ** 6 for d, _ in nearest]
        total = sum(weights)
        blend = {name: weight / total for (_, name), weight in zip(nearest, weights)}

        # The arm's inner side is fused with the torso; fade the armpit band from chest to upper arm, so raising an arm
        # stretches a wide strip of stone a little instead of one thin strip a lot.
        x, z = abs(point.x), point.z
        if 0.22 < x < 0.33 and 0.40 < z < 0.52:
            side = "Left" if point.x > 0 else "Right"
            share = max(0.0, min(1.0, (x - 0.22) / 0.11))
            blend = {"Chest": 1.0 - share, f"{side}_UpperArm": share}

        for name, weight in blend.items():
            if weight > 0.0:
                groups[name].add([vertex.index], weight, "REPLACE")

    mesh.parent = armature
    modifier = mesh.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature


# The golem is heavy: short strides, arms swinging wide, a slight forward hunch.
def idle():
    def frame(breath):
        return pose(hips_drop=-0.006 * breath, chest=3 * breath, head=-2 * breath,
                    left=arm(LEFT, 0, 4 - 3 * breath, -10 - 5 * breath),
                    right=arm(RIGHT, 0, 4 - 3 * breath, -10 - 5 * breath),
                    legs=legs(0, 0, 0, 0))
    return [(0, frame(0)), (36, frame(1)), (72, frame(0))]


def walk():
    def stride(phase):
        return pose(hips_drop=-0.014, chest=7, head=-3,
                    left=arm(LEFT, 20 * phase, 6, -18),
                    right=arm(RIGHT, -20 * phase, 6, -18),
                    legs=legs(-22 * phase, 6, 22 * phase, 6))

    def passing(left_lifted):
        lift = legs(-4, 34, 4, 4) if left_lifted else legs(4, 4, -4, 34)
        return pose(hips_drop=0.008, chest=7, head=-3, left=arm(LEFT, 0, 6, -18), right=arm(RIGHT, 0, 6, -18), legs=lift)

    return [(0, stride(1)), (7, passing(False)), (14, stride(-1)), (21, passing(True)), (28, stride(1))]


def climb():
    def frame(reach_left):
        return pose(chest=-3,
                    left=arm(LEFT, -130 if reach_left else -95, 4, -15 if reach_left else -45),
                    right=arm(RIGHT, -95 if reach_left else -130, 4, -45 if reach_left else -15),
                    legs=legs(-12, 20, -50, 70) if reach_left else legs(-50, 70, -12, 20))
    return [(0, frame(True)), (14, frame(False)), (28, frame(True))]


def fall():
    def frame(flail):
        return pose(chest=-6, head=-6,
                    left=arm(LEFT, -15, -(65 + 10 * flail), -20),
                    right=arm(RIGHT, -15, -(65 - 10 * flail), -20),
                    legs=legs(-20 + 6 * flail, 30, -8 - 6 * flail, 20))
    return [(0, frame(1)), (8, frame(-1)), (16, frame(1))]


def struggle():
    # Trapped in a hole: shoves upwards with alternating arms, body shaking.
    def frame(push_left):
        return pose(hips_drop=-0.01, chest=6 if push_left else -4, head=-10, head_turn=8 if push_left else -8,
                    left=arm(LEFT, -125 if push_left else -90, 10, -20 if push_left else -55),
                    right=arm(RIGHT, -90 if push_left else -125, 10, -55 if push_left else -20),
                    legs=legs(-10, 15, -10, 15))
    return [(0, frame(True)), (6, frame(False)), (12, frame(True))]


CLIPS = {"Idle": idle, "Walk": walk, "Climb": climb, "Fall": fall, "Struggle": struggle}


def main():
    load_model(MODEL, "guardian", log)
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
    for obj in list(bpy.data.objects):
        if obj is not mesh:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    armature, bones = build_skeleton()
    skin(mesh, armature, bones)
    prepare_armature(armature, "Guardian")
    log(f"skeleton with {len(bones)} bones, {len(mesh.data.vertices)} vertices skinned")

    key_clips(armature, CLIPS, log)
    folder = preview_folder()
    if folder:
        render_preview(armature, CLIPS, folder, HEIGHT)
    export(armature, "guardian", log)


main()

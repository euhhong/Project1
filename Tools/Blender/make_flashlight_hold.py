"""
도둑 손전등 비추기 모션(상체만 쓰는 반복 모션)을 만든다. 바탕 = Mixamo 대기(Idle) — 숨 쉬는 몸통 흔들림은 그대로 두고 오른팔만 바꾼다.
- 오른팔(위팔·아래팔·손)을 몸 앞쪽으로 곧게 어깨 높이에 뻗어 든다(pitch_down도만큼 살짝 아래, 손전등이 앞을 비추게).
- 머리는 앞을 본다.
- 처음과 끝 프레임이 같아 자연스럽게 반복된다(바탕 대기도 반복 모션).
Unity에서는 SkillUpper 레이어(상체 마스크)로 재생한다 — 다리는 걷기·달리기 그대로.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_flashlight_hold.py -- <Idle.fbx> <출력.fbx> [pitch_down=4] [aim_yaw=0]
  aim_yaw(도): 게임 안에서 팔이 앞에서 비껴 보이면 그만큼 돌린다(+ = 왼쪽).
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <idle.fbx> <out.fbx> [pitch_down] [aim_yaw]")
    src, dst = argv[0], argv[1]
    pitch_down = float(argv[2]) if len(argv) > 2 else 4.0
    aim_yaw = float(argv[3]) if len(argv) > 3 else 0.0

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    scene = bpy.context.scene
    mw = arm.matrix_world
    pbs = arm.pose.bones
    for b in pbs:
        b.rotation_mode = 'QUATERNION'
    s0, s1 = (int(round(v)) for v in arm.animation_data.action.frame_range)

    scene.frame_set(s0); bpy.context.view_layer.update()
    up = Vector((0, 0, 1))
    right = (mw @ bone(pbs, "RightArm").head) - (mw @ bone(pbs, "LeftArm").head); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    fwd = Quaternion(up, math.radians(aim_yaw)) @ fwd
    side = up.cross(fwd).normalized() * -1  # 몸 오른쪽
    aim = (Quaternion(side, math.radians(-pitch_down)) @ fwd).normalized()
    head = bone(pbs, "Head")
    head0 = (mw @ head.matrix).to_quaternion()

    frames = list(range(s0, s1 + 1))
    samples = {}
    for f in frames:
        scene.frame_set(f); bpy.context.view_layer.update()
        samples[f] = ({b.name: b.rotation_quaternion.copy() for b in pbs}, {b.name: b.location.copy() for b in pbs})
    arm.animation_data_clear()

    def rotate_world(b, q):
        loc, rot, scl = (mw @ b.matrix).decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, q @ rot, scl)
        bpy.context.view_layer.update()

    def aim_bone(name, direction):
        b = bone(pbs, name)
        cur = ((mw @ b.matrix).to_3x3() @ Vector((0, 1, 0))).normalized()
        rotate_world(b, cur.rotation_difference(direction.normalized()))

    for i, f in enumerate(frames, start=1):
        rot, loc = samples[f]
        for b in pbs:
            b.rotation_quaternion = rot[b.name]
            b.location = loc[b.name]
        bpy.context.view_layer.update()
        aim_bone("RightArm", aim)
        aim_bone("RightForeArm", aim)
        aim_bone("RightHand", aim)
        loc_h, rot_h, scl_h = (mw @ head.matrix).decompose()
        head.matrix = mw.inverted() @ Matrix.LocRotScale(loc_h, rot_h.slerp(head0, 0.7), scl_h)
        bpy.context.view_layer.update()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=i)
            if b.parent is None:
                b.keyframe_insert("location", frame=i)

    scene.frame_start, scene.frame_end = 1, len(frames)
    scene.frame_set(1)
    hand = mw @ bone(pbs, "RightHand").head; sh = mw @ bone(pbs, "RightArm").head
    print(f"[make_flashlight_hold] arm dir·aim={(hand - sh).normalized().dot(aim):.3f} (1 = 앞으로 곧게), frames {len(frames)} from {s0}-{s1}")
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_flashlight_hold] wrote {dst}")


main()

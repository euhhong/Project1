"""
모션 FBX의 머리가 항상 앞(기준 프레임의 머리 방향)을 보게 고쳐 새 FBX로 내보낸다.
허리·척추·팔 등 다른 뼈는 그대로 두고, 목(Neck)과 머리(Head) 회전만 바꾼다.
몸을 숙이거나 비틀어도 머리의 월드 방향은 기준 프레임과 같게 유지된다.
보정량은 목이 neck_share만큼, 나머지는 머리가 맡는다(목이 꺾여 보이지 않게).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/head_look_forward.py -- <입력.fbx> <출력.fbx> [기준 프레임=첫 프레임] [neck_share=0.3] [yaw=0] [pitch=0]
  yaw / pitch(도): 기준 머리 방향을 더 돌린다(월드 위축 / 좌우축). Unity가 몸 방향 기준으로 모션을 돌려 놓아
  게임 안에서 머리가 정면에서 벗어나 보이면 그만큼 되돌린다.
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <in.fbx> <out.fbx> [ref_frame] [neck_share] [yaw] [pitch]")
    src, dst = argv[0], argv[1]
    ref = int(argv[2]) if len(argv) > 2 else None
    neck_share = float(argv[3]) if len(argv) > 3 else 0.3
    yaw = float(argv[4]) if len(argv) > 4 else 0.0
    pitch = float(argv[5]) if len(argv) > 5 else 0.0

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    f0, f1 = (int(round(v)) for v in act.frame_range)
    if ref is None:
        ref = f0
    pb = arm.pose.bones
    neck = next(b for b in pb if b.name.lower().endswith("neck"))
    head = next(b for b in pb if b.name.lower().endswith("head"))
    for b in (neck, head):
        b.rotation_mode = 'QUATERNION'

    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = f0, f1

    def world_rot(b):
        return (arm.matrix_world @ b.matrix).to_quaternion()

    scene.frame_set(ref)
    target = world_rot(head)
    # 월드 위축(Z) 둘레로 yaw, 좌우축(X) 둘레로 pitch만큼 더 돌린 방향을 목표로 한다.
    target = Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(pitch)) @ target

    # 프레임마다 먼저 원래 값을 모두 읽고(키를 넣으면 커브가 바뀌므로), 그다음 고친다.
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        cur = world_rot(head)
        delta = target @ cur.inverted()           # 월드 기준으로 머리를 목표 방향으로 돌리는 회전
        if delta.w < 0:
            delta.negate()
        # 목: 보정의 일부만(월드 회전을 목 뼈에 앞에서 곱한다)
        part = Quaternion().slerp(delta, neck_share)
        nw = arm.matrix_world @ neck.matrix
        loc, rot, scl = nw.decompose()
        neck.matrix = arm.matrix_world.inverted() @ (
            Matrix.LocRotScale(loc, part @ rot, scl))
        bpy.context.view_layer.update()
        # 머리: 남은 만큼 — 월드 회전을 정확히 목표로
        hw = arm.matrix_world @ head.matrix
        loc, rot, scl = hw.decompose()
        head.matrix = arm.matrix_world.inverted() @ (
            Matrix.LocRotScale(loc, target, scl))
        bpy.context.view_layer.update()
        neck.keyframe_insert("rotation_quaternion", frame=f)
        head.keyframe_insert("rotation_quaternion", frame=f)

    # 확인용: 고친 뒤 머리 방향 오차(도)
    worst = 0.0
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        worst = max(worst, world_rot(head).rotation_difference(target).angle)
    print(f"[head_look_forward] frames {f0}-{f1}, ref {ref}, neck_share {neck_share}, max head error {worst * 57.2958:.2f} deg")

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[head_look_forward] wrote {dst}")


main()

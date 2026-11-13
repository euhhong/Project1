"""
사냥터지기 그랩 쏘기 모션을 새로 만든다(몸을 앞으로 기울이며 신중하게 던지기).
- 바탕 자세: 대기 모션 FBX의 첫 프레임(Mixamo 뼈대).
- 프레임(30fps): 1 서 있음 → 8 겨눔(오른손은 귀 옆, 팔꿈치는 옆·뒤로, 왼팔은 앞으로 겨눔, 상체 오른쪽 뒤로)
  → 12 던짐(상체를 앞으로 22° 기울이며 오른팔을 어깨 높이로 앞으로 뻗음, 왼팔은 왼쪽 뒤로 벌려 균형) → 31 다시 서 있음.
  게임에서는 12프레임(던진 자세)에서 멈춘 채 그랩이 손에서 날아가고, 돌아오면 나머지를 재생한다.
- 머리는 내내 정면을 본다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_grab_throw.py -- <바탕 대기.fbx> <출력.fbx>
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector

# 머리 방향 보정(도): 게임 안 감시자 대기 자세의 정면에 맞춘다(바탕 대기 FBX와 게임 대기가 조금 달라서). + yaw = 왼쪽으로, + pitch = 위로.
HEAD_YAW, HEAD_PITCH = 11.0, 12.0

KEYS = {  # 프레임: (상체 비틀기 도(+ = 오른쪽 어깨가 뒤로), 앞으로 기울기 도, 팔 자세)
    1: (0.0, 0.0, None),
    8: (20.0, -3.0, "cock"),
    12: (-10.0, 22.0, "thrust"),
    16: (-9.0, 21.0, "thrust"),
    31: (0.0, 0.0, None),
}


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <base_idle.fbx> <out.fbx>")
    src, dst = argv[0], argv[1]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    scene = bpy.context.scene
    mw = arm.matrix_world
    pbs = arm.pose.bones
    for b in pbs:
        b.rotation_mode = 'QUATERNION'

    scene.frame_set(int(arm.animation_data.action.frame_range[0]))
    bpy.context.view_layer.update()
    base_rot = {b.name: b.rotation_quaternion.copy() for b in pbs}
    base_loc = {b.name: b.location.copy() for b in pbs}
    head = bone(pbs, "Head")
    base_head_world = (mw @ head.matrix).to_quaternion()  # (아래에서 몸 방향을 구한 뒤 보정)

    # 몸 방향(월드): 오른쪽 = 왼 어깨 → 오른 어깨, 위 = Z, 앞 = 위 × 오른쪽(대기 자세는 발끝이 벌어져 발로는 못 정한다)
    def wpos(b):
        return mw @ b.head
    up = Vector((0, 0, 1))
    right = wpos(bone(pbs, "RightArm")) - wpos(bone(pbs, "LeftArm")); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    base_head_world = Quaternion(up, math.radians(HEAD_YAW)) @ Quaternion(right, math.radians(HEAD_PITCH)) @ base_head_world
    print(f"[make_grab_throw] forward={tuple(round(v, 2) for v in fwd)} right={tuple(round(v, 2) for v in right)}")

    arm.animation_data_clear()  # 새 모션: 대기 모션 키는 버리고 지금(첫 프레임) 자세에서 시작

    def rotate_world(b, q):
        """뼈 b를 자기 머리(관절) 둘레로 월드 회전 q만큼 돌린다."""
        w = mw @ b.matrix
        loc, rot, scl = w.decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, q @ rot, scl)
        bpy.context.view_layer.update()

    def aim(b, direction):
        """뼈 b의 길이 방향(뼈 Y축)을 월드 방향 direction으로."""
        w = mw @ b.matrix
        cur = (w.to_3x3() @ Vector((0, 1, 0))).normalized()
        rotate_world(b, cur.rotation_difference(direction.normalized()))

    spine, spine2 = bone(pbs, "Spine"), bone(pbs, "Spine2")
    r_up, r_lo, r_hand = bone(pbs, "RightArm"), bone(pbs, "RightForeArm"), bone(pbs, "RightHand")
    l_up, l_lo = bone(pbs, "LeftArm"), bone(pbs, "LeftForeArm")

    for frame, (twist, lean, pose) in KEYS.items():
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()
        # 상체: 위축 둘레로 비틀기(+ = 오른쪽 어깨가 뒤로) → 몸 앞으로 기울기
        if twist:
            rotate_world(spine2, Quaternion(up, -math.radians(twist)))   # 위축 둘레 + 회전은 오른쪽이 앞으로 가므로 반대로
        if lean:
            rotate_world(spine, Quaternion(right, -math.radians(lean)))  # 오른쪽축 둘레 + 회전은 뒤로 젖히므로 반대로
        if pose == "cock":
            # 던지기 전: 오른팔은 팔꿈치를 옆·뒤로 들고 손은 귀 옆(겨누기), 왼팔은 앞으로 뻗어 겨눈다.
            aim(r_up, 0.65 * right - 0.6 * fwd + 0.1 * up)
            aim(r_lo, 0.25 * fwd + 0.95 * up - 0.1 * right)
            aim(r_hand, 0.5 * fwd + 0.85 * up)
            aim(l_up, 0.75 * fwd - 0.3 * right - 0.35 * up)
            aim(l_lo, 0.9 * fwd - 0.1 * right - 0.1 * up)
        elif pose == "thrust":
            # 던짐: 몸을 앞으로 기울이며 오른팔을 어깨 높이로 앞으로 조심스럽게 뻗는다(팔꿈치 살짝 굽힘).
            # 왼팔은 왼쪽 뒤로 벌려 균형을 잡는다.
            aim(r_up, fwd + 0.25 * up + 0.22 * right)
            aim(r_lo, fwd + 0.08 * up + 0.12 * right)
            aim(r_hand, fwd)
            aim(l_up, -0.75 * right - 0.45 * up - 0.35 * fwd)
            aim(l_lo, -0.7 * right - 0.55 * up + 0.2 * fwd)
        # 머리는 서 있을 때 방향 그대로(정면)
        w = mw @ head.matrix
        loc, _, scl = w.decompose()
        head.matrix = mw.inverted() @ Matrix.LocRotScale(loc, base_head_world, scl)
        bpy.context.view_layer.update()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=frame)
            if b.parent is None:
                b.keyframe_insert("location", frame=frame)

    scene.frame_start, scene.frame_end = min(KEYS), max(KEYS)
    scene.frame_set(12)
    hand = wpos(r_hand); sh = wpos(r_up)
    print(f"[make_grab_throw] frame 12: right hand - right shoulder = {tuple(round(v, 3) for v in (hand - sh))} (forward·up·right "
          f"{(hand - sh).dot(fwd):.3f} {(hand - sh).dot(up):.3f} {(hand - sh).dot(right):.3f})")

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_grab_throw] wrote {dst}")


main()

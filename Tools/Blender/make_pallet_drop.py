"""
생존자 판자 내리기 모션을 새로 만든다(두 손으로 판자 윗부분을 잡고 앞으로 세게 밀어 넘어뜨리기).
- 바탕 자세: 대기 모션 FBX의 첫 프레임(Mixamo 뼈대). make_grab_throw.py와 같은 방식.
- 프레임(30fps): 1 서 있음 → 7 잡음(두 손을 어깨 위 앞으로 들어 판자 윗부분을 잡음, 몸 살짝 뒤로)
  → 12 밂(몸을 앞으로 24° 기울이며 두 팔을 앞 아래로 쭉 뻗어 밀어 넘어뜨림) → 16 민 채 → 30 다시 서 있음.
  게임에서는 12프레임 즈음 판자가 넘어가기 시작한다(Pallet.dropDelay).
- 머리는 내내 정면을 본다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_pallet_drop.py -- <바탕 대기.fbx> <출력.fbx>
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector

KEYS = {  # 프레임: (앞으로 기울기 도, 팔 자세)
    1: (0.0, None),
    7: (-6.0, "grab"),
    12: (24.0, "push"),
    16: (22.0, "push"),
    30: (0.0, None),
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
    base_head_world = (mw @ head.matrix).to_quaternion()

    def wpos(b):
        return mw @ b.head
    up = Vector((0, 0, 1))
    right = wpos(bone(pbs, "RightArm")) - wpos(bone(pbs, "LeftArm")); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    print(f"[make_pallet_drop] forward={tuple(round(v, 2) for v in fwd)} right={tuple(round(v, 2) for v in right)}")

    arm.animation_data_clear()

    def rotate_world(b, q):
        w = mw @ b.matrix
        loc, rot, scl = w.decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, q @ rot, scl)
        bpy.context.view_layer.update()

    def aim(b, direction):
        w = mw @ b.matrix
        cur = (w.to_3x3() @ Vector((0, 1, 0))).normalized()
        rotate_world(b, cur.rotation_difference(direction.normalized()))

    spine = bone(pbs, "Spine")
    r_up, r_lo, r_hand = bone(pbs, "RightArm"), bone(pbs, "RightForeArm"), bone(pbs, "RightHand")
    l_up, l_lo, l_hand = bone(pbs, "LeftArm"), bone(pbs, "LeftForeArm"), bone(pbs, "LeftHand")

    for frame, (lean, pose) in KEYS.items():
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()
        if lean:
            rotate_world(spine, Quaternion(right, -math.radians(lean)))  # 오른쪽축 둘레 + 회전은 뒤로 젖히므로 반대로
        if pose == "grab":
            # 두 손을 어깨 위 앞으로 들어 판자 윗부분을 잡는다(팔꿈치 굽힘, 손바닥이 앞).
            aim(r_up, 0.7 * fwd + 0.55 * up + 0.2 * right)
            aim(r_lo, 0.45 * fwd + 0.9 * up - 0.1 * right)
            aim(r_hand, 0.8 * fwd + 0.6 * up)
            aim(l_up, 0.7 * fwd + 0.55 * up - 0.2 * right)
            aim(l_lo, 0.45 * fwd + 0.9 * up + 0.1 * right)
            aim(l_hand, 0.8 * fwd + 0.6 * up)
        elif pose == "push":
            # 밂: 두 팔을 앞 아래로 쭉 뻗어 판자를 넘어뜨린다(어깨 폭).
            aim(r_up, fwd - 0.15 * up + 0.12 * right)
            aim(r_lo, fwd - 0.25 * up + 0.05 * right)
            aim(r_hand, fwd - 0.1 * up)
            aim(l_up, fwd - 0.15 * up - 0.12 * right)
            aim(l_lo, fwd - 0.25 * up - 0.05 * right)
            aim(l_hand, fwd - 0.1 * up)
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
    for f in (7, 12):
        scene.frame_set(f)
        d = wpos(r_hand) - wpos(r_up)
        print(f"[make_pallet_drop] frame {f}: right hand from shoulder forward·up·right = {d.dot(fwd):.3f} {d.dot(up):.3f} {d.dot(right):.3f}")

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_pallet_drop] wrote {dst}")


main()

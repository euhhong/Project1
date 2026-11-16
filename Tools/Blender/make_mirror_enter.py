"""
거울 순간이동 들어가기 모션을 새로 만든다(거울에 오른손을 뻗고 → 한 발 내디디며 → 몸을 앞으로 기울여 빨려 들어감).
- 바탕 자세: 대기 모션 FBX의 첫 프레임(Mixamo 뼈대). make_grab_throw.py와 같은 방식.
- 프레임(30fps): 1 서 있음 → 6 뻗음(오른손을 거울 쪽 가슴 높이로, 몸 살짝 앞) → 12 내디딤(오른발 앞으로, 두 팔 앞, 몸 14°)
  → 18 빨려 듦(몸 28° 앞으로, 두 팔 앞으로 쭉) → 24 그대로(이때 반대 거울로 옮겨짐 — MirrorTraveler.enterTime 0.8초).
- 나올 때는 게임이 이 모션을 손을 뻗은 자세(진행도 0.45)에서 거꾸로 서 있는 자세까지 돌린다(MirrorTravelAnimator).
- 머리는 내내 정면을 본다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_mirror_enter.py -- <바탕 대기.fbx> <출력.fbx>
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector

KEYS = {  # 프레임: (앞으로 기울기 도, 팔·다리 자세)
    1: (0.0, None),
    6: (5.0, "reach"),
    12: (14.0, "step"),
    18: (28.0, "dive"),
    24: (28.0, "dive"),
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
    print(f"[make_mirror_enter] forward={tuple(round(v, 2) for v in fwd)} right={tuple(round(v, 2) for v in right)}")

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
    r_thigh, r_shin = bone(pbs, "RightUpLeg"), bone(pbs, "RightLeg")
    l_thigh, l_shin = bone(pbs, "LeftUpLeg"), bone(pbs, "LeftLeg")

    for frame, (lean, pose) in KEYS.items():
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()
        if lean:
            rotate_world(spine, Quaternion(right, -math.radians(lean)))  # 오른쪽축 둘레 + 회전은 뒤로 젖히므로 반대로
        if pose == "reach":
            # 뻗음: 오른손을 거울 쪽 가슴 높이로(손바닥이 거울), 왼팔은 몸 옆에 살짝 든다.
            aim(r_up, fwd + 0.05 * up + 0.15 * right)
            aim(r_lo, fwd + 0.1 * up)
            aim(r_hand, fwd + 0.25 * up)
            aim(l_up, 0.25 * fwd - up - 0.2 * right)
            aim(l_lo, 0.45 * fwd - up)
        elif pose in ("step", "dive"):
            # 내디딤·빨려 듦: 두 팔 앞으로(오른팔이 조금 더 위), 오른발을 앞으로 내디딘다(dive는 더 크게).
            k = 1.0 if pose == "dive" else 0.6
            aim(r_up, fwd + 0.15 * up + 0.12 * right)
            aim(r_lo, fwd + 0.2 * up)
            aim(r_hand, fwd + 0.3 * up)
            aim(l_up, fwd - (0.35 - 0.3 * k) * up - 0.12 * right)
            aim(l_lo, fwd - (0.2 - 0.2 * k) * up)
            aim(l_hand, fwd)
            aim(r_thigh, (0.35 + 0.25 * k) * fwd - up)
            aim(r_shin, 0.1 * fwd - up)
            aim(l_thigh, -0.15 * k * fwd - up)
            aim(l_shin, -0.35 * k * fwd - up)
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
    for f in (6, 18):
        scene.frame_set(f)
        d = wpos(r_hand) - wpos(r_up)
        print(f"[make_mirror_enter] frame {f}: right hand from shoulder forward·up·right = {d.dot(fwd):.3f} {d.dot(up):.3f} {d.dot(right):.3f}")

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_mirror_enter] wrote {dst}")


main()

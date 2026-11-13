"""
모션 FBX를 좌우로 뒤집어 새 FBX로 내보낸다(왼팔 공격 → 오른팔 공격).
Left*/Right* 뼈끼리 동작을 맞바꾸고, 몸 가운데 면에 비춘 회전으로 바꾼다.
뼈의 축 방향과 상관없이 월드 기준(쉼 자세 대비 회전)으로 계산하므로, 쉼 자세가 좌우 대칭이면 정확하다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/mirror_motion.py -- <입력.fbx> <출력.fbx>
"""
import bpy, sys
from mathutils import Matrix, Quaternion, Vector


def counterpart(name, names):
    for a, b in (("Left", "Right"), ("Right", "Left")):
        if a in name:
            other = name.replace(a, b)
            if other in names:
                return other
    return name


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <in.fbx> <out.fbx>")
    src, dst = argv[0], argv[1]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    f0, f1 = (int(round(v)) for v in act.frame_range)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = f0, f1
    mw = arm.matrix_world
    pbs = arm.pose.bones
    names = {b.name for b in pbs}
    for b in pbs:
        b.rotation_mode = 'QUATERNION'

    # 좌우 축: 쉼 자세에서 왼쪽 어깨 - 오른쪽 어깨의 가장 큰 성분(월드)
    def rest_head(n):
        return mw @ arm.data.bones[n].head_local
    left = next(n for n in names if "LeftArm" in n or "LeftShoulder" in n)
    side = rest_head(left) - rest_head(counterpart(left, names))
    axis = max(range(3), key=lambda i: abs(side[i]))
    # 비춤 면의 위치: 쉼 자세 엉덩이(가운데 뼈)의 좌우 좌표
    hips = next(b for b in pbs if b.parent is None)
    center = rest_head(hips.name)[axis]
    S = Matrix.Identity(3); S[axis][axis] = -1.0

    def mirror_point(p):
        q = p.copy(); q[axis] = 2.0 * center - q[axis]; return q

    def mirror_rot(q):  # 반사 S로 켤레: S R S (회전 행렬로 계산)
        return (S @ q.to_matrix() @ S).to_quaternion()

    rest_rot = {b.name: (mw @ b.bone.matrix_local).to_quaternion() for b in pbs}

    # 1) 모든 프레임의 원래 월드 자세를 먼저 읽는다(키를 넣으면 커브가 바뀌므로).
    frames = {}
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        snap = {}
        for b in pbs:
            w = mw @ b.matrix
            loc, rot, scl = w.decompose()
            snap[b.name] = (loc, rot @ rest_rot[b.name].inverted(), scl)  # 쉼 자세 대비 월드 회전
        frames[f] = snap

    # 부모 → 자식 순서
    order = []
    def walk(b):
        order.append(b)
        for c in b.children:
            walk(c)
    for b in pbs:
        if b.parent is None:
            walk(b)

    # 2) 프레임마다 짝 뼈의 동작을 비춰 넣는다.
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        snap = frames[f]
        for b in order:
            loc_c, delta_c, _ = snap[counterpart(b.name, names)]
            _, _, scl = snap[b.name]
            rot = mirror_rot(delta_c) @ rest_rot[b.name]
            b.matrix = mw.inverted() @ Matrix.LocRotScale(mirror_point(loc_c), rot, scl)
            bpy.context.view_layer.update()
        for b in order:
            b.keyframe_insert("rotation_quaternion", frame=f)
            if b.parent is None:
                b.keyframe_insert("location", frame=f)

    # 확인: 비춘 뒤 짝 뼈 위치 오차(첫·가운데 프레임)
    worst = 0.0
    for f in (f0, (f0 + f1) // 2):
        scene.frame_set(f)
        for b in pbs:
            want = mirror_point(frames[f][counterpart(b.name, names)][0])
            worst = max(worst, ((mw @ b.matrix).to_translation() - want).length)
    print(f"[mirror_motion] frames {f0}-{f1}, axis {'XYZ'[axis]}, bones {len(order)}, max joint error {worst:.4f}")

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[mirror_motion] wrote {dst}")


main()

"""
반복 이동 모션(걷기·달리기)의 두 팔을 고르고 차분하게 만든다(생존자 이동이 우악스러워 보이던 것 — 한 팔만 흔들고 다른 팔은 굽힌 채 들고 있거나, 너무 크게 휘저음).
- 고르게(symmetric=1): 오른팔(어깨·위팔·아래팔·손·손가락)을 왼팔 동작을 몸 가운데 면에 비춘 것으로, 반 걸음(반 주기) 뒤 프레임에서 가져온다
  → 두 팔이 같은 크기로 다리와 엇갈려 흔들린다. (mirror_motion.py와 같은 계산 — 쉼 자세 대비 월드 회전을 비춤, 같은 파일 안에서)
- 차분하게(amp): 두 팔 뼈의 회전을 한 주기 평균 자세 쪽으로 당긴다(1 = 그대로, 0.7 = 흔들림 70%).
모션은 마지막 프레임 = 첫 프레임인 반복 모션이어야 한다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/balance_arms.py -- <원본.fbx> <출력.fbx> <amp> <symmetric 0|1>
"""
import bpy, sys
from mathutils import Matrix, Quaternion

ARM_PARTS = ("Shoulder", "Arm", "ForeArm", "Hand")


def is_arm(name, side):
    n = name.split(":")[-1]
    return n.startswith(side) and any(n[len(side):].startswith(p) for p in ARM_PARTS)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    src, dst, amp, sym = argv[0], argv[1], float(argv[2]), argv[3] == "1"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    a = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    scene = bpy.context.scene
    f0, f1 = (int(round(v)) for v in a.animation_data.action.frame_range)
    n = f1 - f0
    half = n / 2.0
    mw = a.matrix_world
    pbs = a.pose.bones
    for pb in pbs: pb.rotation_mode = 'QUATERNION'
    names = {pb.name for pb in pbs}

    order = []  # 부모 → 자식
    def walk(pb):
        order.append(pb)
        for c in pb.children: walk(c)
    for pb in pbs:
        if pb.parent is None: walk(pb)
    right = [pb.name for pb in order if is_arm(pb.name, "Right")]
    left = [pb.name for pb in order if is_arm(pb.name, "Left")]

    # 좌우 축·비춤(mirror_motion.py와 같음)
    def rest_head(nm): return mw @ a.data.bones[nm].head_local
    la = next(nm for nm in names if nm.endswith("LeftArm")); ra = la.replace("Left", "Right")
    side = rest_head(la) - rest_head(ra)
    axis = max(range(3), key=lambda i: abs(side[i]))
    S = Matrix.Identity(3); S[axis][axis] = -1.0
    def mirror_rot(q): return (S @ q.to_matrix() @ S).to_quaternion()
    rest_rot = {pb.name: (mw @ pb.bone.matrix_local).decompose()[1] for pb in pbs}

    # 1) 왼팔의 '쉼 자세 대비 월드 회전'을 반 주기 뒤에서 읽어 둔다
    donor = {}
    if sym:
        for f in range(f0, f1 + 1):
            g = f0 + ((f - f0 + half) % n)
            fi = int(g); scene.frame_set(fi, subframe=g - fi)
            donor[f] = {}
            for nm in right:
                src_nm = nm.replace("Right", "Left")
                if src_nm not in names: continue
                rot = (mw @ pbs[src_nm].matrix).decompose()[1]
                donor[f][nm] = mirror_rot(rot @ rest_rot[src_nm].inverted())
    # 2) 프레임마다 오른팔을 비춘 왼팔로(부모부터, 위치는 자기 사슬 그대로) → 로컬 회전을 읽어 둔다
    rot = {}
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        if sym:
            for nm in right:
                if nm not in donor[f]: continue
                pb = pbs[nm]
                loc, _, scl = (mw @ pb.matrix).decompose()
                pb.matrix = mw.inverted() @ Matrix.LocRotScale(loc, donor[f][nm] @ rest_rot[nm], scl)
                bpy.context.view_layer.update()
        rot[f] = {nm: pbs[nm].rotation_quaternion.copy() for nm in right + left}
    # 3) 한 주기 평균 쪽으로 당겨(차분하게) 키를 넣는다
    mean = {}
    for nm in right + left:
        ref = rot[f0][nm]; w = x = y = z = 0.0
        for f in range(f0, f1):
            q = rot[f][nm]
            if q.dot(ref) < 0: q = -q
            w += q.w; x += q.x; y += q.y; z += q.z
        mean[nm] = Quaternion((w, x, y, z)).normalized()
    for f in range(f0, f1 + 1):
        for nm in right + left:
            q = rot[f][nm]
            if q.dot(mean[nm]) < 0: q = -q
            pbs[nm].rotation_quaternion = mean[nm].slerp(q, amp)
            pbs[nm].keyframe_insert("rotation_quaternion", frame=f)
    scene.frame_start, scene.frame_end = f0, f1
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[balance_arms] frames {f0}-{f1} half {half} amp {amp} symmetric {sym} -> {dst}")


main()

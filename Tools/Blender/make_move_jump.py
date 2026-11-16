"""
생존자 '움직이며 점프' 모션 4방향을 새로 만든다(걸으면서 점프 Air_F를 대신 — 방향키를 누르며 점프하면 그쪽으로 자연스럽게 뛰어오름).
- 바탕 자세: 앞 걷기(Walk_F) 첫 프레임(Mixamo 뼈대, 정면을 봄 — 대기는 몸이 30° 틀어져 있어 안 씀). make_pallet_drop.py와 같은 방식(월드 방향으로 뼈를 겨눔).
- 프레임(30fps, 1~20 ≈ 게임 공중 시간 0.69초에 맞춰 재생): 1 도약 → 5 오름 → 10 꼭대기 → 15 내려옴(다리가 몸 밑으로) → 20 착지 준비(무릎 굽힘).
- Air_Move_F(앞): 앞 무릎을 들고 뒷다리를 접은 성큼 뛰기, 팔은 다리와 엇갈려 흔듦, 몸 살짝 앞으로.
- Air_Move_B(뒤): 두 무릎을 모아 접고 두 팔을 앞으로 내밀어 균형, 착지 때 발이 뒤로 먼저.
- Air_Move_L / Air_Move_R(옆): 두 다리를 접어 옆으로 깡충 — 가는 쪽 다리가 먼저 뻗고, 두 팔을 옆으로 벌려 균형.
- 머리는 내내 정면을 본다(시야 방향).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_move_jump.py -- <바탕 Walk_F.fbx> <출력 폴더>
"""
import bpy, sys, math, os
from mathutils import Matrix, Quaternion, Vector

FRAMES = (1, 5, 10, 15, 20)


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def poses(fwd, right, up, kind):
    """프레임별 (기울기 도, 기울일 방향, {뼈: 겨눌 방향}). 팔: 'LA'·'LF'(왼 위팔·아래팔) 'RA'·'RF', 다리: 'LT'·'LS'(왼 허벅지·정강이) 'RT'·'RS', 발 'LFt'·'RFt'."""
    out = {}
    toe = 0.8 * fwd - 0.6 * up  # 공중에서 발끝은 살짝 아래
    if kind == "F":
        t = fwd
        out[1] = (8, t, {"RT": 0.75 * fwd - 0.65 * up, "RS": -0.2 * fwd - up, "LT": -0.35 * fwd - up, "LS": -0.6 * fwd - 0.8 * up,
                          "LA": 0.35 * fwd - 0.9 * up - 0.12 * right, "LF": 0.75 * fwd + 0.55 * up, "RA": -0.45 * fwd - 0.85 * up + 0.12 * right, "RF": 0.15 * fwd - 0.95 * up})
        out[5] = (10, t, {"RT": 0.85 * fwd - 0.5 * up, "RS": -0.1 * fwd - up, "LT": -0.3 * fwd - up, "LS": -0.75 * fwd - 0.6 * up,
                          "LA": 0.45 * fwd - 0.85 * up - 0.15 * right, "LF": 0.7 * fwd + 0.65 * up, "RA": -0.5 * fwd - 0.8 * up + 0.15 * right, "RF": 0.2 * fwd - 0.95 * up})
        out[10] = (10, t, {"RT": 0.8 * fwd - 0.6 * up, "RS": -0.05 * fwd - up, "LT": -0.15 * fwd - up, "LS": -0.8 * fwd - 0.5 * up,
                           "LA": 0.35 * fwd - 0.9 * up - 0.18 * right, "LF": 0.75 * fwd + 0.45 * up, "RA": -0.35 * fwd - 0.9 * up + 0.18 * right, "RF": 0.25 * fwd - 0.95 * up})
        out[15] = (6, t, {"RT": 0.45 * fwd - 0.9 * up, "RS": -0.2 * fwd - up, "LT": 0.3 * fwd - 0.95 * up, "LS": -0.3 * fwd - up,
                          "LA": 0.15 * fwd - 0.9 * up - 0.35 * right, "LF": 0.65 * fwd - 0.4 * up - 0.15 * right, "RA": 0.05 * fwd - 0.9 * up + 0.35 * right, "RF": 0.6 * fwd - 0.45 * up + 0.15 * right})
        out[20] = (4, t, {"RT": 0.3 * fwd - up, "RS": -0.1 * fwd - up, "LT": 0.25 * fwd - up, "LS": -0.15 * fwd - up,
                          "LA": 0.15 * fwd - 0.95 * up - 0.2 * right, "LF": 0.35 * fwd - 0.9 * up, "RA": 0.1 * fwd - 0.95 * up + 0.2 * right, "RF": 0.35 * fwd - 0.9 * up})
    elif kind == "B":
        t = fwd  # 뒤로 갈 때 몸은 앞으로 살짝 숙여 균형
        arm = lambda k, o: {"LA": k * fwd - 0.9 * up - o * right, "LF": (k + 0.35) * fwd - 0.55 * up - 0.1 * right, "RA": k * fwd - 0.9 * up + o * right, "RF": (k + 0.35) * fwd - 0.55 * up + 0.1 * right}  # 팔은 살짝 앞·옆으로 벌려 균형(팔꿈치 조금 굽힘)
        out[1] = (6, t, {"LT": 0.25 * fwd - up, "LS": -0.35 * fwd - up, "RT": 0.25 * fwd - up, "RS": -0.35 * fwd - up, **arm(0.3, 0.2)})
        out[5] = (8, t, {"LT": 0.5 * fwd - 0.85 * up, "LS": -0.4 * fwd - 0.9 * up, "RT": 0.5 * fwd - 0.85 * up, "RS": -0.4 * fwd - 0.9 * up, **arm(0.35, 0.26)})
        out[10] = (8, t, {"LT": 0.55 * fwd - 0.8 * up, "LS": -0.4 * fwd - 0.9 * up, "RT": 0.5 * fwd - 0.85 * up, "RS": -0.45 * fwd - 0.9 * up, **arm(0.35, 0.28)})
        out[15] = (6, t, {"LT": 0.05 * fwd - up, "LS": -0.2 * fwd - up, "RT": -0.1 * fwd - up, "RS": -0.25 * fwd - up, **arm(0.25, 0.24)})
        out[20] = (4, t, {"LT": -0.05 * fwd - up, "LS": -0.15 * fwd - up, "RT": -0.1 * fwd - up, "RS": -0.15 * fwd - up, **arm(0.15, 0.18)})
    else:
        s = -right if kind == "L" else right  # 가는 쪽
        lead_t, lead_s, trail_t, trail_s = ("LT", "LS", "RT", "RS") if kind == "L" else ("RT", "RS", "LT", "LS")
        lead_a, lead_f, trail_a, trail_f = ("LA", "LF", "RA", "RF") if kind == "L" else ("RA", "RF", "LA", "LF")
        out[1] = (6, s, {lead_t: 0.35 * s + 0.25 * fwd - 0.85 * up, lead_s: -0.1 * s - up, trail_t: -0.35 * s - up, trail_s: -0.3 * s - up,
                         lead_a: 0.55 * s - 0.75 * up, lead_f: 0.7 * s - 0.6 * up, trail_a: -0.35 * s - 0.85 * up, trail_f: -0.3 * s - 0.9 * up})
        out[5] = (8, s, {lead_t: 0.3 * s + 0.35 * fwd - 0.85 * up, lead_s: -0.3 * fwd - up, trail_t: -0.1 * s + 0.3 * fwd - 0.9 * up, trail_s: -0.35 * fwd - up,
                         lead_a: 0.65 * s - 0.65 * up, lead_f: 0.7 * s - 0.5 * up + 0.2 * fwd, trail_a: -0.45 * s - 0.8 * up, trail_f: -0.4 * s - 0.85 * up})
        out[10] = (8, s, {lead_t: 0.2 * s + 0.35 * fwd - 0.9 * up, lead_s: -0.3 * fwd - up, trail_t: 0.35 * fwd - 0.9 * up, trail_s: -0.3 * fwd - up,
                          lead_a: 0.6 * s - 0.7 * up, lead_f: 0.65 * s - 0.55 * up + 0.2 * fwd, trail_a: -0.45 * s - 0.8 * up, trail_f: -0.4 * s - 0.85 * up})
        out[15] = (5, s, {lead_t: 0.3 * s - up, lead_s: 0.1 * s - up, trail_t: -0.05 * s + 0.15 * fwd - up, trail_s: -0.2 * s - up,
                          lead_a: 0.45 * s - 0.85 * up, lead_f: 0.5 * s - 0.8 * up, trail_a: -0.3 * s - 0.9 * up, trail_f: -0.25 * s - 0.95 * up})
        out[20] = (3, s, {lead_t: 0.2 * s + 0.1 * fwd - up, lead_s: -up, trail_t: -0.1 * s - up, trail_s: -up,
                          lead_a: 0.25 * s - 0.95 * up, lead_f: 0.3 * s - 0.9 * up, trail_a: -0.2 * s - 0.95 * up, trail_f: -0.2 * s - 0.95 * up})
    for f in out:  # 공중에서는 발끝을 살짝 아래로(착지 준비 프레임은 평평하게)
        out[f][2].setdefault("LFt", toe if f < 20 else fwd - 0.2 * up)
        out[f][2].setdefault("RFt", toe if f < 20 else fwd - 0.2 * up)
    return out


def build(src, dst, kind):
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
    # 몸 방향 = 어깨·골반 좌우 방향의 평균(Unity Humanoid 몸 방향과 같게) — 대기 자세는 어깨만 조금 틀어져 있다
    r1 = wpos(bone(pbs, "RightArm")) - wpos(bone(pbs, "LeftArm")); r2 = wpos(bone(pbs, "RightUpLeg")) - wpos(bone(pbs, "LeftUpLeg"))
    r1.z = 0; r2.z = 0
    right = (r1.normalized() + r2.normalized()).normalized()
    fwd = up.cross(right).normalized()
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
    bones = {"LA": bone(pbs, "LeftArm"), "LF": bone(pbs, "LeftForeArm"), "RA": bone(pbs, "RightArm"), "RF": bone(pbs, "RightForeArm"),
             "LT": bone(pbs, "LeftUpLeg"), "LS": bone(pbs, "LeftLeg"), "RT": bone(pbs, "RightUpLeg"), "RS": bone(pbs, "RightLeg"),
             "LFt": bone(pbs, "LeftFoot"), "RFt": bone(pbs, "RightFoot")}
    order = ["LT", "LS", "LFt", "RT", "RS", "RFt", "LA", "LF", "RA", "RF"]  # 부모 먼저
    table = poses(fwd, right, up, kind)
    for frame in FRAMES:
        lean, toward, aims = table[frame]
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()
        if lean:
            axis = up.cross(toward.normalized())
            if axis.length > 1e-4:
                rotate_world(spine, Quaternion(axis.normalized(), math.radians(lean)))
        for k in order:
            if k in aims:
                aim(bones[k], aims[k])
        w = mw @ head.matrix  # 머리는 정면
        loc, _, scl = w.decompose()
        head.matrix = mw.inverted() @ Matrix.LocRotScale(loc, base_head_world, scl)
        bpy.context.view_layer.update()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=frame)
            if b.parent is None:
                b.keyframe_insert("location", frame=frame)
    scene.frame_start, scene.frame_end = FRAMES[0], FRAMES[-1]
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_move_jump] {kind}: forward={tuple(round(v, 2) for v in fwd)} wrote {dst}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <base_idle.fbx> <out_dir>")
    src, outdir = argv[0], argv[1]
    for kind in ("F", "B", "L", "R"):
        build(src, os.path.join(outdir, f"Air_Move_{kind}.fbx"), kind)


main()

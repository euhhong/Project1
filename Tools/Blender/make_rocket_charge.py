"""
광대 로켓 돌진(E) 모션 두 개를 새로 만든다(바탕 = 대기 모션 FBX 첫 프레임, Mixamo 뼈대).
- Skill_SmileyFace_RocketCharge_Raise: E를 꾹 누르는 동안 — 로켓을 두 손으로 머리 위로 치켜든 상체 자세(반복, 게임에서는 상체만 쓰고 다리는 걷기).
- Skill_SmileyFace_RocketCharge: E를 떼면 돌진 — 상체를 앞으로 기울이고 두 손으로 로켓을 가슴 앞에 내밀어(머리가 정면) 들이받는 자세,
  무게중심을 낮추고 로켓을 껴안듯 두 팔로 감싸 안는다. 다리는 달리기 모션(Run_F)의 다리를 그대로 가져와 엇갈려 뛴다.
  상체는 떨림 없이 그대로(반복 = 달리기 한 걸음 주기).
- 머리는 내내 정면.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_rocket_charge.py -- <바탕 대기.fbx> <출력 폴더> <달리기.fbx(Run_F)>
"""
import bpy, sys, os, math
from mathutils import Matrix, Quaternion, Vector

# 머리 방향 보정(도): 바탕 대기 FBX와 게임 안 감시자 대기가 조금 달라서(make_grab_throw.py와 같은 값).
HEAD_YAW, HEAD_PITCH = 11.0, 12.0


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    for b in arm.pose.bones:
        b.rotation_mode = 'QUATERNION'
    bpy.context.scene.frame_set(int(arm.animation_data.action.frame_range[0]))
    bpy.context.view_layer.update()
    return arm


def build(src, dst, keys, frame_end):
    arm = load(src)
    mw = arm.matrix_world
    pbs = arm.pose.bones
    base_rot = {b.name: b.rotation_quaternion.copy() for b in pbs}
    base_loc = {b.name: b.location.copy() for b in pbs}
    wpos = lambda b: mw @ b.head
    up = Vector((0, 0, 1))
    right = wpos(bone(pbs, "RightArm")) - wpos(bone(pbs, "LeftArm")); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    head = bone(pbs, "Head")
    base_head = Quaternion(up, math.radians(HEAD_YAW)) @ Quaternion(right, math.radians(HEAD_PITCH)) @ (mw @ head.matrix).to_quaternion()
    arm.animation_data_clear()

    def rotate_world(b, q):
        loc, rot, scl = (mw @ b.matrix).decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, q @ rot, scl)
        bpy.context.view_layer.update()

    def aim(name, direction):
        b = bone(pbs, name)
        cur = ((mw @ b.matrix).to_3x3() @ Vector((0, 1, 0))).normalized()
        rotate_world(b, cur.rotation_difference(direction.normalized()))

    def lean(name, deg):  # + = 앞으로 숙임
        rotate_world(bone(pbs, name), Quaternion(right, -math.radians(deg)))

    def set_local(name, q):
        bone(pbs, name).rotation_quaternion = q
        bpy.context.view_layer.update()

    def set_hips(rot, loc):
        h = next(b for b in pbs if b.parent is None)
        h.rotation_quaternion = rot; h.location = loc
        bpy.context.view_layer.update()

    ctx = dict(aim=aim, lean=lean, set_local=set_local, set_hips=set_hips, fwd=fwd, up=up, right=right)
    for frame, pose in keys.items():
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()
        pose(ctx)
        loc, _, scl = (mw @ head.matrix).decompose()
        head.matrix = mw.inverted() @ Matrix.LocRotScale(loc, base_head, scl)  # 머리는 정면
        bpy.context.view_layer.update()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=frame)
            if b.parent is None:
                b.keyframe_insert("location", frame=frame)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, frame_end
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_rocket_charge] wrote {dst}")


def raise_pose(lift):
    """두 팔을 머리 위로 들어 두 손이 머리 위에서 만난다(로켓을 치켜듦). lift: 조금씩 오르내림(0~1)."""
    def pose(c):
        f, u, r = c["fwd"], c["up"], c["right"]
        c["lean"]("Spine1", -3.0)  # 가슴을 살짝 편다
        for side, s in (("Right", 1.0), ("Left", -1.0)):
            c["aim"](side + "Arm", (0.85 + 0.05 * lift) * u + 0.32 * s * r + 0.12 * f)
            c["aim"](side + "ForeArm", 0.45 * u - 0.85 * s * r + 0.1 * f)
            c["aim"](side + "Hand", 0.6 * u - 0.7 * s * r)
    return pose


LEG_BONES = ("LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase")


def sample_run_legs(run_fbx):
    """달리기 모션(Run_F)의 프레임마다 다리 뼈 로컬 회전을 읽는다(같은 Mixamo 뼈대)."""
    arm = load(run_fbx)
    f0, f1 = (int(round(v)) for v in arm.animation_data.action.frame_range)
    frames = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        hips = next(b for b in arm.pose.bones if b.parent is None)
        d = {n: bone(arm.pose.bones, n).rotation_quaternion.copy() for n in LEG_BONES}
        d["@hips_rot"] = hips.rotation_quaternion.copy(); d["@hips_loc"] = hips.location.copy()  # 엉덩이 높이·흔들림도 달리기대로(발이 땅을 뚫지 않게)
        frames.append(d)
    return frames


def ram_pose(legs):
    """무게중심을 낮추고(상체를 앞으로 크게 숙임) 로켓을 껴안듯 두 팔로 감싸 안고 들이받는다.
    다리는 달리기 모션(legs = 그 프레임의 다리 로컬 회전)으로 엇갈려 뛴다. 상체는 떨림 없이 그대로."""
    def pose(c):
        f, u, r = c["fwd"], c["up"], c["right"]
        c["set_hips"](legs["@hips_rot"], legs["@hips_loc"])
        for n, q in legs.items():
            if n.startswith("@"): continue
            c["set_local"](n, q)
        c["lean"]("Spine", 20.0)
        c["lean"]("Spine1", 12.0)
        c["lean"]("Spine2", 6.0)
        for side, s in (("Right", 1.0), ("Left", -1.0)):
            c["aim"](side + "Arm", 0.45 * f - 0.75 * u + 0.35 * s * r)       # 위팔: 앞·아래로, 바깥으로 벌려 감쌀 자리
            c["aim"](side + "ForeArm", 0.55 * f + 0.05 * u - 0.85 * s * r)  # 아래팔: 가슴 밑에서 안쪽으로 감아 로켓을 껴안음
            c["aim"](side + "Hand", 0.2 * f + 0.4 * u - 0.9 * s * r)        # 손: 로켓을 위로 감싸 쥠
    return pose


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 3:
        raise SystemExit("usage: -- <base_idle.fbx> <out_dir> <run.fbx>")
    src, out, run = argv[0], argv[1], argv[2]
    build(src, os.path.join(out, "Skill_SmileyFace_RocketCharge_Raise.fbx"),
          {1: raise_pose(0.0), 16: raise_pose(1.0), 31: raise_pose(0.0)}, 31)
    legs = sample_run_legs(run)
    build(src, os.path.join(out, "Skill_SmileyFace_RocketCharge.fbx"),
          {i + 1: ram_pose(l) for i, l in enumerate(legs)}, len(legs))  # 달리기 한 걸음 주기(반복)


main()

"""
공군 신호총 쏘기 모션을 만든다(바탕 = Mixamo "Turn Left 45 Degrees" — 그대로 쓰지 않고 바꿔 쓴다).
- 몸 돌리기: 바탕의 왼쪽으로 도는 발걸음(1~19프레임)을 앞 7프레임(0.2초, 겨누는 시간)에 줄여 넣어 몸을 옆으로 틀고,
  남은 발걸음(19~36)은 뒤로 늘여 쏜 뒤 자세를 다듬는 데 쓴다.
- 오른팔: 겨누는 동안 처음 바라보던 방향(목표 쪽, 살짝 위 pitch_up도)으로 곧게 들어 올린다 → 7프레임에 쏨
  → 8~10 반동으로 위로 튐 → 11~15 겨눈 채 → 16~24 내린다.
- 머리: 겨누는 동안 목표(처음 앞)를 본다.
길이 24프레임(0.8초) — FlareShotSkill의 windup 0.2 + afterShot 0.6에 맞춘다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_flare_shot.py -- <Turn Left 45 Degrees.fbx> <출력.fbx> [pitch_up=12] [aim_yaw=0]
  aim_yaw(도): 게임 안에서 팔이 목표에서 비껴 보이면 그만큼 돌린다(+ = 왼쪽).
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector

FRAMES = 24
FIRE = 7


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <turn.fbx> <out.fbx> [pitch_up] [aim_yaw]")
    src, dst = argv[0], argv[1]
    pitch_up = float(argv[2]) if len(argv) > 2 else 12.0
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

    def source_frame(k):
        # 1..FIRE ← 바탕 1..19(빠르게 몸을 틂), FIRE..FRAMES ← 바탕 19..36(쏜 뒤 발걸음 마무리)
        if k <= FIRE:
            return s0 + (k - 1) * (19 - s0) / (FIRE - 1)
        return 19 + (k - FIRE) * (s1 - 19) / (FRAMES - FIRE)

    def sample(f):
        scene.frame_set(int(math.floor(f)), subframe=f - math.floor(f))
        bpy.context.view_layer.update()
        return ({b.name: b.rotation_quaternion.copy() for b in pbs}, {b.name: b.location.copy() for b in pbs})

    # 처음 자세에서 몸 방향(목표 = 처음 앞)과 머리 방향
    scene.frame_set(s0); bpy.context.view_layer.update()
    up = Vector((0, 0, 1))
    right = (mw @ bone(pbs, "RightArm").head) - (mw @ bone(pbs, "LeftArm").head); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    fwd = Quaternion(up, math.radians(aim_yaw)) @ fwd
    side = up.cross(fwd).normalized() * -1  # 목표 기준 오른쪽
    aim = (Quaternion(side, math.radians(pitch_up)) @ fwd).normalized()
    head = bone(pbs, "Head")
    head0 = (mw @ head.matrix).to_quaternion()

    samples = {k: sample(source_frame(k)) for k in range(1, FRAMES + 1)}
    arm.animation_data_clear()

    def rotate_world(b, q):
        loc, rot, scl = (mw @ b.matrix).decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, q @ rot, scl)
        bpy.context.view_layer.update()

    def aim_bone(name, direction, weight):
        b = bone(pbs, name)
        cur = ((mw @ b.matrix).to_3x3() @ Vector((0, 1, 0))).normalized()
        full = cur.rotation_difference(direction.normalized())
        rotate_world(b, Quaternion().slerp(full, weight))

    def arm_dir(k):
        """오른팔이 향할 방향과 그 정도(0 = 바탕 그대로, 1 = 완전히 겨눔)."""
        if k <= FIRE:
            return aim, smooth((k - 1) / (FIRE - 1))
        kick = {FIRE + 1: 14.0, FIRE + 2: 9.0, FIRE + 3: 4.0}.get(k, 0.0)  # 반동
        d = (Quaternion(side, math.radians(kick)) @ aim).normalized()
        if k <= 15:
            return d, 1.0
        return d, 1.0 - smooth((k - 15) / (FRAMES - 15))

    for k in range(1, FRAMES + 1):
        rot, loc = samples[k]
        for b in pbs:
            b.rotation_quaternion = rot[b.name]
            b.location = loc[b.name]
        bpy.context.view_layer.update()
        d, w = arm_dir(k)
        if w > 0.001:
            aim_bone("RightArm", d, w)
            aim_bone("RightForeArm", d, w)
            aim_bone("RightHand", d, w)
            # 머리: 겨누는 만큼 목표(처음 앞)를 본다
            loc_h, rot_h, scl_h = (mw @ head.matrix).decompose()
            head.matrix = mw.inverted() @ Matrix.LocRotScale(loc_h, rot_h.slerp(head0, w), scl_h)
            bpy.context.view_layer.update()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=k)
            if b.parent is None:
                b.keyframe_insert("location", frame=k)

    scene.frame_start, scene.frame_end = 1, FRAMES
    scene.frame_set(FIRE)
    hand = mw @ bone(pbs, "RightHand").head; sh = mw @ bone(pbs, "RightArm").head
    v = (hand - sh).normalized()
    print(f"[make_flare_shot] fire frame {FIRE}: arm dir·aim={v.dot(aim):.3f} (1 = 목표로 곧게), source frames {s0}-{s1}")
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_flare_shot] wrote {dst}")


main()

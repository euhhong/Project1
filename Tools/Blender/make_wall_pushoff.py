"""
용병 아대 도약 모션을 새로 만든다: 벽을 짚은 자세에서 두 손으로 벽을 밀어내며 튕겨 나간다.
- 바탕 자세: 벽 짚는 자세 FBX(Skill_Mercenary_Guard_Wall, Male Action Pose)의 첫 프레임.
- 프레임(30fps): 1 벽 짚음 → 4 두 팔을 끝까지 뻗어 벽을 밀어냄(가슴이 뒤로 젖혀지기 시작)
  → 10 벽에서 튕겨 나가듯 몸 전체가 뒤로 28° 젖혀짐(두 팔은 앞에 남은 채 살짝 풀림). 도약 시간(0.3초)과 맞춘다.
  벽에 부딪힌 뒤 보통 뒤·옆으로 도약하므로(벽은 앞) 벽을 밀어내고 물러나는 모양으로 만든다.
- 머리는 벽(앞)을 본 채.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_wall_pushoff.py -- <벽 짚는 자세.fbx> <출력.fbx>
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <wall_pose.fbx> <out.fbx>")
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

    def wpos(b):
        return mw @ b.head
    up = Vector((0, 0, 1))
    right = wpos(bone(pbs, "RightArm")) - wpos(bone(pbs, "LeftArm")); right.z = 0; right.normalize()
    fwd = up.cross(right).normalized()
    head = bone(pbs, "Head")
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

    def bone_dir(b):
        return ((mw @ b.matrix).to_3x3() @ Vector((0, 1, 0))).normalized()

    spine = bone(pbs, "Spine")
    hips = next(b for b in pbs if b.parent is None)
    sides = [(bone(pbs, s + "Arm"), bone(pbs, s + "ForeArm"), bone(pbs, s + "Hand")) for s in ("Right", "Left")]
    # 벽 짚는 자세에서 손이 향한 방향(어깨 → 손) = 미는 방향
    push = {}
    for up_b, lo_b, hand_b in sides:
        d = wpos(hand_b) - wpos(up_b)
        push[up_b.name] = d.normalized()

    def reset():
        for b in pbs:
            b.rotation_quaternion = base_rot[b.name]
            b.location = base_loc[b.name]
        bpy.context.view_layer.update()

    def head_forward(pitch_deg):
        # 진행 방향(앞)을 보게: 머리 뼈 방향은 위, 얼굴은 앞. 몸이 기울어도 고개를 들어 앞을 본다.
        w = mw @ head.matrix
        loc, rot, scl = w.decompose()
        target = Quaternion(right, -math.radians(pitch_deg)) @ base_head
        head.matrix = mw.inverted() @ Matrix.LocRotScale(loc, target, scl)
        bpy.context.view_layer.update()

    reset()
    base_head = (mw @ head.matrix).to_quaternion()

    keys = {}
    # 1: 벽 짚음(바탕 그대로)
    keys[1] = lambda: None

    def push_out():
        rotate_world(hips, Quaternion(right, math.radians(10)))          # 밀면서 몸 전체가 뒤로 일어나기 시작(벽 짚은 자세는 앞으로 기울어 있음)
        for up_b, lo_b, hand_b in sides:
            d = push[up_b.name]
            aim(up_b, d); aim(lo_b, d); aim(hand_b, d)                    # 팔을 끝까지 뻗어 밀어냄
        head_forward(0)
    keys[4] = push_out

    def launch():
        rotate_world(hips, Quaternion(right, math.radians(28)))          # 벽에서 튕겨 나가듯 몸 전체가 뒤로 젖혀짐
        rotate_world(spine, Quaternion(right, math.radians(8)))
        for side, (up_b, lo_b, hand_b) in zip((1, -1), sides):
            aim(up_b, 0.85 * fwd - 0.35 * up + 0.2 * side * right)       # 밀어낸 팔은 앞에 남은 채 살짝 풀림
            aim(lo_b, 0.8 * fwd - 0.25 * up + 0.1 * side * right)
            aim(hand_b, 0.8 * fwd + 0.2 * up)
        head_forward(0)                                                   # 벽(앞)을 본 채
    keys[10] = launch

    for frame, pose in keys.items():
        reset()
        pose()
        for b in pbs:
            b.keyframe_insert("rotation_quaternion", frame=frame)
            if b.parent is None:
                b.keyframe_insert("location", frame=frame)

    scene.frame_start, scene.frame_end = 1, 10
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_wall_pushoff] forward={tuple(round(v, 2) for v in fwd)} push R={tuple(round(v, 2) for v in push[sides[0][0].name])} L={tuple(round(v, 2) for v in push[sides[1][0].name])}")
    print(f"[make_wall_pushoff] wrote {dst}")


main()

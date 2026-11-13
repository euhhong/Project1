"""
대기 모션의 상체를 편하게 앞으로 구부린 새 대기 모션을 만든다(루키노처럼 사람보다 큰 돌연변이).
- 프레임마다 척추 세 마디(Spine, Spine1, Spine2)를 앞으로 hunch/3도씩 숙인다(호흡 같은 원래 움직임은 그대로).
- 머리는 원래 대기의 월드 방향 그대로(구부려도 앞을 본다), 위팔도 원래 월드 방향 그대로(팔은 편하게 아래로 늘어짐).
- 하체(엉덩이·다리)는 건드리지 않는다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_hunched_idle.py -- <입력 대기.fbx> <출력.fbx> [hunch=24]
"""
import bpy, sys, math
from mathutils import Matrix, Quaternion, Vector


def bone(pbs, suffix):
    return next(b for b in pbs if b.name.endswith(suffix))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if len(argv) < 2:
        raise SystemExit("usage: -- <in.fbx> <out.fbx> [hunch]")
    src, dst = argv[0], argv[1]
    hunch = float(argv[2]) if len(argv) > 2 else 24.0

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    scene = bpy.context.scene
    mw = arm.matrix_world
    pbs = arm.pose.bones
    f0, f1 = (int(round(v)) for v in arm.animation_data.action.frame_range)
    scene.frame_start, scene.frame_end = f0, f1

    spines = [bone(pbs, n) for n in ("Spine", "Spine1", "Spine2")]
    head = bone(pbs, "Head")
    uppers = [bone(pbs, "RightArm"), bone(pbs, "LeftArm")]
    keep = [head] + uppers
    for b in spines + keep:
        b.rotation_mode = 'QUATERNION'

    def world(b):
        return mw @ b.matrix

    def set_world_rot(b, rot):
        loc, _, scl = world(b).decompose()
        b.matrix = mw.inverted() @ Matrix.LocRotScale(loc, rot, scl)
        bpy.context.view_layer.update()

    # 1) 원래 머리·위팔의 월드 회전을 모든 프레임 먼저 읽는다.
    orig = {}
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        orig[f] = {b.name: world(b).to_quaternion() for b in keep}

    # 2) 프레임마다 척추를 앞으로 숙이고, 머리·위팔은 원래 월드 방향으로 되돌린다.
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        r = (mw @ bone(pbs, "RightArm").head) - (mw @ bone(pbs, "LeftArm").head)
        r.z = 0; r.normalize()
        step = Quaternion(r, -math.radians(hunch / len(spines)))  # 오른쪽축 둘레 - 회전 = 앞으로 숙임
        for s in spines:
            loc, rot, scl = world(s).decompose()
            s.matrix = mw.inverted() @ Matrix.LocRotScale(loc, step @ rot, scl)
            bpy.context.view_layer.update()
        for b in keep:
            set_world_rot(b, orig[f][b.name])
        for b in spines + keep:
            b.keyframe_insert("rotation_quaternion", frame=f)

    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[make_hunched_idle] frames {f0}-{f1}, hunch {hunch} deg, wrote {dst}")


main()

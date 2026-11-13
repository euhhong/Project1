"""
모션을 앞뒤로 뒤집어(옆 축 X로 180도) 등이 아래, 얼굴이 하늘을 보게 만든다. 머리 방향도 앞 ↔ 뒤로 바뀐다.
예: Mixamo Falling(엎드려 허우적대며 떨어짐) → 하늘을 보며 등으로 떨어짐(뒤로 걷다가 떨어질 때, 머리가 떨어지는 쪽 = 뒤).
Hips(루트 뼈)의 매 프레임 월드 자세를 Hips 높이 평균을 축으로 돌리고 다시 키를 박는다(자식 뼈는 따라온다).
Unity에서는 이 모션의 Root Rotation을 Original 기준으로 가져온다(MixamoMotionPostprocessor.OriginalOrientation).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/flip_on_back.py -- <입력.fbx> <출력.fbx>
"""
import bpy, sys
from mathutils import Matrix, Vector


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    src, dst = args[0], args[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, automatic_bone_orientation=False, ignore_leaf_bones=False)
    sc = bpy.context.scene
    arm = next(o for o in sc.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    f0, f1 = (int(x) for x in act.frame_range)
    hips = next(b for b in arm.pose.bones if b.parent is None)
    mw = arm.matrix_world

    # 원래 Hips 월드 자세를 먼저 모은다.
    worlds = {}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        worlds[f] = (mw @ hips.matrix).copy()
    pivot = sum((m.to_translation() for m in worlds.values()), Vector()) / len(worlds)
    flip = Matrix.Translation(pivot) @ Matrix.Rotation(3.141592653589793, 4, 'X') @ Matrix.Translation(-pivot)

    hips.rotation_mode = 'QUATERNION'
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        hips.matrix = mw.inverted() @ (flip @ worlds[f])
        hips.keyframe_insert("location", frame=f)
        hips.keyframe_insert("rotation_quaternion", frame=f)

    sc.frame_set(f0)
    head = next(b for b in arm.pose.bones if b.name.endswith("Head"))
    print(f"[flip_on_back] frames {f0}-{f1}, pivot {tuple(round(x, 2) for x in pivot)}, head at f0 {tuple(round(x, 2) for x in (mw @ head.head))}")
    bpy.ops.export_scene.fbx(
        filepath=dst, use_selection=False, object_types={'ARMATURE'},
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        armature_nodetype='NULL', apply_unit_scale=True)
    print(f"[flip_on_back] wrote {dst}")


main()

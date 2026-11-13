"""
Mixamo 모션의 한 구간을 거꾸로 재생하는 모션으로 만든다(예: 맹인 지팡이 왼쪽 후리기 → 오른쪽 후리기).
사용: blender -b --python Tools/Blender/reverse_motion.py -- <in.fbx> <out.fbx> <시작 프레임> <끝 프레임>
구간 [a, b]의 프레임 t를 a + b - t로 옮기고, 구간 밖 키는 지운다. 내보내면 프레임이 1 밀린다(Unity 자르기 구간 +1).
"""
import bpy, sys
args = sys.argv[sys.argv.index("--") + 1:]
src, out, a, b = args[0], args[1], float(args[2]), float(args[3])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
act = arm.animation_data.action

def curves(action):
    if hasattr(action, "fcurves") and len(action.fcurves) > 0:
        return list(action.fcurves)
    res = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags:
                res.extend(bag.fcurves)
    return res

n = 0
for fc in curves(act):
    keys = [(k.co[0], k.co[1]) for k in fc.keyframe_points if a - 0.01 <= k.co[0] <= b + 0.01]
    if not keys:
        continue
    for i in range(len(fc.keyframe_points) - 1, -1, -1):
        fc.keyframe_points.remove(fc.keyframe_points[i])
    for t, v in sorted(((a + b - t, v) for t, v in keys)):
        kp = fc.keyframe_points.insert(t, v)
        kp.interpolation = 'LINEAR'
    n += 1
bpy.context.scene.frame_start = int(a); bpy.context.scene.frame_end = int(b)
print("REVERSED", n, "curves", a, b)
for o in bpy.context.scene.objects: o.select_set(o == arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                         add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL')
print("DONE")

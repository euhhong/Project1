"""
Mixamo 모션의 한 구간을 앞으로 갔다가 거꾸로 돌아오게(왕복) 이어 반복용 모션을 만든다 — 처음과 끝이 같아 끊김 없이 반복된다.
예: 하스터 이동 = Zombie Attack 앞부분(몸을 숙였다 펴며 꾸물대는 부분, 발이 없어 미끄러지듯 이동).
사용: blender -b --python Tools/Blender/make_pingpong.py -- <in.fbx> <out.fbx> <시작> <끝> <배속>
내보낸 모션은 프레임 1부터 시작한다(Unity에서 자르기 없이 전체를 쓴다).
"""
import bpy, sys, math
args = sys.argv[sys.argv.index("--") + 1:]
src, out, a, b, speed = args[0], args[1], float(args[2]), float(args[3]), float(args[4])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
def curves(action):
    if hasattr(action, "fcurves") and len(action.fcurves) > 0: return list(action.fcurves)
    res = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags: res.extend(bag.fcurves)
    return res
L = b - a
for fc in curves(arm.animation_data.action):
    pts = [(k.co[0], k.co[1]) for k in fc.keyframe_points if a - 0.01 <= k.co[0] <= b + 0.01]
    if not pts: continue
    for i in range(len(fc.keyframe_points) - 1, -1, -1): fc.keyframe_points.remove(fc.keyframe_points[i])
    seq = [(t - a, v) for t, v in pts] + [(2 * L - (t - a), v) for t, v in reversed(pts) if (t - a) < L - 0.01]
    for t, v in seq:
        kp = fc.keyframe_points.insert(1 + t / speed, v); kp.interpolation = 'LINEAR'
sc = bpy.context.scene; sc.frame_start = 1; sc.frame_end = int(math.ceil(1 + 2 * L / speed))
print("OUT frames 1 ..", round(1 + 2 * L / speed, 2))
for o in sc.objects: o.select_set(o == arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                         add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL')
print("DONE")

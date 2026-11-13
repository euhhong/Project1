"""
Mixamo 모션에서 가장 자연스럽게 이어지는(처음·끝 자세가 가장 비슷한) 구간을 골라 잘라 내고, 배속을 바꿔 반복용 모션으로 내보낸다.
예: 안토니오 대기 = Playing The Violin의 활 긋기 부분만, 1.5배속.
사용: blender -b --python Tools/Blender/make_loop_clip.py -- <in.fbx> <out.fbx> <찾을 시작> <찾을 끝> <최소 길이> <최대 길이> <배속>
내보내면 프레임이 1부터 시작한다(Unity에서 자르기 없이 전체를 쓴다).
"""
import bpy, sys, math
args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
s0, s1, minLen, maxLen, speed = int(args[2]), int(args[3]), int(args[4]), int(args[5]), float(args[6])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
names = [b.name for b in arm.pose.bones]
def find(s): return next(n for n in names if n.lower().endswith(s.lower()))
keys = [find(k) for k in ("Hips", "Head", "RightHand", "LeftHand", "RightForeArm", "LeftForeArm", "Spine2", "RightHandIndex1")]
hips = keys[0]
sc = bpy.context.scene
poses = {}
for f in range(s0, s1 + 1):
    sc.frame_set(f)
    h = arm.pose.bones[hips].head
    poses[f] = [arm.pose.bones[k].head - h for k in keys[1:]]
def dist(a, b): return sum((x - y).length for x, y in zip(poses[a], poses[b]))
best = None
for a in range(s0, s1 - minLen + 1, 2):
    for b in range(a + minLen, min(a + maxLen, s1) + 1, 2):
        d = dist(a, b) + 0.0005 * 0  # 처음·끝 자세 차이
        # 속도(앞뒤 1프레임 차이)도 비슷해야 자연스럽다
        if a + 1 in poses and b + 1 in poses:
            va = [x - y for x, y in zip(poses[a + 1], poses[a])]; vb = [x - y for x, y in zip(poses[b + 1], poses[b])]
            d += 2.0 * sum((x - y).length for x, y in zip(va, vb))
        if best is None or d < best[0]: best = (d, a, b)
d, a, b = best
print("BEST", a, b, "len", b - a, "score", round(d, 4))

def curves(action):
    if hasattr(action, "fcurves") and len(action.fcurves) > 0: return list(action.fcurves)
    res = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags: res.extend(bag.fcurves)
    return res
act = arm.animation_data.action
for fc in curves(act):
    pts = [(k.co[0], k.co[1]) for k in fc.keyframe_points if a - 0.01 <= k.co[0] <= b + 0.01]
    for i in range(len(fc.keyframe_points) - 1, -1, -1): fc.keyframe_points.remove(fc.keyframe_points[i])
    for t, v in pts:
        kp = fc.keyframe_points.insert(1 + (t - a) / speed, v); kp.interpolation = 'LINEAR'
end = 1 + (b - a) / speed
sc.frame_start = 1; sc.frame_end = int(math.ceil(end))
print("OUT frames 1 ..", round(end, 2))
for o in sc.objects: o.select_set(o == arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                         add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL')
print("DONE")

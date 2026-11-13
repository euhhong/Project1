"""
하스터 소환 촉수: Hastur_Tentacle.fbx에서 텍스처가 입혀진 촉수 하나를 꺼내 지렁이처럼 리깅하고(뼈 N개 사슬), 모션 4개를 만든다.
사용: blender -b --python Tools/Blender/rig_tentacle.py -- <Hastur_Tentacle.fbx> <out.fbx>

- 메시: 오른쪽(+X) 촉수만 남긴다(왼쪽 것은 텍스처가 없는 다른 재질). 밑동 가운데를 원점에, 끝이 +Z(위)로, 키 1(Unity에서 크기로 키운다).
- 뼈: Root(밑동) 아래로 Seg0 ~ Seg{N-1}이 +Z 방향 사슬. 높이에 따라 이웃한 두 뼈에 무게를 나눠 준다(지렁이처럼 마디마다 부드럽게 굽는다).
- 앞 = Blender -Y(Unity로 내보내면 +Z). 뼈를 로컬 X축으로 + 돌리면 앞으로 굽는다.
- 모션(30fps):
  Rise  0~18  땅속(-1.05)에서 꿈틀거리며 솟아오른다(끝에서 살짝 넘쳤다 섬)
  Idle  0~60  마디를 타고 흐르는 물결로 꿈틀거림(반복)
  Slam  0~30  뒤로 젖히며 몸을 말아 올림(0~12) → 앞으로 내리쳐 땅에 눕는다(12~17) → 누운 채(17~22) → 다시 섬(22~30)
  Sink  0~15  꿈틀거리며 땅속으로 들어간다
"""
import bpy, sys, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
N = 8

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sc = bpy.context.scene
sc.render.fps = 30

mesh = [o for o in sc.objects if o.type == 'MESH'][0]
# 원래 뼈·무게를 떼어 낸다(변형된 모양 그대로 굳힌다).
bpy.context.view_layer.objects.active = mesh
for o in sc.objects: o.select_set(o == mesh)
for m in list(mesh.modifiers):
    if m.type == 'ARMATURE': mesh.modifiers.remove(m)
mw = mesh.matrix_world.copy()
mesh.parent = None
mesh.matrix_world = mw
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh.vertex_groups.clear()
for o in [o for o in sc.objects if o.type == 'ARMATURE']: bpy.data.objects.remove(o, do_unlink=True)

# 오른쪽 촉수만 남긴다.
import bmesh
bm = bmesh.new(); bm.from_mesh(mesh.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.x < 0.0], context='VERTS')
bm.to_mesh(mesh.data); bm.free()
mats = [m for m in mesh.data.materials if m is not None and 'zhujian' in m.name]
mesh.data.materials.clear()
mat = mats[0] if mats else bpy.data.materials.new("HasturTentacle")
mat.name = "HasturTentacle"
mesh.data.materials.append(mat)
for p in mesh.data.polygons: p.material_index = 0

# 밑동 가운데 → 원점, 키 1.
vs = [v.co for v in mesh.data.vertices]
zmin = min(v.z for v in vs); zmax = max(v.z for v in vs)
base = [v for v in vs if v.z < zmin + 0.05 * (zmax - zmin)]
cx = sum(v.x for v in base) / len(base); cy = sum(v.y for v in base) / len(base)
h = zmax - zmin
for v in mesh.data.vertices:
    v.co = Vector(((v.co.x - cx) / h, (v.co.y - cy) / h, (v.co.z - zmin) / h))
mesh.name = "HasturTentacle"; mesh.data.name = "HasturTentacle"

# 뼈 사슬.
arm_data = bpy.data.armatures.new("TentacleRig")
arm = bpy.data.objects.new("TentacleRig", arm_data)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
for o in sc.objects: o.select_set(o == arm)
bpy.ops.object.mode_set(mode='EDIT')
root = arm_data.edit_bones.new("Root")
root.head = (0, 0, -0.05); root.tail = (0, 0, 0.0); root.roll = 0
prev = root
for i in range(N):
    b = arm_data.edit_bones.new(f"Seg{i}")
    b.head = (0, 0, i / N); b.tail = (0, 0, (i + 1) / N); b.roll = 0
    b.parent = prev; b.use_connect = i > 0
    prev = b
bpy.ops.object.mode_set(mode='OBJECT')

# 무게: 높이 t(0~1)에서 마디 중심 사이를 선형으로 나눈다.
groups = [mesh.vertex_groups.new(name=f"Seg{i}") for i in range(N)]
for v in mesh.data.vertices:
    s = max(0.0, min(N - 1.0, v.co.z * N - 0.5))
    i0 = int(math.floor(s)); i1 = min(N - 1, i0 + 1); f = s - i0
    groups[i0].add([v.index], 1.0 - f, 'REPLACE')
    if i1 != i0 and f > 0: groups[i1].add([v.index], f, 'REPLACE')
mesh.parent = arm
mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm

# ---------- 모션 ----------
pbones = [arm.pose.bones[f"Seg{i}"] for i in range(N)]
proot = arm.pose.bones["Root"]
for pb in pbones + [proot]: pb.rotation_mode = 'XYZ'

def key_pose(frame, root_z, bends, sways):
    proot.location = (0, 0, 0); proot.rotation_euler = (0, 0, 0)
    # Root 뼈의 로컬 Y = 위(+Z). 위아래로 옮길 때는 로컬 Y.
    proot.location = (0, root_z, 0)
    proot.keyframe_insert("location", frame=frame)
    proot.keyframe_insert("rotation_euler", frame=frame)
    for i, pb in enumerate(pbones):
        pb.rotation_euler = (math.radians(bends[i]), 0, math.radians(sways[i]))
        pb.keyframe_insert("rotation_euler", frame=frame)

def wave(t, amp, freq=1.0, phase_step=0.9, lateral=0.6):
    bends = [amp * (0.4 + 0.6 * i / (N - 1)) * math.sin(2 * math.pi * freq * t - i * phase_step) for i in range(N)]
    sways = [lateral * amp * (0.4 + 0.6 * i / (N - 1)) * math.cos(2 * math.pi * freq * t * 0.8 - i * phase_step) for i in range(N)]
    return bends, sways

def new_action(name):
    act = bpy.data.actions.new(name)
    arm.animation_data_create()
    arm.animation_data.action = act
    return act

actions = []
# Rise
act = new_action("Rise")
for f in range(0, 19):
    t = f / 18.0
    e = 1 - (1 - t) ** 3                      # 빠르게 솟았다 느려짐
    z = -1.05 + 1.05 * e + 0.06 * math.sin(math.pi * t) * (t > 0.6)
    b, s = wave(f / 30.0, 14 * (1 - 0.6 * t), freq=1.6)
    key_pose(f, z, b, s)
actions.append(act)
# Idle (반복: 2초에 정확히 두 번 흐름)
act = new_action("Idle")
for f in range(0, 61, 2):
    b, s = wave(f / 30.0, 9, freq=1.0)
    key_pose(f, 0.0, b, s)
actions.append(act)
# Slam
act = new_action("Slam")
def slam_pose(f):
    if f <= 12:     # 뒤로 젖히며 말아 올림
        k = math.sin(0.5 * math.pi * f / 12)
        bends = [-(10 + 16 * i / (N - 1)) * k for i in range(N)]
    elif f <= 17:   # 앞으로 내리침: 밑동이 크게 꺾이고 몸이 땅에 눕는다
        k = (f - 12) / 5.0
        k = k * k * (3 - 2 * k)
        down = [80, 6, 3, 2, 2, 1, 1, 1]
        back = [-(10 + 16 * i / (N - 1)) for i in range(N)]
        bends = [back[i] + (down[i] - back[i]) * k for i in range(N)]
    elif f <= 22:   # 누운 채 꿈틀
        bends = [80 + 2 * math.sin(f), 6, 3, 2, 2, 1, 1, 1 + 3 * math.sin(f * 1.7)]
    else:           # 다시 섬
        k = (f - 22) / 8.0
        k = k * k * (3 - 2 * k)
        down = [80, 6, 3, 2, 2, 1, 1, 1]
        bends = [down[i] * (1 - k) for i in range(N)]
    return bends
for f in range(0, 31):
    key_pose(f, 0.0, slam_pose(f), [0.0] * N)
actions.append(act)
# Sink
act = new_action("Sink")
for f in range(0, 16):
    t = f / 15.0
    z = -1.05 * (t * t)
    b, s = wave(f / 30.0, 12, freq=1.8)
    key_pose(f, z, b, s)
actions.append(act)

for act in actions:
    act.use_fake_user = True
    for fc in getattr(act, "fcurves", []):
        for k in fc.keyframe_points: k.interpolation = 'BEZIER'
arm.animation_data.action = actions[1]

for o in sc.objects: o.select_set(o in (arm, mesh))
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         bake_anim_force_startend_keying=True, add_leaf_bones=False,
                         primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
                         path_mode='COPY', embed_textures=False)
print("DONE", out, "height", h, "verts", len(mesh.data.vertices))

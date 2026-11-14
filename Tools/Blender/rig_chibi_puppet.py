"""
공장장 좌클릭 차징 스킬 모델(Evil Chibi Puppet): 뼈 없는 메시 하나(T자세, 키 약 1m, 앞 = Blender -Y → Unity +Z)에
Mixamo 이름의 사람형 뼈대를 넣고 자동 무게로 묶는다(Unity Humanoid 아바타가 자동으로 맞춰지게).
사용: blender -b --python Tools/Blender/rig_chibi_puppet.py -- <원본.fbx> <출력 폴더> <이름>
  → <출력 폴더>/<이름>.fbx, <이름>_BaseColor.png, <이름>_Normal.png

- 원점 = 두 발 사이 바닥(발바닥이 z 0). 크기는 원본 그대로(키 약 0.98m, Unity에서 키운다).
- 관절 자리는 메시를 재서 정했다(팔 높이 z -0.155, 발목 -0.43, 무릎 -0.355, 골반 -0.28 — 원본 좌표, 바닥 -0.498).
- 큰 머리(턱 아래 z -0.11 위 전부)는 Head 뼈에만 묶는다(자동 무게가 머리카락·귀를 팔에 나눠 주지 않게).
"""
import bpy, sys, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, outdir, name = args[0], args[1], args[2]
os.makedirs(outdir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sc = bpy.context.scene
mesh = [o for o in sc.objects if o.type == 'MESH'][0]
mesh.name = name

FLOOR = -0.498
Y = -0.035  # 몸통 가운데 깊이
def P(x, y, z): return Vector((x, y, z - FLOOR))  # 바닥을 0으로

# 메시를 바닥 원점으로 옮긴다.
bpy.context.view_layer.objects.active = mesh
for o in sc.objects: o.select_set(o == mesh)
mesh.location.z -= FLOOR
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# 뼈대(Mixamo 이름 — Unity Humanoid 자동 매핑)
arm_data = bpy.data.armatures.new(name + "_Rig")
rig = bpy.data.objects.new(name + "_Rig", arm_data)
sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
for o in sc.objects: o.select_set(o == rig)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(n, head, tail, parent=None, connect=False):
    b = eb.new(n); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]; b.use_connect = connect
    return b
bone("Hips", P(0, Y, -0.28), P(0, Y, -0.22))
bone("Spine", P(0, Y, -0.22), P(0, Y, -0.175), "Hips", True)
bone("Spine1", P(0, Y, -0.175), P(0, Y, -0.13), "Spine", True)
bone("Neck", P(0, Y, -0.13), P(0, Y, -0.10), "Spine1", True)
bone("Head", P(0, Y, -0.10), P(0, Y, 0.20), "Neck", True)
bone("HeadTop_End", P(0, Y, 0.20), P(0, Y, 0.48), "Head", True)
for s, side in ((1, "Left"), (-1, "Right")):  # 캐릭터 왼쪽 = +X (앞이 -Y)
    bone(f"{side}Shoulder", P(0.03 * s, Y, -0.145), P(0.12 * s, Y, -0.155), "Spine1")
    bone(f"{side}Arm", P(0.12 * s, Y, -0.155), P(0.205 * s, Y, -0.155), f"{side}Shoulder", True)
    bone(f"{side}ForeArm", P(0.205 * s, Y, -0.155), P(0.268 * s, Y, -0.155), f"{side}Arm", True)
    bone(f"{side}Hand", P(0.268 * s, Y, -0.155), P(0.315 * s, Y, -0.155), f"{side}ForeArm", True)
    bone(f"{side}UpLeg", P(0.07 * s, -0.045, -0.28), P(0.068 * s, -0.045, -0.355), "Hips")
    bone(f"{side}Leg", P(0.068 * s, -0.045, -0.355), P(0.066 * s, -0.05, -0.43), f"{side}UpLeg", True)
    bone(f"{side}Foot", P(0.066 * s, -0.05, -0.43), P(0.066 * s, -0.12, -0.485), f"{side}Leg", True)
    bone(f"{side}ToeBase", P(0.066 * s, -0.12, -0.485), P(0.066 * s, -0.165, -0.485), f"{side}Foot", True)
# 뼈 굴림: 팔·다리가 같은 방향으로 굽게 앞(-Y)을 기준으로 맞춘다.
for b in eb:
    b.align_roll(Vector((0, -1, 0)) if abs(b.vector.z) > abs(b.vector.y) else Vector((0, 0, 1)))
bpy.ops.object.mode_set(mode='OBJECT')

# 뼈대로 묶고(빈 무게 그룹), 무게는 직접 정한다 — AI가 만든 메시는 열 무게(자동)가 자주 실패해 이음매가 찢어진다.
for o in sc.objects: o.select_set(o in (mesh, rig))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_NAME')

groups = {g.name: g for g in mesh.vertex_groups}
deform = [b.name for b in arm_data.bones if b.name != "HeadTop_End"]
for n in deform:
    if n not in groups: groups[n] = mesh.vertex_groups.new(name=n)
bones_world = {b.name: (rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local) for b in arm_data.bones if b.name in deform}

def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def z0(z): return z - FLOOR  # 원본 높이 → 바닥 0 기준
HEAD_Z, ARM_LO, ARM_HI, LEG_Z = z0(-0.11), z0(-0.205), z0(-0.085), z0(-0.27)  # 소매 윗부분이 -0.09까지 올라온다(그 위 -0.07부터 머리)
TORSO = ["Hips", "Spine", "Spine1", "Neck", "LeftShoulder", "RightShoulder"]

def candidates(p):
    side = "Left" if p.x > 0 else "Right"
    if abs(p.x) >= 0.15 and ARM_LO <= p.z <= ARM_HI:                          # 어깨 관절 너머 팔(소매·손): 팔 사슬만
        return [f"{side}Arm", f"{side}ForeArm", f"{side}Hand"]
    if abs(p.x) > 0.09 and ARM_LO <= p.z <= ARM_HI:                           # 어깨 관절 둘레: 어깨·위팔·가슴을 부드럽게
        return [f"{side}Shoulder", f"{side}Arm", "Spine1"]
    if p.z >= HEAD_Z and abs(p.x) < 0.33: return ["Head"]                     # 머리(머리카락·귀 포함) — 팔(소매 윗부분)을 먼저 본다
    if p.z < LEG_Z:                                                           # 다리(바지 끝·신발)
        return [f"{side}UpLeg", f"{side}Leg", f"{side}Foot", f"{side}ToeBase", "Hips"]
    return TORSO

for v in mesh.data.vertices:
    p = mesh.matrix_world @ v.co
    cands = candidates(p)
    power = 2 if f"{'Left' if p.x > 0 else 'Right'}Shoulder" in cands else 4  # 어깨 관절 둘레는 더 넓게 섞는다(찢어지지 않게)
    ws = [(1.0 / max(seg_dist(p, *bones_world[n]), 0.004) ** power, n) for n in cands]
    ws = sorted(ws, reverse=True)[:3]
    total = sum(w for w, _ in ws)
    for w, n in ws:
        w /= total
        if w > 0.02: groups[n].add([v.index], w, 'REPLACE')
print(f"RIG {name}: verts {len(mesh.data.vertices)}")
for n in deform:
    cnt = sum(1 for v in mesh.data.vertices for g in v.groups if g.group == groups[n].index and g.weight > 0.01)
    print(f"  {n}: {cnt}")

# 텍스처를 파일로 꺼낸다.
for img, suffix in ((bpy.data.images.get("base_color"), "BaseColor"), (bpy.data.images.get("normal"), "Normal")):
    if img is None: continue
    path = os.path.join(outdir, f"{name}_{suffix}.png")
    img.filepath_raw = path; img.file_format = 'PNG'
    img.save()
    print("TEX", path)

for o in sc.objects: o.select_set(o in (mesh, rig))
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                         bake_anim=False, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                         armature_nodetype='NULL', path_mode='STRIP', embed_textures=False)
print("OUT", os.path.join(outdir, name + ".fbx"))

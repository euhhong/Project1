# 벽에 붙어 자라는 담쟁이 덩굴 한 무더기(장식용, 충돌 없음)를 만든다 — 줄기(가는 관) + 잎(5갈래 담쟁이 잎 모양 판).
# 텍스처 없이 머티리얼 색만 쓴다: 진한 녹색·녹색·연한 녹색·검붉은 잎(조금) + 갈색 줄기.
# 좌표: 벽 = Blender XZ 평면(y = 0), 덩굴은 벽 앞(−Y) 쪽에 붙는다. 원점 = 덩굴 아래 가운데(땅에 닿는 곳).
# 사용: blender -b --python Tools/Blender/make_ivy.py -- <출력 FBX> [미리보기 PNG] [seed] [폭 m] [높이 m]
import bpy, bmesh, math, random, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out_fbx = argv[0] if len(argv) > 0 else "Ivy_Wall.fbx"
out_png = argv[1] if len(argv) > 1 and argv[1] != "-" else None
seed = int(argv[2]) if len(argv) > 2 else 7
WIDTH = float(argv[3]) if len(argv) > 3 else 5.5
HEIGHT = float(argv[4]) if len(argv) > 4 else 8.5
random.seed(seed)

STEP = 0.12
stems = []  # (points, radius, depth)

def grow(p, ang, length, up_bias, radius, depth):
    pts = [p.copy()]
    for i in range(int(length / STEP)):
        ang += random.gauss(0.0, 0.32)
        ang += (math.pi / 2 - ang) * up_bias  # 위로 끌림
        d = Vector((math.cos(ang), 0.0, math.sin(ang)))
        p = p + d * STEP
        p.x = max(-WIDTH / 2 - 0.4, min(WIDTH / 2 + 0.4, p.x))
        p.z = max(0.02, min(HEIGHT, p.z))
        p.y = -0.015 - random.random() * 0.01
        pts.append(p.copy())
        if depth < 2 and i > 3 and random.random() < (0.07 if depth == 0 else 0.04):
            side = random.choice((-1, 1))
            grow(p.copy(), ang + side * random.uniform(0.7, 1.4), length * random.uniform(0.25, 0.55) * (1 - i * STEP / length * 0.5),
                 up_bias * 0.35, radius * 0.6, depth + 1)
    stems.append((pts, radius, depth))

roots = 5
for k in range(roots):
    x = -WIDTH / 2 + 0.5 + (WIDTH - 1.0) * (k + random.uniform(0.25, 0.75)) / roots
    h = HEIGHT * random.uniform(0.5, 1.0) if k not in (1, 3) else HEIGHT * random.uniform(0.85, 1.0)
    grow(Vector((x, -0.02, 0.02)), math.pi / 2 + random.gauss(0, 0.25), h, 0.16, 0.035, 0)

# ───────── 줄기 → 곡선(굵기) → 메시 ─────────
bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, rgb, rough=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1.0)
    b.inputs["Roughness"].default_value = rough
    return m

mats = [
    material("Ivy_LeafDark", (0.035, 0.085, 0.03), 0.5),
    material("Ivy_Leaf", (0.07, 0.16, 0.045), 0.5),
    material("Ivy_LeafLight", (0.14, 0.25, 0.07), 0.55),
    material("Ivy_LeafRed", (0.2, 0.035, 0.025), 0.55),
    material("Ivy_Stem", (0.12, 0.075, 0.045), 0.85),
]

curve = bpy.data.curves.new("IvyStems", "CURVE")
curve.dimensions = "3D"
curve.bevel_depth = 1.0
curve.bevel_resolution = 0
curve.use_fill_caps = True
for pts, radius, depth in stems:
    if len(pts) < 2:
        continue
    sp = curve.splines.new("POLY")
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        t = i / (len(pts) - 1)
        sp.points[i].co = (p.x, p.y, p.z, 1.0)
        sp.points[i].radius = radius * (1.0 - 0.75 * t)
stem_obj = bpy.data.objects.new("IvyStems", curve)
bpy.context.scene.collection.objects.link(stem_obj)
stem_obj.data.materials.append(mats[4])
bpy.context.view_layer.objects.active = stem_obj
stem_obj.select_set(True)
bpy.ops.object.convert(target="MESH")
stem_obj = bpy.context.active_object

# ───────── 잎 ─────────
# 담쟁이 잎 윤곽(잎자루 = (0,0), 잎끝 = +Y). 5갈래.
OUTLINE = [(0.0, 0.1), (0.2, 0.0), (0.44, 0.06), (0.56, 0.24), (0.38, 0.34), (0.44, 0.55), (0.2, 0.62), (0.0, 1.0),
           (-0.2, 0.62), (-0.44, 0.55), (-0.38, 0.34), (-0.56, 0.24), (-0.44, 0.06), (-0.2, 0.0)]
CENTER = (0.0, 0.4)

leaf_mesh = bpy.data.meshes.new("IvyLeaves")
bm = bmesh.new()
leaves = 0

def add_leaf(pos, size, tip_angle, lift, mat_index):
    global leaves
    # 잎 판의 축: 잎끝 방향(벽 평면 안) + 벽에서 들려 바깥(−Y)으로 기울인다
    tip = Vector((math.sin(tip_angle), 0.0, math.cos(tip_angle)))
    out = Vector((0.0, -1.0, 0.0))
    tip_l = (tip * math.cos(lift) + out * math.sin(lift)).normalized()
    side = tip.cross(out).normalized()
    normal = side.cross(tip_l).normalized()
    if normal.y > 0:  # 앞면이 벽 바깥을 보게
        side = -side
        normal = -normal
    twist = random.uniform(-0.25, 0.25)
    side = (side * math.cos(twist) + normal * math.sin(twist)).normalized()
    normal = side.cross(tip_l).normalized()
    if normal.y > 0:
        normal = -normal
    def P(u, v):
        cup = -0.1 * (u * u) - 0.05 * v * v  # 가운데가 오목하게, 끝은 살짝 처짐
        return pos + (side * u + tip_l * v + normal * cup) * size
    c = bm.verts.new(P(*CENTER))
    ring = [bm.verts.new(P(u, v)) for (u, v) in OUTLINE]
    for i in range(len(ring)):
        a, b = ring[i], ring[(i + 1) % len(ring)]
        f = bm.faces.new((c, b, a))
        f.material_index = mat_index
        if f.normal.dot(normal) < 0:
            f.normal_flip()
    leaves += 1

def pick_mat(z):
    r = random.random()
    if r < 0.06:
        return 3
    light = 0.25 + 0.25 * (z / HEIGHT)  # 위쪽(햇빛)이 조금 밝다
    if r < 0.06 + light * 0.6:
        return 2
    return 1 if random.random() < 0.6 else 0

for pts, radius, depth in stems:
    n = len(pts)
    i = 1
    alt = 1
    while i < n:
        t = i / max(1, n - 1)
        p = pts[i]
        size = random.uniform(0.22, 0.38) * (1.0 - 0.45 * t) * (0.85 if depth else 1.0)
        alt = -alt
        tip_angle = math.pi + alt * random.uniform(0.35, 1.5) + random.gauss(0, 0.2)  # 잎끝은 대체로 아래·옆
        lift = random.uniform(0.25, 0.75)
        off = Vector((alt * random.uniform(0.02, 0.1), -random.uniform(0.03, 0.09), random.uniform(-0.04, 0.04)))
        add_leaf(p + off, size, tip_angle, lift, pick_mat(p.z))
        if random.random() < 0.35:  # 가끔 한 마디에 두 장
            add_leaf(p + Vector((-alt * 0.06, -0.05, 0.03)), size * 0.8, math.pi - alt * random.uniform(0.3, 1.2), lift, pick_mat(p.z))
        i += random.choice((1, 1, 2, 2))

bm.to_mesh(leaf_mesh)
bm.free()
leaf_obj = bpy.data.objects.new("IvyLeaves", leaf_mesh)
bpy.context.scene.collection.objects.link(leaf_obj)
for m in mats:
    leaf_obj.data.materials.append(m)

# 합치기
bpy.ops.object.select_all(action="DESELECT")
leaf_obj.select_set(True)
stem_obj.select_set(True)
bpy.context.view_layer.objects.active = leaf_obj
bpy.ops.object.join()
ivy = bpy.context.active_object
ivy.name = "Ivy_Wall"
tris = sum(len(p.vertices) - 2 for p in ivy.data.polygons)
print(f"IVY stems={len(stems)} leaves={leaves} tris={tris}")

bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, path_mode="STRIP", embed_textures=False,
                         mesh_smooth_type="FACE", add_leaf_bones=False, apply_scale_options="FBX_SCALE_ALL")

# ───────── 미리보기(돌벽 색 판 앞) ─────────
if out_png:
    wall_m = material("PreviewWall", (0.32, 0.3, 0.28), 0.9)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0.01, HEIGHT / 2 + 0.3), rotation=(math.pi / 2, 0, 0))
    wall = bpy.context.active_object
    wall.scale = (WIDTH + 3, HEIGHT + 1.6, 1)
    wall.data.materials.append(wall_m)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -3, 0))
    ground = bpy.context.active_object
    ground.scale = (WIDTH + 6, 6, 1)
    ground.data.materials.append(material("PreviewGround", (0.18, 0.15, 0.11), 0.95))
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = (4.5, -13.0, 4.2)
    direction = Vector((0, 0, HEIGHT * 0.48)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 38
    bpy.context.scene.camera = cam
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.25, 0.28, 0.32, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    bpy.context.scene.world = world
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            pass
    sc.render.resolution_x = 900
    sc.render.resolution_y = 1000
    sc.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", out_png)

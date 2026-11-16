"""
풀 모델 여러 개(맵 소품 Grass/Grass_01 ~ Grass_NN)를 만든다 — 척박한 땅에 실오라기 같은 풀이 듬성듬성 난 모양을 여러 조합으로.
- 텍스처는 한 장(아틀라스 4칸 × 2줄): 0~6칸 = 풀 사진 7종(VARCO, 배경을 뺌), 7칸 = 흙 조각 사진. 모든 모델이 머티리얼 하나를 같이 쓴다(가볍게).
- 모델마다: 흙 조각(있거나 없음, 지름 0.8~1.8m, 바닥에서 1cm) + 풀 포기 1~9개(풀 종류·높이 0.12~0.55m·자리·방향 무작위).
  포기마다 세로 판 두 장을 엇갈려 세운다(어느 쪽에서 봐도 보이게).
VARCO 3D가 가는 풀잎을 메시로 못 만들어 이미지로 만든다. Unity에서 알파 잘라내기·양면 머티리얼.
사용: blender -b --python Tools/Blender/make_grass_cards.py -- <아틀라스.png> <out 폴더> <모델 수>
"""
import bpy, sys, os, math, random, shutil
args = sys.argv[sys.argv.index("--") + 1:]
atlas_png, outdir, count = args[0], args[1], int(args[2])
os.makedirs(outdir, exist_ok=True)
tex = os.path.join(outdir, "Grass_Atlas.png"); shutil.copyfile(atlas_png, tex)
COLS, ROWS, SOIL = 4, 2, 7
GRASS_KINDS = 7

def cell_uv(c):
    x, y = c % COLS, c // COLS
    u0, v0 = x / COLS, 1 - (y + 1) / ROWS
    return [(u0, v0), (u0 + 1 / COLS, v0), (u0 + 1 / COLS, v0 + 1 / ROWS), (u0, v0 + 1 / ROWS)]

def build(idx, rnd):
    verts, faces, uvs = [], [], []
    soil = rnd.random() < 0.7
    patch = rnd.uniform(0.8, 1.8)
    if soil:
        h = patch / 2; b = len(verts)
        a0 = rnd.random() * math.tau
        for k in range(4):  # 흙 판을 돌려 놓아 모델마다 달라 보이게
            a = a0 + k * math.pi / 2 + math.pi / 4
            verts.append((math.cos(a) * h * 1.414, math.sin(a) * h * 1.414, 0.01))
        faces.append((b, b + 1, b + 2, b + 3)); uvs.append(cell_uv(SOIL))
    tufts = rnd.randint(2, 5) if not soil else rnd.randint(5, 12)
    main = rnd.randrange(GRASS_KINDS)
    for t in range(tufts):
        kind = main if rnd.random() < 0.5 else rnd.randrange(GRASS_KINDS)
        r = math.sqrt(rnd.random()) * patch * 0.4
        a = rnd.random() * math.tau
        cx, cy = math.cos(a) * r, math.sin(a) * r
        ht = rnd.uniform(0.2, 0.65); wd = ht * rnd.uniform(0.7, 1.0)
        rot = rnd.random() * math.pi
        for c in range(2):
            ang = rot + c * math.pi / 2
            dx, dy = math.cos(ang) * wd / 2, math.sin(ang) * wd / 2
            b = len(verts)
            verts += [(cx - dx, cy - dy, 0), (cx + dx, cy + dy, 0), (cx + dx, cy + dy, ht), (cx - dx, cy - dy, ht)]
            faces.append((b, b + 1, b + 2, b + 3)); uvs.append(cell_uv(kind))
    name = f"Grass_{idx:02d}"
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    uv = me.uv_layers.new(name="UVMap")
    for fi, poly in enumerate(me.polygons):
        for j, li in enumerate(poly.loop_indices): uv.data[li].uv = uvs[fi][j]
    me.materials.append(mat)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE', add_leaf_bones=False)
    print(name, "soil" if soil else "-", "tufts", tufts)

bpy.ops.wm.read_factory_settings(use_empty=True)
mat = bpy.data.materials.new("Grass"); mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex)
mat.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
mat.node_tree.links.new(t.outputs["Alpha"], bsdf.inputs["Alpha"])
rnd = random.Random(11)
for i in range(1, count + 1): build(i, rnd)
print("DONE", count)

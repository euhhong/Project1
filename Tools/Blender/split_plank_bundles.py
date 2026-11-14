"""
나무 판자 묶음(사용자 모델 Bundled Wooden Planks): 한 메시에 묶음 4개가 네 귀퉁이(좌우 × 위아래)에 함께 들어 있다 → 하나씩 떼어 따로 내보낸다.
사용: blender -b --python Tools/Blender/split_plank_bundles.py -- <원본.fbx> <출력 폴더> <이름> [면 수 목표]
  → <출력 폴더>/<이름>_1~4.fbx, <이름>_BaseColor.png, <이름>_Normal.png

- 원본은 AI 메시라 조각이 1만 개 넘게 흩어져 있어 '이어진 덩어리'로는 못 나눈다 → 면 가운데가 어느 귀퉁이(X 부호 × Z 부호)에 있는지로 나눈다.
- 각 묶음: 원점 = 묶음 가운데(바운딩 박스 중심), 면 수를 목표(기본 12000 삼각형)로 줄인다(원본 묶음 하나 약 12만 → 게임용).
- 번호: 1 = 왼쪽 위, 2 = 오른쪽 위, 3 = 왼쪽 아래, 4 = 오른쪽 아래(앞 = Blender -Y에서 본 화면 기준).
"""
import bpy, sys, os, bmesh
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, outdir, name = args[0], args[1], args[2]
target = int(args[3]) if len(args) > 3 else 12000
os.makedirs(outdir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sc = bpy.context.scene
src_obj = [o for o in sc.objects if o.type == 'MESH'][0]
mat = src_obj.data.materials[0]
mat.name = name

# 텍스처를 파일로 꺼낸다(네 묶음이 함께 쓴다).
for img, suffix in ((bpy.data.images.get("base_color"), "BaseColor"), (bpy.data.images.get("normal"), "Normal")):
    if img is None: continue
    path = os.path.join(outdir, f"{name}_{suffix}.png")
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    print("TEX", path)

quads = [(-1, 1), (1, 1), (-1, -1), (1, -1)]  # (X 부호, Z 부호): 왼쪽 위, 오른쪽 위, 왼쪽 아래, 오른쪽 아래(화면에서 -X가 왼쪽)
mw = src_obj.matrix_world
for i, (sx, sz) in enumerate(quads, start=1):
    me = src_obj.data.copy()
    bm = bmesh.new(); bm.from_mesh(me)
    drop = [f for f in bm.faces if not ((mw @ f.calc_center_median()).x * sx > 0 and (mw @ f.calc_center_median()).z * sz > 0)]
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(me); bm.free()
    part = bpy.data.objects.new(f"{name}_{i}", me)
    part.matrix_world = mw
    sc.collection.objects.link(part)
    for o in sc.objects: o.select_set(o == part)
    bpy.context.view_layer.objects.active = part
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # 원점 = 바운딩 박스 가운데
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    part.location = (0, 0, 0)
    # 면 수 줄이기
    tris = sum(len(p.vertices) - 2 for p in part.data.polygons)
    if tris > target:
        dec = part.modifiers.new("Decimate", 'DECIMATE'); dec.ratio = target / tris
        bpy.ops.object.modifier_apply(modifier=dec.name)
    tris2 = sum(len(p.vertices) - 2 for p in part.data.polygons)
    print(f"PART {part.name}: tris {tris} -> {tris2}, size {tuple(round(d, 3) for d in part.dimensions)}")
    bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, f"{name}_{i}.fbx"), use_selection=True, object_types={'MESH'},
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
    bpy.data.objects.remove(part, do_unlink=True)
print("DONE")

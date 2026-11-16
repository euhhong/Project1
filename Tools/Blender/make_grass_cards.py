"""
풀 한 포기 모델을 만든다(맵 소품 Grass): 세로 판 세 장을 60°씩 엇갈려 세우고(어느 쪽에서 봐도 풀 덩어리로 보임) 풀 사진(배경을 뺀 PNG)을 입힌다.
VARCO 3D가 가는 풀잎을 메시로 못 만들어(두 번 실패) 이미지(VARCO로 만든 풀 사진)로 만든다. Unity에서 알파 잘라내기·양면 머티리얼.
사용: blender -b --python Tools/Blender/make_grass_cards.py -- <풀_알파.png> <out 폴더>
"""
import bpy, sys, os, math, shutil
args = sys.argv[sys.argv.index("--") + 1:]
img_path, outdir = args[0], args[1]
os.makedirs(outdir, exist_ok=True)
tex = os.path.join(outdir, "Grass_BaseColor.png")
shutil.copyfile(img_path, tex)
bpy.ops.wm.read_factory_settings(use_empty=True)
verts, faces, uvs = [], [], []
for k in range(3):
    a = math.radians(k * 60)
    dx, dy = math.cos(a) * 0.5, math.sin(a) * 0.5
    b = len(verts)
    verts += [(-dx, -dy, 0), (dx, dy, 0), (dx, dy, 1), (-dx, -dy, 1)]
    faces.append((b, b + 1, b + 2, b + 3))
    uvs += [(0, 0), (1, 0), (1, 1), (0, 1)]
me = bpy.data.meshes.new("Grass"); me.from_pydata(verts, [], faces); me.update()
uv = me.uv_layers.new(name="UVMap")
for i, l in enumerate(me.loops): uv.data[i].uv = uvs[l.vertex_index]
o = bpy.data.objects.new("Grass", me); bpy.context.scene.collection.objects.link(o)
mat = bpy.data.materials.new("Grass"); mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
t = mat.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex)
mat.node_tree.links.new(t.outputs["Color"], bsdf.inputs["Base Color"])
mat.node_tree.links.new(t.outputs["Alpha"], bsdf.inputs["Alpha"])
me.materials.append(mat)
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, "Grass.fbx"), use_selection=True, path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE")

"""
손에 드는 소품 FBX(VARCO 등)를 Unity에서 쓰기 좋게 정리한다:
- 면 수를 줄이고(decimate 비율) 텍스처를 1024로 줄여 PNG로 저장.
- 긴 축(PCA)을 찾아, grip 쪽 끝(gripHint 방향 끝)을 원점으로, 다른 끝을 Unity +Z(앞)로,
  downHint 방향(긴 축에 수직 성분)을 Unity -Y(아래)로 돌려 내보낸다. 길이(m)를 출력한다.
사용: blender -b --python Tools/Blender/orient_prop.py -- <in.fbx> <outdir> <이름> <decimate 비율> <gx,gy,gz> <dx,dy,dz>
예: 안토니오 바이올린(꼬리 끝 = 원점, 스크롤 = 앞, 뒤판 = 아래), 활(손잡이 끝 = 원점, 끝 = 앞, 말총 = 아래).
"""
import bpy, sys, os, math, mathutils
args = sys.argv[sys.argv.index("--") + 1:]
src, outdir, name, ratio = args[0], args[1], args[2], float(args[3])
gh = mathutils.Vector([float(v) for v in args[4].split(",")]).normalized()
dh = mathutils.Vector([float(v) for v in args[5].split(",")]).normalized()
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
o.name = name; o.data.name = name
bpy.context.view_layer.objects.active = o; o.select_set(True)
if ratio < 0.999:
    m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = ratio; m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
bpy.ops.object.shade_smooth()
mat = o.data.materials[0]; mat.name = name
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE' and n.image and n.outputs[0].links:
        sock = n.outputs[0].links[0].to_socket.name
        nm = name + "_" + ("BaseColor" if sock == "Base Color" else "Normal")
        img = n.image
        if img.size[0] > 1024: img.scale(1024, 1024)
        img.filepath_raw = os.path.join(outdir, nm + ".png"); img.file_format = 'PNG'; img.save(); img.name = nm
pts = [o.matrix_world @ v.co for v in o.data.vertices]
c = sum(pts, mathutils.Vector()) / len(pts)
cov = [[0.0] * 3 for _ in range(3)]
for p in pts:
    d = p - c
    for i in range(3):
        for j in range(3): cov[i][j] += d[i] * d[j]
M = mathutils.Matrix(cov); a = gh.copy()
for _ in range(80): a = (M @ a).normalized()
if a.dot(gh) > 0: a = -a        # a = grip → 앞 끝
proj = [(p - c).dot(a) for p in pts]
grip = c + a * min(proj)        # grip 쪽 끝(축 위)
L = max(proj) - min(proj)
down = (dh - a * dh.dot(a)).normalized()
side = a.cross(down)
# 대상(Blender): 앞 = -Y(Unity +Z), 아래 = -Z(Unity -Y)
src_b = mathutils.Matrix((a, down, side)).transposed()
fwd_t = mathutils.Vector((0, -1, 0)); down_t = mathutils.Vector((0, 0, -1)); side_t = fwd_t.cross(down_t)
dst_b = mathutils.Matrix((fwd_t, down_t, side_t)).transposed()
R = dst_b @ src_b.inverted()
o.data.transform(R.to_4x4() @ mathutils.Matrix.Translation(-grip))
o.matrix_world = mathutils.Matrix.Identity(4)
ws = [v.co for v in o.data.vertices]
print("LENGTH", round(L, 4), "POLYS", len(o.data.polygons), "BOUNDS", tuple(round(min(p[i] for p in ws), 3) for i in range(3)), tuple(round(max(p[i] for p in ws), 3) for i in range(3)))
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, name + ".fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE")

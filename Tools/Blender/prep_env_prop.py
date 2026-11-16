"""
VARCO로 만든 맵 소품 FBX를 맵에 쓰기 좋게 정리한다(MapDresser 형식: Assets/Environment/Models/VARCO/{Key}/{Key}.fbx + {Key}_BaseColor.jpg + {Key}_Normal.jpg).
- 삼각형 수를 목표(target)까지 줄인다(Decimate). 많이 늘어놓는 소품(계단 단 등)은 아주 적게.
- 텍스처는 1024 JPG로 따로 저장하고, FBX는 파일 이름만 가리킨다(Unity가 옆의 JPG를 찾아 머티리얼에 붙인다).
- 축·원점은 그대로(MapDresser가 모델 경계로 맞춘다).
사용: blender -b --python Tools/Blender/prep_env_prop.py -- <in.fbx> <out 폴더> <Key> <목표 삼각형 수>
"""
import bpy, sys, os
args = sys.argv[sys.argv.index("--") + 1:]
src, outdir, key, target = args[0], args[1], args[2], int(args[3])
os.makedirs(outdir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
o = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.join()
o.name = key; o.data.name = key
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
if tris > target:
    m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = max(0.002, target / tris); m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
after = sum(len(p.vertices) - 2 for p in o.data.polygons)
bpy.ops.object.shade_smooth()
mat = o.data.materials[0] if o.data.materials else None
if mat is not None:
    mat.name = key
    for n in mat.node_tree.nodes:
        if n.type != 'TEX_IMAGE' or n.image is None or not n.outputs[0].links: continue
        sock = n.outputs[0].links[0].to_socket
        is_color = sock.name == "Base Color"
        # 노멀 맵 노드를 거쳐 들어가면 Normal
        nm = f"{key}_{'BaseColor' if is_color else 'Normal'}"
        img = n.image
        if img.size[0] > 1024: img.scale(1024, 1024)
        path = os.path.join(outdir, nm + ".jpg")
        sc = bpy.context.scene
        sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 90
        img.save_render(path, scene=sc)
        img.filepath = path; img.name = nm
print(f"PREP {key}: tris {tris} -> {after}")
bpy.ops.export_scene.fbx(filepath=os.path.join(outdir, key + ".fbx"), use_selection=True, path_mode='STRIP', embed_textures=False,
                         mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE")

"""
캐릭터 메시를 히트 판정용 부위로 나눈다: <캐릭터>_Head(머리·얼굴·머리 장식), _Body(몸통·팔·다리), _Hands(손), _Feet(발).
면의 모든 정점이 가장 크게 묶인 뼈가 머리/손/발 뼈(또는 그 자식 뼈)이면 그 부위로 보낸다. 경계 면은 몸통에 남는다.
손에 붙인 소품(_Prop, _Syringe)은 건드리지 않는다.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/split_body_parts.py -- <FBX 파일 또는 폴더> [...]
원본 FBX를 덮어쓴다. 이미 나뉜 파일(_Head 오브젝트가 있음)은 건너뛴다.
"""
import bpy, bmesh, sys, os, glob


def part_of_bone(bone):
    """뼈와 그 조상을 따라 올라가며 부위를 정한다."""
    b = bone
    while b is not None:
        n = b.name.lower()
        if "hand" in n or "finger" in n:
            return "Hands"
        if "foot" in n or "toe" in n:
            return "Feet"
        if "head" in n:
            return "Head"
        b = b.parent
    return "Body"


def separate(obj, face_ids, name):
    if not face_ids:
        return None
    before = set(bpy.data.objects)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE'); bpy.ops.mesh.select_all(action='DESELECT')
    bm = bmesh.from_edit_mesh(obj.data); bm.faces.ensure_lookup_table()
    for i in face_ids: bm.faces[i].select_set(True)
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    new = (set(bpy.data.objects) - before).pop()
    new.name = new.data.name = name
    return new


def process(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    name = os.path.basename(path)
    if any(o.name.endswith("_Head") for o in bpy.data.objects):
        print(f"SPLIT {name}: 이미 나뉨 → 건너뜀"); return
    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        print(f"SPLIT {name}: 뼈대 없음 → 건너뜀"); return
    char = os.path.splitext(name)[0].split("_")[-1]
    bone_part = {b.name: part_of_bone(b) for b in arm.data.bones}
    meshes = [o for o in bpy.data.objects if o.type == 'MESH'
              and not o.name.endswith("_Prop") and not o.name.endswith("_Syringe") and o.parent_type != 'BONE']
    result = {}
    for k, body in enumerate(meshes):
        def dom_parts():
            parts = []
            for v in body.data.vertices:
                g = max(v.groups, key=lambda g: g.weight, default=None)
                parts.append(bone_part.get(body.vertex_groups[g.group].name, "Body") if g else "Body")
            return parts
        suffix = "" if k == 0 else str(k + 1)
        for part in ("Head", "Hands", "Feet"):
            dp = dom_parts()
            ids = [p.index for p in body.data.polygons if all(dp[i] == part for i in p.vertices)]
            new = separate(body, ids, f"{char}_{part}{suffix}")
            if new: result[new.name] = len(new.data.vertices)
        body.name = body.data.name = f"{char}_Body{suffix}"
        result[body.name] = len(body.data.vertices)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
            bpy.ops.object.material_slot_remove_unused()
    print(f"SPLIT {name}: {result}")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True, armature_nodetype='NULL')


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    files = []
    for a in args:
        files += sorted(glob.glob(os.path.join(a, "**", "*.fbx"), recursive=True)) if os.path.isdir(a) else [a]
    for f in files:
        process(f)

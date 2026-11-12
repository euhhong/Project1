"""
캐릭터 FBX 안에 섞여 있는 무기·소품을 따로 떼어 손 뼈에 붙인다(손에 쥐게).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/attach_prop_to_hand.py -- <FBX> <고르는 방법> <손 뼈 이름> <쥐는 방식> [쥐는 위치 0~1] [이름 접미사]
고르는 방법:
  mat:<재질 이름,...>   그 재질의 면
  <재질 이름,...>       (예전 방식) 위와 같음
  rootbone              뿌리 뼈에 가장 크게 묶인 면(몸에 붙지 않고 떠 있는 무기)
  bones:<뼈 이름,...>   그 뼈와 자식 뼈에 가장 크게 묶인 면
쥐는 방식:
  forward  : 긴 축을 캐릭터 앞쪽으로, 가는 끝(총구·바늘·칼끝)이 앞을 향하게. 쥐는 위치는 굵은 끝에서부터 비율.
  up       : 긴 축을 위로 세운다. 쥐는 위치는 아래 끝에서부터 비율.
  ground   : 세운 채 높이는 그대로 두고 손 위치로만 옮긴다(지팡이처럼 땅에 닿게).
  palm     : 회전 없이 소품 중심을 손바닥에 붙인다(공 등).
  keep     : 지금 자리 그대로 두고 손 뼈의 자식으로만 만든다(이미 손에 들려 있는 무기).
  skinned  : 뼈 가중치를 그대로 두고 오브젝트만 따로 뗀다(이미 손 뼈를 따라 움직이는 무기).
원본 FBX를 덮어쓴다. 오브젝트 이름은 <캐릭터>_<접미사>(기본 Prop, 무기는 Weapon). 같은 이름이 있으면 건너뛴다.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix


def pick_faces(obj, selector, arm):
    """고르는 방법에 맞는 면 번호 목록."""
    if selector.startswith("rootbone") or selector.startswith("bones:"):
        if selector.startswith("rootbone"):
            wanted = {b.name for b in arm.data.bones if b.parent is None}
        else:
            roots = set(selector[6:].split(","))
            wanted = set()
            for b in arm.data.bones:
                x = b
                while x is not None:
                    if x.name in roots: wanted.add(b.name); break
                    x = x.parent
        dom = []
        for v in obj.data.vertices:
            g = max(v.groups, key=lambda g: g.weight, default=None)
            dom.append(obj.vertex_groups[g.group].name if g else "")
        return [p.index for p in obj.data.polygons if all(dom[i] in wanted for i in p.vertices)]
    mats = set((selector[4:] if selector.startswith("mat:") else selector).split(","))
    idx = {i for i, s in enumerate(obj.material_slots) if s.material and s.material.name in mats}
    return [p.index for p in obj.data.polygons if p.material_index in idx]


def separate(obj, selector, name, arm):
    chosen = pick_faces(obj, selector, arm)
    if not chosen:
        return None
    before = set(bpy.data.objects)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE'); bpy.ops.mesh.select_all(action='DESELECT')
    bm = bmesh.from_edit_mesh(obj.data); bm.faces.ensure_lookup_table()
    for i in chosen: bm.faces[i].select_set(True)
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    new = (set(bpy.data.objects) - before).pop()
    new.name = new.data.name = name
    for o in (obj, new):
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
        bpy.ops.object.material_slot_remove_unused()
    return new


def main(path, selector, hand_bone, mode, grip, suffix="Prop"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    char = os.path.splitext(os.path.basename(path))[0].split("_")[-1]
    name = f"{char}_{suffix}"
    if any(o.name == name for o in bpy.data.objects):
        print(f"PROP {name} 이미 있음 → 건너뜀"); return
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    # 부위로 나뉜 모델이면 여러 메시에서 골라 하나로 합친다.
    pieces = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.parent_type != 'BONE']:
        p = separate(o, selector, name + "_part", arm)
        if p: pieces.append(p)
    if not pieces:
        print("PROP 고를 면을 찾지 못함", selector); return
    bpy.ops.object.select_all(action='DESELECT')
    for p in pieces: p.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    if len(pieces) > 1: bpy.ops.object.join()
    prop = bpy.context.view_layer.objects.active
    prop.name = prop.data.name = name

    if mode == "skinned":
        print(f"PROP {name}: {len(prop.data.vertices)}정점, 뼈 가중치 유지(skinned)")
        export(path); return

    if mode == "keep":
        prop.vertex_groups.clear()
        for md in list(prop.modifiers): prop.modifiers.remove(md)
        mw = prop.matrix_world.copy(); prop.parent = None; prop.matrix_world = mw
        prop.parent = arm; prop.parent_type = 'BONE'; prop.parent_bone = hand_bone
        bpy.context.view_layer.update(); prop.matrix_world = mw
        print(f"PROP {name}: {len(prop.data.vertices)}정점, 자리 그대로 {hand_bone}에 붙임(keep)")
        export(path); return

    prop.vertex_groups.clear()
    for md in list(prop.modifiers): prop.modifiers.remove(md)
    mw = prop.matrix_world.copy(); prop.parent = None; prop.matrix_world = mw
    bpy.context.view_layer.objects.active = prop
    bpy.ops.object.select_all(action='DESELECT'); prop.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    pts = [v.co.copy() for v in prop.data.vertices]
    mn = Vector([min(p[k] for p in pts) for k in range(3)]); mx = Vector([max(p[k] for p in pts) for k in range(3)])
    size = mx - mn
    ax = max(range(3), key=lambda k: size[k])           # 긴 축
    length = size[ax]

    # 손바닥(월드)
    hb = arm.data.bones[hand_bone]
    h0 = arm.matrix_world @ hb.head_local; h1 = arm.matrix_world @ hb.tail_local
    fingers = [arm.matrix_world @ b.head_local for b in hb.children if not b.name.lower().endswith("nub")]
    palm = (h0 + h1) / 2
    if fingers: palm = (palm + sum(fingers, Vector()) / len(fingers)) / 2
    hand_dir = (h1 - h0).normalized()
    forward = Vector((0, -1, 0))

    def spread(lo, hi):
        sel = [p for p in pts if lo <= p[ax] <= hi]
        if not sel: return 0
        c = sum(sel, Vector()) / len(sel)
        return max((p - c - (p - c).project(Vector([1 if k == ax else 0 for k in range(3)]))).length for p in sel)

    center = (mn + mx) / 2
    if mode == "palm":
        prop.data.transform(Matrix.Translation(-center))
        prop.matrix_world = Matrix.Translation(palm)
    elif mode == "ground":
        prop.data.transform(Matrix.Translation(Vector((-center.x, -center.y, 0))))
        prop.matrix_world = Matrix.Translation(Vector((palm.x, palm.y, 0)))
    else:
        # 긴 축을 로컬 Z로: thin_end_positive 이면 +Z 쪽이 가는 끝
        thin_hi = spread(mx[ax] - 0.15 * length, mx[ax]) < spread(mn[ax], mn[ax] + 0.15 * length)
        to_z = {0: Matrix.Rotation(-math.pi / 2, 4, 'Y'), 1: Matrix.Rotation(math.pi / 2, 4, 'X'), 2: Matrix.Identity(4)}[ax]
        if mode == "forward":
            # 굵은 끝에서 grip 비율 지점을 원점으로, 가는 끝을 +Z
            g = (mx[ax] - grip * length) if not thin_hi else (mn[ax] + grip * length)
            gp = center.copy(); gp[ax] = g
            flip = Matrix.Identity(4) if thin_hi else Matrix.Rotation(math.pi, 4, 'X')
            prop.data.transform(flip @ to_z @ Matrix.Translation(-gp))
            z_axis = (forward - hand_dir * forward.dot(hand_dir)).normalized()   # 가는 끝 = 앞
        else:  # up
            g = mn[ax] + grip * length
            gp = center.copy(); gp[ax] = g
            prop.data.transform(to_z @ Matrix.Translation(-gp))
            z_axis = Vector((0, 0, 1))
        x_axis = hand_dir.cross(z_axis)
        if x_axis.length < 1e-4: x_axis = Vector((1, 0, 0))
        x_axis.normalize(); y_axis = z_axis.cross(x_axis).normalized()
        prop.matrix_world = Matrix.Translation(palm) @ Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()

    mw = prop.matrix_world.copy()
    prop.parent = arm; prop.parent_type = 'BONE'; prop.parent_bone = hand_bone
    bpy.context.view_layer.update(); prop.matrix_world = mw
    print(f"PROP {prop.name}: {len(pts)}정점, 길이 {length:.3f}, 방식 {mode}, 손 {hand_bone}, 손바닥 {tuple(round(x,3) for x in palm)}")
    export(path)


def export(path):
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True, armature_nodetype='NULL')


if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:]
    main(a[0], a[1], a[2], a[3], float(a[4]) if len(a) > 4 else 0.5, a[5] if len(a) > 5 else "Prop")

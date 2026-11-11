"""
의사(Survivor_Doctor) 모델 정리:
  1) 몸 옆에 떠 있던 주사기를 따로 떼어 오른손 뼈(biped R Hand)의 자식으로 붙인다(손에 들고 있게).
  2) 나머지 메시를 부위별 오브젝트로 나눈다: Head(얼굴·머리), Body(몸통), Hands(손), Feet(발).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/split_doctor_parts.py -- <Survivor_Doctor.fbx>
원본 FBX를 덮어쓴다. 여러 번 실행해도 되도록, 이미 나뉜 파일이면 건너뛴다.
"""
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

SYRINGE_MATS = ("h55_scene_sedative_01", "08 - Default.001")
HEAD_MAT = "08 - Default"
HAND_BONE = "biped R Hand"


def dominant_bone(obj, v):
    g = max(v.groups, key=lambda g: g.weight, default=None)
    return obj.vertex_groups[g.group].name if g else ""


def separate_faces(obj, pred, new_name):
    """pred(polygon) 가 True 인 면을 새 오브젝트로 떼어 낸다."""
    bpy.ops.object.mode_set(mode='OBJECT')
    chosen = [p.index for p in obj.data.polygons if pred(p)]
    if not chosen:
        return None
    before = set(bpy.data.objects)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.select_all(action='DESELECT')
    bm = bmesh.from_edit_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    for i in chosen:
        bm.faces[i].select_set(True)
    bmesh.update_edit_mesh(obj.data)
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    new = (set(bpy.data.objects) - before).pop()
    new.name = new.data.name = new_name
    return new


def clean_unused_slots(obj):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.material_slot_remove_unused()


def main(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if any(o.name.endswith("_Syringe") for o in meshes):
        print("SPLIT 이미 정리된 파일 → 건너뜀"); return
    body = meshes[0]
    prefix = "Doctor"
    mats = [s.material.name if s.material else "" for s in body.material_slots]

    # 1) 주사기
    syr_idx = {i for i, n in enumerate(mats) if n in SYRINGE_MATS}
    syringe = separate_faces(body, lambda p: p.material_index in syr_idx, f"{prefix}_Syringe")

    # 2) 머리(얼굴) — 머리 텍스처 재질
    head_idx = {i for i, n in enumerate(mats) if n == HEAD_MAT}
    head = separate_faces(body, lambda p: p.material_index in head_idx, f"{prefix}_Head")

    # 3) 손·발 — 면의 모든 정점이 손/발 뼈에 가장 크게 묶인 면
    me = body.data
    dom = [dominant_bone(body, v) for v in me.vertices]
    is_hand = lambda n: ("Hand" in n or "Finger" in n)
    is_foot = lambda n: ("Foot" in n or "Toe" in n)
    hands = separate_faces(body, lambda p: all(is_hand(dom[i]) for i in p.vertices), f"{prefix}_Hands")
    me = body.data; dom = [dominant_bone(body, v) for v in me.vertices]
    feet = separate_faces(body, lambda p: all(is_foot(dom[i]) for i in p.vertices), f"{prefix}_Feet")
    body.name = body.data.name = f"{prefix}_Body"

    for o in (syringe, head, hands, feet, body):
        if o: clean_unused_slots(o)

    # 4) 주사기를 오른손에 쥐여 준다
    if syringe:
        syringe.vertex_groups.clear()
        for md in list(syringe.modifiers):
            syringe.modifiers.remove(md)
        mw = syringe.matrix_world.copy(); syringe.parent = None; syringe.matrix_world = mw
        bpy.context.view_layer.objects.active = syringe
        bpy.ops.object.select_all(action='DESELECT'); syringe.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

        pts = [v.co.copy() for v in syringe.data.vertices]
        zmin = min(p.z for p in pts); zmax = max(p.z for p in pts)
        length = zmax - zmin
        center_xy = lambda zs: (sum((p for p in pts if zs[0] <= p.z <= zs[1]), Vector()) /
                                max(1, sum(1 for p in pts if zs[0] <= p.z <= zs[1])))
        # 바늘 쪽 판단: 아래 15%와 위 15% 중 단면이 더 가는 쪽
        def spread(z0, z1):
            sel = [p for p in pts if z0 <= p.z <= z1]
            if not sel: return 0
            c = sum(sel, Vector()) / len(sel)
            return max((Vector((p.x - c.x, p.y - c.y, 0))).length for p in sel)
        needle_down = spread(zmin, zmin + 0.15 * length) < spread(zmax - 0.15 * length, zmax)
        # 쥐는 지점: 몸통(배럴) 중간, 바늘 반대쪽으로 55%
        grip_z = zmin + (0.55 if needle_down else 0.45) * length
        grip = center_xy((grip_z - 0.05 * length, grip_z + 0.05 * length))
        grip.z = grip_z
        # 원점을 쥐는 지점으로, 바늘 방향을 로컬 -Z로 맞춘다
        axis_fix = Matrix.Identity(4) if needle_down else Matrix.Rotation(math.pi, 4, 'X')
        syringe.data.transform(axis_fix @ Matrix.Translation(-grip))

        # 손바닥 위치·방향 (월드)
        hb = arm.data.bones[HAND_BONE]
        h_head = arm.matrix_world @ hb.head_local
        h_tail = arm.matrix_world @ hb.tail_local
        fingers = [arm.matrix_world @ b.head_local for b in arm.data.bones
                   if b.name.startswith("biped R Finger") and not b.name.endswith("Nub") and b.parent and b.parent.name == HAND_BONE]
        palm = (h_head + h_tail) / 2
        if fingers:
            palm = (palm + sum(fingers, Vector()) / len(fingers)) / 2
        hand_dir = (h_tail - h_head).normalized()
        forward = Vector((0, -1, 0))  # 캐릭터 앞(Blender -Y)
        # 바늘은 앞쪽을 향하고 손 방향과 수직이 되게
        needle = (forward - hand_dir * forward.dot(hand_dir)).normalized()
        z_axis = -needle                       # 로컬 -Z = 바늘
        x_axis = hand_dir.cross(z_axis).normalized()
        y_axis = z_axis.cross(x_axis).normalized()
        rot = Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()
        syringe.matrix_world = Matrix.Translation(palm) @ rot

        # 오른손 뼈의 자식으로 (월드 위치 유지)
        mw = syringe.matrix_world.copy()
        syringe.parent = arm
        syringe.parent_type = 'BONE'
        syringe.parent_bone = HAND_BONE
        bpy.context.view_layer.update()
        syringe.matrix_world = mw
        print(f"SPLIT 주사기: 길이 {length:.3f}, 바늘 {'아래' if needle_down else '위'}, 손바닥 {tuple(round(x,3) for x in palm)}")

    print("SPLIT 부위:", [(o.name, len(o.data.vertices)) for o in bpy.data.objects if o.type == 'MESH'])
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True,
        armature_nodetype='NULL')
    blend = os.path.join(os.environ.get("SPLIT_BLEND_DIR", os.path.dirname(path)), "Survivor_Doctor_split.blend")
    if os.environ.get("SPLIT_BLEND_DIR"):
        bpy.ops.wm.save_as_mainfile(filepath=blend)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:]
    main(args[0])

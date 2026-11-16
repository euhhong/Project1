"""
노란옷의 왕(Hunter_Hastur.fbx): 소매 아래로 땅까지 닿는 검은 촉수 팔 메시를 지워, 소매 속에 가려져 있던 사람 손
(붕대 손목 + 붉은 손가락)이 소매 끝에서 살짝 튀어나오게 한다. 손은 원래 자리·원래 무게(biped L/R Hand·Finger) 그대로.

- 메시를 떨어진 덩어리로 나누고, 덩어리마다 가장 많이 무게를 받은 뼈가 촉수 팔 사슬
  (왼쪽 biped_bone_00~03, 오른쪽 biped_bone_27·26·24·23)이면 그 덩어리를 지운다.
- 뼈는 그대로 둔다(뼈 이름·쉬는 자세가 같아 기존 모션이 그대로 먹는다. 촉수 팔 뼈는 메시 없이 남는다).
- 내보내기 설정은 fix_model_facing.py와 같다.

사용: blender -b --python Tools/Blender/remove_hastur_tentacle_arms.py -- <Hunter_Hastur.fbx> [out.fbx]
      out을 주지 않으면 원본을 덮어쓴다. 이미 지운 모델에 다시 돌려도 지울 덩어리가 없어 그대로 나간다.
"""
import bpy, sys, bmesh
from collections import Counter

TENTACLE_ARM = {"biped_bone_00", "biped_bone_01", "biped_bone_02", "biped_bone_03",
                "biped_bone_27", "biped_bone_26", "biped_bone_24", "biped_bone_23"}

args = sys.argv[sys.argv.index("--") + 1:]
src = args[0]
out = args[1] if len(args) > 1 else src

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    gn = {g.index: g.name for g in o.vertex_groups}
    me = o.data
    dom = {}
    for v in me.vertices:
        if v.groups:
            dom[v.index] = gn[max(v.groups, key=lambda g: g.weight).group]
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen, kill = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop(); comp.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen: seen.add(y.index); stack.append(y)
        top = Counter(dom.get(x.index, "") for x in comp).most_common(1)[0][0]
        if top in TENTACLE_ARM: kill += comp
    before = len(bm.verts)
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
        bm.to_mesh(me); me.update()  # 지우기만 하므로 남은 면의 사용자 정의 법선은 그대로 따라간다
    bm.free()
    print(f"TENTACLE_ARMS {o.name}: 정점 {before} → {len(me.vertices)} (지움 {before - len(me.vertices)})")

bpy.ops.export_scene.fbx(
    filepath=out, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'},
    add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True,
    armature_nodetype='NULL')
print("EXPORTED", out)

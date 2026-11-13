"""
공장장 분신의 무기 — 갈고리 같은 낫(한 손 낫)을 만든다. 실제 게임 모델 참고 없이 새로 만든 모양.
- 손잡이: 검은 쇠 자루(길이 0.62m) + 감은 가죽 띠 + 끝 고리.
- 날: 자루 끝에서 앞으로 크게 휘어 내려오는 초승달 날, 끝은 안쪽으로 말려 들어가는 갈고리. 안쪽(베는 쪽) 가장자리는 얇게.
- 재질: 날 = 검게 그을린 쇠, 날 가장자리 = 달아오른 붉은빛(발광), 자루 = 검은 쇠, 띠 = 짙은 갈색.
좌표: Blender Z가 위, 자루는 원점(쥐는 자리 가운데)에서 +Z로, 날은 +Y(앞)로 휜다. Unity에서는 +Y 위, +Z 앞.

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/make_hook_scythe.py -- <출력.fbx> [미리보기 폴더]
미리보기 폴더를 주면 옆·앞·비스듬히 세 장(PNG)을 그린다.
"""
import bpy, bmesh, sys, math, os
from mathutils import Vector


def material(name, color, emit=None, strength=0.0, rough=0.5, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = strength
    return m


def obj_from_bm(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    for f in o.data.polygons:
        f.use_smooth = True
    return o


def tube(name, mat, z0, z1, r, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=z1 - z0)
    bmesh.ops.translate(bm, vec=Vector((0, 0, (z0 + z1) / 2)), verts=bm.verts)
    return obj_from_bm(name, bm, mat)


def blade(mat_blade, mat_edge):
    """초승달 날: 바깥 호(등)와 안쪽 호(날)를 이어 면을 만들고 두께를 준다. 끝은 갈고리로 말린다."""
    top = Vector((0, 0.0, 0.62))  # 자루 끝
    n = 40
    outer, inner = [], []
    for i in range(n + 1):
        t = i / n
        # 등: 자루 끝(쇠고리)에서 시작해 위로 올라 앞으로 넘어가 앞쪽으로 크게 휘어 내려오고, 끝(t→1)은 자루 쪽으로 말려 들어가는 갈고리.
        # 원 가운데 c는 자루 끝 앞쪽 — 시작점(170°)이 바로 자루 끝이다.
        a = math.radians(170 - 245 * t)               # 170° → -75°
        R = 0.30 - 0.09 * max(0.0, (t - 0.65) / 0.35) ** 1.5
        c = top + Vector((0, 0.29, -0.05))
        p = c + Vector((0, math.cos(a) * R, math.sin(a) * R))
        width = 0.012 + 0.085 * math.sin(math.pi * (0.18 + 0.82 * t)) ** 0.7  # 뿌리는 자루에 붙을 만큼, 가운데가 넓고 끝은 뾰족
        q = c + Vector((0, math.cos(a) * (R - width), math.sin(a) * (R - width)))
        outer.append(p); inner.append(q)
    bm = bmesh.new()
    th = 0.011
    ov = [[bm.verts.new(p + Vector((s * th, 0, 0))) for p in outer] for s in (-1, 1)]
    iv = [[bm.verts.new(q + Vector((s * th * 0.15, 0, 0))) for q in inner] for s in (-1, 1)]  # 날 쪽은 얇게
    for i in range(n):
        for s in range(2):
            bm.faces.new((ov[s][i], ov[s][i + 1], iv[s][i + 1], iv[s][i]) if s else (iv[s][i], iv[s][i + 1], ov[s][i + 1], ov[s][i]))
        bm.faces.new((ov[0][i], ov[0][i + 1], ov[1][i + 1], ov[1][i]))  # 등
        bm.faces.new((iv[1][i], iv[1][i + 1], iv[0][i + 1], iv[0][i]))  # 날
    bm.faces.new((ov[0][0], ov[1][0], iv[1][0], iv[0][0]))
    bm.faces.new((iv[0][n], iv[1][n], ov[1][n], ov[0][n]))
    bm.normal_update()
    o = obj_from_bm("Scythe_Blade", bm, mat_blade)
    # 달아오른 날 가장자리: 안쪽 호를 따라 가는 관
    curve = bpy.data.curves.new("EdgeCurve", 'CURVE'); curve.dimensions = '3D'
    sp = curve.splines.new('POLY'); sp.points.add(len(inner) - 1)
    for i, q in enumerate(inner):
        sp.points[i].co = (*q, 1)
    curve.bevel_depth = 0.006; curve.bevel_resolution = 2
    e = bpy.data.objects.new("Scythe_Edge", curve)
    bpy.context.scene.collection.objects.link(e)
    e.data.materials.append(mat_edge)
    return o, e


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        raise SystemExit("usage: -- <out.fbx> [preview_dir]")
    dst = argv[0]
    preview = argv[1] if len(argv) > 1 else None
    bpy.ops.wm.read_factory_settings(use_empty=True)

    iron = material("Scythe_Iron", (0.035, 0.032, 0.03), rough=0.45, metal=0.9)
    blade_m = material("Scythe_BladeIron", (0.05, 0.04, 0.035), rough=0.35, metal=0.95)
    edge_m = material("Scythe_HotEdge", (0.9, 0.18, 0.05), emit=(1.0, 0.22, 0.04), strength=6.0)
    wrap_m = material("Scythe_Wrap", (0.16, 0.07, 0.04), rough=0.9)

    parts = [tube("Scythe_Handle", iron, -0.22, 0.64, 0.022)]
    for k in range(7):  # 감은 띠(쥐는 자리)
        z = -0.12 + k * 0.045
        parts.append(tube(f"Scythe_Wrap{k}", wrap_m, z, z + 0.03, 0.026))
    parts.append(tube("Scythe_Pommel", iron, -0.26, -0.22, 0.034))
    parts.append(tube("Scythe_Collar", iron, 0.56, 0.66, 0.032))
    b, e = blade(blade_m, edge_m)
    parts.append(b)
    # 곡선을 메시로 바꿔 하나로 합친다
    bpy.context.view_layer.objects.active = e; e.select_set(True)
    bpy.ops.object.convert(target='MESH')
    parts.append(e)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    scythe = bpy.context.view_layer.objects.active
    scythe.name = "HellEmber_CloneScythe_Weapon"
    print(f"[make_hook_scythe] verts={len(scythe.data.vertices)} dims={tuple(round(v, 3) for v in scythe.dimensions)}")

    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE')
    print(f"[make_hook_scythe] wrote {dst}")

    if preview:
        os.makedirs(preview, exist_ok=True)
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
        scene.render.resolution_x = scene.render.resolution_y = 640
        world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.57, 0.6, 1)
        world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
        sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); scene.collection.objects.link(sun)
        sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(30)); sun.data.energy = 3
        cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
        target = Vector((0, 0.18, 0.4))
        for name, pos in (("side", (1.9, 0.18, 0.45)), ("front", (0.0, 2.0, 0.5)), ("angle", (1.3, 1.2, 1.0))):
            cam.location = pos
            d = target - Vector(pos)
            cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = os.path.join(preview, f"scythe_{name}.png")
            bpy.ops.render.render(write_still=True)
        print(f"[make_hook_scythe] previews in {preview}")


main()

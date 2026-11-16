"""
세워 두는 소품 FBX를 Unity에서 쓰기 좋게 정리한다(예: 판자 — 생존자가 내리는 판자 = WoodPlanks_1):
- 꼭짓점 분포(PCA)로 세 축을 찾아 가장 긴 축 = Unity 위(+Y), 두 번째 = 오른쪽(+X), 가장 짧은 축(두께) = 앞(+Z)으로 돌린다.
- 원점 = 바닥 가운데(가장 긴 축의 아래 끝). 크기는 그대로. 길이(위·옆·두께, m)를 출력한다.
사용: blender -b --python Tools/Blender/orient_upright_prop.py -- <in.fbx> <out.fbx>
"""
import bpy, sys, mathutils
args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
o = [x for x in bpy.context.scene.objects if x.type == 'MESH'][0]
bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
pts = [o.matrix_world @ v.co for v in o.data.vertices]
c = sum(pts, mathutils.Vector()) / len(pts)
cov = [[0.0] * 3 for _ in range(3)]
for p in pts:
    d = p - c
    for i in range(3):
        for j in range(3): cov[i][j] += d[i] * d[j]
M = mathutils.Matrix(cov)
def power(Mx, seed):
    a = seed.normalized()
    for _ in range(100): a = (Mx @ a).normalized()
    return a
a1 = power(M, mathutils.Vector((0.3, 0.2, 1)))
lam1 = (M @ a1).dot(a1)
M2 = M - lam1 * mathutils.Matrix([[a1[i] * a1[j] for j in range(3)] for i in range(3)])
a2 = power(M2, mathutils.Vector((1, 0.1, 0.2)))
a2 = (a2 - a1 * a2.dot(a1)).normalized()
a3 = a1.cross(a2).normalized()
proj = [(p - c).dot(a1) for p in pts]
if abs(min(proj)) < abs(max(proj)): a1 = -a1; a3 = -a3; proj = [-x for x in proj]   # 원점 쪽 끝을 바닥으로(어느 쪽이든 상관없지만 정하기만)
bottom = c + a1 * min(proj)
# 대상(Blender 축): Unity +Y = Blender +Z, Unity +X = Blender -X(FBX 기본 축 변환), Unity +Z = Blender -Y
src_b = mathutils.Matrix((a2, a3, a1)).transposed()          # 열 = (오른쪽, 앞, 위)
dst_b = mathutils.Matrix(((-1, 0, 0), (0, -1, 0), (0, 0, 1))).transposed()
R = dst_b @ src_b.inverted()
o.data.transform(R.to_4x4() @ mathutils.Matrix.Translation(-bottom))
o.matrix_world = mathutils.Matrix.Identity(4)
ws = [v.co for v in o.data.vertices]
size = [max(p[i] for p in ws) - min(p[i] for p in ws) for i in range(3)]
print("SIZE x(옆)=%.3f y(두께)=%.3f z(위)=%.3f" % tuple(size))
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE', add_leaf_bones=False)
print("DONE")

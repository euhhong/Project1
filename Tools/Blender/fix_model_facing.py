"""
캐릭터 FBX가 Unity에서 +Z(앞)를 보도록 방향을 맞춰 다시 내보낸다.

규칙: Blender 기준 캐릭터가 -Y를 보고 있어야 Unity로 내보냈을 때 +Z를 본다.
발끝(Toe) 뼈가 발(Foot) 뼈보다 +Y 쪽에 있으면 뒤로 돌아 있는 것이므로 Z축 180° 회전한다.
이미 맞는 모델은 건드리지 않는다(여러 번 돌려도 안전).

사용법 (Blender 5.x):
  blender -b --python Tools/Blender/fix_model_facing.py -- <FBX 파일 또는 폴더> [...]
  폴더를 주면 그 아래 모든 .fbx를 처리한다. 원본 파일을 덮어쓴다.
"""
import bpy, sys, os, glob, math
from mathutils import Matrix, Vector


def collect(paths):
    files = []
    for p in paths:
        if os.path.isdir(p):
            files += glob.glob(os.path.join(p, "**", "*.fbx"), recursive=True)
        elif p.lower().endswith(".fbx"):
            files.append(p)
    return sorted(files)


def facing_y(arms):
    """발끝-발 방향의 Y 성분. +면 +Y를 보고 있다. 판단할 수 없으면 None."""
    feet, toes = [], []
    for a in arms:
        for b in a.data.bones:
            n = b.name.lower()
            p = a.matrix_world @ b.head_local
            if "toe" in n and "nub" not in n and "end" not in n:
                toes.append(p)
            elif "foot" in n:
                feet.append(p)
    if not feet or not toes:
        return None
    avg = lambda ps: sum(ps, Vector()) / len(ps)
    return (avg(toes) - avg(feet)).y


def process(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    fy = facing_y(arms)
    name = os.path.basename(path)
    if fy is None:
        print(f"FACING {name}: 발/발끝 뼈가 없어 판단 불가 → 건너뜀")
        return
    if fy < 0:
        print(f"FACING {name}: 이미 앞(-Y)을 봄 → 그대로")
        return
    rot = Matrix.Rotation(math.pi, 4, 'Z')
    for o in bpy.data.objects:
        if o.parent is None and o.type in {'ARMATURE', 'MESH', 'EMPTY'}:
            o.matrix_world = rot @ o.matrix_world
    bpy.context.view_layer.update()
    after = facing_y(arms)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH', 'EMPTY'},
        add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=True,
        armature_nodetype='NULL')
    print(f"FACING {name}: 180° 회전 후 다시 내보냄 (toe-foot y {fy:+.3f} → {after:+.3f})")


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for f in collect(args):
        process(f)

using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>잔상 하나: 굳힌 메시들을 반투명 색으로 그리다가 life 동안 옅어지며 사라진다(AfterimageTrail이 만든다).</summary>
    public class AfterimageGhost : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new();
        private readonly List<MeshRenderer> renderers = new();
        private MaterialPropertyBlock block;
        private Color color;
        private float age, life;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void Create(List<(Mesh mesh, Matrix4x4 matrix)> parts, float life, Color color, Material material)
        {
            var go = new GameObject("Afterimage");
            var g = go.AddComponent<AfterimageGhost>();
            g.life = life; g.color = color;
            g.block = new MaterialPropertyBlock();
            foreach (var (mesh, matrix) in parts)
            {
                var part = new GameObject("Part");
                part.transform.SetParent(go.transform, false);
                part.transform.SetPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = part.AddComponent<MeshRenderer>();
                var mats = new Material[mesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                g.meshes.Add(mesh);
                g.renderers.Add(r);
            }
            g.Apply();
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            Apply();
        }

        private void Apply()
        {
            var c = color; c.a *= 1f - age / life;
            block.SetColor(ColorId, c);
            foreach (var r in renderers) r.SetPropertyBlock(block);
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}

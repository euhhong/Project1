using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>한 번 터지고 사라지는 "펑" 효과: 카메라를 보는 별 두 겹 + 주먹에서 뻗는 속도선.</summary>
    public class PunchBurst : MonoBehaviour
    {
        private const int Points = 10;
        private float life, age, size;
        private Color outer, inner;
        private Vector3 fist;
        private Mesh mesh;
        private MeshRenderer meshRenderer;

        public static void Create(Vector3 center, Vector3 fist, float size, float life, Color outer, Color inner, Material material)
        {
            var go = new GameObject("PunchBurst");
            go.transform.position = center;
            var b = go.AddComponent<PunchBurst>();
            b.fist = fist; b.size = size; b.life = life; b.outer = outer; b.inner = inner;
            b.mesh = new Mesh { name = "PunchBurst" };
            b.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            b.meshRenderer = go.AddComponent<MeshRenderer>();
            b.meshRenderer.sharedMaterial = material;
            b.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.meshRenderer.receiveShadows = false;
            b.Rebuild();
        }

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            Rebuild();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private void Rebuild()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
            float t = Mathf.Clamp01(age / life);
            float grow = Mathf.Lerp(0.35f, 1f, 1f - (1f - t) * (1f - t)); // 빨리 커졌다가
            float alpha = 1f - t * t;                                    // 끝에 사라진다
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();

            Star(verts, cols, tris, size * 0.5f * grow, size * 0.22f * grow, Fade(outer, alpha), 0.0f, 0f);
            Star(verts, cols, tris, size * 0.3f * grow, size * 0.12f * grow, Fade(inner, alpha), 0.32f, -0.01f);

            // 속도선: 주먹 → 별 가운데 방향의 가는 줄 몇 개(로컬 좌표로 바꿔 넣는다).
            Vector3 from = transform.InverseTransformPoint(fist);
            Vector3 dir = -from; dir.z = 0f;
            if (dir.sqrMagnitude > 1e-4f)
            {
                Vector3 n = new Vector3(-dir.y, dir.x, 0f).normalized;
                for (int i = -2; i <= 2; i++)
                {
                    Vector3 a = from + n * (i * size * 0.08f) + new Vector3(0f, 0f, -0.02f);
                    Vector3 b = a + dir * Mathf.Lerp(0.5f, 0.9f, grow);
                    float w = size * 0.012f * (1f - Mathf.Abs(i) * 0.25f);
                    int s = verts.Count;
                    verts.Add(a - n * w); verts.Add(a + n * w); verts.Add(b + n * w * 0.2f); verts.Add(b - n * w * 0.2f);
                    var c = Fade(inner, alpha * 0.8f);
                    cols.Add(c); cols.Add(c); cols.Add(c); cols.Add(c);
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        private static Color Fade(Color c, float a) { c.a *= a; return c; }

        // 뾰족한 별(가운데 + 바깥 꼭짓점 Points개, 안쪽 오목점 Points개).
        private static void Star(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Color> c,
                                 System.Collections.Generic.List<int> t, float outerR, float innerR, Color color, float spin, float z)
        {
            int center = v.Count;
            v.Add(new Vector3(0f, 0f, z)); c.Add(color);
            for (int i = 0; i < Points * 2; i++)
            {
                float a = spin + i * Mathf.PI / Points;
                float r = i % 2 == 0 ? outerR * (i % 4 == 0 ? 1f : 0.8f) : innerR;
                v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z)); c.Add(color);
            }
            for (int i = 0; i < Points * 2; i++)
            {
                t.Add(center); t.Add(center + 1 + i); t.Add(center + 1 + (i + 1) % (Points * 2));
            }
        }
    }
}

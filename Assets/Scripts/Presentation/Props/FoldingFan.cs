using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 접었다 펼 수 있는 고전 부채(쥘부채). 코드로 만든 소품 — 대나무 살(rib) + 주름진 종이 + 금색 테두리.
    /// - Openness 0 = 접힘(살이 한 장으로 겹침), 1 = 활짝 폄(spreadAngle).
    /// - 사북(고정 못, 손잡이 쪽 끝)이 이 오브젝트의 원점이고, 부채는 +Y 쪽으로 뻗는다. 펴지는 면은 XY 평면.
    /// - 크기는 실제 길이(m) 기준. 캐릭터 손 뼈 아래에 붙일 때는 부모 크기를 고려해 맞춘다.
    /// 붉은나비(미치코)의 무기.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class FoldingFan : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("살 개수(양 끝 두꺼운 겉살 포함).")]
        [SerializeField, Range(5, 30)] private int ribCount = 16;
        [Tooltip("부채 길이(m, 사북 ~ 끝).")]
        [SerializeField, Min(0.05f)] private float length = 0.32f;
        [Tooltip("종이가 시작되는 지점(길이 대비).")]
        [SerializeField, Range(0.1f, 0.9f)] private float paperStart = 0.42f;
        [Tooltip("금색 테두리 두께(길이 대비).")]
        [SerializeField, Range(0f, 0.3f)] private float trimWidth = 0.07f;
        [Tooltip("활짝 폈을 때 각도(도).")]
        [SerializeField, Range(30f, 180f)] private float spreadAngle = 150f;
        [Tooltip("종이 주름 깊이(m).")]
        [SerializeField, Min(0f)] private float pleatDepth = 0.006f;

        [Header("색")]
        [SerializeField] private Color paperColor = new(0.72f, 0.07f, 0.09f);
        [SerializeField] private Color trimColor = new(0.95f, 0.75f, 0.3f);
        [SerializeField] private Color ribColor = new(0.18f, 0.1f, 0.06f);

        [Header("상태")]
        [SerializeField, Range(0f, 1f)] private float openness = 1f;

        private Mesh mesh;
        private float builtOpenness = -1f;
        private readonly List<Vector3> verts = new();
        private readonly List<int>[] tris = { new(), new(), new() }; // 0 종이, 1 테두리, 2 살

        /// <summary>펼침 정도 0(접힘) ~ 1(활짝).</summary>
        public float Openness
        {
            get => openness;
            set => openness = Mathf.Clamp01(value);
        }

        public float Length => length;
        public float SpreadAngle => spreadAngle;

        private void OnEnable()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { MakeMaterial("FanPaper", paperColor, 0.2f), MakeMaterial("FanTrim", trimColor, 0.6f), MakeMaterial("FanRib", ribColor, 0.35f) };
            if (mesh == null) mesh = new Mesh { name = "FoldingFan" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            builtOpenness = -1f;
            Rebuild();
        }

        private void OnValidate() => builtOpenness = -1f;

        private void LateUpdate()
        {
            if (!Mathf.Approximately(builtOpenness, openness)) Rebuild();
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        private static Material MakeMaterial(string name, Color color, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // 양면(종이 뒷면도 보이게)
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private void Rebuild()
        {
            builtOpenness = openness;
            verts.Clear();
            foreach (var t in tris) t.Clear();

            float total = spreadAngle * Mathf.Max(openness, 0.02f);
            int n = Mathf.Max(ribCount, 2);
            float inner = length * paperStart;
            float trim = length * (1f - trimWidth);

            // 살 방향(+Y 기준, 가운데 대칭).
            var dirs = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (-total * 0.5f + total * i / (n - 1)) * Mathf.Deg2Rad;
                dirs[i] = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
            }

            // 종이: 살 사이마다 한 칸. 칸 가운데를 앞뒤로 번갈아 밀어 주름을 만든다. 바깥쪽 띠는 금색 테두리.
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 d0 = dirs[i], d1 = dirs[i + 1];
                Vector3 mid = (d0 + d1).normalized;
                Vector3 z = new(0f, 0f, (i % 2 == 0 ? 1f : -1f) * pleatDepth * openness);
                Quad(0, d0 * inner, mid * inner + z, mid * trim + z, d0 * trim);
                Quad(0, mid * inner + z, d1 * inner, d1 * trim, mid * trim + z);
                Quad(1, d0 * trim, mid * trim + z, mid * length + z, d0 * length);
                Quad(1, mid * trim + z, d1 * trim, d1 * length, mid * length + z);
            }

            // 살: 사북에서 끝까지 가는 얇은 막대(앞뒤 두 면). 양 끝 겉살은 더 굵다.
            for (int i = 0; i < n; i++)
            {
                bool guard = i == 0 || i == n - 1;
                float w = guard ? 0.009f : 0.004f;
                Vector3 d = dirs[i];
                Vector3 side = Vector3.Cross(d, Vector3.forward).normalized * w * 0.5f;
                Vector3 front = new(0f, 0f, guard ? 0.005f : 0.0025f);
                float end = guard ? length * 1.02f : inner * 1.05f; // 속살은 종이 안쪽에서 멈추고, 겉살은 끝까지
                Quad(2, -side + front, side + front, d * end + side + front, d * end - side + front);
                Quad(2, side - front, -side - front, d * end - side - front, d * end + side - front);
            }

            // 사북(못)
            float r = 0.009f;
            Quad(1, new Vector3(-r, -r, 0.007f), new Vector3(r, -r, 0.007f), new Vector3(r, r, 0.007f), new Vector3(-r, r, 0.007f));

            mesh.Clear();
            mesh.subMeshCount = 3;
            mesh.SetVertices(verts);
            for (int s = 0; s < 3; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private void Quad(int sub, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int s = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            var t = tris[sub];
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
        }
    }
}

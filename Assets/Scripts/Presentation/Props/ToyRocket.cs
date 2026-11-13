using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 아기자기한 로켓 폭죽 모형(광대의 무기). 코드로 만든 소품 — 줄무늬 몸통 + 노란 원뿔 머리 + 파란 날개 4장 + 꼬리 노즐 + 심지.
    /// 원점은 꼬리(노즐) 끝이고 로켓은 +Z로 뻗는다. 크기는 실제 길이(m) 기준.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ToyRocket : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float length = 0.8f;
        [SerializeField, Min(0.02f)] private float radius = 0.085f;
        [SerializeField, Range(2, 12)] private int stripes = 5;
        [SerializeField, Range(8, 64)] private int segments = 32;

        [SerializeField] private Color stripeA = new(0.9f, 0.15f, 0.15f);
        [SerializeField] private Color stripeB = new(0.97f, 0.95f, 0.9f);
        [Tooltip("드릴 머리 색(어두운 적갈색).")]
        [SerializeField] private Color noseColor = new(0.36f, 0.13f, 0.08f);
        [Tooltip("드릴 머리 끝을 둥글게 마감하는 반지름(몸통 반지름 대비).")]
        [SerializeField, Range(0.05f, 0.8f)] private float tipRoundness = 0.45f;
        [SerializeField] private Color finColor = new(0.2f, 0.45f, 0.95f);
        [SerializeField] private Color darkColor = new(0.15f, 0.13f, 0.12f);

        public float Length => length;
        public float Radius => radius;

        private Mesh mesh;
        private readonly List<Vector3> verts = new();
        private readonly List<int>[] tris = { new(), new(), new(), new(), new() }; // 줄무늬A, 줄무늬B, 머리, 날개, 어두운 색

        private void OnEnable()
        {
            var filter = GetComponent<MeshFilter>(); if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            var r = GetComponent<MeshRenderer>(); if (r == null) r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[]
            {
                Mat("RocketA", stripeA, 0.45f), Mat("RocketB", stripeB, 0.45f), Mat("RocketNose", noseColor, 0.35f),
                Mat("RocketFin", finColor, 0.4f), Mat("RocketDark", darkColor, 0.2f),
            };
            if (mesh == null) mesh = new Mesh { name = "ToyRocket" };
            filter.sharedMesh = mesh;
            Build();
        }

        private void OnValidate() { if (mesh != null) Build(); }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
        }

        private static Material Mat(string name, Color c, float smooth)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private void Build()
        {
            verts.Clear(); foreach (var t in tris) t.Clear();
            float nozzle = length * 0.08f, noseStart = length * 0.76f; // 머리(원뿔)는 짧고 끝은 둥글게 — 덜 뾰족하게
            // 꼬리 노즐: 살짝 벌어진 짧은 원통(어두운 색)
            Ring(4, 0f, radius * 0.75f, nozzle, radius * 0.6f);
            // 몸통 줄무늬
            float bodyLen = noseStart - nozzle;
            for (int i = 0; i < stripes; i++)
            {
                float z0 = nozzle + bodyLen * i / stripes, z1 = nozzle + bodyLen * (i + 1) / stripes;
                Ring(i % 2, z0, radius, z1, radius);
            }
            Disk(4, nozzle, radius, false);          // 몸통 뒤 덮개
            // 드릴 머리: 매끈한 원뿔 + 끝은 둥근 반구로 마감(한 장으로 이어 붙여 각지지 않게)
            Nose(noseStart, length);
            // 날개 4장(꼬리 쪽)
            for (int k = 0; k < 4; k++) Fin(k * 90f + 45f, nozzle, nozzle + bodyLen * 0.35f);
            // 심지(꼬리 뒤로 삐죽)
            Ring(4, -length * 0.07f, radius * 0.08f, 0f, radius * 0.08f);

            mesh.Clear();
            mesh.subMeshCount = tris.Length;
            mesh.SetVertices(verts);
            for (int s = 0; s < tris.Length; s++) mesh.SetTriangles(tris[s], s);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // z0(반지름 r0) ~ z1(반지름 r1) 사이의 원통·원뿔 옆면. 경계마다 정점을 따로 둬서 색 띠 경계가 또렷하다.
        private void Ring(int sub, float z0, float r0, float z1, float r1)
        {
            int start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(d * r0 + Vector3.forward * z0);
                verts.Add(d * r1 + Vector3.forward * z1);
            }
            var t = tris[sub];
            for (int i = 0; i < segments; i++)
            {
                int a0 = start + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                t.Add(a0); t.Add(a1); t.Add(b0);
                t.Add(b0); t.Add(a1); t.Add(b1);
            }
        }

        // 원뿔(noseStart ~ 끝)을 둘레를 두 배로 나눠 매끈하게, 끝은 반지름 tipRoundness*radius의 반구로 둥글게.
        // 모든 단이 정점을 공유해서 법선이 부드럽게 이어진다(다각형처럼 각져 보이지 않음).
        private void Nose(float z0, float z1)
        {
            int seg = segments * 2;
            float tipR = radius * tipRoundness;
            // 반구 가운데: 원뿔 옆면이 반구에 닿는 곳. 끝(z1)에서 tipR만큼 안쪽.
            float capCenter = z1 - tipR;
            var profile = new List<Vector2>(); // (z, 반지름)
            const int coneSteps = 10, capSteps = 8;
            for (int k = 0; k <= coneSteps; k++)
            {
                float t = k / (float)coneSteps;
                profile.Add(new Vector2(Mathf.Lerp(z0, capCenter, t), Mathf.Lerp(radius, tipR, t)));
            }
            for (int k = 1; k <= capSteps; k++)
            {
                float a = k / (float)capSteps * Mathf.PI * 0.5f;
                profile.Add(new Vector2(capCenter + Mathf.Sin(a) * tipR, Mathf.Cos(a) * tipR));
            }
            int start = verts.Count;
            foreach (var p in profile)
                for (int i = 0; i <= seg; i++)
                {
                    float ang = i * Mathf.PI * 2f / seg;
                    verts.Add(new Vector3(Mathf.Cos(ang) * p.y, Mathf.Sin(ang) * p.y, p.x));
                }
            var t2 = tris[2];
            int row = seg + 1;
            for (int k = 0; k < profile.Count - 1; k++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = start + k * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    t2.Add(a0); t2.Add(a1); t2.Add(b0);
                    t2.Add(b0); t2.Add(a1); t2.Add(b1);
                }
        }

        private void Disk(int sub, float z, float r, bool front)
        {
            int c = verts.Count; verts.Add(Vector3.forward * z);
            for (int i = 0; i <= segments; i++) { float a = i * Mathf.PI * 2f / segments; verts.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z)); }
            var t = tris[sub];
            for (int i = 0; i < segments; i++) { if (front) { t.Add(c); t.Add(c + 1 + i); t.Add(c + 2 + i); } else { t.Add(c); t.Add(c + 2 + i); t.Add(c + 1 + i); } }
        }

        // 몸통 옆에서 바깥으로 뻗는 삼각 날개(양면).
        private void Fin(float angleDeg, float z0, float z1)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            var side = new Vector3(-d.y, d.x, 0f) * 0.004f;
            Vector3 p0 = d * radius + Vector3.forward * z1, p1 = d * radius + Vector3.forward * (z0 - length * 0.02f), p2 = d * (radius * 2.3f) + Vector3.forward * (z0 - length * 0.05f);
            int s = verts.Count;
            verts.Add(p0 + side); verts.Add(p1 + side); verts.Add(p2 + side);
            verts.Add(p0 - side); verts.Add(p1 - side); verts.Add(p2 - side);
            var t = tris[3];
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s + 3); t.Add(s + 5); t.Add(s + 4);
        }
    }
}

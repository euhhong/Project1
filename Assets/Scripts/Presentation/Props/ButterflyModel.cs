using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 붉은나비의 나비(임시 모델, 코드로 만듦 — VARCO 3D 나비 모델이 오면 바꾼다).
    /// 무늬 없는 흰 날개(앞날개·뒷날개) + 날개 테두리가 약간 붉게 빛나고, 날개를 파닥거린다.
    /// - 원점은 몸 가운데, 몸은 +Z(앞), 날개는 ±X로 펴진다. 크기는 날개 끝~끝 약 1m(transform 크기로 줄인다).
    /// - Ghost: 놓을 자리 미리보기(반투명, 놓을 수 없으면 붉게). Highlight: 겨누는 중(테두리가 더 밝게).
    /// </summary>
    [DisallowMultipleComponent]
    public class ButterflyModel : MonoBehaviour
    {
        [Tooltip("파닥거림(초당 번).")]
        [SerializeField, Min(0f)] private float flapRate = 6f;
        [Tooltip("파닥거림 크기 / 평소 날개를 든 각도(도).")]
        [SerializeField] private float flapAmplitude = 38f, flapLift = 18f;
        [SerializeField] private Color wingColor = new(0.97f, 0.96f, 0.95f);
        [SerializeField] private Color rimColor = new(1f, 0.18f, 0.15f);
        [Tooltip("테두리 발광 세기(평소 / 겨눌 때).")]
        [SerializeField, Min(0f)] private float rimGlow = 0.08f, rimGlowHighlight = 0.3f; // 거의 안 보일 만큼만(사용자 요청)

        private Transform leftWing, rightWing;
        private readonly List<Renderer> renderers = new();
        private Material wingMat, rimMat, ghostOk, ghostBad;
        private float phase;
        private bool ghost, ghostValid = true, highlight;

        // 오른쪽(+X) 날개 모양(X, Z). 왼쪽은 X를 뒤집는다.
        private static readonly Vector2[] Fore = { new(0.03f, 0.04f), new(0.10f, 0.20f), new(0.28f, 0.33f), new(0.47f, 0.30f), new(0.52f, 0.16f), new(0.40f, 0.03f), new(0.10f, -0.01f) };
        private static readonly Vector2[] Hind = { new(0.03f, -0.01f), new(0.20f, -0.04f), new(0.34f, -0.13f), new(0.33f, -0.27f), new(0.20f, -0.33f), new(0.08f, -0.22f) };
        private const float RimWidth = 0.018f;

        public bool Ghost { get => ghost; set { if (ghost != value) { ghost = value; ApplyMaterials(); } } }
        public bool GhostValid { get => ghostValid; set { if (ghostValid != value) { ghostValid = value; ApplyMaterials(); } } }
        public bool Highlight { get => highlight; set { if (highlight != value) { highlight = value; ApplyMaterials(); } } }

        private void Awake()
        {
            wingMat = Lit("ButterflyWing", wingColor, 0.35f, Color.black);
            rimMat = Lit("ButterflyRim", rimColor, 0.3f, rimColor * rimGlow);
            ghostOk = Transparent("ButterflyGhost", new Color(1f, 1f, 1f, 0.45f));
            ghostBad = Transparent("ButterflyGhostBad", new Color(1f, 0.25f, 0.2f, 0.45f));
            leftWing = Wing("LeftWing", -1f);
            rightWing = Wing("RightWing", 1f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
            renderers.Add(body.GetComponent<Renderer>());
            phase = Random.value * 10f;
            ApplyMaterials();
        }

        private void OnDestroy()
        {
            foreach (var m in new[] { wingMat, rimMat, ghostOk, ghostBad }) if (m != null) Destroy(m);
            foreach (var r in renderers) if (r != null && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null && f.sharedMesh.name.StartsWith("ButterflyWing")) Destroy(f.sharedMesh);
        }

        private void Update()
        {
            phase += Time.deltaTime * flapRate * (ghost ? 0.5f : 1f) * Mathf.PI * 2f;
            float angle = flapLift + Mathf.Sin(phase) * flapAmplitude;
            leftWing.localRotation = Quaternion.AngleAxis(-angle, Vector3.forward);   // 왼쪽(-X) 날개: 위로 들림
            rightWing.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);   // 오른쪽(+X) 날개
        }

        private void ApplyMaterials()
        {
            if (renderers.Count == 0) return;
            if (rimMat != null) rimMat.SetColor("_EmissionColor", rimColor * (highlight ? rimGlowHighlight : rimGlow));
            Material g = ghostValid ? ghostOk : ghostBad;
            foreach (var r in renderers)
            {
                if (r.sharedMaterials.Length == 2) r.sharedMaterials = ghost ? new[] { g, g } : new[] { wingMat, rimMat };
                else r.sharedMaterial = ghost ? g : wingMat;
            }
        }

        private Transform Wing(string name, float side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "ButterflyWing" };
            var verts = new List<Vector3>();
            var fill = new List<int>();
            var rim = new List<int>();
            foreach (var shape in new[] { Fore, Hind }) AddShape(shape, side, verts, fill, rim);
            // 뒷면: 정점을 따로 복사해 감는 방향을 뒤집는다(같은 정점을 쓰면 앞뒤 법선이 서로 지워져 까맣게 보인다).
            int count = verts.Count;
            verts.AddRange(verts.ToArray());
            foreach (var list in new[] { fill, rim })
            {
                int tris = list.Count;
                for (int i = 0; i < tris; i += 3) { list.Add(list[i] + count); list.Add(list[i + 2] + count); list.Add(list[i + 1] + count); }
            }
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fill, 0);
            mesh.SetTriangles(rim, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { wingMat, rimMat };
            renderers.Add(r);
            return go.transform;
        }

        // 날개 한 장(앞면만): 안쪽은 흰 면(부채꼴로 채움), 가장자리는 붉은 띠(바깥으로 RimWidth). 뒷면은 Wing()에서 따로 만든다.
        private static void AddShape(Vector2[] shape, float side, List<Vector3> verts, List<int> fill, List<int> rim)
        {
            Vector2 c = Vector2.zero;
            foreach (var p in shape) c += p;
            c /= shape.Length;
            Vector3 P(Vector2 v) => new(v.x * side, 0f, v.y);
            int n = shape.Length;
            // 면
            int center = verts.Count; verts.Add(P(c));
            int first = verts.Count;
            foreach (var p in shape) verts.Add(P(p));
            for (int i = 0; i < n; i++)
            {
                int a = first + i, b = first + (i + 1) % n;
                One(fill, center, a, b);
            }
            // 테두리 띠
            int inner = verts.Count;
            foreach (var p in shape) verts.Add(P(p));
            int outer = verts.Count;
            foreach (var p in shape) verts.Add(P(p + (p - c).normalized * RimWidth));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                One(rim, inner + i, outer + i, outer + j);
                One(rim, inner + i, outer + j, inner + j);
            }
        }

        private static void One(List<int> t, int a, int b, int c)
        {
            t.Add(a); t.Add(b); t.Add(c);
        }

        private static Material Lit(string name, Color c, float smooth, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        private static Material Transparent(string name, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }
    }
}

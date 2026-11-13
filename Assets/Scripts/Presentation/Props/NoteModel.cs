using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 안토니오의 붉은 음표(임시 모델, 코드로 만듦 — 8분음표 ♪: 비스듬한 머리 + 기둥 + 꼬리). 색은 붉게, 빛은 거의 없게.
    /// 원점은 머리 가운데쯤, 위로 기둥이 선다(키 약 0.7m). 늘 카메라 쪽을 보게 돈다(모양이 잘 보이게).
    /// Ghost: 놓을 자리 미리보기(반투명, 놓을 수 없으면 흐린 회색).
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteModel : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.82f, 0.07f, 0.09f);
        [Tooltip("아주 약한 발광(0 = 없음).")]
        [SerializeField, Min(0f)] private float glow = 0.15f;

        private readonly List<Renderer> parts = new();
        private Material solid, ghostOk, ghostBad;
        private bool ghost, ghostValid = true;

        public bool Ghost { get => ghost; set { if (ghost != value) { ghost = value; Apply(); } } }
        public bool GhostValid { get => ghostValid; set { if (ghostValid != value) { ghostValid = value; Apply(); } } }

        private void Awake()
        {
            solid = Lit(color, glow);
            ghostOk = Transparent(new Color(1f, 0.35f, 0.35f, 0.45f));
            ghostBad = Transparent(new Color(0.6f, 0.6f, 0.6f, 0.35f));
            // 머리(비스듬한 타원)
            Part(PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 0f, 25f), new Vector3(0.3f, 0.21f, 0.14f));
            // 기둥(머리 오른쪽에서 위로)
            Part(PrimitiveType.Cube, new Vector3(0.13f, 0.3f, 0f), Quaternion.identity, new Vector3(0.04f, 0.6f, 0.04f));
            // 꼬리(기둥 꼭대기에서 아래로 휘어 내림 — 두 마디)
            Part(PrimitiveType.Cube, new Vector3(0.21f, 0.52f, 0f), Quaternion.Euler(0f, 0f, -50f), new Vector3(0.05f, 0.24f, 0.04f));
            Part(PrimitiveType.Cube, new Vector3(0.27f, 0.38f, 0f), Quaternion.Euler(0f, 0f, -15f), new Vector3(0.045f, 0.16f, 0.04f));
            Apply();
        }

        private void Part(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            parts.Add(r);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 to = cam.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(-to, Vector3.up); // 앞면(+Z 반대)이 카메라 쪽
        }

        private void Apply()
        {
            Material m = ghost ? (ghostValid ? ghostOk : ghostBad) : solid;
            foreach (var r in parts) if (r != null) r.sharedMaterial = m;
        }

        private void OnDestroy()
        {
            foreach (var m in new[] { solid, ghostOk, ghostBad }) if (m != null) Destroy(m);
        }

        private static Material Lit(Color c, float glow)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "Note" };
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.5f);
            if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); }
            return m;
        }

        private static Material Transparent(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = "NoteGhost" };
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }
    }
}

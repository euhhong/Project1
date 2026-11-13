using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 안토니오의 붉은 음표. 모델은 Resources의 RedMusicNote(VARCO에서 만든 8분음표 ♪, Blender로 줄이고 원점을 머리 가운데로 옮김)를 쓴다.
    /// 원점 = 음표 머리(공) 가운데 → 전류선이 머리 가운데에서 나간다. 위로 기둥·꼬리가 선다(키 약 0.7m = size 1).
    /// 늘 카메라 쪽으로 ♪ 모양(꼬리가 오른쪽)을 보이게 돈다. 모델을 못 찾으면 코드로 만든 임시 모양을 쓴다.
    /// Ghost: 놓을 자리 미리보기(반투명, 놓을 수 없으면 흐린 회색).
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteModel : MonoBehaviour
    {
        private const string ModelResource = "RedMusicNote/RedMusicNote";

        [SerializeField] private Color color = new(0.82f, 0.07f, 0.09f);
        [Tooltip("아주 약한 발광(0 = 없음).")]
        [SerializeField, Min(0f)] private float glow = 0.15f;
        [Tooltip("모델 크기(원본 키 1m 기준).")]
        [SerializeField, Min(0.05f)] private float modelScale = 0.7f;
        [Tooltip("모델을 세로축으로 더 돌릴 각도(도). ♪ 앞면이 카메라를 보게 맞춘 값.")]
        [SerializeField] private float modelYaw = 0f;

        private readonly List<Renderer> parts = new();
        private readonly List<Material> modelMats = new();
        private Material solid, ghostOk, ghostBad;
        private bool ghost, ghostValid = true;

        public bool Ghost { get => ghost; set { if (ghost != value) { ghost = value; Apply(); } } }
        public bool GhostValid { get => ghostValid; set { if (ghostValid != value) { ghostValid = value; Apply(); } } }

        private void Awake()
        {
            ghostOk = Transparent(new Color(1f, 0.35f, 0.35f, 0.45f));
            ghostBad = Transparent(new Color(0.6f, 0.6f, 0.6f, 0.35f));
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab != null)
            {
                var model = Instantiate(prefab, transform, false);
                model.name = "Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, modelYaw, 0f) * prefab.transform.localRotation); // FBX 축 맞춤(눕힘 방지)은 그대로 두고 세로축으로만 돌린다
                model.transform.localScale = Vector3.one * modelScale;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    parts.Add(r);
                    modelMats.Add(r.sharedMaterial);
                }
                Apply();
                return;
            }
            solid = Lit(color, glow);
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
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                Material solidMat = i < modelMats.Count ? modelMats[i] : solid; // 모델이면 모델 머티리얼(텍스처), 임시 모양이면 붉은 Lit
                parts[i].sharedMaterial = ghost ? (ghostValid ? ghostOk : ghostBad) : solidMat;
            }
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

using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 도둑 손전등 연출(FlashlightSkill, BE를 읽어 그린다).
    /// - 손전등(임시 모델, 코드로 만듦 — 검은 몸통 + 넓은 머리 + 빛나는 렌즈)은 평소엔 넣어 두고, 비추는 동안만 오른손에 들고 앞을 겨눈다.
    /// - 렌즈에서 앞으로 스포트라이트 + 옅은 빛 원뿔(벽에 닿으면 거기까지).
    /// - 빛에 지져지는 캐릭터(LightExposure) 머리 위에 게이지 두 줄: 위 = 정면(천천히 참, 다 차면 기절), 아래 = 어디로든(빠르게 참, 다 차면 "E 봉인" — 0까지 떨어지면 풀림).
    ///   게이지는 비추지 않으면 계속 떨어진다(BE).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 도둑만).
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightVisual : MonoBehaviour
    {
        private const string SkillId = "Thief_Flashlight";
        private const float BeamHalfAngle = 16f; // FlashlightSkill.halfAngle과 맞춘다(빛 원뿔 모양)

        [SerializeField] private Color beamColor = new(1f, 0.95f, 0.75f, 1f);
        [SerializeField, Min(0f)] private float lightIntensity = 8f;
        [Tooltip("스킬이 끝난 뒤 손전등을 넣기까지(초).")]
        [SerializeField, Min(0f)] private float holsterDelay = 0.2f;

        private static FlashlightVisual gaugeDrawer; // 게이지는 한 곳에서만 그린다
        private ICharacterSkillState skill;
        private Animator animator;
        private Transform root, torch, lens;
        private Light spot;
        private MeshFilter beamFilter;
        private Mesh beamMesh;
        private Material bodyMat, headMat, lensMat, beamMat;
        private float showUntil = -1f;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private void OnEnable() { if (gaugeDrawer == null) gaugeDrawer = this; }

        private void OnDisable()
        {
            if (gaugeDrawer == this) gaugeDrawer = null;
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var m in new Object[] { bodyMat, headMat, lensMat, beamMat, beamMesh }) if (m != null) Destroy(m);
        }

        private void LateUpdate()
        {
            if (skill == null) TryGetComponent(out skill);
            if (root == null) Build();
            if (animator == null || !animator.isActiveAndEnabled) animator = GetComponentInChildren<Animator>();
            bool shining = skill != null && skill.State == SkillState.Casting && skill.SkillId == SkillId && skill.Reach > 0f;
            if (shining) showUntil = Time.time + holsterDelay;
            bool show = Time.time <= showUntil;
            root.gameObject.SetActive(show);
            if (!show) return;

            Vector3 dir = skill != null && skill.Direction.sqrMagnitude > 1e-4f ? skill.Direction : transform.forward;
            Vector3 hand = HandPoint();
            torch.SetPositionAndRotation(hand, Quaternion.LookRotation(dir, Vector3.up)); // 손에 쥐고 늘 앞을 겨눈다
            spot.enabled = shining;
            beamFilter.gameObject.SetActive(shining);
            if (!shining) return;

            // 빛 원뿔: 렌즈에서 앞으로, 가운데가 벽에 닿으면 거기까지
            float reach = skill.Reach;
            Vector3 from = lens.position;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, reach, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (Hits[i].collider.GetComponentInParent<CharacterIdentity>() != null) continue;
                reach = Mathf.Min(reach, Hits[i].distance);
            }
            spot.range = skill.Reach + 2f;
            beamFilter.transform.localScale = Vector3.one * Mathf.Max(0.1f, reach); // 길이에 맞춰 원뿔 전체를 키운다(벌어지는 각도 그대로)
        }

        /// <summary>오른손 손바닥(가운데손가락 뿌리 쪽으로 조금). 뼈가 없으면 몸 앞 BE 자리.</summary>
        private Vector3 HandPoint()
        {
            if (animator != null && animator.isHuman)
            {
                var h = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var m = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                if (h != null) return m != null ? Vector3.Lerp(h.position, m.position, 0.5f) : h.position;
            }
            return skill != null ? skill.ReachOrigin : transform.position + Vector3.up * 1.3f + transform.forward * 0.5f;
        }

        private void Build()
        {
            root = new GameObject("Flashlight").transform; // 캐릭터를 따라 자리만 옮긴다
            torch = new GameObject("Torch").transform;
            torch.SetParent(root, false);
            bodyMat = Lit(new Color(0.12f, 0.12f, 0.13f), 0.6f, 0f);
            headMat = Lit(new Color(0.25f, 0.25f, 0.27f), 0.8f, 0f);
            lensMat = Lit(new Color(1f, 0.97f, 0.85f), 0.9f, 2.5f);
            // 몸통(쥐는 곳, 손 뒤로 조금) — 실린더는 Y축이 길어 X로 90도 눕힌다
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.02f), new Vector3(0.05f, 0.09f, 0.05f), bodyMat);
            // 머리(넓음)
            Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.13f), new Vector3(0.08f, 0.035f, 0.08f), headMat);
            // 렌즈
            var l = Part(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.166f), new Vector3(0.068f, 0.003f, 0.068f), lensMat);
            lens = l.transform;

            var sgo = new GameObject("FlashlightSpot");
            sgo.transform.SetParent(lens, false);
            sgo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 렌즈(눕힌 실린더)의 앞 = 손전등 앞
            spot = sgo.AddComponent<Light>();
            spot.type = LightType.Spot; spot.color = beamColor; spot.intensity = lightIntensity;
            spot.spotAngle = BeamHalfAngle * 2f; spot.innerSpotAngle = BeamHalfAngle * 1.2f; spot.range = 16f;
            spot.shadows = LightShadows.Soft;

            var bgo = new GameObject("FlashlightBeam");
            bgo.transform.SetParent(torch, false);
            bgo.transform.localPosition = new Vector3(0f, 0f, 0.17f);
            beamMesh = BuildCone();
            beamFilter = bgo.AddComponent<MeshFilter>();
            beamFilter.sharedMesh = beamMesh;
            var br = bgo.AddComponent<MeshRenderer>();
            beamMat = new Material(Shader.Find("Sprites/Default")) { name = "FlashlightBeam" }; // 꼭짓점 색(멀수록 투명), 양면
            br.sharedMaterial = beamMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.gameObject.SetActive(false);
        }

        private GameObject Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(torch, false);
            go.transform.SetLocalPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>길이 1(+Z)의 빛 원뿔(끝 반지름 = tan(반각)). 렌즈 쪽이 진하고 멀수록 사라진다. 크기는 localScale(= 빛 길이)로.</summary>
        private Mesh BuildCone()
        {
            const int sides = 24, rings = 6;
            float tan = Mathf.Tan(BeamHalfAngle * Mathf.Deg2Rad);
            var verts = new System.Collections.Generic.List<Vector3>();
            var cols = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float z = r / (float)rings;
                float rad = 0.003f + tan * z; // 렌즈 크기(길이 14m에서 약 4cm)에서 시작
                float a = 0.13f * Mathf.Pow(1f - z, 1.4f);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, z));
                    cols.Add(new Color(beamColor.r, beamColor.g, beamColor.b, a));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = r * sides + s, i1 = r * sides + (s + 1) % sides, j0 = i0 + sides, j1 = i1 + sides;
                    tris.AddRange(new[] { i0, j0, i1, i1, j0, j1 });
                }
            var m = new Mesh { name = "FlashlightCone" };
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        private static Material Lit(Color c, float smooth, float glow)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "Flashlight" };
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (glow > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * glow); }
            return m;
        }

        // ── 머리 위 게이지 ──
        private static Texture2D white;

        private void OnGUI()
        {
            if (gaugeDrawer != this) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            float u = Screen.height / 1080f;
            foreach (var ex in LightExposure.All)
            {
                if (ex == null || (ex.Exposure < 0.005f && ex.FrontExposure < 0.005f)) continue;
                var cc = ex.GetComponent<CharacterController>();
                float top = cc != null ? cc.center.y + cc.height * 0.5f : 1.9f;
                Vector3 sp = cam.WorldToScreenPoint(ex.transform.position + Vector3.up * (top + 0.35f));
                if (sp.z <= 0f) continue;
                float w = 110f * u, h = 9f * u, x = sp.x - w * 0.5f, y = Screen.height - sp.y;
                // 위 칸: 정면(다 차면 기절)
                Bar(new Rect(x, y, w, h * 0.8f), ex.FrontExposure, new Color(1f, 0.15f, 0.1f));
                // 아래 칸: 어디로든(다 차면 E 봉인)
                Bar(new Rect(x, y + h * 0.8f + 3f * u, w, h), ex.Exposure, ex.SkillLocked ? new Color(1f, 0.35f, 0.1f) : new Color(1f, 0.8f, 0.3f));
                if (ex.SkillLocked)
                {
                    var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter };
                    style.normal.textColor = new Color(1f, 0.5f, 0.2f);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x - 20f * u, y - 24f * u, w + 40f * u, 22f * u), "E 봉인", style);
                }
            }
            GUI.color = Color.white;
        }

        private static void Bar(Rect r, float fill, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, white);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), white);
        }
    }
}

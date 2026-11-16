using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 기절 표시: 이 캐릭터가 기절(ICharacterStun.IsStunned)한 동안 머리 위를 작은 병아리 몇 마리가 빙빙 돌고,
    /// 그 위에 작은 게이지가 기절 시간 동안 차오른다(가득 차면 풀림). 기절이 끝나면 사라진다.
    /// 머리 위치는 Humanoid면 머리 뼈, 아니면(노란옷의 왕 등 Generic) 이름에 "head"가 든 뼈, 그것도 없으면 CharacterController 꼭대기.
    /// 도형(구·큐브·쿼드)으로만 만들어 외부 에셋이 없다. 캐릭터 루트에 붙인다(CharacterLoadout / StunDizzyAttacher). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class StunDizzyIndicator : MonoBehaviour
    {
        [SerializeField, Range(1, 6)] private int chickCount = 3;
        [Tooltip("병아리가 도는 빠르기(도/초).")]
        [SerializeField] private float spinSpeed = 220f;
        [Tooltip("게이지 크기(가로, 세로 m).")]
        [SerializeField] private Vector2 gaugeSize = new(0.42f, 0.055f);
        [SerializeField] private Color gaugeFill = new(1f, 0.82f, 0.2f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Dictionary<Color, Material> Materials = new();

        private ICharacterStun stun;
        private CharacterController controller;
        private Animator boundTo;
        private Transform head;

        private GameObject root;
        private Transform ring;
        private readonly List<Transform> chicks = new();
        private Transform gauge;
        private Transform gaugeBar;
        private float spin;

        private void Awake()
        {
            stun = GetComponent<ICharacterStun>();
            controller = GetComponent<CharacterController>();
        }

        private void OnDisable() { if (root != null) root.SetActive(false); }
        private void OnDestroy() { if (root != null) Destroy(root); }

        private void LateUpdate()
        {
            stun ??= GetComponent<ICharacterStun>();
            bool show = stun != null && stun.IsStunned;
            if (!show) { if (root != null && root.activeSelf) root.SetActive(false); return; }

            if (root == null) Build();
            if (!root.activeSelf) root.SetActive(true);

            float height = CharacterHeight();
            Vector3 top = HeadTop(height);
            float s = Mathf.Clamp(height / 1.8f, 0.6f, 2f); // 큰 감시자는 조금 크게

            // 병아리 고리: 머리 위에서 빙빙
            spin = (spin + spinSpeed * Time.deltaTime) % 360f;
            ring.SetPositionAndRotation(top + Vector3.up * (0.06f * s), Quaternion.Euler(0f, spin, 0f));
            ring.localScale = Vector3.one * s;
            for (int i = 0; i < chicks.Count; i++)
            {
                float bob = Mathf.Sin(Time.time * 9f + i * 2.1f) * 0.025f;
                var p = chicks[i].localPosition;
                chicks[i].localPosition = new Vector3(p.x, bob, p.z);
            }

            // 게이지: 병아리 위, 카메라를 봄. 기절 시간 동안 왼쪽부터 차오름
            var cam = Camera.main;
            gauge.position = top + Vector3.up * (0.24f * s);
            if (cam != null) gauge.rotation = cam.transform.rotation;
            gauge.localScale = Vector3.one * s;
            float total = Mathf.Max(stun.StunDuration, 0.01f);
            float filled = 1f - Mathf.Clamp01(stun.StunRemaining / total);
            float w = gaugeSize.x * filled;
            gaugeBar.localScale = new Vector3(Mathf.Max(w, 0.0001f), gaugeSize.y, 1f);
            gaugeBar.localPosition = new Vector3(-gaugeSize.x * 0.5f + w * 0.5f, 0f, -0.002f);
        }

        // ---------- 머리 위치 ----------

        private float CharacterHeight()
        {
            float scale = transform.lossyScale.y;
            return controller != null ? controller.height * scale : 1.8f * scale;
        }

        /// <summary>머리 꼭대기 근처(월드). 모델이 바뀌면 머리 뼈를 다시 찾는다.</summary>
        private Vector3 HeadTop(float height)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != boundTo || (head == null && anim != null)) { boundTo = anim; head = FindHead(anim); }
            if (head != null)
            {
                float top = Mathf.Max(head.position.y + height * 0.1f, transform.position.y + height * 0.6f);
                return new Vector3(head.position.x, top, head.position.z);
            }
            Vector3 basePos = controller != null ? transform.TransformPoint(controller.center) : transform.position;
            return new Vector3(basePos.x, transform.position.y + height, basePos.z);
        }

        private static Transform FindHead(Animator anim)
        {
            if (anim == null) return null;
            if (anim.isHuman)
            {
                var h = anim.GetBoneTransform(HumanBodyBones.Head);
                if (h != null) return h;
            }
            Transform best = null;
            foreach (var t in anim.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (!n.Contains("head") && !n.Contains("頭") && !n.Contains("머리")) continue;
                if (t.GetComponent<Renderer>() != null) continue; // 메시 말고 뼈
                if (best == null || t.position.y < best.position.y) best = t; // 가장 아래 = 목 위 머리 뼈(머리끝 뼈 말고)
            }
            return best;
        }

        // ---------- 만들기 ----------

        private void Build()
        {
            root = new GameObject($"{name}_StunDizzy");
            ring = new GameObject("Chicks").transform;
            ring.SetParent(root.transform, false);
            const float radius = 0.26f;
            for (int i = 0; i < chickCount; i++)
            {
                float a = i * Mathf.PI * 2f / chickCount;
                var chick = new GameObject("Chick").transform;
                chick.SetParent(ring, false);
                chick.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                // 도는 방향(접선)을 보게
                chick.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), Vector3.up);
                var yellow = new Color(1f, 0.88f, 0.18f, 1f);
                Part(PrimitiveType.Sphere, chick, new Vector3(0f, 0f, 0f), new Vector3(0.1f, 0.085f, 0.11f), yellow);           // 몸
                Part(PrimitiveType.Sphere, chick, new Vector3(0f, 0.07f, 0.04f), new Vector3(0.07f, 0.07f, 0.07f), yellow);     // 머리
                Part(PrimitiveType.Cube, chick, new Vector3(0f, 0.065f, 0.082f), new Vector3(0.025f, 0.018f, 0.03f), new Color(1f, 0.5f, 0.1f, 1f)); // 부리
                var eye = new Color(0.08f, 0.06f, 0.05f, 1f);
                Part(PrimitiveType.Sphere, chick, new Vector3(0.022f, 0.085f, 0.068f), Vector3.one * 0.014f, eye);
                Part(PrimitiveType.Sphere, chick, new Vector3(-0.022f, 0.085f, 0.068f), Vector3.one * 0.014f, eye);
                Part(PrimitiveType.Sphere, chick, new Vector3(0f, 0.01f, -0.055f), new Vector3(0.05f, 0.03f, 0.04f), new Color(1f, 0.78f, 0.1f, 1f)); // 꼬리
                chicks.Add(chick);
            }

            gauge = new GameObject("Gauge").transform;
            gauge.SetParent(root.transform, false);
            var back = Part(PrimitiveType.Quad, gauge, Vector3.zero, new Vector3(gaugeSize.x + 0.02f, gaugeSize.y + 0.02f, 1f), new Color(0.08f, 0.08f, 0.1f, 1f));
            back.name = "Back";
            gaugeBar = Part(PrimitiveType.Quad, gauge, new Vector3(0f, 0f, -0.002f), new Vector3(0.0001f, gaugeSize.y, 1f), gaugeFill);
            gaugeBar.name = "Fill";
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = MaterialFor(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go.transform;
        }

        /// <summary>색마다 하나씩 나눠 쓰는 그림자 없는 단색 머티리얼(URP Unlit).</summary>
        private static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
            m = new Material(shader) { name = "StunDizzy", hideFlags = HideFlags.DontSave };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
            Materials[color] = m;
            return m;
        }
    }
}

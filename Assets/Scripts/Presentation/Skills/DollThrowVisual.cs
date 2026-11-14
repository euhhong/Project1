using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 인형 던지기 연출(IDollThrow, BE DollThrow를 읽어 그린다).
    /// - 좌클릭을 꾹 누르는 동안: 던지면 인형이 날아갈 포물선 보조선 + 떨어질 자리에 원(나비 놓기 미리보기처럼). 못 던지는 곳 = 붉게, 몸 근처(떼면 취소) = 흐리게.
    /// - 던진 인형: 날아가는 동안은 작은 검은 구체, 땅에 닿아 멈춰 있으면 꼭두각시 인형 모델(Resources EvilPuppet — 팔을 내린 자세, 공장장 쪽을 봄) + 바닥에 옅은 원.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그린 것은 캐릭터를 따라 움직이지 않게 따로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrowVisual : MonoBehaviour
    {
        [Tooltip("인형 구체 지름(m) — BE 판정 반지름(0.18)의 두 배 근처.")]
        [SerializeField, Min(0.05f)] private float dollSize = 0.36f;
        [SerializeField] private Color dollColor = new(0.04f, 0.03f, 0.03f, 1f);
        [Tooltip("포물선 보조선 굵기(m) / 색.")]
        [SerializeField, Min(0.005f)] private float arcWidth = 0.05f;
        [SerializeField] private Color arcColor = new(1f, 0.55f, 0.2f, 0.75f);
        [SerializeField] private Color invalidColor = new(1f, 0.25f, 0.2f, 0.6f);
        [Tooltip("떨어질 자리 원 반지름(m) / 색.")]
        [SerializeField, Min(0.05f)] private float ringRadius = 0.6f;
        [SerializeField] private Color ringColor = new(1f, 1f, 1f, 0.7f);
        [Tooltip("멈춰 있는 인형 모델(꼭두각시)의 키(m) / 팔을 내리는 정도(0 = T자세, 1 = 몸에 붙임).")]
        [SerializeField, Min(0.1f)] private float puppetHeight = 1f;
        [SerializeField, Range(0f, 1f)] private float puppetArmsDown = 0.85f;
        private const string PuppetResource = "EvilPuppet/EvilPuppet";

        private IDollThrow doll;
        private Transform root;
        private LineRenderer arc, ring, restRing;
        private GameObject ball, puppet;
        private bool wasResting;
        private const int RingSegments = 40;

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        private void OnDisable()
        {
            if (root != null) root.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (root != null) root.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            doll ??= GetComponent<IDollThrow>();
            if (doll == null) return;
            if (root == null) Build();

            // 겨누는 중: 포물선 + 떨어질 자리 원
            bool aiming = doll.Aiming && doll.AimArc.Count > 1;
            arc.gameObject.SetActive(aiming);
            ring.gameObject.SetActive(aiming);
            if (aiming)
            {
                var pts = doll.AimArc;
                arc.positionCount = pts.Count;
                for (int i = 0; i < pts.Count; i++) arc.SetPosition(i, pts[i]);
                Color c = doll.AimValid ? arcColor : invalidColor;
                arc.startColor = new Color(c.r, c.g, c.b, c.a * 0.4f); arc.endColor = c;
                ring.startColor = ring.endColor = doll.AimValid ? ringColor : invalidColor;
                SetRing(ring, doll.AimLanding, ringRadius * (1f + Mathf.Sin(Time.time * 6f) * 0.06f), Time.time * 60f);
            }

            // 인형: 날아가는 동안 작은 검은 구체, 멈춰 있으면 꼭두각시 모델 + 바닥 원
            bool resting = doll.DollActive && !doll.DollFlying;
            ball.SetActive(doll.DollActive && (!resting || puppet == null));
            if (ball.activeSelf) ball.transform.position = doll.DollPosition;
            if (puppet != null)
            {
                puppet.SetActive(resting);
                if (resting)
                {
                    Vector3 feet = doll.DollPosition - Vector3.up * (dollSize * 0.5f);
                    if (!wasResting)
                    {
                        // 멈춘 순간 공장장 쪽을 본다(자리를 바꿔도 다음에 멈출 때 다시)
                        Vector3 toOwner = transform.position - feet; toOwner.y = 0f;
                        puppet.transform.rotation = toOwner.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toOwner.normalized, Vector3.up) : Quaternion.Euler(0f, transform.eulerAngles.y + 180f, 0f);
                    }
                    puppet.transform.position = feet;
                }
            }
            wasResting = resting;
            restRing.gameObject.SetActive(resting);
            if (resting) SetRing(restRing, doll.DollPosition - Vector3.up * (dollSize * 0.5f), ringRadius * 0.6f, Time.time * -40f);
        }

        private void Build()
        {
            root = new GameObject("HellEmberDoll").transform;
            arc = Line("AimArc", arcColor, false);
            arc.widthMultiplier = arcWidth;
            ring = Line("AimRing", ringColor, true);
            ring.positionCount = RingSegments;
            restRing = Line("DollRing", new Color(1f, 0.45f, 0.15f, 0.45f), true);
            restRing.positionCount = RingSegments;

            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Doll";
            Destroy(ball.GetComponent<Collider>()); // 보이기만(판정은 BE)
            ball.transform.SetParent(root, false);
            ball.transform.localScale = Vector3.one * dollSize;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader != null ? shader : Shader.Find("Standard")) { color = dollColor };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            ball.GetComponent<MeshRenderer>().sharedMaterial = mat;
            ball.SetActive(false);
            puppet = BuildPuppet();
        }

        /// <summary>멈춰 있는 인형 모델: 꼭두각시(Resources EvilPuppet, 키 puppetHeight, 원점 = 발 사이 바닥). 팔을 T자세에서 내린다. 없으면 null(구체로 대신).</summary>
        private GameObject BuildPuppet()
        {
            var prefab = Resources.Load<GameObject>(PuppetResource);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root);
            go.name = "DollPuppet";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c); // 보이기만(판정은 BE)
            float h = 0f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - go.transform.position.y);
            if (h > 1e-3f) go.transform.localScale = Vector3.one * (puppetHeight / h);
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                LowerArm(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
                LowerArm(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
                animator.enabled = false; // 움직이는 모션 없이 그 자세로 서 있다
            }
            go.SetActive(false);
            return go;
        }

        /// <summary>T자세로 옆으로 뻗은 위팔을 아래로(puppetArmsDown만큼) 내린다 — 팔 전체가 같이 돈다.</summary>
        private void LowerArm(Animator animator, HumanBodyBones upper, HumanBodyBones lower)
        {
            Transform u = animator.GetBoneTransform(upper), l = animator.GetBoneTransform(lower);
            if (u == null || l == null) return;
            Vector3 along = l.position - u.position;
            if (along.sqrMagnitude < 1e-8f) return;
            Vector3 side = Vector3.ProjectOnPlane(along, Vector3.up).normalized;
            Vector3 want = Vector3.Slerp(side, (Vector3.down + side * 0.15f).normalized, puppetArmsDown);
            u.rotation = Quaternion.FromToRotation(along, want) * u.rotation;
        }

        private LineRenderer Line(string name, Color color, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = 0.06f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.gameObject.SetActive(false);
            return lr;
        }

        // 바닥 원, spin(도)만큼 돌린다.
        private static void SetRing(LineRenderer lr, Vector3 center, float radius, float spin)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments * 360f + spin) * Mathf.Deg2Rad;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.04f);
            }
        }
    }
}

using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 만화 같은 타격 효과("펑")와 늘어난 펀치 거리(리치). CharacterLoadout이 붙인다.
    /// - 펀치(punchReach 켬, 생존자 기본 공격 Mutant Punch를 쓰는 캐릭터): 몸 앞쪽에 판정 상자(Hitbox_Reach, 오른손 자리)를 둔다.
    ///   오른손 공격의 판정 구간에 같이 켜져서 주먹이 닿는 곳보다 멀리 맞는다. 판정 구간이 시작될 때 그 자리에 "펑"이 터진다.
    /// - 발차기(모든 생존자, 앞으로 달리다 뛴 공격 = 플라잉킥): 판정 구간이 시작될 때 앞으로 더 나간 발 앞에서 "펑"이 터진다.
    /// "펑" = 노랑·흰 별 두 겹 + 주먹/발에서 뻗는 속도선, 0.2초 만에 사라진다(PunchBurst).
    /// </summary>
    [DisallowMultipleComponent]
    public class CartoonPunch : MonoBehaviour
    {
        [Tooltip("펀치 리치·효과를 쓸지(생존자 기본 공격). 끄면 발차기 효과만 쓴다.")]
        [SerializeField] private bool punchReach = true;

        [Header("리치(판정 상자, 캐릭터 루트 기준 m)")]
        [Tooltip("몸 앞으로 판정이 닿는 거리.")]
        [SerializeField, Min(0.2f)] private float reach = 1.8f;
        [Tooltip("판정 상자가 시작되는 거리(몸 바로 앞).")]
        [SerializeField, Min(0f)] private float reachStart = 0.3f;
        [SerializeField, Min(0.1f)] private float reachWidth = 0.9f;
        [SerializeField, Min(0.1f)] private float reachHeight = 0.9f;
        [Tooltip("판정 상자 높이(캐릭터 키 대비, 가슴쯤).")]
        [SerializeField, Range(0.2f, 1f)] private float heightRatio = 0.62f;

        [Header("효과")]
        [SerializeField, Min(0.05f)] private float effectTime = 0.2f;
        [SerializeField, Min(0.1f)] private float burstSize = 1.1f;
        [Tooltip("발차기 \"펑\"을 발 앞으로 얼마나 띄울지(m).")]
        [SerializeField, Min(0f)] private float kickOffset = 0.35f;
        [SerializeField] private Color outerColor = new(1f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color innerColor = new(1f, 1f, 1f, 1f);

        private ICharacterAttackState attack;
        private Animator animator;
        private BoxCollider reachBox;
        private AttackPhase lastPhase = AttackPhase.None;
        private static Material material;

        /// <summary>펀치 리치·효과를 켜거나 끈다(캐릭터를 고를 때).</summary>
        public void SetPunchReach(bool on)
        {
            punchReach = on;
            if (on && reachBox == null && isActiveAndEnabled) BuildReachBox();
            else if (!on && reachBox != null) { Destroy(reachBox.gameObject); reachBox = null; RefreshDetector(); }
        }

        private void Awake() => attack = GetComponent<ICharacterAttackState>();

        private void Start()
        {
            if (punchReach && reachBox == null) BuildReachBox();
        }

        private void OnDestroy()
        {
            if (reachBox != null) Destroy(reachBox.gameObject);
        }

        private void BuildReachBox()
        {
            var cc = GetComponent<CharacterController>();
            float height = cc != null ? cc.height : 1.7f;
            var go = new GameObject("Hitbox_Reach");
            go.transform.SetParent(transform, false);
            reachBox = go.AddComponent<BoxCollider>();
            float length = Mathf.Max(reach - reachStart, 0.1f);
            reachBox.center = new Vector3(0f, height * heightRatio, reachStart + length * 0.5f);
            reachBox.size = new Vector3(reachWidth, reachHeight, length);
            go.AddComponent<Hitbox>().SetSlot(HitboxSlot.RightHand);
            RefreshDetector();
        }

        private void RefreshDetector()
        {
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void LateUpdate()
        {
            if (attack == null) return;
            var phase = attack.AttackPhase;
            bool striking = attack.AttackState == CharacterAttackState.Light || attack.AttackState == CharacterAttackState.Heavy;
            if (striking && phase == AttackPhase.Active && lastPhase != AttackPhase.Active)
            {
                if (attack.IsRunJumpAttack) SpawnKick();
                else if (punchReach) SpawnPunch();
            }
            lastPhase = phase;
        }

        private Animator Model()
        {
            if (animator == null || !animator.isActiveAndEnabled) animator = GetComponentInChildren<Animator>();
            return animator != null && animator.isHuman ? animator : null;
        }

        private float Scale => attack.AttackState == CharacterAttackState.Heavy ? 1.35f : 1f;

        private void SpawnPunch()
        {
            if (reachBox == null) return;
            Vector3 center = reachBox.transform.TransformPoint(reachBox.center + new Vector3(0f, 0f, reachBox.size.z * 0.3f));
            var hand = Model()?.GetBoneTransform(HumanBodyBones.RightHand);
            PunchBurst.Create(center, hand != null ? hand.position : center, burstSize * Scale, effectTime, outerColor, innerColor, GetMaterial());
        }

        private void SpawnKick()
        {
            var model = Model();
            if (model == null) return;
            var l = model.GetBoneTransform(HumanBodyBones.LeftFoot);
            var r = model.GetBoneTransform(HumanBodyBones.RightFoot);
            if (l == null && r == null) return;
            // 앞으로 더 나간 발이 차는 발이다.
            Transform foot = l == null ? r : r == null ? l
                : Vector3.Dot(l.position - transform.position, transform.forward) >= Vector3.Dot(r.position - transform.position, transform.forward) ? l : r;
            Vector3 center = foot.position + transform.forward * kickOffset;
            PunchBurst.Create(center, foot.position - transform.forward * 0.2f, burstSize * 0.9f * Scale, effectTime, outerColor, innerColor, GetMaterial());
        }

        private static Material GetMaterial()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "CartoonPunch", renderQueue = 3100 };
            return material;
        }
    }
}

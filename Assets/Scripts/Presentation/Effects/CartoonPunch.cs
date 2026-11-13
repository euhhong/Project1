using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 만화 같은 타격 효과("펑")와 늘어난 펀치 거리(리치). CharacterLoadout이 붙인다.
    /// - 펀치(punchReach 켬, 주먹 기본 공격 Mutant Punch·훅 펀치를 쓰는 캐릭터): 몸 앞쪽에 판정 상자(Hitbox_Reach, 오른손 자리)를 둔다.
    ///   오른손 공격의 판정 구간에 같이 켜져서 주먹이 닿는 곳보다 멀리 맞는다.
    ///   앞으로 이동하면서 치면 판정 끝이 조금 더 멀리 나간다(moveReachBonus, 달리기 속도에서 최대). 판정 구간이 시작될 때 판정 끝에 "펑"이 터져 닿는 곳을 보여 준다.
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
        [SerializeField, Min(0.1f)] private float reachHeight = 0.8f;
        [Tooltip("판정 상자 높이(캐릭터 키 대비). 상대 몸 가운데(배~가슴)를 덮되 아래쪽으로 — 점프 꼭대기에 있는 상대 발보다 낮게.")]
        [SerializeField, Range(0.2f, 1f)] private float heightRatio = 0.45f; // 몸 가운데를 노리되 조금 아래쪽(점프 꼭대기의 상대는 닿지 않게)
        [Tooltip("앞으로 이동하면서 칠 때 판정 끝이 더 나가는 거리(m, 달리기 속도에서 최대, 걷기면 그만큼 덜).")]
        [SerializeField, Min(0f)] private float moveReachBonus = 0.45f;

        [Header("효과")]
        [Tooltip("예전 만화 \"펑\"(노랑·흰 별, 말풍선 모양)을 터뜨릴지. 지금은 끔 — 타격 효과는 SwingTrail(휘두르는 궤적)이 맡는다. 펀치 리치(판정 상자)는 이것과 상관없이 그대로.")]
        [SerializeField] private bool showBurst = false;
        [SerializeField, Min(0.05f)] private float effectTime = 0.2f;
        [SerializeField, Min(0.1f)] private float burstSize = 1.1f;
        [Tooltip("발차기 \"펑\"을 발 앞으로 얼마나 띄울지(m).")]
        [SerializeField, Min(0f)] private float kickOffset = 0.35f;
        [SerializeField] private Color outerColor = new(1f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color innerColor = new(1f, 1f, 1f, 1f);

        private ICharacterAttackState attack;
        private ICharacterState state;
        private float bonus; // 이번 공격에서 판정 끝이 더 나간 거리(m)
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

        private void Awake()
        {
            attack = GetComponent<ICharacterAttackState>();
            state = GetComponent<ICharacterState>();
        }

        // 판정 상자 길이를 앞으로 가는 속도에 맞춘다(판정이 켜지기 전·켜진 동안 매 틱). 공격이 아니면 원래 길이.
        private void FixedUpdate()
        {
            if (reachBox == null || attack == null) return;
            bool striking = (attack.AttackState == CharacterAttackState.Light || attack.AttackState == CharacterAttackState.Heavy) && !attack.IsRunJumpAttack;
            if (!striking) bonus = 0f;
            else if (attack.AttackPhase == AttackPhase.Windup || attack.AttackPhase == AttackPhase.Active)
            {
                float fwd = state != null ? state.LocalPlanarVelocity.y : 0f;
                float run = state != null ? Mathf.Max(state.RunSpeed, 0.1f) : 6.5f;
                bonus = Mathf.Max(bonus, moveReachBonus * Mathf.Clamp01(fwd / run)); // 판정 중 멈춰도 줄지 않는다
            }
            SetReachLength(reach + bonus);
        }

        private void SetReachLength(float farEnd)
        {
            float length = Mathf.Max(farEnd - reachStart, 0.1f);
            var c = reachBox.center; var s = reachBox.size;
            reachBox.center = new Vector3(c.x, c.y, reachStart + length * 0.5f);
            reachBox.size = new Vector3(s.x, s.y, length);
        }

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
            if (showBurst && striking && phase == AttackPhase.Active && lastPhase != AttackPhase.Active)
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
            // 판정 끝(앞으로 가며 치면 더 멀리)에서 터뜨려 닿는 곳을 보여 준다.
            Vector3 center = reachBox.transform.TransformPoint(reachBox.center + new Vector3(0f, 0f, reachBox.size.z * 0.5f - 0.15f));
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

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 리퍼 손톱에 감기는 안개(BE).
    /// - 공격(약·강·모으기)도 스킬도 하지 않는 동안 chargeInterval(7초)마다 안개가 한 칸씩 모인다(최대 maxCharges = 2칸).
    ///   공격하거나 스킬을 쓰면 다음 칸까지 모으던 시간은 처음부터 다시 센다(이미 찬 칸은 그대로).
    /// - 2칸이 다 찬 상태에서 공격하면: 판정이 나는 순간(Active) 2칸을 모두 쓰고, 손톱 모양 안개 칼날이 공격 방향으로
    ///   맵 끝까지(maxRange) 일자로 날아간다(HomingProjectile, 목표 없음). 가장 먼저 닿은 것(상대·벽·바닥)에 맞고 부서진다.
    ///   덜 찬 상태의 공격은 칼날이 나가지 않는다.
    /// - E(MistBladeSkill)는 1칸만 써서 같은 칼날을 약하게(weakPower) 날린다.
    /// - 칼날 모양을 휘두른 궤적대로 세우도록, 손톱(무기 판정 상자, 없으면 왼손)이 지나간 방향을 LastSwingAxis로 알린다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 리퍼만). 그리기는 FE(MistClawVisual, MistBladeTrail). 체력·데미지는 다루지 않는다(세기 배율만 HitResult.Power로 알린다).
    /// </summary>
    [DisallowMultipleComponent]
    public class MistCharge : MonoBehaviour, IMistCharge
    {
        public const string BladeKind = "MistBlade";

        [Tooltip("안개 한 칸이 모이는 시간(초) — 공격하지 않고 있어야 모인다.")]
        [SerializeField, Min(0.1f)] private float chargeInterval = 7f;
        [Tooltip("모을 수 있는 칸 수. 다 찬 상태에서 공격해야 칼날이 나간다.")]
        [SerializeField, Min(1)] private int maxCharges = 2;

        [Header("안개 칼날")]
        [Tooltip("날아가는 속도(m/s).")]
        [SerializeField, Min(1f)] private float bladeSpeed = 30f;
        [Tooltip("날아가는 최대 거리(m) — 맵 끝까지.")]
        [SerializeField, Min(1f)] private float maxRange = 200f;
        [Tooltip("상대를 맞히는 굵기(반지름, m) — 2칸 공격 / E(1칸). 벽·바닥에 닿는 굵기.")]
        [SerializeField, Min(0.05f)] private float fullHitRadius = 0.6f, weakHitRadius = 0.45f, wallRadius = 0.12f;
        [Tooltip("세기 배율(HitResult.Power) — 2칸 공격 / E(1칸, 약함).")]
        [SerializeField, Min(0.01f)] private float fullPower = 1f, weakPower = 0.5f;
        [Tooltip("날아가기 시작하는 자리(캐릭터 키 대비 높이 / 몸 앞 m).")]
        [SerializeField, Range(0.1f, 1f)] private float heightRatio = 0.55f;
        [SerializeField, Min(0f)] private float forwardOffset = 0.6f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        private const int Samples = 6;
        private readonly Vector3[] clawSamples = new Vector3[Samples];
        private int sampleCount, sampleHead;
        private CharacterAttack attack;
        private ICharacterSkillState skillState;
        private CharacterIdentity self;
        private Hitbox claw;
        private AttackPhase lastPhase;
        private int bladeCounter;

        public int Charges { get; private set; }
        public int MaxCharges => maxCharges;
        public float NextProgress { get; private set; }
        public float ChargeInterval => chargeInterval;
        public Vector3 LastSwingAxis { get; private set; } = Vector3.up;
        public float WeakPower => weakPower;

        private void Awake()
        {
            attack = GetComponent<CharacterAttack>();
            self = GetComponent<CharacterIdentity>();
        }

        private void OnDisable()
        {
            Charges = 0; NextProgress = 0f; sampleCount = 0; lastPhase = AttackPhase.None;
        }

        /// <summary>안개를 n칸 쓴다(모자라면 쓰지 않고 false).</summary>
        public bool TrySpend(int n)
        {
            if (Charges < n) return false;
            Charges -= n;
            return true;
        }

        private void FixedUpdate()
        {
            if (attack == null) Awake();
            if (skillState == null) TryGetComponent(out skillState);
            SampleClaw();

            bool attacking = attack != null && attack.AttackState != CharacterAttackState.None;
            bool casting = skillState != null && skillState.State != SkillState.Ready && skillState.State != SkillState.Cooldown;

            // 다 찬 상태의 공격 → 판정이 나는 순간 2칸을 쓰고 칼날
            AttackPhase phase = attack != null ? attack.AttackPhase : AttackPhase.None;
            if (attacking && phase == AttackPhase.Active && lastPhase != AttackPhase.Active && Charges >= maxCharges)
            {
                Charges = 0;
                LaunchBlade(false);
            }
            lastPhase = phase;

            // 공격·스킬을 하지 않는 동안에만 모인다(하면 다음 칸까지 처음부터)
            if (Charges >= maxCharges) { NextProgress = 0f; return; }
            if (attacking || casting) { NextProgress = 0f; return; }
            NextProgress += Time.fixedDeltaTime / chargeInterval;
            if (NextProgress >= 1f) { Charges++; NextProgress = 0f; }
        }

        /// <summary>안개 칼날을 몸 앞 방향(수평)으로 날린다. weak = E(1칸) 칼날.</summary>
        public void LaunchBlade(bool weak)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward; // 몸 = 시야 방향
            var cc = GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            Vector3 origin = transform.position + Vector3.up * h * heightRatio + fwd * forwardOffset;
            LastSwingAxis = SwingAxis(fwd);
            var blade = HomingProjectile.Launch(BladeKind, self, null, origin, fwd, bladeSpeed, 0f,
                weak ? weakHitRadius : fullHitRadius, wallRadius, maxRange / bladeSpeed, slot, -(300000 + ++bladeCounter),
                weak ? weakPower : fullPower);
            if (TryGetComponent(out MistTrailField trail)) trail.Track(blade, origin, LastSwingAxis, weak ? weakPower : fullPower); // 지나간 자리에 안개를 남긴다
        }

        /// <summary>최근 손톱이 지나간 방향(날아가는 방향에 수직인 면 위). 거의 안 움직였으면 위아래(내려치기).</summary>
        private Vector3 SwingAxis(Vector3 fwd)
        {
            if (sampleCount >= 2)
            {
                Vector3 newest = clawSamples[(sampleHead - 1 + Samples) % Samples];
                Vector3 oldest = clawSamples[(sampleHead - sampleCount + Samples) % Samples];
                Vector3 m = Vector3.ProjectOnPlane(newest - oldest, fwd);
                if (m.magnitude > 0.05f) return m.normalized;
            }
            return Vector3.up;
        }

        private void SampleClaw()
        {
            if (claw == null || !claw.isActiveAndEnabled)
            {
                claw = null; sampleCount = 0;
                foreach (var hb in GetComponentsInChildren<Hitbox>())
                {
                    if (hb.Slot == HitboxSlot.Weapon) { claw = hb; break; }
                    if (hb.Slot == HitboxSlot.LeftHand) claw = hb;
                }
                if (claw == null) return;
            }
            clawSamples[sampleHead] = claw.transform.position;
            sampleHead = (sampleHead + 1) % Samples;
            sampleCount = Mathf.Min(sampleCount + 1, Samples);
        }
    }
}

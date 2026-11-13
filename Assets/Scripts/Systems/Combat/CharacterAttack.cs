using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 입력(LightAttack / HeavyCharging / HeavyRelease)을 받아 공격 상태를 진행한다.
    /// - 상태: 없음 → 약공격, 또는 없음 → 강공격 모으기 → 강공격.
    /// - 공격마다 준비 → 판정 → 후딜레이. 판정 구간에만 AttackHitDetector로 Hitbox를 켠다.
    ///   땅: 약=오른손, 강=오른손+소품. 앞으로 달리다 뛴 공중 공격(IsRunJumpAttack, 플라잉킥 등): 양발. 그 밖의 공중 공격은 땅 공격과 같다.
    /// - 다른 행동을 막는 스킬(ICharacterSkillState.LocksActions, 예: 전위 돌진)·기절 중에는 공격을 시작하지 않는다.
    /// - 공중에서는 꾹 눌러 모을 수 없다: 누르는 순간 공중 공격이 한 번 나가고, 키를 뗄 때까지 새 입력을 받지 않는다.
    /// - 공격 중에는 새 공격을 받지 않는다. 공중에서도 공격할 수 있고(IsAirAttack), 도중에 착지해도 공격은 끝까지 이어진다.
    /// - 땅에서 강공격을 모으는 동안에는 걷기 속도로 제한한다(IMoveSpeedLimit). 공중에서는 점프 궤적을 건드리지 않는다.
    /// 공격 방향은 몸 방향(= 시야 Yaw, CharacterMotor가 맞춘다)이다. 데미지는 다루지 않는다.
    /// </summary>
    [RequireComponent(typeof(AttackHitDetector))]
    [DisallowMultipleComponent]
    public class CharacterAttack : MonoBehaviour, ICharacterAttackState, IMoveSpeedLimit
    {
        [SerializeField] private AttackSpec lightAttack = new(0.1f, 0.1f, 0.25f, HitboxSlot.RightHand);
        [SerializeField] private AttackSpec heavyAttack = new(0.25f, 0.15f, 0.5f, HitboxSlot.RightHand | HitboxSlot.Weapon);

        [Header("달리기 점프 공격 (앞으로 달리다 뛴 공중 공격, 발 판정)")]
        [Tooltip("공중 공격을 시작할 때 앞으로 이 속도 비율(걷기 0 ~ 달리기 1) 이상이면 달리기 점프 공격으로 본다.")]
        [SerializeField, Range(0f, 1f)] private float runJumpThreshold = 0.5f;
        [SerializeField] private AttackSpec airLightAttack = new(0.12f, 0.12f, 0.30f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);
        [SerializeField] private AttackSpec airHeavyAttack = new(0.20f, 0.15f, 0.40f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);

        private ICharacterInputSource inputSource;
        private ICharacterState characterState;
        private AttackHitDetector hitDetector;
        private ICharacterSkillState skillState; // 실행 중에 붙을 수 있어 필요할 때 찾는다
        private ICharacterStun stun;
        private bool waitAirRelease; // 공중에서 누른 공격 키를 뗄 때까지 새 입력을 받지 않는다

        private float phaseTime;

        public CharacterAttackState AttackState { get; private set; }
        public AttackPhase AttackPhase { get; private set; }
        public float PhaseProgress { get; private set; }
        public float ChargeTime { get; private set; }
        public bool IsAirAttack { get; private set; }
        public bool IsRunJumpAttack { get; private set; }

        public event Action<CharacterAttackState> AttackStarted;
        public event Action ChargeStarted;
        public event Action AttackEnded;

        public AttackSpec LightAttack => lightAttack;
        public AttackSpec HeavyAttack => heavyAttack;
        public AttackSpec AirLightAttack => airLightAttack;
        public AttackSpec AirHeavyAttack => airHeavyAttack;

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            if (AttackState == CharacterAttackState.HeavyCharge && characterState != null && characterState.IsGrounded)
            {
                maxSpeed = characterState.WalkSpeed;
                return true;
            }
            maxSpeed = 0f;
            return false;
        }

        private HitboxSlot defaultLightSlots, defaultHeavySlots;
        private bool slotDefaultsSaved;

        /// <summary>
        /// 약·강공격 판정 부위를 바꾼다(캐릭터를 고를 때, 예: 리퍼 = 왼손 + 무기). None이면 처음(인스펙터) 값으로 되돌린다.
        /// </summary>
        /// <summary>
        /// 지금 누르고 있는 공격 키는 뗄 때까지 무시한다(뗄 때 나오는 약공격·강공격 입력도 버린다).
        /// 다른 기능이 공격 키 누름을 이미 썼을 때 부른다(예: 광대 로켓 들기를 좌클릭으로 취소 — 그 클릭으로 공격이 나가지 않게).
        /// </summary>
        public void IgnoreAttackUntilRelease() => waitAirRelease = true;

        public void SetAttackSlots(HitboxSlot light, HitboxSlot heavy)
        {
            if (!slotDefaultsSaved) { defaultLightSlots = lightAttack.Slots; defaultHeavySlots = heavyAttack.Slots; slotDefaultsSaved = true; }
            lightAttack.Slots = light == HitboxSlot.None ? defaultLightSlots : light;
            heavyAttack.Slots = heavy == HitboxSlot.None ? defaultHeavySlots : heavy;
        }

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            characterState = GetComponent<ICharacterState>();
            hitDetector = GetComponent<AttackHitDetector>();
        }

        private void OnDisable()
        {
            if (AttackState != CharacterAttackState.None)
            {
                EndAttack();
            }
        }

        private void FixedUpdate()
        {
            if (hitDetector == null)
            {
                Awake(); // Play 중 재컴파일로 참조가 비었을 때 복구
            }

            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            bool grounded = characterState == null || characterState.IsGrounded;
            float dt = Time.fixedDeltaTime;

            // 공중에서 누른 공격 키를 뗐으면(뗄 때 약공격 또는 강공격 발동 입력이 온다) 다시 입력을 받는다.
            if (waitAirRelease && (input.LightAttack || input.HeavyRelease))
            {
                waitAirRelease = false;
                input.LightAttack = false;
                input.HeavyRelease = false;
            }

            switch (AttackState)
            {
                case CharacterAttackState.None:
                    // 다른 행동을 막는 스킬(예: 전위 돌진)·기절 중에는 공격을 시작하지 않는다.
                    if (skillState == null) TryGetComponent(out skillState);
                    if (stun == null) TryGetComponent(out stun);
                    if ((skillState != null && skillState.LocksActions) || (stun != null && stun.IsStunned))
                    {
                        break;
                    }
                    // 공중에서는 꾹 눌러 모을 수 없다: 누르는 순간 바로 공중 공격(첫 입력만), 키를 뗄 때까지 다른 입력은 무시한다.
                    if (!grounded && input.AttackPressed)
                    {
                        IsAirAttack = true;
                        waitAirRelease = !(input.LightAttack || input.HeavyRelease); // 아주 짧게 눌러 같은 틱에 뗐으면 기다리지 않는다
                        StartAttack(CharacterAttackState.Light);
                    }
                    else if (waitAirRelease)
                    {
                        // 공중에서 누른 키를 아직 떼지 않았다(뗄 때 나오는 약공격·강공격 입력도 버린다).
                    }
                    else if (input.LightAttack)
                    {
                        IsAirAttack = !grounded;
                        StartAttack(CharacterAttackState.Light);
                    }
                    else if (input.HeavyCharging && TryGetComponent(out IAttackHoldOverride hold) && hold.OverridesHeavyHold)
                    {
                        // 꾹 누르기는 다른 기능이 쓴다(예: 붉은나비 나비 놓기). 강공격을 모으지 않는다.
                    }
                    else if (input.HeavyCharging && !grounded)
                    {
                        // 땅에서 누르기 시작해 공중에서 0.3초를 넘긴 경우: 모으지 않는다(뗄 때까지 무시).
                        waitAirRelease = true;
                    }
                    else if (input.HeavyCharging)
                    {
                        IsAirAttack = !grounded;
                        AttackState = CharacterAttackState.HeavyCharge;
                        ChargeTime = 0f;
                        ChargeStarted?.Invoke();
                    }
                    break;

                case CharacterAttackState.HeavyCharge:
                    ChargeTime += dt;
                    if (input.HeavyRelease || !input.HeavyCharging)
                    {
                        ChargeTime = 0f;
                        StartAttack(CharacterAttackState.Heavy);
                    }
                    break;

                case CharacterAttackState.Light:
                case CharacterAttackState.Heavy:
                    AdvancePhase(dt);
                    break;
            }
        }

        // 공중 전용 구간(발 판정)은 앞으로 달리다 뛴 공격(플라잉킥 등)에만 쓴다. 그 밖의 공중 공격은 땅 공격과 같은 구간·손 판정.
        private AttackSpec CurrentSpec => AttackState == CharacterAttackState.Heavy
            ? (IsRunJumpAttack ? airHeavyAttack : heavyAttack)
            : (IsRunJumpAttack ? airLightAttack : lightAttack);

        /// <summary>앞으로 달리는 중인지(걷기~달리기 속도 사이 runJumpThreshold 이상, 앞쪽이 옆보다 큼).</summary>
        private bool IsRunningForward()
        {
            if (characterState == null) return false;
            Vector2 v = characterState.LocalPlanarVelocity;
            float walk = characterState.WalkSpeed, run = Mathf.Max(characterState.RunSpeed, walk + 0.01f);
            return v.y > Mathf.Abs(v.x) && v.y >= Mathf.Lerp(walk, run, runJumpThreshold);
        }

        private void StartAttack(CharacterAttackState state)
        {
            IsRunJumpAttack = IsAirAttack && IsRunningForward();
            AttackState = state;
            AttackPhase = AttackPhase.Windup;
            phaseTime = 0f;
            PhaseProgress = 0f;
            AttackStarted?.Invoke(state);
            AdvancePhase(0f); // 길이 0인 구간은 바로 넘긴다
        }

        private void AdvancePhase(float dt)
        {
            phaseTime += dt;
            AttackSpec spec = CurrentSpec;

            while (true)
            {
                float length = PhaseLength(spec, AttackPhase);
                if (phaseTime < length)
                {
                    PhaseProgress = length > 0f ? phaseTime / length : 1f;
                    return;
                }

                phaseTime -= length;
                switch (AttackPhase)
                {
                    case AttackPhase.Windup:
                        AttackPhase = AttackPhase.Active;
                        hitDetector.BeginAttack(spec.Slots);
                        break;
                    case AttackPhase.Active:
                        AttackPhase = AttackPhase.Recovery;
                        hitDetector.EndAttack();
                        break;
                    default:
                        EndAttack();
                        return;
                }
            }
        }

        private static float PhaseLength(AttackSpec spec, AttackPhase phase)
        {
            return phase switch
            {
                AttackPhase.Windup => spec.Windup,
                AttackPhase.Active => spec.Active,
                AttackPhase.Recovery => spec.Recovery,
                _ => 0f,
            };
        }

        private void EndAttack()
        {
            if (hitDetector != null && hitDetector.IsAttacking)
            {
                hitDetector.EndAttack();
            }
            AttackState = CharacterAttackState.None;
            AttackPhase = AttackPhase.None;
            PhaseProgress = 0f;
            ChargeTime = 0f;
            phaseTime = 0f;
            IsAirAttack = false;
            IsRunJumpAttack = false;
            AttackEnded?.Invoke();
        }
    }
}

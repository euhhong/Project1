using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 입력(LightAttack / HeavyCharging / HeavyRelease)을 받아 공격 상태를 진행한다.
    /// - 상태: 없음 → 약공격, 또는 없음 → 강공격 모으기 → 강공격.
    /// - 공격마다 준비 → 판정 → 후딜레이. 판정 구간에만 AttackHitDetector로 Hitbox를 켠다.
    ///   땅: 약=오른손, 강=오른손+소품. 공중(발로 내려찍기): 약·강 모두 양발. 구간 길이는 공격마다 따로.
    /// - 다른 행동을 막는 스킬(ICharacterSkillState.LocksActions, 예: 전위 돌진) 중에는 공격을 시작하지 않는다.
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

        [Header("공중 공격 (발로 내려찍기)")]
        [SerializeField] private AttackSpec airLightAttack = new(0.12f, 0.12f, 0.30f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);
        [SerializeField] private AttackSpec airHeavyAttack = new(0.20f, 0.15f, 0.40f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);

        private ICharacterInputSource inputSource;
        private ICharacterState characterState;
        private AttackHitDetector hitDetector;
        private ICharacterSkillState skillState; // 실행 중에 붙을 수 있어 필요할 때 찾는다
        private ICharacterStun stun;

        private float phaseTime;

        public CharacterAttackState AttackState { get; private set; }
        public AttackPhase AttackPhase { get; private set; }
        public float PhaseProgress { get; private set; }
        public float ChargeTime { get; private set; }
        public bool IsAirAttack { get; private set; }

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
                    if (input.LightAttack)
                    {
                        IsAirAttack = !grounded;
                        StartAttack(CharacterAttackState.Light);
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

        private AttackSpec CurrentSpec => AttackState == CharacterAttackState.Heavy
            ? (IsAirAttack ? airHeavyAttack : heavyAttack)
            : (IsAirAttack ? airLightAttack : lightAttack);

        private void StartAttack(CharacterAttackState state)
        {
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
            AttackEnded?.Invoke();
        }
    }
}

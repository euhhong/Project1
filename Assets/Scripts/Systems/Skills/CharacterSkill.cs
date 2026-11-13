using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 고유 스킬을 돌린다. 입력(CharacterInputFrame.Skill, E 키)을 받으면 시전 → 효과 → 재사용 대기.
    /// - 어떤 스킬인지는 SkillDefinition(캐릭터별 데이터)이 정한다. 캐릭터를 고를 때 SetSkill로 넣는다.
    /// - 공격·기절 중에는 시작하지 않는다. 시전 중 공격: LocksActions인 스킬(돌진)은 공격을 막고, 아니면(자가치료) 시전이 끊긴다.
    ///   시전 중 기절하면 시전이 끊긴다.
    /// - 시전 중 이동: 스킬이 이동을 정하면(돌진) IMoveOverride로, 아니면 castMoveSpeed로 속도만 제한한다(IMoveSpeedLimit).
    /// - 스킬이 ShouldEndEarly(예: 장애물에 닿음)를 알리면 그때 끝낸다. 이때 돌려준 시간만큼 Recovering(넘어짐)으로 못 움직인다.
    /// 캐릭터 루트에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSkill : MonoBehaviour, ICharacterSkillState, IMoveSpeedLimit, IMoveOverride, IFacingOverride
    {
        [SerializeField] private SkillDefinition skill;

        private ICharacterInputSource inputSource;
        private ICharacterAttackState attack;
        private ICharacterStun stun; // 실행 중에 붙을 수 있어 필요할 때 찾는다
        private float castTime;
        private float recoveryTime, recoveryDuration;

        public SkillDefinition Skill => skill;
        public string SkillId => skill != null ? skill.id : string.Empty;
        public string SkillName => skill != null ? skill.displayName : string.Empty;
        public SkillState State { get; private set; }
        public float CastProgress { get; private set; }
        public float RecoveryProgress { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CooldownDuration => skill != null ? skill.cooldown : 0f;
        public string BlockedReason { get; private set; }

        // 스킬 데이터(SkillDefinition)가 이 캐릭터의 진행 상태를 적어 두는 칸. 시전을 시작할 때 비운다.
        /// <summary>스킬 안의 단계(예: 도약 0 = 떠오름, 1 = 내려찍기).</summary>
        public int Phase { get; set; }
        /// <summary>지금 단계가 시작된 뒤 지난 시간(초).</summary>
        public float PhaseTime { get; set; }
        /// <summary>이번 시전 번호(공격 판정 AttackId로 쓴다).</summary>
        public int UseCount { get; private set; }
        public bool ShowArea { get; set; }
        public Vector3 AreaCenter { get; set; }
        public float AreaRadius { get; set; }
        /// <summary>스킬이 쓰는 방향(예: 용병 도약 방향, 시야 기준 x = 우, z = 앞).</summary>
        public Vector3 Direction { get; set; }
        /// <summary>지금 뻗어 나간 길이(m, 예: 사냥터지기 그랩). 0이면 안 나가 있다.</summary>
        public float Reach { get; set; }
        /// <summary>뻗어 나가는 시작점(월드, 예: 사냥터지기 그랩 = 서 있을 때의 오른쪽 어깨).</summary>
        public Vector3 ReachOrigin { get; set; }

        public bool ShowAfterimage => skill != null && State == SkillState.Casting && skill.Afterimage(this);
        public string StatusText => skill != null && State == SkillState.Casting ? skill.StatusText(this) : null;

        public string AnimationState =>
            skill == null || (State != SkillState.Casting && State != SkillState.Recovering) ? string.Empty : skill.AnimationState(this);

        // 넘어짐·착지 경직(Recovering)은 모션 길이를 경직 시간에 맞춘다(예: 루키노 Hard Landing, 전위 Fall_1).
        public bool SyncAnimationToProgress => skill != null && (State == SkillState.Recovering || skill.SyncAnimationToCast);

        public AttackPhase MotionPhase =>
            skill != null && State == SkillState.Casting && skill.TryGetMotionPhase(this, out var phase, out _) ? phase : AttackPhase.None;

        public float MotionPhaseProgress =>
            skill != null && State == SkillState.Casting && skill.TryGetMotionPhase(this, out _, out var p) ? Mathf.Clamp01(p) : 0f;

        /// <summary>단계를 바꾼다(PhaseTime을 0으로).</summary>
        public void SetPhase(int phase)
        {
            Phase = phase;
            PhaseTime = 0f;
        }

        public bool LocksActions =>
            State == SkillState.Recovering || (State == SkillState.Casting && skill != null && skill.LocksActions);

        public bool CanUse
        {
            get
            {
                if (skill == null) { BlockedReason = "스킬 없음"; return false; }
                if (State != SkillState.Ready) { BlockedReason = null; return false; }
                if (IsStunned) { BlockedReason = "기절"; return false; }
                if (attack != null && attack.AttackState != CharacterAttackState.None) { BlockedReason = "공격 중"; return false; }
                bool ok = skill.CanStart(gameObject, out var reason);
                BlockedReason = ok ? null : reason;
                return ok;
            }
        }

        public event Action SkillStarted;
        public event Action<bool> SkillEnded;

        private bool IsStunned
        {
            get
            {
                if (stun == null) TryGetComponent(out stun);
                return stun != null && stun.IsStunned;
            }
        }

        /// <summary>스킬을 바꾼다(캐릭터 선택 때). 진행 중인 시전·대기는 지운다.</summary>
        public void SetSkill(SkillDefinition newSkill)
        {
            if (State == SkillState.Casting) EndCast(false);
            skill = newSkill;
            State = SkillState.Ready;
            CooldownRemaining = 0f;
            RecoveryProgress = 0f;
        }

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            maxSpeed = skill != null ? skill.castMoveSpeed : 0f;
            return State == SkillState.Casting && skill != null && skill.LimitsMoveSpeed && !TryGetMoveOverride(out _, out _, out _);
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            if (State == SkillState.Casting && skill != null)
                return skill.TryGetMoveOverride(this, out localMove, out speed, out allowJump);
            if (State == SkillState.Recovering)
            {
                // 넘어져 있는 동안: 제자리, 점프 불가.
                localMove = Vector2.zero; speed = 0f; allowJump = false;
                return true;
            }
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        public bool TryGetFacingYaw(out float yaw)
        {
            yaw = 0f;
            return State == SkillState.Casting && skill != null && skill.TryGetFacingYaw(this, out yaw);
        }

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            attack = GetComponent<ICharacterAttackState>();
        }

        private void OnDisable()
        {
            if (State == SkillState.Casting) EndCast(false);
        }

        private void FixedUpdate()
        {
            if (inputSource == null) Awake(); // Play 중 재컴파일로 참조가 비었을 때 복구
            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            float dt = Time.fixedDeltaTime;

            switch (State)
            {
                case SkillState.Ready:
                    if (input.Skill && CanUse) StartCast();
                    break;

                case SkillState.Casting:
                    bool interruptedByAttack = skill.CancelledByAttack && attack != null && attack.AttackState != CharacterAttackState.None;
                    if (interruptedByAttack || IsStunned)
                    {
                        EndCast(false);
                        break;
                    }
                    castTime += dt;
                    CastProgress = skill.castTime > 0f ? Mathf.Clamp01(castTime / skill.castTime) : 1f;
                    PhaseTime += dt;
                    float recovery = 0f;
                    bool early = castTime < skill.castTime && skill.Tick(this, input, castTime, out recovery);
                    if (castTime >= skill.castTime || early)
                    {
                        skill.Complete(gameObject);
                        EndCast(true, early ? recovery : 0f);
                    }
                    break;

                case SkillState.Recovering:
                    recoveryTime += dt;
                    RecoveryProgress = recoveryDuration > 0f ? Mathf.Clamp01(recoveryTime / recoveryDuration) : 1f;
                    if (recoveryTime >= recoveryDuration) StartCooldown();
                    break;

                case SkillState.Cooldown:
                    CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
                    if (CooldownRemaining <= 0f) State = SkillState.Ready;
                    break;
            }
        }

        private void StartCast()
        {
            State = SkillState.Casting;
            castTime = 0f;
            CastProgress = 0f;
            UseCount++;
            SetPhase(0);
            ShowArea = false;
            Reach = 0f;
            skill.OnStart(this);
            SkillStarted?.Invoke();
        }

        private void EndCast(bool completed, float recovery = 0f)
        {
            CastProgress = 0f;
            ShowArea = false;
            Reach = 0f;
            if (completed && recovery > 0f)
            {
                // 도중에 막혀 끝남(예: 돌진이 벽에 부딪힘) → 넘어져 있다가 재사용 대기.
                State = SkillState.Recovering;
                recoveryTime = 0f;
                recoveryDuration = recovery;
                RecoveryProgress = 0f;
            }
            else if (completed)
            {
                StartCooldown();
            }
            else
            {
                State = SkillState.Ready;
            }
            SkillEnded?.Invoke(completed);
        }

        private void StartCooldown()
        {
            RecoveryProgress = 0f;
            if (skill != null && skill.cooldown > 0f)
            {
                State = SkillState.Cooldown;
                CooldownRemaining = skill.cooldown;
            }
            else
            {
                State = SkillState.Ready;
            }
        }
    }
}

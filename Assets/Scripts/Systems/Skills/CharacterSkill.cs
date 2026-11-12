using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 고유 스킬을 돌린다. 입력(CharacterInputFrame.Skill, E 키)을 받으면 시전 → 효과 → 재사용 대기.
    /// - 어떤 스킬인지는 SkillDefinition(캐릭터별 데이터)이 정한다. 캐릭터를 고를 때 SetSkill로 넣는다.
    /// - 공격 중에는 시작하지 않는다. 시전 중 공격: LocksActions인 스킬(돌진)은 공격을 막고, 아니면(자가치료) 시전이 끊긴다.
    /// - 시전 중 이동: 스킬이 이동을 정하면(돌진) IMoveOverride로, 아니면 castMoveSpeed로 속도만 제한한다(IMoveSpeedLimit).
    /// - 스킬이 ShouldEndEarly(예: 장애물에 닿음)를 알리면 그때 끝낸다.
    /// 캐릭터 루트에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSkill : MonoBehaviour, ICharacterSkillState, IMoveSpeedLimit, IMoveOverride
    {
        [SerializeField] private SkillDefinition skill;

        private ICharacterInputSource inputSource;
        private ICharacterAttackState attack;
        private float castTime;

        public SkillDefinition Skill => skill;
        public string SkillId => skill != null ? skill.id : string.Empty;
        public string SkillName => skill != null ? skill.displayName : string.Empty;
        public SkillState State { get; private set; }
        public float CastProgress { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CooldownDuration => skill != null ? skill.cooldown : 0f;
        public string BlockedReason { get; private set; }
        public bool LocksActions => State == SkillState.Casting && skill != null && skill.LocksActions;

        public bool CanUse
        {
            get
            {
                if (skill == null) { BlockedReason = "스킬 없음"; return false; }
                if (State != SkillState.Ready) { BlockedReason = null; return false; }
                if (attack != null && attack.AttackState != CharacterAttackState.None) { BlockedReason = "공격 중"; return false; }
                bool ok = skill.CanStart(gameObject, out var reason);
                BlockedReason = ok ? null : reason;
                return ok;
            }
        }

        public event Action SkillStarted;
        public event Action<bool> SkillEnded;

        /// <summary>스킬을 바꾼다(캐릭터 선택 때). 진행 중인 시전·대기는 지운다.</summary>
        public void SetSkill(SkillDefinition newSkill)
        {
            if (State == SkillState.Casting) EndCast(false);
            skill = newSkill;
            State = SkillState.Ready;
            CooldownRemaining = 0f;
        }

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            maxSpeed = skill != null ? skill.castMoveSpeed : 0f;
            return State == SkillState.Casting && !TryGetMoveOverride(out _, out _, out _);
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            if (State == SkillState.Casting && skill != null)
                return skill.TryGetMoveOverride(gameObject, out localMove, out speed, out allowJump);
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
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
                    if (!skill.LocksActions && attack != null && attack.AttackState != CharacterAttackState.None)
                    {
                        EndCast(false);
                        break;
                    }
                    castTime += dt;
                    CastProgress = skill.castTime > 0f ? Mathf.Clamp01(castTime / skill.castTime) : 1f;
                    if (castTime >= skill.castTime || skill.ShouldEndEarly(gameObject, castTime))
                    {
                        skill.Complete(gameObject);
                        EndCast(true);
                    }
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
            SkillStarted?.Invoke();
        }

        private void EndCast(bool completed)
        {
            CastProgress = 0f;
            if (completed && skill != null && skill.cooldown > 0f)
            {
                State = SkillState.Cooldown;
                CooldownRemaining = skill.cooldown;
            }
            else
            {
                State = SkillState.Ready;
            }
            SkillEnded?.Invoke(completed);
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>AI가 본 상대 한 순간의 모습(AiOpponentView가 틱마다 찍는다).</summary>
    public struct AiOpponentSnapshot
    {
        public bool Valid;
        public float Time;
        public Vector3 Position;
        /// <summary>수평 앞 방향(정규화).</summary>
        public Vector3 Forward;
        /// <summary>수평 속도(m/s).</summary>
        public Vector3 Velocity;
        public CharacterAttackState Attack;
        public AttackPhase Phase;
        public bool RunJumpAttack;
        public bool Parrying;
        public bool Stunned;
        public bool Dead;
        public bool Grounded;
        public bool IsHunter;
        public SkillState Skill;
        public string SkillId;
        public bool SkillLocksActions;

        /// <summary>휘두르는 중(약·강 공격, 모으기 제외).</summary>
        public readonly bool Swinging => Attack is CharacterAttackState.Light or CharacterAttackState.Heavy;

        /// <summary>판정이 곧 나오거나 나오는 중(준비·판정 구간).</summary>
        public readonly bool Threatening => Swinging && Phase is AttackPhase.Windup or AttackPhase.Active;

        /// <summary>후딜레이(헛치거나 친 뒤 마무리 중) — 파고들 틈.</summary>
        public readonly bool Recovering => Swinging && Phase == AttackPhase.Recovery;

        /// <summary>스킬을 쓰는 중이라 다른 행동을 못 함(돌진·자가치료 등).</summary>
        public readonly bool SkillBusy => Skill == SkillState.Recovering || (Skill == SkillState.Casting && SkillLocksActions && !ChargingAtSomeone);

        /// <summary>돌진형 스킬(전위·광대)로 달려드는 중 — 틈이 아니라 피해야 할 때.</summary>
        public readonly bool ChargingAtSomeone => Skill == SkillState.Casting && AiSkillProfile.For(SkillId).Charge;
    }

    /// <summary>
    /// AI의 눈: 상대 캐릭터의 상태(위치·속도·공격 구간·막기·기절·스킬)를 틱마다 기록해 두고,
    /// 사람처럼 반응이 늦게 보이도록 delay초 전의 모습(Seen)을 꺼내 준다. 판단은 하지 않는다.
    /// </summary>
    public sealed class AiOpponentView
    {
        private const int Capacity = 64; // 50Hz 기준 약 1.3초

        private readonly AiOpponentSnapshot[] ring = new AiOpponentSnapshot[Capacity];
        private int head = -1, count;

        private Transform target;
        private ICharacterAttackState attack;
        private ICharacterParryState parry;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterSkillState skill;
        private ICharacterState state;
        private ICharacterTraversalState traversal;

        public Transform Target => target;

        /// <summary>가장 최근에 찍은 모습(반응 지연 없이 — 위치·거리 계산처럼 몸으로 아는 것에 쓴다).</summary>
        public AiOpponentSnapshot Latest => count > 0 ? ring[head] : default;

        public void SetTarget(Transform t)
        {
            if (t == target) return;
            target = t;
            count = 0; head = -1;
            attack = null; parry = null; stun = null; health = null; skill = null; state = null; traversal = null;
        }

        /// <summary>지금 모습을 한 장 찍는다(틱마다 한 번).</summary>
        public void Capture(float now)
        {
            if (target == null || !target.gameObject.activeInHierarchy) { count = 0; head = -1; return; }
            var go = target.gameObject;
            // 스킬·막기 등은 캐릭터를 고를 때 나중에 붙으므로 없으면 다시 찾는다.
            if (attack == null) go.TryGetComponent(out attack);
            if (parry == null) go.TryGetComponent(out parry);
            if (stun == null) go.TryGetComponent(out stun);
            if (health == null) go.TryGetComponent(out health);
            if (skill == null) go.TryGetComponent(out skill);
            if (state == null) go.TryGetComponent(out state);
            if (traversal == null) go.TryGetComponent(out traversal);

            var s = new AiOpponentSnapshot
            {
                Valid = true,
                Time = now,
                Position = target.position,
                Forward = Flat(target.forward),
                Attack = attack != null ? attack.AttackState : CharacterAttackState.None,
                Phase = attack != null ? attack.AttackPhase : AttackPhase.None,
                RunJumpAttack = attack != null && attack.IsRunJumpAttack,
                Parrying = parry != null && parry.IsParrying,
                Stunned = stun != null && stun.IsStunned,
                Dead = health != null && health.IsDead,
                Grounded = state == null || state.IsGrounded,
                IsHunter = traversal != null && traversal.HunterStyle,
                Skill = skill != null ? skill.State : SkillState.Ready,
                SkillId = skill != null ? skill.SkillId : string.Empty,
                SkillLocksActions = skill != null && skill.LocksActions,
            };
            if (count > 0)
            {
                var prev = ring[head];
                float dt = now - prev.Time;
                s.Velocity = dt > 1e-4f ? Flat(s.Position - prev.Position, normalize: false) / dt : prev.Velocity;
            }
            head = (head + 1) % Capacity;
            ring[head] = s;
            if (count < Capacity) count++;
        }

        /// <summary>delay초 전의 모습(반응 지연). 기록이 모자라면 가장 오래된 것.</summary>
        public AiOpponentSnapshot Seen(float now, float delay)
        {
            if (count == 0) return default;
            float t = now - delay;
            int i = head;
            for (int n = 0; n < count; n++)
            {
                if (ring[i].Time <= t) return ring[i];
                if (n < count - 1) i = (i - 1 + Capacity) % Capacity;
            }
            return ring[i];
        }

        private static Vector3 Flat(Vector3 v, bool normalize = true)
        {
            v.y = 0f;
            return normalize ? (v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward) : v;
        }
    }
}

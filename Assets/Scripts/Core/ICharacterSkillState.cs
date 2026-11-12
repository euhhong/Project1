using System;

namespace Project1.Core
{
    public enum SkillState
    {
        /// <summary>쓸 수 있음(또는 스킬 없음).</summary>
        Ready,
        /// <summary>시전 중(예: 주사기를 꽂는 중).</summary>
        Casting,
        /// <summary>재사용 대기.</summary>
        Cooldown,
        /// <summary>스킬이 도중에 막혀 끝난 뒤 못 움직이는 중(예: 전위 돌진이 벽에 부딪혀 넘어짐). 끝나면 Cooldown.</summary>
        Recovering,
    }

    /// <summary>
    /// 고유 스킬 상태(읽기 전용). 스킬 모션·HUD(FE)가 읽는다. 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface ICharacterSkillState
    {
        /// <summary>스킬 ID(예: Doctor_SelfHeal). 스킬이 없으면 빈 문자열.</summary>
        string SkillId { get; }

        /// <summary>화면에 보일 이름(예: 자가치료).</summary>
        string SkillName { get; }

        SkillState State { get; }

        /// <summary>시전 진행도 0~1. Casting이 아니면 0.</summary>
        float CastProgress { get; }

        /// <summary>Recovering 진행도 0~1. Recovering이 아니면 0.</summary>
        float RecoveryProgress { get; }

        /// <summary>남은 재사용 대기 시간(초).</summary>
        float CooldownRemaining { get; }

        /// <summary>재사용 대기 전체 시간(초).</summary>
        float CooldownDuration { get; }

        /// <summary>시전 중이고, 그 스킬이 다른 행동(공격)을 막는지(예: 돌진).</summary>
        bool LocksActions { get; }

        /// <summary>지금 키를 누르면 쓸 수 있는지. 못 쓰면 이유(예: 체력이 가득 참)를 BlockedReason에 둔다.</summary>
        bool CanUse { get; }

        string BlockedReason { get; }

        event Action SkillStarted;

        /// <summary>시전이 끝날 때. true = 끝까지 해서 효과가 났다, false = 도중에 끊겼다.</summary>
        event Action<bool> SkillEnded;
    }
}

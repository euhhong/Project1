using System;

namespace Project1.Core
{
    /// <summary>
    /// 캐릭터의 읽기 전용 공격 상태. 공격 모션(FE)이 읽는다.
    /// 캐릭터 루트에서 GetComponent&lt;ICharacterAttackState&gt;()로 얻는다.
    /// </summary>
    public interface ICharacterAttackState
    {
        CharacterAttackState AttackState { get; }

        /// <summary>Light·Heavy일 때의 구간. 그 밖에는 None.</summary>
        AttackPhase AttackPhase { get; }

        /// <summary>현재 구간 진행도 0~1.</summary>
        float PhaseProgress { get; }

        /// <summary>강공격을 모은 시간(초). HeavyCharge가 아니면 0.</summary>
        float ChargeTime { get; }

        /// <summary>
        /// 지금 공격(모으기 포함)을 공중에서 시작했는지. 공격이 끝날 때까지 유지되고, 도중에 착지해도 바뀌지 않는다.
        /// 공중 공격 모션을 고를 때 쓴다.
        /// </summary>
        bool IsAirAttack { get; }

        /// <summary>
        /// 앞으로 달리다 뛰어서 한 공중 공격인지(플라잉킥 등, 발 판정). IsAirAttack이어도 이게 false면 땅 공격과 같은 구간·손 판정이다.
        /// 공격이 끝날 때까지 유지된다.
        /// </summary>
        bool IsRunJumpAttack { get; }

        /// <summary>약공격(Light) 또는 강공격(Heavy)이 시작될 때(준비 구간 시작).</summary>
        event Action<CharacterAttackState> AttackStarted;

        /// <summary>강공격 모으기가 시작될 때.</summary>
        event Action ChargeStarted;

        /// <summary>공격이 끝나 None으로 돌아갈 때(후딜레이 끝, 또는 모으기 취소).</summary>
        event Action AttackEnded;
    }
}

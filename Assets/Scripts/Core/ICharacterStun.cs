using System;

namespace Project1.Core
{
    /// <summary>
    /// 기절 상태(읽기 전용). 기절 중에는 이동·점프·공격·스킬을 못 한다. 모션·HUD(FE)가 읽는다.
    /// 캐릭터 루트에서 GetComponent&lt;ICharacterStun&gt;()로 얻는다.
    /// </summary>
    public interface ICharacterStun
    {
        bool IsStunned { get; }

        /// <summary>남은 기절 시간(초).</summary>
        float StunRemaining { get; }

        /// <summary>이번 기절이 등 뒤에서 맞아 생겼는지(예: 뒤에서 돌진·신호탄에 맞음 — 휘청이며 쓰러지는 모션). 방향 없이 기절시키면 거짓.</summary>
        bool StunnedFromBehind { get; }

        /// <summary>기절이 시작되거나 길어질 때(이번 기절 전체 시간, 초).</summary>
        event Action<float> Stunned;

        /// <summary>기절이 풀릴 때.</summary>
        event Action Recovered;
    }
}

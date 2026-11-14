using System;

namespace Project1.Core
{
    /// <summary>
    /// 캐릭터 스태미나(읽기 전용). HUD(FE)가 읽는다. 캐릭터 루트에서 GetComponent&lt;ICharacterStamina&gt;()로 얻는다.
    /// </summary>
    public interface ICharacterStamina
    {
        float Current { get; }
        float Max { get; }

        /// <summary>다 써서 지친 상태(일정량 회복할 때까지 달릴 수 없다).</summary>
        bool IsExhausted { get; }

        /// <summary>스태미나가 바뀔 때(현재, 최대).</summary>
        event Action<float, float> Changed;
    }
}

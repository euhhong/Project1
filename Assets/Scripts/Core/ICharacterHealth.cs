using System;

namespace Project1.Core
{
    /// <summary>
    /// 캐릭터 체력(읽기 전용). HUD(FE)가 읽는다. 캐릭터 루트에서 GetComponent&lt;ICharacterHealth&gt;()로 얻는다.
    /// </summary>
    public interface ICharacterHealth
    {
        float Current { get; }
        float Max { get; }

        /// <summary>체력이 바뀔 때(현재, 최대).</summary>
        event Action<float, float> Changed;
    }
}

namespace Project1.Core
{
    /// <summary>캐릭터의 읽기 전용 막기 상태(예: 생존자 주먹 막기). 막는 자세 모션(FE)·이동 제한이 읽는다.</summary>
    public interface ICharacterParryState
    {
        /// <summary>지금 막는 자세인지(공격 키를 꾹 누르는 동안).</summary>
        bool IsParrying { get; }

        /// <summary>지금까지 막아 낸 수(막을 때마다 1씩).</summary>
        int BlockCount { get; }
    }
}

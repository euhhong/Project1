namespace Project1.Core
{
    /// <summary>
    /// 거울 순간이동 중인 캐릭터의 읽기 전용 상태(BE → FE 모션·연출). 캐릭터 루트에서 GetComponent&lt;IMirrorTraveler&gt;()로 얻는다.
    /// 들어가기(Entering, 거울로 걸어 들어감) → 순간이동 → 나오기(Emerging, 반대 거울에서 걸어 나옴).
    /// </summary>
    public interface IMirrorTraveler
    {
        /// <summary>거울로 들어가는 중(제자리, 다른 행동 불가).</summary>
        bool Entering { get; }

        /// <summary>들어가기 진행도(0~1). 1에서 반대 거울로 옮겨진다.</summary>
        float EnterProgress { get; }

        /// <summary>반대 거울에서 나오는 중(제자리, 다른 행동 불가).</summary>
        bool Emerging { get; }

        /// <summary>나오기 진행도(0~1).</summary>
        float EmergeProgress { get; }

        /// <summary>거울을 지날 때마다 1씩(연출이 새 이동을 알아챈다).</summary>
        int TravelCount { get; }
    }
}

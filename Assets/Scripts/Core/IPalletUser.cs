namespace Project1.Core
{
    /// <summary>
    /// 판자를 내리는 캐릭터(생존자)의 읽기 전용 상태. 판자 내리는 모션(FE)이 읽는다.
    /// 캐릭터 루트에서 GetComponent&lt;IPalletUser&gt;()로 얻는다.
    /// </summary>
    public interface IPalletUser
    {
        /// <summary>판자를 내린 횟수(내릴 때마다 1씩 — FE가 새로 내린 것을 알아챈다).</summary>
        int DropCount { get; }

        /// <summary>지금 판자를 내리는 동작 중인지(제자리, 점프·공격 불가).</summary>
        bool Dropping { get; }

        /// <summary>내리는 동작 진행도(0~1).</summary>
        float DropProgress { get; }
    }
}

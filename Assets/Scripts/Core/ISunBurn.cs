namespace Project1.Core
{
    /// <summary>
    /// 지붕 높이에서 햇볕에 타는 중인지(BE → FE 연출). 캐릭터 루트에서 GetComponent&lt;ISunBurn&gt;()로 얻는다.
    /// </summary>
    public interface ISunBurn
    {
        /// <summary>지금 타는 중(지붕 높이에 서 있음).</summary>
        bool Burning { get; }

        /// <summary>타기 시작한 뒤 흐른 시간(초, 안 타면 0) — 연출이 점점 세진다.</summary>
        float BurnTime { get; }
    }
}

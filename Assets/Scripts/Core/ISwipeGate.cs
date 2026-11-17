namespace Project1.Core
{
    /// <summary>
    /// 좌우로 그어 후리기(맹인 지팡이)를 지금 그 방향으로 쓸 수 있는지. 재사용 대기 중이면 false — 그은 입력은 무시된다(CharacterAttack).
    /// </summary>
    public interface ISwipeGate
    {
        /// <param name="dir">-1 = 왼쪽, +1 = 오른쪽.</param>
        bool CanSwipe(int dir);
    }

    /// <summary>
    /// HUD 오른쪽 아래에 자리가 고정된 작은 재사용 대기 시계들(왼쪽부터 0번). 쓸 수 있을 때도 자리는 남는다.
    /// 예: 맹인 — 왼쪽 휘두르기 · 찌르기 · 오른쪽 휘두르기.
    /// </summary>
    public interface IFixedCooldownSlots
    {
        int SlotCount { get; }
        /// <param name="fill">0~1, 1 = 쓸 수 있음(대기 끝).</param>
        void GetSlot(int index, out string label, out float fill);
    }
}

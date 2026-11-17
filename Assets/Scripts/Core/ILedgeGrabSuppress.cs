namespace Project1.Core
{
    /// <summary>
    /// 지금 매달리기·턱 오르기를 하지 않을지(CharacterTraversal이 본다). 예: 용병 아대가 켜져 있고 벽 옆을 달리는 중 — 점프가 아대 도약이 되게.
    /// </summary>
    public interface ILedgeGrabSuppress
    {
        bool SuppressLedgeGrab { get; }
    }
}

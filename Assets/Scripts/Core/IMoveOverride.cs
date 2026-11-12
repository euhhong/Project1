namespace Project1.Core
{
    /// <summary>
    /// 입력 대신 이동을 정하는 쪽(예: 전위 럭비공 돌진 — 앞으로만, 점프 불가).
    /// 이동 컴포넌트는 같은 오브젝트에서 처음으로 true를 돌려준 것을 쓰고, 이때는 속도 제한을 무시한다.
    /// </summary>
    public interface IMoveOverride
    {
        /// <param name="localMove">시야 기준 방향(x = 우, y = 앞). 크기 1.</param>
        /// <param name="speed">속도(m/s).</param>
        /// <param name="allowJump">점프를 받을지.</param>
        bool TryGetMoveOverride(out UnityEngine.Vector2 localMove, out float speed, out bool allowJump);
    }
}

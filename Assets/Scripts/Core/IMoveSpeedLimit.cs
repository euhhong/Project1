namespace Project1.Core
{
    /// <summary>
    /// 이동 속도를 일시적으로 제한하는 쪽(예: 강공격 모으기 중 걷기 속도).
    /// 이동 컴포넌트는 같은 오브젝트의 모든 제한 중 가장 낮은 값을 쓴다.
    /// </summary>
    public interface IMoveSpeedLimit
    {
        /// <summary>제한이 있으면 true와 최대 속도(m/s)를 돌려준다.</summary>
        bool TryGetSpeedLimit(out float maxSpeed);
    }
}

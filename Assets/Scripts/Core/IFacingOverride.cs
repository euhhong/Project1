namespace Project1.Core
{
    /// <summary>
    /// 몸이 보는 방향(좌우)을 시야 대신 정하는 쪽(예: 붉은나비 나비에게 날아가는 동안 — 시작할 때 각도를 지킨다).
    /// 이동 컴포넌트는 같은 오브젝트에서 처음으로 true를 돌려준 값을 쓴다. 시야(LookYaw, 카메라)는 그대로 돈다.
    /// </summary>
    public interface IFacingOverride
    {
        /// <param name="yaw">몸이 볼 좌우 각도(도, 월드 Y축, 0 = +Z).</param>
        bool TryGetFacingYaw(out float yaw);
    }
}

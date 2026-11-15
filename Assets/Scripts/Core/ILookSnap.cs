namespace Project1.Core
{
    /// <summary>
    /// 시야(좌우 방향)를 게임 코드가 즉시 돌린다(예: 붉은나비 도약이 끝나면 가까운 상대 쪽으로). 입력(LocalPlayerInput)이 받고,
    /// 카메라(ThirdPersonCamera)는 SnapCount가 바뀌면 부드럽게 따라가지 않고 SnapYaw로 바로 돌린다. 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface ILookSnap
    {
        /// <summary>시야 좌우 각도(도, 월드)를 바로 이 값으로.</summary>
        void SnapLook(float yaw);

        /// <summary>seconds초 동안 시야가 target 쪽을 계속 따라 돈다(카메라도 바로). 그동안 직접 돌리기는 잘 안 된다.</summary>
        void TrackLook(UnityEngine.Transform target, float seconds);

        /// <summary>SnapLook을 부를 때마다(따라가는 동안엔 매 프레임) 1씩 늘어난다(카메라가 바뀐 것을 알아챈다).</summary>
        int SnapCount { get; }

        /// <summary>마지막 SnapLook 각도(도).</summary>
        float SnapYaw { get; }
    }
}

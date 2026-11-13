namespace Project1.Core
{
    /// <summary>
    /// 지금 카메라 상하(시야 Pitch) 감도를 바꿀지(예: 안토니오가 음표 자리를 고르는 동안 상하로 거의 안 움직이게).
    /// 입력(LocalPlayerInput)이 같은 캐릭터 루트에 붙은 것을 모두 곱한다. 1 = 그대로.
    /// </summary>
    public interface ILookSensitivity
    {
        float PitchScale { get; }
    }
}

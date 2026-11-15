namespace Project1.Core
{
    /// <summary>
    /// 이동 컴포넌트(CharacterMotor)가 걷기·달리기 속도에 곱하는 감속 배율(0~1). 여럿이면 모두 곱한다.
    /// 속도 제한(IMoveSpeedLimit)과 달리 달리기를 막지 않고 지금 속도를 조금씩 줄인다.
    /// 예: 도둑 손전등을 비추는 동안 0.85배. 캐릭터 루트에 붙인다.
    /// </summary>
    public interface IMoveSpeedScale
    {
        bool TryGetSpeedScale(out float multiplier);
    }
}

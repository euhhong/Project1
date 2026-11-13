namespace Project1.Core
{
    /// <summary>
    /// 이동 컴포넌트(CharacterMotor)가 걷기·달리기 속도에 곱하는 배율(1보다 크면 빨라짐). 여럿이면 가장 큰 값을 쓴다.
    /// 속도 제한(IMoveSpeedLimit)을 적용한 뒤 곱한다. 스킬 등이 이동을 대신 정할 때(IMoveOverride)는 쓰지 않는다.
    /// 예: 리퍼가 안개 칼날이 남긴 안개 안에 있으면 1.3배. 캐릭터 루트에 붙인다.
    /// </summary>
    public interface IMoveSpeedBoost
    {
        bool TryGetSpeedBoost(out float multiplier);
    }
}

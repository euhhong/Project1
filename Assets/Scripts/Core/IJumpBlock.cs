namespace Project1.Core
{
    /// <summary>
    /// 지금 이 캐릭터의 점프(스페이스바)를 막을지. 이동(CharacterMotor)이 점프하기 전에 본다.
    /// 예: 생존자가 세워진 판자의 내리기 범위 안에 있으면 스페이스바 = 판자 내리기라 점프하지 않는다(PalletUser).
    /// </summary>
    public interface IJumpBlock
    {
        bool BlocksJump { get; }
    }
}

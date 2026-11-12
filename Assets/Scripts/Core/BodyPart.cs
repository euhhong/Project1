namespace Project1.Core
{
    /// <summary>
    /// 맞는 부위. Hurtbox마다 하나를 지정한다. 값(0~3)은 네트워크·자동 배치에서 쓰므로 바꾸지 않는다.
    /// </summary>
    public enum BodyPart : byte
    {
        Head = 0,
        Body = 1,
        Arms = 2,
        Legs = 3,
    }
}

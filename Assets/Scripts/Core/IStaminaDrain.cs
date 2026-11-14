namespace Project1.Core
{
    /// <summary>
    /// 달리기 말고 스태미나를 쓰는 동작(예: 사다리 타기 — CharacterTraversal). 캐릭터 루트에 붙은 것을 CharacterStamina가 모두 읽는다.
    /// 0보다 크면 그동안 회복하지 않고 초당 그만큼 줄어든다.
    /// </summary>
    public interface IStaminaDrain
    {
        float StaminaDrainPerSecond { get; }
    }
}

namespace Project1.Core
{
    /// <summary>
    /// 공중에서 누른 공격 키를 다른 기능이 대신 쓰는지(예: 루키노 — 높이 떨어지는 중 공격 = 도약 스킬의 내려찍기).
    /// true면 CharacterAttack은 공중 공격을 내지 않고, 누른 키를 뗄 때까지 무시한다.
    /// </summary>
    public interface IAirAttackOverride
    {
        bool OverridesAirAttack { get; }
    }
}

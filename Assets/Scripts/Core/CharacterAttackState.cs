namespace Project1.Core
{
    /// <summary>공격 상태.</summary>
    public enum CharacterAttackState : byte
    {
        None,
        Light,
        HeavyCharge,
        Heavy,
    }

    /// <summary>공격 하나 안의 구간. 판정(Active) 구간에만 Hitbox가 켜진다.</summary>
    public enum AttackPhase : byte
    {
        None,
        Windup,
        Active,
        Recovery,
    }
}

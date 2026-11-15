namespace Project1.Core
{
    /// <summary>
    /// 캐릭터가 주는·받는 피해 배율(1 = 그대로). 여럿이면 모두 곱한다. 캐릭터 루트에 붙인다.
    /// 주는 피해는 데미지 규칙(DamageRules.Damage)이, 받는 피해는 체력(CharacterHealth.Damage — 모든 피해)이 곱한다.
    /// 예: 카우보이 올가미 뛰어넘기 직후 1초(화면에 따로 표시하지 않음).
    /// </summary>
    public interface IDamageScale
    {
        float OutgoingDamageScale { get; }
        float IncomingDamageScale { get; }
    }
}

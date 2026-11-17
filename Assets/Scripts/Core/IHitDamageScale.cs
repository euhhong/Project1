namespace Project1.Core
{
    /// <summary>
    /// 때린 쪽이 히트 하나하나를 보고 피해를 더 곱할지(DamageRules가 때린 쪽에 붙은 것을 모두 곱한다). 1 = 그대로.
    /// 예: 리퍼가 안개 칼날을 날리는 평타(2칸)를 바짝 붙어 써 팔(무기) 판정으로 맞히면 크게 줄임(MistCharge) — 칼날 자체 피해는 그대로.
    /// </summary>
    public interface IHitDamageScale
    {
        float DamageScaleFor(in HitResult hit);
    }
}

namespace Project1.Core
{
    /// <summary>
    /// HUD 가운데 아래 스킬 칸 안에 그림으로 보여 줄 "쥔 개수"(예: 붉은나비 나비 — 나비 그림 3칸). BE는 값만 알려 주고 그림은 FE(PlayerHud).
    /// 가진 만큼 진하게, 다음 하나는 차는 만큼 아래에서 위로 채워지고, 나머지는 흐리게.
    /// </summary>
    public interface IHudChargeIcons
    {
        /// <summary>그림 종류(예: "butterfly").</summary>
        string IconKind { get; }
        int IconCount { get; }
        int IconMax { get; }
        /// <summary>다음 하나가 차는 진행도(0~1). 가득이면 0.</summary>
        float IconNextFill { get; }
    }
}

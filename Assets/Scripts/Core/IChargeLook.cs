namespace Project1.Core
{
    /// <summary>
    /// 공격 키를 꾹 누르는 동안(강공격 모으기 시간이 지난 뒤) 마우스로 시점을 돌릴 수 있게 하는 기능(예: 공장장 인형 겨누기 — 좌클릭 끌기 = 자리, 우클릭 끌기 = 시야 높이).
    /// LocalPlayerInput이 캐릭터 루트에 붙은 것을 찾는다. LookScale = 그동안 마우스 감도 배율.
    /// </summary>
    public interface IChargeLook
    {
        bool LookWhileCharging { get; }
        float ChargeLookScale { get; }
        /// <summary>차징을 시작하는 순간 시야를 이만큼(도) 위로 올리고 그 높이에 고정한다(좌클릭 끌기는 LookDelta로만 — 예: 공장장 인형 자리 조절, 우클릭 끌기 = 시야 높이, 위쪽 한계 열림). 0 = 올리지 않음.</summary>
        float ChargeViewRaise { get; }
    }
}

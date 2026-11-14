namespace Project1.Core
{
    /// <summary>
    /// 공격 키를 꾹 누르는 동안(강공격 모으기 시간이 지난 뒤) 마우스로 시점을 돌릴 수 있게 하는 기능(예: 공장장 인형 겨누기 — 우클릭 없이 좌클릭만으로 포물선을 움직임).
    /// LocalPlayerInput이 캐릭터 루트에 붙은 것을 찾는다. LookScale = 그동안 마우스 감도 배율.
    /// </summary>
    public interface IChargeLook
    {
        bool LookWhileCharging { get; }
        float ChargeLookScale { get; }
        /// <summary>그동안 시야 상하 각도를 이 값(도, - = 위)으로 고정한다(마우스 위아래는 시점을 움직이지 않고 LookDelta로만 — 예: 공장장 인형 거리 끌기). NaN = 고정하지 않음. 떼면 원래 각도로.</summary>
        float ChargeViewPitch { get; }
    }
}

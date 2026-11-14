namespace Project1.Core
{
    /// <summary>
    /// 꾹 눌러 자리를 고르는(차징) 기능이 지금 고르는 중이면, 어떻게 취소하는지 알려 주는 짧은 글(HUD 스킬 칸 두 번째 줄에 뜬다).
    /// 고르는 중이 아니면 null. 예: 붉은나비 나비 놓기, 안토니오 음표, 하스터 촉수, 공장장 인형 던지기.
    /// </summary>
    public interface IChargeCancelHint
    {
        string ChargeCancelHint { get; }
    }
}

namespace Project1.Core
{
    /// <summary>
    /// HUD 오른쪽 아래 재사용 대기 게이지에 보여 줄 기술 하나(BE가 값만 알려 준다 — 그리기는 FE PlayerHud).
    /// 예: 고유 스킬(E) 재사용 대기, 안토니오 다음 음표, 공장장 자리 바꾸기, 붉은나비 나비 차기, 리퍼 안개 차기.
    /// 캐릭터 루트에 붙은 것을 모두 본다.
    /// </summary>
    public interface ICooldownGauge
    {
        /// <param name="label">게이지 이름(짧게).</param>
        /// <param name="fill">0~1. 차오르는 게이지면 다 차면 쓸 수 있음.</param>
        /// <param name="draining">true면 줄어드는 게이지(예: 그랩 다시 쏘기 남은 시간 — 다 줄면 끝).</param>
        /// <returns>지금 보여 줄 게이지가 있으면 true(재사용 대기 중 등). 쓸 수 있으면 false.</returns>
        bool TryGetCooldown(out string label, out float fill, out bool draining);
    }
}

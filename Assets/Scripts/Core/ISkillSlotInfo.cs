namespace Project1.Core
{
    /// <summary>
    /// 보통 고유 스킬(CharacterSkill)이 없는데 E를 직접 받는 기능(하스터 촉수·안토니오 음표)이 HUD 가운데 아래 스킬 칸에 보여 줄 값.
    /// 글은 단어·구절만(필요할 때만 — 재사용 대기 진행은 오른쪽 아래 게이지가 보여 주므로 쓰지 않는다).
    /// </summary>
    public interface ISkillSlotInfo
    {
        /// <summary>칸 이름(예: 촉수, 음표).</summary>
        string SlotName { get; }

        /// <summary>둘째 줄 글(필요한 것만, 없으면 null).</summary>
        string SlotStatus { get; }

        /// <summary>지금 E를 쓸 수 있는지(이름을 금색으로).</summary>
        bool SlotReady { get; }
    }
}

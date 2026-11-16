namespace Project1.Core
{
    /// <summary>
    /// 보통 고유 스킬(CharacterSkill)이 없는데 E를 직접 받는 기능(노란옷의 왕 촉수·안토니오 음표)이 HUD 가운데 아래 스킬 칸에 보여 줄 값.
    /// 칸 규칙(모든 캐릭터 같음): 쓸 수 있으면 "E 이름"만, 못 쓰면 칸만 남고 글 없음, 시간이 가는 단계(예: 둘째 음표까지 5초)는 칸 안 막대.
    /// </summary>
    public interface ISkillSlotInfo
    {
        /// <summary>칸 이름(예: 내리기, 음표).</summary>
        string SlotName { get; }

        /// <summary>지금 E를 쓸 수 있는지 — 쓸 수 있을 때만 이름이 뜬다.</summary>
        bool SlotReady { get; }

        /// <summary>칸 안 막대(0~1, 차오름). 시간이 가는 단계가 없으면 음수.</summary>
        float SlotFill { get; }
    }
}

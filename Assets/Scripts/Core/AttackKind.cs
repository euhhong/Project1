namespace Project1.Core
{
    /// <summary>히트를 낸 공격의 종류. 데미지·경직·넉백 규칙(DamageRules)이 이걸로 표를 고른다. 0 = 스킬·기타(평타가 아님).</summary>
    public enum AttackKind
    {
        /// <summary>스킬·투사체·장판 등 평타가 아닌 것.</summary>
        Skill = 0,
        /// <summary>약공격(짧게 누름).</summary>
        Light,
        /// <summary>강공격(꾹 눌러 모았다 뗌).</summary>
        Heavy,
        /// <summary>달리기 점프 공격(앞으로 달리다 뛴 공중 공격).</summary>
        RunJump,
        /// <summary>달리기 점프 공격 뒤 한 번 더 — 두 번째 날아 차기.</summary>
        FollowUp,
    }
}

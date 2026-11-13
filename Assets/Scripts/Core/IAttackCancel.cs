namespace Project1.Core
{
    /// <summary>
    /// 공격의 후딜레이를 끊고 고유 스킬(E)로 이어 갈 수 있는지(예: 맹인 휘두르기 → E 찌르기 연속 동작). CharacterSkill이 E 선입력을 받아 둘 때 본다.
    /// </summary>
    public interface IAttackCancel
    {
        /// <summary>지금 공격 중이고, 이 공격을 끊고 스킬로 이어 갈 수 있는지(선입력을 받아 둘지).</summary>
        bool CanCancelForSkill { get; }

        /// <summary>후딜레이 구간이면 공격을 끊는다(끊었거나 이미 공격 중이 아니면 true).</summary>
        bool TryCancelForSkill();
    }
}

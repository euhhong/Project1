namespace Project1.Core
{
    /// <summary>
    /// 고유 스킬(E)을 막는 상태(읽기 전용, 예: 도둑 손전등에 지져짐). CharacterSkill이 시작하기 전에 본다.
    /// 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface ISkillLock
    {
        bool SkillLocked { get; }

        /// <summary>막힌 이유(HUD에 보임).</summary>
        string LockReason { get; }
    }

    /// <summary>
    /// 도둑 손전등 빛에 지져진 정도(읽기 전용). FE가 머리 위 게이지를 그린다.
    /// - Exposure: 빛을 받으면 차고, 받지 않으면 계속 떨어진다. LockThreshold 이상이면 E가 잠긴다(ISkillLock).
    /// - FrontExposure: 빛을 정면(얼굴 쪽)으로 받을 때만 차는 따로 된 게이지. 다 차면 기절하고 0으로.
    /// </summary>
    public interface ILightExposure : ISkillLock
    {
        float Exposure { get; }

        float LockThreshold { get; }

        float FrontExposure { get; }

        /// <summary>이번 틱(또는 바로 전 틱)에 빛을 받는 중인지.</summary>
        bool Lit { get; }

        /// <summary>정면으로 받는 중인지.</summary>
        bool LitFront { get; }
    }
}

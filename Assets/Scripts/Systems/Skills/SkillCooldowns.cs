namespace Project1.Systems
{
    /// <summary>
    /// 재사용 대기(쿨타임) 전체 스위치(BE). 연습 모드에서는 꺼서(Disabled = true) 모든 스킬을 쿨타임 없이 바로 다시 쓸 수 있게 한다.
    /// 쿨타임을 거는 곳(CharacterSkill·인형 자리 바꾸기·나비 놓기·음표 다시 잇기·용병 아대 도약 등)이 Scale로 곱한다. 게임 시작(AI 봇 전투) 모드는 켬.
    /// </summary>
    public static class SkillCooldowns
    {
        /// <summary>true = 쿨타임 없음(연습 모드).</summary>
        public static bool Disabled { get; set; }

        /// <summary>쿨타임 시간에 곱한다: 꺼졌으면 0.</summary>
        public static float Scale(float seconds) => Disabled ? 0f : seconds;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Disabled = false;
    }
}

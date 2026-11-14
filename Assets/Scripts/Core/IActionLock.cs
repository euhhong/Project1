namespace Project1.Core
{
    /// <summary>
    /// 보이는 표시 없이 잠깐 공격·스킬을 막는다(기절과 달리 머리 위 표시·HUD 글자 없음). 예: 공장장 인형과 자리를 바꾼 직후 짧은 경직(DollThrow).
    /// CharacterAttack·CharacterSkill이 캐릭터 루트에 붙은 것을 모두 본다. 이동은 따로 IMoveSpeedLimit로 막는다.
    /// </summary>
    public interface IActionLock
    {
        bool ActionsLocked { get; }
    }
}

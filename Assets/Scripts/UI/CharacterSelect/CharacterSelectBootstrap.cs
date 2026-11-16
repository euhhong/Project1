using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.UI
{
    /// <summary>
    /// 게임 씬(SampleScene 등 — 로컬 플레이어 LocalPlayerInput이 있는 씬)이 열리면, 캐릭터 선택 씬에서 고른 캐릭터(CharacterPick)를 입힌다:
    /// 모델 교체(CharacterLoadout.Apply), 연습 모드 = 쿨타임 없음 + ESC 나가기(PracticeExit), 게임 시작 = AI 봇과 전투(BattleMatch).
    /// 고른 게 없으면(에디터에서 SampleScene을 바로 Play) 캐릭터 선택 씬으로 넘어간다. 씬에 아무것도 붙일 필요가 없다.
    /// 끄려면 Assets/UI/Resources/CharacterRoster의 Show On Start를 끈다(씬의 플레이어 그대로 시작).
    /// </summary>
    public static class CharacterSelectBootstrap
    {
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => OnSceneLoaded(); }
            OnSceneLoaded();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook() => hooked = false;

        private static void OnSceneLoaded()
        {
            var player = Object.FindAnyObjectByType<Project1.Systems.LocalPlayerInput>();
            if (player == null) return; // 게임 씬이 아님(예: 캐릭터 선택 씬)
            var roster = CharacterRoster.Load();
            if (roster == null || !roster.showOnStart || roster.entries.Count == 0) return;

            var entry = CharacterPick.Has ? roster.entries.Find(e => e.id == CharacterPick.EntryId && e.model != null) : null;
            if (entry == null)
            {
                // 고른 캐릭터 없이 게임 씬을 바로 열었다 → 캐릭터 선택 씬부터
                if (Application.CanStreamedLevelBeLoaded(CharacterSelectScreen.SceneName)) SceneManager.LoadScene(CharacterSelectScreen.SceneName);
                else Debug.LogWarning($"[UI] 캐릭터 선택 씬({CharacterSelectScreen.SceneName})이 Build Settings에 없어 씬의 플레이어 그대로 시작합니다.");
                return;
            }

            bool battle = CharacterPick.Battle;
            CharacterLoadout.Apply(player.gameObject, entry);
            Project1.Systems.SkillCooldowns.Disabled = !battle; // 연습 모드 = 쿨타임 없이 스킬, 게임 시작 = 쿨타임 그대로
            if (battle) BattleMatch.Begin(roster, player.gameObject, entry);
            else PracticeExit.Begin(); // 연습 모드: ESC·오른쪽 위 버튼으로 선택 화면으로 돌아가기
            Debug.Log($"[UI] {entry.displayName}({(entry.role == CharacterRole.Hunter ? "감시자" : "생존자")})로 {(battle ? "게임(AI 봇과 전투)" : "연습 모드")}를 시작합니다.");
        }
    }
}

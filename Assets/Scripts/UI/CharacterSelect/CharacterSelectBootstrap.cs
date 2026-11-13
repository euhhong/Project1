using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 씬을 시작하면 캐릭터 선택 화면을 자동으로 띄운다. 씬에 아무것도 붙일 필요가 없다.
    /// 조건: Resources에 CharacterRoster가 있고 showOnStart가 켜져 있으며, 씬에 로컬 플레이어(LocalPlayerInput)가 있을 때.
    /// 끄려면 Assets/UI/Resources/CharacterRoster의 Show On Start를 끈다.
    /// </summary>
    public static class CharacterSelectBootstrap
    {
        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // 씬을 다시 불러도(예: 게임 시작 모드에서 R로 다시 하기) 다시 띄운다.
            if (!hooked) { hooked = true; UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Show(); }
            Show();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook() => hooked = false;

        private static void Show()
        {
            if (Object.FindAnyObjectByType<CharacterSelectScreen>() != null) return;
            var roster = CharacterRoster.Load();
            if (roster == null || !roster.showOnStart || roster.entries.Count == 0) return;
            var player = Object.FindAnyObjectByType<Project1.Systems.LocalPlayerInput>();
            if (player == null) return;
            new GameObject("CharacterSelect").AddComponent<CharacterSelectScreen>().Init(roster, player.gameObject);
        }
    }
}

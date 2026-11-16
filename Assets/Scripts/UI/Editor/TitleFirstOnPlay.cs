using UnityEditor;
using UnityEditor.SceneManagement;

namespace Project1.UI.EditorTools
{
    /// <summary>
    /// 에디터에서 Play를 누르면 어느 씬을 열어 두었든 타이틀 씬(Assets/Scenes/Title.unity)부터 시작한다
    /// (빌드한 게임과 같은 순서: 타이틀 → 게임 시작 → 캐릭터 선택 → 게임). Play를 멈추면 열어 두었던 씬으로 돌아온다.
    /// 끄기/켜기: 메뉴 Tools/UI/Play는 타이틀부터.
    /// </summary>
    [InitializeOnLoad]
    public static class TitleFirstOnPlay
    {
        private const string TitlePath = "Assets/Scenes/Title.unity";
        private const string PrefKey = "Project1.TitleFirstOnPlay";
        private const string MenuPath = "Tools/UI/Play는 타이틀부터";

        static TitleFirstOnPlay() => EditorApplication.delayCall += Apply;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() { Enabled = !Enabled; Apply(); }

        [MenuItem(MenuPath, true)]
        private static bool ToggleCheck() { Menu.SetChecked(MenuPath, Enabled); return true; }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath) : null;
        }
    }
}

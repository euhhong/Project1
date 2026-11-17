using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 게임 중 메뉴(연습 모드·게임 시작 모두): ESC 또는 화면 오른쪽 위 "ESC 메뉴" 버튼 → 가운데 창(게임은 멈춤).
    /// 계속하기(ESC로도 닫힘) / 캐릭터 선택 / 메인 메뉴로(타이틀). 나갈 때는 게임 속도를 되돌리고 어두워졌다가 그 씬으로.
    /// 게임 씬이 시작할 때(CharacterSelectBootstrap) 만든다. ESC는 다른 곳에서 쓰지 않는다(커서 잠금은 우클릭·차징 때만).
    /// </summary>
    public class InGameMenu : MonoBehaviour
    {
        private RuntimeUiPanel panel;
        private TitleDialog dialog;
        private bool leaving;

        public static void Begin()
        {
            if (FindAnyObjectByType<InGameMenu>() != null) return;
            new GameObject("InGameMenu").AddComponent<InGameMenu>();
        }

        private void Start()
        {
            panel = RuntimeUiPanel.Create("InGameMenuUI", 50);
            panel.Root.pickingMode = PickingMode.Ignore;

            // 오른쪽 위 버튼
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 44f; holder.style.top = 30f;
            panel.Root.Add(holder);
            var buttonFont = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
            var button = new SlantedButton(holder, "ESC  메뉴", buttonFont, 40, 250f, 62f, primary: false, Toggle);
            button.SetAccent(CharacterSelectBackdrop.Maroon);

            // 가운데 창
            var font = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            dialog = new TitleDialog(panel.Root, font, 520f);
            var title = TitleDialog.Text(dialog.Box, "일시 정지", font, 44, TitleDialog.Ink);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 18f;
            AddButton("계속하기", Resume, primary: true, font);
            AddButton("캐릭터 선택", () => Leave(CharacterSelectScreen.SceneName), primary: false, font);
            AddButton("메인 메뉴로", () => Leave(TitleScreen.SceneName), primary: false, font);
            dialog.CancelAction = Resume;
            dialog.DefaultAction = Resume;
        }

        private void AddButton(string text, System.Action onClick, bool primary, Font font)
        {
            var b = TitleDialog.MakeButton(dialog.Box, text, font, onClick, primary);
            b.style.width = StyleKeyword.Auto;
            b.style.alignSelf = Align.Stretch;
            b.style.marginLeft = b.style.marginRight = 0f;
            b.style.marginTop = 14f;
        }

        private void Update()
        {
            if (leaving) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) Toggle();
            else if (dialog != null && dialog.IsOpen && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) dialog.DefaultAction?.Invoke();
        }

        private void Toggle()
        {
            if (leaving || dialog == null) return;
            if (dialog.IsOpen) Resume(); else Pause();
        }

        private void Pause()
        {
            dialog.Open();
            Time.timeScale = 0f; // 창이 열려 있는 동안 게임 멈춤(봇·스킬·쿨타임 모두)
        }

        private void Resume()
        {
            dialog.Close();
            Time.timeScale = 1f;
        }

        private void Leave(string scene)
        {
            if (leaving) return;
            leaving = true;
            Time.timeScale = 1f;
            ScreenFade.Out(() => SceneManager.LoadScene(scene), scene); // 어두워졌다가 그 씬이 열리면 밝아짐
        }

        private void OnDestroy()
        {
            if (dialog != null && dialog.IsOpen) Time.timeScale = 1f; // 창이 열린 채 씬이 바뀌어도 멈춘 채로 남지 않게
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}

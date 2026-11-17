using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 게임 중 메뉴(연습 모드·게임 시작 모두): ESC 또는 화면 오른쪽 위 "ESC 메뉴" 버튼 → 가운데 창(게임은 멈춤).
    /// 제목 PAUSE, 버튼 둘: 재개(ESC로도 닫힘) / 나가기(캐릭터 선택 화면). 나갈 때는 게임 속도를 되돌리고 어두워졌다가 그 씬으로.
    /// 게임 씬이 시작할 때(CharacterSelectBootstrap) 만든다. ESC는 다른 곳에서 쓰지 않는다(커서 잠금은 우클릭·차징 때만).
    /// </summary>
    public class InGameMenu : MonoBehaviour
    {
        private RuntimeUiPanel panel;
        private TitleDialog dialog;
        private bool leaving;

        private bool practice;

        /// <summary>practice = 연습 모드: ESC(오른쪽 위 버튼도)는 창 없이 바로 캐릭터 선택 화면으로(쓰던 캐릭터가 골라진 채).</summary>
        public static void Begin(bool practice = false)
        {
            if (FindAnyObjectByType<InGameMenu>() != null) return;
            new GameObject("InGameMenu").AddComponent<InGameMenu>().practice = practice;
        }

        private void Start()
        {
            panel = RuntimeUiPanel.Create("InGameMenuUI", 50);
            panel.Root.pickingMode = PickingMode.Ignore;

            // 오른쪽 위 버튼
            holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 44f; holder.style.top = 30f;
            panel.Root.Add(holder);
            var buttonFont = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
            var button = new SlantedButton(holder, practice ? "ESC  나가기" : "ESC  메뉴", buttonFont, 40, 250f, 62f, primary: false, Toggle);
            button.SetAccent(CharacterSelectBackdrop.Maroon);

            // 가운데 창
            var font = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            dialog = new TitleDialog(panel.Root, font, 460f, slanted: true); // 오른쪽 위 'ESC 메뉴' 버튼과 같은 느낌의 기운 창
            var title = TitleDialog.SlantedText(dialog.Box, "PAUSE", 76);
            title.style.marginBottom = 14f;
            AddButton("재개", Resume, primary: true);
            AddButton("나가기", () => Leave(CharacterSelectScreen.SceneName), primary: false); // 캐릭터 선택 화면으로
            dialog.CancelAction = Resume;
            dialog.DefaultAction = Resume;
        }

        private void AddButton(string text, System.Action onClick, bool primary) => _ = AddButton(dialog, text, onClick, primary);

        private static SlantedButton AddButton(TitleDialog d, string text, System.Action onClick, bool primary)
        {
            var b = TitleDialog.MakeSlantedButton(d.Box, text, onClick, primary, width: 280f, height: 70f);
            b.Root.style.marginTop = 22f;
            return b;
        }

        // ───────── 승패 결과 창 ─────────
        private VisualElement holder;   // 오른쪽 위 'ESC 메뉴' 버튼 자리
        private TitleDialog resultDialog;

        /// <summary>
        /// 승패가 났을 때(BattleMatch): 오른쪽 위 버튼을 숨기고, PAUSE 창과 같은 기운 창에 결과(승리·패배)와 'R 다시하기'·'ESC 나가기' 버튼을 띄운다.
        /// R·ESC 키는 BattleMatch가 받는다(같은 동작).
        /// </summary>
        public static void ShowResult(string text, System.Action retry, System.Action exit)
        {
            var m = FindAnyObjectByType<InGameMenu>();
            if (m != null) m.OpenResult(text, retry, exit);
        }

        private void OpenResult(string text, System.Action retry, System.Action exit)
        {
            if (panel == null || resultDialog != null) return;
            if (dialog != null && dialog.IsOpen) Resume();
            if (holder != null) holder.style.display = DisplayStyle.None;
            resultDialog = new TitleDialog(panel.Root, Resources.Load<Font>("Fonts/GowunDodum-Regular"), 460f, slanted: true);
            var title = TitleDialog.SlantedText(resultDialog.Box, text, 110);
            title.style.marginBottom = 6f;
            AddButton(resultDialog, "R  다시하기", retry, primary: true);
            AddButton(resultDialog, "ESC  나가기", exit, primary: false).HoverEffect = false; // 결과 창 나가기: 마우스를 올려도 들뜨지 않고 소리 없음
            resultDialog.CancelAction = null;   // ESC는 BattleMatch가(나가기)
            resultDialog.DefaultAction = retry; // Enter = 다시하기
            resultDialog.Open();
        }

        private void Update()
        {
            if (leaving) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            if (resultDialog != null) { if (enter) resultDialog.DefaultAction?.Invoke(); return; } // 결과 창: Enter = 다시하기(R·ESC는 BattleMatch)
            if (kb.escapeKey.wasPressedThisFrame) Toggle();
            else if (dialog != null && dialog.IsOpen && enter) dialog.DefaultAction?.Invoke();
        }

        private void Toggle()
        {
            if (leaving || dialog == null) return;
            if (BattleMatch.ResultShown) return; // 승패가 뜬 뒤엔 PAUSE 창 없음(ESC = 나가기는 BattleMatch가)
            if (practice) { Leave(CharacterSelectScreen.SceneName); return; } // 연습 모드: 창 없이 바로 캐릭터 선택으로
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

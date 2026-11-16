using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 연습 모드에서 나가기: ESC 또는 화면 오른쪽 위 "ESC 나가기" 버튼 → 캐릭터 선택 씬(CharacterSelect)으로 돌아간다.
    /// 게임 씬이 연습 모드로 시작할 때(CharacterSelectBootstrap) 만든다. ESC는 다른 곳에서 쓰지 않는다(커서 잠금은 우클릭·차징 때만).
    /// </summary>
    public class PracticeExit : MonoBehaviour
    {
        private RuntimeUiPanel panel;
        private bool leaving;

        public static void Begin()
        {
            if (FindAnyObjectByType<PracticeExit>() != null) return;
            new GameObject("PracticeExit").AddComponent<PracticeExit>();
        }

        private void Start()
        {
            panel = RuntimeUiPanel.Create("PracticeExitUI", 50);
            panel.Root.pickingMode = PickingMode.Ignore;
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.style.right = 44f; holder.style.top = 30f;
            panel.Root.Add(holder);
            var font = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
            var button = new SlantedButton(holder, "ESC  나가기", font, 40, 250f, 62f, primary: false, Leave);
            button.SetAccent(CharacterSelectBackdrop.Maroon);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Leave();
        }

        private void Leave()
        {
            if (leaving) return;
            leaving = true;
            ScreenFade.Out(() => SceneManager.LoadScene(CharacterSelectScreen.SceneName), CharacterSelectScreen.SceneName); // 어두워졌다가 선택 화면이 열리면 밝아짐
        }

        private void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}

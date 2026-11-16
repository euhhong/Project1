using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 타이틀 화면(Assets/Scenes/Title.unity — 빌드 첫 씬). 씬을 열면 스스로 화면을 만든다(인스펙터 연결 없음).
    /// 배경은 Resources/Title/TitleArt(메뉴 버튼 4개가 그림에 그려져 있음)를 비율을 지키며 화면을 꽉 덮게 깔고,
    /// 그려진 버튼 자리마다 보이지 않는 클릭 칸 + 마우스를 올리면 은은한 진홍 빛을 띄운다.
    /// - 게임 시작 → 캐릭터 선택 씬, 옵션 → 환경설정(음량·Policy), 크레딧 → 크레딧 창, 게임 종료 → 종료 확인(예/아니오).
    /// - 키보드: ↑↓(W·S) 고르기, Enter·Space 누르기, ESC = 창 닫기(창이 없으면 종료 확인).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public const string SceneName = "Title";
        private const string ArtResource = "Title/TitleArt";
        private const float ArtWidth = 1672f, ArtHeight = 941f; // 그림 픽셀 크기(버튼 자리를 이 기준으로 적음)
        private const float FadeTime = 0.3f;

        /// <summary>그림 속 비스듬한 버튼 자리(그림 픽셀 꼭짓점, 위가 0: 왼위·오위·오아래·왼아래).</summary>
        private static readonly Vector2[][] ButtonShapes =
        {
            new Vector2[] { new(1214f, 420f), new(1559f, 409f), new(1527f, 492f), new(1182f, 506f) }, // 게임 시작
            new Vector2[] { new(1218f, 535f), new(1540f, 530f), new(1522f, 597f), new(1200f, 603f) }, // 옵션
            new Vector2[] { new(1219f, 630f), new(1536f, 627f), new(1520f, 692f), new(1203f, 697f) }, // 크레딧
            new Vector2[] { new(1218f, 726f), new(1538f, 723f), new(1520f, 788f), new(1199f, 793f) }, // 게임 종료
        };

        private RuntimeUiPanel panel;
        private VisualElement art, fade;
        private readonly TitleMenuButton[] buttons = new TitleMenuButton[4];
        private TitleOptionsPanel options;
        private TitleDialog credits, exitConfirm;
        private int selected;
        private float fadeStart = -1f;
        private bool ownsPanel;

        private void Start()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            var font = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            // 씬의 TitleUI(UIDocument — Title.uxml, 에디터에서도 배경이 보임)를 쓰고, 없으면 코드로 만든다
            var doc = FindAnyObjectByType<UIDocument>();
            ownsPanel = doc == null;
            panel = doc != null ? RuntimeUiPanel.Attach(doc) : RuntimeUiPanel.Create("TitleUI", 0);
            var root = panel.Root;
            root.style.backgroundColor = Color.black;

            art = root.Q("title-art");
            if (art == null)
            {
                art = new VisualElement();
                art.style.backgroundImage = Resources.Load<Texture2D>(ArtResource);
                root.Add(art);
            }
            // 실행 중에는 그림 비율 그대로 크기를 정해 버튼 자리가 그림과 맞게 한다(CoverScreen)
            art.style.position = Position.Absolute;
            art.style.right = StyleKeyword.Auto; art.style.bottom = StyleKeyword.Auto;
            art.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            root.RegisterCallback<GeometryChangedEvent>(_ => CoverScreen());

            for (int i = 0; i < ButtonShapes.Length; i++) MakeHitArea(i);

            options = new TitleOptionsPanel(root, font);
            credits = TitleCredits.Create(root, font);
            exitConfirm = TitleDialog.Confirm(root, font, "정말 종료하시겠습니까?", Quit);

            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute;
            fade.style.left = 0; fade.style.top = 0; fade.style.right = 0; fade.style.bottom = 0;
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 0f;
            root.Add(fade);

            Select(0);
            Project1.Presentation.BgmPlayer.Play("Title"); // 타이틀 배경음악
        }

        /// <summary>그림을 비율 그대로 화면을 꽉 덮게(넘치는 쪽은 가운데 기준으로 잘림).</summary>
        private void CoverScreen()
        {
            var size = panel.Root.layout.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float s = Mathf.Max(size.x / ArtWidth, size.y / ArtHeight);
            float w = ArtWidth * s, h = ArtHeight * s;
            art.style.width = w; art.style.height = h;
            art.style.left = (size.x - w) * 0.5f; art.style.top = (size.y - h) * 0.5f;
        }

        // ───────── 그림 버튼 ─────────

        private void MakeHitArea(int i)
        {
            var hit = new TitleMenuButton(ButtonShapes[i], ArtWidth, ArtHeight);
            art.Add(hit);
            buttons[i] = hit;
            hit.RegisterCallback<PointerEnterEvent>(_ => { if (!AnyDialogOpen) { Select(i); HoverSound(); } });
            hit.RegisterCallback<ClickEvent>(_ => { if (!AnyDialogOpen) Activate(i); });
        }

        private void Select(int i)
        {
            selected = (i + buttons.Length) % buttons.Length;
            for (int k = 0; k < buttons.Length; k++) buttons[k].Lit = k == selected;
        }

        /// <summary>버튼 위로 지나갈 때: 종이 넘기는 가볍고 날카로운 셕(ui_page, VARCO Sound).</summary>
        private static void HoverSound() => Project1.Presentation.SfxPlayer.PlayUi("ui_page", 1f, 0.08f);

        private void Activate(int i)
        {
            if (fadeStart >= 0f) return;
            Project1.Presentation.SfxPlayer.PlayUi("ui_confirm", 1f, 0.03f); // 고름 완료: 짧고 높은 슁-핑
            switch (i)
            {
                case 0: // 어두워진 뒤 캐릭터 선택으로(열리면 밝아짐)
                    fadeStart = float.MaxValue; // 넘어가는 동안 입력 막기
                    ScreenFade.Out(() => SceneManager.LoadSceneAsync(CharacterSelectScreen.SceneName), CharacterSelectScreen.SceneName, FadeTime);
                    break;
                case 1: options.Open(); break;
                case 2: credits.Open(); break;
                case 3: exitConfirm.Open(); break;
            }
        }

        private bool AnyDialogOpen => options.IsOpen || credits.IsOpen || exitConfirm.IsOpen;

        private TitleDialog OpenDialog => options.IsOpen ? options.Dialog : credits.IsOpen ? credits : exitConfirm.IsOpen ? exitConfirm : null;

        // ───────── 키보드·넘어가기 ─────────

        private void Update()
        {
            if (options == null || fade == null) return; // Play 중 다시 컴파일되면 화면이 비어 있다
            if (fadeStart >= 0f)
            {
                float t = (Time.unscaledTime - fadeStart) / FadeTime;
                fade.style.opacity = Mathf.Clamp01(t);
                if (t >= 1f) { fadeStart = float.MaxValue; SceneManager.LoadScene(CharacterSelectScreen.SceneName); }
                return;
            }

            var kb = Keyboard.current;
            if (kb == null || panel == null) return;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            var dialog = OpenDialog;
            if (dialog != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) dialog.CancelAction?.Invoke();
                else if (enter) dialog.DefaultAction?.Invoke();
                return;
            }
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { Select(selected - 1); HoverSound(); }
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { Select(selected + 1); HoverSound(); }
            if (enter) Activate(selected);
            if (kb.escapeKey.wasPressedThisFrame) exitConfirm.Open();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (panel != null && ownsPanel) Destroy(panel.gameObject);
        }
    }
}

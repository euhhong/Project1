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
    /// - Start Game → 캐릭터 선택 씬, Options → 환경설정(음량·Policy), Multi Battle → "아직 지원하지 않는 기능입니다.", Exit → 종료 확인(예/아니오).
    /// - 키보드: ↑↓(W·S) 고르기, Enter·Space 누르기, ESC = 창 닫기(창이 없으면 종료 확인).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public const string SceneName = "Title";
        private const string ArtResource = "Title/TitleArt";
        private const float ArtWidth = 1670f, ArtHeight = 941f; // 그림 픽셀 크기(버튼 자리를 이 기준으로 적음)
        private const float FadeTime = 0.3f;

        /// <summary>그림 속 버튼 자리(그림 픽셀: x, y, 너비, 높이).</summary>
        private static readonly Rect[] ButtonRects =
        {
            new(1382f, 592f, 286f, 60f), // Start Game
            new(1382f, 667f, 286f, 48f), // Options
            new(1382f, 733f, 286f, 49f), // Multi Battle
            new(1382f, 799f, 286f, 49f), // Exit
        };

        private RuntimeUiPanel panel;
        private VisualElement art, fade;
        private readonly VisualElement[] glows = new VisualElement[4];
        private TitleOptionsPanel options;
        private TitleDialog notSupported, exitConfirm;
        private int selected;
        private float fadeStart = -1f;

        private void Start()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            var font = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            panel = RuntimeUiPanel.Create("TitleUI", 0);
            var root = panel.Root;
            root.style.backgroundColor = Color.black;

            art = new VisualElement();
            art.style.position = Position.Absolute;
            art.style.backgroundImage = Resources.Load<Texture2D>(ArtResource);
            art.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            root.Add(art);
            root.RegisterCallback<GeometryChangedEvent>(_ => CoverScreen());

            for (int i = 0; i < ButtonRects.Length; i++) MakeHitArea(i);

            options = new TitleOptionsPanel(root, font);
            notSupported = TitleDialog.Message(root, font, "아직 지원하지 않는 기능입니다.");
            exitConfirm = TitleDialog.Confirm(root, font, "정말 종료하시겠습니까?", Quit);

            fade = new VisualElement { pickingMode = PickingMode.Ignore };
            fade.style.position = Position.Absolute;
            fade.style.left = 0; fade.style.top = 0; fade.style.right = 0; fade.style.bottom = 0;
            fade.style.backgroundColor = Color.black;
            fade.style.opacity = 0f;
            root.Add(fade);

            Select(0);
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
            var r = ButtonRects[i];
            var hit = new VisualElement();
            hit.style.position = Position.Absolute;
            hit.style.left = Length.Percent(r.x / ArtWidth * 100f);
            hit.style.top = Length.Percent(r.y / ArtHeight * 100f);
            hit.style.width = Length.Percent(r.width / ArtWidth * 100f);
            hit.style.height = Length.Percent(r.height / ArtHeight * 100f);
            art.Add(hit);

            // 은은한 빛: 바깥 번짐(넓고 옅게) + 안쪽 띠(왼쪽 진홍 막대·아래 밝은 줄)
            var glow = new VisualElement { pickingMode = PickingMode.Ignore };
            glow.style.position = Position.Absolute;
            glow.style.left = -6f; glow.style.right = -2f; glow.style.top = -5f; glow.style.bottom = -5f;
            glow.style.backgroundColor = new Color(0.85f, 0.18f, 0.16f, 0.10f);
            glow.style.borderTopLeftRadius = glow.style.borderBottomLeftRadius = 6f;
            glow.style.opacity = 0f;
            glow.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName> { new("opacity") };
            glow.style.transitionDuration = new System.Collections.Generic.List<TimeValue> { new(0.14f, TimeUnit.Second) };
            hit.Add(glow);

            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            band.style.position = Position.Absolute;
            band.style.left = 6f; band.style.right = 2f; band.style.top = 5f; band.style.bottom = 5f;
            band.style.backgroundColor = new Color(1f, 0.55f, 0.45f, 0.10f);
            band.style.borderLeftWidth = 4f; band.style.borderLeftColor = new Color(1f, 0.32f, 0.28f, 0.95f);
            band.style.borderBottomWidth = 1f; band.style.borderBottomColor = new Color(1f, 0.85f, 0.75f, 0.55f);
            glow.Add(band);
            glows[i] = glow;

            hit.RegisterCallback<PointerEnterEvent>(_ => { if (!AnyDialogOpen) Select(i); });
            hit.RegisterCallback<ClickEvent>(_ => { if (!AnyDialogOpen) Activate(i); });
        }

        private void Select(int i)
        {
            selected = (i + glows.Length) % glows.Length;
            for (int k = 0; k < glows.Length; k++) glows[k].style.opacity = k == selected ? 1f : 0f;
        }

        private void Activate(int i)
        {
            if (fadeStart >= 0f) return;
            switch (i)
            {
                case 0: fadeStart = Time.unscaledTime; break; // 어두워진 뒤 캐릭터 선택으로
                case 1: options.Open(); break;
                case 2: notSupported.Open(); break;
                case 3: exitConfirm.Open(); break;
            }
        }

        private bool AnyDialogOpen => options.IsOpen || notSupported.IsOpen || exitConfirm.IsOpen;

        private TitleDialog OpenDialog => options.IsOpen ? options.Dialog : notSupported.IsOpen ? notSupported : exitConfirm.IsOpen ? exitConfirm : null;

        // ───────── 키보드·넘어가기 ─────────

        private void Update()
        {
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
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Select(selected - 1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Select(selected + 1);
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
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}

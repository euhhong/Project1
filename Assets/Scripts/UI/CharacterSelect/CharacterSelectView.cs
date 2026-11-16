using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 UI(UI Toolkit, 코드로 만든다). 배치·글꼴·넘길 때 연출만 맡고, 무엇을 고를지는 CharacterSelectScreen이 정한다.
    /// 배치(기준 1920×1080): 맨 왼쪽 3D 모델(창·테두리 없이 배경에 바로 서서 일러스트 아래쪽 앞에 겹침), 가운데 크게 일러스트(누끼) + 바로 아래 캐릭터 이름,
    /// 오른쪽 위 끝에 게임 시작·연습 버튼, 오른쪽에 조작 칸(CharacterControlsPanel), 양 끝 ‹ › 화살표, 왼쪽 위 진영 탭.
    /// </summary>
    public class CharacterSelectView : IDisposable
    {
        public event Action<int> StepRequested;          // -1 = 이전, +1 = 다음
        public event Action<CharacterRole> RoleRequested;
        public event Action<bool> StartRequested;        // true = 게임 시작(AI 봇), false = 연습
        public event Action<float> ModelDragged;         // 모델을 가로로 끈 거리(기준 픽셀)

        /// <summary>3D 모델 그림 크기(기준 픽셀). 무대 RenderTexture도 이 비율로 만든다.</summary>
        public static readonly Vector2 ModelPanelSize = new(300f, 630f);
        private const float ModelLeft = 20f, ModelTop = 1080f - 630f; // 왼쪽 아래 구석에 작게 — 일러스트는 가리지 않는다
        private const float IllusLeft = 170f, IllusRight = 640f, IllusTop = 16f, IllusBottom = 175f; // 화면에 꽉 차게(가운데 왼쪽), 아래 175는 이름 자리
        private const float ColumnLeft = 1270f, ColumnWidth = 490f;
        private const float SwitchTime = 0.34f;

        private static readonly Color Ink = new(0.9f, 0.92f, 0.95f);
        private static readonly Color InkDim = new(0.6f, 0.66f, 0.74f);
        private static readonly Color Line = new(0.78f, 0.84f, 0.92f, 0.4f);
        private static readonly Color ButtonFill = new(0.07f, 0.09f, 0.13f, 0.85f);
        private static readonly Color SurvivorAccent = new(0.25f, 0.57f, 0.84f);
        private static readonly Color HunterAccent = new(0.74f, 0.17f, 0.21f);

        private readonly RuntimeUiPanel panel;
        private readonly CharacterSelectBackdrop backdrop;
        private readonly Font titleFont, bodyFont;
        private readonly Texture2D radial;
        private readonly Dictionary<string, Texture2D> illustrations = new();
        private readonly HashSet<VisualElement> hovered = new();

        private VisualElement illusLayer, illus, glow, nameBlock, nameRule;
        private CharacterSelectFlipTransition flip;
        private CharacterControlsPanel controls;
        private Label watermark, missing, nameLabel, subLabel;
        private Button hunterTab, survivorTab, startButton, practiceButton, prevArrow, nextArrow;
        private CharacterRole role;
        private Color accent = SurvivorAccent;
        private float animTime = 99f;
        private int animDir;

        public CharacterSelectView(Texture modelTexture)
        {
            titleFont = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
            bodyFont = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            radial = MakeRadial(128);
            panel = RuntimeUiPanel.Create("CharacterSelectUI", 100);
            var root = panel.Root;

            backdrop = new CharacterSelectBackdrop();
            root.Add(backdrop.Root);
            BuildIllustration(root);
            BuildName(root);
            BuildModel(root, modelTexture); // 일러스트 다음에 넣어 일러스트 왼쪽 아래 위에 살짝 겹쳐 선다
            BuildColumn(root);
            BuildTabs(root);
            BuildArrows(root);
            flip = new CharacterSelectFlipTransition(); // 넘길 때 파라락(맨 위에 잠깐 덮음)
            root.Add(flip.Root);
        }

        // ───────── 만들기 ─────────

        private void BuildIllustration(VisualElement root)
        {
            // 일러스트 뒤 진영 색 빛(웅장하게)
            glow = Abs(root, left: IllusLeft + 60f, top: IllusTop + 40f, right: IllusRight + 60f, bottom: IllusBottom - 120f);
            glow.style.backgroundImage = new StyleBackground(radial);
            glow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));

            illusLayer = Abs(root, left: IllusLeft, top: 0f, right: IllusRight, bottom: IllusBottom - 30f);
            illusLayer.style.overflow = Overflow.Hidden;

            watermark = Text(illusLayer, "", titleFont, 380, new Color(1f, 1f, 1f, 0.045f));
            watermark.style.position = Position.Absolute;
            watermark.style.left = -400f; watermark.style.right = -400f; watermark.style.top = 120f;
            watermark.style.unityTextAlign = TextAnchor.MiddleCenter;
            watermark.style.whiteSpace = WhiteSpace.NoWrap;

            illus = Abs(illusLayer, left: 0f, top: IllusTop, right: 0f, bottom: 30f);
            illus.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            illus.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            illus.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            illus.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);

            missing = Text(illusLayer, "일러스트 준비 중", bodyFont, 30, new Color(InkDim.r, InkDim.g, InkDim.b, 0.6f));
            missing.style.position = Position.Absolute;
            missing.style.left = 0; missing.style.right = 0; missing.style.top = Length.Percent(50);
            missing.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void BuildName(VisualElement root)
        {
            // 일러스트 바로 아래: 큰 이름 + 진영 색 선 + 진영
            nameBlock = Abs(root, left: IllusLeft, top: 1080f - IllusBottom - 30f, right: IllusRight, bottom: float.NaN);
            nameBlock.style.alignItems = Align.Center;

            nameLabel = Text(nameBlock, "", titleFont, 108, Ink);
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.textShadow = new TextShadow { offset = new Vector2(0f, 5f), blurRadius = 14f, color = new Color(0f, 0f, 0f, 0.75f) };

            nameRule = new VisualElement { pickingMode = PickingMode.Ignore };
            nameRule.style.width = 260f; nameRule.style.height = 3f;
            nameRule.style.marginTop = -8f;
            nameBlock.Add(nameRule);

            subLabel = Text(nameBlock, "", bodyFont, 24, InkDim);
            subLabel.style.marginTop = 10f;
            subLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void BuildModel(VisualElement root, Texture modelTexture)
        {
            var area = new VisualElement();
            area.style.position = Position.Absolute;
            area.style.left = ModelLeft; area.style.top = ModelTop;
            area.style.width = ModelPanelSize.x; area.style.height = ModelPanelSize.y;
            root.Add(area);

            // 발밑 그림자(모델 그림보다 먼저 = 뒤에)
            var shadow = Abs(area, left: ModelPanelSize.x * 0.5f - 110f, top: ModelPanelSize.y * CharacterSelectStage.FeetFromTop - 20f, right: float.NaN, bottom: float.NaN);
            shadow.style.width = 220f; shadow.style.height = 40f;
            shadow.style.backgroundImage = new StyleBackground(radial);
            shadow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            shadow.style.unityBackgroundImageTintColor = new Color(0f, 0f, 0f, 0.75f);

            var model = Abs(area, left: 0f, top: 0f, right: 0f, bottom: 0f);
            model.pickingMode = PickingMode.Position;
            if (modelTexture is RenderTexture rt) model.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));

            // 끌어서 조금 돌려 볼 수 있다
            float lastX = 0f;
            model.RegisterCallback<PointerDownEvent>(e => { model.CapturePointer(e.pointerId); lastX = e.position.x; });
            model.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!model.HasPointerCapture(e.pointerId)) return;
                ModelDragged?.Invoke(e.position.x - lastX);
                lastX = e.position.x;
            });
            model.RegisterCallback<PointerUpEvent>(e => model.ReleasePointer(e.pointerId));
        }

        private void BuildColumn(VisualElement root)
        {
            // 게임 시작·연습 버튼: 화면 오른쪽 위 끝
            var buttons = new VisualElement();
            buttons.style.position = Position.Absolute;
            buttons.style.right = 36f; buttons.style.top = 32f;
            buttons.style.width = 320f;
            root.Add(buttons);
            startButton = MakeButton(buttons, "게임 시작", 58, 88f, () => StartRequested?.Invoke(true));
            practiceButton = MakeButton(buttons, "연습", 46, 66f, () => StartRequested?.Invoke(false));
            practiceButton.style.marginTop = 12f;

            // 정보 칸: 오른쪽, 버튼 아래 — 조작만(CharacterControlsPanel)
            controls = new CharacterControlsPanel(root, titleFont, bodyFont, ColumnLeft, 266f, ColumnWidth, 110f);
        }

        private void BuildTabs(VisualElement root)
        {
            var tabs = new VisualElement();
            tabs.style.position = Position.Absolute;
            tabs.style.left = 110f; tabs.style.top = 36f;
            tabs.style.flexDirection = FlexDirection.Row;
            root.Add(tabs);
            hunterTab = MakeButton(tabs, "감시자", 34, 50f, () => RoleRequested?.Invoke(CharacterRole.Hunter));
            hunterTab.style.width = 150f;
            survivorTab = MakeButton(tabs, "생존자", 34, 50f, () => RoleRequested?.Invoke(CharacterRole.Survivor));
            survivorTab.style.width = 150f;
            survivorTab.style.marginLeft = 12f;
        }

        private void BuildArrows(VisualElement root)
        {
            prevArrow = MakeArrow(root, "<", () => StepRequested?.Invoke(-1));
            prevArrow.style.left = 14f;
            nextArrow = MakeArrow(root, ">", () => StepRequested?.Invoke(+1));
            nextArrow.style.right = 14f;
        }

        private Button MakeArrow(VisualElement parent, string text, Action onClick)
        {
            var b = MakeButton(parent, text, 150, 200f, onClick);
            b.style.position = Position.Absolute;
            b.style.width = 76f;
            b.style.top = Length.Percent(50);
            b.style.translate = new Translate(0f, Length.Percent(-50));
            Border(b, 0f, Color.clear);
            return b;
        }

        // ───────── 보여 주기 ─────────

        /// <summary>캐릭터 하나를 보여 준다. dir = 넘긴 방향(+1 다음, −1 이전, 0 연출 없이 페이드만).</summary>
        public void Show(CharacterRoster.Entry entry, CharacterRole shownRole, int index, int count, int dir)
        {
            SetRole(shownRole);
            nameLabel.text = entry.displayName;
            watermark.text = entry.displayName;
            string roleName = shownRole == CharacterRole.Hunter ? "감시자" : "생존자";
            subLabel.text = roleName;
            controls.Show(entry, accent);

            var tex = Illustration(entry.id);
            illus.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            missing.style.display = tex != null ? DisplayStyle.None : DisplayStyle.Flex;

            // 넘길 때: 띠가 아래에서 위로 파라락 지나가고, 화면을 덮는 순간부터 새 캐릭터가 들어온다
            animDir = dir;
            animTime = dir != 0 ? -CharacterSelectFlipTransition.CoverTime : 0f;
            if (dir != 0) flip.Play(accent, dir);
            Animate();
        }

        /// <summary>진영에 고를 캐릭터가 없을 때.</summary>
        public void ShowEmpty(CharacterRole shownRole)
        {
            SetRole(shownRole);
            nameLabel.text = "없음";
            watermark.text = "";
            subLabel.text = "고를 캐릭터가 없습니다";
            controls.ShowEmpty();
            illus.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            missing.style.display = DisplayStyle.None;
        }

        private void SetRole(CharacterRole shownRole)
        {
            role = shownRole;
            accent = role == CharacterRole.Hunter ? HunterAccent : SurvivorAccent;
            nameRule.style.backgroundColor = accent;
            subLabel.style.color = accent;
            glow.style.unityBackgroundImageTintColor = new Color(accent.r, accent.g, accent.b, 0.22f);
            backdrop.SetAccent(accent);
            Restyle();
        }

        private Texture2D Illustration(string id)
        {
            if (!illustrations.TryGetValue(id, out var tex))
                illustrations[id] = tex = Resources.Load<Texture2D>("Illustrations/" + id);
            return tex;
        }

        public void Tick(float dt)
        {
            backdrop.Tick(dt);
            flip.Tick(dt);
            if (animTime < SwitchTime + 0.05f)
            {
                animTime += dt;
                Animate();
            }
        }

        private void Animate()
        {
            float p = Mathf.Clamp01(animTime / SwitchTime);
            float e = 1f - Mathf.Pow(1f - p, 3f); // 빨리 들어와서 천천히 멈춘다
            illus.style.translate = new Translate(animDir * 300f * (1f - e), 0f);
            illus.style.scale = new Scale(Vector2.one * (1.05f - 0.05f * e));
            illus.style.opacity = e;
            watermark.style.translate = new Translate(animDir * 140f * (1f - e), 0f);
            nameBlock.style.translate = new Translate(0f, 28f * (1f - e));
            nameBlock.style.opacity = e;
        }

        // ───────── 버튼 색 ─────────

        private void Restyle()
        {
            StyleTab(hunterTab, role == CharacterRole.Hunter, HunterAccent);
            StyleTab(survivorTab, role == CharacterRole.Survivor, SurvivorAccent);
            bool startHot = hovered.Contains(startButton);
            startButton.style.backgroundColor = startHot ? Color.Lerp(accent, Color.white, 0.18f) : accent;
            Border(startButton, 2f, new Color(1f, 1f, 1f, startHot ? 0.8f : 0.35f));
            startButton.style.color = Color.white;
            bool practiceHot = hovered.Contains(practiceButton);
            practiceButton.style.backgroundColor = practiceHot ? Color.Lerp(ButtonFill, accent, 0.35f) : ButtonFill;
            Border(practiceButton, 2f, practiceHot ? accent : Line);
            practiceButton.style.color = Ink;
            foreach (var a in new[] { prevArrow, nextArrow })
            {
                a.style.backgroundColor = Color.clear;
                a.style.color = hovered.Contains(a) ? accent : new Color(Ink.r, Ink.g, Ink.b, 0.75f);
            }
        }

        private void StyleTab(Button tab, bool active, Color tabAccent)
        {
            bool hot = hovered.Contains(tab);
            tab.style.backgroundColor = active ? tabAccent : hot ? Color.Lerp(ButtonFill, tabAccent, 0.3f) : ButtonFill;
            Border(tab, 2f, active ? new Color(1f, 1f, 1f, 0.55f) : hot ? tabAccent : Line);
            tab.style.color = active ? Color.white : InkDim;
        }

        // ───────── 도우미 ─────────

        private Button MakeButton(VisualElement parent, string text, int fontSize, float height, Action onClick)
        {
            var b = new Button(onClick) { text = text, focusable = false };
            b.style.height = height;
            b.style.marginLeft = b.style.marginRight = b.style.marginTop = b.style.marginBottom = 0f;
            b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0f;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 0f;
            if (titleFont != null) b.style.unityFontDefinition = FontDefinition.FromFont(titleFont);
            b.style.fontSize = fontSize;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.RegisterCallback<PointerEnterEvent>(_ => { hovered.Add(b); Restyle(); });
            b.RegisterCallback<PointerLeaveEvent>(_ => { hovered.Remove(b); Restyle(); });
            parent.Add(b);
            return b;
        }

        private static Label Text(VisualElement parent, string text, Font font, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (font != null) l.style.unityFontDefinition = FontDefinition.FromFont(font);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0f;
            parent.Add(l);
            return l;
        }

        /// <summary>절대 위치 요소. NaN인 변은 비워 둔다.</summary>
        private static VisualElement Abs(VisualElement parent, float left, float top, float right, float bottom)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            if (!float.IsNaN(left)) el.style.left = left;
            if (!float.IsNaN(top)) el.style.top = top;
            if (!float.IsNaN(right)) el.style.right = right;
            if (!float.IsNaN(bottom)) el.style.bottom = bottom;
            parent.Add(el);
            return el;
        }

        private static void Border(VisualElement el, float width, Color color)
        {
            el.style.borderLeftWidth = el.style.borderRightWidth = el.style.borderTopWidth = el.style.borderBottomWidth = width;
            el.style.borderLeftColor = el.style.borderRightColor = el.style.borderTopColor = el.style.borderBottomColor = color;
        }

        /// <summary>가운데가 하얗고 둘레로 갈수록 투명해지는 원(빛·그림자에 색을 입혀 쓴다).</summary>
        private static Texture2D MakeRadial(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SelectRadial" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        public void Dispose()
        {
            backdrop.Dispose();
            UnityEngine.Object.Destroy(radial);
            if (panel != null) UnityEngine.Object.Destroy(panel.gameObject);
        }
    }
}

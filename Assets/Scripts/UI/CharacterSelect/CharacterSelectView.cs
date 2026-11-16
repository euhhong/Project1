using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 UI(UI Toolkit, 코드로 만든다). 배치·글꼴·넘길 때 연출만 맡고, 무엇을 고를지는 CharacterSelectScreen이 정한다.
    /// 배치(기준 1920×1080): 왼쪽 일러스트 | 가운데 3D 모델 창 | 오른쪽 게임 시작·연습 버튼과 정보, 양 끝에 ‹ › 화살표, 왼쪽 위 제목·진영 탭.
    /// </summary>
    public class CharacterSelectView : IDisposable
    {
        public event Action<int> StepRequested;          // -1 = 이전, +1 = 다음
        public event Action<CharacterRole> RoleRequested;
        public event Action<bool> StartRequested;        // true = 게임 시작(AI 봇), false = 연습
        public event Action<float> ModelDragged;         // 모델 창을 가로로 끈 거리(기준 픽셀)

        public static readonly Vector2 ModelPanelSize = new(400f, 860f);
        private const float ModelPanelRight = 470f, ModelPanelTop = 120f;
        private const float ColumnRight = 90f, ColumnWidth = 350f;
        private const float SwitchTime = 0.34f, WipeTime = 0.26f;

        private static readonly Color Ink = new(0.88f, 0.91f, 0.95f);
        private static readonly Color InkDim = new(0.6f, 0.66f, 0.74f);
        private static readonly Color Line = new(0.78f, 0.84f, 0.92f, 0.4f);
        private static readonly Color PanelFill = new(0.03f, 0.045f, 0.07f, 0.62f);
        private static readonly Color ButtonFill = new(0.07f, 0.09f, 0.13f, 0.85f);
        private static readonly Color SurvivorAccent = new(0.25f, 0.57f, 0.84f);
        private static readonly Color HunterAccent = new(0.74f, 0.17f, 0.21f);

        private readonly RuntimeUiPanel panel;
        private readonly CharacterSelectBackdrop backdrop;
        private readonly Font titleFont, bodyFont;
        private readonly Dictionary<string, Texture2D> illustrations = new();
        private readonly HashSet<VisualElement> hovered = new();

        private VisualElement illusLayer, illus, wipe, wipeLine, dots;
        private Label watermark, missing, roleLabel, nameLabel, skillName, skillMeta;
        private Button hunterTab, survivorTab, startButton, practiceButton, prevArrow, nextArrow;
        private VisualElement modelPanel, modelAccent;
        private CharacterRole role;
        private Color accent = SurvivorAccent;
        private float animTime = 99f;
        private int animDir;

        public CharacterSelectView(Texture modelTexture)
        {
            titleFont = Resources.Load<Font>("Fonts/EastSeaDokdo-Regular");
            bodyFont = Resources.Load<Font>("Fonts/GowunDodum-Regular");
            panel = RuntimeUiPanel.Create("CharacterSelectUI", 100);
            var root = panel.Root;

            backdrop = new CharacterSelectBackdrop();
            root.Add(backdrop.Root);
            BuildIllustration(root);
            BuildModelPanel(root, modelTexture);
            BuildColumn(root);
            BuildHeader(root);
            BuildArrows(root);
        }

        // ───────── 만들기 ─────────

        private void BuildIllustration(VisualElement root)
        {
            illusLayer = Abs(root, left: 90f, top: 0f, right: ModelPanelRight + ModelPanelSize.x + 10f, bottom: 0f);
            illusLayer.style.overflow = Overflow.Hidden;

            watermark = Text(illusLayer, "", titleFont, 300, new Color(1f, 1f, 1f, 0.05f));
            watermark.style.position = Position.Absolute;
            watermark.style.left = 0; watermark.style.right = 0; watermark.style.top = 210f;
            watermark.style.unityTextAlign = TextAnchor.MiddleCenter;
            watermark.style.whiteSpace = WhiteSpace.NoWrap;

            illus = Abs(illusLayer, left: 0f, top: 250f, right: 0f, bottom: 0f);
            illus.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            illus.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            illus.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Bottom);
            illus.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);

            missing = Text(illusLayer, "일러스트 준비 중", bodyFont, 30, new Color(InkDim.r, InkDim.g, InkDim.b, 0.6f));
            missing.style.position = Position.Absolute;
            missing.style.left = 0; missing.style.right = 0; missing.style.top = Length.Percent(55);
            missing.style.unityTextAlign = TextAnchor.MiddleCenter;

            // 넘길 때 비스듬히 휙 지나가는 띠(진영 색) + 뒤따르는 흰 선
            wipe = Abs(illusLayer, left: 0f, top: -400f, right: float.NaN, bottom: -400f);
            wipe.style.width = 300f;
            wipe.style.rotate = new Rotate(Angle.Degrees(16f));
            wipe.style.display = DisplayStyle.None;
            wipeLine = Abs(illusLayer, left: 0f, top: -400f, right: float.NaN, bottom: -400f);
            wipeLine.style.width = 22f;
            wipeLine.style.rotate = new Rotate(Angle.Degrees(16f));
            wipeLine.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
            wipeLine.style.display = DisplayStyle.None;
        }

        private void BuildModelPanel(VisualElement root, Texture modelTexture)
        {
            modelPanel = new VisualElement();
            modelPanel.style.position = Position.Absolute;
            modelPanel.style.right = ModelPanelRight; modelPanel.style.top = ModelPanelTop;
            modelPanel.style.width = ModelPanelSize.x; modelPanel.style.height = ModelPanelSize.y;
            modelPanel.style.backgroundColor = CharacterSelectStage.BackgroundColor;
            if (modelTexture is RenderTexture rt) modelPanel.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            Border(modelPanel, 2f, Line);
            root.Add(modelPanel);

            modelAccent = Abs(modelPanel, left: 0f, top: 0f, right: 0f, bottom: float.NaN);
            modelAccent.style.height = 4f;

            var hint = Text(root, "드래그해서 돌려 보기", bodyFont, 19, InkDim);
            hint.style.position = Position.Absolute;
            hint.style.right = ModelPanelRight; hint.style.width = ModelPanelSize.x;
            hint.style.top = ModelPanelTop + ModelPanelSize.y + 10f;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;

            float lastX = 0f;
            modelPanel.RegisterCallback<PointerDownEvent>(e => { modelPanel.CapturePointer(e.pointerId); lastX = e.position.x; });
            modelPanel.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!modelPanel.HasPointerCapture(e.pointerId)) return;
                ModelDragged?.Invoke(e.position.x - lastX);
                lastX = e.position.x;
            });
            modelPanel.RegisterCallback<PointerUpEvent>(e => modelPanel.ReleasePointer(e.pointerId));
        }

        private void BuildColumn(VisualElement root)
        {
            var col = new VisualElement();
            col.style.position = Position.Absolute;
            col.style.right = ColumnRight; col.style.width = ColumnWidth;
            col.style.top = ModelPanelTop; col.style.bottom = 1080f - ModelPanelTop - ModelPanelSize.y;
            col.style.flexDirection = FlexDirection.Column;
            root.Add(col);

            startButton = MakeButton(col, "게임 시작", 58, 96f, () => StartRequested?.Invoke(true));
            practiceButton = MakeButton(col, "연습", 46, 72f, () => StartRequested?.Invoke(false));
            practiceButton.style.marginTop = 14f;

            var info = new VisualElement();
            info.style.marginTop = 28f;
            info.style.flexGrow = 1f;
            info.style.backgroundColor = PanelFill;
            Border(info, 1f, Line);
            info.style.paddingLeft = info.style.paddingRight = 28f;
            info.style.paddingTop = 22f; info.style.paddingBottom = 22f;
            col.Add(info);

            roleLabel = Text(info, "", bodyFont, 22, accent);
            nameLabel = Text(info, "", titleFont, 104, Ink);
            nameLabel.style.marginTop = -6f;
            nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
            nameLabel.style.textShadow = new TextShadow { offset = new Vector2(0f, 4f), blurRadius = 10f, color = new Color(0f, 0f, 0f, 0.6f) };

            dots = new VisualElement();
            dots.style.flexDirection = FlexDirection.Row;
            dots.style.marginTop = 4f;
            info.Add(dots);

            var rule = new VisualElement();
            rule.style.height = 1f;
            rule.style.backgroundColor = Line;
            rule.style.marginTop = 22f; rule.style.marginBottom = 18f;
            info.Add(rule);

            var header = Text(info, "고유 스킬", titleFont, 42, Ink);
            header.name = "skill-header";
            skillName = Text(info, "", bodyFont, 30, Ink);
            skillName.style.marginTop = 6f;
            skillName.style.whiteSpace = WhiteSpace.Normal;
            skillMeta = Text(info, "", bodyFont, 22, InkDim);
            skillMeta.style.marginTop = 6f;

            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1f;
            info.Add(spacer);

            var keys = Text(info, "A·D  캐릭터 넘기기\nW·S  감시자 / 생존자\n2·G  게임 시작     1·Enter  연습", bodyFont, 18, InkDim);
            keys.style.whiteSpace = WhiteSpace.Normal;
        }

        private void BuildHeader(VisualElement root)
        {
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.style.position = Position.Absolute;
            header.style.left = 120f; header.style.top = 44f;
            root.Add(header);

            var title = Text(header, "캐릭터 선택", titleFont, 84, Ink);
            title.style.textShadow = new TextShadow { offset = new Vector2(0f, 4f), blurRadius = 12f, color = new Color(0f, 0f, 0f, 0.7f) };

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.marginTop = 8f;
            header.Add(tabs);
            hunterTab = MakeButton(tabs, "감시자", 38, 56f, () => RoleRequested?.Invoke(CharacterRole.Hunter));
            hunterTab.style.width = 180f;
            survivorTab = MakeButton(tabs, "생존자", 38, 56f, () => RoleRequested?.Invoke(CharacterRole.Survivor));
            survivorTab.style.width = 180f;
            survivorTab.style.marginLeft = 14f;
        }

        private void BuildArrows(VisualElement root)
        {
            prevArrow = MakeArrow(root, "<", () => StepRequested?.Invoke(-1));
            prevArrow.style.left = 16f;
            nextArrow = MakeArrow(root, ">", () => StepRequested?.Invoke(+1));
            nextArrow.style.right = 16f;
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
            roleLabel.text = $"{(shownRole == CharacterRole.Hunter ? "감시자" : "생존자")}   ·   {index + 1:00} / {count:00}";
            nameLabel.text = entry.displayName;
            watermark.text = entry.displayName;

            if (entry.skill != null)
            {
                skillName.text = string.IsNullOrEmpty(entry.skill.displayName) ? entry.skill.name : entry.skill.displayName;
                skillMeta.text = $"재사용 대기 {entry.skill.cooldown:0.#}초   ·   E";
            }
            else
            {
                skillName.text = "없음";
                skillMeta.text = "";
            }

            dots.Clear();
            for (int i = 0; i < count; i++)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                bool on = i == index;
                d.style.width = on ? 26f : 10f; d.style.height = 6f;
                d.style.marginRight = 6f;
                d.style.backgroundColor = on ? accent : new Color(Ink.r, Ink.g, Ink.b, 0.25f);
                dots.Add(d);
            }

            var tex = Illustration(entry.id);
            illus.style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
            missing.style.display = tex != null ? DisplayStyle.None : DisplayStyle.Flex;

            animTime = 0f;
            animDir = dir;
            Animate();
        }

        /// <summary>진영에 고를 캐릭터가 없을 때.</summary>
        public void ShowEmpty(CharacterRole shownRole)
        {
            SetRole(shownRole);
            roleLabel.text = shownRole == CharacterRole.Hunter ? "감시자" : "생존자";
            nameLabel.text = "없음";
            watermark.text = "";
            skillName.text = "고를 캐릭터가 없습니다";
            skillMeta.text = "";
            dots.Clear();
            illus.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            missing.style.display = DisplayStyle.None;
        }

        private void SetRole(CharacterRole shownRole)
        {
            role = shownRole;
            accent = role == CharacterRole.Hunter ? HunterAccent : SurvivorAccent;
            roleLabel.style.color = accent;
            modelAccent.style.backgroundColor = accent;
            wipe.style.backgroundColor = new Color(accent.r, accent.g, accent.b, 0.9f);
            var header = nameLabel.parent.Q<Label>("skill-header");
            if (header != null) header.style.color = accent;
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
            illus.style.translate = new Translate(animDir * 280f * (1f - e), 0f);
            illus.style.scale = new Scale(Vector2.one * (1.05f - 0.05f * e));
            illus.style.opacity = e;
            watermark.style.translate = new Translate(animDir * 120f * (1f - e), 0f);
            nameLabel.style.translate = new Translate(0f, 26f * (1f - e));
            nameLabel.style.opacity = e;

            float q = Mathf.Clamp01(animTime / WipeTime);
            bool wiping = animDir != 0 && q < 1f;
            wipe.style.display = wipeLine.style.display = wiping ? DisplayStyle.Flex : DisplayStyle.None;
            if (!wiping) return;
            float width = illusLayer.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f) width = 1000f;
            float s = q * q * (3f - 2f * q);
            // 다음(+1)은 오른쪽에서 왼쪽으로, 이전(−1)은 왼쪽에서 오른쪽으로 훑는다(새 일러스트가 들어오는 쪽에서)
            float x = animDir > 0 ? Mathf.Lerp(width + 200f, -560f, s) : Mathf.Lerp(-560f, width + 200f, s);
            wipe.style.left = x;
            wipeLine.style.left = animDir > 0 ? x + 330f : x - 52f;
            wipe.style.opacity = wipeLine.style.opacity = 1f - q * q;
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
            b.style.unityFontDefinition = FontDefinition.FromFont(titleFont);
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

        public void Dispose()
        {
            backdrop.Dispose();
            if (panel != null) UnityEngine.Object.Destroy(panel.gameObject);
        }
    }
}

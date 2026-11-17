using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 타이틀 화면의 가운데 창(뒤를 어둡게 덮고 클릭을 막는다). 어두운 보라 판 + 진홍 테두리 — 타이틀 그림 버튼과 같은 느낌.
    /// Message = 글 + 확인, Confirm = 글 + 예/아니오. 환경설정 창(TitleOptionsPanel)도 이 창 위에 내용을 채운다.
    /// 키보드: Enter = DefaultAction, ESC = CancelAction(TitleScreen이 부른다).
    /// </summary>
    public class TitleDialog
    {
        public static readonly Color PanelColor = new(0.055f, 0.04f, 0.075f, 0.97f);
        public static readonly Color Crimson = new(0.66f, 0.13f, 0.14f);
        public static readonly Color CrimsonHover = new(0.82f, 0.2f, 0.19f);
        public static readonly Color Ink = new(0.94f, 0.9f, 0.85f);
        public static readonly Color InkDim = new(0.72f, 0.68f, 0.7f);

        private readonly VisualElement layer;
        public VisualElement Box { get; }
        public Font Font { get; }
        public bool IsOpen => layer.style.display == DisplayStyle.Flex;
        public Action DefaultAction { get; set; }
        public Action CancelAction { get; set; }

        /// <param name="slanted">
        /// true = 오른쪽 위 'ESC 메뉴' 버튼(SlantedButton)과 같은 느낌: 살짝 기운 검은 판 + 흰 테두리 + 뒤에 어긋나게 깔린 적갈색 그림자판 + 왼쪽 사선 띠.
        /// 이때 글·버튼은 SlantedText·MakeSlantedButton으로(붓글씨 글꼴).
        /// </param>
        public TitleDialog(VisualElement root, Font font, float width, bool slanted = false)
        {
            Font = font;
            layer = new VisualElement();
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
            layer.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
            layer.style.justifyContent = Justify.Center;
            layer.style.alignItems = Align.Center;
            layer.style.display = DisplayStyle.None;
            root.Add(layer);

            Box = new VisualElement();
            Box.style.width = width;
            if (slanted)
            {
                var frame = new VisualElement();
                frame.style.rotate = new Rotate(Angle.Degrees(-3f));
                layer.Add(frame);
                var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
                shadow.style.position = Position.Absolute;
                shadow.style.left = 0; shadow.style.top = 0; shadow.style.right = 0; shadow.style.bottom = 0;
                shadow.style.backgroundColor = CharacterSelectBackdrop.Maroon;
                shadow.style.translate = new Translate(16f, 16f);
                frame.Add(shadow);
                Box.style.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 0.97f);
                SetBorder(Box, 3f, new Color(1f, 1f, 1f, 0.95f), 0f);
                Box.style.overflow = Overflow.Hidden;
                Box.style.paddingLeft = 96f; Box.style.paddingRight = 56f; // 왼쪽 사선 띠 자리
                Box.style.paddingTop = 30f; Box.style.paddingBottom = 40f;
                Box.style.alignItems = Align.Center;
                Stripe(Box, 26f, 22f, CharacterSelectBackdrop.Maroon);
                Stripe(Box, 60f, 5f, new Color(1f, 1f, 1f, 0.6f));
                frame.Add(Box);
            }
            else
            {
                Box.style.backgroundColor = PanelColor;
                SetBorder(Box, 2f, Crimson, 6f);
                Box.style.borderTopWidth = 5f;
                Box.style.paddingLeft = Box.style.paddingRight = 44f;
                Box.style.paddingTop = 34f; Box.style.paddingBottom = 32f;
                layer.Add(Box);
            }
            CancelAction = Close;
        }

        private const string BrushFont = "Fonts/EastSeaDokdo-Regular";

        /// <summary>판 왼쪽의 비스듬한 장식 띠(SlantedButton과 같은 느낌).</summary>
        private static void Stripe(VisualElement box, float left, float width, Color color)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.style.position = Position.Absolute;
            d.style.left = left; d.style.width = width;
            d.style.top = -60f; d.style.bottom = -60f;
            d.style.rotate = new Rotate(Angle.Degrees(20f));
            d.style.backgroundColor = color;
            box.Add(d);
        }

        /// <summary>기운 창의 글(붓글씨, 흰 글자 + 검은 그림자).</summary>
        public static Label SlantedText(VisualElement parent, string text, int size)
        {
            var l = Text(parent, text, Resources.Load<Font>(BrushFont), size, Color.white);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.textShadow = new TextShadow { offset = new Vector2(3f, 3f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.85f) };
            return l;
        }

        /// <summary>기운 창의 버튼 = 'ESC 메뉴'와 같은 SlantedButton(붓글씨, 적갈색). primary = 적갈색 얼굴판.</summary>
        public static SlantedButton MakeSlantedButton(VisualElement parent, string text, Action onClick, bool primary, float width = 230f, float height = 66f)
        {
            var b = new SlantedButton(parent, text, Resources.Load<Font>(BrushFont), 40, width, height, primary, onClick) { HoverSound = "ui_page" };
            b.SetAccent(CharacterSelectBackdrop.Maroon);
            b.Root.style.marginLeft = b.Root.style.marginRight = 16f;
            return b;
        }

        public void Open() { layer.style.display = DisplayStyle.Flex; layer.BringToFront(); }
        public void Close() => layer.style.display = DisplayStyle.None;

        // ───────── 자주 쓰는 창 ─────────

        /// <summary>글 한 줄 + 확인.</summary>
        public static TitleDialog Message(VisualElement root, Font font, string text)
        {
            var d = new TitleDialog(root, font, 620f);
            Text(d.Box, text, font, 34, Ink).style.unityTextAlign = TextAnchor.MiddleCenter;
            var row = ButtonRow(d.Box);
            MakeButton(row, "확인", font, d.Close, primary: true);
            d.DefaultAction = d.Close;
            return d;
        }

        /// <summary>질문 + 예/아니오. Enter·ESC는 아니오(실수로 넘어가지 않게) — 예는 클릭으로만. slanted = 'ESC 메뉴' 버튼 느낌의 기운 창.</summary>
        public static TitleDialog Confirm(VisualElement root, Font font, string text, Action onYes, bool slanted = false)
        {
            var d = new TitleDialog(root, font, slanted ? 680f : 620f, slanted);
            if (slanted)
            {
                SlantedText(d.Box, text, 52);
                var srow = ButtonRow(d.Box);
                srow.style.marginTop = 34f;
                MakeSlantedButton(srow, "예", () => { d.Close(); onYes?.Invoke(); }, primary: true, width: 190f);
                MakeSlantedButton(srow, "아니오", d.Close, primary: false, width: 190f);
                d.DefaultAction = d.Close;
                return d;
            }
            Text(d.Box, text, font, 34, Ink).style.unityTextAlign = TextAnchor.MiddleCenter;
            var row = ButtonRow(d.Box);
            MakeButton(row, "예", font, () => { d.Close(); onYes?.Invoke(); }, primary: true);
            MakeButton(row, "아니오", font, d.Close, primary: false);
            d.DefaultAction = d.Close;
            return d;
        }

        // ───────── 도우미 ─────────

        public static VisualElement ButtonRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.marginTop = 30f;
            parent.Add(row);
            return row;
        }

        public static Button MakeButton(VisualElement parent, string text, Font font, Action onClick, bool primary)
        {
            var b = new Button(onClick) { text = text };
            if (font != null) b.style.unityFontDefinition = FontDefinition.FromFont(font);
            b.style.fontSize = 30;
            b.style.color = Ink;
            b.style.width = 180f; b.style.height = 60f;
            b.style.marginLeft = b.style.marginRight = 12f;
            var normal = primary ? Crimson : new Color(1f, 1f, 1f, 0.07f);
            var hover = primary ? CrimsonHover : new Color(1f, 1f, 1f, 0.16f);
            b.style.backgroundColor = normal;
            SetBorder(b, 2f, primary ? CrimsonHover : new Color(1f, 1f, 1f, 0.35f), 4f);
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = hover);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = normal);
            parent.Add(b);
            return b;
        }

        public static Label Text(VisualElement parent, string text, Font font, int size, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (font != null) l.style.unityFontDefinition = FontDefinition.FromFont(font);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0f;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0f;
            parent.Add(l);
            return l;
        }

        public static void SetBorder(VisualElement el, float width, Color color, float radius)
        {
            el.style.borderLeftWidth = el.style.borderRightWidth = el.style.borderTopWidth = el.style.borderBottomWidth = width;
            el.style.borderLeftColor = el.style.borderRightColor = el.style.borderTopColor = el.style.borderBottomColor = color;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
        }
    }
}

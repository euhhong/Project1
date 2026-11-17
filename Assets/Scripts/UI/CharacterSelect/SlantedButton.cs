using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 비스듬히 기운 굵은 버튼(캐릭터 선택 화면의 게임 시작·연습). 얼굴판 + 뒤에 어긋나게 깔린 그림자판 + 왼쪽 사선 장식.
    /// primary = 진영 색 얼굴판(게임 시작), 아니면 검은 얼굴판(연습). 마우스를 올리면 얼굴판이 들뜨고 밝아지며, 누르면 그림자판 쪽으로 눌린다.
    /// </summary>
    public class SlantedButton
    {
        private const float Tilt = -6f;

        private readonly bool primary;
        private readonly VisualElement shadow, stripe, stripeThin;
        private readonly Button face;
        private Color accent = Color.white;
        private bool hot, pressed;

        public VisualElement Root { get; }

        /// <summary>마우스를 올릴 때 낼 화면 소리 이름(SfxPlayer.PlayUi). null = 소리 없음.</summary>
        public string HoverSound { get; set; }

        /// <summary>false = 마우스를 올려도 들뜨거나 밝아지지 않고 소리도 없다(누르기는 됨, 예: 결과 창의 나가기).</summary>
        public bool HoverEffect { get; set; } = true;

        public SlantedButton(VisualElement parent, string text, Font font, int fontSize, float width, float height, bool primary, Action onClick)
        {
            this.primary = primary;
            Root = new VisualElement();
            Root.style.width = width; Root.style.height = height;
            Root.style.rotate = new Rotate(Angle.Degrees(Tilt));
            parent.Add(Root);

            shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(shadow);
            Root.Add(shadow);

            face = new Button(onClick) { text = text, focusable = false };
            Fill(face);
            face.style.marginLeft = face.style.marginRight = face.style.marginTop = face.style.marginBottom = 0f;
            face.style.paddingTop = face.style.paddingBottom = face.style.paddingRight = 0f;
            face.style.paddingLeft = height * 0.45f; // 왼쪽 사선 장식 자리만큼 글자를 오른쪽으로
            face.style.borderTopLeftRadius = face.style.borderTopRightRadius = face.style.borderBottomLeftRadius = face.style.borderBottomRightRadius = 0f;
            face.style.overflow = Overflow.Hidden;
            if (font != null) face.style.unityFontDefinition = FontDefinition.FromFont(font);
            face.style.fontSize = fontSize;
            face.style.unityTextAlign = TextAnchor.MiddleCenter;
            face.style.color = Color.white;
            face.style.textShadow = new TextShadow { offset = new Vector2(3f, 3f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.85f) };
            Root.Add(face);

            stripe = Deco(left: height * 0.16f, width: height * 0.14f);
            stripeThin = Deco(left: height * 0.36f, width: Mathf.Max(3f, height * 0.04f));

            face.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!HoverEffect) return;
                hot = true; Restyle();
                if (HoverSound != null) Project1.Presentation.SfxPlayer.PlayUi(HoverSound, 1f, 0.08f); // 타이틀 버튼과 같은 소리
            });
            face.RegisterCallback<PointerLeaveEvent>(_ => { hot = false; pressed = false; Restyle(); });
            face.RegisterCallback<PointerDownEvent>(_ => { pressed = true; Restyle(); }, TrickleDown.TrickleDown);
            face.RegisterCallback<PointerUpEvent>(_ => { pressed = false; Restyle(); }, TrickleDown.TrickleDown);
            Restyle();
        }

        public void SetAccent(Color c)
        {
            accent = c;
            Restyle();
        }

        private void Restyle()
        {
            var black = new Color(0.03f, 0.03f, 0.04f, 1f);
            var white = new Color(1f, 1f, 1f, 0.95f);
            Color faceColor = primary ? accent : black;
            if (hot) faceColor = primary ? Color.Lerp(accent, Color.white, 0.2f) : Color.Lerp(black, accent, 0.35f);
            if (pressed) faceColor = Color.Lerp(faceColor, Color.black, 0.25f);
            face.style.backgroundColor = faceColor;
            Border(face, 3f, white);

            shadow.style.backgroundColor = primary ? black : accent;
            Border(shadow, primary ? 3f : 0f, white);

            // 들뜸(마우스 올림)·눌림: 얼굴판과 그림자판 사이 간격
            Vector2 faceOffset = pressed ? new Vector2(6f, 6f) : hot ? new Vector2(-4f, -4f) : Vector2.zero;
            face.style.translate = new Translate(faceOffset.x, faceOffset.y);
            shadow.style.translate = new Translate(10f, 10f);

            var deco = primary ? new Color(0f, 0f, 0f, 0.4f) : accent;
            stripe.style.backgroundColor = deco;
            stripeThin.style.backgroundColor = primary ? new Color(1f, 1f, 1f, 0.85f) : new Color(1f, 1f, 1f, 0.6f);
        }

        /// <summary>얼굴판 왼쪽의 비스듬한 장식 띠.</summary>
        private VisualElement Deco(float left, float width)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.style.position = Position.Absolute;
            d.style.left = left; d.style.width = width;
            d.style.top = -20f; d.style.bottom = -20f;
            d.style.rotate = new Rotate(Angle.Degrees(20f));
            face.Add(d);
            return d;
        }

        private static void Fill(VisualElement el)
        {
            el.style.position = Position.Absolute;
            el.style.left = 0; el.style.top = 0; el.style.right = 0; el.style.bottom = 0;
        }

        private static void Border(VisualElement el, float width, Color color)
        {
            el.style.borderLeftWidth = el.style.borderRightWidth = el.style.borderTopWidth = el.style.borderBottomWidth = width;
            el.style.borderLeftColor = el.style.borderRightColor = el.style.borderTopColor = el.style.borderBottomColor = color;
        }
    }
}

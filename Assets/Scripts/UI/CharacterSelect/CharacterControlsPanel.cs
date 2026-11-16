using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 오른쪽 정보 칸: 고른 캐릭터의 조작만 키 그림과 함께 짧게 보여 준다(CharacterControlsText).
    /// 감시자: 좌클릭 평타 / 좌클릭 꾹(차징) / E 스킬. 생존자: 여기에 스페이스바 판자 내리기, 차징은 패링(마우스를 올리면 설명).
    /// </summary>
    public class CharacterControlsPanel
    {
        private static readonly Color Ink = new(0.9f, 0.92f, 0.95f);
        private static readonly Color InkDim = new(0.62f, 0.68f, 0.76f);
        private static readonly Color Line = new(0.78f, 0.84f, 0.92f, 0.4f);

        private readonly Font titleFont, bodyFont;
        private readonly VisualElement panel, header, list, tooltip;
        private readonly Label headerLabel, tooltipLabel;
        private Color accent;

        public CharacterControlsPanel(VisualElement parent, Font titleFont, Font bodyFont, float left, float top, float width, float bottom)
        {
            this.titleFont = titleFont;
            this.bodyFont = bodyFont;

            panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = left; panel.style.top = top; panel.style.width = width; panel.style.bottom = bottom;
            panel.style.backgroundColor = new Color(0.03f, 0.045f, 0.07f, 0.62f);
            SetBorder(panel, 1f, Line, 0f);
            panel.style.paddingLeft = panel.style.paddingRight = 30f;
            panel.style.paddingTop = 22f; panel.style.paddingBottom = 22f;
            parent.Add(panel);

            header = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.Add(header);
            headerLabel = Text(header, "조작", titleFont, 46, Ink);
            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            rule.style.height = 2f; rule.style.width = 120f;
            rule.style.marginTop = 2f; rule.style.marginBottom = 26f;
            rule.name = "rule";
            header.Add(rule);

            list = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.Add(list);

            tooltip = new VisualElement { pickingMode = PickingMode.Ignore };
            tooltip.style.position = Position.Absolute;
            tooltip.style.left = 24f; tooltip.style.right = 24f;
            tooltip.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.96f);
            tooltip.style.paddingLeft = tooltip.style.paddingRight = 16f;
            tooltip.style.paddingTop = tooltip.style.paddingBottom = 12f;
            tooltip.style.display = DisplayStyle.None;
            panel.Add(tooltip);
            tooltipLabel = Text(tooltip, "", bodyFont, 19, Ink);
            tooltipLabel.style.whiteSpace = WhiteSpace.Normal;
        }

        public void Show(CharacterRoster.Entry entry, Color roleAccent)
        {
            accent = roleAccent;
            header.Q("rule").style.backgroundColor = accent;
            SetBorder(tooltip, 1f, accent, 4f);
            tooltip.style.display = DisplayStyle.None;
            list.Clear();
            if (entry == null) return;

            bool survivor = entry.role == CharacterRole.Survivor;
            var c = CharacterControlsText.For(entry);

            Row(Mouse(false), "평타", null, null);
            if (survivor) Row(Mouse(true), "차징 · " + CharacterControlsText.SurvivorCharge, null, CharacterControlsText.ParryTooltip);
            else Row(Mouse(true), "차징", c.Charge, null);
            string skillName = entry.skill != null && !string.IsNullOrEmpty(entry.skill.displayName) ? entry.skill.displayName : "스킬";
            Row(Key("E", 44f), skillName, c.Skill, null);
            if (survivor) Row(Key("SPACE", 92f), CharacterControlsText.PalletDrop, CharacterControlsText.PalletDropDesc, null);
        }

        public void ShowEmpty()
        {
            list.Clear();
            tooltip.style.display = DisplayStyle.None;
        }

        // ───────── 한 줄: [키 그림]  제목 / 설명 ─────────

        private void Row(VisualElement icon, string title, string desc, string tip)
        {
            var row = new VisualElement { pickingMode = tip != null ? PickingMode.Position : PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 24f;
            list.Add(row);

            var iconBox = new VisualElement { pickingMode = PickingMode.Ignore };
            iconBox.style.width = 104f;
            iconBox.style.flexShrink = 0f;
            iconBox.style.flexDirection = FlexDirection.Row;
            iconBox.style.alignItems = Align.Center;
            iconBox.style.justifyContent = Justify.Center;
            iconBox.Add(icon);
            row.Add(iconBox);

            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.style.flexGrow = 1f; col.style.flexShrink = 1f;
            col.style.marginLeft = 14f;
            row.Add(col);

            var titleRow = new VisualElement { pickingMode = PickingMode.Ignore };
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            col.Add(titleRow);
            Text(titleRow, title, titleFont, 36, Ink);
            if (tip != null) titleRow.Add(Badge());

            if (!string.IsNullOrEmpty(desc))
            {
                var d = Text(col, desc, bodyFont, 20, InkDim);
                d.style.whiteSpace = WhiteSpace.Normal;
                d.style.marginTop = 2f;
            }

            if (tip == null) return;
            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                tooltipLabel.text = tip;
                tooltip.style.top = row.layout.yMax + list.layout.y + 2f;
                tooltip.style.display = DisplayStyle.Flex;
                tooltip.BringToFront();
            });
            row.RegisterCallback<PointerLeaveEvent>(_ => tooltip.style.display = DisplayStyle.None);
        }

        // ───────── 키 그림 ─────────

        /// <summary>마우스: 왼쪽 버튼을 진영 색으로 칠한다. hold = 꾹(누르고 있기) 표시를 붙인다.</summary>
        private VisualElement Mouse(bool hold)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.style.width = 32f; body.style.height = 46f;
            SetBorder(body, 2f, Ink, 16f);
            body.style.overflow = Overflow.Hidden;
            box.Add(body);

            var left = new VisualElement { pickingMode = PickingMode.Ignore };
            left.style.position = Position.Absolute;
            left.style.left = 0; left.style.top = 0; left.style.width = Length.Percent(50); left.style.height = Length.Percent(44);
            left.style.backgroundColor = accent;
            body.Add(left);
            var split = new VisualElement { pickingMode = PickingMode.Ignore };
            split.style.position = Position.Absolute;
            split.style.left = Length.Percent(50); split.style.top = 0; split.style.width = 2f; split.style.height = Length.Percent(44);
            split.style.translate = new Translate(-1f, 0f);
            split.style.backgroundColor = Ink;
            body.Add(split);
            var waist = new VisualElement { pickingMode = PickingMode.Ignore };
            waist.style.position = Position.Absolute;
            waist.style.left = 0; waist.style.right = 0; waist.style.top = Length.Percent(44); waist.style.height = 2f;
            waist.style.backgroundColor = Ink;
            body.Add(waist);

            if (hold)
            {
                var h = Text(box, "꾹", titleFont, 28, accent);
                h.style.marginLeft = 8f;
            }
            return box;
        }

        /// <summary>키보드 키(살짝 도톰한 아래 테두리).</summary>
        private VisualElement Key(string label, float width)
        {
            var key = new VisualElement { pickingMode = PickingMode.Ignore };
            key.style.width = width; key.style.height = 42f;
            SetBorder(key, 2f, Ink, 7f);
            key.style.borderBottomWidth = 5f;
            key.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            key.style.justifyContent = Justify.Center;
            key.style.alignItems = Align.Center;
            var l = Text(key, label, bodyFont, label.Length > 1 ? 16 : 22, Ink);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return key;
        }

        /// <summary>마우스를 올리면 설명이 뜬다는 표시(?).</summary>
        private VisualElement Badge()
        {
            var b = new VisualElement { pickingMode = PickingMode.Ignore };
            b.style.width = 26f; b.style.height = 26f;
            b.style.marginLeft = 10f;
            SetBorder(b, 2f, accent, 13f);
            b.style.justifyContent = Justify.Center;
            b.style.alignItems = Align.Center;
            var q = Text(b, "?", bodyFont, 17, accent);
            q.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        // ───────── 도우미 ─────────

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

        private static void SetBorder(VisualElement el, float width, Color color, float radius)
        {
            el.style.borderLeftWidth = el.style.borderRightWidth = el.style.borderTopWidth = el.style.borderBottomWidth = width;
            el.style.borderLeftColor = el.style.borderRightColor = el.style.borderTopColor = el.style.borderBottomColor = color;
            el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = radius;
        }
    }
}

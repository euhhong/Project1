using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>타이틀 화면 크레딧 창(TitleDialog 위에 이름·출처 몇 줄 + 닫기).</summary>
    public static class TitleCredits
    {
        private static readonly (string head, string body)[] Lines =
        {
            ("OLETUS", "Non-commercial fan game"),
            ("Character models", "Identity V © NetEase Games (fan use)"),
            ("3D props", "VARCO 3D"),
            ("Fonts", "Gowun Dodum, East Sea Dokdo (SIL Open Font License)"),
            ("Engine", "Made with Unity"),
        };

        public static TitleDialog Create(VisualElement root, Font font)
        {
            var d = new TitleDialog(root, font, 760f);
            TitleDialog.Text(d.Box, "크레딧", font, 46, TitleDialog.Ink);
            var rule = new VisualElement();
            rule.style.height = 2f; rule.style.width = 140f;
            rule.style.backgroundColor = TitleDialog.Crimson;
            rule.style.marginTop = 4f; rule.style.marginBottom = 18f;
            d.Box.Add(rule);

            foreach (var (head, body) in Lines)
            {
                var h = TitleDialog.Text(d.Box, head, font, 22, TitleDialog.CrimsonHover);
                h.style.unityFontStyleAndWeight = FontStyle.Bold;
                h.style.marginTop = 10f;
                TitleDialog.Text(d.Box, body, font, 27, TitleDialog.Ink);
            }

            var row = TitleDialog.ButtonRow(d.Box);
            TitleDialog.MakeButton(row, "닫기", font, d.Close, primary: true);
            d.DefaultAction = d.Close;
            return d;
        }
    }
}

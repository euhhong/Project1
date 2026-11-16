using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 타이틀 그림에 그려진 비스듬한 버튼 하나 위에 겹치는 보이지 않는 클릭 칸.
    /// 모양은 그림 픽셀로 적은 네 꼭짓점(평행사변형)이고, 그 모양 안을 눌러야만 눌린다(ContainsPoint).
    /// 골라지면(마우스를 올리거나 키보드로) 같은 모양으로 빛난다: 바깥 붉은 번짐 + 하얀 테두리 + 옅은 채움.
    /// </summary>
    public class TitleMenuButton : VisualElement
    {
        private static readonly Color GlowColor = new(0.95f, 0.16f, 0.16f);
        private static readonly Color EdgeColor = new(1f, 0.94f, 0.9f);

        private readonly Vector2[] corners; // 0~1, 칸 안에서의 꼭짓점(왼위·오위·오아래·왼아래)
        private bool lit;

        /// <param name="imagePoints">그림 픽셀 꼭짓점(위가 0) — 왼위·오위·오아래·왼아래</param>
        public TitleMenuButton(Vector2[] imagePoints, float artWidth, float artHeight)
        {
            var min = imagePoints[0]; var max = imagePoints[0];
            foreach (var p in imagePoints) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            var size = max - min;
            corners = new Vector2[imagePoints.Length];
            for (int i = 0; i < imagePoints.Length; i++) corners[i] = new Vector2((imagePoints[i].x - min.x) / size.x, (imagePoints[i].y - min.y) / size.y);

            style.position = Position.Absolute;
            style.left = Length.Percent(min.x / artWidth * 100f);
            style.top = Length.Percent(min.y / artHeight * 100f);
            style.width = Length.Percent(size.x / artWidth * 100f);
            style.height = Length.Percent(size.y / artHeight * 100f);
            generateVisualContent += Draw;
        }

        public bool Lit
        {
            get => lit;
            set { if (lit == value) return; lit = value; MarkDirtyRepaint(); }
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f) return false;
            var p = new Vector2(localPoint.x / r.width, localPoint.y / r.height);
            bool inside = false;
            for (int i = 0, j = corners.Length - 1; i < corners.Length; j = i++)
            {
                var a = corners[i]; var b = corners[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            if (!lit) return;
            var r = contentRect;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Miter;

            // 바깥 번짐: 굵고 옅은 붉은 선을 몇 겹
            for (int k = 3; k >= 1; k--)
            {
                p.strokeColor = new Color(GlowColor.r, GlowColor.g, GlowColor.b, 0.13f);
                p.lineWidth = 6f * k;
                Path(p, r);
                p.Stroke();
            }
            // 옅은 채움
            p.fillColor = new Color(1f, 0.85f, 0.8f, 0.10f);
            Path(p, r);
            p.Fill();
            // 하얀 테두리
            p.strokeColor = EdgeColor;
            p.lineWidth = 2.5f;
            Path(p, r);
            p.Stroke();
        }

        private void Path(Painter2D p, Rect r)
        {
            p.BeginPath();
            for (int i = 0; i < corners.Length; i++)
            {
                var pt = new Vector2(r.x + corners[i].x * r.width, r.y + corners[i].y * r.height);
                if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
            }
            p.ClosePath();
        }
    }
}

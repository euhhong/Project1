using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 배경: 어두운 적갈색·검정·흰색만. 대칭·네모 대신 변칙적이고 뾰족한 기하학 조각 몇 개 —
    /// 일러스트 뒤 큰 적갈색 파편, 그 안의 더 어두운 파편, 길게 찌르는 흰 가시, 번개처럼 꺾인 흰 선, 왼쪽 아래(모델 자리)·오른쪽 위(버튼 뒤) 조각.
    /// 조각은 Painter2D로 그린다(기준 1920×1080 좌표를 화면 크기에 맞춰 늘림). 층마다 아주 천천히 다르게 흔들린다.
    /// </summary>
    public class CharacterSelectBackdrop : IDisposable
    {
        public static readonly Color Maroon = new(0.32f, 0.07f, 0.06f);
        public static readonly Color MaroonDark = new(0.17f, 0.035f, 0.03f);
        public static readonly Color Black = new(0.03f, 0.022f, 0.022f);
        private static readonly Color White = new(0.96f, 0.94f, 0.92f);

        private readonly VisualElement shards, spikes, bolts;
        private float time;

        public VisualElement Root { get; }

        public CharacterSelectBackdrop()
        {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0; Root.style.top = 0; Root.style.right = 0; Root.style.bottom = 0;
            Root.style.overflow = Overflow.Hidden;
            Root.style.backgroundColor = Black;

            shards = Layer(DrawShards);
            spikes = Layer(DrawSpikes);
            bolts = Layer(DrawBolts);
        }

        public void Tick(float dt)
        {
            time += dt;
            shards.style.translate = new Translate(Mathf.Sin(time * 0.25f) * 8f, Mathf.Cos(time * 0.2f) * 4f);
            spikes.style.translate = new Translate(Mathf.Sin(time * 0.33f + 1.7f) * -10f, 0f);
            bolts.style.translate = new Translate(Mathf.Sin(time * 0.4f + 0.6f) * 6f, Mathf.Sin(time * 0.3f) * 3f);
        }

        private VisualElement Layer(Action<Painter2D, Vector2> draw)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.left = -30f; el.style.top = -30f; el.style.right = -30f; el.style.bottom = -30f; // 흔들려도 끝이 비지 않게 조금 크게
            el.generateVisualContent += ctx =>
            {
                var r = el.contentRect;
                var scale = new Vector2(r.width / 1980f, r.height / 1140f);
                draw(ctx.painter2D, scale);
            };
            Root.Add(el);
            return el;
        }

        // 기준 좌표(1920×1080) → 이 층 좌표(층이 30씩 크므로 +30)
        private static Vector2 P(Vector2 s, float x, float y) => new((x + 30f) * s.x, (y + 30f) * s.y);

        private static void Poly(Painter2D p, Vector2 s, Color c, params float[] xy)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(P(s, xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(s, xy[i], xy[i + 1]));
            p.ClosePath();
            p.Fill();
        }

        private static void Polyline(Painter2D p, Vector2 s, Color c, float width, params float[] xy)
        {
            p.strokeColor = c;
            p.lineWidth = width * s.y;
            p.lineJoin = LineJoin.Miter;
            p.BeginPath();
            p.MoveTo(P(s, xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(s, xy[i], xy[i + 1]));
            p.Stroke();
        }

        private static void DrawShards(Painter2D p, Vector2 s)
        {
            // 일러스트 뒤 큰 적갈색 파편(들쭉날쭉)
            Poly(p, s, Maroon, 90, -30, 1210, -30, 1010, 300, 1290, 470, 860, 1110, 150, 1110, 330, 700, -30, 610, 210, 330);
            // 그 안의 어두운 파편(비스듬히 찢긴 틈)
            Poly(p, s, MaroonDark, 640, -30, 900, -30, 560, 520, 760, 560, 380, 1110, 250, 1110, 470, 600, 300, 560);
            // 왼쪽 아래 모델 자리 — 밝은 적갈색 쐐기 + 검은 틈
            Poly(p, s, new Color(0.45f, 0.1f, 0.08f), -30, 760, 300, 940, 470, 1110, -30, 1110);
            Poly(p, s, Black, -30, 900, 230, 1010, 120, 1110, -30, 1110);
            // 오른쪽 위 버튼 뒤 — 어두운 적갈색 비스듬한 조각
            Poly(p, s, MaroonDark, 1480, -30, 1950, -30, 1950, 230, 1700, 300, 1840, 120);
        }

        private static void DrawSpikes(Painter2D p, Vector2 s)
        {
            // 위에서 아래로 길게 찌르는 흰 가시(밑동 → 끝으로 가늘어짐)
            Poly(p, s, White, 1395, -30, 1440, -30, 1060, 820);
            Poly(p, s, new Color(White.r, White.g, White.b, 0.55f), 1460, -30, 1476, -30, 1220, 520);
            // 오른쪽 아래에서 왼쪽으로 찌르는 가시
            Poly(p, s, White, 1950, 930, 1950, 975, 1330, 1060);
            // 왼쪽에서 오른쪽 위로 찌르는 검은 가시(적갈색 파편 위)
            Poly(p, s, Black, -30, 290, -30, 330, 760, 140);
            // 적갈색 파편 가장자리에 붙은 작은 흰 조각들
            Poly(p, s, White, 1290, 470, 1335, 455, 1240, 560);
            Poly(p, s, White, 860, 1110, 905, 1110, 930, 990);
            Poly(p, s, new Color(White.r, White.g, White.b, 0.8f), 210, 330, 250, 300, 120, 420);
        }

        private static void DrawBolts(Painter2D p, Vector2 s)
        {
            // 번개처럼 꺾인 흰 선(일러스트 오른쪽)
            Polyline(p, s, new Color(White.r, White.g, White.b, 0.75f), 4f, 1300, -30, 1235, 150, 1320, 190, 1190, 430, 1275, 460, 1130, 780);
            // 가는 적갈색 선(오른쪽 아래)
            Polyline(p, s, Maroon, 3f, 1950, 640, 1640, 720, 1720, 760, 1420, 900);
            // 왼쪽 위 짧은 흰 꺾은선
            Polyline(p, s, new Color(White.r, White.g, White.b, 0.5f), 3f, -30, 120, 140, 90, 90, 160, 300, 110);
        }

        public void Dispose() { }
    }
}

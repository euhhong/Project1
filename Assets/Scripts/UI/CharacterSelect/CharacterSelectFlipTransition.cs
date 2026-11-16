using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터를 넘길 때 "파라락" 연출: 비스듬한 띠 여러 장이 화면 아래에서 위로 차례차례 휙 지나간다(넘긴 방향 쪽 띠부터).
    /// 띠는 적갈색·검정·어두운 적갈색을 번갈아 쓰고, 앞쪽 끝에 흰 선이 있다. 화면을 다 덮는 순간쯤 새 캐릭터로 바뀐다(CoverTime).
    /// </summary>
    public class CharacterSelectFlipTransition
    {
        private const int Strips = 9;
        private const float StripTime = 0.24f;  // 띠 하나가 아래에서 위로 지나가는 시간
        private const float Stagger = 0.028f;   // 띠 사이 시차
        private const float Slant = -18f;

        /// <summary>시작부터 새 캐릭터가 드러나기 시작하는 때(초) — 띠들이 화면을 거의 덮는 순간.</summary>
        public const float CoverTime = 0.12f;

        private readonly VisualElement[] strips = new VisualElement[Strips];
        private float time = 99f;
        private int dir = 1;

        public VisualElement Root { get; }

        public CharacterSelectFlipTransition()
        {
            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0; Root.style.top = 0; Root.style.right = 0; Root.style.bottom = 0;
            Root.style.overflow = Overflow.Hidden;
            Root.style.display = DisplayStyle.None;

            float w = 1920f / Strips;
            for (int i = 0; i < Strips; i++)
            {
                var s = new VisualElement { pickingMode = PickingMode.Ignore };
                s.style.position = Position.Absolute;
                s.style.width = w + 70f; s.style.height = 2400f;
                s.style.left = i * w - 35f; s.style.top = -210f;
                s.style.rotate = new Rotate(Angle.Degrees(-Slant));
                var edge = new VisualElement { pickingMode = PickingMode.Ignore };
                edge.style.position = Position.Absolute;
                edge.style.left = 0; edge.style.right = 0; edge.style.top = 0; edge.style.height = 10f;
                edge.style.backgroundColor = new Color(1f, 1f, 1f, 0.95f);
                s.Add(edge);
                Root.Add(s);
                strips[i] = s;
            }
        }

        /// <summary>파라락을 시작한다. direction: +1 다음(왼쪽 띠부터), −1 이전(오른쪽 띠부터).</summary>
        public void Play(Color accent, int direction)
        {
            dir = direction >= 0 ? 1 : -1;
            // 배경과 같은 색만: 적갈색·검정·어두운 적갈색(앞쪽 끝은 흰 선)
            for (int i = 0; i < Strips; i++)
                strips[i].style.backgroundColor = (i % 3) switch { 0 => CharacterSelectBackdrop.Maroon, 1 => CharacterSelectBackdrop.Black, _ => CharacterSelectBackdrop.MaroonDark };
            time = 0f;
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            Tick(0f);
        }

        public void Tick(float dt)
        {
            if (Root.style.display == DisplayStyle.None) return;
            time += dt;
            bool any = false;
            for (int i = 0; i < Strips; i++)
            {
                int order = dir > 0 ? i : Strips - 1 - i;
                float p = Mathf.Clamp01((time - order * Stagger) / StripTime);
                if (p < 1f) any = true;
                // 아래(+1500)에서 위(−2700, 다 빠져나감)로: 가운데에서 빠르고 양 끝에서 살짝 느리게
                float s = p * p * (3f - 2f * p);
                strips[i].style.translate = new Translate(0f, Mathf.Lerp(1500f, -2700f, s));
            }
            if (!any) Root.style.display = DisplayStyle.None;
        }
    }
}

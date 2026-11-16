using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 배경(댄디하게): 거의 검은 바탕 위에 비스듬한(−18°) 굵은 판들 — 진영 색 큰 판, 흰 테두리 검은 판, 가는 흰 사선,
    /// 망점(점무늬) 판, 왼쪽 아래 진영 색 쐐기(모델 자리), 오른쪽 위 빗금 판. 진영 색(SetAccent)을 따라 칠한다. 판들은 아주 천천히 흔들린다.
    /// 그림(망점·빗금)은 실행 중에 코드로 만든다.
    /// </summary>
    public class CharacterSelectBackdrop : IDisposable
    {
        private const float Slant = -18f;

        private readonly Texture2D dotTex, hatchTex;
        private readonly VisualElement bigSlab, darkSlab, dots, wedge, hatch;
        private readonly VisualElement[] slashes;
        private float time;

        public VisualElement Root { get; }

        public CharacterSelectBackdrop()
        {
            dotTex = MakeDot(32, 0.32f);
            hatchTex = MakeHatch(24);

            Root = new VisualElement { pickingMode = PickingMode.Ignore };
            Root.style.position = Position.Absolute;
            Root.style.left = 0; Root.style.top = 0; Root.style.right = 0; Root.style.bottom = 0;
            Root.style.overflow = Overflow.Hidden;
            Root.style.backgroundColor = new Color(0.035f, 0.035f, 0.045f);

            // 망점 판(일러스트 뒤 넓게)
            dots = Slab(centerX: 640f, width: 1100f);
            dots.style.backgroundImage = new StyleBackground(dotTex);
            dots.style.backgroundSize = new BackgroundSize(new Length(22f), new Length(22f));
            dots.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);

            // 진영 색 큰 판(일러스트 바로 뒤)
            bigSlab = Slab(centerX: 860f, width: 520f);

            // 흰 테두리 검은 판(일러스트 오른쪽)
            darkSlab = Slab(centerX: 1210f, width: 170f);
            darkSlab.style.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
            darkSlab.style.borderLeftWidth = 5f;
            darkSlab.style.borderLeftColor = new Color(1f, 1f, 1f, 0.9f);

            // 가는 흰 사선
            slashes = new[]
            {
                Line(330f, 3f, 0.55f), Line(1330f, 2f, 0.35f), Line(1372f, 6f, 0.18f), Line(1760f, 2f, 0.2f),
            };

            // 오른쪽 위 빗금 판(버튼 뒤)
            hatch = Slab(centerX: 1700f, width: 520f);
            hatch.style.top = -900f; hatch.style.height = 1500f;
            hatch.style.backgroundImage = new StyleBackground(hatchTex);
            hatch.style.backgroundSize = new BackgroundSize(new Length(24f), new Length(24f));
            hatch.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            hatch.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 0.07f);

            // 왼쪽 아래 진영 색 쐐기(작은 3D 모델이 서는 자리)
            wedge = new VisualElement { pickingMode = PickingMode.Ignore };
            wedge.style.position = Position.Absolute;
            wedge.style.width = 720f; wedge.style.height = 720f;
            wedge.style.left = -360f; wedge.style.top = 1080f - 260f;
            wedge.style.rotate = new Rotate(Angle.Degrees(45f + Slant));
            Root.Add(wedge);

            // 아래쪽을 살짝 어둡게(이름 글자가 잘 보이게)
            var shade = new VisualElement { pickingMode = PickingMode.Ignore };
            shade.style.position = Position.Absolute;
            shade.style.left = 0; shade.style.right = 0; shade.style.bottom = 0; shade.style.height = 260f;
            shade.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            Root.Add(shade);

            SetAccent(new Color(0.25f, 0.57f, 0.84f));
        }

        /// <summary>진영 색으로 판을 칠한다.</summary>
        public void SetAccent(Color accent)
        {
            bigSlab.style.backgroundColor = new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f, 1f);
            dots.style.unityBackgroundImageTintColor = new Color(accent.r, accent.g, accent.b, 0.28f);
            wedge.style.backgroundColor = new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f, 1f);
        }

        public void Tick(float dt)
        {
            time += dt;
            // 판들이 아주 천천히 엇갈려 흔들린다(멈춰 있는 그림처럼 보이지 않게)
            bigSlab.style.translate = new Translate(Mathf.Sin(time * 0.35f) * 10f, 0f);
            darkSlab.style.translate = new Translate(Mathf.Sin(time * 0.35f + 1.3f) * -8f, 0f);
            dots.style.translate = new Translate(Mathf.Sin(time * 0.2f) * 16f, Mathf.Cos(time * 0.2f) * 8f);
            for (int i = 0; i < slashes.Length; i++)
                slashes[i].style.translate = new Translate(Mathf.Sin(time * 0.3f + i) * 6f, 0f);
        }

        /// <summary>화면 높이보다 길게 세로로 늘인 판을 비스듬히 기울인다(centerX = 화면 가로 가운데 위치).</summary>
        private VisualElement Slab(float centerX, float width)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.width = width; el.style.height = 2000f;
            el.style.left = centerX - width * 0.5f; el.style.top = -460f;
            el.style.rotate = new Rotate(Angle.Degrees(-Slant));
            Root.Add(el);
            return el;
        }

        private VisualElement Line(float centerX, float width, float alpha)
        {
            var el = Slab(centerX, width);
            el.style.backgroundColor = new Color(1f, 1f, 1f, alpha);
            return el;
        }

        private static Texture2D MakeDot(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "SelectDot" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((radius - d) * size * 0.7f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D MakeHatch(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "SelectHatch" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Repeat(x + y, size) / size; // 대각선 빗금
                    float a = t < 0.3f ? 1f : 0f;
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(dotTex);
            UnityEngine.Object.Destroy(hatchTex);
        }
    }
}

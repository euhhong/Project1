using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 배경: 어두운 남색 + 가운데 옅은 빛 + 안개(천천히 흐름) + 떠다니는 먼지. 그림은 실행 중에 코드로 만든다.
    /// </summary>
    public class CharacterSelectBackdrop : IDisposable
    {
        private struct Mote { public VisualElement el; public Vector2 pos; public float speed, sway, phase; }

        private readonly Texture2D baseTex, fogTex;
        private readonly VisualElement fogFar, fogNear;
        private readonly List<Mote> motes = new();
        private float time;

        public VisualElement Root { get; }

        public CharacterSelectBackdrop()
        {
            baseTex = MakeBase(480, 270);
            fogTex = MakeFog(512, 256);

            Root = Layer();
            var sky = Layer();
            sky.style.backgroundImage = new StyleBackground(baseTex);
            sky.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Root.Add(sky);

            fogFar = FogLayer(0.55f, flip: true);
            fogNear = FogLayer(0.9f, flip: false);

            for (int i = 0; i < 26; i++)
            {
                float size = Random.Range(2f, 5f);
                var el = new VisualElement { pickingMode = PickingMode.Ignore };
                el.style.position = Position.Absolute;
                el.style.width = size; el.style.height = size;
                el.style.borderTopLeftRadius = el.style.borderTopRightRadius = el.style.borderBottomLeftRadius = el.style.borderBottomRightRadius = size;
                el.style.backgroundColor = new Color(0.85f, 0.88f, 0.8f, Random.Range(0.15f, 0.45f));
                Root.Add(el);
                motes.Add(new Mote { el = el, pos = new Vector2(Random.value, Random.value), speed = Random.Range(0.006f, 0.02f), sway = Random.Range(0.004f, 0.012f), phase = Random.Range(0f, 6.3f) });
            }
        }

        private static VisualElement Layer()
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.left = 0; el.style.top = 0; el.style.right = 0; el.style.bottom = 0;
            return el;
        }

        private VisualElement FogLayer(float opacity, bool flip)
        {
            var el = new VisualElement { pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.left = Length.Percent(-20); el.style.width = Length.Percent(140);
            el.style.top = 0; el.style.bottom = 0;
            el.style.backgroundImage = new StyleBackground(fogTex);
            el.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            el.style.opacity = opacity;
            if (flip) el.style.scale = new Scale(new Vector2(-1f, 1f));
            Root.Add(el);
            return el;
        }

        public void Tick(float dt)
        {
            time += dt;
            fogFar.style.translate = new Translate(Mathf.Sin(time * 0.045f) * 140f, 0f);
            fogNear.style.translate = new Translate(Mathf.Sin(time * 0.07f + 2f) * -180f, 0f);
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                m.pos.y -= m.speed * dt;
                if (m.pos.y < -0.02f) { m.pos.y = 1.02f; m.pos.x = Random.value; }
                float x = m.pos.x + Mathf.Sin(time * 0.6f + m.phase) * m.sway;
                m.el.style.left = Length.Percent(x * 100f);
                m.el.style.top = Length.Percent(m.pos.y * 100f);
                motes[i] = m;
            }
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Mathf.PerlinNoise(x, y) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        private static Texture2D MakeBase(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SelectBackdrop" };
            var px = new Color32[w * h];
            var bottom = new Color(0.03f, 0.04f, 0.06f);
            var top = new Color(0.08f, 0.1f, 0.14f);
            var glow = new Color(0.11f, 0.15f, 0.21f);
            var mist = new Color(0.06f, 0.075f, 0.1f);
            float aspect = (float)w / h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f), v = y / (h - 1f); // v = 0이 화면 아래
                    var c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, v));
                    float dx = (u - 0.42f) * aspect, dy = v - 0.6f;
                    c += glow * Mathf.Exp(-(dx * dx + dy * dy) * 2.2f);
                    c += mist * Mathf.Pow(Fbm(u * 3.5f + 11f, v * 2.2f + 3f, 5), 1.6f);
                    float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.52f));
                    c *= 1f - 0.7f * Mathf.SmoothStep(0.3f, 0.85f, r);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D MakeFog(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SelectFog" };
            var px = new Color32[w * h];
            var tint = new Color(0.55f, 0.62f, 0.74f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f), v = y / (h - 1f);
                    float n = Fbm(u * 5f + 40f, v * 2.5f + 7f, 5);
                    float low = Mathf.Pow(1f - v, 1.4f) * 0.8f + 0.2f; // 아래쪽이 짙다
                    float edge = Mathf.SmoothStep(0f, 0.15f, u) * Mathf.SmoothStep(0f, 0.15f, 1f - u); // 좌우 끝은 흐리게(흐를 때 경계가 안 보이게)
                    float a = Mathf.Clamp01((n - 0.38f) * 2.2f) * low * edge * 0.2f;
                    var c = tint; c.a = a;
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(baseTex);
            UnityEngine.Object.Destroy(fogTex);
        }
    }
}

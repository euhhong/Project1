using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.UI.Editor
{
    /// <summary>
    /// 캐릭터 일러스트(Assets/UI/Resources/Illustrations) 가져오기 설정 + 흰 배경 빼기.
    /// 원본 PNG는 그대로 두고, 가져올 때만 테두리에 이어진 흰 배경을 투명하게 만든다(어두운 선택 화면 위에 바로 얹으려고).
    /// 연기·나비처럼 흰색으로 옅어지는 부분은 반투명으로 남고, 캐릭터 안의 흰 부분(얼굴·앞치마)은 외곽선에 막혀 그대로다.
    /// </summary>
    public class IllustrationImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/UI/Resources/Illustrations/";

        private const float SeedMin = 0.9f;   // 테두리에서 이 정도로 흰 픽셀부터 배경으로 본다
        private const float FloodMin = 0.45f; // 배경은 이보다 밝은 픽셀로만 번진다
        private const float FullMin = 0.8f;   // 이보다 밝으면 흰색을 완전히 뺀다(그 사이는 조금만)
        private const float MaxStep = 0.12f;  // 이웃과 밝기 차가 이보다 크면(외곽선) 번지지 않는다
        private const int EdgeGrow = 2;       // 외곽선 둘레 밝은 테두리(헤일로)를 없애려고 배경을 몇 픽셀 더 넓힌다
        private const float WarmMin = 0.14f;  // (R+G)/2 − B가 이보다 크면 노란 빛(불꽃)으로 보고 지킨다
        private const float PureWhite = 0.975f;  // 갇힌 흰 배경을 찾을 때: 이만큼 새하얀 픽셀이
        private const int EnclosedMinArea = 500;  // 이만큼 넓게 뭉쳐 있으면 배경

        public override uint GetVersion() => 3;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // 1402×1122처럼 4의 배수가 아닌 크기가 많아 압축하지 않는다
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(Folder)) return;
            var px = texture.GetPixels32();
            RemoveWhiteBackground(px, texture.width, texture.height);
            texture.SetPixels32(px);
        }

        /// <summary>테두리에 이어진 흰 배경을 투명하게(흰색 → 알파). 검사용 명령에서도 부른다.</summary>
        public static void RemoveWhiteBackground(Color32[] px, int w, int h)
        {
            int n = w * h;
            var lum = new float[n];
            var warm = new bool[n]; // 노랗게 빛나는 부분(불꽃 심지 등)은 흰 배경과 헷갈리지 않게 지킨다
            for (int i = 0; i < n; i++)
            {
                var c = px[i];
                lum[i] = Mathf.Min(c.r, Mathf.Min(c.g, c.b)) / 255f;
                warm[i] = (c.r + c.g) * 0.5f - c.b > WarmMin * 255f;
            }

            var bg = new bool[n];
            var queue = new int[n];
            int head = 0, tail = 0;
            void Seed(int i)
            {
                if (!bg[i] && !warm[i] && lum[i] >= SeedMin) { bg[i] = true; queue[tail++] = i; }
            }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            SeedEnclosedWhite(lum, warm, w, h, Seed);

            void Spread(int from, int j)
            {
                if (bg[j] || warm[j] || lum[j] < FloodMin || Mathf.Abs(lum[j] - lum[from]) > MaxStep) return;
                bg[j] = true;
                queue[tail++] = j;
            }
            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                if (x > 0) Spread(i, i - 1);
                if (x < w - 1) Spread(i, i + 1);
                if (y > 0) Spread(i, i - w);
                if (y < h - 1) Spread(i, i + w);
            }

            // 외곽선 바로 옆의 밝은 픽셀(안티에일리어싱)도 배경에 넣는다.
            for (int g = 0; g < EdgeGrow; g++)
            {
                var grown = (bool[])bg.Clone();
                for (int i = 0; i < n; i++)
                {
                    if (bg[i] || warm[i] || lum[i] < FloodMin) continue;
                    int x = i % w, y = i / w;
                    if ((x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]))
                        grown[i] = true;
                }
                bg = grown;
            }

            for (int i = 0; i < n; i++)
            {
                if (!bg[i]) continue;
                // 흰색 위에 그려진 색으로 보고 알파를 되찾는다: c = a·f + (1 − a)·흰색.
                float strength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FloodMin, FullMin, lum[i]));
                float a = Mathf.Lerp(1f, 1f - lum[i], strength);
                var c = px[i];
                if (a <= 0.004f) { px[i] = new Color32(255, 255, 255, 0); continue; }
                byte Un(byte v) => (byte)Mathf.RoundToInt(Mathf.Clamp01((v / 255f - (1f - a)) / a) * 255f);
                px[i] = new Color32(Un(c.r), Un(c.g), Un(c.b), (byte)Mathf.RoundToInt(a * c.a));
            }
        }

        /// <summary>리본 고리·팔과 몸 사이처럼 테두리와 이어지지 않은 흰 배경. 아주 흰 픽셀이 넓게 뭉친 곳만 배경으로 본다(얼굴·옷의 흰 부분은 이만큼 새하얗고 넓지 않다).</summary>
        private static void SeedEnclosedWhite(float[] lum, bool[] warm, int w, int h, System.Action<int> seed)
        {
            int n = w * h;
            var seen = new bool[n];
            var region = new List<int>();
            var stack = new Stack<int>();
            for (int s = 0; s < n; s++)
            {
                if (seen[s] || warm[s] || lum[s] < PureWhite) continue;
                region.Clear();
                seen[s] = true;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    region.Add(i);
                    int x = i % w, y = i / w;
                    void Visit(int j)
                    {
                        if (seen[j] || warm[j] || lum[j] < PureWhite) return;
                        seen[j] = true;
                        stack.Push(j);
                    }
                    if (x > 0) Visit(i - 1);
                    if (x < w - 1) Visit(i + 1);
                    if (y > 0) Visit(i - w);
                    if (y < h - 1) Visit(i + w);
                }
                if (region.Count >= EnclosedMinArea)
                    foreach (int i in region) seed(i);
            }
        }
    }
}

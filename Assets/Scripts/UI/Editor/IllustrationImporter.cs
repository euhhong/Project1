using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.UI.Editor
{
    /// <summary>
    /// 캐릭터 일러스트(Assets/UI/Resources/Illustrations) 가져오기 설정 + 흰 배경 빼기.
    /// 흰 배경 PNG가 들어오면 테두리에 이어진 흰 배경을 투명하게 뺀 PNG로 파일을 바꿔 저장한다(어두운 선택 화면 위에 바로 얹으려고).
    /// 연기·나비처럼 흰색으로 옅어지는 부분은 반투명으로 남고, 캐릭터 안의 흰 부분(얼굴·앞치마)은 외곽선에 막혀 그대로다.
    /// </summary>
    public class IllustrationImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/UI/Resources/Illustrations/";

        private static float SeedMin = 0.9f;  // 테두리에서 이 정도로 흰 픽셀부터 배경으로 본다
        private static bool EraseFlooded; // true = 번진 배경을 색 없이 지움(StrongWhite 그림)
        private static float FloodMin = 0.45f; // 배경은 이보다 밝은 픽셀로만 번진다
        private static float FullMin = 0.8f;  // 이보다 밝으면 흰색을 완전히 뺀다(그 사이는 조금만)
        private static float MaxStep = 0.12f; // 이웃과 밝기 차가 이보다 크면(외곽선) 번지지 않는다
        private const int EdgeGrow = 2;       // 외곽선 둘레 밝은 테두리(헤일로)를 없애려고 배경을 몇 픽셀 더 넓힌다
        private const float WarmMin = 0.14f;  // (R+G)/2 − B가 이보다 크면 노란 빛(불꽃)으로 보고 지킨다
        private const float SkinMin = 0.03f;  // R이 G·B보다 이만큼 크면(분홍기 도는 살색) 배경으로 번지지 않는다
        // 얼굴이 아주 창백해 흰 배경과 거의 이어진 그림 — 살색 지키기를 켠다(분홍 연기·붉은 리본이 있는 그림에 켜면 그쪽이 덜 빠지므로 필요한 그림만).
        private static readonly HashSet<string> SkinProtectIds = new() { "Survivor_Doctor" };
        // 갇힌 흰 배경으로 볼 크기(픽셀 수)를 그림마다 다르게 — 사냥터지기: 흰 셔츠(넓음)는 남기고 쇠사슬 고리 속 작은 흰 구멍만 뺀다.
        private static readonly Dictionary<string, (int min, int max)> EnclosedRanges = new() { ["Hunter_Gamekeeper"] = (20, int.MaxValue), ["Survivor_Cowboy"] = (200, int.MaxValue), ["Survivor_Mercenary"] = (80, int.MaxValue) };
        // 흰 배경 빼기를 더 세게(그림마다): 카우보이 — 둘레에 날리는 옅은 흰/분홍 천 조각 효과까지 배경으로 빼 버린다(사용자 요청).
        private static readonly Dictionary<string, (float flood, float full, float step)> StrongWhite = new() { ["Survivor_Cowboy"] = (0.3f, 0.62f, 0.3f) };
        // 지킬 칸(그림 위쪽 기준 픽셀 x, y, 너비, 높이) — 사냥터지기: 이마(뿔 사이 흰 털)와 가슴의 흰 셔츠(목깃부터 허리띠 위까지).
        private static readonly Dictionary<string, RectInt[]> ProtectRects = new() { ["Hunter_Gamekeeper"] = new[] { new RectInt(452, 198, 150, 132), new RectInt(464, 330, 106, 60), new RectInt(410, 390, 300, 250) },
            // 검은 배경 그림: 배경에 붙은 검은 옷(칸 안에서는 새까만 배경만 빠짐)
            ["Hunter_Ripper"] = new[] { new RectInt(340, 20, 210, 270), new RectInt(330, 230, 480, 330), new RectInt(300, 560, 600, 562), new RectInt(810, 262, 400, 150) },
            ["Survivor_MindsEye"] = new[] { new RectInt(420, 200, 490, 380), new RectInt(630, 540, 250, 200) }, // 머리카락·고글·목도리 매듭의 어두운 그늘
        };
        private const float PureWhite = 0.975f;  // 갇힌 흰 배경을 찾을 때: 이만큼 새하얀 픽셀이
        private const int EnclosedMinArea = 2000; // 이만큼 넓게 뭉쳐 있으면 배경
        private static float ProtectWhite = 0.99f; // 지킬 칸 안에서도 이만큼 새하얀 픽셀은 배경으로 본다

        public override uint GetVersion() => 6;

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

        // 가져올 때 픽셀만 바꾸면 Unity가 원본(불투명)을 보고 알파 없는 형식(RGB24)으로 굳혀 버린다.
        // 그래서 흰 배경이 남은 PNG는 파일 자체를 누끼 딴 PNG로 바꿔 저장한다(한 번만 — 이미 투명하면 건드리지 않는다).
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var targets = new List<string>();
            foreach (var p in imported)
                if (p.StartsWith(Folder) && p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) targets.Add(p);
            if (targets.Count > 0) EditorApplication.delayCall += () => { foreach (var p in targets) BakeIfWhite(p); };
        }

        [MenuItem("Tools/FE/일러스트 흰 배경 빼기")]
        private static void BakeAll()
        {
            int n = 0;
            foreach (var p in System.IO.Directory.GetFiles(Folder, "*.png"))
                if (BakeIfWhite(p.Replace('\\', '/'))) n++;
            Debug.Log($"[FE] 일러스트 흰 배경 빼기: {n}장 바꿈");
        }

        /// <summary>테두리가 흰 불투명 배경이면 누끼를 따서 PNG를 덮어쓴다. 바꿨으면 true.</summary>
        public static bool BakeIfWhite(string path)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(bytes)) return false;
                var px = tex.GetPixels32();
                bool white = HasPlainBorder(px, tex.width, tex.height, true), black = !white && HasPlainBorder(px, tex.width, tex.height, false);
                if (!white && !black) return false;
                string id = System.IO.Path.GetFileNameWithoutExtension(path);
                if (black)
                {
                    RemoveBlackBackground(px, tex.width, tex.height, ProtectRects.TryGetValue(id, out var keepDark) ? keepDark : null);
                    tex.SetPixels32(px);
                    System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    Debug.Log($"[FE] 일러스트 검은 배경을 뺐습니다: {path}");
                    return true;
                }
                var (encMin, encMax) = EnclosedRanges.TryGetValue(id, out var range) ? range : (EnclosedMinArea, int.MaxValue);
                float flood0 = FloodMin, full0 = FullMin, step0 = MaxStep;
                if (StrongWhite.TryGetValue(id, out var strong)) { FloodMin = strong.flood; FullMin = strong.full; MaxStep = strong.step; EraseFlooded = true; }
                try { RemoveWhiteBackground(px, tex.width, tex.height, SkinProtectIds.Contains(id), encMin, encMax, ProtectRects.TryGetValue(id, out var keep) ? keep : null); }
                finally { FloodMin = flood0; FullMin = full0; MaxStep = step0; EraseFlooded = false; }
                tex.SetPixels32(px);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[FE] 일러스트 흰 배경을 뺐습니다: {path}");
                return true;
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>테두리가 불투명한 흰색(white = true) 또는 검은색 배경인지.</summary>
        private static bool HasPlainBorder(Color32[] px, int w, int h, bool whiteBg)
        {
            int white = 0, total = 0;
            void Check(Color32 c)
            {
                total++;
                if (c.a == 255 && (whiteBg ? c.r >= 230 && c.g >= 230 && c.b >= 230 : c.r <= 25 && c.g <= 25 && c.b <= 25)) white++;
            }
            for (int x = 0; x < w; x += 4) { Check(px[x]); Check(px[(h - 1) * w + x]); }
            for (int y = 0; y < h; y += 4) { Check(px[y * w]); Check(px[y * w + w - 1]); }
            return white > total * 0.3f;
        }

        /// <summary>테두리에 이어진 흰 배경을 투명하게(흰색 → 알파). 검사용 명령에서도 부른다.</summary>
        public static void RemoveWhiteBackground(Color32[] px, int w, int h, bool protectSkin = false, int enclosedMin = EnclosedMinArea, int enclosedMax = int.MaxValue, RectInt[] protect = null)
        {
            int n = w * h;
            var lum = new float[n];
            var warm = new bool[n]; // 노랗게 빛나는 부분(불꽃 심지 등)은 흰 배경과 헷갈리지 않게 지킨다
            for (int i = 0; i < n; i++)
            {
                var c = px[i];
                lum[i] = Mathf.Min(c.r, Mathf.Min(c.g, c.b)) / 255f;
                warm[i] = (c.r + c.g) * 0.5f - c.b > WarmMin * 255f
                       || (protectSkin && c.r - Mathf.Max(c.g, c.b) >= SkinMin * 255f && lum[i] < PureWhite); // 창백한 얼굴이 흰 배경과 이어진 그림만(SkinProtectIds): 분홍기 도는 살색을 지킨다
            }

            // 지킬 칸(그림 위쪽 기준 픽셀 좌표): 흰 옷·흰 털처럼 흰 배경과 이어져 빠지면 안 되는 곳.
            // 칸 안에서는 새하얀(ProtectWhite 이상) 배경만 번져 들어가고, 옅은 흰색(옷·털)은 막는다 — 칸 모양이 네모로 남지 않게.
            var prot = new bool[n];
            if (protect != null)
                foreach (var r in protect)
                    for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(h, r.yMax); y++)
                        for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(w, r.xMax); x++)
                            prot[(h - 1 - y) * w + x] = true;
            bool Guarded(int i) => warm[i] || (prot[i] && lum[i] < ProtectWhite);
            var noEnclosed = new bool[n];
            for (int i = 0; i < n; i++) noEnclosed[i] = warm[i] || prot[i];

            var bg = new bool[n];
            var queue = new int[n];
            int head = 0, tail = 0;
            void Seed(int i)
            {
                if (!bg[i] && !Guarded(i) && lum[i] >= SeedMin) { bg[i] = true; queue[tail++] = i; }
            }
            for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
            SeedEnclosedWhite(lum, noEnclosed, w, h, Seed, enclosedMin, enclosedMax);

            void Spread(int from, int j)
            {
                if (bg[j] || Guarded(j) || lum[j] < FloodMin || Mathf.Abs(lum[j] - lum[from]) > MaxStep) return;
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
                if (EraseFlooded)
                {
                    // 번진 곳은 색을 남기지 않고 지운다(옅은 천 조각 효과까지) — 어두운 경계만 살짝 부드럽게
                    a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FloodMin, FloodMin + 0.06f, lum[i]));
                    if (a <= 0.004f) { px[i] = new Color32(255, 255, 255, 0); continue; }
                    px[i] = new Color32(c.r, c.g, c.b, (byte)Mathf.RoundToInt(a * c.a));
                    continue;
                }
                if (a <= 0.004f) { px[i] = new Color32(255, 255, 255, 0); continue; }
                byte Un(byte v) => (byte)Mathf.RoundToInt(Mathf.Clamp01((v / 255f - (1f - a)) / a) * 255f);
                px[i] = new Color32(Un(c.r), Un(c.g), Un(c.b), (byte)Mathf.RoundToInt(a * c.a));
            }
        }

        /// <summary>
        /// 검은 배경 빼기: 색을 뒤집어 흰 배경 빼기와 같은 방식으로 뺀 뒤 다시 뒤집는다(검정 → 투명, 빛나는 효과는 반투명으로 남음).
        /// 검은 옷이 배경과 섞이기 쉬워 기준을 더 엄하게: 거의 새까만 픽셀에서만 시작하고, 어두운 회색 이상은 번지지 않는다. 넓게 갇힌 새까만 틈(머리카락 사이·고리 속)도 뺀다. 배경에 붙은 검은 옷은 ProtectRects로 지킨다.
        /// </summary>
        public static void RemoveBlackBackground(Color32[] px, int w, int h, RectInt[] protect = null)
        {
            for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = new Color32((byte)(255 - c.r), (byte)(255 - c.g), (byte)(255 - c.b), c.a); }
            float seed = SeedMin, flood = FloodMin, full = FullMin, step = MaxStep, keep = ProtectWhite;
            try
            {
                SeedMin = 0.95f; FloodMin = 0.8f; FullMin = 0.93f; MaxStep = 0.05f; ProtectWhite = 0.965f; // 압축 잡티가 낀 검은 배경(RGB 9 이하)도 칸 안에서 빠지게
                RemoveWhiteBackground(px, w, h, false, EnclosedMinArea, int.MaxValue, protect);
            }
            finally { SeedMin = seed; FloodMin = flood; FullMin = full; MaxStep = step; ProtectWhite = keep; }
            for (int i = 0; i < px.Length; i++) { var c = px[i]; px[i] = c.a == 0 ? new Color32(0, 0, 0, 0) : new Color32((byte)(255 - c.r), (byte)(255 - c.g), (byte)(255 - c.b), c.a); }
        }

        /// <summary>리본 고리·팔과 몸 사이처럼 테두리와 이어지지 않은 흰 배경. 아주 흰 픽셀이 넓게 뭉친 곳만 배경으로 본다(얼굴·옷의 흰 부분은 이만큼 새하얗고 넓지 않다).</summary>
        private static void SeedEnclosedWhite(float[] lum, bool[] warm, int w, int h, System.Action<int> seed, int minArea, int maxArea)
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
                if (region.Count < minArea || region.Count > maxArea) continue;
                // 둘레가 노란 빛(불꽃)이면 배경이 아니라 빛나는 심지다 — 남긴다.
                int edge = 0, warmEdge = 0;
                foreach (int i in region)
                {
                    int x = i % w, y = i / w;
                    void Edge(int j)
                    {
                        if (lum[j] >= PureWhite && !warm[j]) return; // 같은 흰 덩어리
                        edge++;
                        if (warm[j]) warmEdge++;
                    }
                    if (x > 0) Edge(i - 1);
                    if (x < w - 1) Edge(i + 1);
                    if (y > 0) Edge(i - w);
                    if (y < h - 1) Edge(i + w);
                }
                if (edge > 0 && warmEdge > edge * 0.25f) continue;
                foreach (int i in region) seed(i);
            }
        }
    }
}

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 바닥 지형(Terrain)에 풀 무늬를 칠한다(BG) — 메뉴 Tools/BG/땅에 풀 무늬 넣기.
    /// - 마른 풀 레이어(TL_Grass): 전체에 옅게(BaseGrass) + 무늬 둘레에 조금 더.
    /// - 푸른 풀 레이어(TL_GrassGreen — VARCO로 만든 흙에 짧은 풀이 박힌 텍스처, 없으면 만든다): 큰 노이즈 무늬 한가운데에 군데군데.
    /// - 잔 노이즈로 가장자리를 흐트러뜨리고, 콘크리트 길(TL_Concrete가 많은 곳)은 덜 칠한다.
    /// - 여러 번 실행해도 같은 결과(풀 비중 = max(지금, 목표), 나머지 레이어는 비율대로 줄임).
    /// 지형 데이터(FactoryYard_TerrainData.asset)를 직접 고친다.
    /// </summary>
    public static class TerrainGrassPainter
    {
        private const float BaseGrass = 0.12f, RimGrass = 0.25f, GreenCore = 0.85f;
        private const string GreenLayerPath = "Assets/Environment/Materials/TerrainLayers/TL_GrassGreen.terrainlayer";
        private const string GreenTexPath = "Assets/Environment/Textures/Ground_GrassGreen.png";

        [MenuItem("Tools/BG/땅에 풀 무늬 넣기")]
        public static void Paint()
        {
            var ter = Object.FindAnyObjectByType<Terrain>();
            if (ter == null) { Debug.LogWarning("[BG] 지형(Terrain)이 없습니다."); return; }
            var d = ter.terrainData;
            Undo.RegisterCompleteObjectUndo(d, "땅에 풀 무늬 넣기");
            EnsureGreenLayer(d);
            TintBase(d); // 맨 바닥 흙에도 아주 약간 녹색
            int grass = Index(d, "TL_Grass"), green = Index(d, "TL_GrassGreen"), concrete = Index(d, "TL_Concrete");
            if (grass < 0) { Debug.LogWarning("[BG] 지형에 풀 레이어가 없습니다."); return; }
            int res = d.alphamapResolution, layers = d.alphamapLayers;
            var a = d.GetAlphamaps(0, 0, res, res);
            Vector3 origin = ter.transform.position, size = d.size;
            int changed = 0;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float wx = origin.x + (x + 0.5f) / res * size.x, wz = origin.z + (y + 0.5f) / res * size.z;
                    float big = Mathf.PerlinNoise(wx * 0.07f + 13.1f, wz * 0.07f + 7.7f);
                    float mid = Mathf.PerlinNoise(wx * 0.19f + 3.3f, wz * 0.19f + 21.9f);
                    float fine = Mathf.PerlinNoise(wx * 0.9f + 41f, wz * 0.9f + 5f);
                    float n = big * 0.7f + mid * 0.3f + (fine - 0.5f) * 0.12f;
                    float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.66f, n));   // 무늬 둘레(넓게)
                    float core = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.47f, 0.68f, n));  // 무늬 한가운데
                    float less = concrete >= 0 ? Mathf.Lerp(1f, 0.3f, a[y, x, concrete]) : 1f; // 콘크리트 길은 덜
                    float tGrass = (BaseGrass + rim * RimGrass) * less;
                    float tGreen = green >= 0 ? core * GreenCore * less : 0f;
                    float g1 = Mathf.Max(a[y, x, grass], tGrass), g2 = green >= 0 ? Mathf.Max(a[y, x, green], tGreen) : 0f;
                    if (g1 + g2 > 1f) { float s = 1f / (g1 + g2); g1 *= s; g2 *= s; }
                    float restOld = 0f;
                    for (int l = 0; l < layers; l++) if (l != grass && l != green) restOld += a[y, x, l];
                    float restNew = 1f - g1 - g2;
                    bool diff = Mathf.Abs(g1 - a[y, x, grass]) > 1e-4f || (green >= 0 && Mathf.Abs(g2 - a[y, x, green]) > 1e-4f);
                    if (!diff) continue;
                    for (int l = 0; l < layers; l++)
                        if (l != grass && l != green) a[y, x, l] = restOld > 1e-5f ? a[y, x, l] * (restNew / restOld) : 0f;
                    a[y, x, grass] = g1;
                    if (green >= 0) a[y, x, green] = g2;
                    changed++;
                }
            d.SetAlphamaps(0, 0, a);
            EditorUtility.SetDirty(d);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BG] 땅에 풀 무늬: {changed}/{res * res} 칸을 칠했습니다.");
        }

        /// <summary>바탕 흙 레이어(TL_Dirt)의 색을 아주 약간 녹색 쪽으로(빨강·파랑을 조금 낮춤).</summary>
        private static void TintBase(TerrainData d)
        {
            int i = Index(d, "TL_Dirt");
            if (i < 0) return;
            var l = d.terrainLayers[i];
            l.diffuseRemapMax = new Vector4(0.93f, 1f, 0.86f, 1f);
            EditorUtility.SetDirty(l);
        }

        private static int Index(TerrainData d, string name)
        {
            for (int i = 0; i < d.terrainLayers.Length; i++) if (d.terrainLayers[i] != null && d.terrainLayers[i].name == name) return i;
            return -1;
        }

        /// <summary>푸른 풀 레이어(TL_GrassGreen)가 지형에 없으면 만들어 붙인다(텍스처가 있을 때).</summary>
        private static void EnsureGreenLayer(TerrainData d)
        {
            if (Index(d, "TL_GrassGreen") >= 0) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GreenTexPath);
            if (tex == null) return;
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GreenLayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(4f, 4f), smoothness = 0f, metallic = 0f };
                AssetDatabase.CreateAsset(layer, GreenLayerPath);
            }
            d.terrainLayers = d.terrainLayers.Concat(new[] { layer }).ToArray();
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 맵 하늘·햇빛 맞추기. 메뉴: Tools/BG/하늘·햇빛 맞추기.
    /// - 하늘: 밝은 낮 하늘(위는 파랗고 지평선은 옅게, 구름) 파노라마 텍스처를 코드로 만들어 Skybox/Panoramic으로 깐다(이음매 없음).
    /// - 햇빛: 실내 바닥(위에 지붕·천장이 있는 바닥) 점을 맵 전체에서 뽑아, 해 방향 후보마다 그 점에서 해 쪽으로 광선을 쏴
    ///   막히지 않는(창틀·문으로 햇살이 들어오는) 점이 가장 많은 방향을 고른다. 너무 낮은 해(어두움)는 빼고 고른다.
    /// - 전체 분위기 밝게: 해 세기·색, 주변광(하늘·지평선·땅 세 색), 안개 색을 하늘에 맞춰 밝힌다.
    /// 여러 번 실행해도 같은 결과(하늘 텍스처는 있으면 다시 만들지 않음 — 지우면 새로 만듦).
    /// </summary>
    public static class MapSkyAndSun
    {
        private const string Folder = "Assets/Environment/Sky";
        private const string TexPath = Folder + "/Sky_Day.png";
        private const string MatPath = Folder + "/Sky_Day.mat";

        private static readonly Color Zenith = new(0.30f, 0.52f, 0.86f);
        private static readonly Color Horizon = new(0.82f, 0.87f, 0.92f);
        private static readonly Color Ground = new(0.55f, 0.56f, 0.55f);

        [MenuItem("Tools/BG/하늘·햇빛 맞추기")]
        public static void Menu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlaying) return "MapSkyAndSun: Play 중에는 못 합니다.";
            var scene = SceneManager.GetActiveScene();

            // 하늘
            var mat = EnsureSkyMaterial();
            RenderSettings.skybox = mat;

            // 주변광·안개: 밝은 낮
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.80f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.77f, 0.73f);
            RenderSettings.ambientGroundColor = new Color(0.52f, 0.49f, 0.44f);
            BrightenExposure();
            RenderSettings.fogColor = Color.Lerp(Horizon, new Color(0.75f, 0.76f, 0.76f), 0.4f);

            // 해: 창틀로 햇살이 가장 많이 드는 방향
            var sun = FindSun();
            string sunInfo = "해(방향 빛) 없음";
            if (sun != null)
            {
                var (dir, lit, total) = BestSunDirection();
                sun.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                sun.color = new Color(1f, 0.95f, 0.86f); // 따뜻한 낮 햇빛
                sun.intensity = 2.6f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.8f; // 그늘도 너무 새까맣지 않게
                float elev = Mathf.Asin(dir.y) * Mathf.Rad2Deg, azim = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                sunInfo = $"해 높이 {elev:0}°, 방위 {azim:0}° — 실내 바닥 {total}곳 중 {lit}곳에 햇살";
                EditorUtility.SetDirty(sun);
            }

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            return $"MapSkyAndSun: 하늘 = {mat.name}, {sunInfo}";
        }

        /// <summary>씬 전체 볼륨에 색 조정(노출 조금 올림)을 넣는다. 이미 있으면 값만 맞춘다.</summary>
        private static void BrightenExposure()
        {
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            {
                if (!v.isGlobal || v.sharedProfile == null || v.gameObject.scene != SceneManager.GetActiveScene()) continue;
                var profile = v.sharedProfile;
                if (!profile.TryGet(out ColorAdjustments ca))
                {
                    ca = profile.Add<ColorAdjustments>(true);
                    AssetDatabase.AddObjectToAsset(ca, profile);
                }
                ca.active = true;
                ca.postExposure.Override(0.35f);
                ca.contrast.Override(-5f);
                ca.saturation.Override(5f);
                EditorUtility.SetDirty(ca);
                EditorUtility.SetDirty(profile);
            }
        }

        private static Light FindSun()
        {
            Light best = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.gameObject.scene == SceneManager.GetActiveScene() && (best == null || l.intensity > best.intensity)) best = l;
            return best;
        }

        // ───────── 해 방향 고르기 ─────────

        private static (Vector3 dir, int lit, int total) BestSunDirection()
        {
            Physics.SyncTransforms();
            var points = IndoorFloorPoints();
            Vector3 best = new Vector3(0.5f, 0.6f, 0.6f).normalized;
            int bestLit = -1;
            for (float elev = 24f; elev <= 46f; elev += 3f) // 너무 낮은 해는 바깥이 그늘투성이라 뺀다
                for (float azim = 0f; azim < 360f; azim += 7.5f)
                {
                    float e = elev * Mathf.Deg2Rad, a = azim * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
                    int lit = 0;
                    foreach (var p in points)
                        if (!Physics.Raycast(p, d, SunRayLength, ~0, QueryTriggerInteraction.Ignore)) lit++; // 건물 밖으로 나갈 만큼만(먼 담장·나무에 가리는 건 안 셈)
                    // 같으면 높은 해(더 밝음)
                    if (lit > bestLit || (lit == bestLit && d.y > best.y)) { bestLit = lit; best = d; }
                }
            return (best, bestLit, points.Count);
        }

        private const float SunRayLength = 30f;

        /// <summary>위에 지붕·천장이 있는 바닥 바로 위 점(햇살이 바닥에 비치는 자리). 맵 위에서 1.5m 간격으로 아래로 쏴 맞는 윗면들(층마다)을 모은다.</summary>
        private static List<Vector3> IndoorFloorPoints()
        {
            var points = new List<Vector3>();
            var bounds = MapBounds();
            for (float x = bounds.min.x; x <= bounds.max.x; x += 1.5f)
                for (float z = bounds.min.z; z <= bounds.max.z; z += 1.5f)
                {
                    var hits = Physics.RaycastAll(new Vector3(x, bounds.max.y + 5f, z), Vector3.down, bounds.size.y + 10f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in hits)
                    {
                        if (h.normal.y < 0.8f) continue; // 윗면(바닥)만
                        var p = h.point + Vector3.up * 0.15f;
                        if (Physics.Raycast(p, Vector3.up, 14f, ~0, QueryTriggerInteraction.Ignore)) points.Add(p); // 위가 막혀 있으면 실내
                    }
                }
            return points;
        }

        private static Bounds MapBounds()
        {
            var b = new Bounds(Vector3.zero, Vector3.one * 100f);
            bool first = true;
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c.isTrigger || c.gameObject.scene != SceneManager.GetActiveScene()) continue;
                if (c.bounds.size.magnitude > 400f) continue; // 거대한 지형 등은 빼고
                if (first) { b = c.bounds; first = false; } else b.Encapsulate(c.bounds);
            }
            return b;
        }

        // ───────── 하늘 텍스처 ─────────

        private static Material EnsureSkyMaterial()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Environment", "Sky");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath) == null)
            {
                System.IO.File.WriteAllBytes(TexPath, MakeSky(2048, 1024).EncodeToPNG());
                AssetDatabase.ImportAsset(TexPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
                ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic")) { name = "Sky_Day" };
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
            mat.SetFloat("_Mapping", 1f);       // 위도·경도(파노라마)
            mat.SetFloat("_ImageType", 0f);     // 360도
            mat.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++) { sum += Mathf.PerlinNoise(x, y) * amp; norm += amp; x *= 2.02f; y *= 2.02f; amp *= 0.5f; }
            return sum / norm;
        }

        /// <summary>위도·경도 파노라마(가로 = 경도 360°, 세로 = 아래 −90° ~ 위 +90°). 가로 이음매가 없도록 구름 무늬를 원을 따라 뽑는다.</summary>
        private static Texture2D MakeSky(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float lat = (y + 0.5f) / h * 2f - 1f; // −1(아래) ~ 1(위)
                for (int x = 0; x < w; x++)
                {
                    float lon = (x + 0.5f) / w * Mathf.PI * 2f;
                    Color c;
                    if (lat < 0f)
                    {
                        c = Color.Lerp(Horizon, Ground, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(-lat * 4f)));
                    }
                    else
                    {
                        float t = Mathf.Pow(Mathf.Clamp01(lat), 0.55f);
                        c = Color.Lerp(Horizon, Zenith, t);
                        // 구름: 지평선 위 낮은 하늘에 많고 높이 갈수록 옅게. 원을 따라 뽑아 가로 이음매 없음.
                        float r = 3.2f / Mathf.Max(0.18f, lat + 0.12f); // 위로 갈수록 구름이 커 보이게
                        float nx = Mathf.Cos(lon) * r + 50f, ny = Mathf.Sin(lon) * r + 50f;
                        float n = Fbm(nx + lat * 6f, ny - lat * 4f, 5);
                        float band = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.0f, 0.12f, lat)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.9f, lat)));
                        float cloud = Mathf.Clamp01((n - 0.5f) * 3.2f) * band;
                        var cloudColor = Color.Lerp(new Color(0.98f, 0.98f, 0.97f), new Color(0.78f, 0.80f, 0.84f), Mathf.Clamp01((n - 0.6f) * 3f)); // 두꺼운 곳은 아래가 조금 그늘
                        c = Color.Lerp(c, cloudColor, cloud * 0.85f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

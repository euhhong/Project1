using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 분신 겉모습(공장장 분신): 모델의 모든 재질을 "온몸이 검고, 용암 같은 붉은 균열이 군데군데 가서 타오르는" 재질로 바꾼다.
    /// - 바탕: 거의 검은 색(살짝 거친 숯 느낌).
    /// - 균열: 코드로 만든 금 간 무늬 텍스처(보로노이 경계, 군데군데만 — 큰 얼룩 무늬로 가림)를 발광 맵으로. 붉은 주황빛.
    /// - 타오름: 균열 빛이 천천히 일렁인다(LateUpdate에서 발광 세기를 흔듦).
    /// 모델 루트(또는 캐릭터 루트)에 붙인다. 붙이면 바로 바꾸고, 떼면 원래 재질로 돌린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LavaCloneLook : MonoBehaviour
    {
        [SerializeField] private Color bodyColor = new(0.025f, 0.022f, 0.022f);
        [SerializeField] private Color crackColor = new(1f, 0.28f, 0.06f);
        [Tooltip("균열 발광 세기(평균) / 일렁이는 폭 / 빠르기.")]
        [SerializeField, Min(0f)] private float glow = 3.2f, flicker = 0.9f, flickerSpeed = 2.3f;

        private static Texture2D crackTexture;
        private Material material;
        private readonly List<(Renderer r, Material[] mats)> original = new();

        /// <summary>이 오브젝트 아래 렌더러에 분신 재질을 입힌다. skip에 든 렌더러는 그대로 둔다(예: 분신 무기).</summary>
        public void Apply(ICollection<Renderer> skip = null)
        {
            Restore();
            if (material == null) material = Build();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || (skip != null && skip.Contains(r))) continue;
                original.Add((r, r.sharedMaterials));
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = material;
                r.sharedMaterials = mats;
            }
        }

        private void Start() { if (original.Count == 0) Apply(); }

        private void Restore()
        {
            foreach (var (r, mats) in original) if (r != null) r.sharedMaterials = mats;
            original.Clear();
        }

        private void OnDestroy()
        {
            Restore();
            if (material != null) Destroy(material);
        }

        private void LateUpdate()
        {
            if (material == null) return;
            float t = Time.time * flickerSpeed;
            float k = glow + flicker * (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            material.SetColor("_EmissionColor", crackColor * Mathf.Max(0.2f, k));
        }

        private Material Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "LavaClone" };
            m.SetColor("_BaseColor", bodyColor);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetTexture("_EmissionMap", CrackTexture());
            m.SetColor("_EmissionColor", crackColor * glow);
            return m;
        }

        /// <summary>
        /// 금 간 무늬(이어 붙여도 이음매 없음): 보로노이 칸의 경계(F2 - F1이 작은 곳)가 균열. 큰 얼룩 무늬가 밝은 곳에만 남겨 "군데군데".
        /// 균열 가운데는 노랗게 뜨겁고 가장자리로 갈수록 붉게 식는다(발광 색과 곱해짐).
        /// </summary>
        public static Texture2D CrackTexture()
        {
            if (crackTexture != null) return crackTexture;
            const int n = 512, cells = 10;
            var rng = new System.Random(5150);
            var pts = new Vector2[cells, cells];
            for (int y = 0; y < cells; y++)
                for (int x = 0; x < cells; x++)
                    pts[x, y] = new Vector2(x + (float)rng.NextDouble(), y + (float)rng.NextDouble());
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) / n * cells;
                    float f1 = 9f, f2 = 9f;
                    int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y);
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy;
                            Vector2 q = pts[((gx % cells) + cells) % cells, ((gy % cells) + cells) % cells] + new Vector2(gx - (((gx % cells) + cells) % cells), gy - (((gy % cells) + cells) % cells));
                            float d = (q - p).magnitude;
                            if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                        }
                    float edge = f2 - f1;
                    // 균열 굵기를 들쭉날쭉하게, 큰 얼룩으로 군데군데만(이음매 없게 원 둘레로 샘플)
                    float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
                    float patch = Mathf.PerlinNoise(3f + Mathf.Cos(u) * 1.4f + Mathf.Sin(v) * 0.6f, 7f + Mathf.Sin(u) * 1.4f + Mathf.Cos(v) * 1.1f);
                    float width = 0.06f + 0.05f * Mathf.PerlinNoise(Mathf.Cos(u) * 4f + 11f, Mathf.Sin(v) * 4f + 5f);
                    float crack = Mathf.Clamp01(1f - edge / width);
                    crack = crack * crack * (3f - 2f * crack);
                    float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, patch));
                    float a = crack * mask;
                    Color c = Color.Lerp(new Color(0.55f, 0.08f, 0.02f), new Color(1f, 0.85f, 0.45f), crack * crack) * a;
                    px[y * n + x] = new Color(c.r, c.g, c.b, 1f);
                }
            crackTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "LavaCracks", wrapMode = TextureWrapMode.Repeat };
            crackTexture.SetPixels(px);
            crackTexture.Apply();
            return crackTexture;
        }
    }
}

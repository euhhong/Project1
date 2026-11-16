using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 햇볕 화상 연출(FE): 지붕 높이에서 타는 동안(ISunBurn.Burning) 몸에서 하얀 연기가 피어오르고 주황 불티가 튀며,
    /// 몸 둘레에 따뜻한 빛이 일렁인다 — 햇볕에 지글지글 타는 느낌. 오래 탈수록(처음 2초) 점점 세진다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DisallowMultipleComponent]
    public class SunBurnVisual : MonoBehaviour
    {
        private ISunBurn burn;
        private ParticleSystem smoke, embers;
        private Light glow;
        private static Material smokeMat, emberMat;

        private void Start()
        {
            burn = GetComponent<ISunBurn>();
            Build();
        }

        private void Update()
        {
            if (burn == null) { burn = GetComponent<ISunBurn>(); if (burn == null) return; }
            bool on = burn.Burning;
            float k = on ? Mathf.Clamp01(burn.BurnTime / 2f) * 0.6f + 0.4f : 0f; // 0.4 → 1
            var se = smoke.emission; se.rateOverTime = 26f * k;
            var ee = embers.emission; ee.rateOverTime = 14f * k;
            if (on && !smoke.isPlaying) { smoke.Play(); embers.Play(); }
            glow.enabled = on;
            if (on) glow.intensity = (1.2f + Mathf.PerlinNoise(Time.time * 9f, 0.3f) * 1.3f) * k; // 지글지글 일렁임
        }

        private void Build()
        {
            var root = new GameObject("SunBurnFx").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0f, 1.0f, 0f);

            smoke = MakeSystem(root, "SunSmoke", SmokeMaterial());
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            m.startColor = new Color(0.92f, 0.9f, 0.86f, 0.45f);
            m.gravityModifier = -0.15f; // 위로 피어오름
            var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.6f, 1.6f, 0.4f);
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.2f));
            var col = smoke.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f), new GradientColorKey(new Color(0.6f, 0.58f, 0.56f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            embers = MakeSystem(root, "SunEmbers", EmberMaterial());
            var e = embers.main;
            e.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            e.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            e.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            e.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.3f));
            e.gravityModifier = -0.3f;
            var esh = embers.shape; esh.shapeType = ParticleSystemShapeType.Box; esh.scale = new Vector3(0.6f, 1.6f, 0.4f);
            var ecol = embers.colorOverLifetime; ecol.enabled = true;
            var eg = new Gradient();
            eg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.05f), 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            ecol.color = eg;

            var lg = new GameObject("SunGlow");
            lg.transform.SetParent(root, false);
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 3f; glow.color = new Color(1f, 0.6f, 0.25f); glow.shadows = LightShadows.None;
            glow.enabled = false;
        }

        private static ParticleSystem MakeSystem(Transform parent, string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200; main.playOnAwake = false;
            var em = ps.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        private static Material SmokeMaterial()
        {
            if (smokeMat == null) smokeMat = new Material(Shader.Find("Sprites/Default")) { name = "SunSmoke", mainTexture = MistBladeTrail.PuffTexture() };
            return smokeMat;
        }

        private static Material EmberMaterial()
        {
            if (emberMat == null) emberMat = new Material(Shader.Find("Sprites/Default")) { name = "SunEmber", mainTexture = MistBladeTrail.PuffTexture(), renderQueue = 3100 };
            return emberMat;
        }
    }
}

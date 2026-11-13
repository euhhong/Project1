using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 리퍼 안개 칼날이 지나간 자리의 안개(IMistTrail, BE MistTrailField)를 그린다.
    /// - 안개 덩이마다 칼날 모양(손톱 세 줄기) 안개 자국이 날아가던 방향·휘두른 방향 그대로 남는다(칼날이 지나간 궤적에 칼날이 다 남는 모습).
    ///   자국은 천천히 일렁이며 옅게 떠 있고, 사라질 때가 되면(마지막 fadeTime초) 흐려져 없어진다.
    /// - 그 아래로 땅까지 낮게 깔리는 안개(파티클)가 덩이가 남아 있는 동안 계속 피어오른다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 리퍼만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class MistTrailVisual : MonoBehaviour
    {
        private static readonly Color ImprintColor = new(0.82f, 0.84f, 0.9f, 1f);
        private static readonly Color FogColor = new(0.78f, 0.8f, 0.86f, 0.32f);

        [Tooltip("사라지기 전 흐려지는 시간(초).")]
        [SerializeField, Min(0.1f)] private float fadeTime = 1.2f;
        [Tooltip("칼날 자국의 진하기(0~1).")]
        [SerializeField, Range(0f, 1f)] private float imprintAlpha = 0.55f;
        [Tooltip("땅에 깔리는 안개를 덩이마다 1초에 몇 개 피울지.")]
        [SerializeField, Min(0f)] private float fogRate = 3f;

        private class Imprint { public Transform Root; public Material Mat; public float Born, Seed, Power; public Vector3 Base, Ground; }

        private IMistTrail trail;
        private readonly List<Imprint> imprints = new();
        private int seenAdded;
        private Mesh mesh;
        private ParticleSystem fog;
        private Material fogMat;
        private float fogAccumulator;

        private void LateUpdate()
        {
            trail ??= GetComponent<IMistTrail>();
            if (trail == null) return;
            if (mesh == null) mesh = MistBladeTrail.BuildClawMesh();
            if (fog == null) BuildFog();

            // 새로 생긴 덩이(맨 뒤에 붙는다) → 칼날 자국
            var points = trail.Points;
            int fresh = Mathf.Min(trail.TotalAdded - seenAdded, points.Count);
            for (int i = points.Count - fresh; i < points.Count; i++) AddImprint(points[i]);
            seenAdded = trail.TotalAdded;

            float now = Time.time, life = trail.Lifetime;
            for (int i = imprints.Count - 1; i >= 0; i--)
            {
                var im = imprints[i];
                float age = now - im.Born;
                if (age >= life || im.Root == null) { Remove(i); continue; }
                float fadeIn = Mathf.Clamp01(age / 0.15f);
                float fadeOut = Mathf.Clamp01((life - age) / fadeTime);
                var c = ImprintColor; c.a = imprintAlpha * Mathf.Lerp(0.6f, 1f, im.Power) * fadeIn * fadeOut;
                im.Mat.color = c;
                // 천천히 일렁이며 떠 있다
                im.Root.position = im.Base + Vector3.up * (Mathf.Sin(age * 1.3f + im.Seed) * 0.06f);
                im.Root.localScale = Vector3.one * (Mathf.Lerp(0.7f, 1f, im.Power) * (1f + age * 0.03f));
            }

            // 땅에 깔리는 안개
            fogAccumulator += Time.deltaTime * fogRate * imprints.Count;
            var ep = new ParticleSystem.EmitParams();
            while (fogAccumulator >= 1f && imprints.Count > 0)
            {
                fogAccumulator -= 1f;
                var im = imprints[Random.Range(0, imprints.Count)];
                float remain = life - (now - im.Born);
                if (remain < 0.5f) continue;
                Vector2 r = Random.insideUnitCircle * trail.Radius * 0.8f;
                ep.position = im.Ground + new Vector3(r.x, Random.Range(0.05f, 0.9f), r.y);
                ep.velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.02f, 0.12f), Random.Range(-0.15f, 0.15f));
                ep.startSize = Random.Range(0.8f, 1.6f);
                ep.startLifetime = Mathf.Min(remain, Random.Range(1.6f, 2.6f));
                fog.Emit(ep, 1);
            }
        }

        private void AddImprint(in MistTrailPoint p)
        {
            var go = new GameObject("MistTrailImprint");
            go.transform.SetPositionAndRotation(p.Position, Quaternion.LookRotation(p.Direction, Vector3.ProjectOnPlane(p.SwingAxis, p.Direction).sqrMagnitude > 1e-4f ? p.SwingAxis : Vector3.up));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default")) { name = "MistTrailImprint", renderQueue = 3090 };
            mat.color = new Color(ImprintColor.r, ImprintColor.g, ImprintColor.b, 0f);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 안개가 깔릴 땅(칼날 아래)
            Vector3 ground = p.Position;
            if (Physics.Raycast(p.Position, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) ground = hit.point;
            imprints.Add(new Imprint { Root = go.transform, Mat = mat, Born = p.Born, Seed = Random.Range(0f, 6.28f), Power = Mathf.Clamp01(p.Power > 0f ? p.Power : 1f), Base = p.Position, Ground = ground });
        }

        private void Remove(int i)
        {
            var im = imprints[i];
            if (im.Root != null) Destroy(im.Root.gameObject);
            if (im.Mat != null) Destroy(im.Mat);
            imprints.RemoveAt(i);
        }

        private void BuildFog()
        {
            var go = new GameObject("MistTrailFog");
            fog = go.AddComponent<ParticleSystem>();
            fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fog.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.startColor = FogColor;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = fog.emission; emission.enabled = false;
            var shape = fog.shape; shape.enabled = false;
            var size = fog.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));
            var col = fog.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = fog.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 0.4f;
            var rot = fog.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            fogMat = new Material(Shader.Find("Sprites/Default")) { name = "MistTrailFog", mainTexture = MistBladeTrail.PuffTexture() };
            r.sharedMaterial = fogMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fog.Play();
        }

        private void OnDisable()
        {
            for (int i = imprints.Count - 1; i >= 0; i--) Remove(i);
            if (trail != null) seenAdded = trail.TotalAdded; // 다시 켜지면 지난 덩이는 그리지 않는다
        }

        private void OnDestroy()
        {
            if (fog != null) Destroy(fog.gameObject);
            if (fogMat != null) Destroy(fogMat);
            if (mesh != null) Destroy(mesh);
        }
    }
}

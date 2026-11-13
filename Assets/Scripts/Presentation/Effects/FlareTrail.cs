using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 신호탄 하나의 연출: 붉게 빛나는 작은 머리 + 지나간 자리에 남는 붉은 뭉게뭉게 연기(자욱하지 않게) + 끝난 자리의 연기 퍼짐.
    /// 투사체(IProjectile, BE)의 자리를 따라간다. 처음엔 신호총 총구에서 시작해 짧게 BE 자리로 합쳐진다.
    /// FlareShotVisual이 투사체가 생길 때 만든다. 끝나면 연기가 다 흩어진 뒤 스스로 지워진다.
    /// </summary>
    public class FlareTrail : MonoBehaviour
    {
        private const float MuzzleBlend = 0.15f;   // 총구 → BE 자리로 합쳐지는 시간(초)
        private static readonly Color SmokeColor = new(0.85f, 0.12f, 0.1f, 0.42f);

        private IProjectile projectile;
        private Vector3 muzzleOffset;
        private float age;
        private bool ended;
        private Transform head;
        private ParticleSystem smoke;
        private Material headMat, smokeMat;
        private static Texture2D puffTexture;

        public static FlareTrail Create(IProjectile p, Vector3 muzzle)
        {
            var go = new GameObject("FlareTrail");
            go.transform.position = muzzle;
            var t = go.AddComponent<FlareTrail>();
            t.projectile = p;
            t.muzzleOffset = muzzle - p.Position;
            t.Build();
            return t;
        }

        private void Build()
        {
            // 머리: 작은 붉은 공(은은하게 빛남) + 약한 불빛
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "FlareHead";
            Destroy(h.GetComponent<Collider>());
            h.transform.SetParent(transform, false);
            h.transform.localScale = Vector3.one * 0.14f;
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            headMat = new Material(shader) { name = "FlareHead" };
            headMat.SetColor("_BaseColor", new Color(1f, 0.35f, 0.25f));
            headMat.EnableKeyword("_EMISSION");
            headMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.12f) * 1.5f);
            h.GetComponent<Renderer>().sharedMaterial = headMat;
            var light = h.AddComponent<Light>();
            light.type = LightType.Point; light.color = new Color(1f, 0.3f, 0.2f); light.range = 3f; light.intensity = 1.2f;
            head = h.transform;

            // 연기: 지나간 거리만큼 퍼프를 남긴다(월드 공간), 천천히 커지며 옅어진다
            var sgo = new GameObject("FlareSmoke");
            sgo.transform.SetParent(transform, false);
            smoke = sgo.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = SmokeColor;
            main.gravityModifier = -0.03f; // 아주 천천히 떠오름
            main.maxParticles = 400;
            var emission = smoke.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 6f; // 1m에 6개 — 자욱하지 않게
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;
            var size = smoke.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 2.0f));
            var col = smoke.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.2f, 0.15f), 0f), new GradientColorKey(new Color(0.55f, 0.1f, 0.1f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var noise = smoke.noise;
            noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.6f; // 뭉게뭉게 흔들림
            var rot = smoke.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var r = sgo.GetComponent<ParticleSystemRenderer>();
            smokeMat = new Material(Shader.Find("Sprites/Default")) { name = "FlareSmoke", mainTexture = PuffTexture() };
            r.sharedMaterial = smokeMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smoke.Play();
        }

        private void LateUpdate()
        {
            if (ended) return;
            age += Time.deltaTime;
            if (projectile == null || (projectile is Object o && o == null) || !projectile.Alive)
            {
                End();
                return;
            }
            // FixedUpdate 사이를 메워 부드럽게(지난 FixedUpdate 뒤 흐른 시간만큼 앞으로)
            float ahead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            Vector3 pos = projectile.Position + projectile.Direction * (projectile.Speed * ahead);
            pos += muzzleOffset * Mathf.Clamp01(1f - age / MuzzleBlend);
            transform.position = pos;
        }

        private void End()
        {
            ended = true;
            Vector3 point = projectile != null && !(projectile is Object o && o == null) ? projectile.EndPoint : transform.position;
            transform.position = point;
            if (head != null) head.gameObject.SetActive(false);
            var emission = smoke.emission;
            emission.rateOverDistance = 0f;
            // 끝난 자리: 연기가 둥글게 퍼진다
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < 14; i++)
            {
                ep.position = point;
                ep.velocity = Random.onUnitSphere * Random.Range(0.6f, 1.8f);
                ep.startSize = Random.Range(0.4f, 0.7f);
                ep.startLifetime = Random.Range(0.9f, 1.4f);
                smoke.Emit(ep, 1);
            }
            Destroy(gameObject, 2.2f);
        }

        private void OnDestroy()
        {
            if (headMat != null) Destroy(headMat);
            if (smokeMat != null) Destroy(smokeMat);
        }

        /// <summary>가장자리가 부드럽게 사라지는 둥근 연기 한 덩이 텍스처(한 번만 만든다).</summary>
        private static Texture2D PuffTexture()
        {
            if (puffTexture != null) return puffTexture;
            const int n = 64;
            puffTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SmokePuff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // 부드럽게
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            puffTexture.SetPixels(px);
            puffTexture.Apply();
            return puffTexture;
        }
    }
}

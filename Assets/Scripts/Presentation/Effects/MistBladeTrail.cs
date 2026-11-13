using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 리퍼 안개 칼날 하나의 연출: 손톱 모양 그대로(나란한 세 줄기, 끝이 뾰족하고 가운데가 앞으로 휜 초승달) 회색빛 안개 칼날 +
    /// 지나간 자리에 흩어지는 안개 + 끝난 자리에서 칼날이 안개로 부서져 흩어짐.
    /// 칼날은 휘두른 방향(swingAxis, 날아가는 방향에 수직)으로 세운다 — 내려치기면 세로로 선다.
    /// 세기(IProjectile.Power)가 약하면(E, 1칸) 작고 옅다.
    /// 투사체(IProjectile, BE)의 자리를 따라간다. 처음엔 손톱 자리에서 시작해 짧게 BE 자리로 합쳐진다.
    /// MistClawVisual이 투사체가 생길 때 만든다. 끝나면 안개가 다 흩어진 뒤 스스로 지워진다.
    /// </summary>
    public class MistBladeTrail : MonoBehaviour
    {
        private const float StartBlend = 0.12f;   // 손톱 → BE 자리로 합쳐지는 시간(초)
        private const int Claws = 3, Segments = 14;
        private static readonly Color BladeColor = new(0.86f, 0.88f, 0.93f, 0.9f);
        private static readonly Color MistColor = new(0.8f, 0.82f, 0.87f, 0.5f);
        private static Texture2D puffTexture;

        private IProjectile projectile;
        private Vector3 startOffset, swingAxis;
        private float age, scale, strength;
        private bool ended;
        private Transform blade;
        private MeshRenderer bladeRenderer;
        private Mesh mesh;
        private ParticleSystem mist;
        private Material bladeMat, mistMat;

        public static MistBladeTrail Create(IProjectile p, Vector3 start, Vector3 swingAxis)
        {
            var go = new GameObject("MistBladeTrail");
            go.transform.position = start;
            var t = go.AddComponent<MistBladeTrail>();
            t.projectile = p;
            t.startOffset = start - p.Position;
            Vector3 a = Vector3.ProjectOnPlane(swingAxis, p.Direction);
            t.swingAxis = a.sqrMagnitude > 1e-4f ? a.normalized : Vector3.up;
            float power = p.Power > 0f ? p.Power : 1f;
            t.strength = Mathf.Clamp01(power);
            t.scale = Mathf.Lerp(0.6f, 1f, t.strength); // 약한 칼날(0.5)은 0.8배
            t.Build();
            return t;
        }

        private void Build()
        {
            // 칼날: 몸 좌표 — Z = 날아가는 방향, Y = 휘두른 방향(손톱 줄기가 뻗는 쪽), X = 줄기가 나란히 놓이는 쪽
            var bgo = new GameObject("MistBlade");
            bgo.transform.SetParent(transform, false);
            bgo.transform.rotation = Quaternion.LookRotation(projectile.Direction, swingAxis);
            bgo.transform.localScale = Vector3.one * scale;
            mesh = BuildClawMesh();
            bgo.AddComponent<MeshFilter>().sharedMesh = mesh;
            bladeRenderer = bgo.AddComponent<MeshRenderer>();
            bladeMat = new Material(Shader.Find("Sprites/Default")) { name = "MistBlade" }; // 꼭짓점 색(가장자리 투명), 양면
            bladeMat.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 1f, strength));
            bladeRenderer.sharedMaterial = bladeMat;
            bladeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            blade = bgo.transform;

            // 안개: 칼날 줄기 자리에서 지나간 거리만큼 덩이를 남긴다(월드 공간), 천천히 커지며 옅어진다
            var sgo = new GameObject("MistBladeFog");
            sgo.transform.SetParent(blade, false);
            mist = sgo.AddComponent<ParticleSystem>();
            mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = mist.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f * scale, 0.45f * scale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(MistColor.r, MistColor.g, MistColor.b, MistColor.a * Mathf.Lerp(0.6f, 1f, strength));
            main.maxParticles = 600;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = mist.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 16f;
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 1.4f, 0.15f); // 세 줄기가 차지하는 자리(칼날 크기는 위 localScale로 따라감)
            var size = mist.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.8f));
            var col = mist.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.72f, 0.78f), 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var noise = mist.noise;
            noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.8f;
            var rot = mist.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            var r = sgo.GetComponent<ParticleSystemRenderer>();
            mistMat = new Material(Shader.Find("Sprites/Default")) { name = "MistBladeFog", mainTexture = PuffTexture() };
            r.sharedMaterial = mistMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mist.Play();
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
            pos += startOffset * Mathf.Clamp01(1f - age / StartBlend);
            transform.position = pos;
            // 손에서 떨어진 직후 짧게 커지고, 날아가며 살짝 일렁인다
            float grow = Mathf.SmoothStep(0.35f, 1f, Mathf.Clamp01(age / 0.1f));
            float wobble = 1f + Mathf.Sin(age * 31f) * 0.04f;
            blade.localScale = new Vector3(scale * grow, scale * grow * wobble, scale * grow);
        }

        private void End()
        {
            ended = true;
            Vector3 point = projectile != null && !(projectile is Object o && o == null) ? projectile.EndPoint : transform.position;
            transform.position = point;
            Vector3 dir = projectile != null && !(projectile is Object p && p == null) ? projectile.Direction : blade.forward;
            var emission = mist.emission;
            emission.rateOverDistance = 0f;
            // 칼날이 안개로 부서진다: 세 줄기 자리마다 덩이가 터져 흩어진다(앞으로 조금, 사방으로)
            var ep = new ParticleSystem.EmitParams();
            Vector3 side = blade.right, up = blade.up;
            int perClaw = Mathf.RoundToInt(Mathf.Lerp(7f, 11f, strength));
            for (int c = 0; c < Claws; c++)
            {
                float x = (c - (Claws - 1) * 0.5f) * 0.2f * scale;
                for (int i = 0; i < perClaw; i++)
                {
                    float u = Random.Range(-1f, 1f);
                    ep.position = point + side * x + up * (u * 0.7f * scale) - dir * 0.1f;
                    ep.velocity = Random.onUnitSphere * Random.Range(0.8f, 2.4f) - dir * Random.Range(0.3f, 1.2f);
                    ep.startSize = Random.Range(0.25f, 0.5f) * scale;
                    ep.startLifetime = Random.Range(0.6f, 1.1f);
                    mist.Emit(ep, 1);
                }
            }
            blade.gameObject.GetComponent<MeshRenderer>().enabled = false;
            Destroy(gameObject, 1.6f);
        }

        private void OnDestroy()
        {
            if (bladeMat != null) Destroy(bladeMat);
            if (mistMat != null) Destroy(mistMat);
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>
        /// 손톱 세 줄기. 줄기마다 Y(휘두른 방향)로 뻗고, 가운데가 앞(+Z)으로 휜 초승달, 양 끝은 뾰족하다.
        /// 단면이 + 모양(X로 펼친 띠 + Z로 펼친 띠)이라 뒤·옆 어디서 봐도 보인다. 띠 가장자리는 투명(꼭짓점 색).
        /// 가운데 줄기가 가장 길다.
        /// </summary>
        private static Mesh BuildClawMesh()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var colors = new System.Collections.Generic.List<Color>();
            var tris = new System.Collections.Generic.List<int>();
            const float halfLen = 0.75f, bow = 0.28f, spacing = 0.2f, width = 0.09f;
            for (int c = 0; c < Claws; c++)
            {
                float off = (c - (Claws - 1) * 0.5f) * spacing;
                float len = c == 1 ? halfLen : halfLen * 0.82f;
                float shift = (c - 1) * 0.06f; // 손톱마다 조금씩 어긋나게
                for (int band = 0; band < 3; band++)
                {
                    // 0 = 뒤에서 보이는 띠(X), 1 = 옆에서 보이는 띠(Z), 2 = 둘레의 옅은 안개 번짐(넓고 흐림)
                    Vector3 across = band == 1 ? Vector3.forward : Vector3.right;
                    Color core = band == 2 ? new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0.28f) : BladeColor;
                    int baseIndex = verts.Count;
                    for (int s = 0; s <= Segments; s++)
                    {
                        float u = s / (float)Segments * 2f - 1f;              // -1 ~ 1
                        float taper = Mathf.Pow(Mathf.Clamp01(1f - u * u), 0.6f); // 양 끝 뾰족
                        Vector3 center = new(off, u * len + shift, bow * (1f - u * u));
                        float w = width * taper * (band == 0 ? 1f : band == 1 ? 0.8f : 3.2f);
                        verts.Add(center - across * w); colors.Add(new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0f));
                        verts.Add(center);              colors.Add(core);
                        verts.Add(center + across * w); colors.Add(new Color(BladeColor.r, BladeColor.g, BladeColor.b, 0f));
                    }
                    for (int s = 0; s < Segments; s++)
                    {
                        int i0 = baseIndex + s * 3, i1 = i0 + 3;
                        tris.AddRange(new[] { i0, i1, i0 + 1, i0 + 1, i1, i1 + 1 });
                        tris.AddRange(new[] { i0 + 1, i1 + 1, i0 + 2, i0 + 2, i1 + 1, i1 + 2 });
                    }
                }
            }
            var m = new Mesh { name = "MistClawBlade" };
            m.SetVertices(verts); m.SetColors(colors); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>가장자리가 부드럽게 사라지는 둥근 안개 한 덩이 텍스처(한 번만 만든다).</summary>
        public static Texture2D PuffTexture()
        {
            if (puffTexture != null) return puffTexture;
            const int n = 64;
            puffTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "MistPuff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * n + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            puffTexture.SetPixels(px);
            puffTexture.Apply();
            return puffTexture;
        }
    }
}

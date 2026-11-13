using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 리퍼 손톱 안개 연출(IMistCharge, BE를 읽어 그린다).
    /// - 손톱(무기 판정 상자 자리, 없으면 왼손) 둘레에 회색빛 안개가 감겨 돈다. 모인 양(칸 + 다음 칸 진행도)만큼 짙어지고,
    ///   2칸이 다 차면 더 짙고 빠르게 감긴다.
    /// - 이 캐릭터가 날린 안개 칼날(ProjectileEvents, Kind = MistBlade)마다 MistBladeTrail을 붙인다(손톱 자리에서 시작).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 리퍼만).
    /// </summary>
    [DisallowMultipleComponent]
    public class MistClawVisual : MonoBehaviour
    {
        [SerializeField] private Color mistColor = new(0.82f, 0.84f, 0.88f, 0.55f);
        [Tooltip("다 찼을 때(2칸) 1초에 나오는 안개 덩이 수.")]
        [SerializeField, Min(0f)] private float fullRate = 130f;

        private IMistCharge mist;
        private CharacterIdentity identity;
        private Hitbox claw;
        private Transform emitterRoot;
        private ParticleSystem fog;
        private Material fogMat;

        private void OnEnable() => ProjectileEvents.Launched += OnLaunched;

        private void OnDisable()
        {
            ProjectileEvents.Launched -= OnLaunched;
            if (emitterRoot != null) emitterRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (emitterRoot != null) Destroy(emitterRoot.gameObject);
            if (fogMat != null) Destroy(fogMat);
        }

        private void LateUpdate()
        {
            if (mist == null) mist = GetComponent<IMistCharge>();
            if (mist == null) return;
            if (fog == null) Build();
            emitterRoot.gameObject.SetActive(true);
            if (!FindClaw(out Vector3 center, out float radius)) { SetRate(0f); return; }
            emitterRoot.position = center;
            var shape = fog.shape;
            shape.radius = radius;

            int max = Mathf.Max(1, mist.MaxCharges);
            float level = Mathf.Clamp01((mist.Charges + mist.NextProgress) / max);
            bool full = mist.Charges >= max;
            SetRate(full ? fullRate : fullRate * 0.6f * Mathf.Max(level, 0.15f)); // 모이는 동안에도 조금씩 보이게
            var orbit = fog.velocityOverLifetime;
            orbit.orbitalY = full ? 7f : 3.5f; // 다 차면 빠르게 감긴다
            var main = fog.main;
            main.startColor = full ? new Color(mistColor.r, mistColor.g, mistColor.b, Mathf.Min(1f, mistColor.a * 1.35f)) : mistColor;
        }

        private void SetRate(float r)
        {
            var e = fog.emission;
            e.rateOverTime = r;
        }

        /// <summary>손톱 자리(무기 판정 상자 가운데)와 둘레 반지름.</summary>
        private bool FindClaw(out Vector3 center, out float radius)
        {
            if (claw == null || !claw.isActiveAndEnabled)
            {
                claw = null;
                foreach (var hb in GetComponentsInChildren<Hitbox>())
                {
                    if (hb.Slot == HitboxSlot.Weapon) { claw = hb; break; }
                    if (hb.Slot == HitboxSlot.LeftHand) claw = hb;
                }
            }
            if (claw == null || claw.Collider == null) { center = default; radius = 0f; return false; }
            var b = claw.Collider.bounds;
            center = b.center;
            radius = Mathf.Clamp(b.extents.magnitude * 0.6f, 0.08f, 0.3f);
            return true;
        }

        private void Build()
        {
            emitterRoot = new GameObject("MistClaw").transform; // 손톱을 따라 자리만 옮긴다(회전·크기는 따르지 않음)
            fog = emitterRoot.gameObject.AddComponent<ParticleSystem>();
            fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = fog.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 손을 따라 같이 움직이며 둘레를 돈다
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); // 작은 덩이가 많이 — 감긴 실처럼
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = mistColor;
            main.maxParticles = 400;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = fog.emission;
            emission.rateOverTime = 0f;
            var shape = fog.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.15f; shape.radiusThickness = 0.3f; // 겉에서 생긴다
            var vel = fog.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalX = 0f; vel.orbitalY = 3.5f; vel.orbitalZ = 0f; // 세로축으로 감겨 돈다
            vel.radial = -0.05f; // 살짝 손톱 쪽으로 감긴다
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f); // 세 축 모두 같은 방식(두 값 사이)이어야 한다
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            var size = fog.sizeOverLifetime;
            size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
            var col = fog.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.75f, 0.77f, 0.82f), 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var noise = fog.noise;
            noise.enabled = true; noise.strength = 0.12f; noise.frequency = 1.2f;
            var rot = fog.rotationOverLifetime;
            rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var r = emitterRoot.GetComponent<ParticleSystemRenderer>();
            fogMat = new Material(Shader.Find("Sprites/Default")) { name = "MistClaw", mainTexture = MistBladeTrail.PuffTexture() };
            r.sharedMaterial = fogMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fog.Play();
        }

        private void OnLaunched(IProjectile p)
        {
            if (identity == null) identity = GetComponent<CharacterIdentity>();
            if (p.Kind != MistCharge.BladeKind || identity == null || p.OwnerId != identity.Id) return;
            if (mist == null) mist = GetComponent<IMistCharge>();
            Vector3 start = FindClaw(out Vector3 c, out _) ? c : p.Position;
            MistBladeTrail.Create(p, start, mist != null ? mist.LastSwingAxis : Vector3.up);
            if (fog != null) fog.Clear(); // 감겨 있던 안개가 칼날로 떨어져 나간다
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 신호총(공군 E): 잠깐 겨눈 뒤(windup) 신호탄을 쏜다. 신호탄은 바라보는 방향에 가장 맞는 상대(targetAngle·targetRange 안)를
    /// 따라가는 유도탄(HomingProjectile)이다 — 곡선으로 휘지만 길을 찾지 않고 곧장 가므로 벽·바닥·천장에 닿으면 사라진다.
    /// 상대가 없으면 바라보는 방향으로 곧게 난다. 쏘는 동안 잠깐 제자리(이동·공격 불가).
    /// 체력·데미지는 다루지 않는다(맞힌 것은 HitEvents로 알릴 뿐).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Flare Shot")]
    public class FlareShotSkill : SkillDefinition
    {
        private const int Aiming = 0, Fired = 1;

        [Tooltip("겨누는 시간(초) — 이 뒤에 쏜다.")]
        [Min(0f)] public float windup = 0.2f;
        [Tooltip("쏜 뒤 다시 움직일 수 있을 때까지(초).")]
        [Min(0f)] public float afterShot = 0.15f;
        [Tooltip("신호탄 속도(m/s).")]
        [Min(0.1f)] public float flareSpeed = 22f;
        [Tooltip("신호탄이 목표 쪽으로 방향을 트는 빠르기(도/초). 클수록 급하게 휜다.")]
        [Min(0f)] public float turnRate = 140f;
        [Tooltip("상대를 맞히는 굵기(반지름, m) / 벽에 닿는 굵기(m).")]
        [Min(0.01f)] public float hitRadius = 0.35f, wallRadius = 0.08f;
        [Tooltip("신호탄이 사라지기까지(초).")]
        [Min(0.1f)] public float lifetime = 4f;
        [Tooltip("목표로 고를 상대: 최대 거리(m), 바라보는 방향에서의 최대 각도(도).")]
        [Min(1f)] public float targetRange = 40f;
        [Range(1f, 180f)] public float targetAngle = 70f;
        [Tooltip("쏘는 방향을 바라보는 방향보다 위로(도) — 신호탄이 살짝 떠올랐다가 휘어 간다.")]
        public float launchPitchUp = 12f;
        [Tooltip("쏘는 자리(캐릭터 키 대비 높이 / 몸 가운데에서 오른쪽 m / 앞 m) — 오른손 신호총.")]
        [Range(0.1f, 1f)] public float heightRatio = 0.65f;
        public float sideOffset = 0.3f, forwardOffset = 0.6f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Aiming);
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false; // 제자리
            return true;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (ctx.Phase == Aiming)
            {
                if (ctx.PhaseTime >= windup) { Fire(ctx); ctx.SetPhase(Fired); }
                return false;
            }
            return ctx.PhaseTime >= afterShot;
        }

        private void Fire(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 fwd = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 몸 = 시야 방향
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = Origin(ctx, fwd, right);
            Vector3 dir = Quaternion.AngleAxis(-launchPitchUp, right) * fwd;
            var target = FindTarget(self, origin, fwd);
            HomingProjectile.Launch("Flare", self, target, origin, dir, flareSpeed, turnRate, hitRadius, wallRadius, lifetime, slot, -ctx.UseCount);
        }

        private Vector3 Origin(CharacterSkill ctx, Vector3 fwd, Vector3 right)
        {
            var cc = ctx.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            return ctx.transform.position + Vector3.up * h * heightRatio + right * sideOffset + fwd * forwardOffset;
        }

        /// <summary>바라보는 방향(수평)에 가장 맞는, 맞힐 수 있는 상대(거리·각도 안). 없으면 null.</summary>
        private CharacterIdentity FindTarget(CharacterIdentity self, Vector3 origin, Vector3 fwd)
        {
            CharacterIdentity best = null;
            float bestAngle = targetAngle;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>(FindObjectsSortMode.None))
            {
                if (self != null && !self.CanHit(c)) continue;
                Vector3 to = HomingProjectile.AimPoint(c) - origin;
                if (to.magnitude > targetRange) continue;
                Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
                if (flat.sqrMagnitude < 1e-4f) continue;
                float angle = Vector3.Angle(fwd, flat);
                if (angle <= bestAngle) { bestAngle = angle; best = c; }
            }
            return best;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase == Aiming ? "겨누는 중" : "신호탄!";

        public override void Complete(GameObject owner) { }
    }
}

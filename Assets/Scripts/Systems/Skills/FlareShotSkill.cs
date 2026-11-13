using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 신호총(공군 E): 잠깐 겨눈 뒤(windup) 신호탄을 쏜다. 신호탄은 가장 가까운 상대(targetRange 안, 화면·시야 방향과 상관없이)를
    /// 따라가는 유도탄(HomingProjectile)이다. 겨누는 동안 몸이 그 상대 쪽으로 저절로 돌아간다(시점은 그대로).
    /// 신호탄은 — 곡선으로 휘지만 길을 찾지 않고 곧장 가므로 벽·바닥·천장에 닿으면 사라진다.
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
        [Min(0f)] public float afterShot = 0.6f; // 쏘기 모션(Skill_Coordinator_Flare, 0.8초)이 끝날 때까지
        [Tooltip("신호탄 속도(m/s).")]
        [Min(0.1f)] public float flareSpeed = 22f;
        [Tooltip("신호탄이 목표 쪽으로 방향을 트는 빠르기(도/초). 클수록 급하게 휜다.")]
        [Min(0f)] public float turnRate = 140f;
        [Tooltip("상대를 맞히는 굵기(반지름, m) / 벽에 닿는 굵기(m).")]
        [Min(0.01f)] public float hitRadius = 0.35f, wallRadius = 0.08f;
        [Tooltip("신호탄이 사라지기까지(초).")]
        [Min(0.1f)] public float lifetime = 4f;
        [Tooltip("목표로 고를 상대의 최대 거리(m). 이 안에서 가장 가까운 상대를 노린다(보고 있지 않아도).")]
        [Min(1f)] public float targetRange = 40f;
        [Tooltip("쏘는 방향을 바라보는 방향보다 위로(도) — 신호탄이 살짝 떠올랐다가 휘어 간다.")]
        public float launchPitchUp = 12f;
        [Tooltip("쏘는 자리(캐릭터 키 대비 높이 / 몸 가운데에서 오른쪽 m / 앞 m) — 오른손 신호총 총구.")]
        [Range(0.1f, 1f)] public float heightRatio = 0.82f; // 쏘는 순간 총구(Coordinator_Muzzle) 자리에 맞춤: 몸 기준 (0.10, 1.40, 0.84), 키 1.70
        public float sideOffset = 0.1f, forwardOffset = 0.84f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Aiming);
            Aim(ctx);
        }

        /// <summary>쏠 방향(ctx.Direction, 수평) = 가장 가까운 상대 쪽, 없으면 지금 몸 방향.</summary>
        private void Aim(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            var target = FindTarget(self, ctx.transform.position);
            Vector3 dir = target != null ? Vector3.ProjectOnPlane(HomingProjectile.AimPoint(target) - ctx.transform.position, Vector3.up) : Vector3.zero;
            ctx.Direction = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized;
        }

        public override bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            // 쏘는 동안 몸이 노리는 상대 쪽을 본다.
            yaw = Mathf.Atan2(ctx.Direction.x, ctx.Direction.z) * Mathf.Rad2Deg;
            return ctx.Direction.sqrMagnitude > 1e-4f;
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
                Aim(ctx); // 겨누는 동안 상대가 움직이면 따라 돈다
                if (ctx.PhaseTime >= windup) { Fire(ctx); ctx.SetPhase(Fired); }
                return false;
            }
            return ctx.PhaseTime >= afterShot;
        }

        private void Fire(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 fwd = ctx.Direction.sqrMagnitude > 1e-4f ? ctx.Direction : Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 노리는 상대 쪽(몸이 이미 그쪽을 봄)
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = Origin(ctx, fwd, right);
            Vector3 dir = Quaternion.AngleAxis(-launchPitchUp, right) * fwd;
            var target = FindTarget(self, ctx.transform.position);
            HomingProjectile.Launch("Flare", self, target, origin, dir, flareSpeed, turnRate, hitRadius, wallRadius, lifetime, slot, -ctx.UseCount);
        }

        private Vector3 Origin(CharacterSkill ctx, Vector3 fwd, Vector3 right)
        {
            var cc = ctx.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            return ctx.transform.position + Vector3.up * h * heightRatio + right * sideOffset + fwd * forwardOffset;
        }

        /// <summary>가장 가까운, 맞힐 수 있는 상대(targetRange 안 — 화면에 보이지 않아도). 없으면 null.</summary>
        private CharacterIdentity FindTarget(CharacterIdentity self, Vector3 origin)
        {
            CharacterIdentity best = null;
            float bestDist = targetRange;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (self != null && !self.CanHit(c)) continue;
                if (c == self) continue;
                float d = (HomingProjectile.AimPoint(c) - origin).magnitude;
                if (d <= bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase == Aiming ? "겨누는 중" : "신호탄!";

        public override void Complete(GameObject owner) { }
    }
}

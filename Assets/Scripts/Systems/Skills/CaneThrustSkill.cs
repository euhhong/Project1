using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 지팡이 찌르기(맹인 E): 몸이 시야 방향을 보고 지팡이를 앞으로 곧게 찌른다.
    /// 준비(windup) → 찌름(active, 이 동안 가슴 높이에서 앞으로 reach까지 굵기 hitRadius로 훑어 닿는 상대를 한 번씩 맞힘) → 거두기(recovery).
    /// 맞은 상대는 데미지 없이 찌른 방향으로 세게 넉백된다(Knockback). 쓰는 동안은 제자리(이동·점프·공격 불가).
    /// 동작 구간은 TryGetMotionPhase로 알린다(FE가 찌르기 모션을 구간에 맞춰 재생한다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Cane Thrust")]
    public class CaneThrustSkill : SkillDefinition
    {
        private const int Windup = 0, Active = 1, Recovery = 2;

        [Min(0f)] public float windup = 0.22f;
        [Min(0.01f)] public float active = 0.14f;
        [Min(0f)] public float recovery = 0.45f;
        [Tooltip("찌르기가 닿는 거리(몸 앞, m).")]
        [Min(0.3f)] public float reach = 3.2f;
        [Tooltip("판정 굵기(반지름, m).")]
        [Min(0.05f)] public float hitRadius = 0.55f;
        [Tooltip("판정 높이(캐릭터 키 대비, 가슴쯤).")]
        [Range(0.1f, 1f)] public float heightRatio = 0.62f;
        [Tooltip("넉백 처음 속도(m/s) / 시간(초). 거리 ≈ 속도 × 시간 ÷ 2.")]
        [Min(0f)] public float knockbackSpeed = 14f;
        [Min(0.05f)] public float knockbackTime = 0.35f;
        public HitboxSlot slot = HitboxSlot.Weapon;
        [Tooltip("휘두르기에서 이어 쓴 찌르기(연계)만: 이 거리(m) 안의 가장 가까운 상대 쪽으로 몸을 자동으로 돌려 찌른다(오토에임). 그냥 E는 그대로.")]
        [Min(0f)] public float comboAutoAimRange = 6f;

        // 이번 찌르기에서 이미 맞힌 상대(캐릭터마다 따로).
        private static readonly Dictionary<CharacterSkill, HashSet<int>> hitThisCast = new();

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Windup);
            ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized;
            ctx.AreaRadius = ctx.StartedFromCombo && AutoAim(ctx) ? 1f : 0f; // 1 = 오토에임 중(이 스킬은 범위 표시를 쓰지 않는다)
            if (!hitThisCast.TryGetValue(ctx, out var set)) hitThisCast[ctx] = set = new HashSet<int>();
            set.Clear();
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false; // 제자리
            return true;
        }

        public override bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            // 찌르기 시작한 뒤로는 찌르는 방향을 지킨다.
            yaw = Mathf.Atan2(ctx.Direction.x, ctx.Direction.z) * Mathf.Rad2Deg;
            return ctx.Phase != Windup || ctx.AreaRadius > 0f; // 연계 오토에임이면 준비 때부터 상대 쪽으로
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recoveryTime)
        {
            recoveryTime = 0f;
            switch (ctx.Phase)
            {
                case Windup:
                    if (ctx.AreaRadius > 0f) AutoAim(ctx); // 연계 오토에임: 준비 동안 상대를 따라 돈다
                    else ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 준비 동안은 시야를 따라 돈다
                    if (ctx.PhaseTime >= windup) ctx.SetPhase(Active);
                    return false;
                case Active:
                    HitAlong(ctx);
                    if (ctx.PhaseTime >= active) ctx.SetPhase(Recovery);
                    return false;
                default:
                    return ctx.PhaseTime >= recovery;
            }
        }

        public override bool TryGetMotionPhase(CharacterSkill ctx, out AttackPhase phase, out float progress)
        {
            switch (ctx.Phase)
            {
                case Windup: phase = AttackPhase.Windup; progress = windup > 0f ? ctx.PhaseTime / windup : 1f; break;
                case Active: phase = AttackPhase.Active; progress = ctx.PhaseTime / active; break;
                default: phase = AttackPhase.Recovery; progress = recovery > 0f ? ctx.PhaseTime / recovery : 1f; break;
            }
            return true;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase == Windup ? "준비" : "찌르기!";

        public override void Complete(GameObject owner) { }

        /// <summary>가장 가까운 맞힐 수 있는 상대(comboAutoAimRange 안) 쪽으로 찌를 방향을 돌린다. 없으면 false(방향 그대로).</summary>
        private bool AutoAim(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            CharacterIdentity best = null; float bestDist = comboAutoAimRange;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c == self || (self != null && !self.CanHit(c))) continue;
                if (c.TryGetComponent(out ICharacterHealth hp) && hp.IsDead) continue;
                float d = Vector3.Distance(c.transform.position, ctx.transform.position);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            if (best == null) return false;
            Vector3 to = Vector3.ProjectOnPlane(best.transform.position - ctx.transform.position, Vector3.up);
            if (to.sqrMagnitude < 1e-4f) return false;
            ctx.Direction = to.normalized;
            return true;
        }

        private void HitAlong(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            var cc = ctx.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            Vector3 start = ctx.transform.position + Vector3.up * (h * heightRatio);
            var hits = Physics.SphereCastAll(start, hitRadius, ctx.Direction, reach, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            var done = hitThisCast[ctx];
            foreach (var hc in hits)
            {
                if (!hc.collider.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var victim = hurt.Owner;
                if (victim == null || (self != null && !self.CanHit(victim)) || !done.Add(victim.Id)) continue;
                var hit = new HitResult
                {
                    AttackerId = self != null ? self.Id : 0,
                    VictimId = victim.Id,
                    Part = hurt.Part,
                    Slot = slot,
                    AttackId = -ctx.UseCount,
                    Tick = SimulationTick.Current,
                    Point = hc.distance > 0f ? hc.point : hurt.Collider.ClosestPoint(start),
                    NoDamage = true,
                    Knockback = ctx.Direction * knockbackSpeed,
                };
                Knockback.Push(victim.gameObject, hit.Knockback, knockbackTime);
                HitEvents.Raise(hit);
            }
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 그랩 쏘기(사냥터지기): E를 누르면 몸이 바로 시야(카메라) 방향을 보고, 잠깐 준비(windup)한 뒤
    /// 그랩이 용수철처럼 앞으로 곧게 뻗어 나간다(extendSpeed, 최대 range). 길 위에서 처음 닿는 상대를 맞히고(HitEvents),
    /// 맞히거나 끝까지 가면 되감긴다(retractSpeed). 쓰는 동안은 제자리(이동·점프·공격 불가).
    /// 그랩이 지금 얼마나 나가 있는지는 CharacterSkill.Reach / Direction으로 공개한다(FE가 그랩·사슬을 그린다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Grab Shot")]
    public class GrabShotSkill : SkillDefinition
    {
        private const int Windup = 0, Extending = 1, Retracting = 2;

        [Tooltip("뻗기 전 준비 시간(초).")]
        [Min(0f)] public float windup = 0.3f;
        [Tooltip("최대 사거리(m).")]
        [Min(0.5f)] public float range = 12f;
        [Tooltip("뻗는 속도(m/s).")]
        [Min(0.1f)] public float extendSpeed = 45f;
        [Tooltip("되감는 속도(m/s).")]
        [Min(0.1f)] public float retractSpeed = 60f;
        [Tooltip("판정 굵기(반지름, m).")]
        [Min(0.05f)] public float hitRadius = 0.45f;
        [Tooltip("그랩이 나가는 높이(캐릭터 키 대비).")]
        [Range(0.1f, 1f)] public float heightRatio = 0.55f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Windup);
            ctx.Reach = 0f;
            ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 몸 = 시야 방향
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false; // 제자리
            return true;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            float dt = Time.fixedDeltaTime;
            switch (ctx.Phase)
            {
                case Windup:
                    if (ctx.PhaseTime >= windup) ctx.SetPhase(Extending);
                    return false;

                case Extending:
                {
                    float from = ctx.Reach;
                    ctx.Reach = Mathf.Min(range, ctx.Reach + extendSpeed * dt);
                    if (HitAlong(ctx, from, ctx.Reach, out float hitAt))
                    {
                        ctx.Reach = hitAt;
                        ctx.SetPhase(Retracting);
                    }
                    else if (ctx.Reach >= range) ctx.SetPhase(Retracting);
                    return false;
                }

                default:
                    ctx.Reach = Mathf.Max(0f, ctx.Reach - retractSpeed * dt);
                    if (ctx.Reach <= 0f) { ctx.Reach = 0f; return true; }
                    return false;
            }
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase == Windup ? "준비" : "그랩!";

        public override void Complete(GameObject owner) { }

        /// <summary>그랩이 나가는 시작점(몸 앞, 가슴 높이).</summary>
        public Vector3 Origin(CharacterSkill ctx)
        {
            var cc = ctx.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            return ctx.transform.position + Vector3.up * h * heightRatio + ctx.Direction * 0.3f;
        }

        /// <summary>from ~ to 사이를 굵기 hitRadius로 훑어 처음 닿는 다른 캐릭터를 맞힌다. 맞힌 거리를 돌려준다.</summary>
        private bool HitAlong(CharacterSkill ctx, float from, float to, out float hitAt)
        {
            hitAt = to;
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 start = Origin(ctx) + ctx.Direction * from;
            float length = Mathf.Max(to - from, 0.001f);
            var hits = Physics.SphereCastAll(start, hitRadius, ctx.Direction, length, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue; Hurtbox bestHurt = null;
            foreach (var h in hits)
            {
                if (!h.collider.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var owner = hurt.Owner;
                if (owner == null || (self != null && !self.CanHit(owner))) continue;
                float d = h.distance > 0f ? h.distance : 0f;
                if (d < best) { best = d; bestHurt = hurt; }
            }
            if (bestHurt == null) return false;
            hitAt = from + best;
            HitEvents.Raise(new HitResult
            {
                AttackerId = self != null ? self.Id : 0,
                VictimId = bestHurt.Owner.Id,
                Part = bestHurt.Part,
                Slot = slot,
                AttackId = -ctx.UseCount,
                Tick = SimulationTick.Current,
                Point = start + ctx.Direction * best,
            });
            return true;
        }
    }
}

using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 도약 → 내려찍기(루키노).
    /// - 쓰면 앞으로 크게 포물선을 그리며 뛰어오른다(leapHeight 12m = 보통 점프의 10배). 떠 있는 동안 앞으로 forwardSpeed로 날아간다.
    /// - 공중에서 공격 키를 누르면 바로 아래 착지 지점에 공격 범위(slamRadius)가 뜨고, 빠르게 내리꽂는다.
    ///   땅에 닿는 순간 범위 안의 다른 캐릭터를 모두 맞힌다(HitEvents, 부위는 몸통). 그 뒤 slamRecovery 동안 못 움직인다.
    /// - 공중에서 E를 한 번 더 누르면 공격 없이 그 자리에서 바로 빠르게 떨어진다(모션은 내려찍기와 같음).
    /// - 아무것도 안 누르면 그냥 착지하고 끝난다.
    /// 땅에서만 쓴다. 시전 중 일반 공격·점프는 못 한다(공격 키는 내려찍기에 쓴다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Leap")]
    public class LeapSkill : SkillDefinition
    {
        private const int Rising = 0, Slamming = 1, Dropping = 2;

        [Header("도약")]
        [Tooltip("뛰어오르는 높이(m). 보통 점프는 1.2m.")]
        [Min(0f)] public float leapHeight = 12f;
        [Tooltip("머리 위 이 높이(m) 안에 천장(지형·물건)이 있으면 E를 눌러도 뛰지 않는다(HUD에 '천장이 막혀 못 뜀'). 0이면 검사하지 않음.")]
        [Min(0f)] public float ceilingCheckHeight = 6f;
        [Tooltip("떠 있는 동안 앞으로 가는 속도(m/s).")]
        [Min(0f)] public float forwardSpeed = 9f;

        [Header("내려찍기")]
        [Tooltip("내리꽂는 속도(m/s).")]
        [Min(0f)] public float slamSpeed = 30f;
        [Tooltip("착지할 때 맞히는 범위 반지름(m).")]
        [Min(0f)] public float slamRadius = 3f;
        [Tooltip("내려찍은 뒤 못 움직이는 시간(초). 착지 모션(Hard Landing, 1.43초)을 이 시간에 맞춰 재생한다.")]
        [Min(0f)] public float slamRecovery = 1.2f;
        [Tooltip("공중에서 E를 한 번 더 눌러 빠르게 떨어진(공격 없음) 뒤 못 움직이는 시간(초). 짧게 — 착지 모션을 빠르게 재생하고 끝낸다.")]
        [Min(0f)] public float dropRecovery = 0.25f;
        [Tooltip("내려찍기 공격 판정이 나는 자리 표시용(HitResult.Slot).")]
        public HitboxSlot slamSlot = HitboxSlot.Weapon;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var state = owner.GetComponent<ICharacterState>();
            if (state != null && !state.IsGrounded) { reason = "공중에서는 못 씀"; return false; }
            if (HasCeiling(owner)) { reason = "천장이 막혀 못 뜀"; return false; }
            reason = null;
            return true;
        }

        /// <summary>머리 위 ceilingCheckHeight 안에 천장(지형·물건)이 있는지. 캐릭터·판정·트리거는 무시한다.</summary>
        private bool HasCeiling(GameObject owner)
        {
            if (ceilingCheckHeight <= 0f) return false;
            var cc = owner.GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius * 0.9f : 0.3f;
            Vector3 top = owner.transform.position + Vector3.up * ((cc != null ? cc.center.y + cc.height * 0.5f : 1.8f) - radius);
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            foreach (var h in Physics.SphereCastAll(top, radius, Vector3.up, ceilingCheckHeight, mask, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<CharacterIdentity>() != null) continue; // 자기 몸·다른 캐릭터
                if (h.distance <= 0f && h.point == Vector3.zero) continue;                 // 처음부터 겹쳐 있던 것
                return true;
            }
            return false;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor != null) motor.Launch(Mathf.Sqrt(2f * leapHeight * Mathf.Abs(motor.Gravity)));
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = false;
            if (ctx.Phase != Rising) { localMove = Vector2.zero; speed = 0f; } // 똑바로 떨어진다
            else { localMove = Vector2.up; speed = forwardSpeed; }
            return true;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor == null) return true;

            if (ctx.Phase == Rising)
            {
                // 막 뛰어오른 직후에는 아직 땅에 붙어 있을 수 있다.
                if (elapsed > 0.15f && motor.IsGrounded) return true; // 그냥 착지
                if (input.AttackPressed || input.LightAttack || input.HeavyCharging || input.HeavyRelease)
                {
                    ctx.SetPhase(Slamming);
                    ctx.AreaCenter = FindGround(ctx.transform.position);
                    ctx.AreaRadius = slamRadius;
                    ctx.ShowArea = true;
                    motor.Launch(-slamSpeed);
                }
                else if (input.Skill && elapsed > 0.05f)
                {
                    // E를 한 번 더: 공격 없이 그 자리에서 바로 빠르게 떨어진다(모션은 내려찍기와 같다, 범위 표시·판정 없음).
                    ctx.SetPhase(Dropping);
                    motor.Launch(-slamSpeed);
                }
                return false;
            }

            // 내리꽂는 중: 땅에 닿을 때까지 중력보다 빠르게 계속 떨어진다.
            if (!motor.IsGrounded)
            {
                motor.Launch(-slamSpeed);
                return false;
            }
            if (ctx.Phase == Slamming)
            {
                ctx.AreaCenter = ctx.transform.position;
                HitArea(ctx); // 착지 순간 범위 공격(빠른 낙하는 공격 없음)
            }
            recovery = ctx.Phase == Dropping ? dropRecovery : slamRecovery;
            return true;
        }

        // 떠오름 = ID, 내리꽂는 중(공격·빠른 낙하) = ID_Slam, 착지 후 경직 = ID_Land(Hard Landing)
        public override string AnimationState(CharacterSkill ctx) =>
            ctx.State == SkillState.Recovering ? id + "_Land" : ctx.Phase != Rising ? id + "_Slam" : id;

        public override void Complete(GameObject owner) { }

        private static Vector3 FindGround(Vector3 from)
        {
            // 캐릭터 판정(트리거)은 무시하고 땅을 찾는다.
            var hits = Physics.RaycastAll(from + Vector3.up * 0.1f, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 point = from;
            foreach (var h in hits)
            {
                if (h.collider is CharacterController) continue;
                if (h.distance < best) { best = h.distance; point = h.point; }
            }
            return point;
        }

        private void HitArea(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            var victims = new Dictionary<CharacterIdentity, Hurtbox>();
            foreach (var c in Physics.OverlapSphere(ctx.AreaCenter, slamRadius, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide))
            {
                if (!c.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var owner = hurt.Owner;
                if (owner == null || (self != null && !self.CanHit(owner))) continue;
                // 한 캐릭터는 한 번만. 몸통 판정이 있으면 그것을 맞은 부위로 쓴다.
                if (!victims.TryGetValue(owner, out var kept) || (kept.Part != BodyPart.Body && hurt.Part == BodyPart.Body))
                    victims[owner] = hurt;
            }
            foreach (var (victim, hurt) in victims)
            {
                HitEvents.Raise(new HitResult
                {
                    AttackerId = self != null ? self.Id : 0,
                    VictimId = victim.Id,
                    Part = hurt.Part,
                    Slot = slamSlot,
                    AttackId = -ctx.UseCount, // 일반 공격 번호와 겹치지 않게 음수
                    Tick = SimulationTick.Current,
                    Point = hurt.transform.position,
                });
            }
        }
    }
}

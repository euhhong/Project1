using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 도약 → 내려찍기(루키노).
    /// - 쓰면 앞으로 크게 뛰어오른다(보통 점프보다 높게, leapHeight). 떠 있는 동안 앞으로 forwardSpeed로 날아간다.
    /// - 공중에서 공격 키를 누르면 바로 아래 착지 지점에 공격 범위(slamRadius)가 뜨고, 빠르게 내리꽂는다.
    ///   땅에 닿는 순간 범위 안의 다른 캐릭터를 모두 맞힌다(HitEvents, 부위는 몸통). 그 뒤 slamRecovery 동안 못 움직인다.
    /// - 공격 키를 안 누르면 그냥 착지하고 끝난다.
    /// 땅에서만 쓴다. 시전 중 일반 공격·점프는 못 한다(공격 키는 내려찍기에 쓴다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Leap")]
    public class LeapSkill : SkillDefinition
    {
        private const int Rising = 0, Slamming = 1;

        [Header("도약")]
        [Tooltip("뛰어오르는 높이(m). 보통 점프는 1.2m.")]
        [Min(0f)] public float leapHeight = 4f;
        [Tooltip("떠 있는 동안 앞으로 가는 속도(m/s).")]
        [Min(0f)] public float forwardSpeed = 7f;

        [Header("내려찍기")]
        [Tooltip("내리꽂는 속도(m/s).")]
        [Min(0f)] public float slamSpeed = 30f;
        [Tooltip("착지할 때 맞히는 범위 반지름(m).")]
        [Min(0f)] public float slamRadius = 3f;
        [Tooltip("내려찍은 뒤 못 움직이는 시간(초). 내려찍기 모션의 착지~일어서기에 맞춘다.")]
        [Min(0f)] public float slamRecovery = 0.9f;
        [Tooltip("내려찍기 공격 판정이 나는 자리 표시용(HitResult.Slot).")]
        public HitboxSlot slamSlot = HitboxSlot.Weapon;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var state = owner.GetComponent<ICharacterState>();
            if (state != null && !state.IsGrounded) { reason = "공중에서는 못 씀"; return false; }
            reason = null;
            return true;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor != null) motor.Launch(Mathf.Sqrt(2f * leapHeight * Mathf.Abs(motor.Gravity)));
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = false;
            if (ctx.Phase == Slamming) { localMove = Vector2.zero; speed = 0f; } // 똑바로 내리꽂는다
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
                if (input.LightAttack || input.HeavyCharging || input.HeavyRelease)
                {
                    ctx.SetPhase(Slamming);
                    ctx.AreaCenter = FindGround(ctx.transform.position);
                    ctx.AreaRadius = slamRadius;
                    ctx.ShowArea = true;
                    motor.Launch(-slamSpeed);
                }
                return false;
            }

            // 내려찍는 중: 땅에 닿으면 범위 공격.
            if (!motor.IsGrounded)
            {
                motor.Launch(-slamSpeed); // 중력보다 빠르게 계속 내리꽂는다
                return false;
            }
            ctx.AreaCenter = ctx.transform.position;
            HitArea(ctx);
            recovery = slamRecovery;
            return true;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.Phase == Slamming ? id + "_Slam" : id;

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

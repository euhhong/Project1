using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 로켓 돌진(광대 E): 전위 돌진(ChargeSkill)과 같은 돌진 앞에 "들고 다니기"가 붙는다.
    /// 0) 시야(화면 가운데) 가까이(lookBlockAngle 12도 안)에 보이는 상대가 있으면 E로 시작하지 못한다("상대를 보고 있음").
    /// 1) E를 꾹 누르는 동안: 로켓을 두 손으로 머리 위로 치켜든 채 걸어 다닌다(castMoveSpeed, 점프 가능).
    ///    이때 좌클릭(공격)을 누르면 돌진하지 않고 끝난다(취소).
    /// 2) E를 떼면(한 번만 눌러도 바로) 로켓 머리를 앞으로 한 채 chargeSpeed로 돌진한다 — 벽에 부딪히면 기절 페널티 없이 그냥 멈추고(CrashPenalty 끔),
    ///    캐릭터에 몸이 닿으면 옛 판정: 넉백 없이 그 자리에서 돌진 거리만큼 기절(stunPerMeter, min~max — 전위의 넓힌 판정·넉백 박치기와 다름). 돌진 중 E를 한 번 더 누르면 멈춘다.
    /// 동작 구간: 들고 다님 = Windup, 돌진 = Active(TryGetMotionPhase) — FE가 로켓 쥐는 법을 바꾼다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Rocket Charge")]
    public class RocketChargeSkill : ChargeSkill
    {
        private const int Raised = 0, Charging = 1, Cancelled = 2;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Raised);
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            if (ctx.Phase != Charging)
            {
                // 들고 다니는 동안은 평소처럼 걷는다(속도는 castMoveSpeed로 제한)
                localMove = Vector2.zero; speed = 0f; allowJump = true;
                return false;
            }
            return TryGetMoveOverride(ctx.gameObject, out localMove, out speed, out allowJump);
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (ctx.Phase == Cancelled) return true; // 취소한 다음 틱에 끝낸다(취소한 틱에는 아직 시전 중이라 공격이 나가지 않는다)
            if (ctx.Phase != Charging)
            {
                if (input.LightAttack || input.AttackPressed || input.HeavyCharging || input.HeavyRelease)
                {
                    // 좌클릭 → 취소. 그 클릭은 공격으로 쓰지 않는다(아직 키를 안 뗐으면 뗄 때까지 공격 입력 무시).
                    bool released = input.LightAttack || input.HeavyRelease;
                    if (!released && ctx.TryGetComponent(out CharacterAttack attack)) attack.IgnoreAttackUntilRelease();
                    ctx.SetPhase(Cancelled);
                    return false;
                }
                if (!input.SkillHeld) ctx.SetPhase(Charging); // 뗐다(또는 한 번만 눌렀다) → 돌진
                return false;
            }
            // 돌진: 전위와 같다(돌진을 시작한 뒤 지난 시간으로 장애물 판정·기절 시간을 잰다). E를 한 번 더 누르면 취소.
            if (input.Skill && ctx.PhaseTime > cancelGrace) return true;
            return ShouldEndEarly(ctx.gameObject, ctx.PhaseTime, out recovery);
        }

        public override bool TryGetMotionPhase(CharacterSkill ctx, out AttackPhase phase, out float progress)
        {
            phase = ctx.Phase == Charging ? AttackPhase.Active : AttackPhase.Windup; // 취소한 틱도 들고 있는 자세
            progress = 0f;
            return true;
        }

        // 들고 다님 = ID_Raise(상체만, 다리는 걷기), 돌진 = ID(전신), 넘어짐 = ID_End
        public override string AnimationState(CharacterSkill ctx) =>
            ctx.State == SkillState.Recovering ? id + "_End" : ctx.Phase == Charging ? id : id + "_Raise";

        protected override bool CrashPenalty => false; // 로켓 돌진은 벽에 박아도 기절 페널티 없이 그냥 멈춘다

        [Header("옛 박치기 판정 — 몸에 닿으면 바로 기절(넉백 없음)")]
        [Tooltip("돌진한 거리 1m마다 상대 기절 시간(초) / 최소 / 최대.")]
        [Min(0f)] public float stunPerMeter = 0.15f;
        [Min(0f)] public float minStun = 0.5f;
        [Min(0f)] public float maxStun = 4f;

        protected override bool UsesRamZone => false; // 넓힌 판정 없이 몸이 실제로 닿아야

        /// <summary>옛 판정: 몸이 닿은 상대를 넉백 없이 그 자리에서 돌진 거리만큼 기절시키고 멈춘다(뒤에서 들이받으면 휘청이며 쓰러지는 모션).</summary>
        protected override bool ContactHit(GameObject owner, CharacterIdentity victim, float elapsed)
        {
            float seconds = Mathf.Clamp(elapsed * chargeSpeed * stunPerMeter, minStun, maxStun);
            CharacterStun.Apply(victim.gameObject, seconds, victim.transform.position - owner.transform.position);
            return true;
        }

        [Header("시작 제한 — 상대를 바라보고 있으면 E로 시작하지 못함")]
        [Tooltip("시야(화면 가운데) 방향에서 이 각도(도) 안에 보이는(벽에 가리지 않은) 상대가 있으면 시작하지 못한다 / 그 거리(m)까지.")]
        [Range(1f, 45f)] public float lookBlockAngle = 12f;
        [Min(1f)] public float lookBlockDistance = 60f;

        public override bool CanStart(GameObject owner, out string reason)
        {
            if (!base.CanStart(owner, out reason)) return false;
            if (LookingAtEnemy(owner)) { reason = "상대를 보고 있음"; return false; }
            return true;
        }

        /// <summary>시야 방향(LookYaw·LookPitch) 가까이(lookBlockAngle 안, lookBlockDistance 안)에 벽에 가리지 않은 상대가 있는지.</summary>
        private bool LookingAtEnemy(GameObject owner)
        {
            if (!owner.TryGetComponent(out ICharacterState state)) return false;
            var self = owner.GetComponent<CharacterIdentity>();
            Vector3 eye = owner.transform.position + Vector3.up * 1.6f;
            Vector3 look = Quaternion.Euler(state.LookPitch, state.LookYaw, 0f) * Vector3.forward;
            float cos = Mathf.Cos(lookBlockAngle * Mathf.Deg2Rad);
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c == null || c == self || (self != null && !self.CanHit(c))) continue;
                Vector3 point = HomingProjectile.AimPoint(c);
                Vector3 to = point - eye;
                float dist = to.magnitude;
                if (dist < 1e-3f || dist > lookBlockDistance || Vector3.Dot(to / dist, look) < cos) continue;
                if (CombatLineOfSight.Clear(owner, c.gameObject, point)) return true;
            }
            return false;
        }

        public override string StatusText(CharacterSkill ctx) => ctx.Phase == Charging ? "로켓 돌진! — E로 멈춤" :"로켓 들기 — E를 떼면 돌진(좌클릭 취소)";
    }
}

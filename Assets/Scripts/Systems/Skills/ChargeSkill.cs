using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진(전위 럭비공): 쓰면 바라보는 방향으로 계속 달려 나간다.
    /// - 방향 키·달리기 키와 상관없이 앞으로 chargeSpeed로 달린다(달리기 모션). 점프·공격은 못 한다.
    /// - 옆으로 벽·장애물에 닿으면 멈추고 일반 기절(CharacterStun — 기절 모션, 넘어지지 않음)에 걸린다:
    ///   짧게(shortCrashDistance 5m 미만) 달렸으면 shortCrashRecovery(0.4초), 그보다 길면 달린 거리에 비례해 minFallRecovery(1초) ~ crashRecovery(1.8초, 12m 이상).
    /// - 다른 캐릭터에 닿으면(박치기) 상대는 돌진한 거리에 비례한 세기로 밀려나고(knockSpeedPerMeter, 기절 없음 — 밀리는 동안 움직일 수 있음),
    ///   나는 멈추지 않고(딜레이 없이) 바로 평소처럼 움직인다.
    ///   상대가 밀려나다 벽에 박혀야만 기절: wallStunBase(2초) + 돌진한 시간 × wallStunPerSecond(최대 wallStunMax 5초).
    ///   오래 닿지 않으면 castTime(최대 시간)에 그냥 멈춘다.
    /// - 시야(마우스 우클릭)로 방향은 틀 수 있다(몸이 시야 방향을 보기 때문).
    /// - 돌진 중 E를 한 번 더 누르면 그 자리에서 멈춘다(넘어지지 않음, cancelGrace 뒤부터).
    /// 땅에서만 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Charge")]
    public class ChargeSkill : SkillDefinition
    {
        [Tooltip("돌진 속도(m/s). 달리기 속도보다 빠르게.")]
        [Min(0f)] public float chargeSpeed = 16f;

        [Tooltip("시작 직후 이 시간(초) 동안은 장애물에 닿아도 멈추지 않는다(벽에 붙어 시작할 때 바로 끝나지 않게).")]
        [Min(0f)] public float obstacleGrace = 0.1f;

        [Tooltip("벽·장애물에 부딪혀 걸리는 기절 시간(초, 일반 기절 모션) — 멀리(fullCrashDistance 이상) 달려 부딪혔을 때.")]
        [Min(0f)] public float crashRecovery = 1.8f;
        [Tooltip("짧게 달려 부딪힘: 달린 거리(m)가 이보다 짧으면 shortCrashRecovery초만 기절한다.")]
        [Min(0f)] public float shortCrashDistance = 5f;
        [Min(0f)] public float shortCrashRecovery = 0.4f;
        [Tooltip("그보다 길게 달려 부딪히면: 기절 시간 = minFallRecovery(shortCrashDistance에서) ~ crashRecovery(이 거리 m 이상에서), 달린 거리에 비례.")]
        [Min(0f)] public float fullCrashDistance = 12f;
        [Min(0f)] public float minFallRecovery = 1f;

        [Header("캐릭터에 부딪힘(박치기) → 상대 넉백")]
        [Tooltip("돌진한 거리 1m마다 넉백 처음 속도(m/s). 밀리는 거리 ≈ 속도 × knockTime ÷ 2.")]
        [Min(0f)] public float knockSpeedPerMeter = 0.8f;
        [Min(0f)] public float minKnockSpeed = 4f, maxKnockSpeed = 20f;
        [Tooltip("넉백 시간(초). 밀리는 동안 상대는 기절(못 움직임).")]
        [Min(0.05f)] public float knockTime = 0.5f;
        [Header("넉백으로 벽에 박힘 → 기절")]
        [Tooltip("기절 시간 = wallStunBase + 돌진한 시간(초) × wallStunPerSecond, 최대 wallStunMax(초).")]
        [Min(0f)] public float wallStunBase = 2f;
        [Min(0f)] public float wallStunPerSecond = 1f;
        [Min(0f)] public float wallStunMax = 5f;

        public override bool LocksActions => true;

        public override bool SyncAnimationToCast => false; // 달리기 모션은 계속 돈다

        public override bool CanStart(GameObject owner, out string reason)
        {
            var state = owner.GetComponent<ICharacterState>();
            if (state != null && !state.IsGrounded) { reason = "공중에서는 못 씀"; return false; }
            reason = null;
            return true;
        }

        public override bool TryGetMoveOverride(GameObject owner, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.up;
            speed = chargeSpeed;
            allowJump = false;
            return true;
        }

        [Tooltip("돌진을 시작한 뒤 이 시간(초)이 지나면 E를 한 번 더 눌러 돌진을 멈출 수 있다(넘어지지 않음).")]
        [Min(0f)] public float cancelGrace = 0.1f;

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            if (input.Skill && elapsed > cancelGrace) { recovery = 0f; return true; } // E를 한 번 더 → 돌진 취소
            return ShouldEndEarly(ctx.gameObject, elapsed, out recovery);
        }

        public override bool ShouldEndEarly(GameObject owner, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (elapsed < obstacleGrace) return false;
            var motor = owner.GetComponent<CharacterMotor>();
            if (motor == null || !motor.HitObstacle) return false;

            // 부딪힌 것이 다른 캐릭터면(박치기): 돌진한 거리만큼 세게 밀어내고(기절 없음, 벽에 박혀야 기절), 나는 경직 없이 바로 움직인다(recovery 0).
            // 같은 팀·면역(예: 나비에게 날아가는 붉은나비)이면 밀지 않고 멈추기만 한다.
            var victim = motor.LastObstacle != null ? motor.LastObstacle.GetComponentInParent<CharacterIdentity>() : null;
            if (victim != null && victim.gameObject != owner)
            {
                var self = owner.GetComponent<CharacterIdentity>();
                if (self != null && !self.CanHit(victim)) return true;
                float distance = elapsed * chargeSpeed;
                Vector3 dir = Vector3.ProjectOnPlane(victim.transform.position - owner.transform.position, Vector3.up);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
                float speed = Mathf.Clamp(distance * knockSpeedPerMeter, minKnockSpeed, maxKnockSpeed);
                float wallStun = Mathf.Min(wallStunMax, wallStunBase + elapsed * wallStunPerSecond); // 벽에 박히면(돌진한 시간에 비례, 최대 5초)
                Knockback.Push(victim.gameObject, dir * speed, knockTime, stun: false, impactDamage: false, impactStun: wallStun, impactStunWallOnly: true);
                return true;
            }

            // 벽·장애물: 넘어지지 않고 일반 기절(기절 모션). 짧게 달렸으면 잠깐, 길게 달렸으면 달린 거리에 비례해 오래.
            float run = elapsed * chargeSpeed;
            float stunTime = run < shortCrashDistance
                ? shortCrashRecovery
                : Mathf.Lerp(minFallRecovery, crashRecovery, Mathf.InverseLerp(shortCrashDistance, fullCrashDistance, run));
            CharacterStun.Apply(owner, stunTime, Vector3.zero); // 방향 없음 = 앞으로 숙이는 일반 기절(뒤로 쓰러지는 모션 아님)
            recovery = 0f; // 넘어짐(Recovering + Fall_1)은 쓰지 않는다
            return true;
        }

        public override void Complete(GameObject owner) { }
    }
}

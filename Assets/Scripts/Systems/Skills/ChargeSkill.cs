using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진(전위 럭비공): 쓰면 바라보는 방향으로 계속 달려 나간다.
    /// - 방향 키·달리기 키와 상관없이 앞으로 chargeSpeed로 달린다(달리기 모션). 점프·공격은 못 한다.
    /// - 옆으로 벽·장애물에 닿으면 멈추고 crashRecovery 동안 넘어져 못 움직인다.
    /// - 다른 캐릭터에 닿으면 멈추고, 상대는 돌진 거리에 비례해 기절한다(stunPerMeter, min~max). 나는 넘어지지 않는다.
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

        [Tooltip("장애물에 부딪혀 멈추면 넘어져서 못 움직이는 시간(초). 넘어지기 모션(Fall_1, 1.77초)에 맞춘다.")]
        [Min(0f)] public float crashRecovery = 1.8f;

        [Header("캐릭터에 부딪힘 → 상대 기절")]
        [Tooltip("돌진한 거리 1m마다 상대 기절 시간(초).")]
        [Min(0f)] public float stunPerMeter = 0.15f;
        [Min(0f)] public float minStun = 0.5f;
        [Min(0f)] public float maxStun = 4f;

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

            // 부딪힌 것이 다른 캐릭터면: 상대를 돌진 거리만큼 기절시키고, 나는 넘어지지 않고 멈춘다.
            var victim = motor.LastObstacle != null ? motor.LastObstacle.GetComponentInParent<CharacterIdentity>() : null;
            if (victim != null && victim.gameObject != owner)
            {
                float distance = elapsed * chargeSpeed;
                float seconds = Mathf.Clamp(distance * stunPerMeter, minStun, maxStun);
                if (!victim.TryGetComponent(out CharacterStun stun)) stun = victim.gameObject.AddComponent<CharacterStun>();
                stun.Stun(seconds, victim.transform.position - owner.transform.position); // 뒤에서 들이받으면 휘청이며 쓰러진다(모션)
                if (victim.TryGetComponent(out CharacterMotor victimMotor)) victimMotor.RefreshSpeedLimits();
                return true;
            }

            // 벽·장애물: 넘어진다.
            recovery = crashRecovery;
            return true;
        }

        public override void Complete(GameObject owner) { }
    }
}

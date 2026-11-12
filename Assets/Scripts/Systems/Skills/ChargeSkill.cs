using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진(전위 럭비공): 쓰면 바라보는 방향으로 계속 달려 나간다.
    /// - 방향 키·달리기 키와 상관없이 앞으로 chargeSpeed로 달린다(달리기 모션). 점프·공격은 못 한다.
    /// - 옆으로 무언가(벽·장애물·캐릭터)에 닿으면 멈춘다. 오래 닿지 않으면 castTime(최대 시간)에 멈춘다.
    /// - 시야(마우스 우클릭)로 방향은 틀 수 있다(몸이 시야 방향을 보기 때문).
    /// 땅에서만 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Charge")]
    public class ChargeSkill : SkillDefinition
    {
        [Tooltip("돌진 속도(m/s). 달리기 속도보다 빠르게.")]
        [Min(0f)] public float chargeSpeed = 8f;

        [Tooltip("시작 직후 이 시간(초) 동안은 장애물에 닿아도 멈추지 않는다(벽에 붙어 시작할 때 바로 끝나지 않게).")]
        [Min(0f)] public float obstacleGrace = 0.1f;

        public override bool LocksActions => true;

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

        public override bool ShouldEndEarly(GameObject owner, float elapsed)
        {
            if (elapsed < obstacleGrace) return false;
            var motor = owner.GetComponent<CharacterMotor>();
            return motor != null && motor.HitObstacle;
        }

        public override void Complete(GameObject owner) { }
    }
}

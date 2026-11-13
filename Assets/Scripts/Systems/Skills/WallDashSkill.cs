using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 아대(용병): E로 켜고 다시 E로 끈다. 켜져 있는 동안
    /// - 몸이 벽(캐릭터가 아닌 장애물)에 옆으로 닿으면 도약 준비가 된다.
    /// - 그다음 첫 이동(벽에 닿을 때 누르던 방향과 다른 방향을 누르거나, 멈췄다가 다시 움직이면)에
    ///   그 방향으로 초고속(dashSpeed)으로 dashTime 동안 튀어 나간다(잔상). 점프는 못 한다.
    /// - 벽에 닿은 뒤 autoDashDelay(0.5초) 안에 방향 입력이 없으면 바라보는 방향(카메라 앞)으로 저절로 도약한다.
    /// - 도약이 끝나면 다시 켜진 상태로 돌아가고, 벽에 또 닿으면 또 도약할 수 있다.
    /// 켜져 있어도 걷기·달리기·공격은 평소대로 한다(공격해도 꺼지지 않는다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Wall Dash")]
    public class WallDashSkill : SkillDefinition
    {
        private const int Active = 0, Armed = 1, Dashing = 2;

        [Tooltip("도약 속도(m/s).")]
        [Min(0f)] public float dashSpeed = 35f;
        [Tooltip("도약 시간(초). 거리 = 속도 x 시간.")]
        [Min(0.01f)] public float dashTime = 0.3f;
        [Tooltip("벽에 닿을 때 누르던 방향과 이 값(내적)보다 다르면 '새 이동'으로 본다.")]
        [Range(-1f, 1f)] public float newDirectionDot = 0.7f;
        [Tooltip("이동 입력으로 볼 최소 크기.")]
        [Range(0f, 1f)] public float moveDeadZone = 0.3f;
        [Tooltip("벽에 닿은 뒤 이 시간(초) 안에 방향 입력이 없으면 바라보는 방향(카메라 앞)으로 저절로 도약한다.")]
        [Min(0f)] public float autoDashDelay = 0.5f;

        public override bool LocksActions => false;
        public override bool CancelledByAttack => false;
        public override bool LimitsMoveSpeed => false;
        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Active);
            ctx.Direction = Vector3.zero;
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            if (ctx.Phase == Dashing)
            {
                localMove = new Vector2(ctx.Direction.x, ctx.Direction.z);
                speed = dashSpeed;
                allowJump = false;
                return true;
            }
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (input.Skill && elapsed > 0.05f) return true; // E를 다시 누름 → 끈다

            var motor = ctx.GetComponent<CharacterMotor>();
            Vector2 move = input.Move;
            bool moving = move.magnitude > moveDeadZone;

            switch (ctx.Phase)
            {
                case Active:
                case Armed:
                {
                    bool touching = motor != null && motor.HitObstacle && IsWall(ctx, motor.LastObstacle);
                    Vector3 dir = moving ? new Vector3(move.x, 0f, move.y).normalized : Vector3.zero;
                    if (ctx.Phase == Active)
                    {
                        // 벽에 닿음: 지금 누르던 방향을 기억해 두고, 그와 다른 첫 이동을 기다린다(최대 autoDashDelay).
                        if (touching) { ctx.SetPhase(Armed); ctx.Direction = dir; }
                        break;
                    }
                    if (ctx.PhaseTime >= autoDashDelay)
                    {
                        // autoDashDelay 안에 새 방향 입력이 없었다 → 바라보는 방향(시야 = 카메라 앞)으로 저절로 도약.
                        ctx.SetPhase(Dashing);
                        ctx.Direction = Vector3.forward;
                        break;
                    }
                    if (moving && (ctx.Direction == Vector3.zero || Vector3.Dot(dir, ctx.Direction) < newDirectionDot))
                    {
                        // 새 이동(다른 방향, 또는 멈췄다가 다시 움직임) → 그 방향으로 도약.
                        ctx.SetPhase(Dashing);
                        ctx.Direction = dir;
                        break;
                    }
                    if (!moving) ctx.Direction = Vector3.zero;   // 멈췄다가 다시 움직이면 어느 방향이든 도약
                    else if (touching) ctx.Direction = dir;      // 벽 쪽으로 계속 누르는 방향은 새 이동이 아니다
                    break;
                }

                case Dashing:
                    if (ctx.PhaseTime >= dashTime) ctx.SetPhase(Active);
                    break;
            }
            return false;
        }

        public override bool Afterimage(CharacterSkill ctx) => ctx.Phase == Dashing;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase switch
        {
            Armed => $"벽에 닿음 — 이동하면 도약! ({Mathf.Max(0f, autoDashDelay - ctx.PhaseTime):0.0}초 뒤 앞으로)",
            Dashing => "도약!",
            _ => "켜짐 (E로 끄기)",
        };

        // 벽에 닿아 도약을 기다리는 동안 = ID_Wall(벽을 손으로 짚는 자세, Male Action Pose), 도약 중 = ID_Dash(모션이 있으면).
        public override string AnimationState(CharacterSkill ctx) =>
            ctx.Phase == Armed ? id + "_Wall" : ctx.Phase == Dashing ? id + "_Dash" : string.Empty;

        public override void Complete(GameObject owner) { }

        private static bool IsWall(CharacterSkill ctx, Collider hit)
        {
            if (hit == null) return false;
            var other = hit.GetComponentInParent<CharacterIdentity>();
            return other == null || other.gameObject == ctx.gameObject;
        }
    }
}

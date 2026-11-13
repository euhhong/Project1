using System.Collections.Generic;
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
    /// - 도약은 월드 방향으로 곧게 간다(시점을 돌려도 휘지 않음). 도약 중반(steerStart)부터 처음 누른 방향키 하나로 휘는 쪽을 정하고
    ///   그 방향키가 가리키는 쪽으로 최대 곡선(steerRate)까지 휜다. 반대쪽으로는 못 휜다(S자 불가). 시점을 휙 돌리면 U자(maxTurn)까지.
    /// - 도약이 끝나도 꺼지지 않는다(켜진 채 그대로, E로만 끈다). 대신 한 번 도약하면 dashCooldown(5초) 동안 재사용 대기 —
    ///   그동안은 벽에 닿아도 도약 준비가 되지 않는다. 대기는 캐릭터마다 따로 재고, E로 껐다 켜도 이어진다.
    /// 켜져 있어도 걷기·달리기·공격·창틀 넘기·턱 오르기는 평소대로 한다(모션 그대로, 공격해도 꺼지지 않는다). 창틀 넘기·오르기·매달리기·착지 중에는
    /// 아대가 쉰다(도약 준비 안 함, E도 무시) — 끝나고 afterTraversalGrace(0.35초) 동안도 넘은 창틀에 튕기지 않는다. 상태 글자·막대는 도약 재사용 대기만 보여 준다.
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
        [Tooltip("벽에 닿은 뒤 벽을 짚는 자세(_Wall)를 보여 주는 시간(초). 지나면 원래 모션으로 돌아간다.")]
        [Min(0f)] public float wallPoseTime = 0.1f;
        [Tooltip("한 번 도약한 뒤 다음 도약까지 재사용 대기(초). 그동안 벽에 닿아도 도약 준비가 안 된다. 아대는 켜진 채 그대로.")]
        [Min(0f)] public float dashCooldown = 5f;

        // 캐릭터(CharacterSkill)마다 다음 도약이 가능한 시각. 껐다 켜도 남는다.
        private static readonly Dictionary<CharacterSkill, float> ReadyAt = new();

        /// <summary>이 캐릭터의 도약 재사용 대기 남은 시간(초).</summary>
        public float DashCooldownRemaining(CharacterSkill ctx) => ctx != null && ReadyAt.TryGetValue(ctx, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        public override bool LocksActions => false;
        public override bool CancelledByAttack => false;
        public override bool LimitsMoveSpeed => false;
        public override bool SyncAnimationToCast => false;
        // 켜 둔 채로 창틀 넘기·턱 오르기는 평소대로(모션 나옴). 도약하는 동안만 막는다.
        public override bool AllowsTraversal(CharacterSkill ctx) => ctx.Phase != Dashing;

        [Tooltip("창틀 넘기·오르기·매달리기·착지가 끝난 뒤 이 시간(초) 동안은 벽에 닿아도 도약 준비가 안 된다(넘은 창틀에 튕겨 나가지 않게).")]
        [Min(0f)] public float afterTraversalGrace = 0.35f;

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
            // 창틀 넘기·오르기·매달리기·착지 중: 아대는 쉰다(도약 준비 안 함, E도 무시 — 넘는 동안 실수로 꺼지지 않게). 끝나고 afterTraversalGrace 동안도 준비 안 함.
            if (ctx.Phase != Dashing && ctx.TryGetComponent(out ICharacterTraversalState tr) && Pauses(tr.Action))
            {
                ctx.SetPhase(Active); ctx.Direction = Vector3.zero;
                return false;
            }
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
                        // 재사용 대기 중·창틀 넘은 직후(PhaseTime < afterTraversalGrace)에는 준비 안 됨
                        if (touching && DashCooldownRemaining(ctx) <= 0f && ctx.PhaseTime >= afterTraversalGrace) { ctx.SetPhase(Armed); ctx.Direction = dir; }
                        break;
                    }
                    if (ctx.PhaseTime >= autoDashDelay)
                    {
                        // autoDashDelay 안에 새 방향 입력이 없었다 → 바라보는 방향(시야 = 카메라 앞)으로 저절로 도약.
                        StartDash(ctx, Vector3.forward);
                        break;
                    }
                    if (moving && (ctx.Direction == Vector3.zero || Vector3.Dot(dir, ctx.Direction) < newDirectionDot))
                    {
                        // 새 이동(다른 방향, 또는 멈췄다가 다시 움직임) → 그 방향으로 도약.
                        StartDash(ctx, dir);
                        break;
                    }
                    if (!moving) ctx.Direction = Vector3.zero;   // 멈췄다가 다시 움직이면 어느 방향이든 도약
                    else if (touching) ctx.Direction = dir;      // 벽 쪽으로 계속 누르는 방향은 새 이동이 아니다
                    break;
                }

                case Dashing:
                {
                    Quaternion body = Quaternion.Euler(0f, ctx.transform.eulerAngles.y, 0f);
                    Steer(ctx, body, moving ? new Vector3(move.x, 0f, move.y).normalized : Vector3.zero);
                    ctx.Direction = Quaternion.Inverse(body) * ctx.Heading; // 월드 방향 그대로(시점을 돌려도 휘지 않음)
                    if (motor != null && motor.HitObstacle && IsWall(ctx, motor.LastObstacle)) { SlideAlongWall(ctx, motor.LastObstacleNormal); ctx.Heading = body * ctx.Direction; }
                    if (ctx.PhaseTime >= dashTime)
                    {
                        // 도약 끝: 꺼지지 않고 켜진 채로 돌아가고, 재사용 대기 시작
                        ReadyAt[ctx] = Time.time + dashCooldown;
                        ctx.SetPhase(Active);
                        ctx.Direction = Vector3.zero;
                    }
                    break;
                }
            }
            return false;
        }

        [Header("도약 중 휘기")]
        [Tooltip("도약 시간의 이 비율(0~1)이 지난 뒤 처음 누른 방향키로 휘는 쪽을 정한다(그 뒤로는 반대쪽으로 못 휨 — S자 불가).")]
        [Range(0f, 1f)] public float steerStart = 0.5f;
        [Tooltip("휘는 빠르기(도/초) — 최대 곡선.")]
        [Min(0f)] public float steerRate = 1200f;
        [Tooltip("한 번 도약에서 휠 수 있는 최대 각도(도). 180 = U자(시점을 휙 돌리면 뒤쪽까지).")]
        [Range(0f, 360f)] public float maxTurn = 180f;

        /// <summary>도약 시작: 시야 기준 방향을 월드 방향(Heading)으로 바꿔 고정한다. 휘기는 아직 정하지 않음.</summary>
        private void StartDash(CharacterSkill ctx, Vector3 localDir)
        {
            ctx.SetPhase(Dashing);
            ctx.Direction = localDir;
            ctx.Heading = (Quaternion.Euler(0f, ctx.transform.eulerAngles.y, 0f) * localDir).normalized;
            ctx.SteerSide = 0; ctx.Turned = 0f; ctx.SteerInput = Vector3.zero;
        }

        /// <summary>
        /// 도약 중 휘기: steerStart가 지난 뒤 처음 누른 방향키(그 값 하나만)로 휘는 쪽을 정하고, 그 방향키가 가리키는 월드 방향(= 지금 시야 기준)으로
        /// steerRate만큼씩 휜다. 정한 쪽으로만 휘고(S자 불가), 그 방향에 닿거나 maxTurn에 이르면 멈춘다.
        /// 방향키 값은 처음 것 그대로라, 시점을 휙 돌리면 목표가 따라 돌아 U자(최대 maxTurn)까지 휜다.
        /// </summary>
        private void Steer(CharacterSkill ctx, Quaternion body, Vector3 localInput)
        {
            if (ctx.SteerSide == 2) return;
            if (ctx.SteerSide == 0)
            {
                if (ctx.PhaseTime < dashTime * steerStart || localInput == Vector3.zero) return;
                float first = Vector3.SignedAngle(ctx.Heading, body * localInput, Vector3.up);
                // 가는 방향 그대로(도약을 시작한 방향키를 계속 누름) 또는 곧장 뒤는 휘는 쪽을 정하지 않는다 — 다른 쪽을 처음 누를 때 정한다.
                if (Mathf.Abs(first) < 8f || Mathf.Abs(first) > 172f) return;
                ctx.SteerInput = localInput;
                ctx.SteerSide = first < 0f ? 1 : -1;
            }
            Vector3 target = body * ctx.SteerInput;
            // 정한 쪽(왼쪽 = 반시계)으로 잰 목표까지 남은 각도. 목표를 살짝 지나쳐 반대쪽에 있으면(270도 넘음) 다 온 것.
            float a = Vector3.SignedAngle(ctx.Heading, target, Vector3.up) * -ctx.SteerSide;
            if (a < 0f) a += 360f;
            if (a > 270f) return;
            float step = Mathf.Min(steerRate * Time.fixedDeltaTime, a, maxTurn - ctx.Turned);
            if (step <= 0f) return;
            ctx.Heading = (Quaternion.AngleAxis(-ctx.SteerSide * step, Vector3.up) * ctx.Heading).normalized;
            ctx.Turned += step;
        }

        [Tooltip("도약 중 벽에 닿았을 때, 벽을 따라가는 성분이 이 값(0~1)보다 크면 벽을 따라 같은 속도로 미끄러진다(ㄱ자 이동). 정면으로 박으면 그대로.")]
        [Range(0f, 1f)] public float slideMinTangent = 0.15f;

        /// <summary>
        /// 도약 중 벽에 닿으면 벽 쪽으로 파고드는 성분을 빼고 벽을 따라가는 방향으로 바꾼다(속도는 그대로). 비비며 짧게 멈추지 않고 부드럽게 미끄러진다.
        /// Direction은 몸(시야) 기준이므로 월드로 바꿔 계산한 뒤 되돌린다.
        /// </summary>
        private void SlideAlongWall(CharacterSkill ctx, Vector3 wallNormal)
        {
            if (wallNormal.sqrMagnitude < 0.5f) return;
            Quaternion body = ctx.transform.rotation;
            Vector3 world = body * ctx.Direction;
            if (Vector3.Dot(world, wallNormal) >= 0f) return;   // 벽에서 멀어지는 중
            Vector3 along = Vector3.ProjectOnPlane(world, wallNormal);
            along.y = 0f;
            if (along.magnitude < slideMinTangent) return;       // 거의 정면으로 박음 → 그대로
            ctx.Direction = Quaternion.Inverse(body) * along.normalized;
        }

        public override bool Afterimage(CharacterSkill ctx) => ctx.Phase == Dashing;

        // 상태 글자: 도약 재사용 대기 남은 시간만(설명은 없음). 준비되면 빈칸.
        public override string StatusText(CharacterSkill ctx) => DashCooldownRemaining(ctx) > 0f ? $"{DashCooldownRemaining(ctx):0.0}초" : string.Empty;

        // HUD 막대: 재사용 대기 중이면 차오르는 막대, 준비되면 없음.
        public override float StatusBarFill(CharacterSkill ctx)
        {
            float r = DashCooldownRemaining(ctx);
            return r > 0f && dashCooldown > 0f ? 1f - r / dashCooldown : -1f;
        }

        /// <summary>아대가 쉬는 동작: 창틀 넘기·턱 오르기·매달리기·착지(낙하 중 공중에서 벽 튕기기는 그대로).</summary>
        private static bool Pauses(TraversalAction a) => a != TraversalAction.None && a != TraversalAction.Fall;

        // 벽에 닿은 직후 wallPoseTime(0.1초) 동안만 = ID_Wall(벽을 손으로 짚는 자세, Male Action Pose), 그다음은 원래 모션. 도약 중 = ID_Dash(모션이 있으면).
        public override string AnimationState(CharacterSkill ctx) =>
            ctx.Phase == Armed && ctx.PhaseTime < wallPoseTime ? id + "_Wall" : ctx.Phase == Dashing ? id + "_Dash" : string.Empty;

        public override void Complete(GameObject owner) { }

        private static bool IsWall(CharacterSkill ctx, Collider hit)
        {
            if (hit == null) return false;
            var other = hit.GetComponentInParent<CharacterIdentity>();
            return other == null || other.gameObject == ctx.gameObject;
        }
    }
}

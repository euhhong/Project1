using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코) 나비에게로(E): 지금 겨누는 나비(ButterflyField.TargetIndex) 자리로 곧게 빠르게 미끄러져 간다(glideSpeed).
    /// - 겨누는 나비가 없거나 나비를 놓는 중이면(놓기를 E로 취소하고 좌클릭을 아직 안 뗀 동안 포함) 쓰지 않는다.
    /// - 가는 동안 장애물(벽·다른 캐릭터 등)에 부딪히면 거기서 멈추고 툭 떨어진다(잠깐 못 움직임, bumpRecovery).
    ///   단 창틀 아랫부분(창턱)은 뚫고 지나간다(창틀 옆·윗부분은 막힘, PassWindowSills).
    /// - 이동·공격 불가. 중력은 그대로라 턱에서 벗어나면 떨어진다.
    /// - 날아가는 동안 화면을 돌려도 몸(모델)은 시작할 때 각도를 지킨다(TryGetFacingYaw). 끝나면 다시 시야를 본다.
    /// 목표 자리는 시전 동안 CharacterSkill.AreaCenter에 둔다(ShowArea는 끔).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Butterfly Glide")]
    public class ButterflyGlideSkill : SkillDefinition
    {
        [Tooltip("미끄러지는 속도(m/s). 용병 아대 도약(35m/s)의 70%.")]
        [Min(0.1f)] public float glideSpeed = 24.5f;
        [Tooltip("이 거리(m, 수평) 안에 들어오면 도착.")]
        [Min(0.05f)] public float arriveDistance = 0.3f;
        [Tooltip("가다가 장애물에 부딪혀 멈춘 뒤 못 움직이는 시간(초, 툭 떨어짐).")]
        [Min(0f)] public float bumpRecovery = 0.35f;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var field = owner.GetComponent<ButterflyField>();
            if (field == null) { reason = "나비 없음"; return false; }
            if (field.Placing || field.WaitingRelease || field.CancelTick == SimulationTick.Current) { reason = "나비 놓는 중"; return false; }
            if (!field.HasTarget) { reason = "겨눈 나비 없음"; return false; }
            reason = null;
            return true;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            var field = ctx.GetComponent<ButterflyField>();
            ctx.AreaCenter = field != null ? field.TargetPoint : ctx.transform.position;
            ctx.ShowArea = false;
            ctx.SetPhase(0);
            ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 몸은 시작할 때 각도를 지킨다(TryGetFacingYaw)
        }

        private static Vector3 FlatTo(CharacterSkill ctx)
        {
            Vector3 to = ctx.AreaCenter - ctx.transform.position;
            to.y = 0f;
            return to;
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = false;
            Vector3 to = FlatTo(ctx);
            float dist = to.magnitude;
            if (ctx.State != SkillState.Casting || dist < 0.001f) { localMove = Vector2.zero; speed = 0f; return true; }
            // 몸(시야) 기준 방향으로 바꾼다(몸은 시야를 따라 돌므로 매 틱 다시 계산).
            Vector3 local = Quaternion.Inverse(ctx.transform.rotation) * (to / dist);
            localMove = new Vector2(local.x, local.z);
            speed = Mathf.Min(glideSpeed, dist / Time.fixedDeltaTime); // 마지막 틱에 지나치지 않게
            return true;
        }

        public override bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            // 날아가는 동안 화면(시야)을 돌려도 몸은 시작할 때 각도를 그대로 지킨다. 끝나면 다시 시야를 본다.
            yaw = Mathf.Atan2(ctx.Direction.x, ctx.Direction.z) * Mathf.Rad2Deg;
            return ctx.Direction.sqrMagnitude > 0.01f;
        }

        [Header("창틀 뚫기")]
        [Tooltip("창틀 아랫부분(창턱)으로 볼 높이(키 대비, 발 기준)와 두께(m). 이보다 낮고 얇고, 위가 열려 있고 그 위에 창틀 윗부분이 있으면 뚫고 간다.")]
        [Range(0f, 1f)] public float sillMaxHeight = 0.75f;
        [Min(0.05f)] public float sillMaxThickness = 0.8f;
        [Tooltip("창틀 윗부분을 찾는 높이(m, 창턱 위). 이 안에 아무것도 없으면 창문이 아니라 낮은 벽이다(뚫지 않음).")]
        [Min(0.5f)] public float lintelSearch = 5f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// 날아가는 방향 앞의 창턱(창틀 아랫부분)을 찾아 뚫고 지나가게 한다(ColliderPassThrough). 창틀 옆·윗부분과 벽은 그대로 막는다.
        /// 창턱 = 발 기준 키의 sillMaxHeight 아래로 낮고, 날아가는 방향으로 sillMaxThickness 이하로 얇고, 윗면 위가 열려 있고, 그 위 lintelSearch 안에 창틀 윗부분이 있는 것.
        /// </summary>
        private void PassWindowSills(CharacterSkill ctx, Vector3 dir)
        {
            if (!ctx.TryGetComponent(out CharacterController cc)) return;
            Vector3 pos = ctx.transform.position;
            float h = cc.height;
            float look = cc.radius + glideSpeed * 0.12f;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            for (float y = 0.15f; y <= sillMaxHeight * h; y += 0.2f)
            {
                int n = Physics.RaycastNonAlloc(pos + Vector3.up * y, dir, Hits, look, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var col = Hits[i].collider;
                    if (col is CharacterController || col.GetComponentInParent<CharacterIdentity>() != null) continue;
                    Bounds b = col.bounds;
                    float top = b.max.y;
                    if (top - pos.y > sillMaxHeight * h || top - pos.y < 0.1f) continue;           // 창턱 높이가 아니다
                    float thick = Mathf.Abs(dir.x) * b.size.x + Mathf.Abs(dir.z) * b.size.z;
                    if (thick > sillMaxThickness) continue;                                        // 두껍다(상자·벽)
                    Vector3 mid = Hits[i].point + dir * (thick * 0.5f); mid.y = top;
                    if (Physics.CheckSphere(mid + Vector3.up * 0.6f, 0.25f, mask, QueryTriggerInteraction.Ignore)) continue; // 위가 막힘
                    if (!Physics.Raycast(mid + Vector3.up * 0.1f, Vector3.up, lintelSearch, mask, QueryTriggerInteraction.Ignore)) continue; // 위가 하늘 = 낮은 벽
                    if (!ctx.TryGetComponent(out ColliderPassThrough pass)) pass = ctx.gameObject.AddComponent<ColliderPassThrough>();
                    pass.Allow(col);
                }
            }
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            Vector3 to = FlatTo(ctx);
            if (to.magnitude <= arriveDistance) return true; // 도착
            PassWindowSills(ctx, to.normalized); // 창틀 아랫부분은 뚫고 간다
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor != null && motor.HitObstacle && elapsed > 0.04f)
            {
                recovery = bumpRecovery; // 장애물 → 멈추고 툭 떨어짐
                return true;
            }
            return false;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => "나비에게!";

        public override void Complete(GameObject owner) { }
    }
}

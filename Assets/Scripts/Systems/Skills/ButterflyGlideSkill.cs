using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코) 나비에게로(E): 지금 겨누는 나비(ButterflyField.TargetIndex) 자리로 곧게 빠르게 날아간다(glideSpeed). 높낮이도 일직선(1층 → 2층, 2층 → 1층 모두).
    ///   다른 높이로 갈 때 바닥·턱 끝 모서리를 살짝 스치면 뚫고 간다(PassEdges, 모서리에서 edgePassDepth 안). 도착하면 그 바닥에 그대로 선다.
    /// - 겨누는 나비가 없거나 나비를 놓는 중이면(놓기를 E로 취소하고 좌클릭을 아직 안 뗀 동안 포함) 쓰지 않는다.
    /// - 가는 동안 장애물(벽·다른 캐릭터 등)에 부딪히면 거기서 멈추고 툭 떨어진다(잠깐 못 움직임, bumpRecovery).
    ///   단 창틀 아랫부분(창턱)은 뚫고 지나간다(창틀 옆·윗부분은 막힘, PassWindowSills).
    /// - 이동·공격 불가. 날아가는 동안은 위아래 속도를 일직선에 맞춰 중력을 상쇄한다(도착하면 다시 중력).
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
            // 나비까지 일직선(높낮이 포함)을 glideSpeed로 간다 → 수평 빠르기는 그 직선의 수평 몫. 위아래는 Tick이 맞춘다(SteerVertical).
            float total = (ctx.AreaCenter - ctx.transform.position).magnitude;
            float flatSpeed = glideSpeed * (total > 1e-4f ? dist / total : 1f);
            speed = Mathf.Min(flatSpeed, dist / Time.fixedDeltaTime); // 마지막 틱에 지나치지 않게
            return true;
        }

        /// <summary>나비까지 일직선의 위아래 몫만큼 위아래 속도를 준다(중력을 미리 빼 둔다 — 이동이 이번 틱에 중력을 더한다).</summary>
        private void SteerVertical(CharacterSkill ctx, Vector3 to)
        {
            if (!ctx.TryGetComponent(out CharacterMotor motor)) return;
            float total = to.magnitude;
            if (total < 1e-4f) return;
            float vy = glideSpeed * to.y / total;
            vy = Mathf.Clamp(vy, -Mathf.Abs(to.y) / Time.fixedDeltaTime, Mathf.Abs(to.y) / Time.fixedDeltaTime); // 마지막 틱에 지나치지 않게
            motor.Launch(vy - motor.Gravity * Time.fixedDeltaTime);
        }

        [Header("높낮이 — 모서리 뚫기")]
        [Tooltip("다른 높이로 일직선으로 갈 때 바닥·턱 끝 모서리를 이 두께(m)까지는 스쳐도 뚫고 간다(윗면·밑면에서 이 안쪽에 닿은 것). 벽 한가운데는 막힌다.")]
        [Min(0f)] public float edgePassDepth = 0.5f;

        /// <summary>
        /// 일직선으로 가는 길 앞(이번·다음 틱 거리)에 바닥·턱의 끝 모서리가 걸리면 뚫고 간다(ColliderPassThrough). 몸이 그 물건 안에서 빠져나오면 다시 막힌다(나비 자리 바닥에 그대로 선다).
        /// 닿은 자리가 그 물건의 윗면이나 밑면에서 edgePassDepth 안쪽일 때만(모서리를 스침) — 벽 가운데에 부딪히면 그대로 멈춘다.
        /// </summary>
        private void PassEdges(CharacterSkill ctx, Vector3 to)
        {
            if (edgePassDepth <= 0f || !ctx.TryGetComponent(out CharacterController cc)) return;
            float total = to.magnitude;
            if (total < 0.05f || Mathf.Abs(to.y) < 0.2f) return; // 같은 높이로 가면 모서리를 뚫을 일이 없다
            Vector3 dir = to / total;
            Vector3 p0 = cc.transform.TransformPoint(cc.center) + Vector3.up * (cc.height * 0.5f - cc.radius);
            Vector3 p1 = cc.transform.TransformPoint(cc.center) - Vector3.up * (cc.height * 0.5f - cc.radius);
            float look = Mathf.Min(total, glideSpeed * Time.fixedDeltaTime * 3f + cc.radius);
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            int n = Physics.CapsuleCastNonAlloc(p0, p1, cc.radius * 0.95f, dir, Hits, look, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = Hits[i].collider;
                if (col is CharacterController || col.GetComponentInParent<CharacterIdentity>() != null) continue;
                Bounds b = col.bounds;
                Vector3 hp = Hits[i].distance > 0f ? Hits[i].point : cc.transform.position;
                bool nearTop = hp.y >= b.max.y - edgePassDepth, nearBottom = hp.y <= b.min.y + edgePassDepth;
                if (!nearTop && !nearBottom) continue; // 벽 가운데
                // 수평으로도 그 물건의 가장자리 가까이여야 모서리다(바닥 한가운데를 밑에서 뚫고 올라가지 않게).
                float edge = Mathf.Min(Mathf.Min(hp.x - b.min.x, b.max.x - hp.x), Mathf.Min(hp.z - b.min.z, b.max.z - hp.z));
                if (edge > edgePassDepth) continue;
                if (!ctx.TryGetComponent(out ColliderPassThrough pass)) pass = ctx.gameObject.AddComponent<ColliderPassThrough>();
                pass.Allow(col);
            }
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
        [Tooltip("창턱을 찾는 좌우 폭(m, 몸 가운데에서 한쪽으로). 창문 가운데가 아니라 옆쪽으로 비껴 지나가도(몸 옆이 창턱에 걸려도) 뚫린다. 창틀 옆·윗부분·벽은 이 폭 안이어도 막힌다.")]
        [Min(0f)] public float sillSideReach = 0.9f;
        [Tooltip("좌우로 살피는 간격(m).")]
        [Min(0.05f)] public float sillSideStep = 0.15f;

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
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            int steps = Mathf.CeilToInt(sillSideReach / sillSideStep);
            for (int s = -steps; s <= steps; s++)
            {
                Vector3 start = pos + side * Mathf.Clamp(s * sillSideStep, -sillSideReach, sillSideReach); // 몸 가운데 + 좌우로 비껴서도
                for (float y = 0.15f; y <= sillMaxHeight * h; y += 0.2f)
                {
                    int n = Physics.RaycastNonAlloc(start + Vector3.up * y, dir, Hits, look, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        var col = Hits[i].collider;
                        if (IsSill(col, Hits[i].point, dir, pos.y, h, mask)) Pass(ctx, col);
                    }
                }
            }
        }

        private static void Pass(CharacterSkill ctx, Collider col)
        {
            if (!ctx.TryGetComponent(out ColliderPassThrough pass)) pass = ctx.gameObject.AddComponent<ColliderPassThrough>();
            pass.Allow(col);
        }

        /// <summary>col이 창턱인지: 발(footY) 기준 낮고, 얇은 쪽 두께가 sillMaxThickness 이하, 윗면 위가 열려 있고 그 위에 창틀 윗부분이 있음.</summary>
        public bool IsSill(Collider col, Vector3 hitPoint, Vector3 dir, float footY, float h, int mask)
        {
            if (col is CharacterController || col.GetComponentInParent<CharacterIdentity>() != null) return false;
            Bounds b = col.bounds;
            float top = b.max.y;
            if (top - footY > sillMaxHeight * h || top - footY < 0.1f) return false; // 창턱 높이가 아니다(창틀 옆·윗부분·벽은 더 높다)
            // 두께 = 얇은 쪽(비스듬히 지나가도 긴 쪽 길이가 두께로 잡히지 않게).
            float thick;
            if (col is BoxCollider box)
            {
                Vector3 sc = col.transform.lossyScale;
                thick = Mathf.Min(Mathf.Abs(box.size.x * sc.x), Mathf.Abs(box.size.z * sc.z));
            }
            else thick = Mathf.Min(b.size.x, b.size.z);
            if (thick > sillMaxThickness) return false; // 두껍다(상자·벽)
            // 위가 열린 창문인지: 닿은 자리 위, 또는 창턱 가운데 위(옆쪽으로 비껴 닿아 창틀 옆에 가까워도).
            Vector3 atHit = hitPoint + dir * (thick * 0.5f); atHit.y = top;
            Vector3 atCenter = new(b.center.x, top, b.center.z);
            return OpenWindowAbove(atHit, mask) || OpenWindowAbove(atCenter, mask);
        }

        /// <summary>p(창턱 윗면) 위가 비었고(창문 틈), 그 위 lintelSearch 안에 창틀 윗부분이 있는지(없으면 하늘 = 낮은 벽).</summary>
        private bool OpenWindowAbove(Vector3 p, int mask)
        {
            if (Physics.CheckSphere(p + Vector3.up * 0.6f, 0.25f, mask, QueryTriggerInteraction.Ignore)) return false; // 위가 막힘
            return Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, lintelSearch, mask, QueryTriggerInteraction.Ignore);
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            Vector3 to = FlatTo(ctx);
            Vector3 to3 = ctx.AreaCenter - ctx.transform.position;
            if (to.magnitude <= arriveDistance && Mathf.Abs(to3.y) <= 0.6f) // 도착(위아래도 거의 맞음)
            {
                if (ctx.TryGetComponent(out CharacterMotor stopMotor) && stopMotor.VerticalSpeed > 0f) stopMotor.Launch(0f); // 위로 치솟던 속도는 멈춘다
                if (ctx.TryGetComponent(out ColliderPassThrough arrivePass)) arrivePass.RestoreClear(); // 도착한 바닥은 바로 다시 밟는다
                return true;
            }
            if (to.magnitude > 0.05f) PassWindowSills(ctx, to.normalized); // 창틀 아랫부분은 뚫고 간다
            PassEdges(ctx, to3);     // 다른 높이로 갈 때 바닥 끝 모서리는 뚫고 간다
            SteerVertical(ctx, to3); // 나비까지 일직선(높낮이 포함)
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor != null && motor.HitObstacle && elapsed > 0.04f)
            {
                recovery = bumpRecovery; // 장애물 → 멈추고 툭 떨어짐
                if (ctx.TryGetComponent(out ColliderPassThrough bumpPass)) bumpPass.RestoreClear();
                return true;
            }
            return false;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => "나비에게!";

        public override void Complete(GameObject owner) { }
    }
}

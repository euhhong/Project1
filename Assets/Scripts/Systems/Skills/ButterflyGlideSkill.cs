using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코) 나비에게로(E): 지금 겨누는 나비(ButterflyField.TargetIndex) 자리로 대각선 일직선을 따라 곧게 빠르게 날아간다(glideSpeed).
    ///   높낮이가 달라도(2층 → 1층, 1층 → 2층) 처음부터 나비 쪽 직선으로 간다. 중력·땅 붙이기 없이 속도를 통째로 정한다(TryGetVelocityOverride → CharacterMotor.MoveDriven).
    /// - 겨누는 나비가 없거나 나비를 놓는 중이면(놓기를 E로 취소하고 좌클릭을 아직 안 뗀 동안 포함) 쓰지 않는다.
    /// - 날아가는 동안 뚫고 지나가는 것(ColliderPassThrough — 빠져나오면 다시 막힘, IsPassable):
    ///   · 소품·뚱뚱한 물건(분수대·조각상·받침대·상자·VARCO 소품·나무·바위·거울·가구 등 — 맵 뼈대가 아닌 것, IsStructure).
    ///   · 몸 가운데 직선은 틈으로 지나가고 몸 가장자리만 스치는 뼈대(창 아래턱·옆기둥·윗틀, 문틀, 벽 모서리).
    ///   · 눈(발 위 1.7m)에서 나비가 보이는 길을 가리지 않는 뼈대(다리 쪽만 걸리는 기둥 윗부분·바닥 끝).
    ///   · 낮고 얇은 난간·낮은 담(키 1.5m 이하, 두께 0.5m 이하)과 창턱(PassWindowSills). 내려갈 땐 윗면이 허리 아래인 얇은 벽 띠(창밖 아래로 비스듬히 뛰어내리는 길)와 계단도.
    ///   · 지형(Terrain)은 뚫지 않고, 그 아래로 파고들지 않게만 발을 지면 위로 올린다(GlideVelocity).
    ///   · 내려갈 때 떠나는 바닥·계단(바닥판 — 2층 바닥은 1.6m 폭 띠 여러 개라 띠 사이 이음매에 걸려 멈추던 원인).
    ///     올라갈 때는 나비가 놓인 그 바닥(높이 ±0.6m, 나비에서 3m 안)만 뚫는다 — 그 밖의 천장·윗층 바닥 밑면은 막힘.
    ///   · 촉수 기둥·공장장 분신(PassSkillObstacles).
    /// - 맵 뼈대(벽·기둥·탑·지붕·계단 채움·경계)나 다른 캐릭터에 막히면(부딪힘·천장·더 가까워지지 않음) 거기서 끝나고 바로 떨어진다(잠깐 못 움직임, bumpRecovery).
    /// - 도착하면 나비 자리에 선다. 끝난 자리가 물건 안이면 가장 가까운 빈자리로 옮긴다(SettleFree — 물건 속에 끼지 않게).
    /// - 날아가는 동안은 모든 공격·스킬에 면역(IAttackImmunity — 닿지 않은 것으로 봄). 단 맹인의 밀기(지팡이 평타·찌르기)는 받아 밀려나며 날아가기가 끊긴다.
    /// - 이동·공격 불가. 날아가는 동안 화면을 돌려도 몸(모델)은 시작할 때 각도를 지킨다(TryGetFacingYaw). 끝나면 다시 시야를 본다.
    /// 목표 자리는 시전 동안 CharacterSkill.AreaCenter에 둔다(ShowArea는 끔).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Butterfly Glide")]
    public class ButterflyGlideSkill : SkillDefinition
    {
        [Tooltip("날아가는 속도(m/s, 대각선 직선을 따라). 용병 아대 도약(35m/s)의 70%.")]
        [Min(0.1f)] public float glideSpeed = 24.5f;
        [Tooltip("이 거리(m) 안에 들어오면 도착.")]
        [Min(0.05f)] public float arriveDistance = 0.3f;
        [Tooltip("가다가 막혀 멈춘 뒤 못 움직이는 시간(초, 툭 떨어짐).")]
        [Min(0f)] public float bumpRecovery = 0.35f;
        [Tooltip("도착했을 때 이 거리(m, 수평 — 사냥터지기 벽 충격파 범위와 같음) 안에 상대가 있으면 가장 가까운 상대 쪽으로 화면(시야)을 바로 돌린다. 0 = 끔.")]
        [Min(0f)] public float arrivalFaceRadius = 6f;
        [Tooltip("그 뒤 이 시간(초) 동안 화면이 그 상대 움직임을 따라 돈다(직접 돌리기는 잘 안 됨 — LocalPlayerInput.trackInputScale).")]
        [Min(0f)] public float arrivalTrackTime = 0.5f;
        [Tooltip("도착한 뒤 이 시간(초) 동안 받는 피해 배율(0.7 = 30% 덜 아픔, 화면 표시 없음 — TimedCombatBuff).")]
        [Min(0f)] public float arrivalGuardTime = 1f;
        [Range(0f, 1f)] public float arrivalDamageTakenScale = 0.7f;

        [Header("뚫고 지나가기")]
        [Tooltip("이 키(m) 이하이고 얇은(lowBarrierThickness 이하) 맵 뼈대는 난간·낮은 담으로 보고 뚫는다.")]
        [Min(0f)] public float lowBarrierHeight = 1.5f;
        [Min(0f)] public float lowBarrierThickness = 0.5f;
        [Tooltip("올라갈 때 뚫는 바닥: 나비 높이와 이만큼(m) 안이고, 닿은 자리가 나비에서 수평 floorPassNearTarget(m) 안인 바닥만(나비가 놓인 바닥 끝).")]
        [Min(0f)] public float floorPassHeightTolerance = 0.6f;
        [Min(0f)] public float floorPassNearTarget = 3f;
        [Tooltip("눈 높이(m, 발 위). 여기서 나비가 보이는 길을 가리지 않는 뼈대는 몸만 스치는 것으로 보고 뚫는다(ButterflyField 시야와 같은 1.7m).")]
        [Min(0f)] public float eyeLineHeight = 1.7f;
        [Tooltip("옆으로 부딪힌 것의 윗면이 발 위 이 높이(m, 무릎) 이하면 멈추지 않고 뚫는다(바닥 이음매·낮은 턱).")]
        [Min(0f)] public float lowLedgeHeight = 0.6f;

        [Header("막힘")]
        [Tooltip("한 틱에 나비 쪽으로 이만큼(m)도 가까워지지 않으면 걸린 것으로 센다.")]
        [Min(0.01f)] public float stallProgress = 0.08f;
        [Tooltip("걸린 틱이 이만큼 이어지면 끝내고 떨어진다(1틱 = 0.02초).")]
        [Min(1)] public int stallTicks = 3;
        [Tooltip("끝난 자리가 물건 안이면 이 거리(m) 안에서 빈자리를 찾아 옮긴다.")]
        [Min(0f)] public float settleSearchRadius = 2.4f;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[32];

        private static int Mask => ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));

        /// <summary>날아가는 동안은 모든 공격·스킬에 면역 — 맹인(지팡이로 미는 캐릭터, CaneKnockback)의 밀기만 받는다(밀리면 끊김, Tick).</summary>
        public override bool ImmuneTo(CharacterSkill ctx, GameObject attacker) => attacker == null || !attacker.TryGetComponent(out CaneKnockback _);

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
            ctx.Turned = (ctx.AreaCenter - ctx.transform.position).magnitude; // 지금까지 가장 가까웠던 거리(제자리 걸림 판정 — Stalled)
            ctx.SteerSide = 0;                                                // 가까워지지 않은 틱 수
        }

        /// <summary>날아가는 동안 속도를 통째로 정한다: 나비까지 대각선 직선 방향으로 glideSpeed(마지막 틱엔 지나치지 않게). 중력·땅 붙이기 없음.</summary>
        public override bool TryGetVelocityOverride(CharacterSkill ctx, out Vector3 worldVelocity, out bool passThrough)
        {
            worldVelocity = Vector3.zero; passThrough = false;
            if (ctx.TryGetComponent(out Knockback kb) && kb.Active) return false; // 맹인에게 밀리는 중 — 밀려나는 대로
            worldVelocity = GlideVelocity(ctx.transform.position, ctx.AreaCenter, Time.fixedDeltaTime);
            return true;
        }

        /// <summary>
        /// 이번 틱 속도: pos(발)에서 target까지 대각선 직선으로 glideSpeed(마지막 틱엔 지나치지 않게).
        /// 지형(Terrain)만은 그 아래로 파고들지 않게 다음 발 자리를 지면 위로 올린다(마당의 울퉁불퉁한 땅에 걸려 비비지 않게). 바닥·소품·계단은 올리지 않는다(직선 그대로).
        /// </summary>
        public Vector3 GlideVelocity(Vector3 pos, Vector3 target, float dt)
        {
            Vector3 to = target - pos;
            float total = to.magnitude;
            if (total < 1e-4f) return Vector3.zero; // 제자리(곧 도착 처리)
            Vector3 v = to / total * Mathf.Min(glideSpeed, total / dt);
            Vector3 next = pos + v * dt;
            int n = Physics.RaycastNonAlloc(next + Vector3.up * 1f, Vector3.down, TerrainHits, 1.5f, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!(TerrainHits[i].collider is TerrainCollider) || TerrainHits[i].distance <= 0f) continue;
                float ground = TerrainHits[i].point.y + 0.02f;
                if (ground > next.y) v.y += (ground - next.y) / dt;
                break;
            }
            return v;
        }

        private static readonly RaycastHit[] TerrainHits = new RaycastHit[8];

        /// <summary>속도는 TryGetVelocityOverride가 정한다 — 입력으로는 움직이지 않는다.</summary>
        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false;
            return true;
        }

        public override bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            // 날아가는 동안 화면(시야)을 돌려도 몸은 시작할 때 각도를 그대로 지킨다. 끝나면 다시 시야를 본다.
            yaw = Mathf.Atan2(ctx.Direction.x, ctx.Direction.z) * Mathf.Rad2Deg;
            return ctx.Direction.sqrMagnitude > 0.01f;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (ctx.TryGetComponent(out Knockback kb) && kb.Active) // 맹인에게 밀림 → 날아가기가 끊긴다(밀려나는 대로)
            {
                if (ctx.TryGetComponent(out ColliderPassThrough kbPass)) kbPass.RestoreClear();
                return true;
            }
            Vector3 to3 = ctx.AreaCenter - ctx.transform.position;
            Vector3 flat = to3; flat.y = 0f;
            if (to3.magnitude <= arriveDistance || (flat.magnitude <= arriveDistance && Mathf.Abs(to3.y) <= 0.6f)) // 도착
            {
                SettleFree(ctx.gameObject, ctx.AreaCenter); // 나비 자리에 선다(물건 안이면 가장 가까운 빈자리)
                if (ctx.TryGetComponent(out CharacterMotor stopMotor)) stopMotor.Launch(0f);
                if (ctx.TryGetComponent(out ColliderPassThrough arrivePass)) arrivePass.RestoreClear(); // 도착한 바닥은 바로 다시 밟는다
                FaceNearestEnemy(ctx); // 도착: 가까운 상대가 있으면 그쪽으로 화면을 바로 돌린다
                TimedCombatBuff.Give(ctx.gameObject, 1f, arrivalDamageTakenScale, arrivalGuardTime); // 도착 후 잠깐 방어력 증가(받는 피해 줄임)
                return true;
            }

            // 지난 틱에 막혔나: 뼈대 벽·캐릭터에 옆으로 부딪힘, 올라가다 천장, 또는 더 가까워지지 않음 → 끝내고 떨어진다.
            var motor = ctx.GetComponent<CharacterMotor>();
            if (elapsed > 0.04f && motor != null)
            {
                var hit = motor.HitObstacle ? motor.LastObstacle : null;
                bool wall = hit != null && !(hit is TerrainCollider);
                if (wall && !(hit is CharacterController) && hit.GetComponentInParent<CharacterIdentity>() == null && !(hit is MeshCollider { convex: false })
                    && (hit.bounds.max.y <= ctx.transform.position.y + lowLedgeHeight || PassableNow(ctx, hit, to3)))
                {
                    Pass(ctx, hit); wall = false; // 무릎 아래 낮은 턱(바닥 이음매·계단 첫 단 등)이나 뚫어도 되는 것(미리 못 본 것)은 막지 않고 뚫는다
                }
                bool ceiling = motor.HitCeiling && to3.y > 0.1f;
                if (wall || ceiling) return EndBlocked(ctx, out recovery);
            }
            if (Stalled(ctx, to3.magnitude, elapsed)) return EndBlocked(ctx, out recovery);

            // 이번·다음 틱 길에 걸리는 것 중 뚫어도 되는 것은 뚫는다.
            PassAlongPath(ctx, to3);
            if (flat.magnitude > 0.05f) PassWindowSills(ctx, flat.normalized); // 창틀 아랫부분은 뚫고 간다
            PassSkillObstacles(ctx); // 촉수 기둥·공장장 분신은 뚫고 간다
            return false;
        }

        /// <summary>나비까지 거리(remain)가 줄지 않는 틱이 stallTicks 이어졌는지(어딘가에 걸려 제자리에서 비빔).</summary>
        private bool Stalled(CharacterSkill ctx, float remain, float elapsed)
        {
            if (remain < ctx.Turned - stallProgress) { ctx.Turned = remain; ctx.SteerSide = 0; return false; }
            if (elapsed > 0.04f) ctx.SteerSide++;
            return ctx.SteerSide >= stallTicks;
        }

        /// <summary>막혀서 끝: 잠깐 못 움직이고(bumpRecovery) 물건 안이면 빈자리로 옮긴다. 위로 가던 속도는 Complete가 없애 바로 떨어진다.</summary>
        private bool EndBlocked(CharacterSkill ctx, out float recovery)
        {
            recovery = bumpRecovery;
            return true;
        }

        // ───────── 뚫고 지나가기 ─────────

        /// <summary>몸(캡슐)을 나비 쪽 직선으로 이번·다음 틱 거리만큼 쓸어 보고, 걸리는 것 중 뚫어도 되는 것(IsPassable)을 뚫는다.</summary>
        private void PassAlongPath(CharacterSkill ctx, Vector3 to3)
        {
            if (!ctx.TryGetComponent(out CharacterController cc)) return;
            float total = to3.magnitude;
            if (total < 1e-3f) return;
            Vector3 dir = to3 / total;
            Vector3 c = cc.transform.TransformPoint(cc.center);
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            float look = Mathf.Min(total, glideSpeed * Time.fixedDeltaTime * 2f) + cc.radius;
            int n = Physics.CapsuleCastNonAlloc(c + Vector3.up * half, c - Vector3.up * half, cc.radius, dir, Hits, look, Mask, QueryTriggerInteraction.Ignore);
            float footY = ctx.transform.position.y;
            int count = Mathf.Min(n, Hits.Length);
            for (int i = 0; i < count; i++)
            {
                var col = Hits[i].collider;
                if (col == null || col.transform.IsChildOf(ctx.transform) || col is TerrainCollider || col is MeshCollider { convex: false }) continue; // 지형은 뚫지 않는다(ClosestPoint도 안 됨)
                Vector3 p = Hits[i].distance > 0f ? Hits[i].point : col.ClosestPoint(c);
                if (IsPassable(col, p, footY, c, to3, ctx.AreaCenter)) Pass(ctx, col);
            }
        }

        private bool PassableNow(CharacterSkill ctx, Collider col, Vector3 to3)
        {
            if (!ctx.TryGetComponent(out CharacterController cc)) return false;
            Vector3 c = cc.transform.TransformPoint(cc.center);
            return IsPassable(col, col.ClosestPoint(c), ctx.transform.position.y, c, to3, ctx.AreaCenter);
        }

        /// <summary>
        /// 날아가는 동안 col을 뚫어도 되는지(center = 지금 몸 가운데, to3 = 발에서 나비까지). 캐릭터·지형은 안 됨(분신·촉수는 PassSkillObstacles가 따로).
        /// 소품(맵 뼈대가 아닌 것) → 됨. 몸 가운데 직선은 비켜 가고 몸 가장자리만 스치는 뼈대(창문 아래턱·옆기둥·윗틀, 문틀, 벽 모서리) → 됨.
        /// 낮고 얇은 난간·담(발 위 lowBarrierHeight 아래) → 됨. 바닥판 → 내려갈 땐 됨, 올라갈 땐 나비가 놓인 바닥 끝만. 그 밖의 뼈대(가운데 직선이 지나는 벽·기둥·천장 등) → 안 됨.
        /// </summary>
        public bool IsPassable(Collider col, Vector3 hitPoint, float footY, Vector3 center, Vector3 to3, Vector3 target)
        {
            if (col is CharacterController || col.GetComponentInParent<CharacterIdentity>() != null) return false;
            if (col is TerrainCollider || (col is MeshCollider mc && !mc.convex)) return false; // 안에 들어갔는지 잴 수 없어(ClosestPoint) 뚫으면 빠질 수 있다
            if (!IsStructure(col)) return true; // 분수대·조각상·상자·소품 등
            float dy = to3.y;
            float total = to3.magnitude;
            if (total > 1e-3f && !col.Raycast(new Ray(center, to3 / total), out _, total)) return true; // 몸 가운데는 틈으로 지나감 — 가장자리만 스침(창틀·문틀·모서리)
            // 눈에서 나비가 보이는 길을 가리지 않는 것(발·다리 쪽만 걸리는 기둥 윗부분·바닥 끝·턱)은 뚫는다 — 보이는 나비에는 직선으로 간다. 눈앞을 막는 벽은 막힌다.
            Vector3 eye = new(center.x, footY + eyeLineHeight, center.z);
            Vector3 sight = target + Vector3.up * 0.3f - eye;
            if (sight.sqrMagnitude > 1e-4f && !col.Raycast(new Ray(eye, sight.normalized), out _, sight.magnitude)) return true;
            Bounds b = col.bounds;
            // 난간·낮은 담·창턱(발 높이쯤에 있는 것만 — 창 윗틀은 아님). 내려갈 때는 키가 커도 윗면이 허리 아래인 얇은 벽(창 아래 벽 띠)도 — 창밖 아래로 비스듬히 뛰어내리는 길.
            if (Thickness(col) <= lowBarrierThickness && b.max.y <= footY + lowBarrierHeight && (b.size.y <= lowBarrierHeight || dy < -0.3f)) return true;
            if (dy < -0.3f && IsStairs(col)) return true; // 내려갈 때 계단(디딤·경사로·계단 밑 채움)은 높이와 상관없이 지나감 — 계단 위로 비스듬히 내려가는 길
            if (!IsFloor(col)) return false; // 벽·기둥·탑·지붕 경사 등
            if (dy < -0.3f) return b.max.y <= footY + floorPassHeightTolerance; // 내려감: 떠나는 바닥·그 아래 층 바닥을 지나 직선으로
            Vector2 flatToTarget = new(target.x - hitPoint.x, target.z - hitPoint.z);
            return Mathf.Abs(b.max.y - target.y) <= floorPassHeightTolerance && flatToTarget.magnitude <= floorPassNearTarget; // 올라감: 나비가 놓인 바닥 끝만
        }

        private static readonly string[] StructureWords =
        {
            "Wall", "Floor", "Roof", "Ceiling", "Stair", "Step", "Ramp", "Fill", "Column", "Pier", "Pillar", "Porch", "Butt", "Gable",
            "Tower", "Cornice", "Spire", "Fleche", "Perimeter", "Boundary", "Invisible", "Outer", "Corr", "Split", "Walkway", "Terrace", "Rail", "Arch",
        };

        /// <summary>
        /// 맵 뼈대(벽·바닥·지붕·계단·기둥·탑·경계·난간)인지 — 날아가는 동안 막는 것. 지형(Terrain)도 뼈대.
        /// 이름(자기·부모)에 뼈대 낱말이 있거나, 벽처럼 크다(키 2.5m 이상이고 한 변 3m 이상) / 넓은 판(두께 0.6m 이하, 두 변 3m 이상).
        /// VARCO 소품은 이름에 Wall이 있을 때만(무너진 담 등) 뼈대.
        /// </summary>
        public static bool IsStructure(Collider col)
        {
            if (col is TerrainCollider) return true;
            string name = col.name;
            if (name.StartsWith("VARCO_")) return name.Contains("Wall");
            var parent = col.transform.parent;
            if (HasWord(name, PropWords) || (parent != null && HasWord(parent.name, PropWords))) return false; // 분수대·정자·조각상·받침대·상자·방 가구 등
            if (HasWord(name, StructureWords)) return true;
            if (parent != null && !parent.name.StartsWith("VARCO_") && HasWord(parent.name, StructureWords)) return true;
            Bounds b = col.bounds;
            if (b.size.y >= 2.5f && Mathf.Max(b.size.x, b.size.z) >= 3f) return true;
            return b.size.y <= 0.6f && Mathf.Min(b.size.x, b.size.z) >= 3f;
        }

        /// <summary>뼈대 낱말이 들어 있어도 소품으로 보는 것(분수대 받침·기둥, 정자 지붕, 조각상·받침대, 상자, 방 가구, 잔해, 무덤, 탁자).</summary>
        private static readonly string[] PropWords =
        {
            "Fountain", "Gazebo", "Courtyard", "Statue", "Plinth", "Crate", "Rooms", "Collapse_Rubble", "Graveyard", "Table", "Mirror",
        };

        private static bool HasWord(string name, string[] words)
        {
            foreach (var w in words) if (name.Contains(w)) return true;
            return false;
        }

        /// <summary>계단(디딤·경사로·계단 밑 채움)인지.</summary>
        private static bool IsStairs(Collider col)
        {
            string name = col.name;
            if (name.Contains("Step") || name.Contains("Ramp") || name.Contains("Fill")) return true;
            var parent = col.transform.parent;
            return parent != null && parent.name.Contains("Stair");
        }

        /// <summary>바닥판(층 바닥 띠·테라스·지붕 바닥·계단 디딤·경사로)인지.</summary>
        private static bool IsFloor(Collider col)
        {
            string name = col.name;
            if (name.Contains("Floor") || name.Contains("Walkway") || name.Contains("Step") || name.Contains("Ramp")) return true;
            Bounds b = col.bounds;
            return b.size.y <= 0.6f && Mathf.Min(b.size.x, b.size.z) >= 1f;
        }

        /// <summary>얇은 쪽 두께(m, 상자면 돌린 축 기준).</summary>
        private static float Thickness(Collider col)
        {
            if (col is BoxCollider box)
            {
                Vector3 sc = col.transform.lossyScale;
                return Mathf.Min(Mathf.Abs(box.size.x * sc.x), Mathf.Abs(box.size.z * sc.z));
            }
            Bounds b = col.bounds;
            return Mathf.Min(b.size.x, b.size.z);
        }

        private static void Pass(CharacterSkill ctx, Collider col)
        {
            if (!ctx.TryGetComponent(out ColliderPassThrough pass)) pass = ctx.gameObject.AddComponent<ColliderPassThrough>();
            pass.Allow(col);
        }

        /// <summary>날아가는 동안 촉수 기둥(SkillObstacles.Tentacles)·공장장 분신 몸(Summoner가 있는 캐릭터)을 뚫고 간다(ColliderPassThrough). 인형은 원래 걸어서도 지나간다.</summary>
        private static void PassSkillObstacles(CharacterSkill ctx)
        {
            if (!ctx.TryGetComponent(out ColliderPassThrough pass)) pass = ctx.gameObject.AddComponent<ColliderPassThrough>();
            foreach (var col in SkillObstacles.Tentacles) if (col != null) pass.Allow(col);
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
                if (c != null && c.Summoner != null && c.gameObject != ctx.gameObject && c.TryGetComponent(out CharacterController cc)) pass.Allow(cc);
        }

        // ───────── 창틀 ─────────

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
            int mask = Mask;
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

        /// <summary>col이 창턱인지: 발(footY) 기준 낮고, 얇은 쪽 두께가 sillMaxThickness 이하, 윗면 위가 열려 있고 그 위에 창틀 윗부분이 있음.</summary>
        public bool IsSill(Collider col, Vector3 hitPoint, Vector3 dir, float footY, float h, int mask)
        {
            if (col is CharacterController || col.GetComponentInParent<CharacterIdentity>() != null) return false;
            Bounds b = col.bounds;
            float top = b.max.y;
            if (top - footY > sillMaxHeight * h || top - footY < 0.1f) return false; // 창턱 높이가 아니다(창틀 옆·윗부분·벽은 더 높다)
            float thick = Thickness(col); // 얇은 쪽(비스듬히 지나가도 긴 쪽 길이가 두께로 잡히지 않게)
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

        // ───────── 끝난 자리 ─────────

        /// <summary>
        /// 몸을 around(발 자리)에 세운다 — 그 자리에서 몸(캡슐)이 다른 물건과 겹치면, settleSearchRadius 안에서 가장 가까운 빈자리로 옮긴다.
        /// 각 후보는 위에서 내려 비춘 바닥(1m 위 ~ 1m 아래) 위에 세운다(바닥판·물건 속에 끼었으면 그 윗면으로). 빈자리가 없으면 그대로 둔다.
        /// </summary>
        private void SettleFree(GameObject owner, Vector3 around)
        {
            if (owner == null || !owner.TryGetComponent(out CharacterController cc)) return;
            const int Angles = 12;
            for (float r = 0f; r <= settleSearchRadius + 1e-3f; r += 0.3f)
            {
                int count = r < 1e-3f ? 1 : Angles;
                for (int a = 0; a < count; a++)
                {
                    float ang = a * Mathf.PI * 2f / Angles;
                    Vector3 p = around + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                    if (GroundBelow(owner.transform, p + Vector3.up * 1f, 2f, out float groundY)) p.y = groundY + 0.02f;
                    if (!Blocked(cc, owner.transform, p)) { Place(cc, p); return; }
                }
            }
        }

        /// <summary>from에서 아래로 length 안의 가장 가까운 바닥 윗면 높이(자기 몸·캐릭터·벽처럼 선 면은 빼고). 시작점이 물건 안이면 그 물건은 안 잡힌다.</summary>
        private static bool GroundBelow(Transform self, Vector3 from, float length, out float y)
        {
            y = 0f;
            float best = float.MaxValue;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, Hits, length, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance <= 0f || h.distance >= best || h.normal.y < 0.5f) continue;
                if (h.collider.transform.IsChildOf(self) || h.collider is CharacterController || h.collider.GetComponentInParent<CharacterIdentity>() != null) continue;
                best = h.distance; y = h.point.y;
            }
            return best < float.MaxValue;
        }

        /// <summary>발을 feet에 두었을 때 몸(캡슐)이 다른 물건(캐릭터 제외)과 겹치는지.</summary>
        private static bool Blocked(CharacterController cc, Transform self, Vector3 feet)
        {
            Vector3 c = feet + (cc.transform.TransformPoint(cc.center) - cc.transform.position); // 발(루트) 기준 캡슐 가운데
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            int n = Physics.OverlapCapsuleNonAlloc(c + Vector3.up * half, c - Vector3.up * half, cc.radius * 0.95f, Overlaps, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var o = Overlaps[i];
                if (o.transform.IsChildOf(self) || o is CharacterController || o.GetComponentInParent<CharacterIdentity>() != null) continue;
                return true;
            }
            return false;
        }

        private static void Place(CharacterController cc, Vector3 feet)
        {
            if ((cc.transform.position - feet).sqrMagnitude < 1e-6f) return;
            cc.enabled = false;
            cc.transform.position = feet;
            cc.enabled = true;
        }

        // ───────── 도착 ─────────

        /// <summary>도착: arrivalFaceRadius 안(같은 층쯤, 위아래 2.5m)의 가장 가까운 상대 쪽으로 시야를 바로 돌린다(ILookSnap — 카메라도 바로). 없으면 그대로.</summary>
        private void FaceNearestEnemy(CharacterSkill ctx) => ArrivalAutoAim.FaceNearest(ctx.gameObject, arrivalFaceRadius, arrivalTrackTime);

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => string.Empty; // 하는 일을 다시 말하는 글은 쓰지 않는다

        /// <summary>HUD: 손에 쥔 나비 칸(●○)과 다음 한 마리까지 남은 시간.</summary>
        public override string IdleStatusText(CharacterSkill ctx)
        {
            if (!ctx.TryGetComponent(out ButterflyField field) || field.Charges < 0) return null;
            string pips = new string('●', field.Charges) + new string('○', Mathf.Max(0, field.MaxCharges - field.Charges));
            return $"나비 {pips}"; // 다음 한 마리까지는 오른쪽 아래 게이지로
        }

        /// <summary>
        /// 끝날 때(도착·막힘·시간 다 됨): 물건 속이면 가장 가까운 빈자리로 옮기고, 뚫던 것은 다시 막고, 위로 가던 속도는 없앤다(바로 떨어짐).
        /// 맹인에게 밀린 중이면 그대로 둔다(밀려나는 대로 — 뚫던 것은 빠져나오면 다시 막힘).
        /// </summary>
        public override void Complete(GameObject owner)
        {
            if (owner == null || (owner.TryGetComponent(out Knockback kb) && kb.Active)) return;
            if (owner.TryGetComponent(out CharacterController cc) && Blocked(cc, owner.transform, owner.transform.position))
                SettleFree(owner, owner.transform.position);
            if (owner.TryGetComponent(out ColliderPassThrough pass)) pass.RestoreClear();
            if (owner.TryGetComponent(out CharacterMotor motor) && motor.VerticalSpeed > 0f) motor.Launch(0f);
        }
    }
}

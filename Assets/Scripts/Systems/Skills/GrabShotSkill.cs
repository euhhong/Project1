using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 그랩 쏘기(사냥터지기): E를 누르면 몸이 바로 시야(카메라) 방향을 보고,
    /// 1) 오른손을 귀 옆으로 들어 겨눴다가 던지는 준비(windup, 공중에서는 못 씀). E를 꾹 누르고 있으면 던질 자세로 조준한 채 걸어 다닐 수 있고(걷는 동안은 걷기 모션), E를 떼면 던진다. 짧게 누르면 자세를 잡자마자 던진다 → 2) 몸을 앞으로 기울이며 던진 자세로 멈춘 채 그랩이 앞으로 뻗은 오른손에서 용수철처럼 앞으로 곧게 뻗어 나간다(extendSpeed, 최대 range).
    ///    - 길 위에서 처음 닿는 상대를 맞히고(HitEvents) 그 상대를 물고 되감는다(Dragging, dragSpeed): 상대는 그랩 머리를 따라 사냥터지기 앞까지 끌려온다(GrabPull, 그동안 기절).
    ///    - 상대보다 먼저 벽(옆을 보는 면)에 닿으면 그랩이 벽을 문 채 사냥터지기가 벽 쪽으로 끌려간다(Towing, towSpeed). 그랩 머리는 벽에 박힌 채 사슬만 줄어든다.
    ///    - 아무것도 못 맞히거나 바닥 같은 면에 닿으면 그냥 되감긴다(retractSpeed).
    ///    끌어오는/끌려가는 동안은 팔을 당기는 마무리 모션 앞부분(pullMotionShare)을 당긴 정도에 맞춰 재생한다. → 3) 팔을 거둬 마무리한다(followThrough).
    /// 쓰는 동안은 이동·점프·공격 불가(끌려갈 때만 스킬이 몸을 옮긴다), 던진 뒤로는 몸이 던진 방향을 지킨다.
    /// 동작 구간은 TryGetMotionPhase로 알린다(FE가 모션을 구간에 맞춰 멈추고 다시 재생한다).
    /// 그랩이 지금 얼마나 나가 있는지는 CharacterSkill.Reach / Direction / ReachOrigin으로 공개한다(FE가 그랩·사슬을 그 자리에 그린다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Grab Shot")]
    public class GrabShotSkill : SkillDefinition
    {
        private const int Windup = 0, Extending = 1, Retracting = 2, FollowThrough = 3, Dragging = 4, Towing = 5;

        [Tooltip("뻗기 전 준비(팔을 뒤로 당겼다 앞으로 내지르기) 시간(초).")]
        [Min(0f)] public float windup = 0.3f;
        [Tooltip("그랩이 다 돌아온 뒤 팔을 거둬 서 있는 자세로 돌아오는 시간(초).")]
        [Min(0f)] public float followThrough = 0.6f;
        [Tooltip("최대 사거리(m).")]
        [Min(0.5f)] public float range = 16f;
        [Tooltip("뻗는 속도(m/s).")]
        [Min(0.1f)] public float extendSpeed = 45f;
        [Tooltip("못 맞혔을 때 되감는 속도(m/s).")]
        [Min(0.1f)] public float retractSpeed = 60f;
        [Tooltip("판정 굵기(반지름, m).")]
        [Min(0.05f)] public float hitRadius = 0.6f;
        [Tooltip("그랩이 나가는 높이(캐릭터 키 대비). 던진 자세에서 앞으로 뻗은 오른손 높이(서 있을 때 어깨 높이쯤).")]
        [Range(0.1f, 1f)] public float heightRatio = 0.85f;
        [Tooltip("그랩이 나가는 곳: 몸 가운데에서 오른쪽(m) / 앞(m). 던진 자세의 오른손(오른쪽 어깨 줄 앞).")]
        public float sideOffset = 0.3f;
        public float forwardOffset = 0.8f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        [Header("끌어오기 (상대를 맞힘)")]
        [Tooltip("맞힌 상대를 물고 되감는 속도(m/s). 상대가 이 속도로 끌려온다.")]
        [Min(0.1f)] public float dragSpeed = 20f;
        [Tooltip("끌려온 상대가 놓인 뒤 더 기절해 있는 시간(초).")]
        [Min(0f)] public float dragReleaseStun = 0.4f;

        [Header("끌려가기 (벽을 맞힘)")]
        [Tooltip("벽을 문 그랩을 따라 사냥터지기가 끌려가는 속도(m/s).")]
        [Min(0.1f)] public float towSpeed = 20f;
        [Tooltip("벽을 찾는 굵기(반지름, m). 상대 판정(hitRadius)보다 가늘게 — 벽 모서리를 스치기만 해서는 걸리지 않게.")]
        [Min(0.01f)] public float wallProbeRadius = 0.12f;
        [Tooltip("이 각도보다 서 있는 면(바깥 방향이 수평에 가까운 면)만 벽으로 본다. 바닥·경사로에 닿으면 그냥 되감는다. 값 = 바깥 방향의 위쪽 성분 최대값.")]
        [Range(0f, 1f)] public float wallMaxNormalY = 0.6f;
        [Tooltip("그랩 머리(벽)에서 이 거리(m)까지 끌려가면 멈춘다(손이 벽에 닿는 곳).")]
        [Min(0f)] public float towStopReach = 0.1f;

        [Header("조준 (E 꾹 누르기)")]
        [Tooltip("E를 꾹 눌러 조준하는 동안 걷는 속도(걷기 속도 대비).")]
        [Range(0f, 1f)] public float aimMoveSpeedRatio = 1f;

        [Header("조준 보정 (근처를 지나면 꺾어서 문다)")]
        [Tooltip("판정(hitRadius) 밖으로 이만큼(m) 더 떨어져 지나가도 그 상대 쪽으로 꺾어 문다. 0이면 보정 없음.")]
        [Min(0f)] public float snapTolerance = 1f;
        [Tooltip("보정으로 꺾는 최대 각도(도). 이보다 많이 꺾어야 하는 상대는 무시한다.")]
        [Range(0f, 60f)] public float maxSnapAngle = 20f;

        [Tooltip("끌어오는/끌려가는 동안 재생할 마무리 모션(팔을 당겨 거두기)의 앞부분 비율. 나머지는 followThrough 동안.")]
        [Range(0f, 1f)] public float pullMotionShare = 0.6f;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        /// <summary>지금 맞힌 상대를 물고 되감는 중인지(GrabPull이 따라올지 본다).</summary>
        public bool IsDragging(CharacterSkill ctx) => ctx.State == SkillState.Casting && ctx.Phase == Dragging;

        /// <summary>아직 던지기 전(준비·E를 꾹 눌러 조준하는 중)인지. 조준 보조선을 보여 줄 때 쓴다.</summary>
        public bool IsAiming(CharacterSkill ctx) => ctx.State == SkillState.Casting && ctx.Phase == Windup;

        /// <summary>지금 벽을 문 그랩을 따라 끌려가는 중인지.</summary>
        public bool IsTowing(CharacterSkill ctx) => ctx.State == SkillState.Casting && ctx.Phase == Towing;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var state = owner.GetComponent<ICharacterState>();
            if (state != null && !state.IsGrounded) { reason = "공중에서는 못 씀"; return false; }
            reason = null;
            return true;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Windup);
            ctx.Reach = 0f;
            ctx.AreaRadius = 0f; // 끌기 시작한 길이(0 = 끌지 않음)
            ctx.AreaCenter = Vector3.zero; // 조준하며 걷는 입력(준비 중) / 벽에 박힌 자리(끌려가는 중)
            ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized; // 몸 = 시야 방향
            ctx.ReachOrigin = Origin(ctx);
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = false;
            if (ctx.Phase == Towing)
            {
                // 그랩이 문 벽 쪽으로(던진 방향) 끌려간다. 손이 벽에 닿는 곳을 넘지 않게 마지막 틱은 남은 거리만큼만.
                float left = Mathf.Max(0f, ctx.Reach - towStopReach);
                Vector3 local = Quaternion.Inverse(ctx.transform.rotation) * ctx.Direction;
                localMove = new Vector2(local.x, local.z);
                speed = Mathf.Min(towSpeed, left / Time.fixedDeltaTime);
                return true;
            }
            if (ctx.Phase == Windup && ctx.AreaCenter.sqrMagnitude > 0.0001f)
            {
                // E를 꾹 눌러 조준하는 동안은 걷는다(달리기·점프 불가).
                localMove = new Vector2(ctx.AreaCenter.x, ctx.AreaCenter.z);
                speed = ctx.TryGetComponent(out ICharacterState state) ? state.WalkSpeed * aimMoveSpeedRatio : 3.6f;
                return true;
            }
            localMove = Vector2.zero; speed = 0f; // 제자리
            return true;
        }

        public override bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            // 던진 뒤로는 몸이 그랩 방향을 지킨다(시점을 돌려도 사슬과 몸이 어긋나지 않게).
            yaw = Mathf.Atan2(ctx.Direction.x, ctx.Direction.z) * Mathf.Rad2Deg;
            return ctx.Phase != Windup;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            float dt = Time.fixedDeltaTime;
            ctx.ReachOrigin = Origin(ctx); // 끌려가면 손을 따라간다
            switch (ctx.Phase)
            {
                case Windup:
                {
                    // 준비 동안은 몸이 계속 시야를 따라 돈다 → 쏘는 순간(손을 떠날 때) 바라보는 방향으로 날린다.
                    ctx.Direction = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up).normalized;
                    ctx.ReachOrigin = Origin(ctx);
                    // E를 꾹 누르는 동안은 조준(차징): 걸어 다닐 수 있다. 걷기 입력은 AreaCenter(x = 우, z = 앞)에 적어 두고 TryGetMoveOverride가 쓴다.
                    bool held = input.SkillHeld;
                    Vector2 move = held ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero;
                    ctx.AreaCenter = new Vector3(move.x, 0f, move.y);
                    if (move.sqrMagnitude > 0.01f) ctx.PhaseTime = 0f; // 걷는 동안은 던질 자세를 풀고 걷는다(멈추면 다시 자세를 잡는다)
                    // 던질 자세를 다 잡았고 E를 뗐으면 던진다(짧게 한 번 누르면 자세를 잡자마자 바로 던진다).
                    if (ctx.PhaseTime >= windup && !held) ctx.SetPhase(Extending);
                    return false;
                }

                case Extending:
                {
                    float from = ctx.Reach;
                    float to = Mathf.Min(range, ctx.Reach + extendSpeed * dt);
                    bool hitChar = FindVictim(ctx, from, to, out float charAt, out Hurtbox victim);
                    bool hitWall = FindWall(ctx, from, to, out float wallAt, out Vector3 wallNormal);
                    // 조준 보정: 바로 맞히지 못했어도 그랩 머리가 상대 근처(snapTolerance 안)를 지나면 그쪽으로 방향을 꺾어(머리가 옆으로 빡) 문다.
                    if (!hitChar && snapTolerance > 0f && TrySnap(ctx, from, hitWall ? wallAt : to, out charAt, out victim))
                    {
                        hitChar = true;
                        hitWall = false;
                    }
                    if (hitWall && (!hitChar || wallAt < charAt))
                    {
                        ctx.Reach = wallAt;
                        if (wallNormal.y <= wallMaxNormalY && wallAt > towStopReach)
                        {
                            // 벽을 물었다 → 사냥터지기가 벽 쪽으로 끌려간다.
                            ctx.AreaCenter = ctx.ReachOrigin + ctx.Direction * wallAt;
                            ctx.AreaRadius = wallAt;
                            ctx.SetPhase(Towing);
                        }
                        else ctx.SetPhase(Retracting);
                    }
                    else if (hitChar)
                    {
                        ctx.Reach = charAt;
                        Vector3 hookPoint = ctx.ReachOrigin + ctx.Direction * charAt;
                        RaiseHit(ctx, victim, hookPoint);
                        // 문 상대를 끌어온다(되감는 동안 + 놓인 뒤 잠깐 기절).
                        GrabPull.Begin(victim.Owner.gameObject, ctx, this, hookPoint, charAt / dragSpeed + dragReleaseStun);
                        ctx.AreaRadius = charAt;
                        ctx.SetPhase(Dragging);
                    }
                    else
                    {
                        ctx.Reach = to;
                        if (ctx.Reach >= range) ctx.SetPhase(Retracting);
                    }
                    return false;
                }

                case Retracting:
                    ctx.Reach = Mathf.Max(0f, ctx.Reach - retractSpeed * dt);
                    if (ctx.Reach <= 0f) { ctx.Reach = 0f; ctx.SetPhase(FollowThrough); }
                    return false;

                case Dragging:
                    ctx.Reach = Mathf.Max(0f, ctx.Reach - dragSpeed * dt);
                    if (ctx.Reach <= 0f) { ctx.Reach = 0f; ctx.SetPhase(FollowThrough); }
                    return false;

                case Towing:
                {
                    // 그랩 머리는 벽(AreaCenter)에 박혀 있고, 손이 다가간 만큼 사슬이 줄어든다.
                    ctx.Reach = Mathf.Max(0f, Vector3.Dot(Flat(ctx.AreaCenter - ctx.ReachOrigin), ctx.Direction));
                    bool arrived = ctx.Reach <= towStopReach + 0.01f;
                    bool blocked = ctx.PhaseTime > 0.05f && ctx.TryGetComponent(out CharacterMotor motor) && motor.HitObstacle; // 가는 길에 다른 것에 막힘
                    bool timeout = ctx.PhaseTime > ctx.AreaRadius / towSpeed + 0.3f;
                    if (arrived || blocked || timeout) { ctx.Reach = 0f; ctx.SetPhase(FollowThrough); }
                    return false;
                }

                default: // 마무리
                    return ctx.PhaseTime >= followThrough;
            }
        }

        public override bool TryGetMotionPhase(CharacterSkill ctx, out AttackPhase phase, out float progress)
        {
            bool pulled = ctx.AreaRadius > 0f;
            switch (ctx.Phase)
            {
                case Windup: phase = AttackPhase.Windup; progress = windup > 0f ? Mathf.Clamp01(ctx.PhaseTime / windup) : 1f; break; // 꾹 누르는 동안은 던질 자세(끝)에서 멈춘다
                case Extending:
                case Retracting: phase = AttackPhase.Active; progress = 0f; break;
                case Dragging:
                case Towing:
                    // 사슬을 당기는 만큼 팔을 거두는 모션을 앞부분만 재생한다.
                    phase = AttackPhase.Recovery;
                    progress = pullMotionShare * Mathf.Clamp01(1f - ctx.Reach / Mathf.Max(ctx.AreaRadius, 0.001f));
                    break;
                default:
                    phase = AttackPhase.Recovery;
                    float t = followThrough > 0f ? Mathf.Clamp01(ctx.PhaseTime / followThrough) : 1f;
                    progress = pulled ? Mathf.Lerp(pullMotionShare, 1f, t) : t;
                    break;
            }
            return true;
        }

        // 조준하며 걷는 동안은 스킬 모션 없이 걷기 모션 그대로(전신 던지기 모션이면 다리가 미끄러져 보인다).
        public override string AnimationState(CharacterSkill ctx) =>
            ctx.State != SkillState.Casting || (ctx.Phase == Windup && ctx.AreaCenter.sqrMagnitude > 0.0001f) ? string.Empty : id;

        public override string StatusText(CharacterSkill ctx) => ctx.Phase switch
        {
            Windup => ctx.PhaseTime >= windup || ctx.AreaCenter.sqrMagnitude > 0.0001f ? "조준 — E를 떼면 던짐" : "준비",
            Dragging => "끌어오기!",
            Towing => "벽으로!",
            _ => "그랩!",
        };

        public override void Complete(GameObject owner) { }

        /// <summary>그랩이 나가는 시작점(던진 자세의 오른손 = 오른쪽 어깨 줄, 어깨 높이, 몸 앞). 모션과 상관없이 캐릭터 위치·방향으로 정한다.</summary>
        public Vector3 Origin(CharacterSkill ctx) => Origin(ctx.transform, ctx.Direction);

        /// <summary>owner가 dir(수평) 쪽으로 던질 때 그랩이 나가는 시작점(조준 보조선용 — 시전 전에도 쓴다).</summary>
        public Vector3 Origin(Transform owner, Vector3 dir)
        {
            var cc = owner.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            return owner.position + Vector3.up * h * heightRatio + right * sideOffset + dir * forwardOffset;
        }

        /// <summary>origin에서 dir로 던지면 그랩이 갈 수 있는 길이(m): 벽·물건에 먼저 닿으면 그 거리, 아니면 range. 조준 보조선용.</summary>
        public float AimLength(Vector3 origin, Vector3 dir) => FindWall(origin, dir, 0f, range, out float at, out _) ? at : range;

        /// <summary>from ~ to 사이를 굵기 hitRadius로 훑어 처음 닿는 다른 캐릭터를 찾는다(맞히지는 않는다).</summary>
        /// <summary>
        /// 조준 보정. from ~ to 사이를 굵게(hitRadius + snapTolerance) 훑어 근처를 지나는 상대를 찾고, 가까운 순서로
        /// 그 상대 쪽(수평)으로 방향을 꺾어 본다 — 꺾는 각이 maxSnapAngle 이하이고, 꺾은 방향으로 실제 판정에 닿고, 사이에 벽이 없으면 그 방향으로 확정.
        /// 확정하면 ctx.Direction·ReachOrigin이 바뀐다(FE는 그랩 머리를 새 방향의 Reach 자리에 그리므로 옆으로 빡 옮겨 간다). 못 찾으면 그대로 둔다.
        /// </summary>
        private bool TrySnap(CharacterSkill ctx, float from, float to, out float hitAt, out Hurtbox victim)
        {
            hitAt = to; victim = null;
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 oldDir = ctx.Direction, oldOrigin = ctx.ReachOrigin;
            Vector3 start = oldOrigin + oldDir * from;
            float length = Mathf.Max(to - from, 0.001f);
            var hits = Physics.SphereCastAll(start, hitRadius + snapTolerance, oldDir, length, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (!h.collider.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var owner = hurt.Owner;
                if (owner == null || (self != null && !self.CanHit(owner))) continue;
                Vector3 toTarget = Flat(h.collider.bounds.center - oldOrigin);
                if (toTarget.magnitude < 0.3f) continue;
                Vector3 dir = toTarget.normalized;
                if (Vector3.Angle(oldDir, dir) > maxSnapAngle) continue;
                ctx.Direction = dir;
                ctx.ReachOrigin = Origin(ctx);
                if (FindVictim(ctx, 0f, Mathf.Min(range, toTarget.magnitude + hitRadius + 1f), out float at, out var v)
                    && !FindWall(ctx, 0f, at, out _, out _))
                {
                    hitAt = at; victim = v;
                    return true;
                }
                ctx.Direction = oldDir;
                ctx.ReachOrigin = oldOrigin;
            }
            return false;
        }

        private bool FindVictim(CharacterSkill ctx, float from, float to, out float hitAt, out Hurtbox victim)
        {
            hitAt = to; victim = null;
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 start = ctx.ReachOrigin + ctx.Direction * from;
            float length = Mathf.Max(to - from, 0.001f);
            var hits = Physics.SphereCastAll(start, hitRadius, ctx.Direction, length, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (!h.collider.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var owner = hurt.Owner;
                if (owner == null || (self != null && !self.CanHit(owner))) continue;
                float d = h.distance > 0f ? h.distance : 0f;
                if (d < best) { best = d; victim = hurt; }
            }
            if (victim == null) return false;
            hitAt = from + best;
            return true;
        }

        /// <summary>from ~ to 사이를 가늘게(wallProbeRadius) 훑어 처음 닿는 캐릭터가 아닌 것(벽·바닥·물건)을 찾는다.</summary>
        private bool FindWall(CharacterSkill ctx, float from, float to, out float hitAt, out Vector3 normal) =>
            FindWall(ctx.ReachOrigin, ctx.Direction, from, to, out hitAt, out normal);

        private bool FindWall(Vector3 origin, Vector3 dir, float from, float to, out float hitAt, out Vector3 normal)
        {
            hitAt = to; normal = Vector3.zero;
            Vector3 start = origin + dir * from;
            float length = Mathf.Max(to - from, 0.001f);
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            var hits = Physics.SphereCastAll(start, wallProbeRadius, dir, length, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            foreach (var h in hits)
            {
                if (h.distance <= 0f && h.point == Vector3.zero) continue;               // 처음부터 겹쳐 있던 것(손이 벽 속) — 무시
                if (h.collider.GetComponentInParent<CharacterIdentity>() != null) continue; // 캐릭터(자기 몸 포함)는 상대 판정이 맡는다
                if (h.distance < best) { best = h.distance; normal = h.normal; found = true; }
            }
            if (!found) return false;
            hitAt = from + best;
            return true;
        }

        private void RaiseHit(CharacterSkill ctx, Hurtbox victim, Vector3 point)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            HitEvents.Raise(new HitResult
            {
                AttackerId = self != null ? self.Id : 0,
                VictimId = victim.Owner.Id,
                Part = victim.Part,
                Slot = slot,
                AttackId = -ctx.UseCount,
                Tick = SimulationTick.Current,
                Point = point,
            });
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

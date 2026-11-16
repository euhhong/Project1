using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진(포워드 럭비공): 쓰면 바라보는 방향으로 계속 달려 나간다.
    /// - 방향 키·달리기 키와 상관없이 앞으로 chargeSpeed로 달린다(달리기 모션). 점프·공격은 못 한다.
    /// - 옆으로 벽·장애물에 닿으면 멈추고 일반 기절(CharacterStun — 기절 모션, 넘어지지 않음)에 걸린다:
    ///   기절 시간은 달린 거리에 비례: 짧게 달릴수록 짧게(minCrashStun 0.2초, 거의 안 달림) ~ crashRecovery(1.8초, fullCrashDistance 12m 이상).
    /// - 다른 캐릭터에 닿으면(박치기 — 판정은 앞으로는 몸 그대로, 옆으로만 ramSideExtra만큼 넓음, 상대 몸은 상대 앞뒤로 ramDepthExtra만큼 두껍게) 상대는 돌진하던 방향(내 앞)으로, 돌진한 거리에 비례한 세기로 밀려나고(knockSpeedPerMeter, 기절 없음 — 밀리는 동안 움직일 수 있음),
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
        [Tooltip("거의 안 달리고 벽에 부딪혔을 때 기절 시간(초). 달린 거리에 비례해 crashRecovery(fullCrashDistance m 이상)까지 길어진다 — 짧게 달릴수록 페널티가 짧다.")]
        [Min(0f)] public float minCrashStun = 0.2f;
        [Min(0.1f)] public float fullCrashDistance = 12f;

        [Header("캐릭터에 부딪힘(박치기) → 상대 넉백")]
        [Tooltip("돌진하는 동안 박치기 판정을 옆으로만 이만큼(m) 넓힌다 — 앞으로는 넓히지 않는다(정면은 몸이 닿아야 박음). 몸 옆으로 살짝 비켜 선 상대도 박는다.")]
        [Min(0f)] public float ramSideExtra = 0.2f;
        [Tooltip("상대 몸을 상대의 앞·뒤로 이만큼(m) 더 두껍게 본다(박치기 판정만). 캐릭터는 좌우로 넓고 앞뒤로 얇아서, 옆으로 선 상대(내 쪽으로 어깨를 보인 상대)를 스치면 빗나가기 쉬움 — 그 앞뒤를 채운다.")]
        [Min(0f)] public float ramDepthExtra = 0.25f;
        [Tooltip("돌진한 거리 1m마다 넉백 처음 속도(m/s). 밀리는 거리 ≈ 속도 × knockTime ÷ 2.")]
        [Min(0f)] public float knockSpeedPerMeter = 1f;
        [Min(0f)] public float minKnockSpeed = 5f, maxKnockSpeed = 25f;
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
            if (state != null && !state.IsGrounded) { reason = "공중 불가"; return false; }
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
            var motor = owner.GetComponent<CharacterMotor>();
            if (motor == null) return false;
            var self = owner.GetComponent<CharacterIdentity>();

            // 박치기 판정: 앞으로는 몸이 닿는 거리 그대로, 옆으로만 ramSideExtra만큼 넓게 본 범위 안의 상대.
            var victim = UsesRamZone ? FindRamVictim(owner, self) : null; // 옛 판정(광대 로켓)은 몸이 실제로 닿아야
            // 멈춘 공장장 인형: 걸어서는 지나가지만 돌진은 막힌다(벽에 박은 것과 같음).
            bool dollHit = victim == null && elapsed >= obstacleGrace && owner.TryGetComponent(out CharacterController bodyCc)
                           && SkillObstacles.TouchingDoll(owner.transform.position, Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized, bodyCc.radius, bodyCc.height);
            if (victim == null && !dollHit)
            {
                if (elapsed < obstacleGrace || !motor.HitObstacle) return false;
                // 몸이 실제로 닿은 것이 캐릭터인데 박을 수 없는 상대(같은 팀·면역 — 예: 나비에게 날아가는 붉은나비)면 밀지 않고 멈추기만 한다.
                var touched = motor.LastObstacle != null ? motor.LastObstacle.GetComponentInParent<CharacterIdentity>() : null;
                if (touched != null && touched.gameObject != owner)
                {
                    if (self != null && !self.CanHit(touched)) return true;
                    victim = touched;
                }
            }

            // 박치기: 돌진한 거리만큼 세게 밀어내고(기절 없음, 벽에 박혀야 기절), 나는 경직 없이 바로 움직인다(recovery 0).
            if (victim != null && ContactHit(owner, victim, elapsed)) return true; // 옛 판정(광대 로켓): 닿자마자 기절
            if (victim != null)
            {
                float distance = elapsed * chargeSpeed;
                // 넉백 방향 = 돌진하던 쪽(내가 보던 앞). 두 몸의 중심을 잇는 방향은 옆을 스치듯 박으면 엉뚱하게 옆으로 튀어서 쓰지 않는다.
                Vector3 dir = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(victim.transform.position - owner.transform.position, Vector3.up).normalized;
                float speed = Mathf.Clamp(distance * knockSpeedPerMeter, minKnockSpeed, maxKnockSpeed);
                float wallStun = Mathf.Min(wallStunMax, wallStunBase + elapsed * wallStunPerSecond); // 벽에 박히면(돌진한 시간에 비례, 최대 5초)
                Knockback.Push(victim.gameObject, dir * speed, knockTime, stun: false, impactDamage: false, impactStun: wallStun, impactStunWallOnly: true);
                return true;
            }

            if (!CrashPenalty) { recovery = CrashStiffness; return true; } // 벽 기절 페널티가 없는 돌진(광대 로켓): 멈추고 잠깐 경직만(넘어지는 모션 없음)
            // 벽·장애물: 넘어지지 않고 일반 기절(기절 모션). 짧게 달렸으면 잠깐, 길게 달렸으면 달린 거리에 비례해 오래.
            float run = elapsed * chargeSpeed;
            float stunTime = Mathf.Lerp(minCrashStun, crashRecovery, Mathf.InverseLerp(0f, fullCrashDistance, run)); // 짧게 달릴수록 짧게
            CharacterStun.ApplyBreakable(owner, stunTime); // 방향 없음 = 앞으로 숙이는 일반 기절. 깨지는 기절: 맞거나 밀리거나 충격파·다른 기절이 걸리면 바로 풀림
            recovery = 0f; // 넘어짐(Recovering + Fall_1)은 쓰지 않는다
            return true;
        }

        private static readonly Collider[] RamHits = new Collider[32];

        /// <summary>
        /// 박을 수 있는 가장 가까운 상대(없으면 null). 내 몸 = 수평 원(반지름 r), 상대 몸 = 상대 앞뒤로 ramDepthExtra만큼 긴 타원
        /// (좌우 반지름 rv, 앞뒤 반지름 rv + ramDepthExtra — 상대가 어느 쪽을 보든 그 방향대로). 닿는 거리 = r + (그 방향 타원 두께)로,
        /// 앞쪽(몸 중심 앞, 0 ~ 닿는 거리)은 그대로, 옆쪽 거리만 ramSideExtra만큼 더 봐 준다.
        /// </summary>
        private CharacterIdentity FindRamVictim(GameObject owner, CharacterIdentity self)
        {
            if (ramSideExtra <= 0f || !owner.TryGetComponent(out CharacterController cc)) return null;
            Vector3 fwd = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float r = cc.radius * owner.transform.lossyScale.x;
            float h = cc.height * owner.transform.lossyScale.y;
            Vector3 basePos = owner.transform.position;
            float search = r + ramSideExtra + ramDepthExtra + 1f; // 넉넉히 모은 뒤 아래에서 정확히 거른다
            Vector3 a = basePos + Vector3.up * search, b = basePos + Vector3.up * Mathf.Max(search, h);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, search, RamHits, ~0, QueryTriggerInteraction.Collide);
            CharacterIdentity best = null; float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = RamHits[i].GetComponentInParent<CharacterIdentity>();
                if (c == null || c == best || c.gameObject == owner || (self != null && !self.CanHit(c))) continue;
                float rv = c.TryGetComponent(out CharacterController vcc) ? vcc.radius * c.transform.lossyScale.x : 0.35f;
                if (Mathf.Abs(c.transform.position.y - basePos.y) > h) continue; // 위·아래층
                Vector3 to = Vector3.ProjectOnPlane(c.transform.position - basePos, Vector3.up);
                float along = Vector3.Dot(to, fwd), side = Mathf.Abs(Vector3.Dot(to, right));
                // 상대 타원(상대 앞뒤로 긴)의 두께: 내 앞 방향 쪽(touchAlong), 내 옆 방향 쪽(touchSide)
                Vector3 vf = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up);
                vf = vf.sqrMagnitude > 1e-4f ? vf.normalized : fwd;
                float depth = rv + ramDepthExtra;
                float touchAlong = r + Extent(fwd, vf, rv, depth), touchSide = r + Extent(right, vf, rv, depth);
                if (along < 0f || along > touchAlong) continue;  // 앞으로는 넓히지 않음(몸이 닿는 거리까지만), 뒤쪽은 아님
                if (side > touchSide + ramSideExtra) continue;   // 옆으로만 조금 더
                float sa = along / touchAlong, ss = Mathf.Max(0f, side - ramSideExtra) / touchSide;
                if (sa * sa + ss * ss > 1f) continue;            // 앞 모서리는 둥근(타원) 모양 그대로
                float d = to.sqrMagnitude;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>상대 타원(좌우 반지름 halfWidth, 앞뒤 반지름 halfDepth, 앞 = victimFwd)이 방향 u로 얼마나 두꺼운지(중심에서 끝까지).</summary>
        private static float Extent(Vector3 u, Vector3 victimFwd, float halfWidth, float halfDepth)
        {
            float f = Vector3.Dot(u, victimFwd), s = Vector3.Dot(u, Vector3.Cross(Vector3.up, victimFwd));
            return Mathf.Sqrt(halfWidth * halfWidth * s * s + halfDepth * halfDepth * f * f);
        }

        /// <summary>HUD: 돌진 중 멈추는 방법.</summary>
        public override string StatusText(CharacterSkill ctx) => "멈춤: E";

        /// <summary>벽·장애물에 박으면 기절 페널티를 줄지(포워드 럭비공 = 예, 광대 로켓 돌진 = 아니오 — 그냥 멈춤).</summary>
        protected virtual bool CrashPenalty => true;

        /// <summary>CrashPenalty가 없을 때 벽에 박으면 잠깐 못 움직이는 경직(초, 기절 아님 — Recovering). 기본 0.</summary>
        protected virtual float CrashStiffness => 0f;

        /// <summary>HUD: 돌진하는 동안 남은 시간 막대(시전 진행).</summary>
        public override float StatusBarFill(CharacterSkill ctx) => float.NaN;

        /// <summary>박치기 판정을 몸보다 옆으로 넓게 볼지(포워드 = 예). 아니오면 몸이 실제로 닿아야 박는다(광대 로켓 옛 판정).</summary>
        protected virtual bool UsesRamZone => true;

        /// <summary>몸이 상대에 닿았을 때 다르게 처리할지(광대 로켓 옛 판정: 넉백 없이 바로 기절). 처리했으면 true(넉백 박치기를 하지 않음).</summary>
        protected virtual bool ContactHit(GameObject owner, CharacterIdentity victim, float elapsed) => false;

        public override void Complete(GameObject owner) { }
    }
}

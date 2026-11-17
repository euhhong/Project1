using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진(포워드 럭비공): 쓰면 바라보는 방향으로 계속 달려 나간다.
    /// - 방향 키·달리기 키와 상관없이 앞으로 chargeSpeed로 달린다(달리기 모션). 점프·공격은 못 한다.
    /// - 시작 후 wallRubTime(3초) 동안은 벽·물건에 박아도 멈추지 않고 비비며 계속 달린다.
    /// - 그 뒤 옆으로 벽·장애물에 닿으면 멈추고 일반 기절(CharacterStun — 기절 모션, 넘어지지 않음)에 걸린다:
    ///   기절 시간은 달린 거리에 따라 박치기 넉백과 같은 곡선(knockCurve, fullKnockRun): 거의 안 달림 minCrashStun 0.2초 ~ 45m 이상 crashRecovery 1.8초.
    /// - 다른 캐릭터에 닿으면(박치기 — 판정은 앞으로는 몸 그대로, 옆으로만 ramSideExtra만큼 넓음, 상대 몸은 상대 앞뒤로 ramDepthExtra만큼 두껍게) 상대는 돌진하던 방향(내 앞)으로, 돌진한 거리에 따라 곡선으로 확 달라지는 거리만큼 밀려나고(짧게 달리면 0.5m ~ 45m 넘게 달리면 16m, 기절 없음 — 밀리는 동안 움직일 수 있음),
    ///   나는 멈추지 않고(딜레이 없이) 바로 평소처럼 움직인다.
    ///   상대가 밀려나다 벽에 박혀야만(벽 바로 앞 ramWallMargin 안에 들어와도) 기절: 달린 거리에 따라 같은 곡선으로 wallStunMin 0.5초 ~ wallStunMax 5.5초.
    ///   이때 그 상대가 직전 wallRollback(1초) 안에 받은 피해는 되돌린다(CharacterHealth.RollBackRecent).
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

        [Tooltip("돌진을 시작한 뒤 이 시간(초) 동안은 벽·물건에 박아도 멈추지 않고 비비며 계속 달린다(캐릭터 박치기는 그대로). 벽 기절 페널티가 없는 돌진(광대 로켓)은 해당 없음.")]
        [Min(0f)] public float wallRubTime = 3f;

        [Tooltip("벽·장애물에 부딪혀 걸리는 기절 시간(초, 일반 기절 모션) — 멀리(fullKnockRun 이상) 달려 부딪혔을 때.")]
        [Min(0f)] public float crashRecovery = 1.8f;
        [Tooltip("거의 안 달리고 벽에 부딪혔을 때 기절 시간(초). 박치기 넉백과 같은 곡선으로 crashRecovery(fullKnockRun m 이상)까지 길어진다 — 짧게 달리면 아주 짧고 길게 달릴수록 확 길어진다.")]
        [Min(0f)] public float minCrashStun = 0.2f;

        [Header("캐릭터에 부딪힘(박치기) → 상대 넉백")]
        [Tooltip("돌진하는 동안 박치기 판정을 옆으로만 이만큼(m) 넓힌다 — 앞으로는 넓히지 않는다(정면은 몸이 닿아야 박음). 몸 옆으로 살짝 비켜 선 상대도 박는다.")]
        [Min(0f)] public float ramSideExtra = 0.2f;
        [Tooltip("상대 몸을 상대의 앞·뒤로 이만큼(m) 더 두껍게 본다(박치기 판정만). 캐릭터는 좌우로 넓고 앞뒤로 얇아서, 옆으로 선 상대(내 쪽으로 어깨를 보인 상대)를 스치면 빗나가기 쉬움 — 그 앞뒤를 채운다.")]
        [Min(0f)] public float ramDepthExtra = 0.25f;
        [Tooltip("박는 순간 A·D를 누르고 있으면 넉백 방향을 그쪽으로 이만큼(도) 튼다.")]
        [Range(0f, 45f)] public float ramSteerAngle = 15f;
        [Tooltip("밀려나는 거리(m): 거의 안 달리고 박으면 minKnockDistance, fullKnockRun(m) 이상 달려 박으면 maxKnockDistance. 그 사이는 knockCurve 제곱 곡선(짧게 달리면 아주 조금, 길게 달릴수록 확 멀리).")]
        [Min(0f)] public float minKnockDistance = 0.5f, maxKnockDistance = 16f;
        [Min(1f)] public float fullKnockRun = 45f;
        [Min(1f)] public float knockCurve = 1.6f;
        [Tooltip("밀려나는 시간(초): 짧게 달렸을 때 ~ 길게 달렸을 때. 밀리는 동안 상대는 이동은 된다(기절 없음).")]
        [Min(0.05f)] public float minKnockTime = 0.3f, maxKnockTime = 0.9f;
        [Header("넉백으로 벽에 박힘 → 기절")]
        [Tooltip("벽에 박힌 감시자 기절(초): 거의 안 달리고 박으면 wallStunMin, fullKnockRun(m) 이상 달려 박으면 wallStunMax — 밀려나는 거리와 같은 곡선(knockCurve).")]
        [Min(0f)] public float wallStunMin = 0.5f;
        [Min(0f)] public float wallStunMax = 5.5f; // 감시자가 벽에 박혀 걸리는 기절 최대(초)
        [Tooltip("벽을 이만큼(m) 두껍게 본다: 밀려나는 쪽으로 벽·장애물이 이 거리 안에 들어오면 완전히 닿지 않아도 벽에 박힌 것으로 쳐서 기절.")]
        [Min(0f)] public float ramWallMargin = 0.6f;

        /// <summary>박치기로 상대를 밀어냄(돌진한 캐릭터, 부딪힌 자리). 소리·연출(FE)이 구독한다.</summary>
        public static event System.Action<GameObject, Vector3> Rammed;

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
            return RamOrCrash(ctx.gameObject, elapsed, input.Move.x, out recovery); // A·D = 박는 순간 넉백 방향을 살짝 틂
        }

        public override bool ShouldEndEarly(GameObject owner, float elapsed, out float recovery) => RamOrCrash(owner, elapsed, 0f, out recovery);

        /// <summary>돌진 중 매 틱: 캐릭터에 박음(넉백) / 벽에 박음(기절) / 계속. steer = 좌우 입력(-1 A ~ +1 D) — 박는 순간 넉백 방향을 그쪽으로 ramSteerAngle만큼 튼다.</summary>
        private bool RamOrCrash(GameObject owner, float elapsed, float steer, out float recovery)
        {
            recovery = 0f;
            var motor = owner.GetComponent<CharacterMotor>();
            if (motor == null) return false;
            var self = owner.GetComponent<CharacterIdentity>();

            // 박치기 판정: 앞으로는 몸이 닿는 거리 그대로, 옆으로만 ramSideExtra만큼 넓게 본 범위 안의 상대.
            var victim = UsesRamZone ? FindRamVictim(owner, self) : null; // 옛 판정(광대 로켓)은 몸이 실제로 닿아야
            if (victim == null) victim = FindBonusVictim(owner, self); // 광대 로켓: 창틀·판자를 넘는 중인 상대는 조금 멀어도 맞음
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
                else if (Grazing(owner, motor)) return false; // 벽·물건을 비스듬히 스침 → 멈추지 않고 미끄러지며 계속
                else if (CrashPenalty && elapsed < wallRubTime) return false; // 시작 후 wallRubTime초 동안은 벽·물건에 박아도 멈추지 않고 비비며 계속 달린다
            }

            // 박치기: 돌진한 거리만큼 세게 밀어내고(기절 없음, 벽에 박혀야 기절), 나는 경직 없이 바로 움직인다(recovery 0).
            if (victim != null && ContactHit(owner, victim, elapsed)) return true; // 옛 판정(광대 로켓): 닿자마자 기절
            if (victim != null)
            {
                float distance = elapsed * chargeSpeed;
                // 넉백 방향 = 돌진하던 쪽(내가 보던 앞). 두 몸의 중심을 잇는 방향은 옆을 스치듯 박으면 엉뚱하게 옆으로 튀어서 쓰지 않는다.
                Vector3 dir = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up);
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(victim.transform.position - owner.transform.position, Vector3.up).normalized;
                if (Mathf.Abs(steer) > 0.1f) dir = Quaternion.AngleAxis(Mathf.Clamp(steer, -1f, 1f) * ramSteerAngle, Vector3.up) * dir; // A(왼쪽)·D(오른쪽)를 누르고 있으면 그쪽으로 살짝
                // 달린 거리 → 밀려나는 거리(곡선이라 차이가 확 난다: 5m 달림 ≈ 1m, 16m ≈ 3.4m, 32m ≈ 9.6m, 45m 이상 = 16m)
                float t = Mathf.Clamp01(distance / fullKnockRun);
                float pushDist = Mathf.Lerp(minKnockDistance, maxKnockDistance, Mathf.Pow(t, knockCurve));
                float knockTime = Mathf.Lerp(minKnockTime, maxKnockTime, t);
                float speed = 2f * pushDist / knockTime; // 넉백은 처음 속도에서 0까지 줄어든다 → 거리 = 속도 × 시간 ÷ 2
                float wallStun = Mathf.Lerp(wallStunMin, wallStunMax, Mathf.Pow(t, knockCurve)); // 벽에 박히면: 5m 달림 ≈ 0.7초, 16m ≈ 1.5초, 32m ≈ 3.4초, 45m 이상 = 5.5초
                Knockback.Push(victim.gameObject, dir * speed, knockTime, stun: false, impactDamage: false, impactStun: wallStun, impactStunWallOnly: true, wallMargin: ramWallMargin,
                    onWall: wallStun > 0f && wallRollback > 0f ? RollBackOnWall : null); // 벽에 박혀 기절 → 직전 wallRollback초 안에 받은 피해를 되돌림
                Rammed?.Invoke(owner, Vector3.Lerp(owner.transform.position, victim.transform.position, 0.5f) + Vector3.up * 1.1f); // 박치기 소리·연출
                return true;
            }

            if (!CrashPenalty) { recovery = CrashStiffness; return true; } // 벽 기절 페널티가 없는 돌진(광대 로켓): 멈추고 잠깐 경직만(넘어지는 모션 없음)
            // 벽·장애물: 넘어지지 않고 일반 기절(기절 모션). 짧게 달렸으면 잠깐, 길게 달렸으면 달린 거리에 비례해 오래.
            float run = elapsed * chargeSpeed;
            float stunTime = Mathf.Lerp(minCrashStun, crashRecovery, Mathf.Pow(Mathf.Clamp01(run / fullKnockRun), knockCurve)); // 박치기와 같은 곡선: 5m ≈ 0.25초, 16m ≈ 0.5초, 32m ≈ 1.1초, 45m 이상 = 1.8초
            CharacterStun.ApplyBreakable(owner, stunTime); // 방향 없음 = 앞으로 숙이는 일반 기절. 깨지는 기절: 맞거나 밀리거나 충격파·다른 기절이 걸리면 바로 풀림
            recovery = 0f; // 넘어짐(Recovering + Fall_1)은 쓰지 않는다
            return true;
        }

        [Tooltip("박치기로 밀려난 상대가 벽에 박혀 기절하면, 그 상대가 직전 이 시간(초) 안에 받은 피해를 되돌린다(회복). 0이면 안 함.")]
        [Min(0f)] public float wallRollback = 1f;

        private void RollBackOnWall(GameObject victim)
        {
            if (victim != null && victim.TryGetComponent(out CharacterHealth hp)) hp.RollBackRecent(wallRollback);
        }

        [Header("벽·물건에 부딪혀 멈추는 판정")]
        [Tooltip("부딪힌 면이 정면에서 이 각도(도) 안일 때만 멈춘다. 그보다 비스듬히 스치면 미끄러지며 계속 달린다.")]
        [Range(10f, 89f)] public float crashAngle = 45f;
        [Tooltip("비스듬히 스쳐도 실제로 나아가는 빠르기가 돌진 속도의 이 비율보다 느리면(구석에 끼임) 멈춘다.")]
        [Range(0f, 1f)] public float stuckSpeedRatio = 0.3f;

        /// <summary>벽·물건을 비스듬히 스치는 중인지(멈추지 않음): 부딪힌 면이 정면에서 crashAngle보다 비스듬하고, 실제로 잘 나아가고 있으면.</summary>
        private bool Grazing(GameObject owner, CharacterMotor motor)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up).normalized;
            Vector3 n = motor.LastObstacleNormal;
            if (n.sqrMagnitude < 1e-4f || Vector3.Dot(-fwd, n) >= Mathf.Cos(crashAngle * Mathf.Deg2Rad)) return false; // 정면에 가깝게 박음
            if (owner.TryGetComponent(out CharacterController cc))
            {
                Vector3 v = cc.velocity; v.y = 0f;
                if (v.magnitude < chargeSpeed * stuckSpeedRatio) return false; // 구석에 끼어 못 나아감
            }
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

        /// <summary>몸이 닿지 않아도 박치기로 칠 상대(기본 없음 — 광대 로켓: 창틀·판자를 넘는 중인 상대).</summary>
        protected virtual CharacterIdentity FindBonusVictim(GameObject owner, CharacterIdentity self) => null;

        public override void Complete(GameObject owner) { }
    }
}

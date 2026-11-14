using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 공장장(지옥불) 인형 던지기와 자리 바꾸기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신 — IAttackHoldOverride, 짧게 누르는 약공격은 그대로) 화면 가운데(조준점)가 가리키는 곳을 겨눈다 —
    ///   사냥터지기 그랩처럼 커서를 따라가지 않고 시야(카메라 좌우·상하 각도)로만 정한다: 위/아래를 보면 멀리/가까이·높이/낮게, 몸을 돌리면 방향.
    ///   조준점이 창문 틈 너머 바닥을 가리키면 그 바닥, 높은 곳 윗면을 가리키면 그 윗면이 목표다(최대 maxRange).
    ///   목표까지 닿는 포물선을 여러 높이(낮고 빠르게 ~ 높이 띄워)로 날려 보고, 막히지 않고 목표 가까이(landTolerance) 떨어지는 첫 길을 쓴다 —
    ///   창문 틈으로는 낮은 길, 낮은 벽 너머·높은 곳으로는 높은 길. 그런 길이 없으면 기본 길(막히면 거기서 떨어짐).
    ///   겨누는 동안 그 포물선(AimArc)과 멈출 자리(AimLanding)를 보여 준다. 발밑(cancelRadius 안)을 가리키고 떼면 던지지 않는다(취소).
    /// - 떼면 인형을 그 포물선으로 던진다(실제로 날아가는 투사체: 비행 시간 = 수평 거리 ÷ throwSpeed, 중력 gravity).
    ///   바닥(윗면)에 닿으면 멈추고, 벽에 닿으면 수평 속도를 잃고 아래로 떨어진다. 몸 근처를 가리키거나 못 가는 곳이면 던지지 않는다. 인형은 하나 — 다시 던지면 앞 인형은 없어진다.
    /// - TrySwap(E 짧게 — CloneSummonSkill이 부른다): 날아가는 중이든 떨어져 있든 인형 자리와 공장장 자리를 맞바꾼다.
    ///   공장장은 인형이 있던 자리(공중이면 그 공중)로 가서 떨어지고, 인형은 공장장이 있던 자리에 놓인다(멈춤 — 다시 바꾸면 돌아온다).
    ///   바꾼 뒤 다음 착지까지의 낙하는 다치지 않고(ISafeFallSource → FallDamage), 서 있는 자세로 그냥 툭 떨어져 착지 동작 없이 바로 움직인다(IGentleDropSource → CharacterTraversal DollDrop). 그 자리에 몸이 들어갈 곳이 없으면 위로 조금씩 찾고, 없으면 바꾸지 않는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그리기는 FE(DollThrowVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrow : MonoBehaviour, IDollThrow, IAttackHoldOverride, ISafeFallSource, IChargeCancelHint, IGentleDropSource
    {
        [Tooltip("겨눌 수 있는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float maxRange = 30f;
        [Tooltip("던진 인형의 수평 속도(m/s) — 비행 시간 = 수평 거리 ÷ 이 값(minFlight~maxFlight초로 자름). 느릴수록 높이 날아 오래 걸린다.")]
        [SerializeField, Min(1f)] private float throwSpeed = 14f;
        [SerializeField, Min(0.05f)] private float minFlight = 0.4f, maxFlight = 1.8f;
        [Tooltip("인형에 걸리는 중력(m/s²).")]
        [SerializeField, Min(1f)] private float gravity = 20f;
        [Tooltip("인형(구체) 반지름(m) — 벽·바닥에 닿는 판정.")]
        [SerializeField, Min(0.02f)] private float dollRadius = 0.18f;
        [Tooltip("던지는 손 자리(캐릭터 기준 위 m / 오른쪽 m / 앞 m).")]
        [SerializeField] private float handUp = 1.7f, handRight = 0.45f, handForward = 0.3f;
        [Tooltip("이 시간(초)이 지나도록 멈추지 않으면 그 자리에서 멈춘다(허공으로 끝없이 떨어지지 않게).")]
        [SerializeField, Min(0.5f)] private float maxAirTime = 6f;
        [Tooltip("자리를 바꿀 때 몸이 들어갈 곳을 위로 찾는 높이(m).")]
        [SerializeField, Min(0f)] private float swapClearSearch = 1.5f;
        [Tooltip("목표에서 이 거리(m) 안에 떨어지는 길만 \"닿는 길\"로 본다.")]
        [SerializeField, Min(0.1f)] private float landTolerance = 0.7f;
        [Tooltip("조준점이 몸에서 이 거리(m) 안(발밑)이면 떼도 던지지 않는다(취소).")]
        [SerializeField, Min(0f)] private float cancelRadius = 1.5f;
        private Vector3 aimTarget;
        private bool aimCancels;

        private ICharacterInputSource inputSource;
        private ICharacterSkillState skillState;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterState state;
        private CharacterController cc;
        private readonly List<Vector3> arc = new();
        private Vector3 aimVelocity;
        private Vector3 dollPos, dollVel;
        private float dollAir;
        private bool safeFall;
        private int groundedTicks;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        public bool Aiming { get; private set; }
        public IReadOnlyList<Vector3> AimArc => arc;
        public Vector3 AimLanding { get; private set; }
        public bool AimValid { get; private set; }
        public bool DollActive { get; private set; }
        public Vector3 DollPosition => dollPos;
        public bool DollFlying { get; private set; }
        public int ThrowCount { get; private set; }
        public int SwapCount { get; private set; }
        public bool OverridesHeavyHold => isActiveAndEnabled;
        public bool SafeFall => safeFall;
        public bool GentleDrop => safeFall; // 자리를 바꿔 공중에 나타난 뒤 다음 착지까지: 서 있는 자세로 그냥 툭 떨어지고 착지 동작 없음(CharacterTraversal)
        public string ChargeCancelHint => !Aiming ? null : aimCancels ? "떼면 취소" : "취소: 발밑을 보고 떼기";

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            TryGetComponent(out cc);
        }

        private void OnDisable()
        {
            Aiming = false; arc.Clear();
            DollActive = false; DollFlying = false; safeFall = false;
        }

        private void FixedUpdate()
        {
            if (inputSource == null) Awake();
            if (inputSource == null) return;
            CharacterInputFrame input = inputSource.ReadInput();
            if (skillState == null) TryGetComponent(out skillState);
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            state ??= GetComponent<ICharacterState>();
            bool blocked = (skillState != null && skillState.LocksActions) || (stun != null && stun.IsStunned) || (health != null && health.IsDead);

            if (input.HeavyCharging && !blocked)
            {
                Aiming = true;
                UpdateAimTarget(input);
                UpdateAimArc();
            }
            else if (Aiming)
            {
                if (AimValid && !blocked) Throw();
                Aiming = false; arc.Clear();
            }

            if (DollActive && DollFlying) StepDoll(Time.fixedDeltaTime);
            TrackSafeFall();
        }

        // ───────────────────── 던지기 ─────────────────────

        private Vector3 Hand => transform.position + Vector3.up * handUp + transform.right * handRight + transform.forward * handForward;

        /// <summary>
        /// 화면 가운데(조준점)가 가리키는 곳을 목표로 잡는다 — 커서는 쓰지 않는다(사냥터지기 그랩처럼). 광선 = 카메라 자리(AimRayOrigin)에서 시야 각도(LookYaw·LookPitch) 쪽.
        /// 캐릭터·판정은 지나간다. 아무것도 안 닿으면 maxRange 끝 아래 땅, 그것도 없으면 그 허공.
        /// </summary>
        private void UpdateAimTarget(in CharacterInputFrame input)
        {
            Vector3 dir = Quaternion.Euler(input.LookPitch, input.LookYaw, 0f) * Vector3.forward;
            Vector3 eye = input.HasAimRay ? input.AimRayOrigin : transform.position + Vector3.up * handUp;
            // 카메라는 캐릭터 뒤에 있으니 그만큼 더 멀리까지 본다(몸에서 maxRange).
            float camBack = Mathf.Max(0f, Vector3.Dot(transform.position - eye, dir));
            float reach = maxRange + camBack;
            if (RayCast(eye, dir, reach, out var hit))
                aimTarget = hit.point + hit.normal * 0.02f;
            else
            {
                Vector3 end = eye + dir * reach;
                aimTarget = RayCast(end + Vector3.up * 0.5f, Vector3.down, 60f, out var down) ? down.point : end;
            }
            // 좌우는 고정: 던지는 손에서 몸 정면(시야 좌우 각도)으로 곧게 — 목표를 손을 지나는 앞뒤·위아래 평면 위로 옮긴다(카메라가 어깨 옆에 있어 생기는 비스듬함도 없앰).
            // 앞뒤(거리)·위아래(높이)는 조준점이 가리키는 대로. 옮긴 자리가 바닥 위가 아니면 바로 아래 바닥으로(1m 안).
            Vector3 fwd = Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward, right = Vector3.Cross(Vector3.up, fwd);
            float sideOff = Vector3.Dot(aimTarget - Hand, right);
            if (Mathf.Abs(sideOff) > 1e-3f)
            {
                Vector3 moved = aimTarget - right * sideOff;
                aimTarget = RayCast(moved + Vector3.up * 0.5f, Vector3.down, 1.5f, out var snap) ? snap.point : moved;
            }
            Vector3 flat = aimTarget - transform.position; flat.y = 0f;
            aimCancels = flat.magnitude < cancelRadius && Mathf.Abs(aimTarget.y - transform.position.y) < 1.5f; // 발밑을 보면 취소
        }

        /// <summary>
        /// 목표까지 닿는 포물선을 여러 비행 시간(낮고 빠르게 ~ 높이 띄워)으로 날려 보고, 막히지 않고 목표 가까이 떨어지는 첫 길을 쓴다.
        /// 창문 틈은 낮은 길로, 낮은 벽 너머·높은 곳은 높은 길로 넘어간다. 그런 길이 없으면 기본 길(막히면 거기서 떨어짐)을 보여 준다.
        /// </summary>
        private void UpdateAimArc()
        {
            Vector3 from = Hand, to = aimTarget + Vector3.up * dollRadius;
            Vector3 flat = to - from; flat.y = 0f;
            float t0 = Mathf.Clamp(flat.magnitude / throwSpeed, minFlight, maxFlight);
            float[] tries = { t0, t0 * 0.75f, t0 * 0.55f, t0 * 0.4f, t0 * 1.3f, t0 * 1.6f, t0 * 2f };
            bool found = false;
            foreach (float k in tries)
            {
                float t = Mathf.Max(0.2f, k);
                Vector3 v0 = flat / t;
                v0.y = (to.y - from.y + 0.5f * gravity * t * t) / t;
                if (Simulate(from, v0, out Vector3 land) && (land - to).sqrMagnitude <= landTolerance * landTolerance)
                {
                    aimVelocity = v0; found = true; break;
                }
            }
            if (!found)
            {
                aimVelocity = flat / t0;
                aimVelocity.y = (to.y - from.y + 0.5f * gravity * t0 * t0) / t0;
            }

            arc.Clear();
            bool landed = Simulate(from, aimVelocity, out Vector3 end, arc);
            AimLanding = end;
            AimValid = !aimCancels && landed;
        }

        /// <summary>처음 자리·속도로 날려 본다(멈출 때까지 또는 maxAirTime). 멈췄으면 true. path가 있으면 지나간 자리를 담는다.</summary>
        private bool Simulate(Vector3 p, Vector3 v, out Vector3 end, List<Vector3> path = null)
        {
            const float dt = 0.02f;
            path?.Add(p);
            bool landed = false;
            for (float air = 0f; air < maxAirTime && !landed; air += dt)
            {
                landed = Step(ref p, ref v, dt);
                if (path != null && path.Count < 400) path.Add(p);
            }
            end = p;
            return landed;
        }

        private bool RayCast(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, dist, Mask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (Ignored(h.collider) || h.distance >= bestD) continue;
                best = h; bestD = h.distance;
            }
            return bestD < float.MaxValue;
        }

        private void Throw()
        {
            dollPos = Hand;
            dollVel = aimVelocity;
            dollAir = 0f;
            DollActive = true; DollFlying = true;
            ThrowCount++;
        }

        private void StepDoll(float dt)
        {
            dollAir += dt;
            if (Step(ref dollPos, ref dollVel, dt) || dollAir > maxAirTime) { DollFlying = false; dollVel = Vector3.zero; }
        }

        /// <summary>
        /// 한 걸음 날린다. 바닥(윗면)에 닿으면 그 위에 멈추고 true. 벽·천장에 닿으면 면 앞에 멈춰 세우고 수평 속도를 잃는다(아래로 떨어짐, 천장이면 위 속도도 잃음).
        /// 캐릭터·트리거·판정은 지나간다.
        /// </summary>
        private bool Step(ref Vector3 p, ref Vector3 v, float dt)
        {
            v.y -= gravity * dt;
            Vector3 move = v * dt;
            float dist = move.magnitude;
            if (dist < 1e-5f) return false;
            if (SphereCast(p, move / dist, dist, out var hit))
            {
                p = hit.point + hit.normal * (dollRadius + 0.01f);
                if (hit.normal.y > 0.6f) { v = Vector3.zero; return true; } // 바닥
                v.x = 0f; v.z = 0f;                                          // 벽: 아래로 떨어진다
                if (hit.normal.y < -0.3f) v.y = Mathf.Min(v.y, 0f);          // 천장
                return false;
            }
            p += move;
            return false;
        }

        private bool SphereCast(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.SphereCastNonAlloc(origin, dollRadius, dir, Hits, dist, Mask, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance <= 0f && h.point == Vector3.zero) continue; // 처음부터 겹침
                if (Ignored(h.collider) || h.distance >= bestD) continue;
                best = h; bestD = h.distance;
            }
            return bestD < float.MaxValue;
        }

        // ───────────────────── 자리 바꾸기 ─────────────────────

        /// <summary>인형과 자리를 바꾼다(E 짧게). 인형이 없거나 막혔거나 몸이 들어갈 곳이 없으면 false.</summary>
        public bool TrySwap()
        {
            if (!DollActive || cc == null) return false;
            if (TryGetComponent(out ICharacterTraversalState tr) && tr.LocksActions) return false; // 오르는 중·매달림
            if ((stun != null && stun.IsStunned) || (health != null && health.IsDead)) return false;
            Vector3 feet = dollPos - Vector3.up * dollRadius;
            bool found = false;
            for (float up = 0f; up <= swapClearSearch + 1e-3f; up += 0.25f)
                if (Clear(feet + Vector3.up * up)) { feet += Vector3.up * up; found = true; break; }
            if (!found) return false;

            Vector3 old = transform.position;
            cc.enabled = false;
            transform.position = feet;
            cc.enabled = true;
            Physics.SyncTransforms();
            if (TryGetComponent(out CharacterMotor motor)) motor.Launch(0f); // 공중이면 거기서 떨어지기 시작
            dollPos = old + Vector3.up * dollRadius;
            dollVel = Vector3.zero;
            DollFlying = false;
            safeFall = true; groundedTicks = 0; // 다음 착지까지 다치지 않는다
            SwapCount++;
            return true;
        }

        /// <summary>바꾼 뒤 땅에 두 틱 이상 서 있으면 다치지 않는 낙하 표시를 끈다(FallDamage가 착지 틱에 묻는다).</summary>
        private void TrackSafeFall()
        {
            if (!safeFall) return;
            bool grounded = state != null && state.IsGrounded;
            groundedTicks = grounded ? groundedTicks + 1 : 0;
            if (groundedTicks >= 3) safeFall = false;
        }

        /// <summary>발이 feet에 있을 때 몸(CharacterController 캡슐)이 들어갈 빈 곳인지.</summary>
        private bool Clear(Vector3 feet)
        {
            float r = cc.radius * 0.95f;
            Vector3 a = feet + Vector3.up * (r + 0.05f), b = feet + Vector3.up * Mathf.Max(cc.height - r, r + 0.06f);
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, Overlaps, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Overlaps[i])) return false;
            return true;
        }

        private static int Mask => ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
        private static bool Ignored(Collider c) => c is CharacterController || c.GetComponentInParent<CharacterIdentity>() != null;
    }
}

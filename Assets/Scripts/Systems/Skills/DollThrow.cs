using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 공장장(지옥불) 인형 던지기와 자리 바꾸기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신 — IAttackHoldOverride, 짧게 누르는 약공격은 그대로) 시야·커서가 가리키는 땅을 겨눈다(GroundPlacementPreview, 최대 maxRange).
    ///   겨누는 동안 던지면 인형이 날아갈 포물선(AimArc)과 멈출 자리(AimLanding)를 계산해 둔다 — 벽에 막히면 거기서 떨어지는 것까지.
    /// - 떼면 인형을 그 포물선으로 던진다(실제로 날아가는 투사체: 비행 시간 = 수평 거리 ÷ throwSpeed, 중력 gravity).
    ///   바닥(윗면)에 닿으면 멈추고, 벽에 닿으면 수평 속도를 잃고 아래로 떨어진다. 몸 근처를 가리키거나 못 가는 곳이면 던지지 않는다. 인형은 하나 — 다시 던지면 앞 인형은 없어진다.
    /// - TrySwap(E 짧게 — CloneSummonSkill이 부른다): 날아가는 중이든 떨어져 있든 인형 자리와 공장장 자리를 맞바꾼다.
    ///   공장장은 인형이 있던 자리(공중이면 그 공중)로 가서 떨어지고, 인형은 공장장이 있던 자리에 놓인다(멈춤 — 다시 바꾸면 돌아온다).
    ///   바꾼 뒤 다음 착지까지의 낙하는 다치지 않는다(ISafeFallSource → FallDamage). 그 자리에 몸이 들어갈 곳이 없으면 위로 조금씩 찾고, 없으면 바꾸지 않는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그리기는 FE(DollThrowVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrow : MonoBehaviour, IDollThrow, IAttackHoldOverride, ISafeFallSource, IChargeCancelHint
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
        [SerializeField] private GroundPlacementPreview placement = new();

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
        public string ChargeCancelHint => !Aiming ? null : placement.Cancels ? "떼면 취소" : "취소: 몸 쪽으로 끌고 떼기";

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            TryGetComponent(out cc);
            placement.maxPlaceDistance = maxRange; placement.clampToMax = true;
            placement.ledgeSnapHeight = 1f; placement.maxRiseHeight = 7f;
            placement.requireStandSpace = false; placement.seeThroughWalls = false;
        }

        private void OnDisable()
        {
            Aiming = false; placement.Clear(); arc.Clear();
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
                if (!Aiming) placement.Begin();
                Aiming = true;
                placement.Update(transform, input);
                UpdateAimArc();
            }
            else if (Aiming)
            {
                if (AimValid && !blocked) Throw();
                Aiming = false; placement.Clear(); arc.Clear();
            }

            if (DollActive && DollFlying) StepDoll(Time.fixedDeltaTime);
            TrackSafeFall();
        }

        // ───────────────────── 던지기 ─────────────────────

        private Vector3 Hand => transform.position + Vector3.up * handUp + transform.right * handRight + transform.forward * handForward;

        /// <summary>겨누는 곳까지 갈 처음 속도를 정하고, 그대로 날려 보며 길과 멈출 자리를 미리 계산한다(벽에 막히면 거기서 떨어짐까지).</summary>
        private void UpdateAimArc()
        {
            arc.Clear();
            Vector3 from = Hand, to = placement.Point + Vector3.up * dollRadius;
            Vector3 flat = to - from; flat.y = 0f;
            float t = Mathf.Clamp(flat.magnitude / throwSpeed, minFlight, maxFlight);
            aimVelocity = flat / t;
            aimVelocity.y = (to.y - from.y + 0.5f * gravity * t * t) / t;

            Vector3 p = from, v = aimVelocity;
            arc.Add(p);
            const float dt = 0.02f;
            bool landed = false;
            for (float air = 0f; air < maxAirTime && !landed; air += dt)
            {
                landed = Step(ref p, ref v, dt);
                if (arc.Count < 400) arc.Add(p);
            }
            AimLanding = p;
            AimValid = placement.Valid && !placement.Cancels && landed;
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

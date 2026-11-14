using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 공장장(지옥불) 인형 던지기와 자리 바꾸기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신 — IAttackHoldOverride, 짧게 누르는 약공격은 그대로) 화면 가운데(조준점)가 가리키는 곳을 겨눈다 —
    ///   사냥터지기 그랩처럼 커서를 따라가지 않고 시야(카메라 좌우·상하 각도)로만 정한다: 위/아래를 보면 멀리/가까이·높이/낮게, 몸을 돌리면 방향.
    ///   조준점이 창문 틈 너머 바닥을 가리키면 그 바닥, 높은 곳 윗면을 가리키면 그 윗면이 목표다(광선 maxRange — 사실상 제한 없음). 하늘을 가리키면 시야 방향 그대로 skyThrowSpeed로 던진다(높이 볼수록 멀리).
    ///   목표까지 닿는 포물선을 여러 높이(낮고 빠르게 ~ 높이 띄워)로 날려 보고, 막히지 않고 목표 가까이(landTolerance) 떨어지는 첫 길을 쓴다 —
    ///   창문 틈으로는 낮은 길, 낮은 벽 너머·높은 곳으로는 높은 길. 그런 길이 없으면 기본 길(막히면 거기서 떨어짐).
    ///   겨누는 동안 그 포물선(AimArc)과 멈출 자리(AimLanding)를 보여 준다. 발밑(cancelRadius 안)을 가리키고 떼면 던지지 않는다(취소).
    /// - 먼저 앞의 한 지점(목표)을 정하고 던지는 손에서 그 지점까지 포물선으로 잇는다 — 이 선이 보조선(AimArc)이고 떨어질 자리(AimLanding)는 그 지점.
    /// - 떼면 인형이 그 포물선을 그대로 따라가(벽은 지나감) 그 지점에 떨어진다(비행 시간 = 수평 거리 ÷ throwSpeed, minFlight~maxFlight초, 중력 gravity).
    ///   바닥(윗면)에 닿으면 멈추고, 벽에 닿으면 수평 속도를 잃고 아래로 떨어진다. 몸 근처를 가리키거나 못 가는 곳이면 던지지 않는다. 인형은 하나 — 다시 던지면 앞 인형은 없어진다.
    /// - 좌클릭 차징 = 인형 던지기 자세 + 포물선·원. 좌클릭을 누른 채 마우스 = 떨어질 자리 조절(좌우 = 방향, 상하 = 거리 aimLookScale배). 우클릭을 누른 채 마우스 = 시야 높이(위쪽 한계 열림).
    ///   차징을 시작하면 그 순간 시야에서 aimViewRaise도만 위로 올려 고정(IChargeLook — LocalPlayerInput).
    ///   거리는 좌클릭을 누른 채 마우스를 위로 끌면 멀리 / 아래로 끌면 가까이(시야 상하 1도당 dragPerDegree m — 안토니오 음표 놓기 감도, 맵 끝 maxAimDistance까지 — 시점이 위아래 한계에 닿아도 계속 끌림), 방향은 몸 정면. 처음 거리 = 누르기 시작할 때 조준점까지.
    /// - TrySwap(E 짧게 — CloneSummonSkill이 부른다): 누른 순간의 인형 자리를 도착점으로 정하고, swapDelay초 동안 제자리에서 몸을 떤 뒤(FE) 인형 자리와 공장장 자리를 맞바꾼다.
    ///   공장장은 인형이 있던 자리(공중이면 그 공중)로 가서 떨어지고, 인형은 공장장이 있던 자리에 놓인다(멈춤 — 다시 바꾸면 돌아온다).
    ///   바꾼 뒤 다음 착지까지의 낙하는 다치지 않고(ISafeFallSource → FallDamage), 서 있는 자세로 그냥 툭 떨어져 착지 동작 없이 바로 움직인다(IGentleDropSource → CharacterTraversal DollDrop). 그 자리에 몸이 들어갈 곳이 없으면 위로 조금씩 찾고, 없으면 바꾸지 않는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그리기는 FE(DollThrowVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrow : MonoBehaviour, IDollThrow, IAttackHoldOverride, ISafeFallSource, IChargeCancelHint, IGentleDropSource, IChargeLook, IMoveSpeedLimit, IActionLock, IMoveSpeedBoost
    {
        [Tooltip("좌클릭을 꾹 누르는 동안 마우스로 시점(= 포물선)을 움직이는 감도 배율(우클릭 시점 돌리기 감도 기준). 우클릭 없이 좌클릭만으로 겨눈다.")]
        [SerializeField, Min(0.1f)] private float aimLookScale = 1f;
        [Tooltip("좌클릭 차징을 시작하는 순간 그때 시야에서 이만큼(도) 위로 올리고 그 높이에 고정 — 우클릭을 누른 채 마우스로만 높이를 바꾼다(위쪽 한계 열림).")]
        [SerializeField, Range(0f, 45f)] private float aimViewRaise = 10f;
        [Tooltip("E를 짧게 누른 뒤 실제로 자리를 바꾸기까지(초) — 그동안 제자리에서 몸을 떤다. 도착은 누른 순간의 인형 자리.")]
        [SerializeField, Min(0f)] private float swapDelay = 0.35f;
        [Tooltip("자리를 바꾼 직후 짧은 경직(초) — 이동·공격·스킬 불가, 보이는 표시 없음(IActionLock).")]
        [SerializeField, Min(0f)] private float postSwapLock = 0.3f;
        [Tooltip("공중의 인형과 바꿔 떨어져 내려왔으면 착지한 순간부터 이 시간(초) 동안 이동 속도 배율.")]
        [SerializeField, Min(0f)] private float landBoostTime = 2f;
        [SerializeField, Min(1f)] private float landBoostMultiplier = 1.5f;
        private float lockLeft, boostLeft;
        private bool boostArmed, boostAirSeen;
        private float swapLeft;
        private Vector3 swapFeet;
        [Tooltip("겨눌 수 있는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float maxRange = 400f;
        [Tooltip("조준점이 하늘(아무것도 안 닿음)이면 시야 방향 그대로 이 속도(m/s)로 던진다 — 거리 제한 없음, 45도로 올려 보면 가장 멀리(속도² ÷ 중력 ≈ 100m).")]
        [SerializeField, Min(1f)] private float skyThrowSpeed = 45f;
        private bool aimBySky;
        private Vector3 skyVelocity;
        [Tooltip("던진 인형의 수평 속도(m/s) — 비행 시간 = 수평 거리 ÷ 이 값(minFlight~maxFlight초로 자름). 느릴수록 높이 날아 오래 걸린다.")]
        [SerializeField, Min(1f)] private float throwSpeed = 16.5f; // 14 → 16.5: 비행 시간 ×0.85 → 꼭대기 높이 약 72%(완만하게)
        [SerializeField, Min(0.05f)] private float minFlight = 0.4f, maxFlight = 3.4f; // 꼭대기 높이 ≈ 중력 × 비행시간² ÷ 8(평지)
        [Tooltip("겨누는 거리 끌기: 좌클릭을 꾹 누른 채 마우스를 위로 끌면 멀리, 아래로 끌면 가까이 — 시야 상하 1도당 이 거리(m, 안토니오 음표 놓기와 같은 0.25). 맵 끝(maxAimDistance)까지.")]
        [SerializeField, Min(0.001f)] private float dragPerDegree = 0.25f;
        [SerializeField, Min(1f)] private float maxAimDistance = 300f;
        [SerializeField, Min(0.1f)] private float minAimDistance = 1f;
        private float aimDistance;
        private float aimFlightTime, throwTime;
        private Vector3 throwFrom, throwTo, throwVel;
        [Tooltip("인형에 걸리는 중력(m/s²).")]
        [SerializeField, Min(1f)] private float gravity = 20f;
        [Tooltip("인형(구체) 반지름(m) — 벽·바닥에 닿는 판정.")]
        [SerializeField, Min(0.02f)] private float dollRadius = 0.18f;
        [Tooltip("던지는 손 자리(캐릭터 기준 위 m / 오른쪽 m / 앞 m).")]
        [SerializeField] private float handUp = 1.7f, handRight = 0.45f, handForward = 0.3f;
        [Tooltip("이 시간(초)이 지나도록 멈추지 않으면 그 자리에서 멈춘다(허공으로 끝없이 떨어지지 않게).")]
        [SerializeField, Min(0.5f)] private float maxAirTime = 8f;
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
        public string ChargeCancelHint => !Aiming ? null : aimCancels ? "떼면 취소" : "취소: E 또는 몸 쪽으로 끌고 떼기 · 우클릭 = 시야 높이";
        private bool waitRelease;
        /// <summary>차징을 E로 취소한 틱 / 취소한 뒤 좌클릭을 아직 떼지 않음. 이 동안 E 스킬(분신·자리 바꾸기)은 나가지 않는다.</summary>
        public int CancelTick { get; private set; } = int.MinValue;
        public bool WaitingRelease => waitRelease;
        /// <summary>차징 중이거나 방금 취소함 — E 스킬을 막는다(CloneSummonSkill).</summary>
        public bool BlocksSkill => isActiveAndEnabled && (Aiming || waitRelease || CancelTick == SimulationTick.Current);
        public bool LookWhileCharging => isActiveAndEnabled && !waitRelease; // 좌클릭 차징 = 좌클릭 끌기로 자리 조절 + 우클릭 끌기로 시야 높이(LocalPlayerInput), E로 취소하면 끝
        public float ChargeLookScale => aimLookScale;
        public float ChargeViewRaise => aimViewRaise;
        public bool SwapPending => swapLeft > 0f;
        public float SwapProgress => swapDelay > 0f && SwapPending ? 1f - swapLeft / swapDelay : 0f;

        /// <summary>자리를 바꾸려고 기다리는 동안은 제자리(움직이지 않음).</summary>
        public bool TryGetSpeedLimit(out float maxSpeed) { maxSpeed = 0f; return SwapPending || lockLeft > 0f; }

        /// <summary>자리를 바꾼 직후 짧은 경직(보이는 표시 없음) — 공격·스킬 불가.</summary>
        public bool ActionsLocked => isActiveAndEnabled && (SwapPending || lockLeft > 0f);

        /// <summary>공중의 인형과 바꿔 떨어져 내려온 뒤 landBoostTime초 동안 빠르게.</summary>
        public bool TryGetSpeedBoost(out float multiplier) { multiplier = landBoostMultiplier; return boostLeft > 0f; }

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            TryGetComponent(out cc);
            if (TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기다리는 동안 멈추게(IMoveSpeedLimit)
        }

        private void OnDisable()
        {
            Aiming = false; arc.Clear();
            DollActive = false; DollFlying = false; safeFall = false; swapLeft = 0f; waitRelease = false; lockLeft = boostLeft = 0f; boostArmed = false;
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

            if (waitRelease)
            {
                if (!input.HeavyCharging) waitRelease = false; // E로 취소한 뒤 좌클릭을 뗄 때까지 다시 겨누지 않는다
            }
            else if (input.HeavyCharging && !blocked)
            {
                if (!Aiming) StartAimDistance(input); // 누르기 시작: 지금 화면 가운데가 가리키는 곳까지의 거리에서 시작
                Aiming = true;
                UpdateDragTarget(input);
                UpdateAimArc();
                if (input.Skill)
                {
                    // 차징 중 E = 취소(던지지 않음, 분신·자리 바꾸기도 안 나감 — CloneSummonSkill.CanStart가 본다)
                    Aiming = false; arc.Clear(); waitRelease = true;
                    CancelTick = SimulationTick.Current;
                }
            }
            else if (Aiming)
            {
                if (AimValid && !blocked) Throw();
                Aiming = false; arc.Clear();
            }

            if (SwapPending)
            {
                bool cut = (stun != null && stun.IsStunned) || (health != null && health.IsDead)
                           || (TryGetComponent(out ICharacterTraversalState tr) && tr.LocksActions);
                swapLeft -= Time.fixedDeltaTime;
                if (cut) swapLeft = 0f;                    // 기절·쓰러짐·오르기: 바꾸지 않음
                else if (swapLeft <= 0f) { swapLeft = 0f; DoSwap(); }
            }
            if (DollActive && DollFlying) StepDoll(Time.fixedDeltaTime);
            TrackSafeFall();
            TrackSwapAfter(Time.fixedDeltaTime);
        }

        // ───────────────────── 던지기 ─────────────────────

        private Vector3 Hand => transform.position + Vector3.up * handUp + transform.right * handRight + transform.forward * handForward;

        /// <summary>겨누기 시작: 화면 가운데(조준점)가 가리키는 곳까지의 수평 거리에서 시작한다(하늘이면 40m).</summary>
        private void StartAimDistance(in CharacterInputFrame input)
        {
            UpdateAimTarget(input);
            Vector3 flat = aimTarget - transform.position; flat.y = 0f;
            aimDistance = aimBySky ? 40f : Mathf.Clamp(flat.magnitude, minAimDistance, maxAimDistance);
        }

        /// <summary>
        /// 겨누는 동안: 좌클릭을 누른 채 마우스를 위로 끌면 멀리, 아래로 끌면 가까이(시야 상하 변화량 LookDelta.y — 시점이 위아래 한계에 닿아도 계속 끌린다), 방향은 몸 정면(시야 좌우).
        /// 목표 = 그 거리의 땅(위에서 아래로 찾은 첫 윗면 — 지붕 위도), 땅이 없으면 발 높이. maxAimDistance(맵 끝)까지. 발밑까지 끌어 내리면(cancelRadius 안) 취소.
        /// </summary>
        private void UpdateDragTarget(in CharacterInputFrame input)
        {
            aimBySky = false;
            aimDistance = Mathf.Clamp(aimDistance - input.LookDelta.y * dragPerDegree, minAimDistance, maxAimDistance); // 마우스를 올리면 멀리(안토니오 음표 놓기처럼)
            Vector3 fwd = Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward;
            Vector3 p = transform.position + fwd * aimDistance;
            aimTarget = RayCast(p + Vector3.up * 80f, Vector3.down, 200f, out var ground) ? ground.point : new Vector3(p.x, transform.position.y, p.z);
            aimCancels = aimDistance < cancelRadius;
        }

        /// <summary>
        /// 화면 가운데(조준점)가 가리키는 곳을 목표로 잡는다 — 커서는 쓰지 않는다(사냥터지기 그랩처럼). 광선 = 카메라 자리(AimRayOrigin)에서 시야 각도(LookYaw·LookPitch) 쪽.
        /// 캐릭터·판정은 지나간다. 아무것도 안 닿으면(하늘) 거리 제한 없이 시야 방향 그대로 skyThrowSpeed로 던진다.
        /// </summary>
        private void UpdateAimTarget(in CharacterInputFrame input)
        {
            Vector3 dir = Quaternion.Euler(input.LookPitch, input.LookYaw, 0f) * Vector3.forward;
            Vector3 eye = input.HasAimRay ? input.AimRayOrigin : transform.position + Vector3.up * handUp;
            // 카메라는 캐릭터 뒤에 있으니 그만큼 더 멀리까지 본다(몸에서 maxRange).
            float camBack = Mathf.Max(0f, Vector3.Dot(transform.position - eye, dir));
            float reach = maxRange + camBack;
            aimBySky = false;
            if (RayCast(eye, dir, reach, out var hit))
                aimTarget = hit.point + hit.normal * 0.02f;
            else
            {
                // 하늘(아무것도 안 닿음): 거리 제한 없이 시야 방향 그대로 skyThrowSpeed로 던진다 — 높이 볼수록(45도까지) 멀리 날아간다.
                aimBySky = true;
                skyVelocity = dir * skyThrowSpeed;
                aimTarget = eye + dir * reach;
                aimCancels = false;
                return;
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
            // 먼저 앞의 한 지점(aimTarget)을 정하고, 던지는 손에서 그 지점까지 포물선으로 잇는다 — 이 선이 공장장에게 보이는 보조선이고, 인형은 이 선을 그대로 따라가 그 지점에 떨어진다.
            Vector3 from = Hand, to = aimTarget + Vector3.up * dollRadius;
            Vector3 flat = to - from; flat.y = 0f;
            float t0 = Mathf.Clamp(flat.magnitude / throwSpeed, minFlight, maxFlight);
            // 막히지 않는 높이가 있으면 그 높이로(창문 틈 = 낮게, 벽 너머 = 높게), 없으면 기본 높이 — 그래도 선·인형은 벽을 지나 그 지점까지 간다.
            float[] tries = { t0, t0 * 0.75f, t0 * 0.55f, t0 * 1.3f, t0 * 1.6f, t0 * 2f };
            aimFlightTime = t0;
            foreach (float k in tries)
            {
                float t = Mathf.Max(0.2f, k);
                if (Simulate(from, LaunchVelocity(from, to, t), out Vector3 land) && (land - to).sqrMagnitude <= landTolerance * landTolerance) { aimFlightTime = t; break; }
            }
            aimVelocity = LaunchVelocity(from, to, aimFlightTime);

            arc.Clear();
            const int samples = 60;
            for (int i = 0; i <= samples; i++) arc.Add(ArcPoint(from, aimVelocity, aimFlightTime * i / samples));
            AimLanding = aimTarget;
            AimValid = !aimCancels;
        }

        private Vector3 LaunchVelocity(Vector3 from, Vector3 to, float t)
        {
            Vector3 v = (to - from) / t;
            v.y = (to.y - from.y + 0.5f * gravity * t * t) / t;
            return v;
        }

        private Vector3 ArcPoint(Vector3 from, Vector3 v, float t) => from + v * t + Vector3.down * (0.5f * gravity * t * t);

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
            throwFrom = Hand;
            throwTo = aimTarget + Vector3.up * dollRadius;
            throwTime = Mathf.Max(0.05f, aimFlightTime);
            throwVel = aimVelocity; // 던진 뒤 다시 겨눠도 날아가는 길은 그대로
            dollPos = throwFrom;
            dollVel = aimVelocity;
            dollAir = 0f;
            DollActive = true; DollFlying = true;
            ThrowCount++;
        }

        /// <summary>날아가는 인형: 보조선과 같은 포물선을 그대로 따라가(벽은 지나감) 비행 시간이 되면 겨눈 지점에 멈춘다.</summary>
        private void StepDoll(float dt)
        {
            dollAir += dt;
            if (dollAir >= throwTime) { dollPos = throwTo; dollVel = Vector3.zero; DollFlying = false; return; }
            dollPos = ArcPoint(throwFrom, throwVel, dollAir);
            dollVel = throwVel + Vector3.down * (gravity * dollAir);
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
            if (SwapPending) return false; // 이미 바꾸는 중
            if (!FindRoom(dollPos - Vector3.up * dollRadius, out swapFeet)) return false;
            // 도착 자리는 지금(누른 순간) 정하고, swapDelay초 뒤에 옮긴다 — 그동안 제자리에서 몸을 떤다(FE).
            swapLeft = swapDelay;
            if (swapDelay <= 0f) DoSwap();
            return true;
        }

        /// <summary>feet에서 위로 swapClearSearch까지 몸이 들어갈 첫 자리.</summary>
        private bool FindRoom(Vector3 feet, out Vector3 room)
        {
            for (float up = 0f; up <= swapClearSearch + 1e-3f; up += 0.25f)
                if (Clear(feet + Vector3.up * up)) { room = feet + Vector3.up * up; return true; }
            room = feet;
            return false;
        }

        /// <summary>기다린 뒤 실제로 자리를 바꾼다: 누른 순간의 인형 자리(swapFeet)로 가고, 인형은 지금 서 있던 자리에 놓는다. 그 사이 막혔으면 위로 다시 찾고, 없으면 바꾸지 않는다.</summary>
        private void DoSwap()
        {
            if (!Clear(swapFeet) && !FindRoom(swapFeet, out swapFeet)) return;
            Vector3 feet = swapFeet;
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
            lockLeft = postSwapLock; // 바꾼 직후 짧은 경직(보이는 표시 없음)
            // 공중(발밑 0.3m 넘게 빔)으로 바꿨으면 떨어져 내려와 착지한 순간부터 빠르게
            boostArmed = !RayCast(feet + Vector3.up * 0.1f, Vector3.down, 0.4f, out _); boostAirSeen = false;
        }

        /// <summary>바꾼 직후 경직 시간을 줄이고, 공중으로 바꿨으면 떨어져 내려와 착지한 순간 가속(landBoostTime초)을 건다.</summary>
        private void TrackSwapAfter(float dt)
        {
            if (lockLeft > 0f) lockLeft = Mathf.Max(0f, lockLeft - dt);
            if (boostLeft > 0f) boostLeft = Mathf.Max(0f, boostLeft - dt);
            if (!boostArmed) return;
            bool grounded = state != null && state.IsGrounded;
            if (!grounded) boostAirSeen = true;
            else if (boostAirSeen) { boostArmed = false; boostLeft = landBoostTime; } // 착지(이동 완료)
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

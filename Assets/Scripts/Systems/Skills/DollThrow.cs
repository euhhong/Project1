using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 공장장(지옥불) 인형 던지기와 자리 바꾸기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신 — IAttackHoldOverride, 짧게 누르는 약공격은 그대로) 인형을 겨눈다(던지기 자세 + 포물선·흰 조준원).
    ///   목표(흰 조준원)는 늘 1층(공장장 발 높이) 바닥 — 몸 정면 aimDistance m(위층·지붕 윗면에 붙지 않음). 몸 쪽으로 끌어당겨도 취소하지 않는다(취소는 E만).
    /// - 포물선 꼭대기 = 발에서 apexBase + 거리 × apexPerMeter(멀수록 조금씩 높아짐) + 위를 본 시야 각도 × apexPerDegree + 각도² × apexPerDegreeSq(우클릭으로 시야를 올릴수록 점점 더 높아짐 — 높이 조절).
    /// - 떼면 인형이 그 포물선을 실제로 날아간다: 처음 닿는 윗면(1층 바닥·위층 바닥·지붕)에 멈추고, 벽에 닿으면 수평 속도를 잃고 아래로 떨어진다 —
    ///   높이 띄우면 위층·지붕에도 올라간다. 보조선(AimArc)은 실제로 멈출 곳(ActualLanding)까지. 인형은 하나 — 다시 던지면 앞 인형은 없어진다.
    ///   창틀(얇은 턱, WindowSillCheck) 위에는 멈추지 않고 가로질러 미끄러져 떨어진다(sillSlideSpeed).
    /// - 좌클릭 차징 = 인형 던지기 자세 + 포물선·원. 좌클릭을 누른 채 마우스 = 떨어질 자리 조절(좌우 = 방향, 상하 = 거리 aimLookScale배). 우클릭을 누른 채 마우스 = 시야 높이(위쪽 한계 열림).
    ///   차징을 시작하면 그 순간 시야에서 aimViewRaise도만 위로 올려 고정(IChargeLook — LocalPlayerInput).
    ///   거리는 좌클릭을 누른 채 마우스를 위로 끌면 멀리 / 아래로 끌면 가까이(시야 상하 1도당 dragPerDegree m — 안토니오 음표 놓기 감도, 맵 끝 maxAimDistance까지 — 시점이 위아래 한계에 닿아도 계속 끌림), 방향은 몸 정면. 처음 거리 = 누르기 시작할 때 조준점까지.
    /// - TrySwap(E 짧게 — CloneSummonSkill이 부른다): 누른 순간의 인형 자리를 도착점으로 정하고, swapDelay초 동안 제자리에서 몸을 떤 뒤(FE) 인형 자리와 공장장 자리를 맞바꾼다.
    ///   공장장은 인형이 있던 자리(공중이면 그 공중)로 가서 떨어지고, 인형은 공장장이 있던 자리에 놓인다(멈춤 — 다시 바꾸면 돌아온다).
    ///   바꾼 뒤 다음 착지까지의 낙하는 다치지 않고(ISafeFallSource → FallDamage), 서 있는 자세로 그냥 툭 떨어져 착지 동작 없이 바로 움직인다(IGentleDropSource → CharacterTraversal DollDrop). 그 자리에 몸이 들어갈 곳이 없으면 위로 조금씩 찾고, 없으면 바꾸지 않는다.
    /// - 인형 회수: 자기 평타의 판정 구간에 손·무기 판정이 인형(멈춘 인형 기둥 / 날아가는 구체)에 닿으면 거둔다 — 집어 드는 동안 retrieveTime초 이동·공격·스킬 불가(FE: 평타에서 Taking Item 모션으로 마무리).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 그리기는 FE(DollThrowVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class DollThrow : MonoBehaviour, IDollThrow, IAttackHoldOverride, ISafeFallSource, IChargeCancelHint, IGentleDropSource, IChargeLook, IMoveSpeedLimit, IActionLock, IMoveSpeedBoost, ICooldownGauge
    {
        [Tooltip("좌클릭을 꾹 누르는 동안 마우스로 시점(= 포물선)을 움직이는 감도 배율(우클릭 시점 돌리기 감도 기준). 우클릭 없이 좌클릭만으로 겨눈다.")]
        [SerializeField, Min(0.1f)] private float aimLookScale = 1f;
        [Tooltip("좌클릭 차징을 시작하는 순간 그때 시야에서 이만큼(도) 위로 올리고 그 높이에 고정 — 우클릭을 누른 채 마우스로만 높이를 바꾼다(위쪽 한계 열림).")]
        [SerializeField, Range(0f, 45f)] private float aimViewRaise = 0f; // 0 = 차징해도 카메라를 강제로 움직이지 않음
        [Tooltip("E를 짧게 누른 뒤 실제로 자리를 바꾸기까지(초) — 그동안 제자리에서 몸을 떤다. 도착은 누른 순간의 인형 자리.")]
        [SerializeField, Min(0f)] private float swapDelay = 0.35f;
        [Tooltip("자리를 바꾼 직후 경직(초) — 땅이든 공중(낙하)이든 같다. 이동·공격·스킬 불가, 보이는 표시 없음(IActionLock).")]
        [SerializeField, Min(0f)] private float postSwapLock = 1.2f;
        [Tooltip("인형과 자리를 바꾸면(땅이든 공중이든) 경직이 풀린 순간부터 이 시간(초) 동안 이동 속도 배율. 공중(낙하)으로 바꿨으면 거기에 airBoostExtra배 더.")]
        [SerializeField, Min(0f)] private float landBoostTime = 2f;
        [SerializeField, Min(1f)] private float landBoostMultiplier = 1.5f;
        [Tooltip("공중의 인형과 바꿔 떨어져 내려온 경우 이동 속도 배율에 더 곱하는 값(1.5 = 거기서 50% 더 — 1.5 × 1.5 = 2.25배).")]
        [SerializeField, Min(1f)] private float airBoostExtra = 1.5f;
        private bool airSwap; private float boostMultiplier = 1f;
        private float lockLeft, boostLeft;
        [Tooltip("인형과 자리를 바꾼 뒤 다시 바꿀 수 있을 때까지(초) — E 꾹(분신)은 해당 없음.")]
        [SerializeField, Min(0f)] private float swapCooldown = 17f;
        private float swapCooldownLeft;
        /// <summary>다음 자리 바꾸기까지 남은 재사용 대기(초).</summary>
        public float SwapCooldownRemaining => swapCooldownLeft;

        /// <summary>HUD 게이지: 인형과 자리 바꾸기 재사용 대기(차오름).</summary>
        public bool TryGetCooldown(out string label, out float fill, out bool draining)
        {
            label = "자리 바꾸기"; draining = false;
            fill = swapCooldown > 0f ? 1f - swapCooldownLeft / swapCooldown : 1f;
            return swapCooldownLeft > 0f;
        }
        [Tooltip("인형 회수: 공장장이 자기 평타(Light, 판정 구간)로 인형을 맞히면 인형을 거둔다. 인형 판정 = 멈춘 인형은 발밑부터 키(공장장 키 × dollHitHeightScale) 높이의 기둥(반지름 dollHitRadius), 날아가는 인형은 구체.")]
        [SerializeField, Min(0.1f)] private float dollHitRadius = 0.6f;
        [SerializeField, Min(0.1f)] private float dollHitHeightScale = 1.25f;
        [Tooltip("회수하며 집어 드는 동안(초) — 이동·공격·스킬 불가(보이는 표시 없음). FE 모션 길이와 같게.")]
        [SerializeField, Min(0f)] private float retrieveTime = 1f;
        private float retrieveLeft;
        private ICharacterAttackState attackState;
        public int RetrieveCount { get; private set; }
        private bool boostArmed;
        private float swapLeft;
        private Vector3 swapFeet;
        [Tooltip("겨눌 수 있는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float maxRange = 400f;
        [Tooltip("조준점이 하늘(아무것도 안 닿음)이면 시야 방향 그대로 이 속도(m/s)로 던진다 — 거리 제한 없음, 45도로 올려 보면 가장 멀리(속도² ÷ 중력 ≈ 100m).")]
        [SerializeField, Min(1f)] private float skyThrowSpeed = 45f;
        private bool aimBySky;
        private Vector3 skyVelocity;
        [Tooltip("포물선 꼭대기 기본 높이(m, 공장장 발 기준) — 여기에 거리·시야 각도만큼 더해진다.")]
        [SerializeField, Min(0f)] private float apexBase = 2f;
        [Tooltip("포물선 꼭대기가 거리 1m마다 더 높아지는 양(m) / 시야를 위로 1도 볼 때마다 더 높아지는 양(m — 우클릭으로 시야를 올려 높이 조절).")]
        [SerializeField, Min(0f)] private float apexPerMeter = 0.06f, apexPerDegree = 0.1f;
        [Tooltip("시야를 올릴수록 높이가 점점 더 빨리 늘도록 더하는 양(m / 도²) — 10도 약 +1.4m, 30도 약 +6.6m, 45도 약 +12.6m.")]
        [SerializeField, Min(0f)] private float apexPerDegreeSq = 0.004f;
        private float aimLookPitch;
        /// <summary>겨누는 동안 인형이 실제로 멈출 곳(위층·지붕에 걸리면 그곳).</summary>
        public Vector3 ActualLanding { get; private set; }
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
        public string ChargeCancelHint => !Aiming ? null : "E 취소";
        private bool waitRelease;
        /// <summary>차징을 E로 취소한 틱 / 취소한 뒤 좌클릭을 아직 떼지 않음. 이 동안 E 스킬(분신·자리 바꾸기)은 나가지 않는다.</summary>
        public int CancelTick { get; private set; } = int.MinValue;
        public bool WaitingRelease => waitRelease;
        /// <summary>차징 중이거나 방금 취소함 — E 스킬을 막는다(CloneSummonSkill).</summary>
        public bool BlocksSkill => isActiveAndEnabled && (Aiming || waitRelease || CancelTick == SimulationTick.Current);
        public bool LookWhileCharging => isActiveAndEnabled && !waitRelease; // 좌클릭 차징 = 좌클릭 끌기로 자리 조절 + 우클릭 끌기로 시야 높이(LocalPlayerInput), E로 취소하면 끝
        public float ChargeLookScale => aimLookScale;
        public float ChargeViewRaise => aimViewRaise;
        [Tooltip("던진 인형이 땅에 떨어진 뒤에도 시야를 자동으로 되돌리지 않고 그대로 두는 시간(초) — 날아가는 동안은 늘 그대로.")]
        [SerializeField, Min(0f)] private float viewHoldAfterLand = 1f;
        [Tooltip("창틀(얇은 턱) 위에 떨어지면 멈추지 않고 이 속도(m/s)로 창틀을 가로질러 미끄러져 떨어진다.")]
        [SerializeField, Min(0.1f)] private float sillSlideSpeed = 2f;
        private float viewHoldLeft;
        public bool HoldView => isActiveAndEnabled && (DollFlying || viewHoldLeft > 0f);
        public bool SwapPending => swapLeft > 0f;
        public float SwapProgress => swapDelay > 0f && SwapPending ? 1f - swapLeft / swapDelay : 0f;

        /// <summary>자리를 바꾸려고 기다리는 동안은 제자리(움직이지 않음).</summary>
        public bool TryGetSpeedLimit(out float maxSpeed) { maxSpeed = 0f; return SwapPending || lockLeft > 0f || retrieveLeft > 0f; }

        /// <summary>자리를 바꾼 직후 짧은 경직(보이는 표시 없음) — 공격·스킬 불가.</summary>
        public bool ActionsLocked => isActiveAndEnabled && (SwapPending || lockLeft > 0f || retrieveLeft > 0f);

        /// <summary>인형과 자리를 바꾼 뒤(경직이 풀린 순간부터) landBoostTime초 동안 빠르게 — 공중(낙하)으로 바꿨으면 airBoostExtra배 더.</summary>
        public bool TryGetSpeedBoost(out float multiplier) { multiplier = boostMultiplier; return boostLeft > 0f; }

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            TryGetComponent(out cc);
            if (TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기다리는 동안 멈추게(IMoveSpeedLimit)
        }

        private void OnEnable() { if (!SkillObstacles.Dolls.Contains(this)) SkillObstacles.Dolls.Add(this); } // 멈춘 인형 = 돌진·그랩을 막는 방해물

        private void OnDisable()
        {
            SkillObstacles.Dolls.Remove(this);
            Aiming = false; arc.Clear();
            DollActive = false; DollFlying = false; safeFall = false; swapLeft = 0f; waitRelease = false; lockLeft = boostLeft = 0f; boostArmed = false; swapCooldownLeft = 0f; retrieveLeft = 0f;
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
            if (retrieveLeft > 0f) retrieveLeft = Mathf.Max(0f, retrieveLeft - Time.fixedDeltaTime);
            if (viewHoldLeft > 0f) viewHoldLeft = Mathf.Max(0f, viewHoldLeft - Time.fixedDeltaTime);
            CheckRetrieve();
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
        /// 목표 = 그 거리의 땅(위에서 아래로 찾은 첫 윗면 — 지붕 위도), 땅이 없으면 발 높이. maxAimDistance(맵 끝)까지. 몸 쪽으로 끌어당겨도 취소하지 않는다.
        /// </summary>
        private void UpdateDragTarget(in CharacterInputFrame input)
        {
            aimBySky = false;
            aimDistance = Mathf.Clamp(aimDistance - input.LookDelta.y * dragPerDegree, minAimDistance, maxAimDistance); // 마우스를 올리면 멀리(안토니오 음표 놓기처럼)
            Vector3 fwd = Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward;
            Vector3 p = transform.position + fwd * aimDistance;
            aimLookPitch = input.LookPitch;
            // 목표는 늘 1층(공장장 발 높이) 기준 바닥 — 위층·지붕 윗면에 붙지 않는다(발 높이 2m 위에서 아래로 찾음).
            aimTarget = RayCast(new Vector3(p.x, transform.position.y + 2f, p.z), Vector3.down, 30f, out var ground) ? ground.point : new Vector3(p.x, transform.position.y, p.z);
            aimCancels = false; // 몸 쪽으로 끌어당겨도 취소하지 않는다(취소는 E만)
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
            aimCancels = false; // 몸 쪽(발밑)을 가리켜도 취소하지 않는다(취소는 E만)
        }

        /// <summary>
        /// 목표까지 닿는 포물선을 여러 비행 시간(낮고 빠르게 ~ 높이 띄워)으로 날려 보고, 막히지 않고 목표 가까이 떨어지는 첫 길을 쓴다.
        /// 창문 틈은 낮은 길로, 낮은 벽 너머·높은 곳은 높은 길로 넘어간다. 그런 길이 없으면 기본 길(막히면 거기서 떨어짐)을 보여 준다.
        /// </summary>
        private void UpdateAimArc()
        {
            // 목표(흰 조준원)는 늘 1층(공장장 발 높이) 바닥. 포물선 꼭대기 높이 = 기본 + 거리에 따라 조금씩 + 시야를 위로 볼수록 더(우클릭으로 시야 높이 조절).
            // 인형은 이 포물선을 실제로 날아가 처음 닿는 윗면(지붕·위층 바닥 포함)에 멈추고, 벽에 닿으면 떨어진다 — 높이 띄우면 위층·지붕에도 올린다.
            Vector3 from = Hand, to = aimTarget + Vector3.up * dollRadius;
            Vector3 flat = to - from; flat.y = 0f;
            float lookUp = Mathf.Max(0f, -aimLookPitch);                     // 위를 본 각도(도)
            float apexY = transform.position.y + apexBase + flat.magnitude * apexPerMeter + lookUp * apexPerDegree + lookUp * lookUp * apexPerDegreeSq; // 시야를 올릴수록 조금씩 더 높게
            apexY = Mathf.Max(apexY, Mathf.Max(from.y, to.y) + 0.5f);
            aimFlightTime = Mathf.Sqrt(2f * (apexY - from.y) / gravity) + Mathf.Sqrt(2f * (apexY - to.y) / gravity);
            aimVelocity = LaunchVelocity(from, to, aimFlightTime);

            arc.Clear();
            Simulate(from, aimVelocity, out Vector3 end, arc);                // 실제로 멈출 곳까지(위층·지붕에 걸리면 거기서 끝)
            ActualLanding = end;
            AimLanding = end - Vector3.up * dollRadius; // 흰 조준원 = 실제로 멈출 곳(3층에 걸리면 3층) — 포물선은 1층 목표 기준 그대로
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

        /// <summary>날아가는 인형: 보조선과 같은 포물선을 실제로 날아간다 — 처음 닿는 윗면(1층 바닥·위층 바닥·지붕)에 멈추고, 벽에 닿으면 아래로 떨어진다.</summary>
        private void StepDoll(float dt)
        {
            dollAir += dt;
            if (Step(ref dollPos, ref dollVel, dt) || dollAir > maxAirTime) { DollFlying = false; dollVel = Vector3.zero; viewHoldLeft = viewHoldAfterLand; } // 떨어진 뒤 1초까지 시야 그대로
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
                if (hit.normal.y > 0.6f && WindowSillCheck.IsSillTop(hit.point, out Vector3 across))
                {
                    // 창틀(얇은 턱) 위에는 멈추지 않는다: 날아오던 쪽(없으면 공장장 쪽)으로 미끄러져 떨어진다.
                    Vector3 flat = new(v.x, 0f, v.z);
                    float along = Vector3.Dot(flat, across);
                    if (Mathf.Abs(along) < 0.1f) along = Vector3.Dot(transform.position - hit.point, across);
                    v = across * (along >= 0f ? 1f : -1f) * Mathf.Max(sillSlideSpeed, Mathf.Abs(Vector3.Dot(flat, across)));
                    return false;
                }
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
            if (swapCooldownLeft > 0f) return false; // 재사용 대기 중(E 짧게 = 아무 일 없음)
            if (!FindRoom(dollPos - Vector3.up * dollRadius, out swapFeet)) return false;
            // 도착 자리는 지금(누른 순간) 정하고, swapDelay초 뒤에 옮긴다 — 그동안 제자리에서 몸을 떤다(FE).
            swapLeft = swapDelay;
            if (swapDelay <= 0f) DoSwap();
            return true;
        }

        /// <summary>
        /// 인형 자리(feet) 근처에서 몸(캡슐)이 들어갈 첫 자리: 바로 위(swapClearSearch까지) → 둘레(0.35·0.7·1.0m, 8방향, 같은 높이·조금 위) → 바로 아래 바닥.
        /// 좁은 곳(창틀·턱·좁은 틈)에 인형이 올라가 있어도 바꿀 수 있게 — 그래도 없으면 인형 자리 그대로(몸이 겹치면 CharacterController가 밀어낸다). 늘 true.
        /// </summary>
        private bool FindRoom(Vector3 feet, out Vector3 room)
        {
            for (float up = 0f; up <= swapClearSearch + 1e-3f; up += 0.25f)
                if (Clear(feet + Vector3.up * up)) { room = feet + Vector3.up * up; return true; }
            foreach (float r in new[] { 0.35f, 0.7f, 1f })
                for (int i = 0; i < 8; i++)
                {
                    Vector3 side = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * r;
                    foreach (float up in new[] { 0f, 0.3f, 0.7f })
                    {
                        Vector3 p = feet + side + Vector3.up * up;
                        if (!Clear(p)) continue;
                        // 그 자리 바로 아래 바닥(1m 안)이 있으면 바닥에 세운다
                        room = RayCast(p + Vector3.up * 0.1f, Vector3.down, 1.1f, out var g) && g.normal.y > 0.6f && Clear(g.point) ? g.point : p;
                        return true;
                    }
                }
            if (RayCast(feet + Vector3.up * 0.05f, Vector3.down, 3f, out var below) && below.normal.y > 0.6f && Clear(below.point)) { room = below.point; return true; }
            room = feet; // 마지막: 인형 자리 그대로
            return true;
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
            swapCooldownLeft = swapCooldown; // 다음 자리 바꾸기까지 재사용 대기
            airSwap = !RayCast(feet + Vector3.up * 0.1f, Vector3.down, 0.4f, out _); // 공중(발밑 0.3m 넘게 빔)으로 바꿨으면(낙하) 더 빠르게
            boostArmed = true; // 땅이든 공중이든 경직이 풀린 순간부터 빠르게
        }

        /// <summary>
        /// 인형 회수: 자기 평타(Light)의 판정 구간(Active)에 손·무기 판정이 인형에 닿으면 인형을 거둔다(사라짐 — 다시 던질 수 있음).
        /// 멈춘 인형 = 발밑부터 공장장 키 × dollHitHeightScale 높이의 기둥, 날아가는 인형 = 그 자리 구체(반지름 dollHitRadius). 집어 드는 동안(retrieveTime) 이동·공격·스킬 불가.
        /// </summary>
        private void CheckRetrieve()
        {
            if (!DollActive || SwapPending) return;
            if (attackState == null) TryGetComponent(out attackState);
            if (attackState == null || attackState.AttackState != CharacterAttackState.Light || attackState.AttackPhase != AttackPhase.Active) return;
            float height = DollFlying || cc == null ? 0f : cc.height * transform.lossyScale.y * dollHitHeightScale;
            Vector3 bottom = DollFlying ? dollPos : dollPos - Vector3.up * dollRadius, top = bottom + Vector3.up * height;
            foreach (var hb in GetComponentsInChildren<Hitbox>())
            {
                if ((hb.Slot & (HitboxSlot.Weapon | HitboxSlot.RightHand | HitboxSlot.LeftHand)) == 0 || hb.Collider == null || !hb.Collider.enabled) continue;
                Vector3 c = hb.Collider.bounds.center;
                Vector3 axis = top - bottom;
                float t = axis.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(c - bottom, axis) / axis.sqrMagnitude) : 0f;
                Vector3 p = bottom + axis * t;                       // 인형 기둥에서 판정에 가장 가까운 점
                Vector3 q = hb.Collider.ClosestPoint(p);              // 판정에서 그 점에 가장 가까운 점
                if ((q - p).sqrMagnitude > dollHitRadius * dollHitRadius) continue;
                DollActive = false; DollFlying = false; dollVel = Vector3.zero;
                retrieveLeft = retrieveTime;
                swapCooldownLeft = 0f; // 회수하면 자리 바꾸기 재사용 대기가 바로 풀린다
                RetrieveCount++;
                return;
            }
        }

        /// <summary>바꾼 직후 경직 시간을 줄이고, 공중으로 바꿨으면(낙하) 경직이 풀린 순간부터 가속(landBoostTime초)을 건다.</summary>
        private void TrackSwapAfter(float dt)
        {
            if (boostLeft > 0f) boostLeft = Mathf.Max(0f, boostLeft - dt);
            if (swapCooldownLeft > 0f) swapCooldownLeft = Mathf.Max(0f, swapCooldownLeft - dt);
            if (lockLeft <= 0f) return;
            lockLeft = Mathf.Max(0f, lockLeft - dt);
            if (lockLeft <= 0f && boostArmed) { boostArmed = false; boostLeft = landBoostTime; boostMultiplier = landBoostMultiplier * (airSwap ? airBoostExtra : 1f); } // 경직이 풀림 → 빠르게(낙하였으면 더)
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

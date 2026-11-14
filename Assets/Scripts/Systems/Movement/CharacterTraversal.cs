using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 오르기·매달리기·창틀 넘기·낙하·착지(BE). 모션은 FE(CharacterTraversalAnimator, Animator 레이어 Traversal)가 보여 준다.
    /// 턱은 따로 표시하지 않고 지형 모양으로 찾는다(앞쪽 수직 면 + 그 위 윗면 + 위가 빔). 캐릭터·판정·트리거는 무시한다.
    ///
    /// - 창틀 넘기: 땅에서 창틀(키의 25~75% 높이, 두께 0.8m 이하, 위가 열림) 쪽으로 방향키를 누른 채 점프(감시자는 감시자 모션 Jump)
    ///   → 반대편 바닥이 같은 높이면 넘어간다(WindowVault — 점프 중 짚고 넘기와 같은 모션 Jumping (1), 빠르게). 반대편이 dropMin보다 낮으면 턱 위로 올라섰다가 뛰어내린다(WindowDrop, Jumping Down (2)).
    /// - 점프 중 날아가는 방향 앞에 창틀이 있으면 반드시 한 손으로 짚고 넘는다(WindowJump, Jumping (1)).
    ///   창틀 뚫고 지나가기(windowPassThrough, 붉은나비): 넘기 모션 없이 감시자 넘기 길(곧게, 충돌 무시)로 창틀을 뚫고 지나간다(하스터처럼).
    /// - 올라서기: 땅에서 중간 높이 턱(키의 35~110%, 위에 설 자리) 쪽으로 방향키 + 점프 → 뛰어 올라선다(ClimbUp, Running Jump).
    /// - 매달리기: 공중에서 턱 쪽으로 방향키를 누르고 있고 손이 턱에 닿을 높이면 잡고 매달린다(HangGrab → Hang).
    ///   턱 아래가 벽이면 발로 벽을 짚고(Braced Hang), 비었으면 늘어져 매달린다(Freehang).
    ///   매달린 채 앞(W, 또는 점프) → 턱 위에 설 자리가 있으면 올라가고(HangClimb, Freehang Climb), 난간이 막고 그 뒤에 바닥이 있으면 난간을 넘어 그 바닥에 내린다(HangClimbRail, Climbing).
    ///   벽 한 장(창틀 윗부분·담 꼭대기처럼 얇고 반대편이 낮음)이면 앞 → 꼭대기를 잡은 채 끌어 올라 넘어가자마자 반대편으로 떨어진다(HangClimbOver, Freehang Climb 앞부분).
    ///   뒤(S) → 놓고 떨어진다(생존자는 다치지 않을 높이면 벽을 박차고 뒤로 공중제비를 돌아 아래 바닥에 내려선다 — HangFlip, Backflip). 잡는 턱은 손이 닿는 가장 높은 윗면(창틀·벽 맨 꼭대기, 창틀 없는 바닥 끝도).
    ///   잡는 중·매달린 채·매달려 올라가는 중에 넉백되면(IKnockbackReceiver) 손이 풀려 벽 바깥으로 밀려 떨어진다(등으로 떨어지는 낙하, 낙하 데미지는 놓은 것과 같음).
    ///   벽 종류·두께와 상관없이 같은 규칙: 올라서기로는 못 가는 낮은 벽(약 1.8~2.9m)도 발로 벽을 짚고 매달린다(루트는 바닥 아래로 내려가도 되고, 놓으면 바닥 위로 올림).
    /// - 감시자(HunterStyle): 창틀 넘기·올라서기는 들어오던 속도로 출발해 속도를 이어 나가고(Carry), 매달려 오르기는 붙기·옮기기 끝을 조금 늘려 덜 끊긴다.
    /// - 낙하: 점프하지 않고 0.5m 넘게 떨어지면 Fall(걸어서 떨어짐 = Jumping, 넉백·뒤로 걷다 떨어짐 = Falling을 뒤집어 하늘을 보며 등으로).
    ///   앞·뒤는 떨어지기 시작할 때(땅에서 떨어지는 틱: 넉백·뒤 방향키·뒤로 가던 속도) 정하고 공중에서 바뀌지 않는다. 뒤로 뛰어내리면 구르지 않고 다친다, 앞으로 뛰어내리면 구르며 다치지 않는다(hardFall 미만).
    /// - 착지: 떨어진 높이(가장 높았던 곳 → 착지)로 고른다 — 고공(hardFall 이상) = Hard Landing, 점프해서 높이 = 앞구르기(Falling To Roll),
    ///   걸어서 높이 = 깊이 웅크림(Jumping), 낮게 = 살짝 웅크림(Jumping Down). 크게 착지하면 잠깐 못 움직인다.
    /// 오르는 중·매달림·구르기·크게 착지하는 중에는 공격·스킬을 막는다(LocksActions). 기절·쓰러지면 놓친다.
    /// 높이 기준은 모델의 Humanoid 단위(Animator.humanScale × 모델 크기, 골반 높이 ≈ 1)라서 키가 다른 캐릭터도 손이 턱에 맞는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DefaultExecutionOrder(-50)] // 이동(CharacterMotor)보다 먼저: 점프 입력을 창틀 넘기·올라서기로 가로챈다
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterTraversal : MonoBehaviour, ICharacterTraversalState, IVelocityOverride, IMoveOverride, IFacingOverride, IMoveSpeedLimit, IKnockbackReceiver
    {
        // 모션 기준 높이(Humanoid 단위, 루트 = 모션의 바닥). 의사·루키노 모델에 모션을 입혀 잼 — 모델이 달라도 같다.
        private const float FreeHangTop = 2.05f;   // Jump To Freehang: 매달린 자세의 턱 윗면(손목 1.93 + 손가락)
        private const float BracedHangTop = 2.28f; // Idle To Braced Hang(손목 2.16)
        private const float BracedFeet = 1.0f;     // Braced Hang에서 벽을 짚는 발 높이
        private const float BracedFloorGap = 0.1f; // 벽을 짚은 발과 바닥 사이에 남길 틈(m) — 낮은 벽(약 1.8m~)도 발로 짚고 매달린다
        private const float HangClimbTop = 2.2f;   // Freehang Climb 첫 자세의 턱 윗면(발 기준)
        private const float RailClimbTop = 2.12f;  // Climbing(난간 넘기) 첫 자세의 난간 윗면(발 기준)
        private const float LedgeInset = 0.06f;    // 턱 윗면을 찾을 때 앞면에서 안으로 들어가는 거리(난간 두께 0.15보다 얇게)
        private const float Skin = 0.03f;

        [Header("창틀 넘기 (방향키 + 점프)")]
        [Tooltip("창틀 높이(키 대비) 범위.")]
        [SerializeField, Range(0f, 1f)] private float sillMin = 0.25f, sillMax = 0.75f;
        [Tooltip("창틀까지 거리(m, 몸 둘레에서).")]
        [SerializeField, Min(0f)] private float sillReach = 0.7f;
        [Tooltip("이보다 두꺼우면 창틀이 아니다(m).")]
        [SerializeField, Min(0.1f)] private float sillMaxThickness = 0.8f;
        [Tooltip("반대편 바닥이 이만큼(m) 넘게 낮으면 넘지 않고 턱 위에서 뛰어내린다.")]
        [SerializeField, Min(0f)] private float dropMin = 0.8f;
        [Tooltip("걸어서 창틀 넘기 시간(초) — 점프 중 짚고 넘기(Jumping (1), 0.75초)와 같은 모션을 거의 같은 빠르기로 / 창틀 위에서 뛰어내리기 시간.")]
        [SerializeField, Min(0.1f)] private float vaultTime = 0.8f, windowDropTime = 0.9f;
        [Tooltip("감시자 창틀 넘기(Jump) 시간(초).")]
        [SerializeField, Min(0.1f)] private float hunterVaultTime = 0.85f;
        [Tooltip("점프 중 창틀을 짚고 넘기(Jumping (1)) 시간(초). 넘은 뒤 반대편으로 더 가는 거리(m).")]
        [SerializeField, Min(0.1f)] private float windowJumpTime = 0.75f;
        [SerializeField, Min(0f)] private float windowJumpCarry = 0.6f;

        [Header("올라서기 (중간 높이, 방향키 + 점프)")]
        [Tooltip("턱 높이(키 대비) 범위.")]
        [SerializeField, Min(0f)] private float climbMin = 0.35f, climbMax = 1.1f;
        [SerializeField, Min(0f)] private float climbReach = 0.9f;
        [SerializeField, Min(0.1f)] private float climbTime = 0.65f;

        [Header("매달리기 (공중에서 방향키)")]
        [SerializeField, Min(0f)] private float hangReach = 0.45f;
        [Tooltip("매달릴 자리보다 이만큼 아래에서도 끌어 올려 잡는다 / 위에서도 내려 잡는다(Humanoid 단위).")]
        [SerializeField, Min(0f)] private float pullUp = 0.25f, pullDown = 0.9f;
        [Tooltip("매달릴 자리로 붙는 시간(초).")]
        [SerializeField, Min(0.01f)] private float snapTime = 0.1f;
        [SerializeField, Min(0.1f)] private float grabTime = 0.6f, bracedGrabTime = 0.55f;
        [Tooltip("놓은 뒤 다시 잡을 수 있을 때까지(초).")]
        [SerializeField, Min(0f)] private float regrabDelay = 0.4f;
        [Tooltip("매달려 올라가기(Freehang Climb, 벽 한 장 넘기는 이 시간 × OverClipShare) / 난간 넘기(Climbing) 시간(초). 모션은 이 시간에 맞춰 재생된다.")]
        [SerializeField, Min(0.1f)] private float hangClimbTime = 1.5f, railClimbTime = 1.15f;
        [Tooltip("매달려 올라갈 때 발 높이(0 = 매달린 자리, 1 = 턱 위) — Freehang Climb의 낮은 발 높이를 따른다.")]
        [SerializeField] private AnimationCurve hangClimbRise = new(
            new Keyframe(0f, 0f), new Keyframe(0.08f, 0.06f), new Keyframe(0.13f, 0.29f), new Keyframe(0.16f, 0.42f), new Keyframe(0.21f, 0.55f),
            new Keyframe(0.26f, 0.81f), new Keyframe(0.29f, 0.84f), new Keyframe(0.4f, 0.68f), new Keyframe(0.47f, 0.54f), new Keyframe(0.58f, 0.64f),
            new Keyframe(0.66f, 0.62f), new Keyframe(0.71f, 0.86f), new Keyframe(0.74f, 0.97f), new Keyframe(0.77f, 1f), new Keyframe(1f, 1f));
        [Tooltip("난간 넘을 때 발 높이(0 = 매달린 자리, 1 = 난간 위) — Climbing의 낮은 발 높이를 따른다. 그 뒤 난간 뒤 바닥으로 내려간다.")]
        [SerializeField] private AnimationCurve railClimbRise = new(
            new Keyframe(0f, 0f), new Keyframe(0.2f, 0f), new Keyframe(0.27f, 0.08f), new Keyframe(0.33f, 0.24f), new Keyframe(0.4f, 0.41f),
            new Keyframe(0.47f, 0.59f), new Keyframe(0.53f, 0.83f), new Keyframe(0.6f, 1f), new Keyframe(1f, 1f));

        [Header("감시자 넘기 (부드럽게)")]
        [Tooltip("감시자: 창틀 넘기·뛰어 올라서기를 들어오던 속도로 출발해, 끝날 때 이 비율의 속도로 이어 나간다(0 = 멈췄다 출발하고 멈춰 끝남). 생존자는 쓰지 않는다.")]
        [SerializeField, Range(0f, 1f)] private float hunterSpeedCarry = 1f;
        [Tooltip("감시자: 매달려 올라가기·난간 넘기·벽 한 장 넘기에서 매달린 자리 → 오르기 시작 자세로 붙는 구간과, 턱 위로 옮겨 가는 구간의 끝을 이만큼(진행도) 늘려 천천히 옮긴다.")]
        [SerializeField, Range(0f, 0.2f)] private float hunterClimbSpread = 0.08f;

        [Header("낙하·착지 (떨어진 높이, m)")]
        [SerializeField, Min(0f)] private float fallStartDrop = 0.5f;
        [Tooltip("이보다 빨리(m/s) 뒤로 가다 떨어지면 허우적대며 떨어진다.")]
        [SerializeField, Min(0f)] private float backwardSpeed = 0.5f;
        [Tooltip("걸어서 떨어진 낮은 착지 / 점프 포함 낮은 착지 / 걸어서 높은 착지 / 점프해서 구르기 / 고공 착지.")]
        [SerializeField, Min(0f)] private float lowFall = 0.8f, landAny = 1.9f, highFall = 2.5f, rollFall = 2.6f, hardFall = 6f;
        [SerializeField, Min(0.1f)] private float landTime = 0.45f, landHighTime = 0.65f, landRollTime = 1f, landHardTime = 1.1f;
        [Tooltip("높은·고공 착지 뒤 못 움직이는 시간(초).")]
        [SerializeField, Min(0f)] private float landHighStop = 0.3f, landHardStop = 0.65f;
        [Tooltip("턱에 매달렸다가 놓아(뒤 S) 떨어지면 매달린 높이부터 이 높이(m) 미만은 다치지 않는다 — 낙하 데미지·크게 착지(경직) 없이 가볍게 착지. 이 이상이면 보통 낙하 규칙.")]
        [SerializeField, Min(0f)] private float hangDropSafeHeight = 6f;
        [Tooltip("생존자: 매달려서 뒤(S) → 다치지 않을 높이면 벽을 박차고 뒤로 공중제비(Backflip)를 돌아 이만큼(m) 물러난 아래 바닥에 내려선다. 뒤가 막혀 0.5m도 못 물러나면 그냥 놓는다.")]
        [SerializeField, Min(0f)] private float flipBack = 1.4f;
        [Tooltip("공중제비 시간(초, Backflip 17~58프레임 = 1.37초) / 발이 바닥에 닿는 진행도(40프레임).")]
        [SerializeField, Min(0.1f)] private float flipTime = 1.37f;
        [SerializeField, Range(0.2f, 0.9f)] private float flipLand = 0.56f;
        [Tooltip("착지하는 동안 걷기 속도 대비 빠르기.")]
        [SerializeField, Range(0f, 1f)] private float landSlow = 0.5f;

        private struct Ledge { public float Top; public Vector3 Face; public Vector3 Normal; }

        private CharacterController cc;
        private ICharacterInputSource inputSource;
        private ICharacterState state;
        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private Animator animator;

        private TraversalAction action;
        private float actionTime, actionDuration, landStop, rollSpeed, regrab, faceYaw;
        private bool braced, fallBackward;
        private Ledge ledge;
        private Vector3 pathStart, pathMid, pathEnd, hangPos, entryVelocity;
        private float root0, railTop;
        private bool airborne, jumped, jumpedFlag, knocked, hangDrop, backAtStart, safeJump;
        private float peakY, airStartY;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        public TraversalAction Action => action;
        public float ActionProgress => actionDuration > 0f ? Mathf.Clamp01(actionTime / actionDuration) : 0f;
        public bool BracedHang => braced;
        public bool FallBackward => fallBackward;
        public bool HunterStyle => hunterStyle;

        [Tooltip("감시자: 창틀을 감시자 모션(Jump)으로 뛰어넘는다. 캐릭터를 고를 때 SetHunterStyle로 정한다.")]
        [SerializeField] private bool hunterStyle;

        /// <summary>감시자인지 정한다(캐릭터를 고를 때).</summary>
        public void SetHunterStyle(bool hunter) => hunterStyle = hunter;

        public bool PassesThroughWindows => windowPassThrough;

        [Tooltip("창틀 뚫고 지나가기(붉은나비): 창틀 넘기 모션 없이 감시자 넘기 길(곧은 길, 충돌 무시)로 창틀을 뚫고 지나간다(하스터처럼). 캐릭터를 고를 때 SetWindowPassThrough로 정한다.")]
        [SerializeField] private bool windowPassThrough;

        /// <summary>창틀을 모션 없이 뚫고 지나갈지 정한다(캐릭터를 고를 때).</summary>
        public void SetWindowPassThrough(bool pass) => windowPassThrough = pass;

        /// <summary>창틀 넘기를 감시자 길(곧게 Jump 시간)로 하는지 — 감시자 또는 창틀 뚫고 지나가기.</summary>
        private bool HunterPath => hunterStyle || windowPassThrough;
        public bool LocksActions => IsScripted(action) || action == TraversalAction.HangGrab || action == TraversalAction.Hang || action == TraversalAction.LandRoll
                                    || ((action == TraversalAction.LandHigh || action == TraversalAction.LandHard) && actionTime < landStop);

        private static bool IsScripted(TraversalAction a) => a == TraversalAction.ClimbUp || a == TraversalAction.WindowVault || a == TraversalAction.WindowDrop
                                                             || a == TraversalAction.WindowJump || a == TraversalAction.HangClimb || a == TraversalAction.HangClimbRail || a == TraversalAction.HangClimbOver || a == TraversalAction.HangFlip;
        private static bool IsLanding(TraversalAction a) => a == TraversalAction.Land || a == TraversalAction.LandHigh || a == TraversalAction.LandRoll || a == TraversalAction.LandHard;

        private void Awake() => Init();

        private void Init()
        {
            cc = GetComponent<CharacterController>();
            inputSource = GetComponent<ICharacterInputSource>();
            state = GetComponent<ICharacterState>();
            attack = GetComponent<ICharacterAttackState>();
        }

        private void OnEnable()
        {
            state ??= GetComponent<ICharacterState>();
            if (state != null) state.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            if (state != null) state.Jumped -= OnJumped;
            action = TraversalAction.None;
        }

        private void OnJumped() => jumpedFlag = true;

        private void FixedUpdate()
        {
            if (cc == null) Init(); // Play 중 재컴파일로 참조가 비었을 때
            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            float dt = Time.fixedDeltaTime;
            regrab -= dt;
            if (skill == null) TryGetComponent(out skill);
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            bool dead = health != null && health.IsDead;
            bool stunned = stun != null && stun.IsStunned;
            bool grounded = state == null || state.IsGrounded;

            if ((IsScripted(action) || action == TraversalAction.HangGrab || action == TraversalAction.Hang) && (dead || stunned))
            {
                Release(); // 기절·쓰러짐: 놓친다
                return;
            }
            switch (action)
            {
                case TraversalAction.ClimbUp: case TraversalAction.WindowVault: case TraversalAction.WindowDrop: case TraversalAction.WindowJump:
                case TraversalAction.HangClimb: case TraversalAction.HangClimbRail: case TraversalAction.HangClimbOver: case TraversalAction.HangFlip:
                    actionTime += dt;
                    // 진행도 1인 틱에 이동이 끝 자리로 옮긴 뒤 다음 틱에 끝낸다.
                    if (actionTime > actionDuration + dt * 0.5f) FinishScripted();
                    return;
                case TraversalAction.HangGrab:
                    actionTime += dt;
                    if (actionTime >= actionDuration) { action = TraversalAction.Hang; actionTime = 0f; actionDuration = 0f; }
                    return;
                case TraversalAction.Hang:
                    HandleHang(input);
                    return;
            }
            if (IsLanding(action))
            {
                actionTime += dt;
                if (actionTime >= actionDuration) { action = TraversalAction.None; actionTime = actionDuration = 0f; }
            }

            TrackAir(grounded, dead, input);
            if (dead || stunned || IsBusy()) return;

            Vector3 dir = MoveDirection(input);
            // 점프 중 창틀을 넘어가려 하면(날아가는 방향 앞에 창틀) 반드시 짚고 넘는다 — 방향키를 놓아도 날아가던 방향으로.
            if (!grounded && airborne && jumped && action == TraversalAction.None && TryStartWindowJump(FlightDirection(dir))) return;
            if (dir == Vector3.zero) return;
            if (grounded && input.Jump && (action == TraversalAction.None || IsLanding(action)))
            {
                if (TryStartWindow(dir) || TryStartClimbUp(dir)) return;
            }
            if (!grounded && regrab <= 0f && (action == TraversalAction.None || action == TraversalAction.Fall)) TryStartHang(dir);
        }

        /// <summary>공중에서 날아가는 수평 방향(빠르면 속도 방향, 아니면 방향키).</summary>
        private Vector3 FlightDirection(Vector3 inputDir)
        {
            if (state == null) return inputDir;
            Vector2 v = state.LocalPlanarVelocity;
            if (v.sqrMagnitude < 1f) return inputDir;
            Vector3 w = transform.rotation * new Vector3(v.x, 0f, v.y);
            w.y = 0f;
            return w.normalized;
        }

        /// <summary>공격 중·스킬 시전 중에는 오르지 않는다.</summary>
        private bool IsBusy() =>
            (attack != null && attack.AttackState != CharacterAttackState.None) || SkillBusy();

        // 켜 둔 채 다니는 스킬(용병 아대)은 창틀 넘기·턱 오르기를 막지 않는다(AllowsTraversal).
        private bool SkillBusy() => skill != null && !skill.AllowsTraversal && (skill.LocksActions || skill.State == SkillState.Casting || skill.State == SkillState.Recovering);

        /// <summary>방향키가 가리키는 월드 수평 방향(몸 = 시야 방향 기준). 누르지 않으면 0.</summary>
        private Vector3 MoveDirection(in CharacterInputFrame input)
        {
            if (input.Move.sqrMagnitude < 0.25f) return Vector3.zero;
            Vector3 d = Quaternion.Euler(0f, input.LookYaw, 0f) * new Vector3(input.Move.x, 0f, input.Move.y);
            d.y = 0f;
            return d.normalized;
        }

        // ───────────────────── 낙하·착지 ─────────────────────

        private void TrackAir(bool grounded, bool dead, in CharacterInputFrame input)
        {
            Vector3 pos = transform.position;
            if (!grounded)
            {
                if (!airborne)
                {
                    airborne = true;
                    airStartY = peakY = pos.y;
                    jumped = jumpedFlag;
                    knocked = TryGetComponent(out Knockback kb) && kb.Active;
                    // 뒤로 떨어지는지는 떨어지기 시작할 때(땅에서 떨어지는 틱) 정한다 — 넉백, 뒤 방향키, 뒤로 가던 속도. 공중에서 뒤를 눌러도 바뀌지 않는다.
                    backAtStart = knocked || input.Move.y < -0.5f || (state != null && state.LocalPlanarVelocity.y < -backwardSpeed);
                    safeJump = jumped && !backAtStart; // 앞(옆)으로 뛰어내림: 구르며 착지하고 다치지 않는다
                }
                jumpedFlag = false;
                peakY = Mathf.Max(peakY, pos.y);
                if (IsLanding(action)) action = TraversalAction.None; // 착지하다 다시 떨어짐(구르다 턱 끝에서 등)
                if (action == TraversalAction.None && !jumped && airStartY - pos.y > fallStartDrop && !SkillBusy())
                {
                    bool back = backAtStart || (TryGetComponent(out Knockback k) && k.Active);
                    StartAction(TraversalAction.Fall, 0f);
                    fallBackward = back;
                }
                return;
            }
            if (!airborne) { jumpedFlag = false; hangDrop = false; safeJump = false; return; } // 다치지 않는 낙하 표시는 착지한 다음 틱에 끝(FallDamage가 착지 틱에 묻는다)
            airborne = false;
            bool walkedOff = action == TraversalAction.Fall;
            if (walkedOff) action = TraversalAction.None;
            float drop = peakY - pos.y;
            if (dead || SkillBusy()) { }
            else if (hangDrop && drop < hangDropSafeHeight) { if (drop >= lowFall) { StartAction(TraversalAction.Land, landTime); landStop = 0f; } } // 매달렸다 놓음: 다치지 않게 가볍게만
            else StartLanding(drop, jumped && !backAtStart, walkedOff); // 뒤로 뛰어내림은 구르지 않는다(걸어서 떨어진 것처럼 크게 착지)
            jumped = false;
        }

        private void StartLanding(float drop, bool fromJump, bool walkedOff)
        {
            if (drop >= hardFall) { StartAction(TraversalAction.LandHard, landHardTime); landStop = landHardStop; }
            else if (fromJump && drop >= rollFall)
            {
                StartAction(TraversalAction.LandRoll, landRollTime);
                faceYaw = transform.eulerAngles.y;
                float run = state != null ? state.RunSpeed : 6f;
                float planar = state != null ? state.LocalPlanarVelocity.magnitude : 0f;
                rollSpeed = Mathf.Max(run * 0.7f, planar);
            }
            else if (!fromJump && drop >= highFall) { StartAction(TraversalAction.LandHigh, landHighTime); landStop = landHighStop; }
            else if ((walkedOff && drop >= lowFall) || drop >= landAny) { StartAction(TraversalAction.Land, landTime); landStop = 0f; }
        }

        // ───────────────────── 창틀 넘기·올라서기 ─────────────────────

        private struct Window { public Ledge Sill; public Vector3 Mid, Beyond; public RaycastHit Floor; public float OtherDrop; }

        /// <summary>
        /// dir 쪽 창틀: 바닥(groundY) 기준 키의 sillMin~sillMax 높이 턱, 두께 sillMaxThickness 이하, 위가 열린 틈, 반대편 바닥.
        /// OtherDrop = 이쪽 바닥 - 반대편 바닥(+ = 반대편이 낮다).
        /// </summary>
        private bool FindWindow(Vector3 dir, float groundY, float reach, out Window w)
        {
            w = default;
            float h = cc.height;
            Vector3 up = Vector3.up;
            if (!FindLedge(dir, groundY, sillMin * h, sillMax * h, reach, out var l)) return false;
            // 두께: 반대편에서 거꾸로 쏴서 뒷면을 찾는다(너무 두꺼우면 시작점이 벽 안이라 안 맞는다).
            float probe = sillMaxThickness + 0.05f;
            Vector3 far = l.Face - l.Normal * probe; far.y = l.Top - 0.08f;
            if (!Cast(far, l.Normal, probe, out var back)) return false;
            float thick = probe - back.distance;
            Vector3 mid = l.Face - l.Normal * (thick * 0.5f); mid.y = l.Top;
            // 위가 열린 틈이어야 한다(창문).
            if (OverlapSphere(mid + up * 0.6f, 0.25f) || OverlapSphere(mid + up * Mathf.Min(1.4f, h * 0.6f), 0.25f)) return false;
            // 반대편 바닥
            Vector3 o = l.Face - l.Normal * (thick + cc.radius + 0.2f); o.y = l.Top + 0.3f;
            if (OverlapSphere(o, 0.1f) || !Cast(o, Vector3.down, o.y - groundY + 8f, out var floor) || floor.normal.y < 0.7f) return false;
            w = new Window { Sill = l, Mid = mid, Beyond = o, Floor = floor, OtherDrop = groundY - floor.point.y };
            return w.OtherDrop >= -0.4f; // 반대편이 더 높으면 넘지 않는다
        }

        private bool TryStartWindow(Vector3 dir)
        {
            Vector3 pos = transform.position;
            if (!FindWindow(dir, pos.y, sillReach, out var w)) return false;
            faceYaw = YawOf(-w.Sill.Normal);
            pathStart = pos;
            if (w.OtherDrop <= dropMin)
            {
                if (!StandRoom(w.Floor.point)) return false;
                pathEnd = w.Floor.point;
                StartAction(TraversalAction.WindowVault, HunterPath ? hunterVaultTime : vaultTime);
            }
            else
            {
                Vector3 edge = new(w.Beyond.x, w.Sill.Top + 0.02f, w.Beyond.z); // 창밖 허공(턱 높이) — 여기서 놓으면 떨어진다
                if (OverlapCapsule(edge, cc.radius * 0.9f)) return false;
                pathMid = w.Mid + Vector3.up * 0.02f;
                pathEnd = edge;
                // 창틀 모션은 생존자만: 감시자는 감시자 넘기(Jump)로 창밖까지 뛰어넘고 그대로 떨어진다.
                if (HunterPath) StartAction(TraversalAction.WindowVault, hunterVaultTime);
                else StartAction(TraversalAction.WindowDrop, windowDropTime);
            }
            return true;
        }

        /// <summary>점프 중: 날아가는 방향 앞에 창틀이 있고 반대편 바닥이 비슷한 높이면 반드시 창틀을 짚고 넘는다.</summary>
        private bool TryStartWindowJump(Vector3 dir)
        {
            if (dir == Vector3.zero) return false;
            Vector3 pos = transform.position;
            if (!Cast(pos + Vector3.up * 0.1f, Vector3.down, 3f, out var ground)) return false; // 바닥 높이 기준으로 창틀을 잰다
            float speed = state != null ? state.LocalPlanarVelocity.magnitude : 0f;
            if (!FindWindow(dir, ground.point.y, sillReach + speed * 0.12f, out var w) || w.OtherDrop > dropMin) return false;
            if (pos.y > w.Sill.Top + cc.height * 0.5f) return false; // 창틀보다 한참 위로 날아감(짚을 수 없다)
            Vector3 end = w.Floor.point - w.Sill.Normal * windowJumpCarry;
            if (!StandRoom(end)) { end = w.Floor.point; if (!StandRoom(end)) return false; }
            faceYaw = YawOf(-w.Sill.Normal);
            pathStart = pos;
            pathEnd = end;
            StartAction(TraversalAction.WindowJump, HunterPath ? hunterVaultTime : windowJumpTime);
            return true;
        }

        private bool TryStartClimbUp(Vector3 dir)
        {
            float h = cc.height;
            if (!FindLedge(dir, transform.position.y, climbMin * h, climbMax * h, climbReach, out var l)) return false;
            if (!HasStandRoom(l, out var stand)) return false;
            faceYaw = YawOf(-l.Normal);
            pathStart = transform.position;
            pathEnd = stand;
            StartAction(TraversalAction.ClimbUp, climbTime);
            return true;
        }

        // ───────────────────── 매달리기 ─────────────────────

        private bool TryStartHang(Vector3 dir)
        {
            if (!FindHang(transform.position, dir, out var l, out bool wall, out var hp)) return false;
            ledge = l;
            braced = wall;
            hangPos = hp;
            faceYaw = YawOf(-l.Normal);
            StartAction(TraversalAction.HangGrab, wall ? bracedGrabTime : grabTime);
            return true;
        }

        /// <summary>
        /// 발이 pos에 있을 때 dir 쪽으로 잡을 턱과 매달릴 자리(hp = 매달린 자세의 루트). 벽 종류·두께와 상관없이 손이 닿는 모서리면 잡는다.
        /// 턱 아래가 벽이면 발로 벽을 짚고(루트는 바닥 아래로 내려가도 된다 — 몸의 가장 낮은 곳은 벽을 짚은 발), 비었으면 늘어져 매달린다(늘어진 발 = 루트).
        /// </summary>
        private bool FindHang(Vector3 pos, Vector3 dir, out Ledge l, out bool wall, out Vector3 hp)
        {
            float hu = HumanUnit();
            wall = false; hp = default;
            float lo = (FreeHangTop - pullDown) * hu, hi = (BracedHangTop + pullUp) * hu;
            if (!FindLedge(pos, dir, lo, hi, hangReach, out l)) return false;
            Vector3 basePos = l.Face + l.Normal * (cc.radius + Skin);
            // 턱 아래 벽이 있으면 발로 짚는다.
            Vector3 foot = basePos; foot.y = l.Top - (BracedHangTop - BracedFeet) * hu;
            wall = Cast(foot, -l.Normal, cc.radius + 0.4f, out _);
            hp = basePos; hp.y = l.Top - (wall ? BracedHangTop : FreeHangTop) * hu;
            float dy = pos.y - hp.y;
            // 손이 닿는 가장 높은 턱은 늘어진 매달림도 벽을 짚는 매달림과 같게(늘어진 자세는 손이 낮은 만큼 더 끌어 올린다).
            float pull = (pullUp + (wall ? 0f : BracedHangTop - FreeHangTop)) * hu;
            if (dy < -pull || dy > pullDown * hu) return false;
            // 몸의 가장 낮은 곳(늘어진 발 = 루트, 벽을 짚은 발 = 루트 + BracedFeet) 바로 아래가 바닥이면 매달릴 높이가 아니다.
            Vector3 low = hp + Vector3.up * ((wall ? BracedFeet * hu - BracedFloorGap : 0f) + 0.3f);
            if (OverlapSphere(low, 0.05f) || Cast(low, Vector3.down, 0.45f, out _)) return false;
            return true;
        }

        /// <summary>
        /// 발이 feet에 있을 때 dir 쪽 턱을 점프해서 잡을 수 있는지, 매달린 뒤 앞으로 가면 무엇을 하는지(HangClimb = 위에 섬, HangClimbRail = 난간 넘어 뒤 바닥,
        /// HangClimbOver = 벽 한 장 넘어 반대편으로 떨어짐, None = 매달리기만). 상태는 바꾸지 않는다(에디터 검사·AI용).
        /// </summary>
        public bool ProbeGrab(Vector3 feet, Vector3 dir, out TraversalAction climb)
        {
            climb = TraversalAction.None;
            if (cc == null) Init();
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f || !FindHang(feet, dir.normalized, out var l, out _, out _)) return false;
            climb = HasStandRoom(l, out _) ? TraversalAction.HangClimb
                  : FindFloorBehindRail(l, out _) ? TraversalAction.HangClimbRail
                  : FindOverThinTop(l, out _) ? TraversalAction.HangClimbOver : TraversalAction.None;
            return true;
        }

        private void HandleHang(in CharacterInputFrame input)
        {
            // 앞(또는 점프) = 올라가기, 뒤 = 놓고 떨어지기. 올라갈 자리가 없으면 앞은 그대로 매달림.
            if ((input.Move.y > 0.5f || input.Jump) && TryStartHangClimb()) return;
            if (input.Move.y < -0.5f && !TryStartHangFlip()) Release(); // 생존자는 다치지 않을 높이면 벽을 박차고 뒤로 공중제비
        }

        /// <summary>
        /// 생존자: 매달린 채 뒤(S) — 벽 반대쪽으로 flipBack 물러난 아래 바닥이 다치지 않을 높이(hangDropSafeHeight 미만)면 벽을 박차고 뒤로 공중제비를 돌아 거기 내려선다.
        /// 감시자이거나 뒤가 막혔거나 너무 높으면 거짓(그냥 놓고 떨어진다).
        /// </summary>
        private bool TryStartHangFlip()
        {
            if (hunterStyle) return false;
            float hu = HumanUnit();
            Vector3 pos = transform.position, n = ledge.Normal;
            Vector3 body = pos + Vector3.up * (BracedFeet * hu); // 매달린 몸(벽을 짚은 발) 높이 — 낮은 벽에서 루트가 바닥 아래여도 바닥 위
            float back = flipBack;
            if (Cast(body, n, back + cc.radius, out var blocked)) back = blocked.distance - cc.radius - 0.05f;
            if (back < 0.5f) return false;
            Vector3 land = body + n * back;
            if (OverlapSphere(land, cc.radius * 0.8f)) return false;
            if (!Cast(land, Vector3.down, BracedFeet * hu + hangDropSafeHeight, out var floor) || floor.normal.y < 0.7f) return false;
            if (pos.y - floor.point.y >= hangDropSafeHeight || !StandRoom(floor.point)) return false;
            pathStart = pos;
            pathEnd = floor.point;
            StartAction(TraversalAction.HangFlip, flipTime);
            hangDrop = true; // 매달렸다 놓은 낙하로 친다 — 다치지 않는다(StartAction이 끈 뒤에 켠다)
            return true;
        }

        private bool TryStartHangClimb()
        {
            float hu = HumanUnit();
            pathStart = transform.position;
            if (HasStandRoom(ledge, out var stand))
            {
                pathEnd = stand;
                root0 = ledge.Top - HangClimbTop * hu;
                StartAction(TraversalAction.HangClimb, hangClimbTime);
                return true;
            }
            if (FindFloorBehindRail(ledge, out stand))
            {
                pathEnd = stand;
                root0 = ledge.Top - RailClimbTop * hu;
                railTop = ledge.Top;
                StartAction(TraversalAction.HangClimbRail, railClimbTime);
                return true;
            }
            if (FindOverThinTop(ledge, out var over))
            {
                // 벽 한 장(창틀 윗부분·담 꼭대기): 매달린 손 그대로 꼭대기를 잡고 끌어 올라(Freehang Climb) 꼭대기에 올라선 뒤 넘어가자마자 반대편으로 떨어진다.
                pathEnd = over;
                root0 = ledge.Top - HangClimbTop * hu;
                railTop = ledge.Top;
                StartAction(TraversalAction.HangClimbOver, hangClimbTime * OverClipShare);
                return true;
            }
            return false;
        }

        /// <summary>벽 한 장 넘기는 매달려 올라가기(Freehang Climb) 모션의 앞부분(꼭대기에 올라섬까지)만 쓴다 — 허리를 펴는 뒷부분은 자르고 넘어가 바로 떨어진다.</summary>
        public const float OverClipShare = 0.8f;

        /// <summary>
        /// 얇은 꼭대기(두께 1.2m 이하 — 벽 한 장·창틀 윗부분) 너머: 꼭대기 위가 비고, 넘어간 자리(반대편 허공, 꼭대기 높이)에 몸이 들어가고, 반대편이 더 낮으면 그 자리.
        /// </summary>
        private bool FindOverThinTop(in Ledge l, out Vector3 over)
        {
            over = default;
            const float probe = 1.25f;
            Vector3 far = l.Face - l.Normal * probe; far.y = l.Top - 0.08f;
            if (!Cast(far, l.Normal, probe, out var back)) return false; // 두껍다(위에 설 자리가 없을 뿐인 두꺼운 턱)
            float thick = probe - back.distance;
            Vector3 mid = l.Face - l.Normal * (thick * 0.5f); mid.y = l.Top;
            if (OverlapSphere(mid + Vector3.up * 0.5f, 0.25f)) return false; // 꼭대기 위가 막힘(천장 등)
            Vector3 beyond = l.Face - l.Normal * (thick + cc.radius + 0.15f); beyond.y = l.Top + 0.03f;
            if (OverlapCapsule(beyond, cc.radius * 0.9f)) return false;           // 넘어간 자리가 막힘
            if (Cast(beyond + Vector3.up * 0.1f, Vector3.down, 0.4f, out _)) return false; // 반대편이 바로 바닥 = 난간 넘기·올라서기 쪽
            over = beyond;
            return true;
        }

        public bool IsSafeFall(float drop) => (hangDrop && drop < hangDropSafeHeight) || (safeJump && drop < hardFall);

        private void Release()
        {
            LiftOutOfFloor();
            hangDrop = action == TraversalAction.Hang || action == TraversalAction.HangGrab; // 매달렸다 놓음: 아주 높지 않으면 다치지 않는다
            action = TraversalAction.None;
            actionTime = actionDuration = 0f;
            regrab = regrabDelay;
            airborne = false; // 놓은 자리부터 떨어진 높이를 잰다
            jumpedFlag = false;
        }

        // ───────────────────── 매달린 채 넉백 ─────────────────────

        [Header("매달린 채 넉백")]
        [Tooltip("매달린(잡는 중·매달려 올라가는 중 포함) 채 넉백되면 손을 놓고 벽 바깥으로 밀려 떨어진다. 미는 힘 가운데 벽 바깥쪽 몫을 이 비율 이상으로 돌린다(벽 쪽으로 밀려 벽에 박히지 않게).")]
        [SerializeField, Range(0f, 1f)] private float knockOffOutward = 0.7f;
        [Tooltip("미는 힘이 이보다 약하면 이 속도(m/s)로 벽 바깥으로 민다.")]
        [SerializeField, Min(0f)] private float knockOffMinSpeed = 4f;
        [Tooltip("넉백으로 떨어진 뒤 다시 턱을 잡을 수 없는 시간(초).")]
        [SerializeField, Min(0f)] private float knockOffRegrab = 1f;

        private static bool IsHanging(TraversalAction a) => a == TraversalAction.HangGrab || a == TraversalAction.Hang
                                                            || a == TraversalAction.HangClimb || a == TraversalAction.HangClimbRail || a == TraversalAction.HangClimbOver;

        /// <summary>
        /// 넉백(Knockback.Push) 직전: 턱을 잡고 있으면(잡기·매달림·매달려 올라가기) 손이 풀려 떨어진다. 미는 방향은 벽 바깥쪽으로 돌린다.
        /// 떨어지는 동안은 넉백 낙하(하늘을 보며 등으로 — FallBackward), 낙하 데미지는 손으로 놓은 것과 같다(매달린 높이부터 hangDropSafeHeight 미만이면 없음).
        /// </summary>
        public void OnKnockback(ref Vector3 velocity)
        {
            if (!IsHanging(action)) return;
            Vector3 n = Vector3.ProjectOnPlane(ledge.Normal, Vector3.up); // 벽 → 캐릭터(바깥)
            if (n.sqrMagnitude > 1e-4f)
            {
                n.Normalize();
                Vector3 v = Vector3.ProjectOnPlane(velocity, Vector3.up);
                float speed = Mathf.Max(v.magnitude, knockOffMinSpeed);
                Vector3 side = Vector3.ProjectOnPlane(v, n);
                float outward = Mathf.Max(Vector3.Dot(v, n), speed * knockOffOutward);
                Vector3 dir = side + n * outward;
                velocity = (dir.sqrMagnitude > 1e-4f ? dir.normalized : n) * speed;
            }
            Release();
            hangDrop = true; // 놓은 낙하로 친다(매달린 높이부터 아주 높지 않으면 다치지 않음)
            regrab = knockOffRegrab;
        }

        /// <summary>낮은 벽에 발로 짚고 매달리면 루트(모션의 바닥)가 바닥 아래에 있을 수 있다 — 놓을 때 발을 바닥 위로 올린다.</summary>
        private void LiftOutOfFloor()
        {
            Vector3 pos = transform.position;
            float half = cc.height * 0.5f;
            if (!Cast(pos + Vector3.up * half, Vector3.down, half, out var floor) || floor.normal.y < 0.7f || floor.point.y <= pos.y) return;
            cc.enabled = false;
            transform.position = new Vector3(pos.x, floor.point.y, pos.z);
            cc.enabled = true;
        }

        private void FinishScripted()
        {
            var was = action;
            action = TraversalAction.None;
            actionTime = actionDuration = 0f;
            airborne = false;
            jumpedFlag = was == TraversalAction.WindowDrop || (was == TraversalAction.WindowVault && HunterPath); // 창밖으로 뛰어내림 = 점프해서 떨어진 것(높으면 구르기)
        }

        private void StartAction(TraversalAction a, float duration)
        {
            action = a;
            actionTime = 0f;
            actionDuration = duration;
            fallBackward = false;
            if (a != TraversalAction.Fall && !IsLanding(a)) hangDrop = false; // 다시 잡거나 오르면 놓은 낙하가 아니다
            // 감시자: 들어오던 수평 속도(월드)를 기억해 넘기 길이 그 속도로 출발하게 한다.
            Vector2 v = state != null ? state.LocalPlanarVelocity : Vector2.zero;
            entryVelocity = transform.rotation * new Vector3(v.x, 0f, v.y);
        }

        /// <summary>
        /// 감시자 넘기 진행도(0~1): a~b 동안 들어오던 속도(× hunterSpeedCarry)로 출발해, carryOut이면 같은 빠르기로 끝난다(Hermite — 멈췄다 출발하지 않게).
        /// carryOut이 아니면(b 뒤에 멈춰 있는 길) 멈추며 끝난다. maxIn = 출발 기울기 한도(작을수록 처음에 덜 앞서 간다). 가만히 서서 시작하면 SmoothStep과 같다.
        /// </summary>
        private float Carry(float p, float a, float b, bool carryOut = true, float maxIn = 2.1f)
        {
            float u = Mathf.InverseLerp(a, b, p);
            Vector3 path = pathEnd - pathStart; path.y = 0f;
            float length = path.magnitude, span = (b - a) * actionDuration;
            float m0 = 0f, m1 = 0f;
            if (length > 0.05f && span > 0.01f)
            {
                Vector3 along = path / length;
                float inSpeed = Mathf.Max(0f, Vector3.Dot(entryVelocity, along)) * hunterSpeedCarry;
                // 기울기 2.1 이하(두 기울기 제곱 합 ≤ 9): 길을 지나쳤다 되돌아오지 않는다.
                m0 = Mathf.Min(inSpeed * span / length, Mathf.Min(maxIn, 2.1f));
                if (carryOut) m1 = Mathf.Min(entryVelocity.magnitude * hunterSpeedCarry * span / length, 2.1f);
            }
            float u2 = u * u, u3 = u2 * u;
            return (u3 - 2f * u2 + u) * m0 + (3f * u2 - 2f * u3) + (u3 - u2) * m1;
        }

        // ───────────────────── 이동 대신 정하기(CharacterMotor가 부른다) ─────────────────────

        public bool TryGetVelocity(out Vector3 worldVelocity, out bool passThrough)
        {
            passThrough = true;
            worldVelocity = Vector3.zero;
            float dt = Time.fixedDeltaTime;
            Vector3 pos = transform.position;
            if (action == TraversalAction.HangGrab || action == TraversalAction.Hang)
            {
                worldVelocity = Vector3.ClampMagnitude((hangPos - pos) / (hunterStyle ? snapTime * 1.6f : snapTime), 25f); // 감시자는 조금 천천히 붙는다
                return true;
            }
            if (!IsScripted(action)) return false;
            worldVelocity = Vector3.ClampMagnitude((PathPoint(ActionProgress) - pos) / dt, 30f);
            return true;
        }

        private Vector3 PathPoint(float p)
        {
            Vector3 s = pathStart, e = pathEnd;
            // 감시자: 넘기·올라서기는 들어오던 속도로 출발해 속도를 이어 나가고(Carry), 매달려 올라가기는 턱 쪽으로 옮기는 구간을 늘린다.
            bool hunter = hunterStyle;
            float spread = hunter ? hunterClimbSpread : 0f;
            switch (action)
            {
                case TraversalAction.ClimbUp: // Running Jump: 발이 떨어짐(0.08) → 높은 바닥에 닿음(0.42)
                {
                    // 감시자: 달려오던 속도로 출발(발이 아직 낮을 때 너무 앞서 가 벽에 파묻히지 않게 기울기 1까지), 올라선 뒤(0.6~)는 그대로 — 끝 속도는 모션이 멈춰 서므로 이어 가지 않는다.
                    Vector3 xz = Vector3.Lerp(s, e, hunter ? Carry(p, 0f, 0.6f, false, 1f) : Smooth(0.12f, 0.6f, p));
                    xz.y = Mathf.Lerp(s.y, e.y + 0.02f, Smooth(0.08f, 0.42f, p));
                    return xz;
                }
                case TraversalAction.WindowVault: // 생존자 Jumping (1)을 느리게: 손 짚음 → 다리 넘김(0.33~0.55) → 착지(0.78) / 감시자 Jump: 뛰어오름(0.1) → 착지(0.75)
                {
                    if (!HunterPath) return Vector3.Lerp(s, e, Smooth(0.08f, 0.82f, p));
                    Vector3 xz = Vector3.Lerp(s, e, hunter ? Carry(p, 0f, 1f) : Smooth(0.1f, 0.75f, p));
                    xz.y = Mathf.Lerp(s.y, e.y, Smooth(0.1f, 0.75f, p));
                    return xz;
                }
                case TraversalAction.WindowJump: // Jumping (1): 손 짚음 → 다리 넘김(0.35~0.55) → 착지(0.78) — 날아오던 속도 그대로 앞으로
                {
                    Vector3 xz = Vector3.Lerp(s, e, hunter ? Carry(p, 0f, 1f) : HunterPath ? Smooth(0.1f, 0.75f, p) : p);
                    xz.y = Mathf.Lerp(s.y, e.y, Smooth(0.1f, 0.78f, p)); // 모션이 넘는 높이를 낸다(높이는 모션 그대로) — 루트는 반대편 바닥으로
                    return xz;
                }
                case TraversalAction.WindowDrop: // 턱 위로 올라섬(0~0.3) → 앞으로 뛰어내림(0.75~1)
                {
                    Vector3 onSill = Vector3.Lerp(s, pathMid, Smooth(0.05f, 0.35f, p));
                    onSill.y = Mathf.Lerp(s.y, pathMid.y, Smooth(0f, 0.3f, p));
                    return Vector3.Lerp(onSill, e, Smooth(0.75f, 1f, p));
                }
                case TraversalAction.HangClimb:
                {
                    float y0 = Mathf.Lerp(s.y, root0, Smooth(0f, 0.06f + spread, p));
                    Vector3 xz = Vector3.Lerp(s, e, Smooth(0.72f, Mathf.Min(1f, 0.9f + spread), p)); // 시작은 그대로(발이 턱보다 낮을 때 벽 쪽으로 가지 않게), 끝만 늘림
                    xz.y = y0 + hangClimbRise.Evaluate(p) * (e.y - root0);
                    return xz;
                }
                case TraversalAction.HangClimbRail:
                {
                    float y0 = Mathf.Lerp(s.y, root0, Smooth(0f, 0.08f + spread, p));
                    float top = railTop + 0.05f;
                    Vector3 xz = Vector3.Lerp(s, e, Smooth(0.4f, Mathf.Min(1f, 0.95f + spread), p));
                    xz.y = y0 + railClimbRise.Evaluate(p) * (top - root0) - Smooth(0.7f, 1f, p) * (top - e.y); // 난간을 넘은 뒤 그 뒤 바닥으로 내려선다
                    return xz;
                }
                case TraversalAction.HangFlip: // Backflip: 벽을 박차고 뛰어오름 → 뒤로 돌며 떨어짐(높이는 점점 빨리 — 중력처럼) → 발이 닿음(flipLand) → 일어섬
                {
                    float u = Mathf.Clamp01(p / flipLand);
                    Vector3 xz = Vector3.Lerp(s, e, Smooth(0f, flipLand, p));
                    xz.y = Mathf.Lerp(s.y, e.y, u * u);
                    return xz;
                }
                case TraversalAction.HangClimbOver: // Freehang Climb 앞부분(OverClipShare): 손은 꼭대기에 둔 채 끌어 올라 무릎·발을 올림 → 꼭대기에 올라섬 → 넘어감. 끝나면 바로 떨어진다.
                {
                    float q = p * OverClipShare; // 원래 모션(매달려 올라가기) 기준 진행도
                    float y0 = Mathf.Lerp(s.y, root0, Smooth(0f, 0.06f + spread, q));
                    Vector3 xz = Vector3.Lerp(s, e, Smooth(0.86f, 1f, p)); // 꼭대기에 다 올라선 뒤 넘어간다
                    xz.y = y0 + hangClimbRise.Evaluate(q) * (railTop + 0.03f - root0);
                    return xz;
                }
            }
            return transform.position;
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            if (action == TraversalAction.LandRoll)
            {
                localMove = Vector2.up; // 착지한 방향으로 구른다
                speed = Mathf.Lerp(rollSpeed, (state != null ? state.WalkSpeed : 3.6f) * 0.6f, ActionProgress);
                allowJump = false;
                return true;
            }
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        public bool TryGetFacingYaw(out float yaw)
        {
            yaw = faceYaw;
            return IsScripted(action) || action == TraversalAction.HangGrab || action == TraversalAction.Hang || action == TraversalAction.LandRoll;
        }

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            float walk = state != null ? state.WalkSpeed : 3.6f;
            maxSpeed = 0f;
            switch (action)
            {
                case TraversalAction.Land:
                    maxSpeed = walk * landSlow;
                    return ActionProgress < 0.6f;
                case TraversalAction.LandHigh:
                case TraversalAction.LandHard:
                    maxSpeed = actionTime < landStop ? 0f : walk * landSlow;
                    return true;
            }
            return false;
        }

        // ───────────────────── 지형 찾기 ─────────────────────

        /// <summary>
        /// dir 쪽 앞(몸 둘레 + reach 안)의 턱: 마주 본 수직 면, 그 위 윗면(feetY 기준 minAbove~maxAbove 높이), 윗면 바로 위는 빔.
        /// </summary>
        private bool FindLedge(Vector3 dir, float feetY, float minAbove, float maxAbove, float reach, out Ledge result)
        {
            Vector3 feet = transform.position; feet.y = feetY; // 높이 기준(땅에서는 발, 공중 창틀 짚기는 아래 바닥)
            return FindLedge(feet, dir, minAbove, maxAbove, reach, out result);
        }

        private bool FindLedge(Vector3 feet, Vector3 dir, float minAbove, float maxAbove, float reach, out Ledge result)
        {
            result = default;
            Vector3 up = Vector3.up;
            float dist = cc.radius + reach;
            bool found = false;
            RaycastHit wall = default;
            // 낮은 곳부터 높은 곳까지 앞으로 쏴서, 맞은 것 중 가장 높은 수직 면(창문이면 창턱 바로 아래)을 쓴다.
            for (float h = Mathf.Max(0.15f, minAbove - 0.3f); h <= maxAbove - 0.02f; h += 0.12f)
                if (Cast(feet + up * h, dir, dist, out var hit) && Mathf.Abs(hit.normal.y) < 0.3f) { wall = hit; found = true; }
            if (!found) return false;
            Vector3 n = Vector3.ProjectOnPlane(wall.normal, up);
            if (n.sqrMagnitude < 0.25f) return false;
            n.Normalize();
            if (Vector3.Dot(n, -dir) < 0.64f) return false; // 마주 본 면만(약 50도 안)
            Vector3 inside = wall.point - n * LedgeInset;
            Vector3 start = new(inside.x, feet.y + maxAbove + 0.35f, inside.z);
            if (OverlapSphere(start, 0.08f)) return false; // 위가 막힘(벽이 더 높거나 천장)
            if (!Cast(start, Vector3.down, maxAbove + 0.4f - minAbove, out var topHit) || topHit.normal.y < 0.7f) return false;
            if (NoGrab(wall.collider) || NoGrab(topHit.collider)) return false; // 계단(단·옆면·경사)·잡지 않기로 표시한 것은 턱이 아니다
            float above = topHit.point.y - feet.y;
            if (above < minAbove || above > maxAbove) return false;
            if (OverlapSphere(new Vector3(inside.x, topHit.point.y + 0.3f, inside.z), 0.12f)) return false; // 손 놓을 자리 위가 막힘
            result = new Ledge { Top = topHit.point.y, Face = new Vector3(wall.point.x, topHit.point.y, wall.point.z), Normal = n };
            return true;
        }

        /// <summary>턱 위에 설 자리(턱과 같은 높이의 바닥, 몸이 들어갈 빈 곳, 턱 끝에 난간 없음).</summary>
        private bool HasStandRoom(in Ledge l, out Vector3 stand)
        {
            stand = l.Face - l.Normal * (cc.radius + 0.2f);
            if (!Cast(stand + Vector3.up * 0.4f, Vector3.down, 0.6f, out var floor) || floor.normal.y < 0.7f || Mathf.Abs(floor.point.y - l.Top) > 0.15f) return false;
            stand = floor.point;
            if (!StandRoom(stand)) return false;
            // 턱 끝(앞면 바로 안쪽)에 난간이 있으면 못 올라선다(난간 넘기로).
            Vector3 lip = l.Face - l.Normal * (cc.radius * 0.5f);
            float r = cc.radius * 0.45f;
            return !OverlapCapsule(lip + Vector3.up * (0.1f + r), lip + Vector3.up * 1.2f, r);
        }

        /// <summary>난간(얇은 턱) 뒤의 바닥(난간 윗면보다 0.3~1.5m 아래, 설 자리 있음).</summary>
        private bool FindFloorBehindRail(in Ledge l, out Vector3 stand)
        {
            foreach (float d in new[] { 0.35f, 0.6f, 0.85f })
            {
                Vector3 p = l.Face - l.Normal * (d + cc.radius); p.y = l.Top + 0.1f;
                if (OverlapSphere(p, 0.06f)) continue;
                if (!Cast(p, Vector3.down, 1.7f, out var floor) || floor.normal.y < 0.7f) continue;
                float below = l.Top - floor.point.y;
                if (below < 0.3f || below > 1.5f || !StandRoom(floor.point)) continue;
                stand = floor.point;
                return true;
            }
            stand = default;
            return false;
        }

        private bool StandRoom(Vector3 feet) => !OverlapCapsule(feet, cc.radius * 0.95f);

        private bool OverlapCapsule(Vector3 feet, float r) =>
            OverlapCapsule(feet + Vector3.up * (r + 0.05f), feet + Vector3.up * Mathf.Max(cc.height - r, r + 0.06f), r);

        /// <summary>Humanoid 단위(m): 모델 골반 높이 ≈ 1. 모션 높이를 이 캐릭터 크기로 바꿀 때 쓴다.</summary>
        private float HumanUnit()
        {
            if (animator == null || !animator.isActiveAndEnabled) animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman && animator.humanScale > 0.01f) return animator.humanScale * animator.transform.lossyScale.y;
            return cc.height / 1.75f;
        }

        private static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ───────────────────── 물리 검사(캐릭터·판정·트리거 무시) ─────────────────────

        private static int Mask => ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));

        /// <summary>
        /// 잡거나 올라서는 턱이 될 수 없는 것: NoLedgeGrab 표시가 붙은 것(과 그 자식), 블록아웃 계단(Ramp_Collider 경사 자신, 또는 형제에 Ramp_Collider가 있는 단).
        /// </summary>
        private static bool NoGrab(Collider c)
        {
            if (c == null) return false;
            if (c.GetComponentInParent<NoLedgeGrab>() != null) return true;
            if (c.name == StairRampName) return true;
            Transform p = c.transform.parent;
            return p != null && p.Find(StairRampName) != null;
        }

        private const string StairRampName = "Ramp_Collider"; // MapBlockoutBuilder.Stair()가 단 모서리를 잇는 보이지 않는 경사

        private static bool Ignored(Collider c) => c is CharacterController || c.GetComponentInParent<CharacterIdentity>() != null;

        private static bool Cast(Vector3 origin, Vector3 dir, float distance, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, distance, Mask, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (Ignored(Hits[i].collider) || Hits[i].distance >= bestDistance) continue;
                best = Hits[i];
                bestDistance = Hits[i].distance;
            }
            return bestDistance < float.MaxValue;
        }

        private static bool OverlapSphere(Vector3 center, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, Overlaps, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Overlaps[i])) return true;
            return false;
        }

        private static bool OverlapCapsule(Vector3 a, Vector3 b, float radius)
        {
            int n = Physics.OverlapCapsuleNonAlloc(a, b, radius, Overlaps, Mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!Ignored(Overlaps[i])) return true;
            return false;
        }
    }
}

using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 입력(LightAttack / HeavyCharging / HeavyRelease)을 받아 공격 상태를 진행한다.
    /// - 상태: 없음 → 약공격, 또는 없음 → 강공격 모으기 → 강공격.
    /// - 공격마다 준비 → 판정 → 후딜레이. 판정 구간에만 AttackHitDetector로 Hitbox를 켠다.
    ///   땅: 약=오른손, 강=오른손+소품. 앞으로 달리다 뛴 공중 공격(IsRunJumpAttack, 플라잉킥 등): 양발. 그 밖의 공중 공격은 땅 공격과 같다.
    /// - 다른 행동을 막는 스킬(ICharacterSkillState.LocksActions, 예: 전위 돌진)·기절 중에는 공격을 시작하지 않는다.
    /// - 공중에서는 꾹 눌러 모을 수 없다: 누르는 순간 공중 공격이 한 번 나가고, 키를 뗄 때까지 새 입력을 받지 않는다.
    /// - 공격이 끝난 뒤에도 잠깐(약 lightReattackDelay / 강 heavyReattackDelay) 새 공격을 받지 않는다. 이동은 되고 안내·쿨다운 표시는 없다.
    ///   (달리기 점프 공격에 이어 가는 두 번째 날아 차기, 맹인 후리기 잇기는 예외)
    /// - 공격 중에는 새 공격을 받지 않는다. 공중에서도 공격할 수 있고(IsAirAttack), 도중에 착지해도 공격은 끝까지 이어진다.
    /// - 땅에서 강공격을 모으는 동안에는 걷기 속도로 제한한다(IMoveSpeedLimit). 공중에서는 점프 궤적을 건드리지 않는다.
    /// 공격 방향은 몸 방향(= 시야 Yaw, CharacterMotor가 맞춘다)이다. 데미지는 다루지 않는다.
    /// </summary>
    [RequireComponent(typeof(AttackHitDetector))]
    [DisallowMultipleComponent]
    public class CharacterAttack : MonoBehaviour, ICharacterAttackState, IMoveSpeedLimit, IAttackCancel
    {
        [SerializeField] private AttackSpec lightAttack = new(0.1f, 0.1f, 0.25f, HitboxSlot.RightHand);
        [SerializeField] private AttackSpec heavyAttack = new(0.25f, 0.15f, 0.5f, HitboxSlot.RightHand | HitboxSlot.Weapon);

        [Header("다시 공격 대기 (표시 없음 — 손맛으로만 느낌)")]
        [Tooltip("약공격(공중 공격 포함)이 끝난 뒤 다시 공격을 받기까지의 시간(초). 그동안 이동은 되고, 누른 공격 키는 뗄 때까지 무시한다.")]
        [SerializeField, Min(0f)] private float lightReattackDelay = 0.3f;
        [Tooltip("강공격이 끝난 뒤 다시 공격을 받기까지의 시간(초).")]
        [SerializeField, Min(0f)] private float heavyReattackDelay = 0.5f;
        private float reattackTimer; // 남은 다시 공격 대기(초)

        [Header("달리기 점프 공격 (앞으로 달리다 뛴 공중 공격, 발 판정)")]
        [Tooltip("공중 공격을 시작할 때 앞으로 이 속도 비율(걷기 0 ~ 달리기 1) 이상이면 달리기 점프 공격으로 본다.")]
        [SerializeField, Range(0f, 1f)] private float runJumpThreshold = 0.5f;
        [SerializeField] private AttackSpec airLightAttack = new(0.12f, 0.12f, 0.30f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);
        [SerializeField] private AttackSpec airHeavyAttack = new(0.20f, 0.15f, 0.40f, HitboxSlot.LeftFoot | HitboxSlot.RightFoot);

        [Header("두 번째 점프 공격 (달리기 점프 공격 중 한 번 더 누름, 생존자)")]
        [Tooltip("달리기 점프 공격 중(또는 그 점프가 끝나기 전) 공격을 한 번 더 누르면 다시 뛰어오르며 머리를 노리는 날아 차기를 한다. 캐릭터를 고를 때 SetRunJumpFollowUp으로 켠다.")]
        [SerializeField] private bool runJumpFollowUp;
        [Tooltip("두 번째 날아 차기 구간(준비·판정·공중 마무리). 판정은 왼발(차는 발).")]
        [SerializeField] private AttackSpec followUpKick = new(0.08f, 0.22f, 0.3f, HitboxSlot.LeftFoot);
        [Tooltip("두 번째 날아 차기를 시작할 때 지금 높이에서 더 뛰어오르는 높이(m).")]
        [SerializeField, Min(0f)] private float followUpJumpHeight = 0.5f;
        [Tooltip("두 번째 날아 차기 뒤 착지해서 못 움직이고 공격도 못 하는 후딜레이(초).")]
        [SerializeField, Min(0f)] private float followUpLandRecovery = 0.45f;
        [Tooltip("머리를 노릴 상대를 찾는 거리(m, 수평)와 앞쪽 각도(도).")]
        [SerializeField, Min(0f)] private float followUpSeekRange = 4f;
        [SerializeField, Range(0f, 90f)] private float followUpSeekAngle = 60f;
        [Tooltip("판정 한가운데 시각에 차는 발이 몸(루트) 위로 올라와 있는 높이(m, 날아 차기 모션 기준).")]
        [SerializeField, Min(0f)] private float followUpFootRise = 0.85f;
        [Tooltip("머리를 노려 내려갈 때 최대 아래 속도(m/s).")]
        [SerializeField, Min(0f)] private float followUpMaxDiveSpeed = 6f;
        private bool runJumpAirborne;  // 이번 점프에서 달리기 점프 공격을 했다(착지하면 지운다)
        private bool followUpUsed;     // 이번 점프에서 두 번째 날아 차기를 이미 했다
        private float followUpLanded = -1f; // 두 번째 날아 차기 뒤 착지한 지 지난 시간(-1 = 아직 공중)

        public bool IsFollowUpKick { get; private set; }

        /// <summary>두 번째 점프 공격(달리기 점프 공격 중 한 번 더)을 켜거나 끈다(캐릭터를 고를 때, 생존자만).</summary>
        public void SetRunJumpFollowUp(bool on) => runJumpFollowUp = on;

        [Tooltip("땅 강공격 동안 이동을 멈출지(생존자 돌려차기). 캐릭터를 고를 때 SetHeavyRoots로 켠다.")]
        [SerializeField] private bool heavyRoots;

        /// <summary>땅 강공격 동안 멈춰 서서 칠지 켜거나 끈다(캐릭터를 고를 때, 생존자만).</summary>
        public void SetHeavyRoots(bool on) => heavyRoots = on;

        private ICharacterInputSource inputSource;
        private ICharacterState characterState;
        private AttackHitDetector hitDetector;
        private ICharacterSkillState skillState; // 실행 중에 붙을 수 있어 필요할 때 찾는다
        private ICharacterStun stun;
        private ICharacterHealth health; // 쓰러지면 공격하지 않는다
        private ICharacterTraversalState traversal; // 오르기·매달리기 중에는 공격하지 않는다
        private bool waitAirRelease; // 공중에서 누른 공격 키를 뗄 때까지 새 입력을 받지 않는다

        private float phaseTime;

        public CharacterAttackState AttackState { get; private set; }
        public AttackPhase AttackPhase { get; private set; }
        public float PhaseProgress { get; private set; }
        public float ChargeTime { get; private set; }
        public bool IsAirAttack { get; private set; }
        public bool IsRunJumpAttack { get; private set; }
        public int Swipe { get; private set; }

        [Tooltip("마우스를 좌우로 그어 그 방향으로 후리는 약공격을 받을지(맹인 지팡이). 캐릭터를 고를 때 SetSwipeAttacks로 켠다.")]
        [SerializeField] private bool swipeAttacks;

        /// <summary>좌우로 그어 후리기를 켜거나 끈다(캐릭터를 고를 때, 예: 맹인).</summary>
        public void SetSwipeAttacks(bool on) => swipeAttacks = on;

        [Tooltip("첫 휘두르기 뒤 받아 둘 수 있는 선입력(다음 휘두르기·E 찌르기) 최대 수. 받아 둔 입력은 후딜레이가 시작되면 바로 이어 나간다.")]
        [SerializeField, Min(0)] private int maxComboFollowUps = 2;
        private int bufferedSwipe;   // 받아 둔 다음 휘두르기 방향(0 = 없음)
        private int comboFollowUps;  // 이번 연속 동작에서 이어 간 수
        private bool chaining;       // 이어 가느라 공격을 끝내는 중(연속 동작 수를 지우지 않음)

        public bool CanCancelForSkill => swipeAttacks && (AttackState == CharacterAttackState.Light || AttackState == CharacterAttackState.Heavy) && comboFollowUps < maxComboFollowUps;

        public bool TryCancelForSkill()
        {
            if (AttackState == CharacterAttackState.None) return true;
            if (!CanCancelForSkill || AttackPhase != AttackPhase.Recovery) return false;
            EndAttack(); // 후딜레이를 끊고 E 찌르기로(스킬이 이어서 시작)
            return true;
        }

        public event Action<CharacterAttackState> AttackStarted;
        public event Action ChargeStarted;
        public event Action AttackEnded;

        public AttackSpec LightAttack => lightAttack;
        public AttackSpec HeavyAttack => heavyAttack;
        public AttackSpec AirLightAttack => airLightAttack;
        public AttackSpec AirHeavyAttack => airHeavyAttack;

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            if (AttackState == CharacterAttackState.HeavyCharge && characterState != null && characterState.IsGrounded)
            {
                maxSpeed = characterState.WalkSpeed;
                return true;
            }
            if (heavyRoots && AttackState == CharacterAttackState.Heavy && !IsAirAttack)
            {
                maxSpeed = 0f; // 땅 강공격(생존자 돌려차기) 동안은 멈춰 서서 찬다
                return true;
            }
            if (IsFollowUpKick && followUpLanded >= 0f)
            {
                maxSpeed = 0f; // 두 번째 날아 차기 뒤 착지 후딜레이: 제자리
                return true;
            }
            maxSpeed = 0f;
            return false;
        }

        private HitboxSlot defaultLightSlots, defaultHeavySlots;
        private bool slotDefaultsSaved;

        /// <summary>
        /// 약·강공격 판정 부위를 바꾼다(캐릭터를 고를 때, 예: 리퍼 = 왼손 + 무기). None이면 처음(인스펙터) 값으로 되돌린다.
        /// </summary>
        /// <summary>
        /// 지금 누르고 있는 공격 키는 뗄 때까지 무시한다(뗄 때 나오는 약공격·강공격 입력도 버린다).
        /// 다른 기능이 공격 키 누름을 이미 썼을 때 부른다(예: 광대 로켓 들기를 좌클릭으로 취소 — 그 클릭으로 공격이 나가지 않게).
        /// </summary>
        public void IgnoreAttackUntilRelease() => waitAirRelease = true;

        public void SetAttackSlots(HitboxSlot light, HitboxSlot heavy)
        {
            if (!slotDefaultsSaved) { defaultLightSlots = lightAttack.Slots; defaultHeavySlots = heavyAttack.Slots; slotDefaultsSaved = true; }
            lightAttack.Slots = light == HitboxSlot.None ? defaultLightSlots : light;
            heavyAttack.Slots = heavy == HitboxSlot.None ? defaultHeavySlots : heavy;
        }

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            characterState = GetComponent<ICharacterState>();
            hitDetector = GetComponent<AttackHitDetector>();
        }

        private void OnDisable()
        {
            if (AttackState != CharacterAttackState.None)
            {
                EndAttack();
            }
        }

        private void FixedUpdate()
        {
            if (hitDetector == null)
            {
                Awake(); // Play 중 재컴파일로 참조가 비었을 때 복구
            }

            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            bool grounded = characterState == null || characterState.IsGrounded;
            float dt = Time.fixedDeltaTime;

            // 공중에서 누른 공격 키를 뗐으면(뗄 때 약공격 또는 강공격 발동 입력이 온다) 다시 입력을 받는다.
            if (waitAirRelease && (input.LightAttack || input.HeavyRelease))
            {
                waitAirRelease = false;
                input.LightAttack = false;
                input.HeavyRelease = false;
            }
            if (reattackTimer > 0f) reattackTimer -= dt;
            if (grounded && !IsFollowUpKick) { runJumpAirborne = false; followUpUsed = false; }
            bool followUp = runJumpFollowUp && !grounded && runJumpAirborne && !followUpUsed && input.AttackPressed && !IsFollowUpKick;

            switch (AttackState)
            {
                case CharacterAttackState.None:
                    // 다른 행동을 막는 스킬(예: 전위 돌진)·기절 중에는 공격을 시작하지 않는다.
                    if (skillState == null) TryGetComponent(out skillState);
                    if (stun == null) TryGetComponent(out stun);
                    if (health == null) TryGetComponent(out health);
                    if (traversal == null) TryGetComponent(out traversal);
                    if ((skillState != null && skillState.LocksActions) || (stun != null && stun.IsStunned) || (health != null && health.IsDead)
                        || (traversal != null && traversal.LocksActions)) // 오르는 중·매달림·구르기·크게 착지하는 중
                    {
                        break;
                    }
                    // 좌우로 그음(맹인): 그은 방향으로 후리는 약공격. 뗄 때 나오는 약공격·강공격 입력은 버린다.
                    if (swipeAttacks && input.AttackSwipe != 0 && grounded)
                    {
                        StartSwipe(input.AttackSwipe);
                        break;
                    }
                    // 달리기 점프 공격을 한 그 점프가 끝나기 전에 한 번 더 누름 → 다시 뛰어오르며 두 번째 날아 차기.
                    if (followUp) { StartFollowUpKick(input); break; }
                    // 공격이 끝난 직후 다시 공격 대기: 이동은 되지만 공격은 안 나간다. 이때 누른 키는 뗄 때까지 무시한다
                    // (대기가 끝난 뒤 떼어 공격이 나가거나 모으기가 시작되지 않게 — 연타하면 헛누름으로 느껴진다).
                    if (reattackTimer > 0f)
                    {
                        if (input.AttackPressed && !(input.LightAttack || input.HeavyRelease)) waitAirRelease = true;
                        break;
                    }
                    // 공중에서는 꾹 눌러 모을 수 없다: 누르는 순간 바로 공중 공격(첫 입력만), 키를 뗄 때까지 다른 입력은 무시한다.
                    if (!grounded && input.AttackPressed)
                    {
                        IsAirAttack = true;
                        waitAirRelease = !(input.LightAttack || input.HeavyRelease); // 아주 짧게 눌러 같은 틱에 뗐으면 기다리지 않는다
                        StartAttack(CharacterAttackState.Light);
                    }
                    else if (waitAirRelease)
                    {
                        // 공중에서 누른 키를 아직 떼지 않았다(뗄 때 나오는 약공격·강공격 입력도 버린다).
                    }
                    else if (input.LightAttack)
                    {
                        IsAirAttack = !grounded;
                        StartAttack(CharacterAttackState.Light);
                    }
                    else if (input.HeavyCharging && TryGetComponent(out IAttackHoldOverride hold) && hold.OverridesHeavyHold)
                    {
                        // 꾹 누르기는 다른 기능이 쓴다(예: 붉은나비 나비 놓기). 강공격을 모으지 않는다.
                    }
                    else if (input.HeavyCharging && !grounded)
                    {
                        // 땅에서 누르기 시작해 공중에서 0.3초를 넘긴 경우: 모으지 않는다(뗄 때까지 무시).
                        waitAirRelease = true;
                    }
                    else if (input.HeavyCharging)
                    {
                        IsAirAttack = !grounded;
                        AttackState = CharacterAttackState.HeavyCharge;
                        ChargeTime = 0f;
                        ChargeStarted?.Invoke();
                    }
                    break;

                case CharacterAttackState.HeavyCharge:
                    ChargeTime += dt;
                    if (swipeAttacks && input.AttackSwipe != 0 && grounded) { ChargeTime = 0f; StartSwipe(input.AttackSwipe); break; } // 모으다가 그음 → 후리기
                    if (input.HeavyRelease || !input.HeavyCharging)
                    {
                        ChargeTime = 0f;
                        StartAttack(CharacterAttackState.Heavy);
                    }
                    break;

                case CharacterAttackState.Light:
                case CharacterAttackState.Heavy:
                    // 달리기 점프 공격 중 한 번 더 누름 → 끊고 다시 뛰어오르며 두 번째 날아 차기.
                    if (followUp && IsRunJumpAttack) { StartFollowUpKick(input); break; }
                    // 연속 동작(맹인): 동작이 끝나기 전에 들어온 다음 휘두르기를 받아 두고(최대 maxComboFollowUps번), 후딜레이가 시작되면 끊고 바로 이어 나간다.
                    if (swipeAttacks && input.AttackSwipe != 0 && comboFollowUps < maxComboFollowUps) bufferedSwipe = input.AttackSwipe;
                    AdvancePhase(dt);
                    if (bufferedSwipe != 0 && AttackState != CharacterAttackState.None && AttackPhase == AttackPhase.Recovery && grounded)
                    {
                        int next = bufferedSwipe;
                        comboFollowUps++;
                        chaining = true; EndAttack(); chaining = false;
                        StartSwipe(next);
                    }
                    break;
            }
        }

        // 공중 전용 구간(발 판정)은 앞으로 달리다 뛴 공격(플라잉킥 등)에만 쓴다. 그 밖의 공중 공격은 땅 공격과 같은 구간·손 판정.
        private AttackSpec CurrentSpec => IsFollowUpKick ? followUpKick : AttackState == CharacterAttackState.Heavy
            ? (IsRunJumpAttack ? airHeavyAttack : heavyAttack)
            : (IsRunJumpAttack ? airLightAttack : lightAttack);

        /// <summary>앞으로 달리는 중인지(걷기~달리기 속도 사이 runJumpThreshold 이상, 앞쪽이 옆보다 큼).</summary>
        private bool IsRunningForward()
        {
            if (characterState == null) return false;
            Vector2 v = characterState.LocalPlanarVelocity;
            float walk = characterState.WalkSpeed, run = Mathf.Max(characterState.RunSpeed, walk + 0.01f);
            return v.y > Mathf.Abs(v.x) && v.y >= Mathf.Lerp(walk, run, runJumpThreshold);
        }

        private void StartSwipe(int dir)
        {
            IsAirAttack = false;
            Swipe = dir < 0 ? -1 : 1;
            waitAirRelease = true; // 누르고 있는 키를 뗄 때까지 새 입력을 받지 않는다
            StartAttack(CharacterAttackState.Light);
        }

        /// <summary>두 번째 날아 차기: 지금 공격(첫 플라잉킥)을 끊고 followUpJumpHeight만큼 다시 뛰어오르며 찬다.</summary>
        private void StartFollowUpKick(in CharacterInputFrame input)
        {
            if (hitDetector != null && hitDetector.IsAttacking) hitDetector.EndAttack();
            followUpUsed = true;
            followUpLanded = -1f;
            waitAirRelease = !(input.LightAttack || input.HeavyRelease); // 누른 키를 뗄 때 나오는 약공격 입력은 버린다(착지 뒤 땅 공격이 나가지 않게)
            if (TryGetComponent(out CharacterMotor motor)) motor.Launch(FollowUpLaunchSpeed(motor));
            IsAirAttack = true;
            IsRunJumpAttack = true;
            IsFollowUpKick = true;
            Swipe = 0;
            AttackState = CharacterAttackState.Light;
            AttackPhase = AttackPhase.Windup;
            phaseTime = 0f;
            PhaseProgress = 0f;
            AttackStarted?.Invoke(AttackState);
            AdvancePhase(0f);
        }

        /// <summary>
        /// 두 번째 날아 차기의 위아래 속도. 앞쪽(followUpSeekRange, ±followUpSeekAngle)에 상대 머리가 있으면 판정 한가운데 시각에
        /// 차는 발(몸 위 followUpFootRise)이 그 머리 높이에 오도록 정한다(머리를 노림 — 높이 떠 있으면 머리 쪽으로 내리꽂듯).
        /// 없으면 지금 높이에서 followUpJumpHeight만큼 더 뛰어오른다.
        /// </summary>
        private float FollowUpLaunchSpeed(CharacterMotor motor)
        {
            float g = motor.Gravity;
            float hop = Mathf.Sqrt(2f * followUpJumpHeight * -g);
            if (!TryFindHeadAhead(out float headY)) return hop;
            float tm = followUpKick.Windup + followUpKick.Active * 0.5f;
            if (tm <= 0.01f) return hop;
            float targetRootY = headY - followUpFootRise;
            float v = (targetRootY - transform.position.y - 0.5f * g * tm * tm) / tm;
            return Mathf.Clamp(v, -followUpMaxDiveSpeed, hop);
        }

        /// <summary>앞쪽에서 가장 가까운 맞힐 수 있는 상대의 머리 판정 높이(월드 y).</summary>
        private bool TryFindHeadAhead(out float headY)
        {
            headY = 0f;
            var self = GetComponent<CharacterIdentity>();
            Vector3 pos = transform.position, fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float best = float.MaxValue;
            foreach (var col in Physics.OverlapSphere(pos, followUpSeekRange + 1f, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide))
            {
                if (!col.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled || hurt.Part != BodyPart.Head) continue;
                var owner = hurt.Owner;
                if (owner == null || owner == self || (self != null && !self.CanHit(owner))) continue;
                Vector3 c = col.bounds.center, flat = Vector3.ProjectOnPlane(c - pos, Vector3.up);
                float d = flat.magnitude;
                if (d > followUpSeekRange || d < 0.2f || Vector3.Angle(fwd, flat) > followUpSeekAngle) continue;
                if (d < best) { best = d; headY = c.y; }
            }
            return best < float.MaxValue;
        }

        /// <summary>
        /// 두 번째 날아 차기의 마무리: 공중에서는 내려오는 자세(진행도 0~0.6)로 착지를 기다리고,
        /// 착지하면 followUpLandRecovery 동안 제자리 후딜레이(0.6~1) 뒤 끝난다.
        /// </summary>
        private void FollowUpRecovery(float dt)
        {
            bool grounded = characterState == null || characterState.IsGrounded;
            if (followUpLanded < 0f && !grounded)
            {
                PhaseProgress = 0.6f * Mathf.Clamp01(phaseTime / Mathf.Max(followUpKick.Recovery, 0.01f));
                return;
            }
            followUpLanded = followUpLanded < 0f ? 0f : followUpLanded + dt;
            PhaseProgress = 0.6f + 0.4f * (followUpLandRecovery > 0f ? Mathf.Clamp01(followUpLanded / followUpLandRecovery) : 1f);
            if (followUpLanded >= followUpLandRecovery) EndAttack();
        }

        private void StartAttack(CharacterAttackState state)
        {
            IsRunJumpAttack = IsAirAttack && IsRunningForward();
            if (IsRunJumpAttack) runJumpAirborne = true;
            AttackState = state;
            AttackPhase = AttackPhase.Windup;
            phaseTime = 0f;
            PhaseProgress = 0f;
            AttackStarted?.Invoke(state);
            AdvancePhase(0f); // 길이 0인 구간은 바로 넘긴다
        }

        private void AdvancePhase(float dt)
        {
            phaseTime += dt;
            AttackSpec spec = CurrentSpec;

            while (true)
            {
                if (IsFollowUpKick && AttackPhase == AttackPhase.Recovery) { FollowUpRecovery(dt); return; }
                float length = PhaseLength(spec, AttackPhase);
                if (phaseTime < length)
                {
                    PhaseProgress = length > 0f ? phaseTime / length : 1f;
                    return;
                }

                phaseTime -= length;
                switch (AttackPhase)
                {
                    case AttackPhase.Windup:
                        AttackPhase = AttackPhase.Active;
                        hitDetector.BeginAttack(spec.Slots);
                        break;
                    case AttackPhase.Active:
                        AttackPhase = AttackPhase.Recovery;
                        hitDetector.EndAttack();
                        break;
                    default:
                        EndAttack();
                        return;
                }
            }
        }

        private static float PhaseLength(AttackSpec spec, AttackPhase phase)
        {
            return phase switch
            {
                AttackPhase.Windup => spec.Windup,
                AttackPhase.Active => spec.Active,
                AttackPhase.Recovery => spec.Recovery,
                _ => 0f,
            };
        }

        private void EndAttack()
        {
            if (hitDetector != null && hitDetector.IsAttacking)
            {
                hitDetector.EndAttack();
            }
            // 다시 공격 대기. 연속 동작(맹인 후리기 잇기)은 바로 이어 가므로 걸지 않는다.
            if (!chaining && AttackState != CharacterAttackState.None)
                reattackTimer = AttackState == CharacterAttackState.Heavy ? heavyReattackDelay : lightReattackDelay;
            AttackState = CharacterAttackState.None;
            AttackPhase = AttackPhase.None;
            PhaseProgress = 0f;
            ChargeTime = 0f;
            phaseTime = 0f;
            IsAirAttack = false;
            IsRunJumpAttack = false;
            IsFollowUpKick = false;
            followUpLanded = -1f;
            Swipe = 0;
            bufferedSwipe = 0;
            if (!chaining) comboFollowUps = 0;
            AttackEnded?.Invoke();
        }
    }
}

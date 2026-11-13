using Project1.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project1.Systems
{
    /// <summary>
    /// 로컬 키보드·마우스(게임패드)를 읽어 CharacterInputFrame을 만든다.
    /// - Move / Sprint / Jump / Look / Attack: 프로젝트 전역 입력 액션(InputSystem_Actions)의 Player 맵.
    /// - 마우스 시점은 우클릭을 누른 동안만 돌고, 그동안 커서를 잠근다. 게임패드 오른쪽 스틱은 항상 돈다.
    /// - 공격(좌클릭): 기준 시간 미만으로 눌렀다 떼면 약공격, 이상 누르면 강공격 모으기 → 떼면 강공격.
    /// - 고유 스킬: E(게임패드 Y). 캐릭터마다 다른 스킬은 CharacterSkill이 정한다.
    /// 나중에 네트워크/AI 입력은 ICharacterInputSource를 구현한 다른 스크립트로 바꾸면 된다.
    /// </summary>
    public class LocalPlayerInput : MonoBehaviour, ICharacterInputSource
    {
        [Header("시점")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.25f; // 픽셀당 도
        [SerializeField, Min(0f)] private float gamepadLookSpeed = 120f;  // 초당 도
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 60f;
        [SerializeField] private bool invertY;

        [Header("공격")]
        [Tooltip("공격 키를 이 시간(초) 이상 누르면 강공격, 미만이면 약공격.")]
        [SerializeField, Min(0f)] private float heavyAttackHoldTime = 0.3f;

        private InputAction moveAction;
        private InputAction runAction;
        private InputAction jumpAction;
        private InputAction lookAction;
        private InputAction attackAction;
        private InputAction lookHoldAction; // 우클릭. 전역 에셋에 없어서 코드에서 만든다.
        private InputAction skillAction;    // 고유 스킬(E / 게임패드 Y). 전역 에셋의 Interact(E)와 따로 코드에서 만든다.

        private float lookYaw;
        private float lookPitch;
        private bool cursorLocked;

        // 일회성 입력: 다음 틱이 가져갈 때까지 쌓아 둔다.
        private bool jumpQueued;
        private bool lightAttackQueued;
        private bool heavyReleaseQueued;
        private bool skillQueued;
        private bool attackPressedQueued;

        // 공격 키 누름 상태.
        private bool attackHeld;
        private float attackPressTime;

        // 마우스 커서가 가리키는 곳(매 프레임 갱신).
        [Header("커서 겨누기")]
        [Tooltip("커서 아래를 찾는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float aimMaxDistance = 200f;
        private readonly RaycastHit[] aimHits = new RaycastHit[16];
        [Tooltip("시점을 돌린 뒤 커서가 이만큼(픽셀) 움직여야 다시 커서로 겨눈다.")]
        [SerializeField, Min(0f)] private float unfreezePixels = 4f;
        private bool hasAim;
        private Vector3 aimPoint, aimNormal;
        private bool aimFrozen;          // 시점을 돌리는 중이거나, 돌린 뒤 커서가 아직 안 움직임
        private Vector2 frozenCursor;    // 잠금이 풀린 뒤 처음 커서 자리(-1 = 아직 모름)

        // 같은 틱에 여러 번 불려도 같은 값을 돌려주기 위한 캐시.
        private int cachedTick = int.MinValue;
        private CharacterInputFrame cachedFrame;

        public CharacterInputFrame ReadInput()
        {
            int tick = SimulationTick.Current;
            if (tick == cachedTick)
            {
                return cachedFrame;
            }

            cachedTick = tick;
            cachedFrame = new CharacterInputFrame
            {
                Move = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero,
                Run = runAction != null && runAction.IsPressed(),
                Jump = jumpQueued,
                LookYaw = lookYaw,
                LookPitch = lookPitch,
                LightAttack = lightAttackQueued,
                HeavyCharging = attackHeld && Time.time - attackPressTime >= heavyAttackHoldTime,
                HeavyRelease = heavyReleaseQueued,
                Skill = skillQueued,
                AttackPressed = attackPressedQueued,
                HasAimPoint = hasAim,
                AimPoint = aimPoint,
                AimNormal = aimNormal,
            };
            jumpQueued = false;
            lightAttackQueued = false;
            heavyReleaseQueued = false;
            attackPressedQueued = false;
            skillQueued = false;
            return cachedFrame;
        }

        private void Awake()
        {
            lookYaw = transform.eulerAngles.y;

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("프로젝트 전역 입력 액션이 설정되지 않았습니다. (Project Settings > Input System Package)", this);
                enabled = false;
                return;
            }

            moveAction = actions.FindAction("Player/Move", throwIfNotFound: true);
            runAction = actions.FindAction("Player/Sprint", throwIfNotFound: true);
            jumpAction = actions.FindAction("Player/Jump", throwIfNotFound: true);
            lookAction = actions.FindAction("Player/Look", throwIfNotFound: true);
            attackAction = actions.FindAction("Player/Attack", throwIfNotFound: true);
            lookHoldAction = new InputAction("LookHold", InputActionType.Button, "<Mouse>/rightButton");
            skillAction = new InputAction("Skill", InputActionType.Button, "<Keyboard>/e");
            skillAction.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            runAction?.Enable();
            jumpAction?.Enable();
            lookAction?.Enable();
            attackAction?.Enable();
            lookHoldAction?.Enable();
            skillAction?.Enable();
        }

        private void OnDisable()
        {
            // 전역 액션은 다른 곳에서도 쓸 수 있으므로 끄지 않는다.
            lookHoldAction?.Disable();
            skillAction?.Disable();
            skillQueued = false;
            SetCursorLocked(false);
            jumpQueued = false;
            lightAttackQueued = false;
            heavyReleaseQueued = false;
            attackHeld = false;
        }

        private void OnDestroy()
        {
            lookHoldAction?.Dispose();
            skillAction?.Dispose();
        }

        private void Update()
        {
            if (jumpAction.WasPressedThisFrame())
            {
                jumpQueued = true; // FixedUpdate에서 꺼내 갈 때까지 유지
            }

            if (skillAction.WasPressedThisFrame())
            {
                skillQueued = true;
            }

            UpdateAttack();
            UpdateLook();
            UpdateAim();
        }

        /// <summary>
        /// 마우스 커서 아래에서 처음 닿는 것(자기 몸·트리거 제외)을 찾는다(게임패드만 있으면 화면 가운데).
        /// 우클릭으로 시점을 돌리는 동안(커서 잠김)과, 놓은 뒤 커서가 화면 가운데로 돌아와 아직 움직이지 않은 동안은
        /// 커서가 가리키는 곳이 아니므로 겨누는 곳이 없다고 알린다(HasAimPoint = false) — 나비 미리보기 등이 마지막 자리를 그대로 지킨다.
        /// </summary>
        private void UpdateAim()
        {
            hasAim = false;
            var cam = Camera.main;
            if (cam == null) return;
            var mouse = Mouse.current;
            if (cursorLocked) { aimFrozen = true; frozenCursor = new Vector2(-1f, -1f); return; }
            if (aimFrozen && mouse != null)
            {
                Vector2 now = mouse.position.ReadValue();
                if (frozenCursor.x < 0f) frozenCursor = now;                       // 잠금이 풀린 첫 프레임의 커서 자리
                if ((now - frozenCursor).sqrMagnitude < unfreezePixels * unfreezePixels) return; // 아직 안 움직임
                aimFrozen = false;
            }
            Vector2 screen = mouse == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : mouse.position.ReadValue();
            if (screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return;
            Ray ray = cam.ScreenPointToRay(screen);
            int n = Physics.RaycastNonAlloc(ray, aimHits, aimMaxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = aimHits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                best = h.distance;
                aimPoint = h.point;
                aimNormal = h.normal;
                hasAim = true;
            }
        }

        private void UpdateAttack()
        {
            if (attackAction.WasPressedThisFrame())
            {
                attackHeld = true;
                attackPressedQueued = true;
                attackPressTime = Time.time;
            }

            if (attackHeld && attackAction.WasReleasedThisFrame())
            {
                attackHeld = false;
                if (Time.time - attackPressTime >= heavyAttackHoldTime)
                {
                    heavyReleaseQueued = true;
                }
                else
                {
                    lightAttackQueued = true;
                }
            }
        }

        private void UpdateLook()
        {
            bool holding = lookHoldAction.IsPressed();
            SetCursorLocked(holding);

            Vector2 look = lookAction.ReadValue<Vector2>();
            InputDevice device = lookAction.activeControl?.device;
            Vector2 deltaDegrees;
            if (device is Pointer)
            {
                deltaDegrees = holding ? look * mouseSensitivity : Vector2.zero;
            }
            else
            {
                deltaDegrees = look * (gamepadLookSpeed * Time.deltaTime);
            }

            lookYaw = Mathf.Repeat(lookYaw + deltaDegrees.x, 360f);
            float pitchDelta = invertY ? deltaDegrees.y : -deltaDegrees.y; // 마우스를 올리면 위를 본다(pitch 감소)
            lookPitch = Mathf.Clamp(lookPitch + pitchDelta, minPitch, maxPitch);
        }

        private void SetCursorLocked(bool locked)
        {
            if (cursorLocked == locked)
            {
                return;
            }
            cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

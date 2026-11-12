using Project1.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project1.Systems
{
    /// <summary>
    /// 로컬 키보드·마우스(게임패드)를 읽어 CharacterInputFrame을 만든다.
    /// - Move / Sprint / Jump / Look: 프로젝트 전역 입력 액션(InputSystem_Actions)의 Player 맵.
    /// - 마우스 시점은 우클릭을 누른 동안만 돌고, 그동안 커서를 잠근다. 게임패드 오른쪽 스틱은 항상 돈다.
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

        private InputAction moveAction;
        private InputAction runAction;
        private InputAction jumpAction;
        private InputAction lookAction;
        private InputAction lookHoldAction; // 우클릭. 전역 에셋에 없어서 코드에서 만든다.

        private float lookYaw;
        private float lookPitch;
        private bool jumpQueued;
        private bool cursorLocked;

        public CharacterInputFrame ReadInput()
        {
            var frame = new CharacterInputFrame
            {
                Move = moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero,
                Run = runAction != null && runAction.IsPressed(),
                Jump = jumpQueued,
                LookYaw = lookYaw,
                LookPitch = lookPitch,
            };
            jumpQueued = false;
            return frame;
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
            lookHoldAction = new InputAction("LookHold", InputActionType.Button, "<Mouse>/rightButton");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            runAction?.Enable();
            jumpAction?.Enable();
            lookAction?.Enable();
            lookHoldAction?.Enable();
        }

        private void OnDisable()
        {
            // 전역 액션은 다른 곳에서도 쓸 수 있으므로 끄지 않는다.
            lookHoldAction?.Disable();
            SetCursorLocked(false);
            jumpQueued = false;
        }

        private void OnDestroy()
        {
            lookHoldAction?.Dispose();
        }

        private void Update()
        {
            if (jumpAction.WasPressedThisFrame())
            {
                jumpQueued = true; // FixedUpdate에서 꺼내 갈 때까지 유지
            }

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

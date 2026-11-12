using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 같은 오브젝트의 ICharacterInputSource에서 매 틱 입력을 받아 캐릭터를 움직인다.
    /// - 이동 방향은 시야 Yaw 기준(W=앞, S=뒤, A/D=옆), 몸은 항상 시야 Yaw를 본다.
    /// - 걷기/달리기, 땅에 있을 때만 점프, 중력.
    /// - 결과 상태는 ICharacterState로 읽기 전용 공개한다.
    /// 키보드나 카메라를 직접 읽지 않는다. 캐릭터 루트 오브젝트에 붙이고, 모델은 자식으로 둔다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMotor : MonoBehaviour, ICharacterState
    {
        [SerializeField, Min(0f)] private float walkSpeed = 3.6f;
        [SerializeField, Min(0f)] private float runSpeed = 6.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;

        private const float GroundStickSpeed = -2f; // 땅에 붙어 있게 살짝 누르는 속도

        private CharacterController controller;
        private ICharacterInputSource inputSource;
        private IMoveSpeedLimit[] speedLimits;

        public CharacterMoveState MoveState { get; private set; }
        public bool IsGrounded { get; private set; }
        public float VerticalSpeed { get; private set; }
        public Vector2 LocalPlanarVelocity { get; private set; }
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;

        public event Action Jumped;
        public event Action Landed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputSource = GetComponent<ICharacterInputSource>();
            if (inputSource == null)
            {
                Debug.LogWarning($"{name}: ICharacterInputSource(LocalPlayerInput 등)가 없어 움직이지 않습니다.", this);
            }
            speedLimits = GetComponents<IMoveSpeedLimit>();
            LookYaw = transform.eulerAngles.y;
            IsGrounded = controller.isGrounded;
        }

        private bool TryGetSpeedLimit(out float maxSpeed)
        {
            maxSpeed = float.MaxValue;
            bool limited = false;
            foreach (IMoveSpeedLimit limit in speedLimits)
            {
                if (limit is Behaviour { isActiveAndEnabled: false })
                {
                    continue;
                }
                if (limit.TryGetSpeedLimit(out float value))
                {
                    maxSpeed = Mathf.Min(maxSpeed, value);
                    limited = true;
                }
            }
            return limited;
        }

        private void FixedUpdate()
        {
            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            if (inputSource != null)
            {
                LookYaw = input.LookYaw;
                LookPitch = input.LookPitch;
            }

            float dt = Time.fixedDeltaTime;

            // 몸을 시야 좌우 방향으로 돌린다.
            transform.rotation = Quaternion.Euler(0f, LookYaw, 0f);

            // 시야 기준 수평 이동 (대각선 정규화는 입력 소스에서 Clamp).
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            bool moving = move.sqrMagnitude > 0.0001f;
            bool running = moving && input.Run;
            float speed = running ? runSpeed : walkSpeed;
            if (TryGetSpeedLimit(out float limit) && speed > limit)
            {
                speed = limit;
                running = speed > walkSpeed;
            }
            Vector2 localPlanar = move * speed;
            Vector3 worldPlanar = transform.rotation * new Vector3(localPlanar.x, 0f, localPlanar.y);

            // 점프와 중력.
            float vertical = VerticalSpeed;
            if (IsGrounded && vertical < 0f)
            {
                vertical = GroundStickSpeed;
            }

            bool jumpedThisTick = false;
            if (input.Jump && IsGrounded)
            {
                vertical = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumpedThisTick = true;
            }
            vertical += gravity * dt;

            controller.Move((worldPlanar + Vector3.up * vertical) * dt);

            // 결과 상태 갱신.
            bool wasGrounded = IsGrounded;
            IsGrounded = controller.isGrounded;
            if (IsGrounded && vertical < 0f)
            {
                vertical = GroundStickSpeed;
            }
            VerticalSpeed = vertical;

            Vector3 actualLocal = transform.InverseTransformDirection(controller.velocity);
            LocalPlanarVelocity = new Vector2(actualLocal.x, actualLocal.z);
            MoveState = !moving ? CharacterMoveState.Idle
                      : running ? CharacterMoveState.Run
                      : CharacterMoveState.Walk;

            if (jumpedThisTick)
            {
                Jumped?.Invoke();
            }
            if (!wasGrounded && IsGrounded)
            {
                Landed?.Invoke();
            }
        }

        // 컴포넌트를 처음 붙일 때 CharacterController 크기를 자식 모델에 맞춘다.
        private void Reset()
        {
            var cc = GetComponent<CharacterController>();
            var renderers = GetComponentsInChildren<Renderer>();
            if (cc == null || renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 scale = transform.lossyScale;
            float height = bounds.size.y / Mathf.Max(scale.y, 0.0001f);
            float radius = Mathf.Min(bounds.extents.x / Mathf.Max(scale.x, 0.0001f),
                                     bounds.extents.z / Mathf.Max(scale.z, 0.0001f));

            cc.height = height;
            cc.radius = Mathf.Clamp(radius, 0.1f, height * 0.5f);
            cc.center = transform.InverseTransformPoint(bounds.center);
        }
    }
}

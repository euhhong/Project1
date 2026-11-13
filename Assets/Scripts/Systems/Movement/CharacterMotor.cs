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
        private IMoveOverride[] moveOverrides;
        private IFacingOverride[] facingOverrides;
        private ICharacterHealth health; // 체력이 0이면(쓰러짐) 움직이지 않는다

        public CharacterMoveState MoveState { get; private set; }
        public bool IsGrounded { get; private set; }
        public float VerticalSpeed { get; private set; }
        public Vector2 LocalPlanarVelocity { get; private set; }
        public float LookYaw { get; private set; }
        public float LookPitch { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;

        /// <summary>지난 틱 이동에서 옆으로 무언가(벽·장애물·다른 캐릭터)에 부딪혔다.</summary>
        public bool HitObstacle { get; private set; }

        /// <summary>HitObstacle일 때 옆으로 부딪힌 Collider(다른 캐릭터인지 구분할 때 쓴다). 없으면 null.</summary>
        public Collider LastObstacle { get; private set; }

        /// <summary>HitObstacle일 때 옆으로 부딪힌 면의 바깥쪽 방향(월드, 수평). 벽을 따라 미끄러질 때 쓴다(예: 용병 도약).</summary>
        public Vector3 LastObstacleNormal { get; private set; }

        /// <summary>중력 가속도(m/s², 음수).</summary>
        public float Gravity => gravity;

        /// <summary>
        /// 수직 속도를 바로 바꾼다(스킬용). + = 위로 띄움(예: 루키노 도약), - = 아래로 내리꽂음(내려찍기).
        /// 다음 틱부터 중력이 다시 붙는다. Jumped 이벤트는 내지 않는다.
        /// </summary>
        public void Launch(float verticalSpeed)
        {
            VerticalSpeed = verticalSpeed;
            if (verticalSpeed > 0f) IsGrounded = false;
        }

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
            moveOverrides = GetComponents<IMoveOverride>();
            facingOverrides = GetComponents<IFacingOverride>();
            LookYaw = transform.eulerAngles.y;
            IsGrounded = controller.isGrounded;
        }

        /// <summary>속도 제한·이동 대체 컴포넌트를 다시 모은다(실행 중에 스킬 등을 붙인 뒤 호출).</summary>
        public void RefreshSpeedLimits()
        {
            speedLimits = GetComponents<IMoveSpeedLimit>();
            moveOverrides = GetComponents<IMoveOverride>();
            facingOverrides = GetComponents<IFacingOverride>();
        }

        private bool TryGetMoveOverride(out Vector2 move, out float speed, out bool allowJump)
        {
            moveOverrides ??= GetComponents<IMoveOverride>();
            foreach (IMoveOverride o in moveOverrides)
            {
                if (o is Behaviour { isActiveAndEnabled: false }) continue;
                if (o.TryGetMoveOverride(out move, out speed, out allowJump)) return true;
            }
            move = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        private bool TryGetFacingOverride(out float yaw)
        {
            facingOverrides ??= GetComponents<IFacingOverride>();
            foreach (IFacingOverride o in facingOverrides)
            {
                if (o is Behaviour { isActiveAndEnabled: false }) continue;
                if (o.TryGetFacingYaw(out yaw)) return true;
            }
            yaw = 0f;
            return false;
        }

        private bool TryGetSpeedLimit(out float maxSpeed)
        {
            maxSpeed = float.MaxValue;
            bool limited = false;
            // Play 중 스크립트가 다시 컴파일되면 Awake 없이 필드가 비므로 다시 모은다.
            speedLimits ??= GetComponents<IMoveSpeedLimit>();
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
            if (controller == null)
            {
                Awake(); // Play 중 재컴파일로 참조가 비었을 때 복구
            }

            CharacterInputFrame input = inputSource != null ? inputSource.ReadInput() : default;
            if (inputSource != null)
            {
                LookYaw = input.LookYaw;
                LookPitch = input.LookPitch;
            }

            float dt = Time.fixedDeltaTime;
            if (health == null) TryGetComponent(out health);
            bool dead = health != null && health.IsDead;

            // 몸을 시야 좌우 방향으로 돌린다(쓰러져 있으면 돌지 않는다).
            if (!dead) transform.rotation = Quaternion.Euler(0f, TryGetFacingOverride(out float facingYaw) ? facingYaw : LookYaw, 0f); // 스킬이 정하면 그 각도(예: 붉은나비 나비에게 — 시작 각도 유지)

            // 시야 기준 수평 이동 (대각선 정규화는 입력 소스에서 Clamp).
            Vector2 move = Vector2.ClampMagnitude(input.Move, 1f);
            bool moving = move.sqrMagnitude > 0.0001f;
            bool running = moving && input.Run;
            float speed = running ? runSpeed : walkSpeed;
            bool allowJump = true;
            if (dead) { move = Vector2.zero; moving = false; running = false; allowJump = false; } // 쓰러짐: 제자리
            else if (TryGetMoveOverride(out Vector2 overrideMove, out float overrideSpeed, out allowJump))
            {
                // 스킬 등이 이동을 정한다(예: 돌진). 입력 방향·달리기 키·속도 제한은 무시한다.
                move = Vector2.ClampMagnitude(overrideMove, 1f);
                moving = move.sqrMagnitude > 0.0001f;
                speed = overrideSpeed;
                running = moving && speed > walkSpeed;
            }
            else if (TryGetSpeedLimit(out float limit) && speed > limit)
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
            if (input.Jump && IsGrounded && allowJump)
            {
                vertical = Mathf.Sqrt(2f * jumpHeight * -gravity);
                jumpedThisTick = true;
            }
            vertical += gravity * dt;

            LastObstacle = null;
            CollisionFlags flags = controller.Move((worldPlanar + Vector3.up * vertical) * dt); // 부딪히면 OnControllerColliderHit이 LastObstacle을 채운다
            HitObstacle = (flags & CollisionFlags.Sides) != 0;
            if (!HitObstacle) LastObstacle = null;

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

        // controller.Move 도중에 불린다. 옆으로 부딪힌 것(바닥·천장 제외)을 기억한다.
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.5f)
            {
                LastObstacle = hit.collider;
                LastObstacleNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
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

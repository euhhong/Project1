using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 상태(ICharacterState, BE-5)를 Animator 파라미터로 넘긴다. 게임 로직은 건드리지 않는다.
    /// 캐릭터 루트에 붙인다. Animator는 자식(교체 가능한 모델)에서 자동으로 찾는다.
    /// 파라미터: MoveX, MoveZ, Speed (float) / IsGrounded (bool) / Jump (trigger) — Docs/Animations.md 5장.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        [Tooltip("MoveX/MoveZ 값 변화의 감쇠 시간(초).")]
        [SerializeField] private float moveDampTime = 0.1f;

        private static readonly int MoveXId = Animator.StringToHash("MoveX");
        private static readonly int MoveZId = Animator.StringToHash("MoveZ");
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        private ICharacterState state;
        private Animator animator;
        private CharacterBodyTurn bodyTurn;

        private void Awake()
        {
            state = GetComponent<ICharacterState>();
            animator = GetComponentInChildren<Animator>();
            bodyTurn = GetComponent<CharacterBodyTurn>();
            if (state == null) Debug.LogWarning($"{name}: ICharacterState가 없어 모션을 연결하지 않습니다.", this);
            if (animator == null) Debug.LogWarning($"{name}: 자식에서 Animator를 찾지 못했습니다.", this);
            // 이동은 BE의 CharacterMotor가 맡는다. 모션의 루트 이동이 겹치지 않게 끈다.
            else animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            if (state != null) state.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            if (state != null) state.Jumped -= OnJumped;
        }

        private void Update()
        {
            if (!IsReady()) return;

            float walk = Mathf.Max(state.WalkSpeed, 0.01f);
            Vector2 v = state.LocalPlanarVelocity;
            // 몸(모델)을 튼 만큼 속도를 모델 기준으로 바꿔, 튼 방향으로 걷는 모션이 나오게 한다.
            if (bodyTurn != null && Mathf.Abs(bodyTurn.YawOffset) > 0.01f)
            {
                float r = -bodyTurn.YawOffset * Mathf.Deg2Rad;
                v = new Vector2(v.x * Mathf.Cos(r) + v.y * Mathf.Sin(r), -v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r));
            }
            animator.SetFloat(MoveXId, v.x / walk, moveDampTime, Time.deltaTime);
            animator.SetFloat(MoveZId, v.y / walk, moveDampTime, Time.deltaTime);
            animator.SetFloat(SpeedId, v.magnitude);
            animator.SetBool(IsGroundedId, state.IsGrounded);
        }

        private void OnJumped()
        {
            if (IsReady()) animator.SetTrigger(JumpId);
        }

        private bool IsReady() => state != null && animator != null && animator.runtimeAnimatorController != null;
    }
}

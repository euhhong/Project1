using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 물건 밀기(BE). 땅에서 밀 수 있는 물건(PushableObject)을 마주 보고 앞(W)으로 걸어 들어가면 민다.
    /// - 처음 pushLeanTime 동안 몸을 기울여 붙고(Push Start 앞부분), 그 뒤 앞으로 천천히(pushSpeed × 물건 배율) 민다. 미는 방향은 닿은 면에 수직으로 고정.
    /// - 물건이 막히면(벽·다른 물건·턱 끝) 제자리에서 버틴다(PushBlocked).
    /// - 앞을 놓거나 점프·공격하거나, 기절·쓰러짐·오르기를 하면 놓는다.
    /// 이동(CharacterMotor)보다 먼저 물건을 옮기고, 캐릭터는 그만큼 따라 걷는다(IMoveOverride). 모션은 FE(CharacterTraversalAnimator)가 보여 준다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DefaultExecutionOrder(-40)] // 오르기(CharacterTraversal, -50) 다음, 이동(CharacterMotor)보다 먼저
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class CharacterPush : MonoBehaviour, ICharacterPushState, IMoveOverride, IFacingOverride
    {
        [Tooltip("미는 빠르기(m/s). 물건의 SpeedScale을 곱한다.")]
        [SerializeField, Min(0.1f)] private float pushSpeed = 1f;
        [Tooltip("밀기 시작할 때 몸을 기울여 붙는 시간(초) — 이동 없음.")]
        [SerializeField, Min(0f)] private float pushLeanTime = 0.4f;
        [Tooltip("물건이 이 거리(m, 몸 둘레에서) 안에 있어야 민다.")]
        [SerializeField, Min(0f)] private float reach = 0.25f;
        [Tooltip("물건 면을 이 각도(도) 안으로 마주 봐야 민다.")]
        [SerializeField, Range(0f, 90f)] private float maxAngle = 40f;

        private CharacterController cc;
        private ICharacterInputSource input;
        private ICharacterState state;
        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterTraversalState traversal;

        private PushableObject target;
        private Vector3 pushDir;
        private float yaw;
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        public bool IsPushing => target != null;
        public float PushTime { get; private set; }
        public bool PushBlocked { get; private set; }

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            input = GetComponent<ICharacterInputSource>();
            state = GetComponent<ICharacterState>();
            attack = GetComponent<ICharacterAttackState>();
        }

        private void OnDisable() => Stop();

        private void FixedUpdate()
        {
            if (cc == null) Awake(); // Play 중 재컴파일로 참조가 비었을 때
            var frame = input != null ? input.ReadInput() : default;
            float dt = Time.fixedDeltaTime;
            bool grounded = state == null || state.IsGrounded;

            if (target == null)
            {
                if (!grounded || Busy() || frame.Move.y < 0.6f || Mathf.Abs(frame.Move.x) > 0.5f || frame.Jump) return;
                TryStart();
                return;
            }
            if (!grounded || Busy() || frame.Move.y < 0.3f || frame.Jump || !StillTouching()) { Stop(); return; }

            PushTime += dt;
            if (PushTime < pushLeanTime) return;
            float want = pushSpeed * target.SpeedScale * dt;
            float moved = target.Move(pushDir * want, cc);
            PushBlocked = moved < want * 0.5f;
        }

        private bool Busy()
        {
            if (skill == null) TryGetComponent(out skill);
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            if (traversal == null) TryGetComponent(out traversal);
            return (attack != null && attack.AttackState != CharacterAttackState.None)
                   || (skill != null && (skill.LocksActions || skill.State == SkillState.Casting))
                   || (stun != null && stun.IsStunned) || (health != null && health.IsDead)
                   || (traversal != null && traversal.Action != TraversalAction.None);
        }

        private void TryStart()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (!FindPushable(fwd, out var hit, out var pushable)) return;
            Vector3 n = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
            if (n.sqrMagnitude < 0.25f) return;
            n.Normalize();
            if (Vector3.Angle(-n, fwd) > maxAngle) return;
            target = pushable;
            pushDir = -n; // 닿은 면에 수직으로 민다
            yaw = Mathf.Atan2(pushDir.x, pushDir.z) * Mathf.Rad2Deg;
            PushTime = 0f;
            PushBlocked = false;
        }

        /// <summary>미는 물건이 아직 바로 앞에 닿아 있는지.</summary>
        private bool StillTouching() => FindPushable(pushDir, out _, out var p) && p == target;

        /// <summary>dir 쪽 바로 앞(무릎·허리 높이)의 밀 수 있는 물건.</summary>
        private bool FindPushable(Vector3 dir, out RaycastHit best, out PushableObject pushable)
        {
            best = default; pushable = null;
            float dist = cc.radius + reach;
            float bestDistance = float.MaxValue;
            foreach (float h in new[] { cc.height * 0.25f, cc.height * 0.45f })
            {
                int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * h, dir, Hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (Hits[i].collider == cc || Hits[i].distance >= bestDistance) continue;
                    best = Hits[i]; bestDistance = Hits[i].distance;
                }
            }
            if (bestDistance == float.MaxValue) return false;
            pushable = best.collider.GetComponentInParent<PushableObject>();
            return pushable != null;
        }

        private void Stop()
        {
            target = null;
            PushTime = 0f;
            PushBlocked = false;
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = true;
            if (target == null) { localMove = Vector2.zero; speed = 0f; return false; }
            // 몸을 기울이는 동안·막혔을 때는 제자리, 아니면 물건을 따라 앞으로(물건이 먼저 움직였다).
            bool moving = PushTime >= pushLeanTime && !PushBlocked;
            localMove = moving ? Vector2.up : Vector2.zero;
            speed = moving ? pushSpeed * target.SpeedScale : 0f;
            return true;
        }

        public bool TryGetFacingYaw(out float facingYaw)
        {
            facingYaw = yaw;
            return target != null;
        }
    }
}

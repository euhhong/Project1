using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 좌우로 움직일 때 몸(모델)을 이동 방향으로 틀고, 상체·목·머리는 원래 보던 방향(시야)으로 일부 되돌린다.
    /// 좌우 이동이 잠시 없으면 몸과 고개를 천천히 원래 각도로 되돌린다.
    /// 연출 전용이다. 캐릭터 루트의 회전·이동(BE)은 건드리지 않고 자식 모델만 돌린다.
    /// 캐릭터 루트에 붙인다. 모델(Animator)은 자식에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterBodyTurn : MonoBehaviour
    {
        [Header("몸 틀기")]
        [Tooltip("좌우 이동으로 몸을 트는 최대 각도(도).")]
        [SerializeField, Range(0f, 90f)] private float maxBodyTurn = 70f;
        [Tooltip("좌우 이동으로 보는 최소 좌우 속도(m/s).")]
        [SerializeField, Min(0f)] private float lateralThreshold = 0.3f;
        [Tooltip("몸을 트는 속도(감쇠 시간, 초). 작을수록 확 틀어진다.")]
        [SerializeField, Min(0f)] private float turnSmoothTime = 0.1f;

        [Header("되돌리기")]
        [Tooltip("좌우 이동이 멈춘 뒤 원래대로 돌아가기 시작할 때까지 기다리는 시간(초).")]
        [SerializeField, Min(0f)] private float returnDelay = 0.35f;
        [Tooltip("원래 각도로 돌아가는 속도(감쇠 시간, 초). 클수록 천천히 돌아온다.")]
        [SerializeField, Min(0f)] private float returnSmoothTime = 0.45f;

        [Header("고개·상체 보정 (몸을 튼 각도 대비 시야 쪽으로 되돌리는 비율)")]
        [SerializeField, Range(0f, 1f)] private float spineCompensation = 0.15f;
        [SerializeField, Range(0f, 1f)] private float chestCompensation = 0.2f;
        [SerializeField, Range(0f, 1f)] private float neckCompensation = 0.25f;
        [SerializeField, Range(0f, 1f)] private float headCompensation = 0.3f;

        /// <summary>모델이 캐릭터 루트 기준으로 틀어진 각도(도). + = 오른쪽.</summary>
        public float YawOffset => yaw;

        private ICharacterState state;
        private ICharacterSkillState skill;
        private Animator animator;
        private Transform model;
        private Quaternion modelBaseRotation;
        private Transform spine, chest, neck, head;
        private float yaw, yawVelocity, targetYaw, lastLateralTime = -999f;
        [Header("몸이 한 번에 크게 돌 때(예: 붉은나비 나비에게 날아간 뒤)")]
        [Tooltip("한 프레임에 이 각도(도)보다 크게 돌면 모델이 부드럽게 따라 돈다.")]
        [SerializeField, Min(1f)] private float snapThreshold = 25f;
        [Tooltip("따라 도는 속도(감쇠 시간, 초).")]
        [SerializeField, Min(0.01f)] private float catchUpSmoothTime = 0.15f;
        private float lastRootYaw;
        private bool catchingUp;

        private void Awake()
        {
            state = GetComponent<ICharacterState>();
            ResolveModel();
        }

        /// <summary>모델(Animator)과 뼈를 찾는다. 모델이 바뀌면 Update에서 다시 부른다.</summary>
        private void ResolveModel()
        {
            animator = GetComponentInChildren<Animator>();
            model = null; spine = chest = neck = head = null;
            yaw = yawVelocity = targetYaw = 0f;
            lastRootYaw = transform.eulerAngles.y; catchingUp = false;
            if (animator == null) return;
            model = animator.transform;
            modelBaseRotation = model.localRotation;
            if (animator.isHuman)
            {
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled) ResolveModel();
            if (state == null || model == null) return;

            // 행동을 막는 스킬(예: 사냥터지기 그랩, 전위 돌진) 중에는 몸을 바로 시야 방향으로 돌린다(틀어 둔 각도 없앰).
            if (skill == null) TryGetComponent(out skill);
            float rootYaw = transform.eulerAngles.y;
            if (skill != null && skill.LocksActions)
            {
                yaw = targetYaw = yawVelocity = 0f;
                model.localRotation = modelBaseRotation;
                lastRootYaw = rootYaw;
                catchingUp = false;
                return;
            }

            // 몸(루트)이 한 번에 크게 돌면(예: 붉은나비 나비에게 날아간 뒤 시작 각도 → 시야로) 모델은 부드럽게 따라 돈다.
            float jump = Mathf.DeltaAngle(lastRootYaw, rootYaw);
            lastRootYaw = rootYaw;
            if (Mathf.Abs(jump) > snapThreshold) { yaw -= jump; yawVelocity = 0f; targetYaw = 0f; catchingUp = true; }
            if (catchingUp)
            {
                yaw = Mathf.SmoothDampAngle(yaw, 0f, ref yawVelocity, catchUpSmoothTime);
                if (Mathf.Abs(yaw) < 0.5f) { yaw = 0f; catchingUp = false; }
                model.localRotation = Quaternion.Euler(0f, yaw, 0f) * modelBaseRotation;
                return;
            }

            Vector2 v = state.LocalPlanarVelocity; // x = 오른쪽, y = 앞
            if (Mathf.Abs(v.x) > lateralThreshold)
            {
                // 앞으로 가면서 옆이면 진행 방향 쪽으로, 뒤로 가면서 옆이면 뒷걸음 방향이 되도록 반대로 튼다.
                float angle = v.y >= -0.1f
                    ? Mathf.Atan2(v.x, Mathf.Max(v.y, 0f)) * Mathf.Rad2Deg
                    : Mathf.Atan2(-v.x, -v.y) * Mathf.Rad2Deg;
                targetYaw = Mathf.Clamp(angle, -maxBodyTurn, maxBodyTurn);
                lastLateralTime = Time.time;
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSmoothTime);
            }
            else if (Time.time - lastLateralTime > returnDelay)
            {
                targetYaw = 0f;
                yaw = Mathf.SmoothDampAngle(yaw, 0f, ref yawVelocity, returnSmoothTime);
            }
            else
            {
                // 잠깐 멈춘 동안은 틀어진 각도를 유지한다.
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSmoothTime);
            }

            model.localRotation = Quaternion.Euler(0f, yaw, 0f) * modelBaseRotation;
        }

        private void LateUpdate()
        {
            // Animator가 자세를 쓴 뒤에, 몸을 튼 만큼 상체·목·머리를 시야 쪽으로 되돌린다.
            if (model == null || Mathf.Abs(yaw) < 0.01f) return;
            float carry = 0f;
            carry = Twist(spine, spineCompensation + carry);
            carry = Twist(chest, chestCompensation + carry);
            carry = Twist(neck, neckCompensation + carry);
            Twist(head, headCompensation + carry);
        }

        /// <summary>뼈를 월드 위쪽 축으로 -yaw*비율 만큼 돌린다. 뼈가 없으면 비율을 다음 뼈로 넘긴다.</summary>
        private float Twist(Transform bone, float ratio)
        {
            if (bone == null) return ratio;
            bone.rotation = Quaternion.AngleAxis(-yaw * ratio, Vector3.up) * bone.rotation;
            return 0f;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 좌/우가 섞인 이동일 때 몸(모델)을 이동 방향 쪽으로 살짝 틀고,
    /// 목·머리는 반대로 돌려 시선은 시야 방향(캐릭터 루트 앞)을 유지한다. 보이는 것만 바꾸고 이동·판정에는 영향 없음.
    /// 캐릭터 루트(ICharacterState가 있는 오브젝트)에 붙인다. 모델은 자식 Animator에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StrafeBodyTwist : MonoBehaviour
    {
        [Tooltip("몸을 트는 최대 각도(도).")]
        [SerializeField, Range(0f, 60f)] private float maxBodyTwist = 35f;
        [Tooltip("이동 방향 각도 중 몸에 반영하는 비율.")]
        [SerializeField, Range(0f, 1f)] private float twistAmount = 0.6f;
        [Tooltip("몸 틀기 감쇠 시간(초).")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [Tooltip("몸을 튼 만큼 머리가 되돌리는 비율(1 = 시선 완전히 유지).")]
        [SerializeField, Range(0f, 1f)] private float headCounter = 1f;
        [Tooltip("이 속도(m/s) 미만이면 이동 안 함으로 본다.")]
        [SerializeField, Min(0f)] private float minSpeed = 0.2f;

        private ICharacterState state;
        private Animator animator;
        private Transform model;
        private Quaternion modelRestLocal;
        private Transform neck, head;
        private Quaternion neckRest, headRest;
        private float twist, twistVelocity;

        private void Start()
        {
            state = GetComponent<ICharacterState>();
            animator = GetComponentInChildren<Animator>();
            if (animator == null || state == null)
            {
                Debug.LogWarning($"{name}: StrafeBodyTwist에 필요한 ICharacterState 또는 자식 Animator가 없습니다.", this);
                enabled = false;
                return;
            }
            model = animator.transform;
            modelRestLocal = model.localRotation;
            if (animator.isHuman)
            {
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (neck) neckRest = neck.localRotation;
            if (head) headRest = head.localRotation;
        }

        private void LateUpdate()
        {
            twist = Mathf.SmoothDampAngle(twist, TargetTwist(), ref twistVelocity, smoothTime);

            // 몸: 모델을 위쪽 축으로 튼다.
            model.localRotation = Quaternion.AngleAxis(twist, Vector3.up) * modelRestLocal;

            // 머리: 애니메이션이 없으면 뼈가 매 프레임 덮어써지지 않으므로 기본 자세에서 다시 시작한다.
            if (animator.runtimeAnimatorController == null)
            {
                if (neck) neck.localRotation = neckRest;
                if (head) head.localRotation = headRest;
            }

            float counter = -twist * headCounter;
            if (neck && head)
            {
                RotateWorldYaw(neck, counter * 0.4f);
                RotateWorldYaw(head, counter * 0.6f);
            }
            else if (head)
            {
                RotateWorldYaw(head, counter);
            }
        }

        // 이동 방향(캐릭터 기준)에서 몸을 틀 각도. 앞+옆이면 그쪽으로, 뒤+옆이면 반대로 틀어 등이 이동 방향을 향하게 한다.
        private float TargetTwist()
        {
            Vector2 v = state.LocalPlanarVelocity; // x = 우, y = 앞
            if (v.magnitude < minSpeed || Mathf.Abs(v.x) < 0.05f) return 0f;
            float side = Mathf.Atan2(v.x, Mathf.Abs(v.y)) * Mathf.Rad2Deg; // -90 ~ 90
            if (v.y < -0.05f) side = -side;
            return Mathf.Clamp(side * twistAmount, -maxBodyTwist, maxBodyTwist);
        }

        private static void RotateWorldYaw(Transform bone, float degrees)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * bone.rotation;
        }
    }
}

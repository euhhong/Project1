using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩·카우보이 올가미를 조준하는 동안(E 꾹 — GrabShotSkill.IsAiming) 줄을 붕붕 돌리는 자세(연출 전용).
    /// 서 있든 걷든(다리는 걷기 모션 그대로) 오른팔을 머리 위로 들고, 손이 줄 도는 박자(RopeSpin)에 맞춰 작은 원을 그린다.
    /// 줄·고리(LassoVisual)·물고기와 사슬(GrabShotVisual)은 이 손을 따라 돈다 — 그래서 이 자세가 먼저 적용된다(DefaultExecutionOrder).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 사냥터지기·카우보이).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(40)]
    public class RopeSpinPose : MonoBehaviour
    {
        [Tooltip("위팔이 가리킬 방향(캐릭터 기준 x = 오른쪽, y = 위, z = 앞) — 머리 위로 들되 살짝 오른쪽·앞.")]
        [SerializeField] private Vector3 upperArmDir = new(0.3f, 1f, 0.15f);
        [Tooltip("아래팔이 가리킬 방향(캐릭터 기준) — 위로.")]
        [SerializeField] private Vector3 forearmDir = new(0.05f, 1f, 0.1f);
        [Tooltip("손이 그리는 작은 원의 크기(아래팔 방향을 이만큼 기울여 돌림, 0~1).")]
        [SerializeField, Range(0f, 0.6f)] private float handCircle = 0.25f;
        [Tooltip("자세가 켜지고 꺼지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float blendIn = 0.12f, blendOut = 0.2f;

        private CharacterSkill skill;
        private Animator animator;
        private Transform upper, fore, handBone;
        private float weight;

        private void LateUpdate()
        {
            if (skill == null) TryGetComponent(out skill);
            var grab = skill != null ? skill.Skill as GrabShotSkill : null;
            bool aiming = grab != null && grab.IsAiming(skill);
            weight = Mathf.MoveTowards(weight, aiming ? 1f : 0f, Time.deltaTime / (aiming ? blendIn : blendOut));
            if (weight <= 0f || !Resolve()) return;
            float w = Mathf.SmoothStep(0f, 1f, weight);

            // 위팔: 머리 위로. 아래팔: 위로 세우되 줄 도는 박자에 맞춰 둘레로 살짝 기울여 손이 작은 원을 그린다.
            Aim(upper, fore, transform.TransformDirection(upperArmDir.normalized), w);
            float a = RopeSpin.Angle;
            Vector3 circle = transform.TransformDirection(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))) * handCircle;
            Aim(fore, handBone, (transform.TransformDirection(forearmDir.normalized) + circle).normalized, w);
        }

        private static void Aim(Transform bone, Transform child, Vector3 worldDir, float w)
        {
            if (bone == null || child == null) return;
            Vector3 cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur.normalized, worldDir), w) * bone.rotation;
        }

        private bool Resolve()
        {
            if (animator != null && animator.isActiveAndEnabled && upper != null) return true;
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            return upper != null && fore != null && handBone != null;
        }
    }

    /// <summary>줄을 붕붕 돌리는 박자(연출 전용) — 팔 자세(RopeSpinPose)와 줄·고리·물고기 그리기가 같은 각도를 쓴다.</summary>
    public static class RopeSpin
    {
        /// <summary>1초에 도는 바퀴 수.</summary>
        public const float TurnsPerSecond = 2f;

        /// <summary>지금 도는 각도(라디안, 위에서 보아 시계 반대 방향으로 늘어남).</summary>
        public static float Angle => Time.time * TurnsPerSecond * Mathf.PI * 2f;

        /// <summary>캐릭터 기준 수평 원 위의 방향(각도 a).</summary>
        public static Vector3 Around(Transform character, float a) => character.TransformDirection(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
    }
}

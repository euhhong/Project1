using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 생존자가 턱(창틀·난간·바닥 끝)에 매달려 있을 때 손이 턱 윗면을 실제로 감싸 쥐게 팔·손목을 아주 조금만 돌린다(FE, 연출 전용).
    /// 매달리기 모션(원본 클립·리깅)은 그대로 두고, 모션이 나온 뒤(LateUpdate) 뼈를 몇 도만 보정한다 — 뼈를 늘리거나 옮기지 않아 모션이 찢어지지 않는다.
    /// - 위팔: 손목이 턱 모서리 바로 위(윗면 + wristAbove, 앞면에서 wristIn 안쪽)로 가도록 어깨에서 돌린다(최대 armMaxAngle도).
    /// - 손: 손가락(가운데 손가락 뿌리 쪽)이 턱 윗면을 넘어 벽 쪽·약간 아래를 향하게 돌린다(최대 wristMaxAngle도).
    /// 잡기 뒤 절반(HangGrab)·매달림(Hang)에서만 켜고, 옆 이동·모서리 돌기(HangHop) 동안은 부드럽게 꺼 그 모션을 그대로 둔다.
    /// 감시자(노란옷의 왕은 HasturHangPose가 따로 한다)에는 쓰지 않는다. CharacterTraversalAnimator가 캐릭터 루트에 붙인다.
    /// </summary>
    [DefaultExecutionOrder(100)] // 다른 뼈 보정(몸통 돌리기 등)이 끝난 뒤 손을 턱에 맞춘다
    [DisallowMultipleComponent]
    public class HangHandGrip : MonoBehaviour
    {
        [Tooltip("손목이 턱 윗면보다 이만큼 위에(Humanoid 단위) — 손바닥이 윗면에 얹힌다.")]
        [SerializeField] private float wristAbove = 0.05f;
        [Tooltip("손목이 턱 앞면에서 이만큼 안쪽(윗면 위, Humanoid 단위).")]
        [SerializeField] private float wristIn = 0.04f;
        [Tooltip("어깨에서 위팔을 돌리는 최대 각도(도) — 아주 조금만.")]
        [SerializeField, Range(0f, 20f)] private float armMaxAngle = 9f;
        [Tooltip("손목을 돌리는 최대 각도(도).")]
        [SerializeField, Range(0f, 30f)] private float wristMaxAngle = 14f;
        [Tooltip("손가락이 향할 방향의 아래쪽 기울기(0 = 수평으로 벽 쪽, 1 = 45도 아래).")]
        [SerializeField, Range(0f, 1f)] private float fingerDown = 0.35f;
        [Tooltip("켜고 끄는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fade = 0.15f;

        private ICharacterTraversalState traversal;
        private Animator animator;
        private float weight;

        private struct Arm { public Transform Upper, Lower, Hand, Finger; }
        private Arm left, right;

        private void LateUpdate()
        {
            traversal ??= GetComponent<ICharacterTraversalState>();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                left = right = default;
                if (animator != null && animator.isHuman)
                {
                    left = Bones(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal);
                    right = Bones(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal);
                }
            }
            if (traversal == null || animator == null || !animator.isHuman) return;

            bool on = !traversal.HunterStyle && traversal.TryGetHangLedge(out _, out _, out _)
                      && (traversal.Action == TraversalAction.Hang || (traversal.Action == TraversalAction.HangGrab && traversal.ActionProgress > 0.5f));
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / fade);
            if (weight <= 0f || !traversal.TryGetHangLedge(out float top, out Vector3 face, out Vector3 normal)) return;
            normal.y = 0f;
            if (normal.sqrMagnitude < 1e-4f) return;
            normal.Normalize();

            float hu = animator.humanScale * animator.transform.lossyScale.y;
            Grip(left, top, face, normal, hu);
            Grip(right, top, face, normal, hu);
        }

        private Arm Bones(HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand, HumanBodyBones finger) => new()
        {
            Upper = animator.GetBoneTransform(upper),
            Lower = animator.GetBoneTransform(lower),
            Hand = animator.GetBoneTransform(hand),
            Finger = animator.GetBoneTransform(finger),
        };

        private void Grip(Arm arm, float top, Vector3 face, Vector3 normal, float hu)
        {
            if (arm.Upper == null || arm.Hand == null) return;
            // 손목 목표: 지금 손목의 옆 위치 그대로, 턱 모서리 바로 위(앞면에서 조금 안쪽).
            Vector3 wrist = arm.Hand.position;
            float outFromFace = Vector3.Dot(wrist - face, normal);
            Vector3 target = wrist - normal * (outFromFace + wristIn * hu);
            target.y = top + wristAbove * hu;

            // 위팔: 어깨 → 손목 방향을 목표 쪽으로 조금만 돌린다(팔 길이·굽힘은 그대로).
            Vector3 shoulder = arm.Upper.position;
            Vector3 from = wrist - shoulder, to = target - shoulder;
            if (from.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f)
            {
                Quaternion turn = Quaternion.FromToRotation(from, to);
                arm.Upper.rotation = Limit(turn, armMaxAngle * weight) * arm.Upper.rotation;
            }

            // 손: 손가락이 윗면 너머 벽 쪽·약간 아래를 향하게.
            if (arm.Finger != null)
            {
                Vector3 finger = arm.Finger.position - arm.Hand.position;
                Vector3 want = (-normal + Vector3.down * fingerDown).normalized;
                if (finger.sqrMagnitude > 1e-6f)
                {
                    Quaternion turn = Quaternion.FromToRotation(finger, want);
                    arm.Hand.rotation = Limit(turn, wristMaxAngle * weight) * arm.Hand.rotation;
                }
            }
        }

        /// <summary>회전을 최대 maxDeg도까지만(같은 축으로 줄여서).</summary>
        private static Quaternion Limit(Quaternion q, float maxDeg)
        {
            q.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(angle) < 1e-3f) return Quaternion.identity;
            return Quaternion.AngleAxis(Mathf.Clamp(angle, -maxDeg, maxDeg), axis);
        }
    }
}

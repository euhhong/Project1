using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 안토니오의 바이올린과 활(VARCO 모델, Resources Violin / ViolinBow — Tools/Blender/orient_prop.py로 원점·축 정리:
    /// 바이올린 원점 = 꼬리 끝, +Z = 스크롤 쪽, +Y = 앞판 / 활 원점 = 손잡이(프로그) 끝, +Z = 활 끝, -Y = 말총 쪽).
    /// 매 프레임 뼈 자리에 맞춰 놓는다(애니메이션 손 자리를 따라간다):
    /// - 켜는 자세(대기 — 바이올린 켜기 모션): 바이올린 꼬리를 목과 왼어깨 사이(턱 밑)에, 스크롤을 왼손 쪽으로, 앞판이 위를 보게.
    ///   활은 손잡이를 오른손바닥에 두고 바이올린 브리지 위를 지나게(오른손이 움직이면 활이 줄 위를 오간다).
    /// - 들고 다니는 자세(걷기·공격·스킬 중): 바이올린은 왼손에 목을 쥐고 아래팔 방향으로, 활은 오른손에 아래팔 방향으로.
    /// 두 자세 사이는 부드럽게 바꾼다. 크기는 몸(목 ~ 왼손 거리)에 맞춘다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 안토니오만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntonioViolin : MonoBehaviour
    {
        private const string ViolinResource = "Violin/Violin", BowResource = "ViolinBow/ViolinBow";
        private const float ViolinModelLength = 1.004f, BowModelLength = 1.123f;

        [Tooltip("켜는 자세에서 바이올린 길이 = 목~왼손 거리 × 이 값(스크롤이 손 너머로 나간다).")]
        [SerializeField, Min(0.5f)] private float violinLengthFactor = 1.3f;
        [Tooltip("활 길이 = 바이올린 길이 × 이 값.")]
        [SerializeField, Min(0.5f)] private float bowLengthFactor = 1.15f;
        [Tooltip("브리지 자리(바이올린 꼬리에서 길이 비율).")]
        [SerializeField, Range(0f, 1f)] private float bridgeAt = 0.24f;
        [Tooltip("들고 다닐 때 왼손이 쥐는 자리(꼬리에서 길이 비율, 목 부분).")]
        [SerializeField, Range(0f, 1f)] private float carryGripAt = 0.72f;
        [Tooltip("자세를 바꾸는 시간(초).")]
        [SerializeField, Min(0.01f)] private float blendTime = 0.25f;

        private Animator animator;
        private Transform neck, lShoulder, lHand, lLower, lMiddle, rHand, rLower, rMiddle;
        private Transform violin, bow;
        private Quaternion violinBaseRot, bowBaseRot;
        private ICharacterState state;
        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private float play; // 0 = 들고 다님, 1 = 켜는 자세
        private float length = 0.6f;

        private void OnDestroy()
        {
            if (violin != null) Destroy(violin.gameObject);
            if (bow != null) Destroy(bow.gameObject);
        }

        private void OnDisable()
        {
            if (violin != null) violin.gameObject.SetActive(false);
            if (bow != null) bow.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled) { if (!Resolve()) return; }
            if (violin == null && !Build()) return;
            violin.gameObject.SetActive(true); bow.gameObject.SetActive(true);
            state ??= GetComponent<ICharacterState>();
            attack ??= GetComponent<ICharacterAttackState>();
            skill ??= GetComponent<ICharacterSkillState>();
            bool idle = (state == null || (state.IsGrounded && state.MoveState == CharacterMoveState.Idle))
                        && (attack == null || attack.AttackState == CharacterAttackState.None)
                        && (skill == null || skill.State != SkillState.Casting);
            play = Mathf.MoveTowards(play, idle ? 1f : 0f, Time.deltaTime / blendTime);

            Vector3 palmL = Palm(lHand, lMiddle, lLower), palmR = Palm(rHand, rMiddle, rLower);
            Vector3 right = transform.right, up = Vector3.up;

            // 켜는 자세
            Vector3 tail = Vector3.Lerp(neck.position, lShoulder.position, 0.55f) + transform.forward * 0.04f;
            Vector3 axisP = palmL - tail;
            float reachP = axisP.magnitude;
            if (play > 0.5f && reachP > 0.05f) length = Mathf.Lerp(length, reachP * violinLengthFactor, 0.2f); // 몸 크기에 맞춤(켜는 동안 천천히 맞춘다)
            axisP = reachP > 1e-3f ? axisP / reachP : transform.forward;
            Vector3 faceP = Vector3.ProjectOnPlane(up + right * 0.35f, axisP).normalized;
            Quaternion vRotP = Quaternion.LookRotation(axisP, faceP);
            Vector3 vPosP = tail;
            Vector3 bridge = tail + axisP * (length * bridgeAt) + faceP * (length * 0.06f);
            Vector3 bowDirP = bridge - palmR;
            bowDirP = bowDirP.sqrMagnitude > 1e-6f ? bowDirP.normalized : right * -1f;
            Quaternion bRotP = Quaternion.LookRotation(bowDirP, Vector3.ProjectOnPlane(faceP, bowDirP).normalized); // 말총(-Y)이 줄 쪽

            // 들고 다니는 자세
            Vector3 armL = (palmL - lLower.position); armL = armL.sqrMagnitude > 1e-6f ? armL.normalized : -up;
            Vector3 faceC = Vector3.ProjectOnPlane(transform.forward, armL).normalized;
            Quaternion vRotC = Quaternion.LookRotation(-armL, faceC); // 스크롤이 손 쪽(위), 몸통이 아래로 늘어짐
            Vector3 vPosC = palmL + armL * (length * carryGripAt);
            Vector3 armR = (palmR - rLower.position); armR = armR.sqrMagnitude > 1e-6f ? armR.normalized : -up;
            Quaternion bRotC = Quaternion.LookRotation(armR, Vector3.ProjectOnPlane(transform.forward, armR).normalized);

            violin.SetPositionAndRotation(Vector3.Lerp(vPosC, vPosP, play), Quaternion.Slerp(vRotC, vRotP, play) * violinBaseRot);
            violin.localScale = Vector3.one * (length / ViolinModelLength) / Mathf.Max(transform.lossyScale.x, 1e-4f);
            bow.SetPositionAndRotation(palmR, Quaternion.Slerp(bRotC, bRotP, play) * bowBaseRot);
            bow.localScale = Vector3.one * (length * bowLengthFactor / BowModelLength) / Mathf.Max(transform.lossyScale.x, 1e-4f);
        }

        private static Vector3 Palm(Transform hand, Transform middle, Transform lower)
        {
            Vector3 fingers = middle != null ? middle.position - hand.position : lower != null ? (hand.position - lower.position) * 0.3f : Vector3.zero;
            return hand.position + fingers * 0.5f;
        }

        private bool Resolve()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head);
            lShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder) ?? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lMiddle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rMiddle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            return neck != null && lShoulder != null && lHand != null && lLower != null && rHand != null && rLower != null;
        }

        private bool Build()
        {
            var vp = Resources.Load<GameObject>(ViolinResource);
            var bp = Resources.Load<GameObject>(BowResource);
            if (vp == null || bp == null) return false;
            violin = Instantiate(vp, transform).transform; violin.name = "Violin";
            bow = Instantiate(bp, transform).transform; bow.name = "ViolinBow";
            violinBaseRot = vp.transform.localRotation; bowBaseRot = bp.transform.localRotation; // FBX 축 맞춤
            foreach (var r in violin.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return true;
        }
    }
}

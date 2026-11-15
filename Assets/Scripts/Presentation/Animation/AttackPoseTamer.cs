using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 평타 모션을 조금 얌전하게(연출 전용, 예: 루키노 가로 베기).
    /// - 고개: 평타를 시작하기 직전의 머리 방향(몸 기준)을 공격 내내 지킨다 — 휘두를 때 고개가 돌아가지 않고 앞을 본다(headLock).
    /// - 오른팔: 휘두르는 폭을 줄인다 — 위팔·아래팔을 시작 직전 자세 쪽으로 (1 - armSwingScale)만큼 되돌린다.
    /// 모션이 쓴 자세 위에 매 프레임 덮는다(Humanoid는 Animator가 매 프레임 뼈를 다시 쓰므로 쌓이지 않음). 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackPoseTamer : MonoBehaviour
    {
        [Tooltip("고개를 시작 직전 방향으로 붙잡는 정도(0 = 모션 그대로, 1 = 완전히 앞).")]
        [SerializeField, Range(0f, 1f)] private float headLock = 1f;
        [Tooltip("오른팔 휘두르는 폭 배율(1 = 모션 그대로, 0.6 = 40% 작게).")]
        [SerializeField, Range(0f, 1f)] private float armSwingScale = 0.6f;
        [SerializeField, Min(0.01f)] private float blendIn = 0.06f, blendOut = 0.15f;

        private ICharacterAttackState attack;
        private Animator animator;
        private Transform head, neck, upper, fore;
        private Quaternion headRel, neckRel, upperLocal, foreLocal; // 시작 직전 자세(머리·목 = 루트 기준, 팔 = 부모 기준)
        private bool wasAttacking, hasSnapshot;
        private float weight;

        private void LateUpdate()
        {
            attack ??= GetComponent<ICharacterAttackState>();
            if (attack == null || !Resolve()) return;
            bool attacking = attack.AttackState is CharacterAttackState.Light or CharacterAttackState.Heavy;
            if (attacking && !wasAttacking) Snapshot(); // 이번 프레임 모션은 이미 공격 첫 자세 — 지난 프레임에 찍어 둔 것을 쓴다
            wasAttacking = attacking;
            weight = Mathf.MoveTowards(weight, attacking ? 1f : 0f, Time.deltaTime / (attacking ? blendIn : blendOut));
            if (!attacking) { Record(); }
            if (weight <= 0f || !hasSnapshot) return;

            Quaternion root = transform.rotation;
            float w = Mathf.SmoothStep(0f, 1f, weight);
            if (neck != null) neck.rotation = Quaternion.Slerp(neck.rotation, root * neckRel, w * headLock * 0.5f);
            if (head != null) head.rotation = Quaternion.Slerp(head.rotation, root * headRel, w * headLock);
            float back = w * (1f - armSwingScale);
            if (upper != null) upper.localRotation = Quaternion.Slerp(upper.localRotation, upperLocal, back);
            if (fore != null) fore.localRotation = Quaternion.Slerp(fore.localRotation, foreLocal, back);
        }

        // 공격하지 않는 동안 매 프레임 자세를 기억해 두었다가(Record) 공격이 시작되면 그것을 기준으로 쓴다(Snapshot).
        private Quaternion lastHeadRel, lastNeckRel, lastUpperLocal, lastForeLocal; private bool hasLast;
        private void Record()
        {
            Quaternion inv = Quaternion.Inverse(transform.rotation);
            if (head != null) lastHeadRel = inv * head.rotation;
            if (neck != null) lastNeckRel = inv * neck.rotation;
            if (upper != null) lastUpperLocal = upper.localRotation;
            if (fore != null) lastForeLocal = fore.localRotation;
            hasLast = true;
        }

        private void Snapshot()
        {
            if (!hasLast) Record();
            headRel = lastHeadRel; neckRel = lastNeckRel; upperLocal = lastUpperLocal; foreLocal = lastForeLocal;
            hasSnapshot = true;
        }

        private bool Resolve()
        {
            if (animator != null && animator.isActiveAndEnabled && head != null) return true;
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hasLast = hasSnapshot = false;
            return head != null;
        }
    }
}

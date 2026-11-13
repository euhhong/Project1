using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 광대의 무기: 아기자기한 로켓 폭죽(ToyRocket)을 두 손으로 받쳐 든다.
    /// - 로켓은 오른손 뼈 아래에 붙는다. 들고 다닐 때(공격 중이 아닐 때)는 매 프레임 꼬리를 오른손 조금 뒤에,
    ///   몸통을 왼손 쪽으로 눕혀 왼손바닥에 얹히게 맞춘다(Carry 레이어 자세).
    /// - 공격 중에는 통나무·각목을 휘두르듯 두 손으로 꼬리 쪽을 쥔다: 오른손이 맨 끝, 로켓은 아래팔이 뻗는 방향으로 나가되
    ///   몸 앞쪽으로 당겨 머리가 되도록 앞을 본다(머리 위로 들 때도 뒤로 넘어가지 않음). 두 자세 사이는 짧게 바꿔 쥔다.
    /// - 로켓 크기의 판정 상자(Hitbox_Rocket, 오른손 자리)를 붙여 오른손 공격이 로켓으로 맞힌다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 광대만). 모델이 바뀌면 새 손에 다시 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RocketWeapon : MonoBehaviour
    {
        [Tooltip("로켓 크기 배율(ToyRocket 기본 길이 0.8m 기준).")]
        [SerializeField, Min(0.1f)] private float worldScale = 1.6f;
        [Tooltip("꼬리를 오른손 뒤로 얼마나 빼는지(m).")]
        [SerializeField, Min(0f)] private float tailBehindHand = 0.15f;
        [Tooltip("로켓을 손바닥 위로 얼마나 띄울지(m, 왼손이 아래에서 받친다).")]
        [SerializeField] private float liftAboveHands = 0.06f;

        [Header("공격 중 쥐는 법(통나무·각목처럼 두 손으로 꼬리 쪽)")]
        [Tooltip("꼬리 끝이 오른손(맨 끝 손)보다 얼마나 더 나오는지(m).")]
        [SerializeField, Min(0f)] private float gripTailPastHand = 0.08f;
        [Tooltip("로켓 머리를 몸 앞쪽으로 당기는 정도(0 = 팔 방향 그대로, 클수록 앞을 본다).")]
        [SerializeField, Min(0f)] private float keepHeadForward = 1.4f;
        [Tooltip("들고 다니는 자세 ↔ 공격 자세로 바꿔 쥐는 시간(초).")]
        [SerializeField, Min(0.01f)] private float gripSwitchTime = 0.08f;

        private ICharacterAttackState attack;
        private Animator animator;
        private Transform leftHand, rightHand, leftLower, rightLower;
        private ToyRocket rocket;
        private float attackBlend;

        private void Awake() => attack = GetComponent<ICharacterAttackState>();

        private void OnDestroy()
        {
            if (rocket != null) Destroy(rocket.gameObject);
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled || rocket == null)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) return;
                Attach();
                if (rocket == null) return;
            }

            bool attacking = attack != null && attack.AttackState != CharacterAttackState.None;
            attackBlend = Mathf.MoveTowards(attackBlend, attacking ? 1f : 0f, Time.deltaTime / gripSwitchTime);

            // 들고 다닐 때: 두 손에 맞춰 눕힌다(오른손(뒤) → 왼손(앞) 방향, 왼손바닥에 얹힘).
            Vector3 r = rightHand.position, l = leftHand.position;
            Vector3 carryDir = l - r;
            if (carryDir.sqrMagnitude < 1e-4f) carryDir = transform.forward;
            carryDir.Normalize();
            Vector3 carryUp = Vector3.ProjectOnPlane(Vector3.up, carryDir).normalized;
            Vector3 carryPos = r - carryDir * tailBehindHand + carryUp * liftAboveHands;
            if (attackBlend <= 0f)
            {
                rocket.transform.SetPositionAndRotation(carryPos, Quaternion.LookRotation(carryDir, carryUp));
                return;
            }

            // 공격 중: 통나무·각목을 휘두르듯 두 손으로 꼬리 쪽을 쥔다(오른손이 맨 끝, 왼손이 그 위).
            // 로켓은 아래팔이 뻗는 방향(팔꿈치 → 손)으로 나가되, 몸 앞쪽으로 당겨 머리가 되도록 앞을 보게 한다.
            Vector3 forearm = (r - rightLower.position) + (l - leftLower.position);
            if (forearm.sqrMagnitude < 1e-4f) forearm = transform.forward;
            Vector3 swingDir = (forearm.normalized + transform.forward * keepHeadForward).normalized;
            Vector3 side = Vector3.Cross(swingDir, transform.right).sqrMagnitude > 1e-4f ? transform.right : transform.up;
            Vector3 swingUp = Vector3.Cross(side, swingDir).normalized;
            Vector3 grip = r - swingDir * gripTailPastHand;
            Vector3 pos = Vector3.Lerp(carryPos, grip, attackBlend);
            Quaternion rot = Quaternion.Slerp(Quaternion.LookRotation(carryDir, carryUp), Quaternion.LookRotation(swingDir, swingUp), attackBlend);
            rocket.transform.SetPositionAndRotation(pos, rot);
        }

        private void Attach()
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (rightHand == null || leftHand == null || rightLower == null || leftLower == null) return;
            if (rocket != null) Destroy(rocket.gameObject);
            var go = new GameObject("Rocket");
            go.transform.SetParent(rightHand, false);
            go.transform.localScale = Vector3.one * (worldScale / Mathf.Max(rightHand.lossyScale.x, 1e-4f));
            rocket = go.AddComponent<ToyRocket>();
            attackBlend = 0f;

            var hb = new GameObject("Hitbox_Rocket");
            hb.transform.SetParent(go.transform, false);
            var box = hb.AddComponent<BoxCollider>();
            float w = rocket.Radius * 2.6f;
            box.center = new Vector3(0f, 0f, rocket.Length * 0.5f);
            box.size = new Vector3(w, w, rocket.Length);
            hb.AddComponent<Hitbox>().SetSlot(HitboxSlot.RightHand);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }
    }
}

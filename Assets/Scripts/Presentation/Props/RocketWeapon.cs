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
    /// - 로켓 돌진(E, RocketChargeSkill): E를 누르는 동안 두 손으로 머리 위에 가로로 치켜들고(머리는 앞),
    ///   돌진하면 무게중심을 낮춘 채 로켓을 껴안듯 감싸 안고(몸통이 두 팔 사이를 지남) 머리를 정면으로 들이받는다.
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

        [Header("로켓 돌진(E) 중 쥐는 법")]
        [Tooltip("치켜들 때 로켓을 두 손 위로 얼마나 올릴지(m).")]
        [SerializeField] private float raiseAboveHands = 0.08f;
        [Tooltip("돌진할 때 로켓 머리를 아래로 기울이는 각도(도).")]
        [SerializeField] private float ramPitchDown = 4f;
        [Tooltip("돌진할 때 로켓을 껴안는 자리: 두 손보다 뒤로 나오는 길이 비율(0 = 꼬리 끝을 쥠, 0.5 = 가운데를 안음).")]
        [SerializeField, Range(0f, 0.7f)] private float ramHugBack = 0.4f;
        private const string RocketChargeId = "SmileyFace_RocketCharge";

        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private Animator animator;
        private Transform leftHand, rightHand, leftLower, rightLower;
        private ToyRocket rocket;
        private float attackBlend, skillBlend, ramBlend;

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
            Vector3 pos = carryPos;
            Quaternion rot = Quaternion.LookRotation(carryDir, carryUp);

            if (attackBlend > 0f)
            {
                // 공격 중: 통나무·각목을 휘두르듯 두 손으로 꼬리 쪽을 쥔다(오른손이 맨 끝, 왼손이 그 위).
                // 로켓은 아래팔이 뻗는 방향(팔꿈치 → 손)으로 나가되, 몸 앞쪽으로 당겨 머리가 되도록 앞을 보게 한다.
                Vector3 forearm = (r - rightLower.position) + (l - leftLower.position);
                if (forearm.sqrMagnitude < 1e-4f) forearm = transform.forward;
                Vector3 swingDir = (forearm.normalized + transform.forward * keepHeadForward).normalized;
                Vector3 side = Vector3.Cross(swingDir, transform.right).sqrMagnitude > 1e-4f ? transform.right : transform.up;
                Vector3 swingUp = Vector3.Cross(side, swingDir).normalized;
                Vector3 grip = r - swingDir * gripTailPastHand;
                pos = Vector3.Lerp(carryPos, grip, attackBlend);
                rot = Quaternion.Slerp(rot, Quaternion.LookRotation(swingDir, swingUp), attackBlend);
            }

            // 로켓 돌진(E): 들고 다님 = 두 손으로 머리 위에 가로로 치켜듦(머리는 앞), 돌진 = 로켓을 껴안고 머리를 정면으로 들이받음.
            if (skill == null) TryGetComponent(out skill);
            bool inSkill = skill != null && skill.State == SkillState.Casting && skill.SkillId == RocketChargeId && skill.MotionPhase != AttackPhase.None;
            bool ramming = inSkill && skill.MotionPhase == AttackPhase.Active;
            skillBlend = Mathf.MoveTowards(skillBlend, inSkill ? 1f : 0f, Time.deltaTime / gripSwitchTime);
            ramBlend = Mathf.MoveTowards(ramBlend, ramming ? 1f : 0f, Time.deltaTime / gripSwitchTime);
            if (skillBlend > 0f)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 mid = (r + l) * 0.5f;
                float length = rocket.Length * worldScale;
                Vector3 raisePos = mid + Vector3.up * raiseAboveHands - fwd * (length * 0.5f); // 가운데를 두 손으로 받침
                Quaternion raiseRot = Quaternion.LookRotation(fwd, Vector3.up);
                Vector3 ramDir = Quaternion.AngleAxis(ramPitchDown, Vector3.Cross(Vector3.up, fwd)) * fwd;
                Vector3 ramPos = mid - ramDir * (length * ramHugBack);                           // 껴안듯: 몸통이 두 팔 사이를 지나 뒤쪽이 가슴에 닿음
                Quaternion ramRot = Quaternion.LookRotation(ramDir, Vector3.up);
                Vector3 skillPos = Vector3.Lerp(raisePos, ramPos, ramBlend);
                Quaternion skillRot = Quaternion.Slerp(raiseRot, ramRot, ramBlend);
                pos = Vector3.Lerp(pos, skillPos, skillBlend);
                rot = Quaternion.Slerp(rot, skillRot, skillBlend);
            }
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

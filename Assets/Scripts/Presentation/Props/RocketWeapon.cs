using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 광대의 무기: 아기자기한 로켓 폭죽(ToyRocket)을 두 손으로 받쳐 든다.
    /// - 로켓은 오른손 뼈 아래에 붙는다. 들고 다닐 때(공격 중이 아닐 때)는 매 프레임 꼬리를 오른손 조금 뒤에,
    ///   몸통을 왼손 쪽으로 눕혀 왼손바닥에 얹히게 맞춘다(Carry 레이어 자세).
    /// - 공격 중에는 팔이 크게 움직이므로, 마지막으로 맞춘 오른손 기준 자세 그대로 오른손에 붙어 함께 휘둘러진다.
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

        private ICharacterAttackState attack;
        private Animator animator;
        private Transform leftHand, rightHand;
        private ToyRocket rocket;
        private Vector3 localPos; private Quaternion localRot;
        private bool hasPose;

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
            if (!attacking || !hasPose)
            {
                // 두 손에 맞춰 눕힌다: 오른손(뒤) → 왼손(앞) 방향.
                Vector3 r = rightHand.position, l = leftHand.position;
                Vector3 dir = l - r;
                if (dir.sqrMagnitude < 1e-4f) dir = transform.forward;
                dir.Normalize();
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir).normalized;
                rocket.transform.SetPositionAndRotation(r - dir * tailBehindHand + up * liftAboveHands, Quaternion.LookRotation(dir, up));
                localPos = rocket.transform.localPosition; localRot = rocket.transform.localRotation; hasPose = true;
            }
            else
            {
                // 공격 중: 오른손에 붙은 채로 같이 움직인다.
                rocket.transform.localPosition = localPos; rocket.transform.localRotation = localRot;
            }
        }

        private void Attach()
        {
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rightHand == null || leftHand == null) return;
            if (rocket != null) Destroy(rocket.gameObject);
            var go = new GameObject("Rocket");
            go.transform.SetParent(rightHand, false);
            go.transform.localScale = Vector3.one * (worldScale / Mathf.Max(rightHand.lossyScale.x, 1e-4f));
            rocket = go.AddComponent<ToyRocket>();
            hasPose = false;

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

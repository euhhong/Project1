using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 붉은나비(미치코)의 접는 부채. 오른손에 쥐고 다니다가(접힘) 공격할 때 펼친다.
    /// - 부채(FoldingFan)는 오른손 뼈 아래에 붙인다. 쥔 주먹에서 몸 앞쪽으로 뻗고, 펴면 팔과 수직인 면으로 펼쳐진다.
    /// - 펼친 부채 크기의 판정 상자(Hitbox, 오른손 자리)를 붙여, 오른손 공격이 부채로 맞힌다.
    /// - 모델이 바뀌면(캐릭터 교체) 새 모델 손에 다시 붙인다.
    /// 캐릭터 루트에 붙인다. CharacterLoadout이 붉은나비일 때만 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FanWeapon : MonoBehaviour
    {
        [Tooltip("부채 실제 길이 배율(FoldingFan 기본 길이 0.32m 기준).")]
        [SerializeField, Min(0.1f)] private float worldScale = 1.4f;
        [Tooltip("펼치는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.01f)] private float openTime = 0.08f;
        [Tooltip("접는 데 걸리는 시간(초).")]
        [SerializeField, Min(0.01f)] private float closeTime = 0.3f;

        private ICharacterAttackState attack;
        private Animator animator;
        private FoldingFan fan;
        private BoxCollider hitBox;

        private void Awake() => attack = GetComponent<ICharacterAttackState>();

        private void OnDestroy()
        {
            if (fan != null) Destroy(fan.gameObject);
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled || fan == null)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) return;
                Attach();
            }
            if (fan == null) return;

            bool open = attack != null && attack.AttackState != CharacterAttackState.None;
            float target = open ? 1f : 0f;
            fan.Openness = Mathf.MoveTowards(fan.Openness, target, Time.deltaTime / (target > fan.Openness ? openTime : closeTime));
        }

        private void Attach()
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;
            if (fan != null) Destroy(fan.gameObject);

            // 손의 길이 방향: 가운뎃손가락 뿌리가 있으면 그쪽, 없으면(붉은나비처럼 손가락 뼈가 없는 모델) 아래팔 → 손.
            var lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Vector3 forearm = lower != null ? hand.position - lower.position : -hand.up;
            Vector3 along = (middle != null ? middle.position - hand.position : forearm).normalized;
            // 부채는 주먹에서 몸 앞쪽으로 뻗고(쥔 손 위로 나온 막대처럼), 펴면 팔과 수직인 면으로 펼쳐진다(찌를 때 상대 쪽으로 펴짐).
            Vector3 ribs = Vector3.ProjectOnPlane(transform.forward, along);
            if (ribs.sqrMagnitude < 1e-6f) ribs = Vector3.ProjectOnPlane(hand.forward, along);
            ribs.Normalize();

            var go = new GameObject("Fan");
            go.transform.SetParent(hand, false);
            // 사북(손잡이 끝)은 손바닥 가운데쯤(손 길이의 약 1/4 지점).
            go.transform.position = hand.position + along * (forearm.magnitude * 0.25f);
            go.transform.rotation = Quaternion.LookRotation(along, ribs);
            float parentScale = Mathf.Max(hand.lossyScale.x, 1e-4f);
            go.transform.localScale = Vector3.one * (worldScale / parentScale);
            fan = go.AddComponent<FoldingFan>();
            fan.Openness = 0f;

            // 판정: 펼친 부채를 덮는 납작한 상자. 오른손 자리라 오른손 공격(약·강)에 같이 켜진다.
            var hb = new GameObject("Hitbox_Fan");
            hb.transform.SetParent(go.transform, false);
            hitBox = hb.AddComponent<BoxCollider>();
            float l = fan.Length;
            float half = Mathf.Sin(fan.SpreadAngle * 0.5f * Mathf.Deg2Rad) * l;
            hitBox.center = new Vector3(0f, l * 0.6f, 0f);
            hitBox.size = new Vector3(half * 2f, l * 0.8f, 0.06f);
            hb.AddComponent<Hitbox>().SetSlot(HitboxSlot.RightHand);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }
    }
}

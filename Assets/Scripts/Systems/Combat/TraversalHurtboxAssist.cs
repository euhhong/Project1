using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 오르기 동작 중 맞는 판정 보정(BE): 몸을 감싸는 보정 판정(Hurtbox, Assist — 몸통으로 맞음)을 그 동작 동안만 켠다.
    /// - 창틀 넘기(WindowVault·WindowDrop·WindowJump): 몸보다 조금 넓게(vaultExtraRadius·vaultExtraHeight) — 넘는 중인 상대를 치기 쉽게.
    /// - 매달리기(HangGrab·Hang·HangHop): 위아래로 길게(hangExtraUp·hangExtraDown), 옆은 거의 그대로 — 턱에 매달린 상대를 위·아래에서 치기 쉽게.
    /// 진짜 부위 판정이 같이 겹치면 그쪽을 쓴다(AttackHitDetector). CharacterTraversal이 캐릭터 루트에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TraversalHurtboxAssist : MonoBehaviour
    {
        [Header("창틀 넘기 중")]
        [Tooltip("몸(CharacterController)보다 넓히는 반지름(m) / 위아래로 늘이는 높이(m, 합).")]
        [SerializeField, Min(0f)] private float vaultExtraRadius = 0.25f;
        [SerializeField, Min(0f)] private float vaultExtraHeight = 0.3f;

        [Header("매달리기 중")]
        [Tooltip("몸보다 넓히는 반지름(m) / 머리 위로 늘이는 높이(m) / 발 아래로 늘이는 높이(m).")]
        [SerializeField, Min(0f)] private float hangExtraRadius = 0.05f;
        [SerializeField, Min(0f)] private float hangExtraUp = 0.6f;
        [SerializeField, Min(0f)] private float hangExtraDown = 0.8f;

        private ICharacterTraversalState traversal;
        private CharacterController body;
        private CapsuleCollider capsule;
        private Hurtbox hurtbox;

        private void Awake()
        {
            TryGetComponent(out traversal);
            TryGetComponent(out body);
            var go = new GameObject("HurtboxAssist");
            go.transform.SetParent(transform, false);
            capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // 위아래
            capsule.isTrigger = true;
            hurtbox = go.AddComponent<Hurtbox>();
            hurtbox.SetPart(BodyPart.Body);
            hurtbox.SetAssist(true);
            SetOn(false);
        }

        private void OnDisable() { if (capsule != null) SetOn(false); }

        private void FixedUpdate()
        {
            if (traversal == null) TryGetComponent(out traversal);
            if (body == null) TryGetComponent(out body);
            var action = traversal != null ? traversal.Action : TraversalAction.None;
            bool vault = action is TraversalAction.WindowVault or TraversalAction.WindowDrop or TraversalAction.WindowJump;
            bool hang = action is TraversalAction.HangGrab or TraversalAction.Hang or TraversalAction.HangHop;
            if (!vault && !hang || body == null) { SetOn(false); return; }

            float r = body.radius, h = body.height;
            Vector3 c = body.center;
            if (vault)
            {
                r += vaultExtraRadius; h += vaultExtraHeight;
            }
            else
            {
                r += hangExtraRadius; h += hangExtraUp + hangExtraDown;
                c.y += (hangExtraUp - hangExtraDown) * 0.5f;
            }
            capsule.center = c;
            capsule.radius = r;
            capsule.height = Mathf.Max(h, r * 2f);
            SetOn(true);
        }

        private void SetOn(bool on)
        {
            if (capsule.enabled != on) capsule.enabled = on;
            if (hurtbox.enabled != on) hurtbox.enabled = on;
        }
    }
}

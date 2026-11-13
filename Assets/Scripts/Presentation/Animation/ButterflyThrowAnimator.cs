using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 붉은나비가 나비를 놓을 때 부채 든 오른팔로 나비를 휙 던지는 상체 모션(연출 전용).
    /// 새 모션 파일 없이 붉은나비의 기본 공격 모션(Attack 레이어 Light = 오른팔 찌르기, Attack_Light_Geisha)의 내뻗는 부분만
    /// throwTime초에 빠르게 재생한다 — 나비를 놓은 순간(IButterflyField.PlacedCount가 늘어남)부터. 공격 중에는 하지 않는다.
    /// 공격 모션 담당(CharacterAttackAnimator)이 정한 레이어 무게를 던지는 동안만 덮어쓰도록 늦게 돈다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 붉은나비만).
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public class ButterflyThrowAnimator : MonoBehaviour
    {
        private const string LayerName = "Attack";
        private static readonly int LightId = Animator.StringToHash("Light");

        [Tooltip("던지는 모션 시간(초).")]
        [SerializeField, Min(0.05f)] private float throwTime = 0.32f;
        [Tooltip("기본 공격 모션에서 쓸 구간(전체 0~1): 팔을 앞으로 내뻗는 부분.")]
        [SerializeField, Range(0f, 1f)] private float fromAt = 0.28f, toAt = 0.7f;

        private IButterflyField field;
        private ICharacterAttackState attack;
        private Animator animator;
        private int layer = -1;
        private int seen = -1;
        private float startTime = -10f;

        private void LateUpdate()
        {
            if (field == null) field = GetComponent<IButterflyField>();
            if (attack == null) attack = GetComponent<ICharacterAttackState>();
            if (field == null) return;
            if (animator == null || !animator.isActiveAndEnabled) { animator = GetComponentInChildren<Animator>(); layer = animator != null ? animator.GetLayerIndex(LayerName) : -1; }
            if (animator == null || layer < 0) return;

            if (seen < 0) seen = field.PlacedCount;
            if (field.PlacedCount != seen) { seen = field.PlacedCount; startTime = Time.time; }
            float p = (Time.time - startTime) / throwTime;
            bool attacking = attack != null && attack.AttackState != CharacterAttackState.None;
            if (p < 0f || p > 1f || attacking) return;

            animator.Play(LightId, layer, Mathf.Lerp(fromAt, toAt, p));
            float w = p < 0.8f ? 1f : Mathf.InverseLerp(1f, 0.8f, p); // 끝에서 부드럽게 놓는다
            animator.SetLayerWeight(layer, w);
        }
    }
}

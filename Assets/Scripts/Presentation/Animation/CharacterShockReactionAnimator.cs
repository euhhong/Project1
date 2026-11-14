using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 데미지 없는 충격파에 맞으면(BE ShockSlow.Shocked — 예: 사냥터지기 그랩이 벽에 도착할 때) Animator "ShockReact" 레이어(전신)에
    /// 휘청이는 모션(Hit_EdgeSlip — Edge Slip)을 한 번 재생하고 끝 무렵 원래 모션으로 돌아온다. 연출 전용.
    /// 캐릭터 루트에 붙인다(CharacterLoadout). ShockSlow는 실행 중에 붙으므로 매 프레임 찾아 한 번만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterShockReactionAnimator : MonoBehaviour
    {
        private const string LayerName = "ShockReact";
        private static readonly int StateId = Animator.StringToHash("Hit_EdgeSlip");

        [Tooltip("모션이 이 비율(0~1)까지 재생되면 레이어를 줄이기 시작한다.")]
        [SerializeField, Range(0.3f, 1f)] private float fadeOutStart = 0.8f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.2f;

        private ShockSlow slow;
        private Animator animator;
        private int layer = -1;
        private float weight;
        private bool playing;

        private bool Resolve()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f; playing = false;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            return layer >= 0;
        }

        private void OnShocked(float seconds)
        {
            if (!Resolve() || !animator.HasState(layer, StateId)) return;
            animator.Play(StateId, layer, 0f);
            weight = 1f;
            playing = true;
        }

        private void LateUpdate()
        {
            if (slow == null && TryGetComponent(out slow)) slow.Shocked += OnShocked;
            if (!playing || !Resolve()) return;
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == StateId && info.normalizedTime >= fadeOutStart) weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / fadeOut);
            if (weight <= 0f) playing = false;
            animator.SetLayerWeight(layer, weight);
        }

        private void OnDisable()
        {
            playing = false; weight = 0f;
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }

        private void OnDestroy()
        {
            if (slow != null) slow.Shocked -= OnShocked;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 무기를 두 손으로 받쳐 들고 다니는 자세(광대의 로켓 폭죽). Animator "Carry" 레이어(두 팔만)를 켠다.
    /// 다리는 걷기·달리기·점프 그대로이고, 공격·피격·스킬 레이어가 더 위라서 그때는 그 모션이 팔을 덮는다.
    /// 손을 쓰는 동안(창틀·턱 잡기·매달림·옆 이동·매달려 오르기·사다리·올라서기·창틀 넘기 — RocketWeapon.UsesHands)은 끈다:
    /// Carry 레이어가 Traversal 레이어보다 위라 켜 두면 팔이 로켓 든 자세(손이 아래)로 남아 위에서 턱을 잡고 매달린 자세가 안 나온다.
    /// 그동안 로켓은 RocketWeapon이 어깨에 받쳐 둔다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 광대만). 모델이 바뀌면 새 Animator를 다시 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterCarryPose : MonoBehaviour
    {
        private const string LayerName = "Carry";

        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [Tooltip("손을 쓰는 동작으로 들어가고 나올 때 들고 다니는 자세를 끄고 켜는 시간(초).")]
        [SerializeField, Min(0.01f)] private float handsFade = 0.12f;

        private Animator animator;
        private ICharacterTraversalState traversal;
        private int layer = -1;
        private float handsFree;

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer < 0) return;
            traversal ??= GetComponent<ICharacterTraversalState>();
            bool busy = traversal != null && RocketWeapon.UsesHands(traversal.Action);
            handsFree = Mathf.MoveTowards(handsFree, busy ? 1f : 0f, Time.deltaTime / handsFade);
            animator.SetLayerWeight(layer, weight * (1f - handsFree));
        }

        private void OnDisable()
        {
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}

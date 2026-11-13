using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 무기를 두 손으로 받쳐 들고 다니는 자세(광대의 로켓 폭죽). Animator "Carry" 레이어(두 팔만)를 켠다.
    /// 다리는 걷기·달리기·점프 그대로이고, 공격·피격·스킬 레이어가 더 위라서 그때는 그 모션이 팔을 덮는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 광대만). 모델이 바뀌면 새 Animator를 다시 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterCarryPose : MonoBehaviour
    {
        private const string LayerName = "Carry";

        [SerializeField, Range(0f, 1f)] private float weight = 1f;

        private Animator animator;
        private int layer = -1;

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer >= 0) animator.SetLayerWeight(layer, weight);
        }

        private void OnDisable()
        {
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 판자 내리는 모션(FE): 생존자가 판자를 내리는 동안(IPalletUser.Dropping) 전신 Skill 레이어에 Pallet_Drop
    /// (Mixamo/Skill_Pallet_Drop.fbx — Tools/Blender/make_pallet_drop.py: 두 손으로 판자 윗부분을 잡고 앞으로 밀어 넘어뜨림)을 진행도에 맞춰 재생한다.
    /// 끝에서 부드럽게 원래 모션으로 돌아간다. 스킬 모션 담당이 정한 레이어 무게를 내리는 동안만 덮어쓰도록 늦게 돈다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 생존자만).
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public class PalletDropAnimator : MonoBehaviour
    {
        private const string LayerName = "Skill";
        private static readonly int StateId = Animator.StringToHash("Pallet_Drop");

        private IPalletUser user;
        private Animator animator;
        private int layer = -1;
        private bool hasState;

        private void LateUpdate()
        {
            if (user == null) user = GetComponent<IPalletUser>();
            if (user == null || !user.Dropping) return;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = animator != null ? animator.GetLayerIndex(LayerName) : -1;
                hasState = layer >= 0 && animator.HasState(layer, StateId);
            }
            if (!hasState) return;
            float p = user.DropProgress;
            animator.Play(StateId, layer, p);
            animator.SetLayerWeight(layer, p < 0.82f ? 1f : Mathf.InverseLerp(1f, 0.82f, p));
        }
    }
}

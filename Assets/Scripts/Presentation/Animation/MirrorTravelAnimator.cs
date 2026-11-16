using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 거울 순간이동 모션(FE): 들어가는 동안(IMirrorTraveler.Entering) 전신 Skill 레이어에 Mirror_Enter
    /// (Mixamo/Skill_Mirror_Enter.fbx — Tools/Blender/make_mirror_enter.py: 거울에 손을 뻗고 한 발 내디디며 몸을 기울여 빨려 들어감)를 진행도에 맞춰 재생하고,
    /// 나오는 동안(Emerging)은 손을 뻗은 자세에서 거꾸로 서 있는 자세로 돌아오며 원래 모션으로 부드럽게 넘어간다.
    /// 스킬 모션 담당이 정한 레이어 무게를 그동안만 덮어쓰도록 늦게 돈다. 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public class MirrorTravelAnimator : MonoBehaviour
    {
        private const string LayerName = "Skill";
        private const float EmergeFrom = 0.45f; // 나올 때 시작 자세(진행도) — 손을 뻗고 한 발 내디딘 자세
        private static readonly int StateId = Animator.StringToHash("Mirror_Enter");

        private IMirrorTraveler traveler;
        private Animator animator;
        private int layer = -1;
        private bool hasState;

        private void LateUpdate()
        {
            if (traveler == null) traveler = GetComponent<IMirrorTraveler>();
            if (traveler == null || (!traveler.Entering && !traveler.Emerging)) return;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = animator != null ? animator.GetLayerIndex(LayerName) : -1;
                hasState = layer >= 0 && animator.HasState(layer, StateId);
            }
            if (!hasState) return;
            if (traveler.Entering)
            {
                animator.Play(StateId, layer, traveler.EnterProgress);
                animator.SetLayerWeight(layer, 1f);
            }
            else
            {
                float p = traveler.EmergeProgress;
                animator.Play(StateId, layer, Mathf.Lerp(EmergeFrom, 0f, p));
                animator.SetLayerWeight(layer, p < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, p));
            }
        }
    }
}

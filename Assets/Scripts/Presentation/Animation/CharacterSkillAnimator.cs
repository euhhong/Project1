using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 고유 스킬 상태(ICharacterSkillState, BE)를 Animator "Skill" 레이어로 보여 준다.
    /// 시전 중에는 스킬 ID와 같은 이름의 상태(예: Doctor_SelfHeal)를 시전 진행도에 맞춰 재생한다.
    /// 상태가 없으면(모션 파일이 아직 없으면) 아무것도 하지 않는다.
    /// 모션 파일: `_Motions/Mixamo/Skill_<스킬 ID>.fbx` → CharacterMotionControllerBuilder가 상태를 만든다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다(모델이 바뀌면 다시 찾는다).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSkillAnimator : MonoBehaviour
    {
        private const string LayerName = "Skill";

        [SerializeField, Min(0.01f)] private float fadeIn = 0.1f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.2f;

        private ICharacterSkillState skill;
        private Animator animator;
        private int layer = -1;
        private float weight;

        private void Awake()
        {
            skill = GetComponent<ICharacterSkillState>();
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<ICharacterSkillState>();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f;
            }
            if (skill == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer < 0) return;

            int state = Animator.StringToHash(skill.SkillId);
            bool casting = skill.State == SkillState.Casting && animator.HasState(layer, state);
            float target = casting ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / (target > weight ? fadeIn : fadeOut));
            animator.SetLayerWeight(layer, weight);
            if (casting) animator.Play(state, layer, Mathf.Min(skill.CastProgress, 0.999f));
        }
    }
}

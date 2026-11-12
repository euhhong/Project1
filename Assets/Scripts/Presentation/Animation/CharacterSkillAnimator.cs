using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 고유 스킬 상태(ICharacterSkillState, BE)를 Animator "Skill" 레이어(전신)로 보여 준다.
    /// - 시전 중: 스킬 ID와 같은 이름의 상태(예: Doctor_SelfHeal). 반복 모션(예: 돌진 달리기)은 상태 배속대로 계속 돌고,
    ///   한 번짜리 모션은 시전 진행도에 맞춰 재생 위치를 정한다.
    /// - 도중에 막혀 넘어짐(Recovering): &lt;스킬 ID&gt;_End 상태(예: Forward_Charge_End = Fall_1)를 넘어짐 진행도에 맞춘다.
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
        private int playing; // 지금 재생 중인 상태(반복 모션은 한 번만 시작한다)

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
                layer = -1; weight = 0f; playing = 0;
            }
            if (skill == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer < 0) return;

            int state = 0;
            float progress = 0f;
            if (skill.State == SkillState.Casting)
            {
                state = Animator.StringToHash(skill.SkillId);
                progress = skill.CastProgress;
            }
            else if (skill.State == SkillState.Recovering)
            {
                state = Animator.StringToHash(skill.SkillId + "_End");
                progress = skill.RecoveryProgress;
            }
            bool active = state != 0 && animator.HasState(layer, state);

            float target = active ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / (target > weight ? fadeIn : fadeOut));
            animator.SetLayerWeight(layer, weight);
            if (!active) { playing = 0; return; }

            bool looping = skill.State == SkillState.Casting && IsLooping(state);
            if (looping)
            {
                if (playing != state) animator.CrossFadeInFixedTime(state, 0.1f, layer);
            }
            else
            {
                animator.Play(state, layer, Mathf.Min(progress, 0.999f));
            }
            playing = state;
        }

        private bool IsLooping(int state)
        {
            // 지금 그 상태가 재생 중이면 클립 정보로, 아니면 컨트롤러의 클립 목록으로 판단한다.
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash == state) return info.loop;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && Animator.StringToHash(clip.name.StartsWith("Skill_") ? clip.name.Substring(6) : clip.name) == state)
                    return clip.isLooping;
            return false;
        }
    }
}

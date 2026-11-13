using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 고유 스킬 상태(ICharacterSkillState, BE)를 Animator "Skill" 레이어(전신)로 보여 준다.
    /// - 재생할 상태는 스킬이 정한다(AnimationState: 예 Doctor_SelfHeal, Forward_Charge_End, Luchino_Leap_Slam).
    /// - SyncAnimationToProgress면 재생 위치를 진행도에 맞추고(주사기 꽂기), 아니면 상태가 바뀔 때 한 번 시작해 그냥 재생한다
    ///   (돌진 달리기 반복, 도약 → 내려찍기).
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
        [Tooltip("상태가 바뀔 때 섞는 시간(초).")]
        [SerializeField, Min(0f)] private float crossFade = 0.08f;

        // 동작 구간(ICharacterSkillState.MotionPhase)을 알리는 스킬: 상태 이름 → (준비가 끝나는 지점, 판정이 끝나는 지점), 모션 전체 0~1 기준.
        // 두 값이 같으면 판정(Active) 동안 그 자세로 멈춘다.
        private static readonly System.Collections.Generic.Dictionary<string, Vector2> MotionTiming = new()
        {
            // 사냥터지기 그랩 쏘기(Skill_Gamekeeper_Grab, Tools/Blender/make_grab_throw.py): 몸을 앞으로 기울이며 오른팔을 어깨 높이로 앞으로 뻗어 던진 자세(12/31프레임 = 37%)에서 멈추고
            // 그랩이 날아갔다 돌아오면 팔을 거둬 서 있는 자세로 돌아온다.
            ["Gamekeeper_Grab"] = new Vector2(11f / 30f, 11f / 30f),
        };

        private ICharacterSkillState skill;
        private Animator animator;
        private int layer = -1;
        private float weight;
        private int playing; // 지금 재생 중인 상태(그냥 재생하는 모션은 한 번만 시작한다)

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

            string name = skill.AnimationState;
            int state = string.IsNullOrEmpty(name) ? 0 : Animator.StringToHash(name);
            bool active = state != 0 && animator.HasState(layer, state);

            float target = active ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / (target > weight ? fadeIn : fadeOut));
            animator.SetLayerWeight(layer, weight);
            if (!active) { playing = 0; return; }

            if (skill.MotionPhase != AttackPhase.None && MotionTiming.TryGetValue(name, out var timing))
            {
                float p = skill.MotionPhaseProgress;
                float at = skill.MotionPhase switch
                {
                    AttackPhase.Windup => Mathf.Lerp(0f, timing.x, p),
                    AttackPhase.Active => Mathf.Lerp(timing.x, timing.y, p),
                    _ => Mathf.Lerp(timing.y, 0.999f, p),
                };
                animator.Play(state, layer, at);
            }
            else if (skill.SyncAnimationToProgress)
            {
                float progress = skill.State == SkillState.Recovering ? skill.RecoveryProgress : skill.CastProgress;
                animator.Play(state, layer, Mathf.Min(progress, 0.999f));
            }
            else if (playing != state)
            {
                animator.CrossFadeInFixedTime(state, crossFade, layer);
            }
            playing = state;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공격 상태(ICharacterAttackState, BE)를 Animator의 상체 공격 레이어로 보여 준다.
    /// 약공격·강공격은 BE의 구간(준비/판정/후딜레이) 진행도에 맞춰 재생 위치를 매 프레임 맞춘다.
    /// 그래서 구간 길이가 바뀌어도 손이 맞는 순간이 항상 판정 구간 안에 온다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAttackAnimator : MonoBehaviour
    {
        private const string LayerName = "Attack";
        // 모션을 만들 때 쓴 구간 비율(ProceduralMotionBuilder와 같아야 한다)
        private const float LightWindupEnd = 0.1f / 0.45f, LightActiveEnd = 0.2f / 0.45f;
        private const float HeavyWindupEnd = 0.25f / 0.9f, HeavyActiveEnd = 0.4f / 0.9f;

        [Tooltip("공격 레이어가 켜지는/꺼지는 속도(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.06f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.15f;

        private static readonly int LightId = Animator.StringToHash("Light");
        private static readonly int ChargeId = Animator.StringToHash("HeavyCharge");
        private static readonly int HeavyId = Animator.StringToHash("Heavy");

        private ICharacterAttackState attack;
        private Animator animator;
        private int layer = -1;
        private float weight;
        private CharacterAttackState shown = CharacterAttackState.None;

        private void Awake()
        {
            attack = GetComponent<ICharacterAttackState>();
            animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (attack == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer < 0) return;

            var state = attack.AttackState;
            float target = state == CharacterAttackState.None ? 0f : 1f;
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / (target > weight ? fadeIn : fadeOut));
            animator.SetLayerWeight(layer, weight);

            switch (state)
            {
                case CharacterAttackState.Light:
                    animator.Play(LightId, layer, Normalized(LightWindupEnd, LightActiveEnd));
                    break;
                case CharacterAttackState.Heavy:
                    animator.Play(HeavyId, layer, Normalized(HeavyWindupEnd, HeavyActiveEnd));
                    break;
                case CharacterAttackState.HeavyCharge:
                    if (shown != CharacterAttackState.HeavyCharge) animator.CrossFadeInFixedTime(ChargeId, 0.12f, layer);
                    break;
            }
            shown = state;
        }

        /// <summary>현재 구간과 진행도를 모션 전체(0~1) 위치로 바꾼다.</summary>
        private float Normalized(float windupEnd, float activeEnd)
        {
            float p = Mathf.Clamp01(attack.PhaseProgress);
            switch (attack.AttackPhase)
            {
                case AttackPhase.Windup: return Mathf.Lerp(0f, windupEnd, p);
                case AttackPhase.Active: return Mathf.Lerp(windupEnd, activeEnd, p);
                case AttackPhase.Recovery: return Mathf.Lerp(activeEnd, 0.999f, p);
                default: return 0f;
            }
        }
    }
}

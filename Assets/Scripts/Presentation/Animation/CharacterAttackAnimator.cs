using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공격 상태(ICharacterAttackState, BE)를 Animator 공격 레이어로 보여 준다.
    /// - 땅: 상체 레이어(Attack) — 약공격 찌르기 / 강공격 모으기 / 강공격 내려치기
    /// - 공중(IsAirAttack): 전신 레이어(AirAttack) — 공중 모으기 / 발 내려찍기(약·강 공통)
    /// 약·강공격은 BE 구간(준비/판정/후딜레이) 진행도에 맞춰 재생 위치를 매 프레임 맞춘다.
    /// 그래서 구간 길이가 바뀌어도 손·발이 맞는 순간이 항상 판정 구간 안에 온다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAttackAnimator : MonoBehaviour
    {
        private const string GroundLayerName = "Attack";
        private const string AirLayerName = "AirAttack";
        // 모션을 만들 때 쓴 구간 비율(ProceduralMotionBuilder와 같아야 한다)
        private const float LightWindupEnd = 0.1f / 0.45f, LightActiveEnd = 0.2f / 0.45f;
        private const float HeavyWindupEnd = 0.25f / 0.9f, HeavyActiveEnd = 0.4f / 0.9f;
        private const float StompWindupEnd = 0.25f, StompActiveEnd = 0.45f;

        [Tooltip("공격 레이어가 켜지는/꺼지는 속도(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.06f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.15f;

        private static readonly int LightId = Animator.StringToHash("Light");
        private static readonly int ChargeId = Animator.StringToHash("HeavyCharge");
        private static readonly int HeavyId = Animator.StringToHash("Heavy");
        private static readonly int StompId = Animator.StringToHash("AirStomp");
        private static readonly int AirChargeId = Animator.StringToHash("AirCharge");

        private ICharacterAttackState attack;
        private Animator animator;
        private int groundLayer = -1, airLayer = -1;
        private float groundWeight, airWeight;
        private CharacterAttackState shown = CharacterAttackState.None;
        private bool shownAir;

        private void Awake()
        {
            attack = GetComponent<ICharacterAttackState>();
            animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (attack == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (groundLayer < 0) groundLayer = animator.GetLayerIndex(GroundLayerName);
            if (airLayer < 0) airLayer = animator.GetLayerIndex(AirLayerName);
            if (groundLayer < 0) return;

            var state = attack.AttackState;
            bool air = attack.IsAirAttack && airLayer >= 0;
            bool active = state != CharacterAttackState.None;
            groundWeight = Fade(groundWeight, active && !air);
            airWeight = Fade(airWeight, active && air);
            animator.SetLayerWeight(groundLayer, groundWeight);
            if (airLayer >= 0) animator.SetLayerWeight(airLayer, airWeight);

            if (air)
            {
                switch (state)
                {
                    case CharacterAttackState.Light:
                    case CharacterAttackState.Heavy:
                        animator.Play(StompId, airLayer, Normalized(StompWindupEnd, StompActiveEnd));
                        break;
                    case CharacterAttackState.HeavyCharge:
                        if (shown != state || !shownAir) animator.CrossFadeInFixedTime(AirChargeId, 0.1f, airLayer);
                        break;
                }
            }
            else
            {
                switch (state)
                {
                    case CharacterAttackState.Light:
                        animator.Play(LightId, groundLayer, Normalized(LightWindupEnd, LightActiveEnd));
                        break;
                    case CharacterAttackState.Heavy:
                        animator.Play(HeavyId, groundLayer, Normalized(HeavyWindupEnd, HeavyActiveEnd));
                        break;
                    case CharacterAttackState.HeavyCharge:
                        if (shown != state || shownAir) animator.CrossFadeInFixedTime(ChargeId, 0.12f, groundLayer);
                        break;
                }
            }
            shown = state; shownAir = air;
        }

        private float Fade(float w, bool on)
        {
            float target = on ? 1f : 0f;
            return Mathf.MoveTowards(w, target, Time.deltaTime / (target > w ? fadeIn : fadeOut));
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

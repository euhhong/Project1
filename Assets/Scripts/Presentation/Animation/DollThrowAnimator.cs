using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 인형 던지기 상체 모션(연출 전용 — IDollThrow를 읽는다). 모션: Throw(Mixamo/Skill_HellEmber_Throw.fbx, 2.2초) — SkillUpper 레이어 상태 HellEmber_Throw(상체만, 다리는 걷기·달리기 그대로).
    /// - 좌클릭 차징(Aiming)을 시작하면 앞부분(팔을 뒤로 젖혀 드는 데까지, 0 ~ windupAt)을 windupTime초에 재생하고 그 자세로 멈춰 들고 다닌다.
    /// - 던지면(ThrowCount가 늘어남) 그 자리부터 끝까지 원래 빠르기로 마저 재생하고, 끝 무렵 부드럽게 놓는다.
    /// - 차징을 취소하면(던지지 않고 Aiming이 꺼짐) 바로 놓는다. 공격 중에는 하지 않는다.
    /// 스킬 모션 담당(CharacterSkillAnimator)이 정한 레이어 무게를 이 동안만 덮어쓰도록 늦게 돈다. 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만).
    /// </summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public class DollThrowAnimator : MonoBehaviour
    {
        private const string LayerName = "SkillUpper";
        private static readonly int StateId = Animator.StringToHash("HellEmber_Throw");

        [Tooltip("팔을 뒤로 젖혀 든 자세(전체 0~1) — Throw 21프레임/66. 차징 동안 여기서 멈춘다.")]
        [SerializeField, Range(0f, 1f)] private float windupAt = 0.32f;
        [Tooltip("차징을 시작해 그 자세까지 가는 시간(초).")]
        [SerializeField, Min(0.05f)] private float windupTime = 0.3f;
        [Tooltip("던진 뒤 나머지(windupAt ~ 끝)를 재생하는 빠르기 배율 / 끝에서 놓기 시작하는 진행도(0~1).")]
        [SerializeField, Min(0.1f)] private float releaseSpeed = 1.2f;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.75f;
        [Tooltip("자세를 켜고 끄는 시간(초).")]
        [SerializeField, Min(0.01f)] private float blend = 0.12f;

        private IDollThrow doll;
        private ICharacterAttackState attack;
        private Animator animator;
        private int layer = -1;
        private int seenThrows = -1;
        private float aimTime, releaseTime = -1f, weight;
        private bool wasAiming;
        private const float ClipLength = 2.2f;

        private void LateUpdate()
        {
            doll ??= GetComponent<IDollThrow>();
            attack ??= GetComponent<ICharacterAttackState>();
            if (doll == null) return;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = animator != null ? animator.GetLayerIndex(LayerName) : -1;
            }
            if (animator == null || layer < 0 || !animator.HasState(layer, StateId)) return;

            if (seenThrows < 0) seenThrows = doll.ThrowCount;
            if (doll.ThrowCount != seenThrows) { seenThrows = doll.ThrowCount; releaseTime = 0f; } // 던졌다: 나머지 재생
            bool aiming = doll.Aiming;
            if (aiming && !wasAiming) aimTime = 0f;
            wasAiming = aiming;
            bool attacking = attack != null && attack.AttackState != CharacterAttackState.None;

            float at; float target;
            if (releaseTime >= 0f)
            {
                releaseTime += Time.deltaTime * releaseSpeed;
                float u = Mathf.Clamp01(releaseTime / (ClipLength * (1f - windupAt)));
                at = Mathf.Lerp(windupAt, 0.999f, u);
                target = u < fadeFrom ? 1f : Mathf.InverseLerp(1f, fadeFrom, u);
                if (u >= 1f) releaseTime = -1f;
            }
            else if (aiming)
            {
                aimTime += Time.deltaTime;
                at = Mathf.Lerp(0f, windupAt, Mathf.SmoothStep(0f, 1f, aimTime / windupTime)); // 앞부분 → 젖힌 자세에서 멈춤
                target = 1f;
            }
            else { at = windupAt; target = 0f; }
            if (attacking) target = 0f;

            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / blend);
            if (weight <= 0f) return;
            animator.Play(StateId, layer, at);
            animator.SetLayerWeight(layer, weight);
        }
    }
}

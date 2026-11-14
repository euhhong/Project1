using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 인형 회수 모션(연출 전용 — IDollThrow.RetrieveCount가 늘면). 평타로 인형을 맞힌 순간 평타 모션에서 Taking Item(Mixamo/Skill_HellEmber_TakeItem.fbx — 손을 뻗어 집는 데부터,
    /// 40~112프레임)으로 넘어가 집어 들고 일어서며 마무리한다: Skill 레이어(전신) 상태 HellEmber_TakeItem을 playTime초에 재생하고, 그동안 평타(Attack 레이어)는 빠르게 덮는다.
    /// BE(DollThrow)가 같은 시간(retrieveTime) 동안 움직임·공격·스킬을 막는다. 스킬·공격 모션 담당이 정한 레이어 무게를 이 동안만 덮어쓰도록 늦게 돈다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만).
    /// </summary>
    [DefaultExecutionOrder(910)]
    [DisallowMultipleComponent]
    public class DollRetrieveAnimator : MonoBehaviour
    {
        private const string SkillLayerName = "Skill", AttackLayerName = "Attack";
        private static readonly int StateId = Animator.StringToHash("HellEmber_TakeItem");

        [Tooltip("집어 드는 모션 시간(초) — BE DollThrow.retrieveTime과 같게(클립 2.4초를 약 1.6배 빠르게).")]
        [SerializeField, Min(0.1f)] private float playTime = 1.5f;
        [Tooltip("평타에서 넘어가는 시간(초) / 끝에서 놓기 시작하는 진행도.")]
        [SerializeField, Min(0.01f)] private float blendIn = 0.12f;
        [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.85f;

        private IDollThrow doll;
        private Animator animator;
        private int skillLayer = -1, attackLayer = -1;
        private int seen = -1;
        private float time = -1f;

        private void LateUpdate()
        {
            doll ??= GetComponent<IDollThrow>();
            if (doll == null) return;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                skillLayer = animator != null ? animator.GetLayerIndex(SkillLayerName) : -1;
                attackLayer = animator != null ? animator.GetLayerIndex(AttackLayerName) : -1;
            }
            if (seen < 0) seen = doll.RetrieveCount;
            if (doll.RetrieveCount != seen) { seen = doll.RetrieveCount; time = 0f; } // 인형을 맞혀 회수: 집어 드는 모션 시작
            if (time < 0f || animator == null || skillLayer < 0 || !animator.HasState(skillLayer, StateId)) return;

            time += Time.deltaTime;
            float p = time / playTime;
            if (p >= 1f) { time = -1f; return; }
            float w = Mathf.Min(time / blendIn, p < fadeFrom ? 1f : Mathf.InverseLerp(1f, fadeFrom, p));
            animator.Play(StateId, skillLayer, p);
            animator.SetLayerWeight(skillLayer, w);
            if (attackLayer >= 0) animator.SetLayerWeight(attackLayer, animator.GetLayerWeight(attackLayer) * (1f - w)); // 평타는 빠르게 덮는다
        }
    }
}

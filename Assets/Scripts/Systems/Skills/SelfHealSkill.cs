using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 자가치료(의사): 주사기를 스스로에게 꽂아, 시전을 마치면 최대 체력의 일부를 회복한다.
    /// 체력이 가득 차 있으면 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Self Heal")]
    public class SelfHealSkill : SkillDefinition
    {
        [Tooltip("회복량(최대 체력 대비 비율).")]
        [Range(0f, 1f)] public float healFraction = 0.4f;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var health = owner.GetComponent<CharacterHealth>();
            if (health == null) { reason = "체력 없음"; return false; }
            if (health.IsFull) { reason = "체력이 가득 참"; return false; }
            reason = null;
            return true;
        }

        public override void Complete(GameObject owner)
        {
            var health = owner.GetComponent<CharacterHealth>();
            if (health != null) health.Heal(health.Max * healFraction);
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 판자로 가거나 도주하는 덮어쓰기(AiPalletUser·AiRetreat)가 근접 AI에게 잠깐 손을 넘길지: 내 스킬 재사용 대기가 끝났고 상대가 가까워
    /// 스킬각이 나왔으면 그 틱은 덮어쓰지 않는다 — 근접 AI(AiMeleeTactics)가 가까우면 반드시 스킬부터 쓴다.
    /// 자가치료(제자리)·켜 두는 스킬(용병 아대)은 해당 없음.
    /// </summary>
    public static class AiSkillYield
    {
        public const float CloseRange = 5f;

        public static bool Now(Transform self, float hunterDist)
        {
            if (!self.TryGetComponent(out ICharacterSkillState skill) || string.IsNullOrEmpty(skill.SkillId)) return false;
            if (skill.State != SkillState.Ready || !skill.CanUse) return false;
            var prof = AiSkillProfile.For(skill.SkillId);
            if (prof.SelfHeal || prof.Toggle) return false;
            if (prof.Charge) return hunterDist < 8f; // 돌진형: 유인·돌진(반대쪽을 보고 시작해 꺾어 옴)
            return hunterDist <= Mathf.Min(prof.MaxRange, CloseRange) && hunterDist >= prof.MinRange;
        }
    }
}

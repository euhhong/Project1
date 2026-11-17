using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 평타 봉인(BE): 걸린 동안 이 캐릭터는 평타(좌클릭 — 약·강공격·후리기)를 시작하지 못한다(CharacterAttack이 본다). 이동·점프·스킬은 된다.
    /// 예: 맹인에게 밀려 벽에 박히면 4초, 휘두르기에 이어 빠르게 쓴 찌르기로 박히면 9초(CaneKnockback·CaneThrustSkill).
    /// 맞은 캐릭터에 그때 붙는다(Apply). HUD 오른쪽 아래에 줄어드는 게이지("평타 봉인").
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackSeal : MonoBehaviour, ICooldownGauge
    {
        private float until = -1f, length = 1f;

        public bool Sealed => Time.time < until;

        /// <summary>target의 평타를 seconds초 봉인한다(이미 더 길게 걸려 있으면 그대로).</summary>
        public static void Apply(GameObject target, float seconds)
        {
            if (target == null || seconds <= 0f) return;
            if (!target.TryGetComponent(out AttackSeal s)) s = target.AddComponent<AttackSeal>();
            if (Time.time + seconds <= s.until) return;
            s.until = Time.time + seconds;
            s.length = seconds;
        }

        public bool TryGetCooldown(out string label, out float fill, out bool draining)
        {
            label = "평타 봉인"; draining = true;
            fill = Sealed ? Mathf.Clamp01((until - Time.time) / Mathf.Max(length, 0.01f)) : 0f;
            return Sealed;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 그랩으로 끌어온 상대를 바로 때리면 더 아프게(IHitModifier — 때린 쪽 캐릭터 루트). 예: 사냥터지기 그랩은 데미지가 없고, 끌어오기를 마친 뒤
    /// window초 안에 그 상대에게 들어간 타격은 피해 × scale(HitResult.Power). GrabShotSkill이 끌어오기를 마칠 때 Mark로 건다(없으면 붙인다).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabFollowUpBonus : MonoBehaviour, IHitModifier
    {
        private int victimId;
        private float until = -1f, scale = 1f;

        public static void Mark(GameObject attacker, int victimId, float seconds, float damageScale)
        {
            if (attacker == null || seconds <= 0f || damageScale <= 1f) return;
            if (!attacker.TryGetComponent(out GrabFollowUpBonus b)) b = attacker.AddComponent<GrabFollowUpBonus>();
            b.victimId = victimId; b.until = Time.time + seconds; b.scale = damageScale;
        }

        public void ModifyHit(ref HitResult hit, GameObject victim)
        {
            if (Time.time > until || hit.VictimId != victimId || hit.NoDamage) return;
            hit.Power = hit.PowerScale * scale; // 끌어온 직후 타격: 피해 증가
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 임시 데미지 규칙(게임 시작 모드에서만 켠다): 맞으면 맞은 캐릭터 체력이 baseDamage × 세기(HitResult.PowerScale)만큼 준다.
    /// 데미지 없는 공격(HitResult.NoDamage, 예: 맹인 지팡이 넉백)은 건너뛴다. 정식 규칙(부위 배율 등)은 아직 정하지 않았다(PM).
    /// </summary>
    public static class BattleDamage
    {
        public static bool Enabled { get; private set; }
        public static float BaseDamage { get; private set; } = 10f;

        public static void Enable(float baseDamage)
        {
            BaseDamage = baseDamage;
            if (Enabled) return;
            Enabled = true;
            HitEvents.HitOccurred += OnHit;
        }

        public static void Disable()
        {
            if (!Enabled) return;
            Enabled = false;
            HitEvents.HitOccurred -= OnHit;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() { Enabled = false; HitEvents.HitOccurred -= OnHit; }

        private static void OnHit(HitResult hit)
        {
            if (hit.NoDamage) return;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c.Id != hit.VictimId) continue;
                if (c.TryGetComponent(out CharacterHealth health)) health.Damage(BaseDamage * hit.PowerScale);
                return;
            }
        }
    }
}

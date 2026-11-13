using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 데미지 규칙(게임 시작 모드에서만 켠다): 맞으면 맞은 캐릭터 체력이 DamageRules.Damage만큼 준다
    /// (공격 종류·때린 쪽 분류(감시자/생존자)별 표 × 부위 배율 × 세기 × 등 뒤 배율 — Docs/DamageSystem.md).
    /// 데미지 없는 공격(HitResult.NoDamage, 예: 맹인 지팡이 넉백)은 건너뛴다. Enable의 baseDamage는 예전 호환용(쓰지 않음).
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
            var victim = DamageRules.Find(hit.VictimId);
            if (victim == null || !victim.TryGetComponent(out CharacterHealth health)) return;
            var attacker = DamageRules.Find(hit.AttackerId);
            health.Damage(DamageRules.Damage(hit, attacker != null ? attacker.gameObject : null, victim.gameObject));
        }
    }
}

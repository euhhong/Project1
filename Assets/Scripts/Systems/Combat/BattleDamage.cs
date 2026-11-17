using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 데미지 규칙(게임 시작 모드에서만 켠다): 맞으면 맞은 캐릭터 체력이 DamageRules.Damage만큼 준다
    /// (공격 종류·때린 쪽 분류(감시자/생존자)별 표 × 부위 배율 × 세기 × 등 뒤 배율 — Docs/DamageSystem.md).
    /// 데미지 없는 공격(HitResult.NoDamage, 예: 맹인 지팡이 넉백)은 건너뛴다. Enable의 baseDamage는 예전 호환용(쓰지 않음).
    /// 평타 연타 줄이기: 같은 상대에게 평타 피해를 넣고 2초 안에 또 평타로 넣으면 직전 피해의 50%까지만(FollowUpDecay).
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
        private static void ResetOnPlay() { Enabled = false; HitEvents.HitOccurred -= OnHit; lastBasic.Clear(); }

        private static void OnHit(HitResult hit)
        {
            if (hit.NoDamage) return;
            var victim = DamageRules.Find(hit.VictimId);
            if (victim == null || !victim.TryGetComponent(out CharacterHealth health)) return;
            var attacker = DamageRules.Find(hit.AttackerId);
            float damage = DamageRules.Damage(hit, attacker != null ? attacker.gameObject : null, victim.gameObject);
            health.Damage(FollowUpDecay(hit, damage));
        }

        // ── 평타 연타 줄이기: 같은 상대에게 평타 피해를 넣은 뒤 FollowUpWindow초 안에 또 평타로 피해를 넣으면 직전 피해의 절반(FollowUpScale)까지만 ──
        public const float FollowUpWindow = 2f, FollowUpScale = 0.5f;
        private static readonly System.Collections.Generic.Dictionary<(int attacker, int victim), (float time, float damage)> lastBasic = new();

        /// <summary>
        /// 평타(Kind ≠ Skill) 피해: 같은 때린 쪽 → 같은 맞은 쪽으로 직전 평타 피해가 FollowUpWindow초 안이면 직전 피해 × FollowUpScale을 넘지 않게(연달아 맞을수록 절반씩).
        /// 스킬·투사체·판자 등은 해당 없음. 피해가 실제로 들어갈 때만 부른다(기록을 남김).
        /// </summary>
        private static float FollowUpDecay(in HitResult hit, float damage)
        {
            if (hit.Kind == AttackKind.Skill || damage <= 0f) return damage;
            var key = (hit.AttackerId, hit.VictimId);
            float now = Time.time;
            if (lastBasic.TryGetValue(key, out var prev) && now - prev.time <= FollowUpWindow)
                damage = Mathf.Min(damage, prev.damage * FollowUpScale);
            lastBasic[key] = (now, damage);
            return damage;
        }
    }
}

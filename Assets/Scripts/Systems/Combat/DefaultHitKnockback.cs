using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 기본 피격 넉백(BE): 맞으면(HitEvents) 맞은 캐릭터가 때린 쪽에서 멀어지는 방향으로 밀려난다. 세기는 규칙 표(DamageRules)를 따른다.
    /// - 평타: 때린 쪽 분류·공격 종류별 속도·시간(감시자 = 작게, 생존자 = 크게). 생존자 평타 넉백은 벽꿍(벽·캐릭터에 박히면 피해 + 추가 경직).
    /// - 스킬: 기본 6m/s × 0.25초(약 0.75m) × 공격 세기, 벽 피해 없음.
    /// - 이미 넉백을 정한 공격(HitResult.Knockback ≠ 0, 예: 맹인 지팡이)은 건너뛴다. 기절은 걸지 않는다(경직은 HitStunRules).
    /// 플레이를 시작하면 저절로 켜진다(붙일 것 없음). 끄려면 Enabled = false.
    /// </summary>
    public static class DefaultHitKnockback
    {
        public static bool Enabled = true;
        public static float MinPower = 0.5f, MaxPower = 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => HitEvents.HitOccurred -= OnHit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            HitEvents.HitOccurred -= OnHit;
            HitEvents.HitOccurred += OnHit;
        }

        private static void OnHit(HitResult hit)
        {
            if (!Enabled || hit.Knockback.sqrMagnitude > 1e-6f || hit.VictimId == hit.AttackerId) return;
            var victim = DamageRules.Find(hit.VictimId);
            if (victim == null) return;
            var attacker = DamageRules.Find(hit.AttackerId);

            // 때린 쪽 → 맞은 쪽(수평). 때린 쪽을 모르면 맞은 자리 → 몸 가운데, 그것도 없으면 맞은 쪽 뒤로.
            Vector3 from = attacker != null ? attacker.transform.position : hit.Point;
            Vector3 dir = Vector3.ProjectOnPlane(victim.transform.position - from, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = -victim.transform.forward;
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;

            float power = Mathf.Clamp(hit.PowerScale, MinPower, MaxPower);
            bool hunter = DamageRules.IsHunter(attacker != null ? attacker.gameObject : null);
            if (DamageRules.TryGetBasic(hunter, hit.Kind, out var spec))
            {
                Knockback.Push(victim.gameObject, dir * (spec.KnockSpeed * power), spec.KnockTime, stun: false,
                    impactDamage: spec.WallSlam, impactPerSpeed: DamageRules.WallSlamDamagePerSpeed, impactStun: spec.WallSlam ? DamageRules.WallSlamStun : 0f);
            }
            else
            {
                Knockback.Push(victim.gameObject, dir * (DamageRules.SkillKnockSpeed * power), DamageRules.SkillKnockTime, stun: false, impactDamage: false);
            }
        }
    }
}

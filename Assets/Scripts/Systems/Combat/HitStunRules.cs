using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 평타 경직·패링 반격 규칙(BE). 값은 DamageRules 표.
    /// - 평타에 맞으면 맞은 쪽이 표의 시간만큼 경직(CharacterStun — 이동·공격·스킬 못 함). 감시자 평타는 짧게, 생존자 평타는 길게(경직·넉백 위주).
    /// - 생존자 평타가 같은 상대를 경직시킨 뒤 SurvivorRestunWindow(1초) 동안은 다시 경직을 걸지 못한다(넉백·피해는 들어감 — 스턴락 방지).
    /// - 주먹 막기(패링)에 막힌 공격: 때린 쪽이 ParryCounterStun(0.5초) 경직(반격 기회).
    /// - 스킬의 경직(루키노 내려찍기·돌진·그랩 등)은 각 스킬이 건다(여기서는 건드리지 않음).
    /// 플레이를 시작하면 저절로 켜진다(붙일 것 없음). 끄려면 Enabled = false.
    /// </summary>
    public static class HitStunRules
    {
        public static bool Enabled = true;
        private static readonly Dictionary<int, float> LastSurvivorStun = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            HitEvents.HitOccurred -= OnHit;
            HitEvents.HitBlocked -= OnBlocked;
            LastSurvivorStun.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            HitEvents.HitOccurred -= OnHit; HitEvents.HitOccurred += OnHit;
            HitEvents.HitBlocked -= OnBlocked; HitEvents.HitBlocked += OnBlocked;
        }

        private static void OnHit(HitResult hit)
        {
            if (!Enabled || hit.VictimId == hit.AttackerId) return;
            var attacker = DamageRules.Find(hit.AttackerId);
            bool hunter = DamageRules.IsHunter(attacker != null ? attacker.gameObject : null);
            if (!DamageRules.TryGetBasic(hunter, hit.Kind, out var spec) || spec.Stun <= 0f) return; // 스킬은 스킬이 건다
            var victim = DamageRules.Find(hit.VictimId);
            if (victim == null) return;

            if (!hunter)
            {
                if (LastSurvivorStun.TryGetValue(victim.Id, out float t) && Time.time - t < DamageRules.SurvivorRestunWindow) return;
                LastSurvivorStun[victim.Id] = Time.time;
            }
            Vector3 dir = attacker != null ? victim.transform.position - attacker.transform.position : Vector3.zero;
            CharacterStun.Apply(victim.gameObject, spec.Stun, dir);
        }

        private static void OnBlocked(HitResult hit)
        {
            if (!Enabled) return;
            var attacker = DamageRules.Find(hit.AttackerId);
            var victim = DamageRules.Find(hit.VictimId);
            if (attacker == null) return;
            Vector3 dir = victim != null ? attacker.transform.position - victim.transform.position : Vector3.zero;
            CharacterStun.Apply(attacker.gameObject, DamageRules.ParryCounterStun, dir);
        }
    }
}

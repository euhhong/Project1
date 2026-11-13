using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 기본 피격 넉백(BE): 맞으면(HitEvents) 맞은 캐릭터가 때린 쪽에서 멀어지는 방향으로 조금 밀려난다.
    /// - 이미 넉백을 정한 공격(HitResult.Knockback ≠ 0, 예: 맹인 지팡이)은 그대로 두고 건너뛴다.
    /// - 세기 = speed × 공격 세기(PowerScale, minPower~maxPower로 자름), duration초 동안 0까지 줄어든다(거리 ≈ 속도 × 시간 ÷ 2, 기본 약 0.75m).
    /// - 기절 없음, 벽에 부딪혀도 데미지 없음(데미지 규칙은 그대로). 벽에 막히면 거기서 멈춘다.
    /// 플레이를 시작하면 저절로 켜진다(붙일 것 없음). 끄려면 Enabled = false.
    /// </summary>
    public static class DefaultHitKnockback
    {
        public static bool Enabled = true;
        /// <summary>밀어내는 처음 속도(m/s).</summary>
        public static float Speed = 6f;
        /// <summary>밀려나는 시간(초).</summary>
        public static float Duration = 0.25f;
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
            CharacterIdentity victim = null, attacker = null;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c.Id == hit.VictimId) victim = c;
                else if (c.Id == hit.AttackerId) attacker = c;
            }
            if (victim == null) return;

            // 때린 쪽 → 맞은 쪽(수평). 때린 쪽을 모르면 맞은 자리 → 몸 가운데, 그것도 없으면 맞은 쪽 뒤로.
            Vector3 from = attacker != null ? attacker.transform.position : hit.Point;
            Vector3 dir = Vector3.ProjectOnPlane(victim.transform.position - from, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) dir = -victim.transform.forward;
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;

            float power = Mathf.Clamp(hit.PowerScale > 0f ? hit.PowerScale : 1f, MinPower, MaxPower);
            Knockback.Push(victim.gameObject, dir * (Speed * power), Duration, stun: false, impactDamage: false);
        }
    }
}

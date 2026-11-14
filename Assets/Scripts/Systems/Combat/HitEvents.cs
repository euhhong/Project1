using System;
using Project1.Core;

namespace Project1.Systems
{
    /// <summary>
    /// 히트 결과를 받는 전역 채널. 데미지·체력(M3-5), 연출(FE), 네트워크 전송이 여기를 구독한다.
    /// 판정을 내는 쪽(AttackHitDetector)만 Raise한다.
    /// </summary>
    public static class HitEvents
    {
        public static event Action<HitResult> HitOccurred;

        /// <summary>맞는 쪽이 막아 낸 히트(IHitBlocker, 예: 주먹 막기). 이때 HitOccurred는 나가지 않는다. 막기 이펙트(FE)가 구독한다.</summary>
        public static event Action<HitResult> HitBlocked;

        // 공격에 면역인 캐릭터 ID(예: 공장장 분신 — HitImmunity). 이 캐릭터가 맞은 히트는 내지 않는다(데미지·경직·넉백·피격 모션·막기 이펙트 없음).
        private static readonly System.Collections.Generic.HashSet<int> immune = new();

        /// <summary>victimId 캐릭터를 공격 면역으로(on) / 되돌림.</summary>
        public static void SetImmune(int victimId, bool on) { if (on) immune.Add(victimId); else immune.Remove(victimId); }
        public static bool IsImmune(int victimId) => immune.Contains(victimId);

        public static void Raise(in HitResult hit)
        {
            if (immune.Contains(hit.VictimId)) return;
            HitOccurred?.Invoke(hit);
        }

        public static void RaiseBlocked(in HitResult hit)
        {
            if (immune.Contains(hit.VictimId)) return;
            HitBlocked?.Invoke(hit);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers()
        {
            HitOccurred = null;
            HitBlocked = null;
            immune.Clear();
        }
    }
}

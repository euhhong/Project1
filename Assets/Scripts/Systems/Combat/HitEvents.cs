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

        public static void Raise(in HitResult hit)
        {
            HitOccurred?.Invoke(hit);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscribers()
        {
            HitOccurred = null;
        }
    }
}

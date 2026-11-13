using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>투사체가 생기고 끝나는 것을 알린다(FE가 연출을 붙인다). 판정은 HitEvents로 따로 알린다.</summary>
    public static class ProjectileEvents
    {
        public static event Action<IProjectile> Launched;
        public static event Action<IProjectile> Ended;

        public static void RaiseLaunched(IProjectile p) => Launched?.Invoke(p);
        public static void RaiseEnded(IProjectile p) => Ended?.Invoke(p);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Launched = null;
            Ended = null;
        }
    }
}

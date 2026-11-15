using System;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 평타가 벽을 정면으로 쳐서 튕겨 나온 순간(AttackHitDetector). 이펙트(FE — 파란 리본 튀기)가 구독한다.
    /// (attacker, 벽에 닿은 자리, 벽 면의 바깥 방향)
    /// </summary>
    public static class WallBounceEvents
    {
        public static event Action<GameObject, Vector3, Vector3> Struck;

        public static void Raise(GameObject attacker, Vector3 point, Vector3 normal) => Struck?.Invoke(attacker, point, normal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Struck = null;
    }
}

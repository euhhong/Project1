using System;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>분신이 생기고 사라지는 것을 알린다(FE가 모델·겉모습·무기를 붙이고, 사라질 때 연출을 낸다).</summary>
    public static class CloneEvents
    {
        /// <summary>(분신 캐릭터 루트, 불러낸 캐릭터). 몸·이동·공격(BE)은 다 붙은 뒤에 부른다 — 모델은 FE가 붙인다.</summary>
        public static event Action<GameObject, CharacterIdentity> Spawned;
        /// <summary>분신이 사라지기 직전(자리·모델이 아직 있음).</summary>
        public static event Action<GameObject> Despawning;

        public static void RaiseSpawned(GameObject clone, CharacterIdentity owner) => Spawned?.Invoke(clone, owner);
        public static void RaiseDespawning(GameObject clone) => Despawning?.Invoke(clone);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Spawned = null;
            Despawning = null;
        }
    }
}

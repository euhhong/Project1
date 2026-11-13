using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>맞은 캐릭터에 붙은 IHitBlocker(예: 주먹 막기)에게 히트를 막을지 묻는다. 히트를 내는 곳(AttackHitDetector)이 히트를 내기 직전에 부른다.</summary>
    public static class HitBlockers
    {
        private static readonly List<IHitBlocker> buffer = new();

        /// <summary>하나라도 막으면 true(그 히트는 내지 않는다).</summary>
        public static bool Blocks(GameObject victim, in HitResult hit, GameObject attacker)
        {
            if (victim == null) return false;
            buffer.Clear();
            victim.GetComponents(buffer);
            foreach (var b in buffer)
                if ((b is not Behaviour beh || beh.isActiveAndEnabled) && b.BlocksHit(hit, attacker)) return true;
            return false;
        }
    }
}

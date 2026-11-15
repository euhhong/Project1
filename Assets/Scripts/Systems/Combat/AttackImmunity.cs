using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>맞는 캐릭터에 붙은 IAttackImmunity에게 이 공격에 면역인지 묻는다. CharacterIdentity.CanHit가 부른다(모든 공격·스킬 판정이 그걸 거친다).</summary>
    public static class AttackImmunity
    {
        private static readonly List<IAttackImmunity> buffer = new();

        /// <summary>하나라도 면역이면 true.</summary>
        public static bool Immune(GameObject victim, GameObject attacker)
        {
            if (victim == null) return false;
            buffer.Clear();
            victim.GetComponents(buffer);
            foreach (var b in buffer)
                if ((b is not Behaviour beh || beh.isActiveAndEnabled) && b.ImmuneTo(attacker)) return true;
            return false;
        }
    }
}

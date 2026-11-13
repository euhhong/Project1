using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>때린 캐릭터에 붙은 IHitModifier(예: 맹인 지팡이 넉백)를 히트 결과에 차례로 적용한다. 히트를 내는 곳(AttackHitDetector, 스킬)이 부른다.</summary>
    public static class HitModifiers
    {
        private static readonly System.Collections.Generic.List<IHitModifier> buffer = new();

        public static void Apply(GameObject attacker, ref HitResult hit, GameObject victim)
        {
            if (attacker == null) return;
            buffer.Clear();
            attacker.GetComponents(buffer);
            foreach (var m in buffer)
                if (m is not Behaviour b || b.isActiveAndEnabled) m.ModifyHit(ref hit, victim);
        }
    }
}

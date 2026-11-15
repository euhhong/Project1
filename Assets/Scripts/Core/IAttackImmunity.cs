using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 맞는 쪽이 지금 공격·스킬에 면역인지(예: 붉은나비가 나비에게 날아가는 동안 — 맹인 밀기만 받는다).
    /// 면역이면 그 공격은 아예 닿지 않은 것으로 본다(데미지·경직·넉백·끌려감·막기 이펙트 없음). 캐릭터 루트에 붙은 것을 모두 본다.
    /// </summary>
    public interface IAttackImmunity
    {
        /// <param name="attacker">공격한 캐릭터 루트(모르면 null).</param>
        bool ImmuneTo(GameObject attacker);
    }
}

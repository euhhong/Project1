using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 캐릭터마다 다른 기본 공격력·방어력(IDamageScale — 늘 걸려 있음). CharacterLoadout이 캐릭터를 입힐 때 Set으로 정한다(인스펙터 연결 없음).
    /// 예: 용병 = 주는 피해 ×1.1, 받는 피해 ×0.9(소폭 상향). 나머지는 1(그대로).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterDamageStats : MonoBehaviour, IDamageScale
    {
        public const float MercenaryAttack = 1.1f, MercenaryDefense = 0.9f;

        [SerializeField, Min(0f)] private float outgoing = 1f, incoming = 1f;

        public float OutgoingDamageScale => outgoing;
        public float IncomingDamageScale => incoming;

        /// <param name="attack">주는 피해 배율(1보다 크면 더 아프게).</param>
        /// <param name="defense">받는 피해 배율(1보다 작으면 덜 아프게).</param>
        public void Set(float attack, float defense) { outgoing = attack; incoming = defense; }

        /// <summary>캐릭터 ID로 정해진 기본 배율.</summary>
        public static (float attack, float defense) For(string id) => id switch
        {
            "Survivor_Mercenary" => (MercenaryAttack, MercenaryDefense),
            _ => (1f, 1f),
        };
    }
}

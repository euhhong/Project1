using System;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>공격 하나의 구간 길이(초)와 켜는 Hitbox 자리. 데미지 같은 수치는 M3-5에서 추가한다.</summary>
    [Serializable]
    public class AttackSpec
    {
        [Min(0f)] public float Windup;
        [Min(0f)] public float Active;
        [Min(0f)] public float Recovery;
        public HitboxSlot Slots;

        public AttackSpec()
        {
        }

        public AttackSpec(float windup, float active, float recovery, HitboxSlot slots)
        {
            Windup = windup;
            Active = active;
            Recovery = recovery;
            Slots = slots;
        }

        public float Duration => Windup + Active + Recovery;
    }
}

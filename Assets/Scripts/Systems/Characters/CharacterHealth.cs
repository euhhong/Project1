using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 캐릭터 체력. 회복(스킬)과 피해를 받는다. 캐릭터 루트에 붙인다.
    /// 히트 판정을 데미지로 바꾸는 규칙(부위 배율 등, M3-5)은 아직 정하지 않았다. 그쪽에서 Damage를 부르면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterHealth : MonoBehaviour, ICharacterHealth
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private float current = -1f;

        public float Max => maxHealth;
        public float Current => current < 0f ? maxHealth : current;
        public bool IsFull => Current >= maxHealth - 0.001f;
        public bool IsDead => Current <= 0f;

        public event Action<float, float> Changed;

        private void Awake()
        {
            if (current < 0f) current = maxHealth;
        }

        public void Heal(float amount) => Set(Current + Mathf.Max(0f, amount));

        public void Damage(float amount) => Set(Current - Mathf.Max(0f, amount));

        public void ResetToFull() => Set(maxHealth);

        private void Set(float value)
        {
            current = Mathf.Clamp(value, 0f, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }
    }
}

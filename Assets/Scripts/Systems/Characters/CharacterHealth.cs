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

        /// <summary>피해를 받는다. 받는 피해 배율(IDamageScale, 예: 카우보이 뛰어넘기 직후 방어)을 곱한다.</summary>
        public void Damage(float amount)
        {
            float before = Current;
            Set(before - Mathf.Max(0f, amount) * TimedCombatBuff.Incoming(gameObject));
            float lost = before - Current;
            if (lost > 0f) recent.Add((Time.time, lost));
            while (recent.Count > 0 && Time.time - recent[0].time > HistorySeconds) recent.RemoveAt(0);
        }

        // 최근에 받은 피해(시각, 양) — 되돌리기(RollBackRecent)용, 오래된 것은 버린다
        private const float HistorySeconds = 3f;
        private readonly System.Collections.Generic.List<(float time, float amount)> recent = new();

        /// <summary>
        /// 직전 seconds초 안에 받은 피해를 되돌린다(그만큼 회복, 되돌린 피해는 다시 되돌리지 않음). 쓰러졌으면 하지 않는다. 되돌린 양을 돌려준다.
        /// 예: 포워드 박치기로 벽에 박혀 기절하면 그 상대가 직전 1초 안에 받은 피해를 되돌림.
        /// </summary>
        public float RollBackRecent(float seconds)
        {
            if (IsDead) return 0f;
            float sum = 0f, now = Time.time;
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                if (now - recent[i].time > seconds) continue;
                sum += recent[i].amount;
                recent.RemoveAt(i);
            }
            if (sum > 0f) Heal(sum);
            return sum;
        }

        public void ResetToFull() => Set(maxHealth);

        /// <summary>체력을 바로 이 값으로(0~최대). 예: 연습 모드 의사는 50에서 시작하고, 가득 차면 다시 50.</summary>
        public void SetCurrent(float value) => Set(value);

        /// <summary>최대 체력을 바꾸고 가득 채운다(캐릭터를 고를 때 — 감시자 150, 생존자 100, DamageRules).</summary>
        public void SetMax(float max)
        {
            maxHealth = Mathf.Max(1f, max);
            Set(maxHealth);
        }

        private void Set(float value)
        {
            current = Mathf.Clamp(value, 0f, maxHealth);
            Changed?.Invoke(current, maxHealth);
        }
    }
}

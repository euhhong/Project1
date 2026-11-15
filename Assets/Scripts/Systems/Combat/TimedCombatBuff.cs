using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 정한 시간 동안 주는 피해를 늘리고 받는 피해를 줄인다(IDamageScale). 예: 카우보이 올가미 뛰어넘기 직후 1초. 화면에 표시하지 않는다.
    /// Give로 건다(없으면 캐릭터 루트에 붙인다). 이미 걸려 있으면 더 센 배율·더 긴 남은 시간을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimedCombatBuff : MonoBehaviour, IDamageScale
    {
        private float outgoing = 1f, incoming = 1f, left;

        public float OutgoingDamageScale => left > 0f ? outgoing : 1f;
        public float IncomingDamageScale => left > 0f ? incoming : 1f;

        /// <param name="outgoing">주는 피해 배율(1보다 크면 더 아프게).</param>
        /// <param name="incoming">받는 피해 배율(1보다 작으면 덜 아프게).</param>
        public static void Give(GameObject target, float outgoing, float incoming, float seconds)
        {
            if (target == null || seconds <= 0f) return;
            if (!target.TryGetComponent(out TimedCombatBuff b)) b = target.AddComponent<TimedCombatBuff>();
            if (b.left <= 0f) { b.outgoing = outgoing; b.incoming = incoming; }
            else { b.outgoing = Mathf.Max(b.outgoing, outgoing); b.incoming = Mathf.Min(b.incoming, incoming); }
            b.left = Mathf.Max(b.left, seconds);
        }

        private void FixedUpdate()
        {
            if (left > 0f) left = Mathf.Max(0f, left - Time.fixedDeltaTime);
        }

        private static readonly List<IDamageScale> buffer = new();

        /// <summary>character에 붙은 모든 배율을 곱한 값(주는 피해 / 받는 피해).</summary>
        public static float Outgoing(GameObject character) => Product(character, true);
        public static float Incoming(GameObject character) => Product(character, false);

        private static float Product(GameObject character, bool outgoingScale)
        {
            if (character == null) return 1f;
            buffer.Clear();
            character.GetComponents(buffer);
            float m = 1f;
            foreach (var s in buffer)
            {
                if (s is Behaviour { isActiveAndEnabled: false }) continue;
                m *= Mathf.Max(0f, outgoingScale ? s.OutgoingDamageScale : s.IncomingDamageScale);
            }
            return m;
        }
    }
}

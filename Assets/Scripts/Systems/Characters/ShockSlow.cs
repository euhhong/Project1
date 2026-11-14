using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 데미지 없는 충격파에 맞아 느려짐(예: 사냥터지기 그랩이 벽에 도착할 때의 충격파). time초 동안 달리기 속도의 ratio배까지만 움직인다(끝 무렵 부드럽게 돌아옴).
    /// 맞으면 Shocked를 알린다(FE가 휘청이는 모션을 띄운다). 캐릭터 루트에 붙는다(Apply가 없으면 붙인다).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShockSlow : MonoBehaviour, IMoveSpeedLimit
    {
        private const float Recover = 0.3f; // 끝 무렵 원래 속도로 돌아오는 시간(초)

        private float until = -1f, ratio = 1f;

        /// <summary>충격파에 맞았다(감속 시간, 초).</summary>
        public event Action<float> Shocked;

        /// <summary>지금 충격파로 느려져 있는지.</summary>
        public bool IsSlowed => Time.time < until;

        /// <summary>target을 time초 동안 달리기 속도의 ratio배로 느리게 한다(더 강한·긴 쪽을 남긴다).</summary>
        public static void Apply(GameObject target, float time, float ratio)
        {
            if (target == null || time <= 0f) return;
            if (!target.TryGetComponent(out ShockSlow slow))
            {
                slow = target.AddComponent<ShockSlow>();
                if (target.TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 감속(IMoveSpeedLimit)을 이동에 알린다
            }
            slow.Begin(time, ratio);
        }

        private void Begin(float time, float r)
        {
            ratio = IsSlowed ? Mathf.Min(ratio, r) : r;
            until = Mathf.Max(until, Time.time + time);
            Shocked?.Invoke(time);
        }

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            maxSpeed = float.MaxValue;
            float left = until - Time.time;
            if (left <= 0f) return false;
            float run = TryGetComponent(out ICharacterState st) ? st.RunSpeed : 6.5f;
            maxSpeed = run * Mathf.Lerp(1f, ratio, Mathf.Clamp01(left / Recover));
            return true;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 정한 시간 동안 이동 속도를 조금 줄인다(IMoveSpeedScale — 달리기는 그대로 되고 속도에만 곱함). 예: 안토니오 전류선을 지나가면 7초 동안 0.9배.
    /// Give로 건다(없으면 캐릭터 루트에 붙인다). 이미 걸려 있으면 더 센 감속·더 긴 남은 시간을 쓴다(겹쳐 곱하지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public class TimedSpeedScale : MonoBehaviour, IMoveSpeedScale
    {
        private float multiplier = 1f, left;

        public static void Give(GameObject target, float multiplier, float seconds)
        {
            if (target == null || seconds <= 0f || multiplier >= 1f) return;
            if (!target.TryGetComponent(out TimedSpeedScale s)) s = target.AddComponent<TimedSpeedScale>();
            s.multiplier = s.left <= 0f ? multiplier : Mathf.Min(s.multiplier, multiplier);
            s.left = Mathf.Max(s.left, seconds);
        }

        public bool TryGetSpeedScale(out float m) { m = multiplier; return left > 0f; }

        private void FixedUpdate()
        {
            if (left > 0f) left = Mathf.Max(0f, left - Time.fixedDeltaTime);
        }
    }
}

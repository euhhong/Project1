using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 정한 시간 동안 이동 속도 배율(IMoveSpeedBoost — CharacterMotor가 틱마다 모은다). 예: 카우보이 올가미 뛰어넘기 성공 뒤 빠르게.
    /// Give로 건다(없으면 캐릭터 루트에 붙인다). 이미 걸려 있으면 더 큰 배율·더 긴 남은 시간을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TimedSpeedBoost : MonoBehaviour, IMoveSpeedBoost
    {
        private float multiplier = 1f, left;

        public static void Give(GameObject target, float multiplier, float seconds)
        {
            if (target == null || seconds <= 0f || multiplier <= 1f) return;
            if (!target.TryGetComponent(out TimedSpeedBoost b)) b = target.AddComponent<TimedSpeedBoost>();
            if (b.left <= 0f) b.multiplier = multiplier; else b.multiplier = Mathf.Max(b.multiplier, multiplier);
            b.left = Mathf.Max(b.left, seconds);
        }

        public bool TryGetSpeedBoost(out float m) { m = multiplier; return left > 0f; }

        private void FixedUpdate()
        {
            if (left > 0f) left = Mathf.Max(0f, left - Time.fixedDeltaTime);
        }
    }
}

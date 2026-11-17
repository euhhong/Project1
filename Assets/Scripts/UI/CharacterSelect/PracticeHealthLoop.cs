using Project1.Systems;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 연습 모드 체력 되돌리기(예: 의사 — 자가 치료를 계속 연습할 수 있게).
    /// 들어가면 체력 StartHealth(50)에서 시작하고, 체력이 가득 차면 HUD 체력 막대가 BlinkTime초 깜빡인 뒤 다시 StartHealth가 된다.
    /// CharacterSelectBootstrap이 연습 모드 의사에게만 붙인다(인스펙터 연결 없음). 깜빡임은 PlayerHud가 Blinking을 보고 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PracticeHealthLoop : MonoBehaviour
    {
        public const float StartHealth = 50f;
        private const float BlinkTime = 1.2f;

        private CharacterHealth health;
        private float blinkUntil = -1f;

        /// <summary>체력 막대를 깜빡이는 중(가득 찬 뒤 50으로 돌아가기 전).</summary>
        public bool Blinking => blinkUntil > 0f;

        private void Start()
        {
            if (!TryGetComponent(out health)) { Destroy(this); return; }
            health.SetCurrent(StartHealth);
        }

        private void Update()
        {
            if (health == null) return;
            if (blinkUntil > 0f)
            {
                if (Time.time < blinkUntil) return;
                blinkUntil = -1f;
                health.SetCurrent(StartHealth); // 다시 50
                return;
            }
            if (health.IsFull && !health.IsDead) blinkUntil = Time.time + BlinkTime;
        }
    }
}

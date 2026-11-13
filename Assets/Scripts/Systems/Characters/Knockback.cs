using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 넉백(밀려남, BE): Push로 받은 속도로 수평으로 밀려나고, duration초 동안 0까지 줄어든다(이동 거리 ≈ 속도 × 시간 ÷ 2).
    /// 밀려나는 동안과 조금 뒤까지 기절(CharacterStun)로 이동·공격·스킬을 못 한다. 벽에 막히면 거기서 멈춘다(CharacterController).
    /// 맞은 캐릭터에 그때 붙는다(직접 붙일 필요 없음). 예: 맹인 지팡이.
    /// </summary>
    [DisallowMultipleComponent]
    public class Knockback : MonoBehaviour
    {
        private Vector3 velocity;
        private float duration, time;
        private CharacterController controller;

        /// <summary>지금 밀려나는 중인지.</summary>
        public bool Active => time < duration;

        /// <summary>target을 velocity(m/s, 수평)로 duration초 동안 밀어낸다. stunAfter = 다 밀린 뒤 더 기절해 있는 시간(초).</summary>
        public static void Push(GameObject target, Vector3 velocity, float duration, float stunAfter = 0.1f)
        {
            if (target == null || duration <= 0f) return;
            if (!target.TryGetComponent(out Knockback k)) k = target.AddComponent<Knockback>();
            k.Begin(velocity, duration, stunAfter);
        }

        private void Begin(Vector3 v, float d, float stunAfter)
        {
            controller = GetComponent<CharacterController>();
            velocity = Vector3.ProjectOnPlane(v, Vector3.up);
            duration = d; time = 0f;
            if (!TryGetComponent(out CharacterStun stun))
            {
                stun = gameObject.AddComponent<CharacterStun>();
                if (TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기절(IMoveOverride)을 이동에 알린다
            }
            stun.Stun(d + stunAfter);
        }

        private void FixedUpdate()
        {
            if (!Active) return;
            float dt = Time.fixedDeltaTime;
            float k = 1f - Mathf.Clamp01(time / duration); // 처음 빠르게, 끝에 0
            Vector3 step = velocity * (k * dt);
            if (controller != null && controller.enabled) controller.Move(step);
            else transform.position += step;
            time += dt;
        }

        private void OnDisable() => time = duration;
    }
}

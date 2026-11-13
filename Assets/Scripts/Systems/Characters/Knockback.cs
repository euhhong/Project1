using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 넉백(밀려남, BE): Push로 받은 속도로 수평으로 밀려나고, duration초 동안 0까지 줄어든다(이동 거리 ≈ 속도 × 시간 ÷ 2).
    /// stun이 켜져 있으면(기본) 밀려나는 동안과 조금 뒤까지 기절(CharacterStun)로 이동·공격·스킬을 못 한다. 끄면 기절 없이 밀리기만 한다(예: 맹인 지팡이 평타). 벽에 막히면 거기서 멈춘다(CharacterController).
    /// 밀려나다가 벽·장애물이나 다른 캐릭터에 옆으로 부딪히면 그때 빠르기만큼 데미지를 입고(impactDamagePerSpeed × 속도, 한 번) 거기서 멈춘다.
    /// 맞은 캐릭터에 그때 붙는다(직접 붙일 필요 없음). 예: 맹인 지팡이.
    /// </summary>
    [DisallowMultipleComponent]
    public class Knockback : MonoBehaviour
    {
        private Vector3 velocity;
        private float duration, time;
        private CharacterController controller;
        private bool impacted, impactDamage = true;
        private float impactPerSpeedOverride = -1f, impactStun;
        private Collider lastHit;

        [Tooltip("부딪혔을 때 데미지 = 이 값 × 부딪힌 순간 밀려나던 속도(m/s). 예: 16m/s로 바로 부딪히면 약 19.")]
        [SerializeField, Min(0f)] private float impactDamagePerSpeed = 1.2f;
        [Tooltip("이보다 느리게 부딪히면(거의 다 밀려남) 데미지 없음(m/s).")]
        [SerializeField, Min(0f)] private float minImpactSpeed = 2f;

        /// <summary>벽·캐릭터에 부딪혀 데미지를 입었을 때(데미지, 부딪힌 자리). 연출용.</summary>
        public event System.Action<float, Vector3> Impacted;

        /// <summary>지금 밀려나는 중인지.</summary>
        public bool Active => time < duration;

        /// <summary>target을 velocity(m/s, 수평)로 duration초 동안 밀어낸다. stun = 밀리는 동안 기절시킬지, stunAfter = 다 밀린 뒤 더 기절해 있는 시간(초),
        /// impactDamage = 벽·캐릭터에 부딪히면 데미지를 줄지(끄면 거기서 멈추기만 — 감시자 평타 넉백), impactPerSpeed = 부딪힘 데미지 배율(음수면 이 컴포넌트 값),
        /// impactStun = 부딪히면(데미지를 입으면) 더 거는 경직(초, 벽꿍 — 생존자 평타).</summary>
        public static void Push(GameObject target, Vector3 velocity, float duration, float stunAfter = 0.1f, bool stun = true, bool impactDamage = true,
            float impactPerSpeed = -1f, float impactStun = 0f)
        {
            if (target == null || duration <= 0f) return;
            if (!target.TryGetComponent(out Knockback k)) k = target.AddComponent<Knockback>();
            k.impactDamage = impactDamage; k.impactPerSpeedOverride = impactPerSpeed; k.impactStun = impactStun;
            k.Begin(velocity, duration, stunAfter, stun);
        }

        private void Begin(Vector3 v, float d, float stunAfter, bool doStun)
        {
            controller = GetComponent<CharacterController>();
            velocity = Vector3.ProjectOnPlane(v, Vector3.up);
            duration = d; time = 0f; impacted = false;
            if (!doStun) return; // 기절 없이 밀리기만(밀리는 동안 이동 입력은 그대로 받는다)
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
            if (controller != null && controller.enabled)
            {
                lastHit = null;
                var flags = controller.Move(step);
                if ((flags & CollisionFlags.Sides) != 0 && !impacted) Impact(velocity.magnitude * k);
            }
            else transform.position += step;
            time += dt;
        }

        // controller.Move 중에 불린다: 옆으로 부딪힌 것(바닥·천장 제외)을 기억한다.
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Active && Mathf.Abs(hit.normal.y) < 0.5f) lastHit = hit.collider;
        }

        /// <summary>밀려나다 벽·장애물·캐릭터에 부딪힘: 빠르기만큼 데미지 한 번, 거기서 멈춘다.</summary>
        private void Impact(float speed)
        {
            impacted = true;
            time = duration; // 더 밀리지 않는다(벽을 따라 미끄러지지 않게)
            if (!impactDamage || speed < minImpactSpeed) return;
            float damage = (impactPerSpeedOverride >= 0f ? impactPerSpeedOverride : impactDamagePerSpeed) * speed;
            if (TryGetComponent(out CharacterHealth health)) health.Damage(damage);
            float r = controller != null ? controller.radius : 0.4f;
            Vector3 point = transform.position + Vector3.up + velocity.normalized * r; // 부딪힌 쪽 몸 가장자리
            Impacted?.Invoke(damage, point);
            if (impactStun > 0f) CharacterStun.Apply(gameObject, impactStun, velocity); // 벽꿍: 박히면 더 멈춤
        }

        private void OnDisable() => time = duration;
    }
}

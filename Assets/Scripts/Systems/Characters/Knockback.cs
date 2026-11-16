using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 넉백(밀려남, BE): Push로 받은 속도로 수평으로 밀려나고, duration초 동안 0까지 줄어든다(이동 거리 ≈ 속도 × 시간 ÷ 2).
    /// stun이 켜져 있으면(기본) 밀려나는 동안과 조금 뒤까지 기절(CharacterStun)로 이동·공격·스킬을 못 한다. 끄면 기절 없이 밀리기만 한다(예: 맹인 지팡이 평타). 벽에 막히면 거기서 멈춘다(CharacterController).
    /// 밀려나다가 벽·장애물이나 다른 캐릭터에 옆으로 부딪히면 그때 빠르기만큼 데미지를 입고(impactDamagePerSpeed × 속도, 한 번) 거기서 멈춘다.
    /// 밀기 직전에 맞은 캐릭터의 IKnockbackReceiver가 반응·방향을 고친다(예: 매달린 채 넉백되면 손을 놓고 벽 바깥으로 떨어져 나감 — CharacterTraversal).
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
        private bool impactStunWallOnly;
        private float wallMargin;
        private static readonly RaycastHit[] WallHits = new RaycastHit[16];
        private Collider lastHit;
        private static readonly System.Collections.Generic.List<Project1.Core.IKnockbackReceiver> Receivers = new();

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
        /// impactStun = 부딪히면(데미지를 입으면) 더 거는 경직(초, 벽꿍 — 생존자 평타).
        /// impactStunWallOnly = impactStun을 벽·장애물에 박힐 때만(캐릭터에 부딪히면 아님), 데미지·빠르기와 상관없이 건다(예: 포워드 박치기).
        /// wallMargin = 벽을 이만큼(m) 두껍게 본다: 밀리는 쪽으로 벽이 이 거리 안에 들어오면 닿지 않아도 박힌 것으로 친다(0 = 실제로 닿아야).</summary>
        public static void Push(GameObject target, Vector3 velocity, float duration, float stunAfter = 0.1f, bool stun = true, bool impactDamage = true,
            float impactPerSpeed = -1f, float impactStun = 0f, bool impactStunWallOnly = false, float wallMargin = 0f)
        {
            if (target == null || duration <= 0f) return;
            CharacterStun.BreakFragile(target); // 밀리면(모션이 바뀜) 깨지는 기절(돌진 벽 충돌 페널티)은 바로 풀린다
            Receivers.Clear();
            target.GetComponents(Receivers);
            foreach (var r in Receivers) r.OnKnockback(ref velocity); // 예: 매달린 채 맞으면 손을 놓고 벽 바깥으로 떨어져 나간다
            if (!target.TryGetComponent(out Knockback k)) k = target.AddComponent<Knockback>();
            k.impactDamage = impactDamage; k.impactPerSpeedOverride = impactPerSpeed; k.impactStun = impactStun; k.impactStunWallOnly = impactStunWallOnly; k.wallMargin = wallMargin;
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
                else if (!impacted && wallMargin > 0f && NearWall(out var wall)) { lastHit = wall; Impact(velocity.magnitude * k); } // 벽 바로 앞(wallMargin 안)
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
            if (impactStunWallOnly)
            {
                // 벽·장애물에 박힘(캐릭터가 아님) → 데미지·빠르기와 상관없이 기절(예: 포워드 박치기)
                bool wall = lastHit == null || lastHit.GetComponentInParent<CharacterIdentity>() == null;
                if (wall && impactStun > 0f) CharacterStun.Apply(gameObject, impactStun, velocity);
                if (!impactDamage || speed < minImpactSpeed) return;
                float dmg = (impactPerSpeedOverride >= 0f ? impactPerSpeedOverride : impactDamagePerSpeed) * speed;
                if (TryGetComponent(out CharacterHealth hp)) hp.Damage(dmg);
                Impacted?.Invoke(dmg, transform.position + Vector3.up + velocity.normalized * (controller != null ? controller.radius : 0.4f));
                return;
            }
            if (!impactDamage || speed < minImpactSpeed) return;
            float damage = (impactPerSpeedOverride >= 0f ? impactPerSpeedOverride : impactDamagePerSpeed) * speed;
            if (TryGetComponent(out CharacterHealth health)) health.Damage(damage);
            float r = controller != null ? controller.radius : 0.4f;
            Vector3 point = transform.position + Vector3.up + velocity.normalized * r; // 부딪힌 쪽 몸 가장자리
            Impacted?.Invoke(damage, point);
            if (impactStun > 0f) CharacterStun.Apply(gameObject, impactStun, velocity); // 벽꿍: 박히면 더 멈춤
        }


        /// <summary>
        /// 밀리는 쪽으로 wallMargin 안에 벽·장애물(캐릭터 아님, 바닥·계단 아님)이 있는지 — 몸(CharacterController 캡슐, 발밑은 계단 높이만큼 띄움)을 그 거리만큼 쓸어 본다.
        /// </summary>
        private bool NearWall(out Collider wall)
        {
            wall = null;
            Vector3 dir = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.zero;
            if (dir == Vector3.zero) return false;
            Vector3 s = transform.lossyScale;
            float r = controller.radius * Mathf.Max(s.x, s.z) * 0.9f, h = controller.height * s.y;
            Vector3 c = transform.position + Vector3.Scale(controller.center, s);
            float lift = Mathf.Min(controller.stepOffset * s.y, h * 0.4f); // 계단 단·문턱에 걸리지 않게 발밑은 조금 띄움
            Vector3 p1 = c + Vector3.up * (-h / 2f + r + lift), p2 = c + Vector3.up * (h / 2f - r);
            if (p2.y < p1.y) p2 = p1;
            int n = Physics.CapsuleCastNonAlloc(p1, p2, r, dir, WallHits, wallMargin + controller.skinWidth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = WallHits[i];
                if (hit.distance <= 0f || Mathf.Abs(hit.normal.y) > 0.5f) continue;          // 처음부터 겹친 것(내 몸 등)·바닥·경사
                if (hit.collider.GetComponentInParent<CharacterIdentity>() != null) continue; // 캐릭터는 벽이 아님
                if (hit.distance < best) { best = hit.distance; wall = hit.collider; }
            }
            return wall != null;
        }
        private void OnDisable() => time = duration;
    }
}

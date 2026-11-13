using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 상대를 따라가는 투사체(유도탄, 예: 공군 신호탄). 판정 전용(BE) — 모양은 FE가 ProjectileEvents로 붙인다.
    /// - 매 FixedUpdate 목표(쏠 때 고른 상대의 몸 가운데) 쪽으로 turnRate(도/초)만큼만 방향을 틀어 곡선으로 휘어 간다.
    ///   길을 찾지 않고 곧장(최단 거리로) 가므로, 사이에 벽·바닥·천장이 있으면 거기 닿아 사라진다.
    /// - 쏜 캐릭터가 맞힐 수 있는 상대의 Hurtbox에 닿으면 HitEvents로 알리고 사라진다. 캐릭터 몸(CharacterController 등)은 벽으로 보지 않는다.
    /// - lifetime(초)이 지나면 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HomingProjectile : MonoBehaviour, IProjectile
    {
        private CharacterIdentity owner, target;
        private float turnRate, hitRadius, wallRadius, lifetime, age;
        private HitboxSlot slot;
        private int attackId;
        private readonly RaycastHit[] hits = new RaycastHit[32];

        public string Kind { get; private set; }
        public int OwnerId => owner != null ? owner.Id : 0;
        public Vector3 Position => transform.position;
        public Vector3 Direction { get; private set; }
        public float Speed { get; private set; }
        public bool Alive { get; private set; }
        public Vector3 EndPoint { get; private set; }
        public bool EndedOnCharacter { get; private set; }
        public float Power { get; private set; }
        /// <summary>맞힌 상대를 기절시키는 시간(초, 0 = 기절 없음 — 예: 신호탄). 맞은 쪽 등 뒤로 날아와 맞으면 등 뒤에서 기절한 것이 된다.</summary>
        public float StunSeconds { get; set; }
        /// <summary>쏠 때 고른 목표(없으면 곧게 날아간다).</summary>
        public CharacterIdentity Target => target;

        /// <summary>투사체를 만든다. target이 없으면 처음 방향으로 곧게 난다(예: 리퍼 안개 칼날). power = 공격 세기 배율(0 = 보통).</summary>
        public static HomingProjectile Launch(string kind, CharacterIdentity owner, CharacterIdentity target, Vector3 origin, Vector3 direction,
            float speed, float turnRateDeg, float hitRadius, float wallRadius, float lifetime, HitboxSlot slot, int attackId, float power = 0f)
        {
            var go = new GameObject($"Projectile_{kind}");
            go.transform.position = origin;
            var p = go.AddComponent<HomingProjectile>();
            p.Kind = kind; p.owner = owner; p.target = target;
            p.Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            p.Speed = speed; p.turnRate = turnRateDeg; p.hitRadius = hitRadius; p.wallRadius = wallRadius;
            p.lifetime = lifetime; p.slot = slot; p.attackId = attackId; p.Power = power; p.Alive = true;
            ProjectileEvents.RaiseLaunched(p);
            return p;
        }

        /// <summary>목표의 몸 가운데(CharacterController가 있으면 그 가운데).</summary>
        public static Vector3 AimPoint(CharacterIdentity c)
        {
            if (c.TryGetComponent(out CharacterController cc)) return c.transform.TransformPoint(cc.center);
            return c.transform.position + Vector3.up * 1.1f;
        }

        private void FixedUpdate()
        {
            if (!Alive) return;
            float dt = Time.fixedDeltaTime;
            age += dt;
            if (target != null && target.isActiveAndEnabled)
            {
                Vector3 desired = AimPoint(target) - transform.position;
                if (desired.sqrMagnitude > 1e-6f)
                    Direction = Vector3.RotateTowards(Direction, desired.normalized, turnRate * Mathf.Deg2Rad * dt, 0f).normalized;
            }

            Vector3 from = transform.position;
            float step = Speed * dt;

            // 상대 Hurtbox
            float hurtDist = float.MaxValue; Hurtbox hurt = null; Vector3 hurtPoint = from;
            int n = Physics.SphereCastNonAlloc(from, hitRadius, Direction, hits, step, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (!h.collider.TryGetComponent(out Hurtbox hb) || !hb.isActiveAndEnabled) continue;
                var victim = hb.Owner;
                if (victim == null || (owner != null && !owner.CanHit(victim))) continue;
                float d = h.distance > 0f ? h.distance : 0f;
                if (d < hurtDist) { hurtDist = d; hurt = hb; hurtPoint = d > 0f ? h.point : from; }
            }

            // 벽·바닥·천장(캐릭터 몸·판정 레이어 제외)
            float wallDist = float.MaxValue; Vector3 wallPoint = from;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            n = Physics.SphereCastNonAlloc(from, wallRadius, Direction, hits, step, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.GetComponentInParent<CharacterIdentity>() != null) continue;
                float d = h.distance > 0f ? h.distance : 0f;
                if (d < wallDist) { wallDist = d; wallPoint = d > 0f ? h.point : from; }
            }

            if (hurt != null && hurtDist <= wallDist)
            {
                HitEvents.Raise(new HitResult
                {
                    AttackerId = OwnerId,
                    VictimId = hurt.Owner.Id,
                    Part = hurt.Part,
                    Slot = slot,
                    AttackId = attackId,
                    Tick = SimulationTick.Current,
                    Point = hurtPoint,
                    Power = Power,
                });
                if (StunSeconds > 0f && hurt.Owner != null)
                {
                    var victim = hurt.Owner.gameObject;
                    if (!victim.TryGetComponent(out CharacterStun stun)) stun = victim.AddComponent<CharacterStun>();
                    stun.Stun(StunSeconds, Direction);
                    if (victim.TryGetComponent(out CharacterMotor victimMotor)) victimMotor.RefreshSpeedLimits(); // 새로 붙인 기절(이동 막기)을 이동이 알도록
                }
                End(hurtPoint, true);
                return;
            }
            if (wallDist < float.MaxValue) { End(wallPoint, false); return; }

            transform.position = from + Direction * step;
            if (age >= lifetime) End(transform.position, false);
        }

        private void End(Vector3 point, bool onCharacter)
        {
            Alive = false;
            EndPoint = point;
            EndedOnCharacter = onCharacter;
            transform.position = point;
            ProjectileEvents.RaiseEnded(this);
            Destroy(gameObject, 1f); // FE가 끝난 자리를 읽을 시간을 잠깐 둔다
        }
    }
}

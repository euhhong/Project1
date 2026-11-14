using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 분신의 움직임 규칙(BE, 분신 캐릭터 루트의 입력 — ICharacterInputSource).
    /// - 늘 상대(불러낸 캐릭터가 맞힐 수 있는 캐릭터, 다른 분신 제외) 중 가장 가까운 하나를 목표로 잡고 끝까지 쫓는다 — 거리·시야 제한 없음.
    ///   목표가 사라지거나 쓰러지면 바로 다음 상대를 찾고, 더 가까운 상대가 생기면 가끔 바꾼다.
    /// - 길은 NavPathFollower(런타임 NavMesh)로 찾는다 — 다른 층이면 계단으로, 벽이 있으면 돌아서 간다. 막히면 점프해 본다.
    /// - 길이 멀면(runDistance 넘게) 달린다. 스태미나(CharacterStamina)가 바닥나면 모터가 걷기 속도로 묶는다.
    /// - 공격 범위(attackRange, 수평) 안이고 높이 차가 작으면 멈춰 서서 공격한다(약공격, 공격이 끝나고 attackGap초 뒤 다시).
    ///   한 번 휘두르면 swingPause초 그 자리에 멈춰 서고, 공격이 맞으면 hitStop초 짧게 멈춘다. 분신의 공격은 피해만 주고 경직·넉백은 없다(HitStunRules·DefaultHitKnockback).
    /// - 제한시간은 없다. 쓰러지거나(체력이 있다면), 불러낸 캐릭터가 사라지거나, 맵 밖으로 떨어지거나, 다시 불러 바뀔 때 사라진다.
    /// 입력은 틱마다 한 번 정해 같은 틱의 이동·공격·스킬이 같은 값을 읽게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavPathFollower))]
    public class CloneBrain : MonoBehaviour, ICharacterInputSource
    {
        private const float RetargetInterval = 1f;
        private const float SwitchRatio = 0.7f;      // 새 상대가 지금 목표보다 이 비율 이상 가까워야 바꾼다(번갈아 흔들리지 않게)
        private const float AttackHeight = 1.6f;     // 이 높이 차(m) 안이어야 같은 바닥으로 보고 때린다
        private const float FallOutY = -100f;

        [Tooltip("공격을 한 번 휘두르고 나면 이만큼(초) 그 자리에 멈춰 선다(목표가 멀어져도 바로 쫓지 않음).")]
        [SerializeField, Min(0f)] private float swingPause = 0.6f;
        [Tooltip("공격이 맞으면 이만큼(초) 짧게 멈춘다(이동 정지).")]
        [SerializeField, Min(0f)] private float hitStop = 0.15f;
        private float holdUntil = -1f;
        private bool wasAttacking;
        private CharacterIdentity self;

        private CharacterIdentity owner, target;
        private float attackRange, attackGap, runDistance;
        private float age, lastAttackEnd = -999f, nextRetarget;
        private ICharacterAttackState attack;
        private ICharacterHealth health;
        private NavPathFollower follower;
        private int frameTick = int.MinValue;
        private CharacterInputFrame frame;
        private bool despawned;

        public CharacterIdentity Target => target;
        public bool InRange { get; private set; }

        public void Init(CharacterIdentity owner, CharacterIdentity target, float attackRange, float attackGap, float runDistance)
        {
            this.owner = owner; this.target = target;
            this.attackRange = attackRange; this.attackGap = attackGap; this.runDistance = runDistance;
        }

        public CharacterInputFrame ReadInput()
        {
            if (frameTick != SimulationTick.Current) { frameTick = SimulationTick.Current; frame = Decide(); }
            return frame;
        }

        private void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (attack == null) TryGetComponent(out attack);
            if (health == null) TryGetComponent(out health);
            bool attacking = attack != null && attack.AttackState != CharacterAttackState.None;
            if (attacking) lastAttackEnd = age;
            if (wasAttacking && !attacking) holdUntil = Mathf.Max(holdUntil, age + swingPause); // 한 번 휘두른 뒤 잠시 멈춤
            wasAttacking = attacking;
            if (owner == null || (health != null && health.IsDead) || transform.position.y < FallOutY) Despawn();
        }

        private void OnEnable() => HitEvents.HitOccurred += OnHit;
        private void OnDisable() => HitEvents.HitOccurred -= OnHit;

        /// <summary>이 분신의 공격이 맞으면 짧게 멈춘다.</summary>
        private void OnHit(HitResult hit)
        {
            if (self == null) TryGetComponent(out self);
            if (self != null && hit.AttackerId == self.Id && hit.VictimId != self.Id) holdUntil = Mathf.Max(holdUntil, age + hitStop);
        }

        public void Despawn()
        {
            if (despawned) return;
            despawned = true;
            CloneEvents.RaiseDespawning(gameObject);
            Destroy(gameObject);
        }

        private CharacterInputFrame Decide()
        {
            var f = new CharacterInputFrame { LookYaw = transform.eulerAngles.y };
            InRange = false;
            if (despawned) return f;
            if (follower == null) follower = GetComponent<NavPathFollower>();
            UpdateTarget();
            if (target == null) return f;

            Vector3 tpos = target.transform.position;
            Vector3 to = tpos - transform.position;
            Vector3 flat = new(to.x, 0f, to.z);
            float dist = flat.magnitude;

            // 휘두른 뒤 잠시·맞힌 뒤 짧게: 제자리에 멈춰 선다(목표만 바라봄, 공격도 다시 내지 않음).
            if (age < holdUntil)
            {
                follower.ResetPath();
                if (dist > 1e-3f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                InRange = dist <= attackRange;
                return f;
            }

            if (dist <= attackRange && Mathf.Abs(to.y) <= AttackHeight)
            {
                // 공격 범위 안: 목표를 보고 멈춰 서서 공격
                InRange = true;
                if (dist > 1e-3f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                follower.ResetPath();
                bool idle = attack == null || attack.AttackState == CharacterAttackState.None;
                if (idle && age - lastAttackEnd >= attackGap)
                {
                    f.LightAttack = true;
                    f.AttackPressed = true;
                    lastAttackEnd = age; // 같은 틱에 두 번 내지 않게(공격이 시작되면 끝날 때까지 다시 갱신된다)
                }
                return f;
            }

            // 길을 따라 다가간다(몸 = 가는 쪽)
            if (follower.Steer(tpos, true, out Vector3 dir))
            {
                f.LookYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                f.Move = new Vector2(0f, 1f);
                f.Run = follower.RemainingDistance > runDistance;
                f.Jump = follower.ConsumeJump();
            }
            else if (dist > 1e-3f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            return f;
        }

        /// <summary>목표가 없거나 못 쓰게 되면 바로, 아니면 가끔 가장 가까운 상대로 다시 고른다.</summary>
        private void UpdateTarget()
        {
            bool valid = IsValid(target);
            if (valid && age < nextRetarget) return;
            nextRetarget = age + RetargetInterval;
            Vector3 pos = transform.position;
            CharacterIdentity best = null;
            float bestSq = float.MaxValue;
            foreach (var c in FindObjectsByType<CharacterIdentity>())
            {
                if (!IsValid(c)) continue;
                float d = (c.transform.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = c; }
            }
            if (!valid) { if (best != target) follower.ResetPath(); target = best; return; }
            if (best != null && best != target
                && bestSq < (target.transform.position - pos).sqrMagnitude * SwitchRatio * SwitchRatio)
            {
                target = best;
                follower.ResetPath();
            }
        }

        private bool IsValid(CharacterIdentity c)
        {
            if (c == null || !c.isActiveAndEnabled || c.Summoner != null || owner == null || !owner.CanHit(c)) return false;
            return !c.TryGetComponent(out ICharacterHealth h) || !h.IsDead;
        }
    }
}

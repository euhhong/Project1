using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 기절. Stun(초)을 받으면 그동안 제자리에 서서 점프·공격·스킬을 못 한다(IMoveOverride로 이동을 0으로).
    /// 기절 중 다시 기절하면 남은 시간이 더 긴 쪽으로 늘어난다(합치지 않는다).
    /// 캐릭터 루트에 붙인다. 예: 전위 돌진에 맞은 캐릭터.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterStun : MonoBehaviour, ICharacterStun, IMoveOverride
    {
        public bool IsStunned => StunRemaining > 0f;
        public float StunRemaining { get; private set; }
        public float StunDuration { get; private set; }

        public bool StunnedFromBehind { get; private set; }

        public event Action<float> Stunned;
        public event Action Recovered;

        public void Stun(float seconds) => Stun(seconds, Vector3.zero);

        /// <summary>target을 seconds초 기절(경직)시킨다. CharacterStun이 없으면 붙이고 이동에 알린다. hitDirection = 맞힌 것이 나아가던 방향(없으면 0).</summary>
        public static void Apply(GameObject target, float seconds, Vector3 hitDirection)
        {
            if (target == null || seconds <= 0f) return;
            if (!target.TryGetComponent(out CharacterStun stun))
            {
                stun = target.AddComponent<CharacterStun>();
                if (target.TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기절(IMoveOverride)을 이동에 알린다
            }
            stun.Stun(seconds, hitDirection);
        }

        /// <summary>지금 기절이 "깨지는 기절"인지(예: 럭비공·로켓 돌진이 벽에 박힌 페널티) — 모션이 바뀌는 일이 생기면 바로 풀린다.</summary>
        public bool Breakable { get; private set; }

        /// <summary>
        /// 깨지는 기절을 건다(없으면 붙인다): 그동안 맞거나(HitEvents) 밀리거나(Knockback)·충격파(ShockSlow)·다른 기절이 걸리면 바로 풀린다
        /// (다른 기절이면 그 기절로 바뀐다). 예: 전위 럭비공·광대 로켓 돌진이 벽에 박힘.
        /// </summary>
        public static void ApplyBreakable(GameObject target, float seconds)
        {
            if (target == null || seconds <= 0f) return;
            bool longerAlready = target.TryGetComponent(out CharacterStun before) && before.IsStunned && !before.Breakable && before.StunRemaining >= seconds;
            Apply(target, seconds, Vector3.zero);
            if (longerAlready) return; // 이미 더 긴 보통 기절 중이면 그대로(깨지는 기절로 바꾸지 않음)
            if (target.TryGetComponent(out CharacterStun stun) && stun.IsStunned) stun.Breakable = true;
        }

        /// <summary>target이 깨지는 기절 중이면 바로 푼다(모션이 바뀌는 일 — 넉백·충격파 등이 부른다).</summary>
        public static void BreakFragile(GameObject target)
        {
            if (target != null && target.TryGetComponent(out CharacterStun stun)) stun.Break();
        }

        private void Break()
        {
            if (!Breakable) return;
            Breakable = false;
            if (!IsStunned) return;
            StunRemaining = 0f;
            StunnedFromBehind = false;
            Recovered?.Invoke();
        }

        private CharacterIdentity identity;
        private void OnEnable() => HitEvents.HitOccurred += OnHit;
        private void OnHit(HitResult hit)
        {
            if (!Breakable) return;
            if (identity == null) TryGetComponent(out identity);
            if (identity != null && hit.VictimId == identity.Id) Break(); // 맞으면 깨지는 기절은 바로 풀린다
        }

        /// <summary>hitDirection = 맞힌 것이 나아가던 방향(월드, 예: 돌진한 쪽 → 나). 내 앞쪽과 같은 쪽으로 나아갔으면 등 뒤에서 맞은 것. 0이면 방향 없음.</summary>
        public void Stun(float seconds, Vector3 hitDirection)
        {
            if (Breakable) Break(); // 깨지는 기절 중 다른 기절이 걸리면: 그 기절로 바꾼다(남은 시간과 비교하지 않음)
            if (seconds <= StunRemaining) return;
            bool fresh = !IsStunned;
            StunRemaining = seconds;
            StunDuration = seconds;
            hitDirection.y = 0f;
            bool behind = hitDirection.sqrMagnitude > 1e-4f && Vector3.Dot(transform.forward, hitDirection.normalized) > 0.3f;
            if (fresh) StunnedFromBehind = behind; // 기절이 길어질 때는 처음 쓰러진 방향 그대로
            Stunned?.Invoke(seconds);
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false;
            return IsStunned;
        }

        private void FixedUpdate()
        {
            if (!IsStunned) return;
            StunRemaining = Mathf.Max(0f, StunRemaining - Time.fixedDeltaTime);
            if (!IsStunned) { StunnedFromBehind = false; Breakable = false; Recovered?.Invoke(); }
        }

        private void OnDisable()
        {
            HitEvents.HitOccurred -= OnHit;
            Breakable = false;
            if (!IsStunned) return;
            StunRemaining = 0f;
            StunnedFromBehind = false;
            Recovered?.Invoke();
        }
    }
}

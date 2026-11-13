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

        /// <summary>hitDirection = 맞힌 것이 나아가던 방향(월드, 예: 돌진한 쪽 → 나). 내 앞쪽과 같은 쪽으로 나아갔으면 등 뒤에서 맞은 것. 0이면 방향 없음.</summary>
        public void Stun(float seconds, Vector3 hitDirection)
        {
            if (seconds <= StunRemaining) return;
            bool fresh = !IsStunned;
            StunRemaining = seconds;
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
            if (!IsStunned) { StunnedFromBehind = false; Recovered?.Invoke(); }
        }

        private void OnDisable()
        {
            if (!IsStunned) return;
            StunRemaining = 0f;
            StunnedFromBehind = false;
            Recovered?.Invoke();
        }
    }
}

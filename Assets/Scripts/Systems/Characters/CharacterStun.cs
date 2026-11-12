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

        public event Action<float> Stunned;
        public event Action Recovered;

        public void Stun(float seconds)
        {
            if (seconds <= StunRemaining) return;
            StunRemaining = seconds;
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
            if (!IsStunned) Recovered?.Invoke();
        }

        private void OnDisable()
        {
            if (!IsStunned) return;
            StunRemaining = 0f;
            Recovered?.Invoke();
        }
    }
}

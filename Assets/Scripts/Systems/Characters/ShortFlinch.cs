using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 짧은 경직(BE): 걸린 동안 제자리(이동·점프 못 함)이고 공격·스킬을 시작하지 못한다 — 기절(CharacterStun)과 달리 기절 모션·병아리 없이.
    /// 예: 맹인에게 밀려 벽에 박힘 = 1초(CaneKnockback·CaneThrustSkill). 맞은 캐릭터에 그때 붙는다(Apply).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShortFlinch : MonoBehaviour, IMoveOverride, IActionLock
    {
        private float until = -1f;

        public bool Active => Time.time < until;
        public bool ActionsLocked => Active;

        /// <summary>target을 seconds초 경직시킨다(이미 더 길게 걸려 있으면 그대로).</summary>
        public static void Apply(GameObject target, float seconds)
        {
            if (target == null || seconds <= 0f) return;
            if (!target.TryGetComponent(out ShortFlinch f))
            {
                f = target.AddComponent<ShortFlinch>();
                if (target.TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 제자리(IMoveOverride)를 이동에 알린다
            }
            f.until = Mathf.Max(f.until, Time.time + seconds);
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false;
            return Active;
        }
    }
}

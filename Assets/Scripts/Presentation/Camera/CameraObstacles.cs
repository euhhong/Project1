using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>카메라가 '벽·천장'으로 치는 Collider 규칙. 캐릭터(몸통·판정)는 막는 것으로 치지 않는다.</summary>
    public static class CameraObstacles
    {
        /// <summary>판정 레이어(Hitbox·Hurtbox)를 뺀 레이어.</summary>
        public static int Mask => ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));

        /// <summary>캐릭터 몸(CharacterController)이나 따라가는 캐릭터의 자식이면 무시한다.</summary>
        public static bool IsIgnored(Collider c, Transform target)
        {
            if (c == null || c.isTrigger) return true;
            if (c is CharacterController) return true;
            if (target != null && c.transform.IsChildOf(target)) return true;
            return c.GetComponentInParent<ICharacterState>() != null;
        }
    }
}

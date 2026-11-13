using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 두 번째 점프 공격(이단으로 뛰어 찬 날아 차기, CharacterAttack.IsFollowUpKick)을 맞히면 상대가 경직(기절, CharacterStun)된다.
    /// 다른 공격에는 아무것도 하지 않는다. 캐릭터 루트에 붙인다(CharacterLoadout, 두 번째 점프 공격을 쓰는 생존자).
    /// </summary>
    [DisallowMultipleComponent]
    public class FollowUpKickStun : MonoBehaviour, IHitModifier
    {
        [Tooltip("경직 시간(초).")]
        [SerializeField, Min(0f)] private float stunTime = 0.8f;

        private CharacterAttack attack;

        public void ModifyHit(ref HitResult hit, GameObject victim)
        {
            if (attack == null) TryGetComponent(out attack);
            if (attack == null || !attack.IsFollowUpKick || victim == null) return;
            if (!victim.TryGetComponent(out CharacterStun stun))
            {
                stun = victim.AddComponent<CharacterStun>();
                if (victim.TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits(); // 기절(IMoveOverride)을 이동에 알린다
            }
            stun.Stun(stunTime);
        }
    }
}

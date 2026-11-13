using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 두 번째 점프 공격(이단으로 뛰어 찬 날아 차기, CharacterAttack.IsFollowUpKick)을 맞히면 상대가 경직(기절, CharacterStun)된다.
    /// 머리에 맞으면(헤드샷 킥) 더 크게: 상대가 뒤로 회오리치며 나가떨어지고(Knockback + HitResult.Knockdown → FE Corkscrew Evade),
    /// 그 모션이 끝나 일어설 때까지(knockdownTime) 못 움직인다.
    /// 다른 공격에는 아무것도 하지 않는다. 캐릭터 루트에 붙인다(CharacterLoadout, 두 번째 점프 공격을 쓰는 생존자).
    /// </summary>
    [DisallowMultipleComponent]
    public class FollowUpKickStun : MonoBehaviour, IHitModifier
    {
        [Tooltip("경직 시간(초) — 몸·팔다리에 맞았을 때.")]
        [SerializeField, Min(0f)] private float stunTime = 0.8f;

        [Header("헤드샷 킥(머리에 맞음) — 나가떨어짐")]
        [Tooltip("나가떨어져 일어설 때까지 못 움직이는 시간(초). 모션(Corkscrew Evade 뛰어오름 → 땅 → 일어섬)에 맞춘다.")]
        [SerializeField, Min(0f)] private float knockdownTime = 1.5f;
        [Tooltip("뒤로 밀려나는 처음 속도(m/s)와 시간(초). 거리 ≈ 속도 × 시간 ÷ 2.")]
        [SerializeField, Min(0f)] private float knockdownSpeed = 8f;
        [SerializeField, Min(0.05f)] private float knockdownPush = 0.5f;

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
            if (hit.Part == BodyPart.Head)
            {
                Vector3 away = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up);
                away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                hit.Knockdown = true;
                hit.Knockback = away * knockdownSpeed;
                Knockback.Push(victim, hit.Knockback, knockdownPush, Mathf.Max(0f, knockdownTime - knockdownPush));
                return;
            }
            stun.Stun(stunTime);
        }
    }
}

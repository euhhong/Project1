using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맹인 지팡이: 지팡이에 맞은 상대는 데미지 대신 넉백(Knockback)된다. 약공격·강공격·좌우 후리기 모두.
    /// 미는 방향 = 맹인 → 상대(수평). 좌우로 그어 후리면(ICharacterAttackState.Swipe) 그 후린 쪽으로 더 민다.
    /// E 찌르기(CaneThrustSkill)는 스킬이 직접 앞으로 세게 민다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 맹인만).
    /// </summary>
    [DisallowMultipleComponent]
    public class CaneKnockback : MonoBehaviour, IHitModifier
    {
        [Tooltip("밀어내는 처음 속도(m/s). 거리 ≈ 속도 × 시간 ÷ 2.")]
        [SerializeField, Min(0f)] private float speed = 16f;
        [Tooltip("밀려나는 시간(초).")]
        [SerializeField, Min(0.05f)] private float duration = 0.35f;
        [Tooltip("좌우로 후렸을 때 옆으로 미는 비율(0 = 앞으로만, 1 = 옆과 앞 같게).")]
        [SerializeField, Range(0f, 2f)] private float swipeSide = 0.9f;

        private ICharacterAttackState attack;

        public void ModifyHit(ref HitResult hit, GameObject victim)
        {
            if (victim == null) return;
            if (attack == null) TryGetComponent(out attack);
            Vector3 dir = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            int swipe = attack != null ? attack.Swipe : 0;
            if (swipe != 0) dir = (dir + transform.right * (swipe * swipeSide)).normalized; // 왼쪽으로 후리면 왼쪽으로, 오른쪽이면 오른쪽으로
            hit.NoDamage = true;
            hit.Knockback = dir * speed;
            Knockback.Push(victim, hit.Knockback, duration);
        }
    }
}

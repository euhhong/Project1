using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맹인 지팡이: 좌우로 그어 후린 지팡이(ICharacterAttackState.Swipe)에 맞은 상대는 데미지 대신 넉백(Knockback)된다.
    /// 그냥 누르는 좌클릭(약공격·강공격)은 일반 평타 — 데미지, 밀지 않음.
    /// 미는 방향 = 맹인 → 상대(수평) + 후린 쪽. 밀려 벽·장애물에 박히면: 벽 데미지(Knockback 그대로) + 평타 봉인 sealTime(4초, AttackSeal) + 짧은 경직 flinchTime(1초, ShortFlinch — 기절 아님).
    /// E 찌르기(CaneThrustSkill)는 스킬이 직접 앞으로 세게 민다(휘두르기에 이어 빠르게 쓴 찌르기로 박히면 봉인 9초).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 맹인만).
    /// </summary>
    [DisallowMultipleComponent]
    public class CaneKnockback : MonoBehaviour, IHitModifier
    {
        public const float SealTime = 4f, ComboSealTime = 9f, FlinchTime = 1f;
        private const float WallMargin = 0.3f;

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
            int swipe = attack != null ? attack.Swipe : 0;
            if (swipe == 0) return; // 그냥 좌클릭 = 일반 평타(데미지, 밀지 않음)
            Vector3 dir = Vector3.ProjectOnPlane(victim.transform.position - transform.position, Vector3.up);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            dir = (dir + transform.right * (swipe * swipeSide)).normalized; // 왼쪽으로 후리면 왼쪽으로, 오른쪽이면 오른쪽으로
            hit.NoDamage = true;
            hit.Knockback = dir * speed;
            Knockback.Push(victim, hit.Knockback, duration, stun: false, wallMargin: WallMargin, onWall: v => OnWall(v, SealTime));
        }

        /// <summary>맹인에게 밀려 벽에 박힘: 평타 봉인 + 짧은 경직(기절 아님). 벽 데미지는 Knockback이 그대로 준다.</summary>
        public static void OnWall(GameObject victim, float sealSeconds)
        {
            AttackSeal.Apply(victim, sealSeconds);
            ShortFlinch.Apply(victim, FlinchTime);
        }
    }
}

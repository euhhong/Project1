using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 주먹 막기(생존자): 공격 키(좌클릭)를 꾹 누르는 동안(CharacterInputFrame.HeavyCharging, CharacterAttack은 강공격 모으기 상태) 막는 자세를 한다.
    /// - 막는 동안 몸 정면 guardAngle(기본 150°, 좌우 75°씩) 안에서 온 주먹 히트(HitResult.Fist)는 맞지 않는다(IHitBlocker) — 데미지·경직·넉백 없음.
    ///   막은 히트는 HitEvents.HitBlocked로 나가 막기 이펙트(FE)가 뜬다.
    /// - 정면 밖(옆·뒤)에서 온 주먹, 주먹이 아닌 공격(무기·발·스킬·투사체)은 그대로 맞는다.
    /// - 키를 떼면 막기가 끝나고 강공격(머리를 노리는 돌려차기)이 나간다(CharacterAttack). 짧게 누르는 약공격은 그대로.
    /// - 공격 중·기절·쓰러짐·오르기/매달리기·행동을 막는 스킬 중에는 막지 않는다. 땅에서 막는 동안은 걷기 속도(CharacterAttack이 제한).
    /// 캐릭터를 고를 때 CharacterLoadout이 생존자에게 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FistParry : MonoBehaviour, IHitBlocker, ICharacterParryState
    {
        [Tooltip("막는 앞쪽 각도(도, 몸 정면 기준 전체). 150 = 좌우 75°씩.")]
        [SerializeField, Range(0f, 360f)] private float guardAngle = 150f;

        private ICharacterInputSource input;
        private ICharacterAttackState attack;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterTraversalState traversal;
        private ICharacterSkillState skill; // 실행 중에 붙을 수 있어 필요할 때 찾는다

        public bool IsParrying { get; private set; }
        public int BlockCount { get; private set; }
        public float GuardAngle => guardAngle;

        private void Awake()
        {
            input = GetComponent<ICharacterInputSource>();
            attack = GetComponent<ICharacterAttackState>();
        }

        private void OnDisable() => IsParrying = false;

        private void FixedUpdate()
        {
            if (input == null) Awake(); // Play 중 재컴파일로 참조가 비었을 때 복구
            bool held = input != null && input.ReadInput().HeavyCharging;
            IsParrying = held && CanParry();
        }

        private bool CanParry()
        {
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            if (traversal == null) TryGetComponent(out traversal);
            if (skill == null) TryGetComponent(out skill);
            if (attack != null && attack.AttackState is not (CharacterAttackState.None or CharacterAttackState.HeavyCharge)) return false; // 치는 중에는 막지 않는다(모으는 중 = 막는 중)
            if (stun != null && stun.IsStunned) return false;
            if (health != null && health.IsDead) return false;
            if (traversal != null && traversal.LocksActions) return false;
            if (skill != null && skill.LocksActions) return false;
            return true;
        }

        public bool BlocksHit(in HitResult hit, GameObject attacker)
        {
            if (!IsParrying || !hit.Fist || !InFront(attacker != null ? attacker.transform.position : hit.Point)) return false;
            BlockCount++;
            return true;
        }

        /// <summary>from(때린 쪽)이 몸 정면 guardAngle 안에 있는지(수평으로 본다).</summary>
        public bool InFront(Vector3 from)
        {
            Vector3 to = Vector3.ProjectOnPlane(from - transform.position, Vector3.up);
            if (to.sqrMagnitude < 1e-6f) return true; // 바로 겹쳐 있으면 앞으로 본다
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return Vector3.Angle(fwd, to) <= guardAngle * 0.5f;
        }
    }
}

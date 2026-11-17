using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 생존자가 판자를 내린다(BE): 세워진 판자(Pallet) 옆(통로 안 — 판자를 지나쳐 등지고 있어도)에서 스페이스바를 누르면 판자를 내린다.
    /// - 내리는 동안(lockTime초) 제자리에 서서(이동·점프 못 함) 판자 쪽을 본다. 판자는 틈을 가로질러 정해진 쪽으로 넘어가 길을 막는다.
    /// - 땅에 서 있고, 기절·쓰러짐·공격·스킬 중이 아닐 때만. 판자 내리기 범위 안에서는 점프하지 않는다(BlocksJump — 내릴 수 없는 순간에도). 범위 밖이면 스페이스바는 그냥 점프.
    /// 이동보다 먼저 돌아야 같은 틱의 점프를 막을 수 있다(DefaultExecutionOrder). 캐릭터 루트에 붙인다(CharacterLoadout, 생존자만).
    /// </summary>
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public class PalletUser : MonoBehaviour, IPalletUser, IMoveOverride, IFacingOverride, IJumpBlock
    {
        [Tooltip("판자를 내리는 동작 시간(초) — 이 동안 제자리(모션 Skill_Pallet_Drop 길이와 맞춤).")]
        [SerializeField, Min(0.1f)] private float lockTime = 0.75f;

        private ICharacterInputSource input;
        private ICharacterState state;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private CharacterIdentity identity;
        private float lockUntil = -1f, lockStart;
        private float faceYaw;

        /// <summary>세워진 판자의 내리기 범위 안(땅 위)이면 점프하지 않는다 — 스페이스바는 판자 내리기만(IJumpBlock, 이동이 본다).</summary>
        public bool BlocksJump { get; private set; }

        public int DropCount { get; private set; }
        public bool Dropping => Time.time < lockUntil;
        public float DropProgress => Dropping ? Mathf.Clamp01((Time.time - lockStart) / lockTime) : 0f;

        private void Awake()
        {
            input = GetComponent<ICharacterInputSource>();
            state = GetComponent<ICharacterState>();
            identity = GetComponent<CharacterIdentity>();
        }

        private void FixedUpdate()
        {
            if (input == null) Awake();
            if (input == null || Dropping) { BlocksJump = false; return; }
            CharacterInputFrame frame = input.ReadInput();
            // 세워진 판자의 내리기 범위 안이면 점프하지 않는다(내릴 수 없는 순간 — 공격·기절 중 등 — 에도). 이동보다 먼저 돌아 같은 틱의 점프를 막는다.
            BlocksJump = Pallet.TryFind(transform.position, transform.forward, out _, out _);
            if (!frame.Jump) return;
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            if (attack == null) TryGetComponent(out attack);
            if (skill == null) TryGetComponent(out skill);
            if (state != null && !state.IsGrounded) return;
            if ((stun != null && stun.IsStunned) || (health != null && health.IsDead)) return;
            if (attack != null && attack.AttackState != CharacterAttackState.None) return;
            if (skill != null && skill.State == SkillState.Casting) return;

            Vector3 look = Quaternion.Euler(0f, frame.LookYaw, 0f) * Vector3.forward; // 시야 방향(몸이 아직 안 돌았어도)
            if (!Pallet.TryFind(transform.position, look, out Pallet pallet, out float fallSign)) return;
            if (!pallet.BeginDrop(identity, fallSign)) return;
            Vector3 dir = Vector3.ProjectOnPlane(pallet.transform.position + pallet.FallDirection * 0.3f - transform.position, Vector3.up); // 판자 쪽을 보고 민다
            if (dir.sqrMagnitude < 1e-3f) dir = pallet.FallDirection;
            faceYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            lockStart = Time.time;
            lockUntil = Time.time + lockTime;
            DropCount++;
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false;
            return Dropping; // 내리는 동안 제자리(이번 틱의 스페이스바 점프도 막는다)
        }

        public bool TryGetFacingYaw(out float yaw)
        {
            yaw = faceYaw;
            return Dropping;
        }
    }
}

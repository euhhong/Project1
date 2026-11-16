using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 거울 순간이동(BE): 맵 구석 전신거울(MirrorPortal) 앞에서 거울 쪽으로 걸어 들어가면
    /// 들어가기(enterTime초, 제자리·거울을 봄 — 모션 Mirror_Enter) → 짝 거울 앞으로 옮겨짐(시야도 거울 바깥쪽으로) → 나오기(emergeTime초, 제자리).
    /// - 땅에 서 있고, 기절·쓰러짐·공격·스킬 중이 아닐 때만. 지난 뒤 cooldown초 동안은 다시 못 들어간다(바로 되돌아가기 방지).
    /// - 생존자·감시자 모두. 이동보다 먼저 돌아야 같은 틱의 이동을 막을 수 있다(DefaultExecutionOrder). 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DefaultExecutionOrder(-79)]
    [DisallowMultipleComponent]
    public class MirrorTraveler : MonoBehaviour, IMirrorTraveler, IMoveOverride, IFacingOverride
    {
        [Tooltip("거울로 들어가는 동작 시간(초) — 모션 Skill_Mirror_Enter 길이와 맞춤.")]
        [SerializeField, Min(0.1f)] private float enterTime = 0.8f;
        [Tooltip("반대 거울에서 나오는 동작 시간(초).")]
        [SerializeField, Min(0f)] private float emergeTime = 0.45f;
        [Tooltip("거울을 지난 뒤 다시 들어갈 수 없는 시간(초).")]
        [SerializeField, Min(0f)] private float cooldown = 5f;

        private ICharacterInputSource input;
        private ICharacterState state;
        private ICharacterStun stun;
        private ICharacterHealth health;
        private ICharacterAttackState attack;
        private ICharacterSkillState skill;
        private MirrorPortal from, to;
        private float enterStart = -99f, emergeStart = -99f, readyAt;
        private float faceYaw;

        public bool Entering => from != null && Time.time - enterStart < enterTime;
        public float EnterProgress => Entering ? Mathf.Clamp01((Time.time - enterStart) / enterTime) : 0f;
        public bool Emerging => Time.time - emergeStart < emergeTime;
        public float EmergeProgress => Emerging ? Mathf.Clamp01((Time.time - emergeStart) / Mathf.Max(0.01f, emergeTime)) : 0f;
        public int TravelCount { get; private set; }

        private void Awake()
        {
            input = GetComponent<ICharacterInputSource>();
            state = GetComponent<ICharacterState>();
        }

        private void FixedUpdate()
        {
            if (input == null) Awake();
            if (from != null)
            {
                if (Time.time - enterStart >= enterTime) Arrive(); // 들어가기 끝 → 반대 거울로
                return;
            }
            if (input == null || Emerging || Time.time < readyAt) return;
            CharacterInputFrame frame = input.ReadInput();
            if (frame.Move.sqrMagnitude < 0.25f) return; // 걸어 들어갈 때만
            if (stun == null) TryGetComponent(out stun);
            if (health == null) TryGetComponent(out health);
            if (attack == null) TryGetComponent(out attack);
            if (skill == null) TryGetComponent(out skill);
            if (state != null && !state.IsGrounded) return;
            if ((stun != null && stun.IsStunned) || (health != null && health.IsDead)) return;
            if (attack != null && attack.AttackState != CharacterAttackState.None) return;
            if (skill != null && skill.State == SkillState.Casting) return;

            Vector3 move = Quaternion.Euler(0f, frame.LookYaw, 0f) * new Vector3(frame.Move.x, 0f, frame.Move.y); // 시야 기준 이동 → 월드
            if (!MirrorPortal.TryFind(transform.position, move, out MirrorPortal mirror)) return;
            from = mirror; to = mirror.Partner;
            Vector3 face = -mirror.Front;
            faceYaw = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg; // 거울을 보고 들어간다
            enterStart = Time.time;
        }

        /// <summary>반대 거울 앞으로 옮기고, 시야·몸을 거울 바깥쪽으로 돌린다.</summary>
        private void Arrive()
        {
            var target = to;
            from = null; to = null;
            readyAt = Time.time + cooldown;
            if (target == null) return;
            var cc = GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (had) cc.enabled = false;
            transform.position = target.ExitPoint;
            Physics.SyncTransforms();
            if (had) cc.enabled = true;
            faceYaw = target.ExitYaw;
            transform.rotation = Quaternion.Euler(0f, faceYaw, 0f);
            if (TryGetComponent(out ILookSnap look)) look.SnapLook(faceYaw); // 카메라도 바로 거울 바깥쪽
            emergeStart = Time.time;
            TravelCount++;
        }

        public bool TryGetMoveOverride(out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false;
            return Entering || Emerging; // 들어가고 나오는 동안 제자리
        }

        public bool TryGetFacingYaw(out float yaw)
        {
            yaw = faceYaw;
            return Entering || Emerging;
        }

        private void OnDisable() { from = null; to = null; }
    }
}

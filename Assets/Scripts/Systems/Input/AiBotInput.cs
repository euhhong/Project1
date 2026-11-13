using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 간단한 AI 봇 입력(게임 시작 모드의 상대). 목표(target)를 바라보고 다가가서 가까우면 때리고, 가끔 고유 스킬(E)을 쓴다.
    /// - 멀면 달려가고, 중간 거리에서는 가끔 옆으로 돌며 다가가고, 공격 거리 안이면 멈춰서 짧게 누르는 약공격을 틈틈이 넣는다.
    /// - 스킬은 스킬 거리 안에서 skillInterval마다 한 번쯤(무작위). 마우스로 겨누는 스킬(나비·음표)은 목표 자리를 겨눈다.
    /// - 목표가 없거나 기절 중이면 가만히 있는다. 행동이 단순한 임시 AI — 나중에 바꿔 끼우면 된다(ICharacterInputSource).
    /// </summary>
    public class AiBotInput : MonoBehaviour, ICharacterInputSource
    {
        [SerializeField] private Transform target;
        [Tooltip("이 거리(m) 안이면 멈춰서 때린다.")]
        [SerializeField, Min(0.3f)] private float attackRange = 1.7f;
        [Tooltip("이보다 멀면 달린다(m).")]
        [SerializeField, Min(0f)] private float runDistance = 6f;
        [Tooltip("약공격 사이 시간(초, 이 값 ~ 1.6배 사이 무작위).")]
        [SerializeField, Min(0.1f)] private float attackInterval = 0.8f;
        [Tooltip("스킬을 쓸 거리(m)와 대략의 간격(초).")]
        [SerializeField, Min(0f)] private float skillRange = 9f;
        [SerializeField, Min(0.5f)] private float skillInterval = 4f;

        private float nextAttack, nextSkill, strafeUntil, strafeDir;
        private int cachedTick = int.MinValue;
        private CharacterInputFrame cachedFrame;

        public void SetTarget(Transform t) => target = t;

        private void Start()
        {
            nextAttack = Time.time + 1f;
            nextSkill = Time.time + Random.Range(2f, skillInterval);
        }

        public CharacterInputFrame ReadInput()
        {
            int tick = SimulationTick.Current;
            if (tick == cachedTick) return cachedFrame;
            cachedTick = tick;
            cachedFrame = Think();
            return cachedFrame;
        }

        private CharacterInputFrame Think()
        {
            var f = new CharacterInputFrame { LookYaw = transform.eulerAngles.y };
            if (target == null || !target.gameObject.activeInHierarchy) return f;
            Vector3 to = target.position - transform.position;
            Vector3 flat = new(to.x, 0f, to.z);
            float dist = flat.magnitude;
            if (dist > 0.01f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            f.LookPitch = 5f;
            // 마우스로 겨누는 스킬용: 목표 발밑을 가리킨다.
            f.HasAimPoint = true; f.AimFromCursor = true; f.AimPoint = target.position; f.AimNormal = Vector3.up;

            float now = Time.time;
            if (dist > attackRange)
            {
                // 다가간다. 중간 거리에서는 가끔 옆으로 돌며.
                if (now > strafeUntil && dist < runDistance && Random.value < 0.01f) { strafeUntil = now + Random.Range(0.6f, 1.4f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
                f.Move = now < strafeUntil ? new Vector2(strafeDir * 0.8f, 0.6f).normalized : Vector2.up;
                f.Run = dist > runDistance;
            }
            else if (now >= nextAttack)
            {
                f.LightAttack = true; f.AttackPressed = true; // 짧게 눌렀다 뗀 약공격
                nextAttack = now + Random.Range(attackInterval, attackInterval * 1.6f);
            }

            if (dist <= skillRange && now >= nextSkill)
            {
                f.Skill = true;
                nextSkill = now + Random.Range(skillInterval * 0.7f, skillInterval * 1.4f);
            }
            return f;
        }
    }
}

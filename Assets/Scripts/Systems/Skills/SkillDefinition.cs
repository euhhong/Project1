using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 고유 스킬 데이터. 캐릭터마다 하나(예: 의사 = 자가치료). CharacterSkill이 시전·재사용 대기를 돌리고,
    /// 시전을 끝까지 마치면 Complete를 부른다. 새 스킬은 이 클래스를 상속해 Complete(효과)를 만든다.
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [Tooltip("스킬 ID. 모션 자리 이름(Skill_<ID>)과 맞춘다.")]
        public string id;
        public string displayName;

        [Tooltip("시전 시간(초). 이 시간 동안 끊기지 않아야 효과가 난다.")]
        [Min(0f)] public float castTime = 1f;

        [Tooltip("재사용 대기(초). 시전을 마친 뒤부터 센다.")]
        [Min(0f)] public float cooldown = 10f;

        [Tooltip("시전 중 최대 이동 속도(m/s). 0이면 제자리.")]
        [Min(0f)] public float castMoveSpeed = 0f;

        /// <summary>지금 쓸 수 있는지. 못 쓰면 이유를 돌려준다.</summary>
        public virtual bool CanStart(GameObject owner, out string reason)
        {
            reason = null;
            return true;
        }

        /// <summary>
        /// 시전 중 다른 행동(공격)을 막는지. false면 시전 중 공격하면 시전이 끊긴다(효과 없음).
        /// </summary>
        public virtual bool LocksActions => false;

        /// <summary>시전 중 이동을 대신 정할지(예: 돌진). 기본은 정하지 않음 → castMoveSpeed로 속도만 제한.</summary>
        public virtual bool TryGetMoveOverride(GameObject owner, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        /// <summary>
        /// 시간이 다 되기 전에 끝낼지(예: 돌진 중 장애물에 닿음). true면 Complete를 부르고 끝낸다.
        /// recovery = 끝난 뒤 못 움직이는 시간(초, 예: 벽에 부딪혀 넘어짐). 0이면 바로 재사용 대기.
        /// 이 데이터는 여러 캐릭터가 같이 쓰므로 캐릭터별 상태를 여기에 저장하지 않는다.
        /// </summary>
        public virtual bool ShouldEndEarly(GameObject owner, float elapsed, out float recovery)
        {
            recovery = 0f;
            return false;
        }

        /// <summary>시전을 마쳤을 때의 효과(시간이 다 됐거나 ShouldEndEarly).</summary>
        public abstract void Complete(GameObject owner);
    }
}

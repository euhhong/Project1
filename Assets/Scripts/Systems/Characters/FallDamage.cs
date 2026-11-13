using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 낙하 데미지. 공중에서 가장 높았던 곳 → 착지한 곳의 높이 차가 safeHeight를 넘으면, 넘은 1m마다 damagePerMeter만큼 체력이 준다.
    /// 앞구르기로 착지하면(점프해서 높이 떨어짐, CharacterTraversal LandRoll) rollScale배로 줄어든다.
    /// 스킬로 뛰어오른 경우(예: 루키노 도약 — 시전 중·후딜레이)는 데미지 없음. 쓰러져 있으면 없음.
    /// 턱에 매달렸다가 놓아 떨어진 경우는 매달린 높이부터 아주 높지 않으면(CharacterTraversal hangDropSafeHeight 미만) 데미지 없음.
    /// 캐릭터 루트에 붙인다(CharacterLoadout). 체력(CharacterHealth)이 없으면 아무것도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FallDamage : MonoBehaviour
    {
        [Tooltip("이 높이(m)까지는 떨어져도 괜찮다(점프 1.45m·창고 지붕 정도).")]
        [SerializeField, Min(0f)] private float safeHeight = 3f;
        [Tooltip("safeHeight를 넘은 1m마다 데미지(최대 체력 100 기준).")]
        [SerializeField, Min(0f)] private float damagePerMeter = 10f;
        [Tooltip("앞구르기로 착지하면 데미지에 곱하는 값.")]
        [SerializeField, Range(0f, 1f)] private float rollScale = 0.5f;

        private ICharacterState state;
        private ICharacterSkillState skill;
        private ICharacterTraversalState traversal;
        private CharacterHealth health;
        private bool airborne, bySkill;
        private float peakY;

        private void FixedUpdate()
        {
            state ??= GetComponent<ICharacterState>();
            if (state == null) return;
            if (skill == null) TryGetComponent(out skill);
            if (traversal == null) TryGetComponent(out traversal);
            if (health == null) TryGetComponent(out health);
            bool skillBusy = skill != null && (skill.LocksActions || skill.State == SkillState.Casting || skill.State == SkillState.Recovering);
            float y = transform.position.y;

            if (!state.IsGrounded)
            {
                if (!airborne) { airborne = true; peakY = y; bySkill = false; }
                peakY = Mathf.Max(peakY, y);
                bySkill |= skillBusy;
                if (traversal != null && traversal.Action is TraversalAction.Hang or TraversalAction.HangGrab) peakY = y; // 매달려 있던 높이부터 잰다
                return;
            }
            if (!airborne) return;
            airborne = false;
            if (bySkill || skillBusy || health == null || health.IsDead) return;
            if (traversal != null && traversal.IsSoftHangDrop(peakY - y)) return; // 턱에 매달렸다 놓아 떨어짐 — 아주 높지 않으면 다치지 않는다
            float over = peakY - y - safeHeight;
            if (over <= 0f) return;
            float damage = over * damagePerMeter;
            if (traversal != null && traversal.Action == TraversalAction.LandRoll) damage *= rollScale; // 구르며 충격을 흘린다
            health.Damage(damage);
        }
    }
}

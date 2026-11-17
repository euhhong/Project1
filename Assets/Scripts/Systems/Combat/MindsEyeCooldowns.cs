using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맹인 지팡이 기술별 재사용 대기(BE): 왼쪽 후리기 · 찌르기(E) · 오른쪽 후리기가 각자 cooldown(7초)씩 따로 돈다.
    /// - 후리기: 그은 쪽 후리기를 쓰면 그쪽만 대기. 대기 중에 그쪽으로 그으면 무시된다(ISwipeGate — CharacterAttack).
    /// - 찌르기: E 스킬 자체 재사용 대기(CaneThrustSkill.CooldownFor)를 그대로 보여 준다.
    /// HUD 오른쪽 아래에 자리가 고정된 시계 세 개(왼쪽 후리기 · 찌르기 · 오른쪽 후리기 — IFixedCooldownSlots). 연습 모드는 대기 없음(SkillCooldowns).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 맹인만).
    /// </summary>
    [DisallowMultipleComponent]
    public class MindsEyeCooldowns : MonoBehaviour, ISwipeGate, IFixedCooldownSlots
    {
        public const float Cooldown = 7f;

        private CharacterAttack attack;
        private CharacterSkill skill;
        private float leftReady, rightReady, leftLen = 1f, rightLen = 1f;

        private void Start()
        {
            if (TryGetComponent(out attack)) attack.AttackStarted += OnAttackStarted;
        }

        private void OnDestroy()
        {
            if (attack != null) attack.AttackStarted -= OnAttackStarted;
        }

        private void OnAttackStarted(CharacterAttackState s)
        {
            if (attack == null || attack.Swipe == 0) return;
            float cd = SkillCooldowns.Scale(Cooldown);
            if (attack.Swipe < 0) { leftReady = Time.time + cd; leftLen = Mathf.Max(cd, 0.01f); }
            else { rightReady = Time.time + cd; rightLen = Mathf.Max(cd, 0.01f); }
        }

        public bool CanSwipe(int dir) => Time.time >= (dir < 0 ? leftReady : rightReady);

        public int SlotCount => 3;

        public void GetSlot(int index, out string label, out float fill)
        {
            switch (index)
            {
                case 0: label = "왼쪽"; fill = Ready(leftReady, leftLen); break;
                case 2: label = "오른쪽"; fill = Ready(rightReady, rightLen); break;
                default:
                    label = "찌르기";
                    if (skill == null) TryGetComponent(out skill);
                    fill = skill != null && skill.State == SkillState.Cooldown && skill.CooldownDuration > 0f
                        ? 1f - skill.CooldownRemaining / skill.CooldownDuration : 1f;
                    break;
            }
        }

        private static float Ready(float readyAt, float len) => Time.time >= readyAt ? 1f : 1f - (readyAt - Time.time) / len;
    }
}

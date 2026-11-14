using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI의 손: "공격 키를 0.6초 꾹 눌렀다 떼기", "E 짧게" 같은 요청을 받아 틱마다 키보드·마우스와 똑같은 입력(CharacterInputFrame)으로 바꾼다.
    /// - 짧게 누르기(TapAttack): 누른 틱에 AttackPressed + LightAttack(같은 틱에 뗌 — 땅 약공격, 공중 공격, 두 번째 날아 차기 모두 그대로 받는다).
    ///   좌우로 긋기(맹인)는 누른 틱에 AttackPressed + AttackSwipe, 다음 틱에 뗌(LightAttack).
    /// - 꾹 누르기(HoldAttack): 누른 틱 AttackPressed, HeavyHoldTime이 지나면 HeavyCharging(생존자는 주먹 막기), 정한 때에 떼면 HeavyRelease(강공격·돌려차기).
    ///   HeavyHoldTime 전에 떼면 약공격이 된다(LocalPlayerInput과 같은 규칙).
    /// - Jump·TapSkill: 한 틱만. HoldSkill: E를 누른 틱 Skill, 정한 시간 동안 SkillHeld(로켓 돌진·그랩 조준), 떼면 발동.
    /// 무엇을 누를지는 정하지 않는다(AiMeleeTactics가 정함).
    /// </summary>
    public sealed class AiKeyPresser
    {
        /// <summary>꾹 누름으로 보는 시간(초). LocalPlayerInput.heavyAttackHoldTime 기본값과 같게.</summary>
        public const float HeavyHoldTime = 0.3f;

        private bool tapPending, swipeReleasePending, pressPending;
        private int tapSwipe;
        private bool attackHeld;
        private float attackPressTime, attackReleaseAt;
        private bool jumpPending, skillTapPending, skillHeld;
        private float skillReleaseAt;

        /// <summary>공격 키를 누르고 있는 중(꾹 누르기).</summary>
        public bool AttackHeld => attackHeld;

        /// <summary>공격 키가 눌려 있거나 곧 눌릴 예정(새 공격 요청을 받지 않는 게 좋음).</summary>
        public bool AttackBusy => attackHeld || tapPending || swipeReleasePending;

        public bool SkillHeld => skillHeld;

        /// <summary>공격 키를 누른 지 지난 시간(초, 꾹 누르는 중일 때만).</summary>
        public float HeldFor(float now) => attackHeld ? now - attackPressTime : 0f;

        public void TapAttack(int swipe = 0)
        {
            if (AttackBusy) return;
            if (swipe != 0) { tapSwipe = swipe; swipeReleasePending = true; pressPending = true; }
            else tapPending = true;
        }

        public void HoldAttack(float now, float seconds)
        {
            if (AttackBusy) return;
            attackHeld = true;
            pressPending = true;
            attackPressTime = now;
            attackReleaseAt = now + Mathf.Max(seconds, HeavyHoldTime + 0.05f);
        }

        /// <summary>꾹 누르는 중이면 적어도 until까지 계속 누른다.</summary>
        public void KeepHolding(float until)
        {
            if (attackHeld) attackReleaseAt = Mathf.Max(attackReleaseAt, until);
        }

        /// <summary>꾹 누르는 중이면 이번 틱에 뗀다(모은 시간이 HeavyHoldTime을 넘었으면 강공격 발동).</summary>
        public void ReleaseAttack(float now)
        {
            if (attackHeld) attackReleaseAt = now;
        }

        public void Jump() => jumpPending = true;

        public void TapSkill() => skillTapPending = true;

        public void HoldSkill(float now, float seconds)
        {
            skillTapPending = true;
            skillHeld = true;
            skillReleaseAt = now + seconds;
        }

        /// <summary>누르던 키를 모두 놓는다(기절 등으로 행동이 끊길 때). 뗄 때 나오는 공격 입력은 만들지 않는다.</summary>
        public void Clear()
        {
            tapPending = swipeReleasePending = pressPending = attackHeld = jumpPending = skillTapPending = skillHeld = false;
            tapSwipe = 0;
        }

        /// <summary>이번 틱 입력에 눌린 키를 써 넣는다(틱마다 한 번).</summary>
        public void Apply(ref CharacterInputFrame f, float now)
        {
            if (tapPending)
            {
                f.AttackPressed = true;
                f.LightAttack = true;
                tapPending = false;
            }
            else if (swipeReleasePending)
            {
                if (pressPending) { f.AttackPressed = true; f.AttackSwipe = tapSwipe; pressPending = false; }
                else { f.LightAttack = true; swipeReleasePending = false; tapSwipe = 0; }
            }
            else if (attackHeld)
            {
                if (pressPending) { f.AttackPressed = true; pressPending = false; }
                float held = now - attackPressTime;
                if (now >= attackReleaseAt)
                {
                    attackHeld = false;
                    if (held >= HeavyHoldTime) f.HeavyRelease = true;
                    else f.LightAttack = true;
                }
                else
                {
                    f.HeavyCharging = held >= HeavyHoldTime;
                }
            }

            if (jumpPending) { f.Jump = true; jumpPending = false; }
            if (skillTapPending) { f.Skill = true; skillTapPending = false; }
            if (skillHeld)
            {
                if (now >= skillReleaseAt) skillHeld = false;
                else f.SkillHeld = true;
            }
        }
    }
}

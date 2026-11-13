using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 안개 칼날(리퍼 E): 손톱에 모인 안개(MistCharge)를 1칸만 써서, 위에서 아래로 내려치며 안개 칼날을 날린다.
    /// 칼날은 2칸 공격 때와 같지만 약하다(MistCharge.weakPower). 안개가 한 칸도 없으면 못 쓴다.
    /// 내려치기(windup) → 판정 순간 칼날이 나감(Active, 짧게) → 마무리(followThrough). 쓰는 동안은 제자리(이동·공격 불가).
    /// 동작 구간은 TryGetMotionPhase로 알린다(FE가 내려치기 모션을 구간에 맞춘다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Mist Blade")]
    public class MistBladeSkill : SkillDefinition
    {
        private const int Windup = 0, Active = 1, FollowThrough = 2;

        [Tooltip("내려치기 준비(팔을 들어 올림) 시간(초) — 이 뒤에 칼날이 나간다.")]
        [Min(0f)] public float windup = 0.25f;
        [Tooltip("내려치는 동안(초).")]
        [Min(0f)] public float active = 0.1f;
        [Tooltip("내려친 뒤 다시 움직일 수 있을 때까지(초).")]
        [Min(0f)] public float followThrough = 0.45f;
        [Tooltip("쓰는 안개 칸 수.")]
        [Min(1)] public int cost = 1;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override bool CanStart(GameObject owner, out string reason)
        {
            if (!owner.TryGetComponent(out MistCharge mist) || mist.Charges < cost) { reason = "안개가 없음"; return false; }
            reason = null;
            return true;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            ctx.SetPhase(Windup);
            if (ctx.TryGetComponent(out MistCharge mist)) mist.TrySpend(cost);
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = false; // 제자리
            return true;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            switch (ctx.Phase)
            {
                case Windup:
                    if (ctx.PhaseTime >= windup)
                    {
                        if (ctx.TryGetComponent(out MistCharge mist)) mist.LaunchBlade(true);
                        ctx.SetPhase(Active);
                    }
                    return false;
                case Active:
                    if (ctx.PhaseTime >= active) ctx.SetPhase(FollowThrough);
                    return false;
                default:
                    return ctx.PhaseTime >= followThrough;
            }
        }

        public override bool TryGetMotionPhase(CharacterSkill ctx, out AttackPhase phase, out float progress)
        {
            switch (ctx.Phase)
            {
                case Windup: phase = AttackPhase.Windup; progress = windup > 0f ? ctx.PhaseTime / windup : 1f; break;
                case Active: phase = AttackPhase.Active; progress = active > 0f ? ctx.PhaseTime / active : 1f; break;
                default: phase = AttackPhase.Recovery; progress = followThrough > 0f ? ctx.PhaseTime / followThrough : 1f; break;
            }
            progress = Mathf.Clamp01(progress);
            return true;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => "안개 칼날!";

        /// <summary>HUD: 안개 칸(●○)과 다음 칸까지 남은 시간.</summary>
        public override string IdleStatusText(CharacterSkill ctx)
        {
            if (!ctx.TryGetComponent(out MistCharge mist)) return null;
            string pips = new string('●', mist.Charges) + new string('○', Mathf.Max(0, mist.MaxCharges - mist.Charges));
            if (mist.Charges >= mist.MaxCharges) return $"안개 {pips}  공격 = 칼날";
            return $"안개 {pips}  다음 {(1f - mist.NextProgress) * mist.ChargeInterval:0.0}초";
        }

        public override void Complete(GameObject owner) { }
    }
}

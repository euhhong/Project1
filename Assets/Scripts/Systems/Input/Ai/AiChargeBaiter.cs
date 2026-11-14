using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 돌진형 스킬(전위 럭비공·광대 로켓) AI의 "유도 → 박치기" 판단.
    /// - 들락날락(Move): 상대 공격 사거리 끝(oppReach)으로 살짝 들어갔다가(유도) 사거리 밖으로 빠지기를 무작위 박자로 되풀이하며 옆으로도 흔든다.
    /// - 박을 때(ShouldFire, 반응 지연이 들어간 상대 모습으로 판단):
    ///   상대가 헛쳐 후딜레이에 들어감 / 사거리 밖에서 휘두르기 시작함(닿기 전에 후딜레이에 박힘) / 막는 자세(주먹 막기는 돌진을 못 막음)
    ///   / 스킬 쓰느라 묶임 / 내가 옆으로 피한 직후 상대가 나를 똑바로 보고 있지 않음 / 오래 기다렸는데 틈이 없으면 멀리서 긴 돌진(기절이 길다).
    ///   같은 틈에는 한 번만 주사위를 굴린다(틱마다 굴리면 확률이 의미 없어짐).
    /// - 돌진 중 방향(SteerYaw): 시야를 초당 turnRate도까지만 돌려 목표를 따라간다(사람이 마우스로 트는 정도 — 옆으로 피하면 빗나갈 수 있게).
    ///   목표가 옆·뒤로 크게 벗어나면 벽에 박혀 넘어지기 전에 E로 멈출지(ShouldCancel) 정한다.
    /// 키를 누르지는 않는다(AiMeleeTactics가 AiKeyPresser로 누름).
    /// </summary>
    public sealed class AiChargeBaiter
    {
        private enum Opening { None, Whiff, OutOfRangeSwing, Parry, SkillBusy, AfterDodge, Patience }

        // 조절 값(AiBotInput 인스펙터에서 넘김)
        public float WhiffChance = 0.85f, OutOfRangeSwingChance = 0.7f, ParryChance = 0.6f, SkillBusyChance = 0.8f, AfterDodgeChance = 0.6f;
        public float MinPatience = 2.5f, MaxPatience = 5f;
        public float TurnRate = 100f;

        private float phaseUntil, side = 1f, baitStart = -1f, patience;
        private bool stepIn;
        private Opening lastOpening;
        private bool cancelRolled, cancelDecision;

        /// <summary>유도를 새로 시작할 때(교전에 들어올 때·돌진이 끝난 뒤).</summary>
        public void Reset(float now)
        {
            baitStart = now;
            patience = Random.Range(MinPatience, MaxPatience);
            phaseUntil = now;
            lastOpening = Opening.None;
        }

        public bool Active => baitStart >= 0f;

        public void Stop() => baitStart = -1f;

        /// <summary>들락날락 이동(시야 기준 로컬 이동, x = 옆, y = 앞).</summary>
        public Vector2 Move(float now, float dist, float oppReach)
        {
            if (!Active) Reset(now);
            if (now >= phaseUntil)
            {
                stepIn = !stepIn;
                phaseUntil = now + (stepIn ? Random.Range(0.25f, 0.45f) : Random.Range(0.35f, 0.75f));
                if (Random.value < 0.35f) side = -side;
            }
            float want = stepIn ? oppReach - 0.05f : oppReach + Random.Range(1.0f, 1.4f);
            float y = Mathf.Clamp((dist - want) * 1.8f, -1f, 1f);
            float x = side * (stepIn ? 0.35f : 0.7f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        /// <summary>
        /// 지금 박을지. seen = 반응 지연이 들어간 상대 모습. facingMe = 상대 정면과 나 사이 각도(도).
        /// lineClear = 나 → 상대 사이가 막히지 않음. sinceDodge = 마지막으로 옆으로 피한 뒤 지난 시간(초).
        /// </summary>
        public bool ShouldFire(float now, float dist, float oppReach, in AiOpponentSnapshot seen, float facingMe, bool lineClear, float sinceDodge)
        {
            if (!Active) Reset(now);
            if (!lineClear || seen.Dead || seen.Stunned || dist < 1.2f) { lastOpening = Opening.None; return false; }

            Opening o = Opening.None;
            if (seen.Recovering && dist < 8f) o = Opening.Whiff;
            else if (seen.Threatening && dist > oppReach + 0.2f && dist < 7f) o = Opening.OutOfRangeSwing;
            else if (seen.Parrying && dist < 7f) o = Opening.Parry;
            else if (seen.SkillBusy && dist < 9f) o = Opening.SkillBusy;
            else if (sinceDodge < 0.45f && facingMe > 35f && dist < 6f) o = Opening.AfterDodge;
            else if (now - baitStart > patience && dist > 4f && dist < 9f) o = Opening.Patience;

            if (o == Opening.None || o == lastOpening) { lastOpening = o; return false; }
            lastOpening = o;
            float chance = o switch
            {
                Opening.Whiff => WhiffChance,
                Opening.OutOfRangeSwing => OutOfRangeSwingChance,
                Opening.Parry => ParryChance,
                Opening.SkillBusy => SkillBusyChance,
                Opening.AfterDodge => AfterDodgeChance,
                _ => 0.5f,
            };
            if (o == Opening.Patience && Random.value >= chance) { Reset(now); return false; } // 이번엔 참고 다시 기다린다
            return Random.value < chance;
        }

        /// <summary>돌진을 시작할 때(방향 따라가기·멈추기 판단을 새로).</summary>
        public void OnChargeStarted()
        {
            cancelRolled = false;
            Stop();
        }

        /// <summary>돌진 중 시야 각도: 지금 각도에서 목표 쪽으로 TurnRate까지만 돈다.</summary>
        public float SteerYaw(float currentYaw, float targetYaw, float dt)
            => Mathf.MoveTowardsAngle(currentYaw, targetYaw, TurnRate * dt);

        /// <summary>돌진 중 멈출지: 목표가 크게 벗어났으면(벽에 박혀 넘어지기 전에) 한 번 주사위를 굴려 정한다.</summary>
        public bool ShouldCancel(float elapsed, float offAngle, float dist)
        {
            if (elapsed < 0.3f || offAngle < 75f || dist < 1.5f) return false;
            if (!cancelRolled) { cancelRolled = true; cancelDecision = Random.value < 0.65f; }
            return cancelDecision;
        }
    }
}

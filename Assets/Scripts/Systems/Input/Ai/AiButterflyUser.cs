using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI 붉은나비(미치코)의 나비 이동(AiBotInput이 근접 AI가 만든 입력 위에 덮는다).
    /// 나비로 날아가기(E)는 놓아 둔 나비가 있어야 쓸 수 있는데, 근접 AI는 나비를 놓지 않아 E를 거의 못 썼다.
    /// - E 재사용 대기가 끝났고 쥔 나비가 있고 상대가 MinRange보다 멀면(다른 층이어도) 좌클릭을 꾹 눌러 상대 바로 앞(내 쪽 LandOffset m)에 나비를 놓고,
    ///   떼자마자 E — 방금 날린 나비는 벽 너머여도 표적(ButterflyField.followJustThrown)이라 바로 그 자리로 날아간다.
    /// - 그 사이 근접 AI의 공격·E 입력은 막는다(섞이면 취소·평타가 된다). 못 놓았거나 못 날았으면 RetryGap초 뒤 다시.
    /// </summary>
    public sealed class AiButterflyUser
    {
        private const float PlaceHoldSeconds = 0.4f; // 좌클릭 꾹(자리 고르기) — 강공격 모으기 시간(0.3초)보다 길게
        private const float MinRange = 6f, MaxRange = 60f;
        private const float LandOffset = 2f;          // 상대 앞(내 쪽) 이만큼 떨어진 자리에 내린다
        private const float GlidePressTime = 0.35f;   // 놓은 뒤 이 시간 동안 E를 눌러 본다(방금 날린 나비 표적 0.5초 안)
        private const float RetryGap = 2f;

        private readonly Transform self;
        private readonly ButterflyField field;
        private ICharacterSkillState skill;
        private float placeUntil = -1f, pressUntil = -1f, nextTry;
        private Vector3 placePoint;
        private bool releasePending;

        public AiButterflyUser(Transform self, ButterflyField field)
        {
            this.self = self;
            this.field = field;
            self.TryGetComponent(out skill);
        }

        /// <summary>근접 AI가 만든 이번 틱 입력 f 위에 나비 이동을 덮는다. target = 상대 루트.</summary>
        public void Apply(ref CharacterInputFrame f, float now, Transform target)
        {
            if (field == null || target == null) return;
            if (skill == null && !self.TryGetComponent(out skill)) return;

            // 1) 좌클릭 꾹 눌러 자리 고르는 중 → 그 자리를 가리키고 누르고 있다가 뗀다(뗀 틱에 나비가 놓임)
            if (placeUntil > 0f)
            {
                Suppress(ref f);
                Aim(ref f, placePoint);
                if (now < placeUntil) { f.HeavyCharging = true; return; }
                placeUntil = -1f; releasePending = true;
                return;
            }
            if (releasePending)
            {
                releasePending = false; Suppress(ref f); Aim(ref f, placePoint);
                pressUntil = now + GlidePressTime;
                return;
            }

            // 2) 놓았으면 E로 그 나비에게 날아간다(시작될 때까지 몇 틱 눌러 봄)
            if (pressUntil > 0f)
            {
                if (skill.State != SkillState.Ready || now >= pressUntil) { pressUntil = -1f; return; }
                Suppress(ref f);
                Look(ref f, placePoint);
                f.Skill = (SimulationTick.Current & 1) == 0; // 누름 — 떼었다 다시(한 틱 걸러)
                return;
            }

            // 3) 쓸 때인지: E 재사용 대기가 끝났고, 쥔 나비가 있고, 상대가 멀면
            if (now < nextTry || skill.State != SkillState.Ready || field.Charges <= 0 || field.Placing || field.WaitingRelease) return;
            if (self.TryGetComponent(out ICharacterStun st) && st.IsStunned) return;
            if (self.TryGetComponent(out ICharacterTraversalState tr) && tr.LocksActions) return;
            Vector3 opp = target.position;
            Vector3 flat = opp - self.position; flat.y = 0f;
            float dist = flat.magnitude;
            bool otherFloor = Mathf.Abs(opp.y - self.position.y) > 2.5f;
            if (dist > MaxRange || (dist < MinRange && !otherFloor)) return;

            nextTry = now + RetryGap;
            Vector3 back = dist > 0.01f ? -flat / dist : -self.forward;
            placePoint = opp + back * Mathf.Min(LandOffset, dist * 0.5f);
            placeUntil = now + PlaceHoldSeconds;
            Suppress(ref f);
            Aim(ref f, placePoint);
            f.HeavyCharging = true;
        }

        private void Aim(ref CharacterInputFrame f, Vector3 point)
        {
            Look(ref f, point);
            f.HasAimPoint = true; f.AimFromCursor = true; f.AimPoint = point; f.AimNormal = Vector3.up;
        }

        private void Look(ref CharacterInputFrame f, Vector3 point)
        {
            Vector3 to = point - (self.position + Vector3.up * 1.7f);
            Vector3 flat = new(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 1e-4f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            f.LookPitch = -Mathf.Atan2(to.y, Mathf.Max(flat.magnitude, 0.01f)) * Mathf.Rad2Deg;
        }

        private static void Suppress(ref CharacterInputFrame f)
        {
            f.LightAttack = false; f.AttackPressed = false; f.HeavyRelease = false; f.HeavyCharging = false;
            f.Skill = false; f.SkillHeld = false;
        }
    }
}

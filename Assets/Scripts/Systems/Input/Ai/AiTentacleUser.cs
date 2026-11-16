using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI 노란옷의 왕의 촉수 쓰기(AiBotInput이 근접 AI가 만든 입력 위에 덮는다). 노란옷의 왕 촉수는 고유 스킬(E 스킬 파일)이 아니라 TentacleField라
    /// 근접 AI의 스킬 쓰기로는 E를 꾹 눌렀다 떼지 않아 촉수를 내려찍지 않았다.
    /// - 내려찍기: 서 있는 촉수 중 밑동에서 상대까지(수평) 내려찍기가 닿는 거리(안쪽 안전 거리 밖)인 것을 바라보고 E를 holdSeconds초 꾹 눌렀다 뗀다
    ///   (TentacleField가 바라보는 촉수를 고르고, 떼면 그 촉수가 상대 쪽으로 내려찍는다).
    /// - 세우기: 닿는 촉수가 없고 상대가 placeMinRange~placeMaxRange 안이면 가끔(placeInterval초마다) 좌클릭을 꾹 눌러 상대 옆(내려찍기가 닿는 자리)에 촉수를 세운다.
    /// 그 동안 근접 AI의 공격·E 입력은 막는다(섞이면 취소·평타가 된다).
    /// </summary>
    public sealed class AiTentacleUser
    {
        private const float HoldSeconds = 0.3f;        // E 꾹(고르기) 시간
        private const float PlaceHoldSeconds = 0.55f;  // 좌클릭 꾹(자리 고르기) 시간 — 강공격 모으기 시간(0.3초)보다 길게
        private const float PlaceInterval = 3.5f;      // 세우기 사이
        private const float SlamInterval = 1.2f;       // 내려찍기 사이
        private const float PlaceMinRange = 3f, PlaceMaxRange = 20f;
        private const float EyeHeight = 1.7f;

        private readonly Transform self;
        private readonly ITentacleField field;
        private float holdUntil = -1f, placeUntil = -1f, nextPlace, nextSlam;
        private Vector3 holdLook, placePoint;
        private bool holdFirstTick, releasePending, placeReleasePending;

        public AiTentacleUser(Transform self, ITentacleField field) { this.self = self; this.field = field; }

        /// <summary>근접 AI가 만든 이번 틱 입력 f 위에 촉수 쓰기를 덮는다. target = 상대 루트.</summary>
        public void Apply(ref CharacterInputFrame f, float now, Transform target)
        {
            if (field == null || target == null) return;
            Vector3 opp = target.position;

            // 1) E 꾹 눌러 고르는 중 → 그 촉수를 바라보고 누르고 있다가 뗀다(뗀 틱에 내려찍기)
            if (holdUntil > 0f)
            {
                Suppress(ref f);
                Look(ref f, holdLook);
                if (now < holdUntil) { f.SkillHeld = true; if (holdFirstTick) { f.Skill = true; holdFirstTick = false; } return; }
                holdUntil = -1f; releasePending = true; // 이번 틱: 뗌
                return;
            }
            if (releasePending) { releasePending = false; Suppress(ref f); return; }

            // 2) 좌클릭 꾹 눌러 세우는 중 → 그 자리를 가리키고 누르고 있다가 뗀다(뗀 틱에 촉수가 솟음)
            if (placeUntil > 0f)
            {
                Suppress(ref f);
                f.HasAimPoint = true; f.AimFromCursor = true; f.AimPoint = placePoint; f.AimNormal = Vector3.up;
                if (now < placeUntil) { f.HeavyCharging = true; return; }
                placeUntil = -1f; placeReleasePending = true;
                return;
            }
            if (placeReleasePending) { placeReleasePending = false; Suppress(ref f); return; }

            // 3) 상대에게 닿는 서 있는 촉수가 있으면 내려찍기
            if (now >= nextSlam && TryReachingTentacle(opp, out Vector3 tentacleTop))
            {
                nextSlam = now + SlamInterval;
                holdLook = tentacleTop;
                holdUntil = now + HoldSeconds; holdFirstTick = true;
                Suppress(ref f); Look(ref f, holdLook); f.SkillHeld = true; f.Skill = true; holdFirstTick = false;
                return;
            }

            // 4) 없으면 가끔 상대 옆에 세운다(상대에서 노란옷의 왕 쪽으로 내려찍기 거리의 절반쯤 — 밑동이 바짝 붙지 않게)
            Vector3 flat = opp - self.position; flat.y = 0f;
            float dist = flat.magnitude;
            if (now >= nextPlace && dist >= PlaceMinRange && dist <= PlaceMaxRange)
            {
                nextPlace = now + PlaceInterval;
                Vector3 back = dist > 0.01f ? -flat / dist : -self.forward;
                placePoint = opp + back * Mathf.Max(field.SlamInnerRadius + 0.8f, field.SlamReach * 0.5f);
                placeUntil = now + PlaceHoldSeconds;
                Suppress(ref f);
                f.HasAimPoint = true; f.AimFromCursor = true; f.AimPoint = placePoint; f.AimNormal = Vector3.up;
                f.HeavyCharging = true;
            }
        }

        /// <summary>서 있는 촉수 중 상대가 내려찍기 거리(안쪽 안전 거리 밖) 안에 있는 것 — 그 촉수 가운데 높이.</summary>
        private bool TryReachingTentacle(Vector3 opp, out Vector3 look)
        {
            look = default;
            var list = field.Tentacles;
            float best = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.State != TentacleState.Idle) continue;
                Vector3 d = opp - t.Position; d.y = 0f;
                float m = d.magnitude;
                if (m < field.SlamInnerRadius || m > field.SlamReach) continue;
                if (m < best) { best = m; look = t.Position + Vector3.up * (field.TentacleHeight * 0.5f); }
            }
            return best < float.MaxValue;
        }

        private void Look(ref CharacterInputFrame f, Vector3 point)
        {
            Vector3 eye = self.position + Vector3.up * EyeHeight;
            Vector3 to = point - eye;
            Vector3 flat = new(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 1e-4f) f.LookYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            f.LookPitch = -Mathf.Atan2(to.y, Mathf.Max(flat.magnitude, 0.01f)) * Mathf.Rad2Deg;
        }

        /// <summary>촉수를 쓰는 동안 근접 AI의 공격·E 입력은 막는다(섞이면 세우기 취소·평타가 된다).</summary>
        private static void Suppress(ref CharacterInputFrame f)
        {
            f.LightAttack = false; f.AttackPressed = false; f.HeavyRelease = false; f.HeavyCharging = false;
            f.Skill = false; f.SkillHeld = false;
        }
    }
}

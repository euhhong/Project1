using System.Collections.Generic;
using Project1.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Project1.Systems
{
    /// <summary>
    /// AI 생존자의 도주(AiBotInput이 근접 AI·판자 쓰기 위에 덮는다). 불리할 때 감시자가 따라오기 힘들게 스태미나를 아끼지 않고 달려
    /// 다른 층이나 다른 판자 구간으로 간다(도착하면 판자 쓰기 AiPalletUser가 이어 받는다).
    /// - 도주하는 때(감시자가 FleeTriggerRange 안일 때만):
    ///   ① 직전 교전에서 크게 다침 — 최근 HurtWindow초 사이 체력을 HurtFraction 넘게 잃었거나 체력이 LowHealth 아래
    ///   ② 주변 판자를 다 씀 — NearPalletRange 안의 판자가 모두 내려짐
    ///   ③ 내 스킬은 재사용 대기 중(SkillGapCooldown초 넘게 남음)인데 상대 스킬은 준비됨
    /// - 갈 곳: 감시자에게서 먼(나보다 감시자가 훨씬 먼) 세워진 판자, 다른 층이면 더 좋게. 없으면 감시자 반대쪽으로 FleeDistance 떨어진 NavMesh 자리.
    /// - 가는 법: 길 찾기(AiRouteFinder — 창틀·판자 넘기 포함)를 따라 지칠 때까지 달린다(스태미나를 남기지 않음). 앞을 보고 달린다.
    /// - 끝: 도착 / 감시자를 EscapedRange 넘게 따돌림 / MaxFleeTime 지남 → RetryDelay 동안 다시 도주하지 않는다.
    /// </summary>
    public sealed class AiRetreat
    {
        private const float FleeTriggerRange = 15f, EscapedRange = 28f, MaxFleeTime = 14f, RetryDelay = 6f;
        private const float HurtWindow = 6f, HurtFraction = 0.3f, LowHealth = 0.35f;
        private const float NearPalletRange = 22f, SkillGapCooldown = 2f, FleeDistance = 25f;
        private const float CheckInterval = 0.4f, ArriveRange = 2f;

        private readonly Transform self;
        private readonly AiRouteFinder route;
        private ICharacterHealth health;
        private ICharacterSkillState skill;
        private ICharacterState state;
        private ICharacterStamina stamina;
        private readonly Queue<(float t, float hp)> hpLog = new();
        private float nextCheck, fleeStart = -1f, noFleeUntil;
        private Vector3 destination;

        public bool Fleeing => fleeStart >= 0f;

        public AiRetreat(GameObject self)
        {
            this.self = self.transform;
            route = new AiRouteFinder(self);
        }

        /// <summary>입력 f 위에 도주를 덮는다(도주 중이면 이동·시야·달리기를 정하고 공격은 하지 않는다). hunter = 상대 루트.</summary>
        public void Apply(ref CharacterInputFrame f, float now, Transform hunter)
        {
            if (hunter == null) { fleeStart = -1f; return; }
            if (health == null) self.TryGetComponent(out health);
            if (skill == null) self.TryGetComponent(out skill);
            if (state == null) self.TryGetComponent(out state);
            if (stamina == null) self.TryGetComponent(out stamina);
            LogHealth(now);

            Vector3 me = self.position, opp = hunter.position;
            float hunterDist = Flat(opp - me).magnitude;

            if (!Fleeing)
            {
                if (now < nextCheck || now < noFleeUntil || hunterDist > FleeTriggerRange) return;
                nextCheck = now + CheckInterval;
                if (!ShouldFlee(now, me, hunter)) return;
                if (!PickDestination(me, opp, out destination)) { noFleeUntil = now + 2f; return; }
                fleeStart = now;
            }

            // 끝
            if (Flat(destination - me).magnitude < ArriveRange || hunterDist > EscapedRange || now - fleeStart > MaxFleeTime
                || (health != null && health.IsDead))
            {
                fleeStart = -1f; noFleeUntil = now + RetryDelay;
                return;
            }

            // 길을 따라 지칠 때까지 달린다(창틀·판자도 넘음)
            bool grounded = state == null || state.IsGrounded;
            Vector3 dir = route.Steer(destination, now, true, grounded, out bool jump);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Flat(destination - me);
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            f.LookYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // 앞을 보고 달린다
            f.Move = Vector2.up;
            f.Run = stamina == null || !stamina.IsExhausted; // 스태미나를 남기지 않는다
            f.Jump = jump;
            f.LightAttack = false; f.AttackPressed = false; f.AttackHeld = false; f.HeavyCharging = false; f.HeavyRelease = false;
            f.Skill = false; f.SkillHeld = false;
        }

        private void LogHealth(float now)
        {
            if (health == null) return;
            hpLog.Enqueue((now, health.Current));
            while (hpLog.Count > 0 && now - hpLog.Peek().t > HurtWindow) hpLog.Dequeue();
        }

        private bool ShouldFlee(float now, Vector3 me, Transform hunter)
        {
            // ① 크게 다침
            if (health != null && health.Max > 0f)
            {
                float max = 0f;
                foreach (var e in hpLog) max = Mathf.Max(max, e.hp);
                if ((max - health.Current) / health.Max >= HurtFraction || health.Current / health.Max < LowHealth) return true;
            }
            // ② 주변 판자를 다 씀(주변에 판자가 있었는데 모두 내려짐 — 처음부터 판자가 없는 곳은 해당 없음)
            bool anyUp = false, anyDown = false;
            foreach (var p in Pallet.All)
            {
                if (p == null || (p.transform.position - me).sqrMagnitude > NearPalletRange * NearPalletRange) continue;
                if (p.State == Pallet.PalletState.Up) anyUp = true; else anyDown = true;
            }
            if (anyDown && !anyUp) return true;
            // ③ 내 스킬은 재사용 대기 중, 상대 스킬은 준비됨
            if (skill != null && skill.State == SkillState.Cooldown && skill.CooldownRemaining > SkillGapCooldown
                && hunter.TryGetComponent(out ICharacterSkillState theirs) && theirs.State == SkillState.Ready && !string.IsNullOrEmpty(theirs.SkillId))
                return true;
            return false;
        }

        /// <summary>감시자에게서 먼 세워진 판자(다른 층이면 더 좋게) — 없으면 감시자 반대쪽 FleeDistance m의 NavMesh 자리.</summary>
        private bool PickDestination(Vector3 me, Vector3 opp, out Vector3 dest)
        {
            dest = default;
            float best = float.MinValue;
            foreach (var p in Pallet.All)
            {
                if (p == null || p.State != Pallet.PalletState.Up) continue;
                Vector3 c = p.transform.position + p.transform.forward * (p.Gap * 0.5f);
                float dMe = (c - me).magnitude, dHunter = (c - opp).magnitude;
                if (dHunter < dMe * 1.2f || dHunter < 10f) continue; // 감시자 쪽이거나 감시자에게 가까움
                bool otherFloor = Mathf.Abs(c.y - me.y) > 3f;
                float score = dHunter - dMe * 0.6f + (otherFloor ? 8f : 0f);
                if (score > best && NavMesh.SamplePosition(c, out var hit, 2f, NavMesh.AllAreas)) { best = score; dest = hit.position; }
            }
            if (best > float.MinValue) return true;
            Vector3 away = Flat(me - opp);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Flat(self.forward).normalized;
            for (int i = 0; i < 7; i++)
            {
                float a = (i % 2 == 0 ? 1 : -1) * (i + 1) / 2 * 25f; // 0, ±25, ±50, ±75도
                Vector3 c = me + Quaternion.Euler(0f, a, 0f) * away * FleeDistance;
                if (NavMesh.SamplePosition(c, out var hit, 6f, NavMesh.AllAreas) && (hit.position - opp).magnitude > FleeDistance * 0.7f) { dest = hit.position; return true; }
            }
            return false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}

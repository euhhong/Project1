using System.Collections.Generic;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 스킬이 만든 방해물 목록(BE).
    /// - 하스터 촉수(서 있는 동안): 단단한 기둥 — 아무도 뚫고 지나가지 못한다(주인 하스터만 지나감). 그랩은 막히지만 끌려가지 않는다(NoTowSurface).
    /// - 공장장 인형(멈춘 인형 기둥): 걸어서는 뚫고 지나가지만 돌진(포워드 럭비공·광대 로켓)과 그랩·올가미는 막힌다(콜라이더 없이 여기서 잰다).
    /// - 공장장 분신은 몸(CharacterController)이 방해물이다(따로 등록하지 않음 — Summoner가 있는 CharacterIdentity).
    /// - 붉은나비 나비에게(도약)는 촉수·분신을 뚫고 간다(ButterflyGlideSkill).
    /// </summary>
    public static class SkillObstacles
    {
        /// <summary>지금 서 있는 촉수 기둥 콜라이더들(TentacleField가 넣고 뺀다).</summary>
        public static readonly List<Collider> Tentacles = new();

        /// <summary>인형이 있는 공장장들(DollThrow가 넣고 뺀다).</summary>
        public static readonly List<DollThrow> Dolls = new();

        /// <summary>멈춘 인형 기둥 크기(반지름 m, 인형 가운데 아래 / 위로 m).</summary>
        public const float DollRadius = 0.35f, DollBelow = 0.3f, DollAbove = 1.1f;

        private static bool Resting(DollThrow d, out Vector3 at)
        {
            at = default;
            if (d == null || !d.isActiveAndEnabled || !d.DollActive || d.DollFlying) return false;
            at = d.DollPosition;
            return true;
        }

        /// <summary>start에서 dir로 length만큼(굵기 radius) 가는 길에 멈춘 인형 기둥이 있으면 처음 닿는 거리(m).</summary>
        public static bool SweepDoll(Vector3 start, Vector3 dir, float length, float radius, out float at)
        {
            at = length;
            bool found = false;
            foreach (var d in Dolls)
            {
                if (!Resting(d, out Vector3 p)) continue;
                float r = DollRadius + radius;
                for (float s = 0f; s <= length; s += 0.1f)
                {
                    Vector3 q = start + dir * s;
                    Vector2 flat = new(q.x - p.x, q.z - p.z);
                    if (flat.sqrMagnitude > r * r || q.y < p.y - DollBelow - radius || q.y > p.y + DollAbove + radius) continue;
                    if (s < at) { at = s; found = true; }
                    break;
                }
            }
            return found;
        }

        /// <summary>몸(발 feet, 반지름 bodyRadius, 키 height)이 앞쪽(forward)으로 멈춘 인형 기둥에 닿았는지(돌진이 막힘).</summary>
        public static bool TouchingDoll(Vector3 feet, Vector3 forward, float bodyRadius, float height)
        {
            foreach (var d in Dolls)
            {
                if (!Resting(d, out Vector3 p)) continue;
                Vector3 to = p - feet; to.y = 0f;
                float r = DollRadius + bodyRadius;
                if (to.sqrMagnitude > r * r || Vector3.Dot(to, forward) <= 0f) continue;
                if (p.y + DollAbove < feet.y || p.y - DollBelow > feet.y + height) continue; // 다른 높이
                return true;
            }
            return false;
        }
    }
}

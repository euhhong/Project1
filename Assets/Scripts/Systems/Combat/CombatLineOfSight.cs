using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 평타 판정용 벽 검사: 때린 쪽 몸 가운데 → 맞은 쪽 사이에 벽·지형(트리거가 아닌 Collider)이 있는지 본다.
    /// 무기·팔다리 판정이 벽을 뚫고 나가 벽 너머 상대를 맞히지 않게 한다(AttackHitDetector).
    /// 캐릭터(CharacterIdentity 아래 Collider — 때린 쪽·맞은 쪽·다른 캐릭터·분신)와 판정 레이어(Hitbox·Hurtbox)는 가리지 않는다.
    /// </summary>
    public static class CombatLineOfSight
    {
        private static readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>attacker 몸 가운데에서 victim 쪽 두 점(맞은 자리, 몸 가운데) 중 하나라도 벽에 막히지 않으면 true.</summary>
        public static bool Clear(GameObject attacker, GameObject victim, Vector3 hitPoint)
        {
            Vector3 from = BodyCenter(attacker);
            return ClearLine(from, hitPoint) || ClearLine(from, BodyCenter(victim));
        }

        /// <summary>
        /// 캐릭터가 아닌 곳(예: 노란옷의 왕 촉수 밑동)에서 victim 쪽 두 점(맞은 자리, 몸 가운데) 중 하나라도 벽·장애물에 막히지 않으면 true.
        /// ignoreRadius = from에서 이 거리(m, 수평) 안의 Collider는 무시(촉수 자기 기둥).
        /// </summary>
        public static bool ClearFrom(Vector3 from, GameObject victim, Vector3 hitPoint, float ignoreRadius = 0f) =>
            ClearLine(from, hitPoint, ignoreRadius) || ClearLine(from, BodyCenter(victim), ignoreRadius);

        /// <summary>
        /// 두 점 사이가 트였는지 — 양쪽 방향으로 다 본다. 한쪽 면만 있는 벽(뒷면이 없는 메시 Collider)은 뒤에서 쏜 광선이 뚫고 지나가
        /// 벽 너머 상대가 맞던 것을 막는다. ignoreRadius = from 둘레(수평) 이 거리 안에서 닿은 것은 무시.
        /// </summary>
        private static bool ClearLine(Vector3 from, Vector3 to, float ignoreRadius = 0f) => ClearRay(from, to, from, ignoreRadius) && ClearRay(to, from, from, ignoreRadius);

        private static bool ClearRay(Vector3 from, Vector3 to, Vector3 origin, float ignoreRadius)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            int n = Physics.RaycastNonAlloc(from, d / len, hits, len, mask, QueryTriggerInteraction.Ignore);
            float r2 = ignoreRadius * ignoreRadius;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.GetComponentInParent<CharacterIdentity>() != null) continue; // 캐릭터 몸은 벽이 아니다
                if (r2 > 0f)
                {
                    Vector3 off = hits[i].point - origin; off.y = 0f;
                    if (off.sqrMagnitude <= r2) continue; // 시작점 둘레(촉수 자기 기둥)
                }
                return false;
            }
            return true;
        }

        /// <summary>캐릭터 몸 가운데(CharacterController가 있으면 그 가운데, 없으면 발에서 0.9m 위).</summary>
        private static Vector3 BodyCenter(GameObject character)
        {
            if (character.TryGetComponent(out CharacterController cc)) return character.transform.TransformPoint(cc.center);
            return character.transform.position + Vector3.up * 0.9f;
        }
    }
}

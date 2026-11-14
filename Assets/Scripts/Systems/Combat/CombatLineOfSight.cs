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

        private static bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return true;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            int n = Physics.RaycastNonAlloc(from, d / len, hits, len, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.GetComponentInParent<CharacterIdentity>() != null) continue; // 캐릭터 몸은 벽이 아니다
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

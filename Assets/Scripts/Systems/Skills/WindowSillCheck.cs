using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 바닥 자리가 창틀(얇은 턱) 윗면인지 판정한다 — 나비·음표·촉수·인형 같은 설치물을 창틀 위에 두지 않게(BE).
    /// 창틀 = 어느 한 방향(가로·세로·대각선)으로 양쪽 모두 maxWidth(0.8m, 창틀 넘기의 두께 한계와 같음) 안에서 minDrop(0.5m) 넘게 꺼지는 좁은 윗면.
    /// 난간·담 꼭대기처럼 같은 모양의 얇은 턱도 같이 걸린다. 계단(한쪽만 꺼짐)·바닥 끝(한쪽만 꺼짐)은 걸리지 않는다.
    /// 캐릭터·트리거·판정은 보지 않는다.
    /// </summary>
    public static class WindowSillCheck
    {
        public const float MaxWidth = 0.8f, MinDrop = 0.5f;
        private const float Step = 0.05f, Lift = 0.05f;

        private static readonly Vector3[] Axes =
        {
            Vector3.right, Vector3.forward,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(0.7071f, 0f, -0.7071f),
        };
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>point(바닥 윗면 위 자리)가 창틀 윗면이면 true.</summary>
        public static bool IsSillTop(Vector3 point) => IsSillTop(point, out _);

        /// <summary>point가 창틀 윗면이면 true와 창틀을 가로지르는 축(양쪽으로 꺼지는 방향, 수평 단위 벡터)을 돌려준다.</summary>
        public static bool IsSillTop(Vector3 point, out Vector3 acrossAxis)
        {
            foreach (var a in Axes)
            {
                if (!DistanceToDrop(point, a, MaxWidth, out float dPos)) continue;
                if (!DistanceToDrop(point, -a, MaxWidth - dPos, out float dNeg)) continue;
                if (dPos + dNeg <= MaxWidth) { acrossAxis = a; return true; }
            }
            acrossAxis = Vector3.zero;
            return false;
        }

        /// <summary>point에서 dir로 maxDist 안에 윗면이 끝나고 minDrop 넘게 꺼지는 곳이 있으면 그 거리. 앞이 벽에 막히면 꺼지지 않은 것으로 본다(창 기둥).</summary>
        private static bool DistanceToDrop(Vector3 point, Vector3 dir, float maxDist, out float dist)
        {
            dist = 0f;
            if (maxDist <= 0f) return false;
            Vector3 origin = point + Vector3.up * Lift;
            float limit = maxDist;
            if (Cast(origin, dir, maxDist, out float block)) limit = block - 0.02f; // 벽(창 기둥)에 막힘
            for (float s = Step; s <= limit + 1e-4f; s += Step)
            {
                if (Cast(origin + dir * s, Vector3.down, Lift + MinDrop, out _)) continue; // 아직 윗면(또는 낮은 턱)
                dist = s;
                return true;
            }
            return false;
        }

        private static bool Cast(Vector3 origin, Vector3 dir, float distance, out float hitDist)
        {
            hitDist = float.MaxValue;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, distance, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (c is CharacterController || c.GetComponentInParent<CharacterIdentity>() != null) continue;
                hitDist = Mathf.Min(hitDist, Hits[i].distance);
            }
            return hitDist < float.MaxValue;
        }
    }
}

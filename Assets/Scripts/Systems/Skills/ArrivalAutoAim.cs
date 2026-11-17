using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 이동기가 끝난 직후 시야 오토에임(붉은나비 나비에게 날아가기·용병 아대 도약이 같이 쓴다):
    /// radius 안(수평, 위아래 2.5m)의 가장 가까운 상대 쪽으로 시야를 바로 돌리고(ILookSnap — 카메라도 바로), trackTime초 동안 그 상대를 따라 화면이 돈다.
    /// 상대가 없으면 그대로.
    /// </summary>
    public static class ArrivalAutoAim
    {
        public static void FaceNearest(GameObject owner, float radius, float trackTime)
        {
            if (owner == null || radius <= 0f || !owner.TryGetComponent(out ILookSnap look)) return;
            var self = owner.GetComponent<CharacterIdentity>();
            Vector3 me = owner.transform.position;
            CharacterIdentity best = null; float bestD = radius * radius;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c == null || c == self || (self != null && !self.CanHit(c))) continue;
                if (c.TryGetComponent(out ICharacterHealth hp) && hp.IsDead) continue;
                Vector3 to = c.transform.position - me;
                if (Mathf.Abs(to.y) > 2.5f) continue;
                to.y = 0f;
                float d = to.sqrMagnitude;
                if (d < 1e-4f || d > bestD) continue;
                bestD = d; best = c;
            }
            if (best == null) return;
            Vector3 dir = best.transform.position - me; dir.y = 0f;
            look.SnapLook(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
            look.TrackLook(best.transform, trackTime); // 이후 잠깐 상대 움직임을 따라 화면이 돈다(직접 돌리기는 잘 안 됨)
        }
    }
}

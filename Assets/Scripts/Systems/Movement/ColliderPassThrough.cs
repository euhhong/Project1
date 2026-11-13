using System.Collections.Generic;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 이 캐릭터(CharacterController)가 정한 Collider를 잠깐 뚫고 지나가게 한다(Physics.IgnoreCollision).
    /// 쓰는 쪽이 지나가는 동안 매 틱 Allow를 부르고, 더 부르지 않은 지 keepTime이 지나고 몸이 그 Collider에서 빠져나오면 다시 막는다
    /// (스킬이 도중에 끊겨도 몸이 박힌 채 막히지 않는다. 그 위에 서 있기만 하면 바로 다시 막혀 밟고 선다). 예: 붉은나비 나비에게로 — 창틀 아랫부분·바닥 끝 모서리를 뚫고 간다.
    /// 캐릭터 루트에 붙는다(쓰는 쪽이 없으면 붙인다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class ColliderPassThrough : MonoBehaviour
    {
        private const float KeepTime = 0.15f;

        private CharacterController cc;
        private readonly Dictionary<Collider, float> ignored = new();
        private readonly List<Collider> done = new();

        /// <summary>c를 뚫고 지나가게 한다(이미 그렇다면 시간만 늘린다).</summary>
        public void Allow(Collider c)
        {
            if (c == null) return;
            if (cc == null) cc = GetComponent<CharacterController>();
            if (!ignored.ContainsKey(c)) Physics.IgnoreCollision(cc, c, true);
            ignored[c] = Time.time;
        }

        private void FixedUpdate()
        {
            if (ignored.Count == 0) return;
            done.Clear();
            foreach (var (c, t) in ignored)
            {
                if (c == null) { done.Add(c); continue; }
                if (Time.time - t < KeepTime) continue;
                if (!Inside(c)) done.Add(c); // 몸이 그 안에서 빠져나왔다(위에 서 있기만 해도 다시 막아 밟고 선다)
            }
            foreach (var c in done)
            {
                if (c != null) Physics.IgnoreCollision(cc, c, false);
                ignored.Remove(c);
            }
        }

        /// <summary>지금 몸이 들어가 있지 않은 것은 기다리지 않고 바로 다시 막는다(도착했을 때 — 서 있는 바닥으로 빠지지 않게).</summary>
        public void RestoreClear()
        {
            if (cc == null) cc = GetComponent<CharacterController>();
            done.Clear();
            foreach (var c in ignored.Keys) if (c == null || !Inside(c)) done.Add(c);
            foreach (var c in done)
            {
                if (c != null) Physics.IgnoreCollision(cc, c, false);
                ignored.Remove(c);
            }
        }

        /// <summary>몸(캡슐)이 c 안으로 파고들어 있는지 — 캡슐 축의 점들에서 c까지 거리가 반지름보다 짧으면 파고든 것(닿기만 한 것은 아님).</summary>
        private bool Inside(Collider c)
        {
            Vector3 center = cc.transform.TransformPoint(cc.center);
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            float r = cc.radius * 0.9f;
            for (int i = 0; i <= 4; i++)
            {
                Vector3 p = center + Vector3.up * Mathf.Lerp(-half, half, i / 4f);
                if ((c.ClosestPoint(p) - p).sqrMagnitude < r * r) return true; // 점이 c 안이면 ClosestPoint = 그 점(거리 0)
            }
            return false;
        }

        private void OnDisable()
        {
            foreach (var c in ignored.Keys) if (c != null && cc != null) Physics.IgnoreCollision(cc, c, false);
            ignored.Clear();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 이 캐릭터(CharacterController)가 정한 Collider를 잠깐 뚫고 지나가게 한다(Physics.IgnoreCollision).
    /// 쓰는 쪽이 지나가는 동안 매 틱 Allow를 부르고, 더 부르지 않은 지 keepTime이 지나고 몸이 그 Collider에서 빠져나오면 다시 막는다
    /// (스킬이 도중에 끊겨도 몸이 박힌 채 막히지 않는다). 예: 붉은나비 나비에게로 — 창틀 아랫부분을 뚫고 간다.
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
                Bounds body = cc.bounds;
                body.Expand(0.1f);
                if (!body.Intersects(c.bounds)) done.Add(c); // 몸이 빠져나왔다
            }
            foreach (var c in done)
            {
                if (c != null) Physics.IgnoreCollision(cc, c, false);
                ignored.Remove(c);
            }
        }

        private void OnDisable()
        {
            foreach (var c in ignored.Keys) if (c != null && cc != null) Physics.IgnoreCollision(cc, c, false);
            ignored.Clear();
        }
    }
}

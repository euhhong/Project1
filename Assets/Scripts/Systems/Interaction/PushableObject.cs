using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 캐릭터가 밀 수 있는 물건(상자 등). 미는 쪽(CharacterPush)이 Move로 수평으로 옮긴다.
    /// 벽·다른 물건·캐릭터에 막히면 막힌 데까지만 가고, 밑에 바닥이 없어질 자리(턱 끝)로는 가지 않는다.
    /// BoxCollider가 있는 물건에 붙인다(트리거 아님). 맵(BG)이 상자를 만들 때 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public class PushableObject : MonoBehaviour
    {
        private const float Skin = 0.02f;

        [Tooltip("미는 빠르기 배율(무거운 물건은 작게).")]
        [SerializeField, Range(0.1f, 2f)] private float speedScale = 1f;

        private BoxCollider box;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public float SpeedScale => speedScale;

        /// <summary>수평으로 delta만큼 옮긴다(막히면 막힌 데까지). 실제로 옮긴 거리(m)를 돌려준다. pusher의 Collider는 무시한다.</summary>
        public float Move(Vector3 delta, Collider pusher)
        {
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return 0f;
            if (box == null) box = GetComponent<BoxCollider>();
            Vector3 dir = delta / distance;
            Vector3 center = transform.TransformPoint(box.center);
            Vector3 half = Vector3.Scale(box.size * 0.5f, Abs(transform.lossyScale));
            Vector3 castHalf = new(half.x - Skin, half.y - 0.06f, half.z - Skin); // 바닥·위에 얹힌 면은 닿지 않게 조금 줄인다
            Vector3 castCenter = center + Vector3.up * 0.03f;

            float allowed = distance;
            int n = Physics.BoxCastNonAlloc(castCenter, castHalf, dir, Hits, transform.rotation, distance + Skin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (c == box || c == pusher || c.transform.IsChildOf(transform)) continue;
                if (Hits[i].distance <= 0f && Hits[i].point == Vector3.zero) continue; // 처음부터 겹친 것(옆에 붙은 바닥 등)
                allowed = Mathf.Min(allowed, Mathf.Max(0f, Hits[i].distance - Skin));
            }
            if (allowed <= 1e-4f) return 0f;

            // 옮긴 자리 밑에 바닥이 있어야 한다(턱 끝에서 떨어뜨리지 않는다).
            Vector3 next = center + dir * allowed;
            if (!HasGround(next, half.y)) return 0f;

            transform.position += dir * allowed;
            Physics.SyncTransforms(); // 같은 틱에 뒤따라 걷는 캐릭터가 옮긴 자리를 보게
            return allowed;
        }

        private bool HasGround(Vector3 center, float halfHeight)
        {
            int n = Physics.RaycastNonAlloc(center, Vector3.down, Hits, halfHeight + 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (Hits[i].collider != box && !Hits[i].collider.transform.IsChildOf(transform) && Hits[i].collider is not CharacterController) return true;
            return false;
        }

        private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}

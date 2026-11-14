using UnityEngine;
using UnityEngine.AI;

namespace Project1.Systems
{
    /// <summary>
    /// 길 따라가기(BE, AI 캐릭터 루트). 목적지를 주면 RuntimeNavMesh로 실제로 걸어갈 수 있는 길(계단 등)을 찾아,
    /// 지금 가야 할 방향(다음 꺾는 점 쪽)을 알려 준다. 이동은 하지 않는다 — 입력을 정하는 쪽(CloneBrain 등)이 그 방향으로 Move를 넣는다.
    /// - 목적지가 많이 움직였거나, 길에서 벗어났거나, 일정 시간이 지나면 길을 다시 찾는다.
    /// - 막혀서 제자리이면 길을 다시 찾고 점프를 한 번 권한다(WantsJump).
    /// - 길을 못 찾으면(NavMesh 밖·공중) 목적지로 곧장 향한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavPathFollower : MonoBehaviour
    {
        [Tooltip("길을 다시 찾는 최대 간격(초).")]
        [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;
        [Tooltip("목적지가 이만큼(m) 움직이면 바로 다시 찾는다.")]
        [SerializeField, Min(0.1f)] private float repathMove = 1.5f;
        [Tooltip("꺾는 점에 이만큼(m, 수평) 다가가면 다음 점으로.")]
        [SerializeField, Min(0.05f)] private float cornerReach = 0.5f;
        [Tooltip("이 시간(초) 동안 거의 못 움직이면 막힌 것으로 본다.")]
        [SerializeField, Min(0.2f)] private float stuckTime = 0.8f;

        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[256];
        private int cornerCount, cornerIndex;
        private Vector3 pathGoal;
        private float lastRepath = -999f;
        private bool hasPath;
        private Vector3 stuckPos;
        private float stuckSince;
        private bool jumpRequested;

        /// <summary>지금 길의 남은 거리(m, 길이 없으면 곧장 거리).</summary>
        public float RemainingDistance { get; private set; }
        /// <summary>길을 찾아 따라가는 중(false면 곧장 향하는 중).</summary>
        public bool HasPath => hasPath;
        /// <summary>막혔으니 점프해 보라(읽으면 꺼진다).</summary>
        public bool ConsumeJump() { bool j = jumpRequested; jumpRequested = false; return j; }

        /// <summary>destination으로 가기 위해 지금 향할 수평 방향(정규화). 이미 도착했으면 false.</summary>
        public bool Steer(Vector3 destination, bool moving, out Vector3 direction)
        {
            path ??= new NavMeshPath();
            Vector3 pos = transform.position;
            float now = Time.time;
            CheckStuck(pos, moving, now);

            bool repath = !hasPath || now - lastRepath >= repathInterval
                          || (destination - pathGoal).sqrMagnitude > repathMove * repathMove
                          || OffPath(pos);
            if (repath) Repath(pos, destination, now);

            Vector3 aim = destination;
            if (hasPath)
            {
                while (cornerIndex < cornerCount - 1 && Flat(corners[cornerIndex] - pos).sqrMagnitude < cornerReach * cornerReach)
                    cornerIndex++;
                aim = corners[cornerIndex];
                // 마지막 점(목적지를 NavMesh에 붙인 자리)에 닿으면 실제 목적지 쪽으로
                if (cornerIndex == cornerCount - 1 && Flat(aim - pos).sqrMagnitude < cornerReach * cornerReach) aim = destination;
                RemainingDistance = Flat(aim - pos).magnitude;
                for (int i = cornerIndex; i < cornerCount - 1; i++) RemainingDistance += Vector3.Distance(corners[i], corners[i + 1]);
            }
            else RemainingDistance = Vector3.Distance(pos, destination);

            Vector3 flat = Flat(aim - pos);
            if (flat.sqrMagnitude < 0.01f) { direction = Vector3.zero; return false; }
            direction = flat.normalized;
            return true;
        }

        /// <summary>길을 잊는다(다음 Steer에서 새로 찾는다).</summary>
        public void ResetPath() { hasPath = false; cornerCount = 0; }

        private void Repath(Vector3 pos, Vector3 destination, float now)
        {
            lastRepath = now;
            pathGoal = destination;
            hasPath = false;
            if (!RuntimeNavMesh.Ensure()) return;
            var filter = RuntimeNavMesh.Filter;
            if (!NavMesh.SamplePosition(pos, out var from, 2.5f, filter)) return;
            // 목적지가 점프 중이면 공중이라 조금 넓게 찾는다
            if (!NavMesh.SamplePosition(destination, out var to, 2.5f, filter)
                && !NavMesh.SamplePosition(destination, out to, 5f, filter)) return;
            if (!NavMesh.CalculatePath(from.position, to.position, filter, path) || path.status == NavMeshPathStatus.PathInvalid) return;
            cornerCount = path.GetCornersNonAlloc(corners);
            if (cornerCount < 1) return;
            cornerIndex = cornerCount > 1 ? 1 : 0; // 0번은 지금 자리
            hasPath = true;
        }

        /// <summary>지금 따라가는 구간에서 많이 벗어났나(밀려남·떨어짐).</summary>
        private bool OffPath(Vector3 pos)
        {
            if (!hasPath || cornerIndex == 0) return false;
            Vector3 a = corners[cornerIndex - 1], b = corners[cornerIndex];
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;
            return Flat(pos - closest).sqrMagnitude > 2.5f * 2.5f || Mathf.Abs(pos.y - closest.y) > 2.5f;
        }

        private void CheckStuck(Vector3 pos, bool moving, float now)
        {
            if (!moving || (pos - stuckPos).sqrMagnitude > 0.25f * 0.25f) { stuckPos = pos; stuckSince = now; return; }
            if (now - stuckSince < stuckTime) return;
            stuckPos = pos; stuckSince = now;
            jumpRequested = true;
            ResetPath();
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

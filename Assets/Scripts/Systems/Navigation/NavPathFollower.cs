using UnityEngine;
using UnityEngine.AI;

namespace Project1.Systems
{
    /// <summary>
    /// 길 따라가기(BE, AI 캐릭터 루트). 목적지를 주면 RuntimeNavMesh로 실제로 걸어갈 수 있는 길(계단 등)을 찾아,
    /// 지금 가야 할 방향(다음 꺾는 점 쪽)을 알려 준다. 이동은 하지 않는다 — 입력을 정하는 쪽(CloneBrain 등)이 그 방향으로 Move를 넣는다.
    /// - 목적지가 움직였거나, 길에서 벗어났거나, 일정 시간이 지나면 길을 다시 찾는다.
    /// - 꺾는 점은 "다음 점이 NavMesh 위에서 곧게 보일 때" 넘긴다 — 모서리를 일찍 꺾어 벽에 비비지 않고, 지나쳐 되돌아가지도 않는다.
    /// - NavMesh 가장자리(벽·난간 쪽)에 너무 붙으면 안쪽으로 살짝 밀어 몸이 벽에 닿지 않게 한다.
    /// - 벽에 비비거나(옆이 닿은 채 앞으로 잘 못 감) 막혀 제자리이면 감지해서: 길을 새로 찾고, 잠깐 벽에서 떨어지며 벽을 따라 미끄러져 빠져나온다.
    ///   SuggestJumpWhenStuck이면 점프도 한 번 권한다(WantsJump — AI 봇용, 분신은 끔).
    /// - 길을 못 찾으면(NavMesh 밖·공중) 목적지로 곧장 향한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavPathFollower : MonoBehaviour
    {
        [Tooltip("길을 다시 찾는 최대 간격(초).")]
        [SerializeField, Min(0.1f)] private float repathInterval = 0.4f;
        [Tooltip("목적지가 이만큼(m) 움직이면 바로 다시 찾는다.")]
        [SerializeField, Min(0.1f)] private float repathMove = 1f;
        [Tooltip("꺾는 점이 곧게 보이지 않아도 이만큼(m, 수평) 다가가면 다음 점으로(멈춤 방지).")]
        [SerializeField, Min(0.05f)] private float cornerReach = 0.3f;
        [Tooltip("이 시간(초) 동안 거의 못 움직이면 막힌 것으로 본다.")]
        [SerializeField, Min(0.2f)] private float stuckTime = 0.6f;
        [Tooltip("옆이 벽에 닿은 채 이 시간(초)을 넘기면 벽에 비비는 것으로 본다.")]
        [SerializeField, Min(0.05f)] private float rubTime = 0.25f;

        private const int LookAheadCorners = 3;    // 한 번에 건너뛰어 볼 꺾는 점 수
        private const float SelfSample = 1.2f;       // 내 자리를 NavMesh에 붙여 볼 반경(m)
        private const float EdgeMargin = 0.15f;      // 몸 반지름보다 이만큼 더 가장자리에서 떨어지려 한다(m)
        private const float GoodProgress = 0.45f;    // 가려는 방향으로 이 비율(걷기 속도 대비) 이상 나가면 잘 가는 중

        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[256];
        private int cornerCount, cornerIndex;
        private Vector3 pathGoal, segStart;
        private float lastRepath = -999f;
        private bool hasPath;
        private bool jumpRequested;

        private CharacterMotor motor;
        private CharacterController body;
        private Vector3 lastPos, lastDir;
        private float lastTime = -1f;
        private Vector3 stuckPos;
        private float stuckSince, rubFor, goodFor;
        private int rubCount;
        private Vector3 recoverDir;
        private float recoverUntil = -1f;

        /// <summary>막혔을 때 점프를 권할지(AI 봇: 켬, 분신: 끔 — 걷기만).</summary>
        public bool SuggestJumpWhenStuck { get; set; } = true;
        /// <summary>지금 길의 남은 거리(m, 길이 없으면 곧장 거리).</summary>
        public float RemainingDistance { get; private set; }
        /// <summary>길을 찾아 따라가는 중(false면 곧장 향하는 중).</summary>
        public bool HasPath => hasPath;
        /// <summary>막혔으니 점프해 보라(읽으면 꺼진다).</summary>
        public bool ConsumeJump() { bool j = jumpRequested; jumpRequested = false; return j; }

        /// <summary>destination으로 가기 위해 지금 향할 수평 방향(정규화). 이미 도착했으면 false. moving = 이 방향으로 걸을 생각인지(막힘 판정용).</summary>
        public bool Steer(Vector3 destination, bool moving, out Vector3 direction)
        {
            path ??= new NavMeshPath();
            if (motor == null) TryGetComponent(out motor);
            if (body == null) TryGetComponent(out body);
            Vector3 pos = transform.position;
            float now = Time.time;
            bool meshReady = RuntimeNavMesh.Ensure();
            var filter = RuntimeNavMesh.Filter;
            NavMeshHit selfHit = default;
            bool onMesh = meshReady && NavMesh.SamplePosition(pos, out selfHit, SelfSample, filter);
            Vector3 navPos = onMesh ? selfHit.position : pos;

            bool blocked = CheckBlocked(pos, moving, now, out bool touching);
            bool repath = blocked
                          || (!hasPath && now - lastRepath >= 0.2f)
                          || (hasPath && now - lastRepath >= repathInterval)
                          || (destination - pathGoal).sqrMagnitude > repathMove * repathMove
                          || OffPath(pos);
            if (repath) Repath(navPos, onMesh, destination, now);

            Vector3 aim = destination;
            if (hasPath)
            {
                AdvanceCorners(pos, navPos, onMesh, filter);
                aim = corners[cornerIndex];
                // 마지막 점(목적지를 NavMesh에 붙인 자리)에 닿으면 실제 목적지 쪽으로
                if (cornerIndex == cornerCount - 1 && Flat(aim - pos).sqrMagnitude < 0.5f * 0.5f) aim = destination;
                RemainingDistance = Flat(aim - pos).magnitude;
                for (int i = cornerIndex; i < cornerCount - 1; i++) RemainingDistance += Vector3.Distance(corners[i], corners[i + 1]);
            }
            else RemainingDistance = Vector3.Distance(pos, destination);

            Vector3 flat = Flat(aim - pos);
            if (flat.sqrMagnitude < 0.01f) { direction = Vector3.zero; lastDir = Vector3.zero; return false; }
            direction = flat.normalized;

            if (blocked) BeginRecover(pos, navPos, onMesh, direction, touching, now);
            if (now < recoverUntil) direction = (recoverDir * 1.2f + direction * 0.4f).normalized;
            else if (onMesh) direction = KeepClearOfEdges(navPos, direction, flat.magnitude, filter);

            lastDir = direction;
            return true;
        }

        /// <summary>길을 잊는다(다음 Steer에서 새로 찾는다).</summary>
        public void ResetPath() { hasPath = false; cornerCount = 0; lastTime = -1f; rubFor = 0f; } // 멈춰 있던 시간은 막힘으로 세지 않는다

        private void Repath(Vector3 from, bool onMesh, Vector3 destination, float now)
        {
            lastRepath = now;
            pathGoal = destination;
            hasPath = false;
            if (!onMesh) return;
            var filter = RuntimeNavMesh.Filter;
            // 목적지가 점프 중이면 공중이라 조금 넓게 찾는다
            if (!NavMesh.SamplePosition(destination, out var to, 2.5f, filter)
                && !NavMesh.SamplePosition(destination, out to, 5f, filter)) return;
            if (!NavMesh.CalculatePath(from, to.position, filter, path) || path.status == NavMeshPathStatus.PathInvalid) return;
            cornerCount = path.GetCornersNonAlloc(corners);
            if (cornerCount < 1) return;
            cornerIndex = cornerCount > 1 ? 1 : 0; // 0번은 지금 자리
            segStart = corners[0];
            hasPath = true;
        }

        /// <summary>
        /// 다음 꺾는 점으로 넘길지: 그 다음 점까지 NavMesh 위로 곧게 갈 수 있으면(모서리를 다 돌았으면) 넘긴다.
        /// 아주 가까이 왔는데도 안 보이면 그래도 넘긴다(제자리 멈춤 방지).
        /// </summary>
        private void AdvanceCorners(Vector3 pos, Vector3 navPos, bool onMesh, NavMeshQueryFilter filter)
        {
            for (int step = 0; step < LookAheadCorners && cornerIndex < cornerCount - 1; step++)
            {
                Vector3 next = corners[cornerIndex + 1];
                bool reached = Flat(corners[cornerIndex] - pos).sqrMagnitude < cornerReach * cornerReach
                               && Mathf.Abs(corners[cornerIndex].y - pos.y) < 1.2f;
                if (!reached && !(onMesh && Level(navPos, corners[cornerIndex], next) && StraightOnMesh(navPos, next, filter))) break;
                segStart = reached ? corners[cornerIndex] : navPos;
                cornerIndex++;
            }
        }

        /// <summary>세 점이 거의 같은 높이(같은 바닥)인가 — 계단·층 사이는 곧게 보이는지 믿지 않고 꺾는 점까지 간다.</summary>
        private static bool Level(Vector3 a, Vector3 b, Vector3 c)
            => Mathf.Abs(a.y - b.y) < 0.6f && Mathf.Abs(a.y - c.y) < 0.6f && Mathf.Abs(b.y - c.y) < 0.6f;

        /// <summary>a에서 b까지 NavMesh 위로 곧게 걸어갈 수 있나(벽·가장자리에 안 걸리고, 다른 층으로 새지 않게 기울기도 본다).</summary>
        private static bool StraightOnMesh(Vector3 a, Vector3 b, NavMeshQueryFilter filter)
        {
            float dy = Mathf.Abs(b.y - a.y);
            float run = Flat(b - a).magnitude;
            if (dy > 0.4f && dy > run * 1.1f) return false; // 바로 위·아래 층: 수평 레이로는 판단 못 함
            if (NavMesh.Raycast(a, b, out var hit, filter)) return false;
            return Mathf.Abs(hit.position.y - b.y) < 1f;
        }

        /// <summary>NavMesh 가장자리(벽·난간)에 몸이 닿을 만큼 붙었으면 안쪽으로 살짝 민다. 가장자리를 따라가는 방향은 그대로 둔다.</summary>
        private Vector3 KeepClearOfEdges(Vector3 navPos, Vector3 dir, float aimDist, NavMeshQueryFilter filter)
        {
            if (aimDist < 0.6f) return dir; // 꺾는 점 바로 앞(가장자리 위에 있는 점)에서는 밀지 않는다
            if (!NavMesh.FindClosestEdge(navPos, out var edge, filter)) return dir;
            float bodyR = body != null ? body.radius : 0.4f;
            float clearance = Mathf.Max(0f, bodyR - RuntimeNavMesh.Radius) + EdgeMargin;
            if (edge.distance >= clearance) return dir;
            Vector3 away = Flat(edge.normal);
            if (away.sqrMagnitude < 1e-4f) return dir;
            away.Normalize();
            float into = Vector3.Dot(dir, -away); // 가장자리 쪽으로 파고드는 정도
            float w = (clearance - edge.distance) / clearance;
            return (dir + away * (0.35f + Mathf.Max(0f, into)) * w).normalized;
        }

        /// <summary>
        /// 막힘 판정: (1) 옆이 벽에 닿은 채 가려는 방향으로 잘 못 나가는 게 rubTime을 넘으면 = 벽에 비빔,
        /// (2) 닿지 않아도 stuckTime 동안 거의 제자리면 = 막힘. 둘 다 한 번 true를 주고 다시 센다.
        /// </summary>
        private bool CheckBlocked(Vector3 pos, bool moving, float now, out bool touching)
        {
            touching = motor != null && motor.HitObstacle;
            bool first = lastTime < 0f;
            float dt = first ? 0f : now - lastTime;
            if (!first && dt <= 0f) return false; // 같은 틱에 두 번 불림
            Vector3 delta = Flat(pos - lastPos);
            lastPos = pos; lastTime = now;
            if (first || !moving || dt > 0.5f || lastDir.sqrMagnitude < 0.5f || (motor != null && !motor.IsGrounded))
            {
                rubFor = 0f; stuckPos = pos; stuckSince = now;
                return false;
            }

            float speed = motor != null && motor.WalkSpeed > 0.1f ? motor.WalkSpeed : 3f;
            float progress = Vector3.Dot(delta, lastDir) / dt; // 가려던 방향으로 실제로 나간 속도
            bool good = progress >= speed * GoodProgress;

            if (touching && !good) rubFor += dt;
            else if (touching) rubFor += dt * 0.5f; // 미끄러지며 가도 오래 닿아 있으면 비비는 것
            else rubFor = Mathf.Max(0f, rubFor - dt * 2f);

            if (good) { goodFor += dt; if (goodFor > 1.5f) rubCount = 0; } else goodFor = 0f;

            bool still = (pos - stuckPos).sqrMagnitude <= 0.25f * 0.25f;
            if (!still) { stuckPos = pos; stuckSince = now; }

            if (rubFor >= rubTime || (still && now - stuckSince >= stuckTime))
            {
                rubFor = 0f; stuckPos = pos; stuckSince = now; goodFor = 0f;
                rubCount++;
                if (SuggestJumpWhenStuck) jumpRequested = true;
                ResetPath();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 막혔을 때 잠깐 빠져나오는 방향: NavMesh 밖으로 밀려나 있으면 NavMesh 쪽으로,
        /// 벽에 닿아 있으면 벽에서 떨어지며 벽을 따라(가려는 쪽에 가까운 방향, 같은 곳에서 거듭 막히면 반대쪽도),
        /// 아무것도 안 닿는데 막혔으면 옆으로 비켜 선다.
        /// </summary>
        private void BeginRecover(Vector3 pos, Vector3 navPos, bool onMesh, Vector3 pathDir, bool touching, float now)
        {
            Vector3 toMesh = Flat(navPos - pos);
            Vector3 dir;
            if (onMesh && toMesh.sqrMagnitude > 0.08f * 0.08f) dir = (toMesh.normalized + pathDir * 0.3f).normalized;
            else if (touching && motor != null && motor.LastObstacleNormal.sqrMagnitude > 0.5f)
            {
                Vector3 n = Flat(motor.LastObstacleNormal).normalized;
                Vector3 tangent = Vector3.Cross(Vector3.up, n);
                float side = Vector3.Dot(tangent, pathDir);
                if (Mathf.Abs(side) < 0.2f) side = 1f;
                if (rubCount % 3 == 0) side = -side; // 같은 곳에서 거듭 막히면 반대쪽으로도 돌아 본다
                dir = (tangent * Mathf.Sign(side) + n * 0.6f).normalized;
            }
            else
            {
                Vector3 side = Vector3.Cross(Vector3.up, pathDir) * (rubCount % 2 == 0 ? 1f : -1f);
                dir = (side - pathDir * 0.3f).normalized;
            }
            recoverDir = dir;
            recoverUntil = now + Mathf.Min(0.25f + 0.12f * rubCount, 0.8f);
        }

        /// <summary>지금 따라가는 구간에서 많이 벗어났나(밀려남·떨어짐).</summary>
        private bool OffPath(Vector3 pos)
        {
            if (!hasPath || cornerIndex == 0) return false;
            Vector3 a = segStart, b = corners[cornerIndex];
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 closest = a + ab * t;
            return Flat(pos - closest).sqrMagnitude > 2f * 2f || Mathf.Abs(pos.y - closest.y) > 2.5f;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

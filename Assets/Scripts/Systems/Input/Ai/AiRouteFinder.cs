using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI의 길 찾기(상대에게 가는 길): 어느 쪽으로 걸을지와 언제 점프할지를 정한다. 키는 누르지 않는다(AiMeleeTactics가 Move·Jump로 넣음).
    /// - 곧장: 나 → 상대 사이가 가슴 높이에서 막히지 않았으면 그쪽으로 간다(창틀처럼 낮은 턱만 있으면 그대로 가서 넘는다).
    /// - 창틀 찾기: 가슴 높이가 벽에 막혔으면 상대 쪽 ±70° 안에서 "발목 높이는 막혔는데 가슴 높이는 뚫린 곳"(창틀)을 찾아
    ///   (거기까지 + 거기서 상대까지)가 가장 짧은 창틀로 간다. 돌아가지 않고 창틀을 넘어 쫓는다.
    /// - 창틀이 없으면 NavMesh 길(NavPathFollower — 이 봇에 따로 붙임, 공장장 분신 것은 건드리지 않음)로 문·계단을 돌아간다.
    /// - 턱 넘기(WantsJump): 가는 방향 바로 앞에 키의 25~110% 높이 턱(창틀·낮은 담·책상)이 있고 그 위가 뚫렸으면 점프 —
    ///   그러면 CharacterTraversal이 플레이어와 같은 규칙(방향키 + 점프)으로 창틀 넘기·올라서기를 한다. 계단 한 칸은 넘지 않는다.
    /// - 막힘: 걸으려는데 한동안 거의 못 움직였으면 한 번 점프해 보고, 잠깐 NavMesh 길로 돌린다.
    /// </summary>
    public sealed class AiRouteFinder
    {
        private const float ScanInterval = 0.4f, WindowMemory = 3f, ScanRange = 12f;

        private readonly Transform tr;
        private readonly CharacterController cc;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private NavPathFollower nav;

        private bool hasWindow;
        private Vector3 windowPoint, windowDir;
        private float windowUntil, nextScan, navUntil, lastJump = -9f;
        private Vector3 stuckPos;
        private float stuckSince = -1f;

        /// <summary>곧장 가지 않고 돌아가는 중(창틀 쪽 또는 NavMesh 길) — 이때는 가는 방향을 본다.</summary>
        public bool Detouring { get; private set; }

        public AiRouteFinder(GameObject self)
        {
            tr = self.transform;
            self.TryGetComponent(out cc);
        }

        private float Height => cc != null ? cc.height : 1.8f;
        private float Radius => cc != null ? cc.radius : 0.35f;

        /// <summary>target까지 지금 걸을 수평 방향(정규화)과 점프 여부. moving = 걷는 입력을 넣을 생각인지(막힘 판정용).</summary>
        public Vector3 Steer(Vector3 target, float now, bool moving, bool grounded, out bool jump)
        {
            jump = false;
            Vector3 pos = tr.position;
            Vector3 toT = target - pos; toT.y = 0f;
            float dist = toT.magnitude;
            Vector3 straight = dist > 1e-3f ? toT / dist : tr.forward;
            float h = Height;

            bool stuck = CheckStuck(pos, moving, now);
            if (stuck) { navUntil = now + 2f; hasWindow = false; if (grounded) jump = true; }

            Vector3 dir = straight;
            Detouring = false;
            bool direct = !Blocked(pos + Vector3.up * (h * 0.85f), straight, dist, out _);
            if (!direct || now < navUntil)
            {
                if (!direct && now >= nextScan) { nextScan = now + ScanInterval; ScanWindows(pos, straight, target, h); }
                if (hasWindow && now < windowUntil)
                {
                    Vector3 toW = windowPoint - pos; toW.y = 0f;
                    // 창틀에 가까우면 창틀을 똑바로 보고 밀고 들어간다(그래야 넘기가 시작된다).
                    dir = toW.magnitude < Radius + 1.2f ? windowDir : toW.normalized;
                    Detouring = true;
                }
                else if (!direct || now < navUntil)
                {
                    hasWindow = false;
                    if (nav == null && !tr.TryGetComponent(out nav)) nav = tr.gameObject.AddComponent<NavPathFollower>();
                    if (nav.Steer(target, moving, out var navDir)) { dir = navDir; Detouring = true; }
                    if (nav.ConsumeJump() && grounded) jump = true;
                }
            }
            else hasWindow = false;

            if (grounded && moving && WantsJump(pos, dir, h)) jump = true;
            if (jump)
            {
                if (now - lastJump < 0.6f) jump = false;
                else lastJump = now;
            }
            return dir;
        }

        /// <summary>앞에 넘을 만한 턱(창틀·낮은 담·책상: 키의 25~110%, 위가 뚫림)이 있는지. 계단 한 칸(키의 25% 미만)은 아니다.</summary>
        private bool WantsJump(Vector3 pos, Vector3 dir, float h)
        {
            float reach = Radius + 0.6f;
            if (!Blocked(pos + Vector3.up * (h * 0.15f), dir, reach, out var low) || low.normal.y > 0.5f) return false;
            // 턱 윗면 높이: 맞은 곳 조금 안쪽을 위에서 내려다본다.
            Vector3 probe = low.point + dir * 0.1f; probe.y = pos.y + h * 1.2f;
            if (!Blocked(probe, Vector3.down, h * 1.2f, out var top)) return false;
            float above = top.point.y - pos.y;
            if (above < h * 0.25f || above > h * 1.1f) return false;
            // 턱 위가 뚫려 있어야(창문·담 위) 넘거나 올라선다.
            return !Blocked(pos + Vector3.up * (above + 0.5f), dir, reach + 0.4f, out _);
        }

        /// <summary>상대 쪽 ±70°에서 발목은 막히고 가슴은 뚫린 곳(창틀)을 찾는다. 가장 빨리 상대에게 닿는 곳을 고른다.</summary>
        private void ScanWindows(Vector3 pos, Vector3 straight, Vector3 target, float h)
        {
            float best = float.MaxValue;
            bool found = false;
            for (float a = -70f; a <= 70.01f; a += 7f)
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * straight;
                if (!Blocked(pos + Vector3.up * (h * 0.15f), d, ScanRange, out var low) || low.normal.y > 0.5f) continue;
                float wallDist = low.distance;
                if (Blocked(pos + Vector3.up * (h * 0.85f), d, wallDist + 1.0f, out _)) continue; // 가슴 높이도 막힘 = 그냥 벽
                Vector3 beyond = low.point + d * 1.0f;
                Vector3 rest = target - beyond; rest.y = 0f;
                float cost = wallDist + rest.magnitude;
                if (cost < best)
                {
                    best = cost; found = true;
                    windowPoint = low.point;
                    Vector3 n = -low.normal; n.y = 0f;
                    windowDir = n.sqrMagnitude > 1e-3f ? n.normalized : d;
                }
            }
            if (found) { hasWindow = true; windowUntil = Time.time + WindowMemory; }
        }

        private bool CheckStuck(Vector3 pos, bool moving, float now)
        {
            if (!moving) { stuckSince = -1f; return false; }
            if (stuckSince < 0f || (pos - stuckPos).sqrMagnitude > 0.3f * 0.3f) { stuckSince = now; stuckPos = pos; return false; }
            if (now - stuckSince < 0.9f) return false;
            stuckSince = now; stuckPos = pos;
            return true;
        }

        /// <summary>origin에서 dir로 distance 안에 막는 것(캐릭터·트리거·내 몸 제외)이 있는지.</summary>
        private bool Blocked(Vector3 origin, Vector3 dir, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int n = Physics.RaycastNonAlloc(origin, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(tr) || c.GetComponentInParent<CharacterIdentity>() != null) continue;
                if (hits[i].distance < best) { best = hits[i].distance; nearest = hits[i]; }
            }
            return best < float.MaxValue;
        }
    }
}

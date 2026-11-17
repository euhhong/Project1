using Project1.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Project1.Systems
{
    /// <summary>
    /// AI의 길 찾기(상대에게 가는 길): 어느 쪽으로 걸을지와 언제 점프할지를 정한다. 키는 누르지 않는다(AiMeleeTactics가 Move·Jump로 넣음).
    /// - 곧장: 나 → 상대 사이가 가슴 높이에서 막히지 않았으면 그쪽으로 간다(창틀처럼 낮은 턱만 있으면 그대로 가서 넘는다).
    /// - 창틀 찾기: 가슴 높이가 벽에 막혔으면 상대 쪽 ±80° 안에서 "발목 높이는 막혔는데 가슴 높이는 뚫린 곳"(창틀)을 찾아
    ///   (거기까지 + 거기서 상대까지)가 NavMesh로 돌아가는 길보다 짧으면(VaultBias만큼 길어도) 창틀 가운데로 가서 넘는다.
    /// - 창틀이 없거나 돌아가는 게 훨씬 짧으면 NavMesh 길(NavPathFollower — 이 봇에 따로 붙임, 공장장 분신 것은 건드리지 않음)로 문·계단을 돌아간다.
    /// - 몸 비켜 가기: 가는 방향으로 몸 크기(캡슐)를 미리 쏴서 상자·벽 모서리에 걸릴 것 같으면 가까운 쪽으로 조금씩 꺾어 비켜 간다.
    ///   앞이 넘을 수 있는 것(창틀·걸친 판자)이면 비키지 않고 넘는다. 낮은 상자는 조금만 비켜 갈 수 있으면 비키고, 아니면 올라선다.
    /// - 턱 넘기: 가는 방향 앞에 넘을 턱(창틀·걸친 판자·키의 25~110% 낮은 담·상자)이 있으면 그 면을 똑바로 보고 밀고 들어가며 점프 —
    ///   그러면 CharacterTraversal이 플레이어와 같은 규칙(방향키 + 점프)으로 창틀 넘기·판자 넘기·올라서기를 한다. 계단 한 칸은 넘지 않는다.
    /// - 막힘: 벽에 닿은 채 앞으로 잘 못 가거나(0.3초) 제자리걸음(0.6초)이면 빨리 알아채고 차례로
    ///   벽을 따라 미끄러져 비켜 가기 → 반대쪽으로 + 점프 → NavMesh 길로 바꾸기를 해 본다. 넘으려다 실패한 턱은 잠깐 넘으려 하지 않는다.
    /// </summary>
    public sealed class AiRouteFinder
    {
        private const float ScanInterval = 0.35f, WindowMemory = 3f, ScanRange = 14f;
        private const float VaultBias = 2f;       // 창틀로 넘는 길이 NavMesh로 돌아가는 길보다 이만큼(m)까지 길어도 넘는다(과감하게)
        private const float AlignReach = 1.2f;    // 몸 둘레에서 이 거리 안의 턱은 똑바로 보고 들어간다
        private const float JumpReach = 0.55f;    // 몸 둘레에서 이 거리 안이면 점프(CharacterTraversal 창틀 0.7·올라서기 0.9 안)
        private const float LookAhead = 1.6f;     // 몸이 걸릴지 미리 보는 거리(m)
        private static readonly float[] AvoidAngles = { 12f, 24f, 36f, 50f, 65f, 80f };

        private readonly Transform tr;
        private CharacterController cc;
        private CharacterMotor motor;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private NavPathFollower nav;
        private NavMeshPath navPath;
        private readonly Vector3[] navCorners = new Vector3[64];

        // 창틀
        private bool hasWindow;
        private Vector3 windowPoint, windowDir;
        private float windowUntil, nextScan;

        // NavMesh 길로 돌리는 시간, 점프
        private float navUntil, lastJump = -9f;
        private Collider lastJumpAt, badHurdle;
        private float badHurdleUntil;

        // 막힘 감지 · 빠져나오기
        private Vector3 lastPos, lastDir, stillPos;
        private float lastTime = -1f, stillSince, rubFor, goodFor;
        private int blockCount;
        private Vector3 recoverDir;
        private float recoverUntil = -1f, recoverSide = 1f;
        private float avoidSide;

        private struct Hurdle { public Collider Collider; public Vector3 Point, Normal; public bool Vault; }

        /// <summary>곧장 가지 않고 돌아가거나 비켜 가는 중(창틀 쪽·NavMesh 길·비켜 가기·턱 마주 보기) — 이때는 가는 쪽을 본다.</summary>
        public bool Detouring { get; private set; }

        public AiRouteFinder(GameObject self)
        {
            tr = self.transform;
        }

        private void RefreshBody()
        {
            if (cc == null) tr.TryGetComponent(out cc);
            if (motor == null) tr.TryGetComponent(out motor);
        }

        private float Height => cc != null ? cc.height : 1.8f;
        private float Radius => cc != null ? cc.radius : 0.35f;

        /// <summary>옆으로 벽·상자(캐릭터 아님)에 닿아 있다.</summary>
        public bool TouchingWall
        {
            get
            {
                RefreshBody();
                if (motor == null || !motor.HitObstacle) return false;
                var c = motor.LastObstacle;
                return c != null && c.GetComponentInParent<CharacterIdentity>() == null;
            }
        }

        /// <summary>나 → target 사이 걷는 길(무릎·가슴 높이)에 벽·상자·창틀이 있다(캐릭터 제외).</summary>
        public bool ObstacleBetween(Vector3 target)
        {
            RefreshBody();
            Vector3 pos = tr.position;
            Vector3 to = target - pos; to.y = 0f;
            float d = to.magnitude - 0.3f;
            if (d <= 0.1f) return false;
            to.Normalize();
            float h = Height;
            return Blocked(pos + Vector3.up * (h * 0.3f), to, d, out _) || Blocked(pos + Vector3.up * (h * 0.8f), to, d, out _);
        }

        /// <summary>target까지 지금 걸을 수평 방향(정규화)과 점프 여부. moving = 걷는 입력을 넣을 생각인지(막힘 판정용).</summary>
        public Vector3 Steer(Vector3 target, float now, bool moving, bool grounded, out bool jump)
        {
            jump = false;
            RefreshBody();
            Vector3 pos = tr.position;
            Vector3 toT = target - pos; toT.y = 0f;
            float dist = toT.magnitude;
            Vector3 straight = dist > 1e-3f ? toT / dist : Flat(tr.forward).normalized;
            float h = Height, r = Radius;
            Detouring = false;

            // 1) 길 고르기: 곧장 / 창틀 / NavMesh
            bool direct = !Blocked(pos + Vector3.up * (h * 0.85f), straight, dist, out _);
            if (direct) hasWindow = false;
            else if (now >= nextScan) { nextScan = now + ScanInterval; ScanWindows(pos, straight, target, h); }
            if (hasWindow && Vector3.Dot(Flat(pos - windowPoint), windowDir) > r + 0.2f) hasWindow = false; // 이미 넘어왔다

            bool forcedNav = now < navUntil;
            Vector3 dir = straight;
            float maxAvoid = 80f;
            bool pushingWindow = false, navMode = false;
            if (hasWindow && now < windowUntil && !forcedNav)
            {
                Vector3 toW = Flat(windowPoint - pos);
                // 창틀에 가까우면 창틀을 똑바로 보고 밀고 들어간다(그래야 넘기가 시작된다).
                pushingWindow = toW.magnitude < r + 1.3f;
                dir = pushingWindow ? windowDir : toW.normalized;
                maxAvoid = 60f;
                Detouring = true;
            }
            else if (!direct || forcedNav)
            {
                hasWindow = false;
                if (nav == null && !tr.TryGetComponent(out nav)) nav = tr.gameObject.AddComponent<NavPathFollower>();
                if (nav.Steer(target, moving, out var navDir)) { dir = navDir; Detouring = true; navMode = true; }
                if (nav.ConsumeJump() && grounded) jump = true;
                maxAvoid = 40f;
            }

            // 2) 막힘: 빨리 알아채고 비켜 가기 → 반대쪽 + 점프 → NavMesh 길
            if (CheckBlocked(pos, moving, grounded, now)) OnBlocked(dir, now, grounded, navMode, ref jump);
            bool recovering = now < recoverUntil && !navMode;
            if (recovering) { dir = (recoverDir * 1.2f + dir * 0.35f).normalized; Detouring = true; }

            // 3) 몸이 상자·벽 모서리에 걸릴 것 같으면 미리 비켜 간다(넘을 것은 그대로)
            if (!pushingWindow && !recovering && moving)
            {
                Vector3 avoided = Avoid(pos, dir, Mathf.Clamp(dist, 0.6f, LookAhead), maxAvoid, h, r, now);
                if (avoided != dir) { dir = avoided; Detouring = true; }
            }

            // 4) 앞의 턱(창틀·걸친 판자·낮은 담·상자): 면을 똑바로 보고 밀고 들어가며 점프 — CharacterTraversal이 넘기·올라서기를 한다.
            if (grounded && moving && ProbeHurdle(pos, dir, h, r + AlignReach, now, out var hz))
            {
                Vector3 push = -hz.Normal;
                if (Vector3.Angle(dir, push) < 70f)
                {
                    dir = push; Detouring = true;
                    float face = Vector3.Dot(Flat(hz.Point - pos), push);
                    if (face <= r + JumpReach) { jump = true; lastJumpAt = hz.Collider; }
                }
            }

            if (jump)
            {
                if (now - lastJump < 0.6f) jump = false;
                else lastJump = now;
            }
            lastDir = dir;
            return dir;
        }

        // ───────────────────────── 턱 ─────────────────────────

        /// <summary>
        /// dir 앞 reach 안의 넘을 턱: 걸친 판자(넘기), 창틀(키의 25~75%, 얇고 위가 뚫림 — 넘기), 낮은 담·상자(키의 25~110%, 위가 뚫림 — 올라서기).
        /// 계단 한 칸(키의 25% 미만)·높은 벽은 아니다. 방금 넘으려다 실패한 것·잡지 않기로 표시한 것(NoLedgeGrab)·계단은 뺀다.
        /// </summary>
        private bool ProbeHurdle(Vector3 pos, Vector3 dir, float h, float reach, float now, out Hurdle hz)
        {
            hz = default;
            if (!Blocked(pos + Vector3.up * (h * 0.15f), dir, reach, out var low) || low.normal.y > 0.5f) return false;
            Vector3 n = Flat(low.normal);
            if (n.sqrMagnitude < 0.25f) return false;
            n.Normalize();
            if ((low.collider == badHurdle && now < badHurdleUntil) || Stair(low.collider) || low.collider.GetComponentInParent<NoLedgeGrab>() != null) return false;
            hz = new Hurdle { Collider = low.collider, Point = low.point, Normal = n };

            // 넘어가 걸친 판자: 판자 넘기(스페이스바)
            var pallet = low.collider.GetComponentInParent<Pallet>();
            if (pallet != null) { hz.Vault = true; return pallet.State == Pallet.PalletState.Down; }

            // 턱 윗면 높이: 맞은 곳 조금 안쪽을 위에서 내려다본다(벽이 1.2배 키보다 높으면 벽 안에서 쏘게 되어 안 맞는다 = 턱 아님).
            Vector3 probe = low.point - n * 0.1f; probe.y = pos.y + h * 1.2f;
            if (!Blocked(probe, Vector3.down, h * 1.2f, out var top)) return false;
            float above = top.point.y - pos.y;
            if (above < h * 0.25f || above > h * 1.1f) return false;
            // 턱 위가 뚫려 있어야(창문·담 위) 넘거나 올라선다.
            if (Blocked(pos + Vector3.up * (above + 0.5f), dir, low.distance + 0.5f, out _)) return false;
            // 창틀: 얇다(건너편에서 거꾸로 쏘면 뒷면이 맞는다) + 키의 75% 이하
            Vector3 far = low.point - n * 0.95f; far.y = top.point.y - 0.08f;
            hz.Vault = above <= h * 0.75f && Blocked(far, n, 0.95f, out _);
            return true;
        }

        // ───────────────────────── 몸 비켜 가기 ─────────────────────────

        /// <summary>dir로 몸(캡슐)이 look 안에 걸리면 가까운 쪽으로 꺾은 방향. 넘을 턱(창틀·판자)이면 그대로, 낮은 상자는 조금만 비켜 보고 안 되면 그대로(올라섬).</summary>
        private Vector3 Avoid(Vector3 pos, Vector3 dir, float look, float maxAngle, float h, float r, float now)
        {
            if (!BodyCast(pos, dir, look, h, r, out var hit)) { avoidSide = 0f; return dir; }
            bool hurdle = ProbeHurdle(pos, dir, h, hit.distance + r + 0.4f, now, out var hz);
            if (hurdle && hz.Vault) return dir;
            float limit = hurdle ? 36f : maxAngle;

            // 먼저 해 볼 쪽: 지난번 비킨 쪽, 아니면 장애물 반대쪽
            float side = avoidSide;
            if (side == 0f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, dir);
                float lateral = Vector3.Dot(Flat(hit.point - pos), right);
                side = Mathf.Abs(lateral) > 0.05f ? -Mathf.Sign(lateral) : (Vector3.Dot(right, hit.normal) >= 0f ? 1f : -1f);
            }
            foreach (float a in AvoidAngles)
            {
                if (a > limit) break;
                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? side : -side;
                    Vector3 d = Quaternion.Euler(0f, a * s, 0f) * dir;
                    if (BodyCast(pos, d, look, h, r, out _)) continue;
                    avoidSide = s;
                    return d;
                }
            }
            return dir;
        }

        /// <summary>몸 캡슐(계단 한 칸 위 ~ 머리)을 dir로 쏜다. 처음부터 겹친 것·캐릭터·내 몸·계단은 뺀다.</summary>
        private bool BodyCast(Vector3 pos, Vector3 dir, float distance, float h, float r, out RaycastHit nearest)
        {
            nearest = default;
            float cr = r * 0.9f;
            float step = cc != null ? cc.stepOffset : 0.3f;
            Vector3 p1 = pos + Vector3.up * (step + cr + 0.05f);
            Vector3 p2 = pos + Vector3.up * Mathf.Max(step + cr + 0.05f, h - cr);
            int n = Physics.CapsuleCastNonAlloc(p1, p2, cr, dir, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (hits[i].distance <= 0f || Mathf.Abs(hits[i].normal.y) > 0.7f) continue; // 처음부터 겹침·바닥/천장
                if (c.transform.IsChildOf(tr) || c.GetComponentInParent<CharacterIdentity>() != null || Stair(c)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; nearest = hits[i]; }
            }
            return best < float.MaxValue;
        }

        private const string StairRampName = "Ramp_Collider"; // MapBlockoutBuilder.Stair()가 단 모서리를 잇는 보이지 않는 경사

        /// <summary>블록아웃 계단(경사 자신, 또는 형제에 경사가 있는 단) — 걸어 오르니 비켜 가거나 넘을 것이 아니다.</summary>
        private static bool Stair(Collider c)
        {
            if (c.name == StairRampName) return true;
            Transform p = c.transform.parent;
            return p != null && p.Find(StairRampName) != null;
        }

        // ───────────────────────── 창틀 찾기 ─────────────────────────

        /// <summary>상대 쪽 ±80°에서 발목은 막히고 가슴은 뚫린 곳(창틀)을 찾는다. 가장 빨리 상대에게 닿는 곳을 고르고, NavMesh로 돌아가는 게 훨씬 짧지 않으면 그 창틀로 간다.</summary>
        private void ScanWindows(Vector3 pos, Vector3 straight, Vector3 target, float h)
        {
            float best = float.MaxValue;
            bool found = false;
            Vector3 bestPoint = default, bestNormal = default;
            for (float a = -80f; a <= 80.01f; a += 4f)
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
                    bestPoint = low.point;
                    Vector3 n = Flat(low.normal);
                    bestNormal = n.sqrMagnitude > 1e-3f ? n.normalized : -d;
                }
            }
            if (found && best > NavLength(pos, target) + VaultBias) found = false; // 돌아가는 길이 훨씬 짧다
            if (!found) { hasWindow = false; return; }
            windowPoint = CenterOfOpening(pos, bestPoint, bestNormal, h);
            windowDir = -bestNormal;
            hasWindow = true;
            windowUntil = Time.time + WindowMemory;
        }

        /// <summary>창틀 맞은 곳에서 벽을 따라 양옆으로 재서 창 틈(아래는 막히고 가슴은 뚫림) 가운데로 옮긴다 — 창문 옆 기둥에 걸리지 않게.</summary>
        private Vector3 CenterOfOpening(Vector3 pos, Vector3 point, Vector3 n, float h)
        {
            Vector3 along = Vector3.Cross(Vector3.up, n);
            if (along.sqrMagnitude < 1e-4f) return point;
            along.Normalize();
            Vector3 front = point + n * 0.6f;
            bool Open(float s)
            {
                Vector3 o = front + along * s;
                Vector3 lo = new(o.x, pos.y + h * 0.15f, o.z), hi = new(o.x, pos.y + h * 0.8f, o.z);
                return Blocked(lo, -n, 1.0f, out _) && !Blocked(hi, -n, 1.2f, out _);
            }
            if (!Open(0f)) return point;
            float left = 0f, right = 0f;
            for (float s = 0.2f; s <= 2.4f && Open(s); s += 0.2f) right = s;
            for (float s = 0.2f; s <= 2.4f && Open(-s); s += 0.2f) left = s;
            return point + along * ((right - left) * 0.5f);
        }

        /// <summary>NavMesh로 걸어가는 길의 길이(m). 길이 없으면 아주 큰 값.</summary>
        private float NavLength(Vector3 from, Vector3 to)
        {
            if (!RuntimeNavMesh.Ensure()) return float.MaxValue;
            var filter = RuntimeNavMesh.Filter;
            if (!NavMesh.SamplePosition(from, out var a, 1.2f, filter) || !NavMesh.SamplePosition(to, out var b, 2.5f, filter)) return float.MaxValue;
            navPath ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(a.position, b.position, filter, navPath) || navPath.status != NavMeshPathStatus.PathComplete) return float.MaxValue;
            int count = navPath.GetCornersNonAlloc(navCorners);
            if (count < 1) return float.MaxValue;
            float len = 0f;
            for (int i = 1; i < count; i++) len += Vector3.Distance(navCorners[i - 1], navCorners[i]);
            return len;
        }

        // ───────────────────────── 막힘 ─────────────────────────

        /// <summary>벽에 닿은 채 가려는 쪽으로 잘 못 나감(0.3초) 또는 제자리걸음(0.6초)이면 한 번 true(다시 센다).</summary>
        private bool CheckBlocked(Vector3 pos, bool moving, bool grounded, float now)
        {
            bool first = lastTime < 0f;
            float dt = first ? 0f : now - lastTime;
            if (!first && dt <= 0f) return false;
            Vector3 delta = Flat(pos - lastPos);
            lastPos = pos; lastTime = now;
            if (first || !moving || !grounded || dt > 0.5f || lastDir.sqrMagnitude < 0.5f)
            {
                rubFor = 0f; stillPos = pos; stillSince = now;
                return false;
            }
            float walk = motor != null && motor.WalkSpeed > 0.1f ? motor.WalkSpeed : 3f;
            bool good = Vector3.Dot(delta, lastDir) / dt >= walk * 0.45f;
            if (!good && TouchingWall) rubFor += dt;
            else rubFor = Mathf.Max(0f, rubFor - dt * 2f);
            if (good) { goodFor += dt; if (goodFor > 1.5f) blockCount = 0; } else goodFor = 0f;
            if ((pos - stillPos).sqrMagnitude > 0.3f * 0.3f) { stillPos = pos; stillSince = now; }
            if (rubFor < 0.3f && now - stillSince < 0.6f) return false;
            rubFor = 0f; stillPos = pos; stillSince = now; goodFor = 0f;
            return true;
        }

        /// <summary>막혔을 때: 1번째 = 벽을 따라 가까운 쪽으로 미끄러짐, 2번째 = 반대쪽 + 점프, 3번째부터 = NavMesh 길로 돌리고 점프.</summary>
        private void OnBlocked(Vector3 dir, float now, bool grounded, bool navMode, ref bool jump)
        {
            blockCount++;
            if (lastJumpAt != null && now - lastJump < 1.5f) { badHurdle = lastJumpAt; badHurdleUntil = now + 4f; } // 넘으려다 막힘 = 못 넘는 턱
            lastJumpAt = null;
            avoidSide = 0f;

            if (blockCount >= 3 || navMode)
            {
                if (!navMode || blockCount >= 3) { navUntil = now + 2.5f; hasWindow = false; if (nav != null) nav.ResetPath(); }
                if (grounded && blockCount >= 2) jump = true;
                if (blockCount >= 5) { blockCount = 0; navUntil = 0f; recoverSide = -recoverSide; } // NavMesh로도 안 되면 처음부터 다시(반대쪽부터)
                return;
            }

            Vector3 n = motor != null && TouchingWall ? Flat(motor.LastObstacleNormal) : Vector3.zero;
            float side;
            if (blockCount == 1)
            {
                Vector3 tangent = n.sqrMagnitude > 0.25f ? Vector3.Cross(Vector3.up, n.normalized) : Vector3.Cross(Vector3.up, dir);
                float t = Vector3.Dot(tangent, dir);
                side = Mathf.Abs(t) > 0.15f ? Mathf.Sign(t) : recoverSide;
            }
            else { side = -recoverSide; if (grounded) jump = true; }
            recoverSide = side;

            if (n.sqrMagnitude > 0.25f)
            {
                n.Normalize();
                recoverDir = (Vector3.Cross(Vector3.up, n) * side + n * 0.5f).normalized; // 벽에서 살짝 떨어지며 벽을 따라
            }
            else recoverDir = (Vector3.Cross(Vector3.up, dir) * side - dir * 0.3f).normalized; // 닿은 건 없는데 제자리 = 옆으로 비켜 섬
            recoverUntil = now + (blockCount == 1 ? 0.4f : 0.55f);
        }

        // ───────────────────────── 공용 ─────────────────────────

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

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

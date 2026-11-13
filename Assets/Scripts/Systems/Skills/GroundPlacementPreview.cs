using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 좌클릭을 꾹 눌러 땅 위에 무언가를 놓을 자리 고르기(미리보기) — 붉은나비 나비, 안토니오 음표가 같이 쓴다(BE).
    /// - 마우스 커서가 가리키는 곳을 따라간다. 땅 위만 된다(면이 위를 향함, 공중·벽 불가), 너무 멀면 안 됨.
    /// - 몸 근처(cancelRadius)로 끌어다 놓으면 "취소" 자리(Cancels).
    /// - orbitWhileTurning을 끄면(안토니오): 시점을 돌리는 동안에도 화면 가운데가 가리키는 땅을 자유롭게 따라간다(거리 고정 없음).
    /// - 우클릭으로 시점을 돌리면(커서가 아님): 캐릭터를 원의 가운데로, 자리까지 거리를 반지름으로 두고 시야 기준 방향을 지켜 시점을 따라 돈다(그 자리 바닥에 붙음).
    ///   시점을 돌리는 중에 새로 고르기 시작하면 화면 가운데가 가리키는 땅, 없으면 앞쪽 defaultDistance m.
    /// - (붉은나비) 위층 바닥 끝 모서리 잡기(ledgeSnapHeight), 위로 높이차 제한(maxRiseHeight), 설 공간 검사(requireStandSpace),
    ///   벽 너머 놓기(seeThroughWalls — 보이는지는 따지지 않고 설 공간만 본다).
    /// </summary>
    [Serializable]
    public class GroundPlacementPreview
    {
        [Tooltip("땅으로 볼 면의 기울기(면 방향의 위쪽 성분). 이보다 낮으면(벽 등) 못 놓는다.")]
        [Range(0f, 1f)] public float minGroundNormalY = 0.7f;
        [Tooltip("놓을 수 있는 최대 거리(m, 수평).")]
        [Min(1f)] public float maxPlaceDistance = 40f;
        [Tooltip("몸 근처(수평 거리, m)로 끌어다 놓으면 놓기를 취소한다.")]
        [Min(0f)] public float cancelRadius = 1.5f;
        [Tooltip("시점을 돌리는 중에 새로 꺼냈는데 화면 가운데가 땅을 가리키지 않으면(하늘 등) 앞쪽 이 거리(m)에 꺼낸다.")]
        [Min(0f)] public float defaultDistance = 6f;
        [Tooltip("바닥을 찾을 때 위·아래로 살피는 거리(m).")]
        [Min(0.1f)] public float groundProbeUp = 3f, groundProbeDown = 8f;
        [Tooltip("우클릭으로 시점을 돌릴 때: 켜면 캐릭터 둘레로 거리를 지켜 돈다(붉은나비), 끄면 화면 가운데가 가리키는 땅을 자유롭게 따라간다(안토니오).")]
        public bool orbitWhileTurning = true;
        [Tooltip("최대 거리보다 먼 곳을 가리키면: 켜면 그 방향 최대 거리 자리로 당겨 놓는다(붉은나비), 끄면 못 놓는 자리.")]
        public bool clampToMax;
        [Tooltip("우클릭으로 시점을 돌리는 중에 고르기 시작했을 때만(안토니오): 마우스로 자리를 직접 민다 — 위·아래 = 멀리·가까이(dragDistancePerDegree), 좌·우 = 시점보다 조금 더 돈다(dragLateralGain). 좌클릭을 먼저 누르고 시점을 돌리면 예전대로(화면 가운데를 따라감).")]
        public bool dragWhileTurning;
        [Min(0f)] public float dragDistancePerDegree = 0.25f;
        [Min(1f)] public float dragLateralGain = 1.25f;
        [Tooltip("턱 끝 잡기: 가리킨 곳이 바닥 판의 옆면·밑면이면(아래층에서 위층 바닥 끝을 겨눔 — 윗면이 안 보임) 그 위 이 높이(m) 안의 윗면으로 올려 붙인다. 0 = 끔.")]
        [Min(0f)] public float ledgeSnapHeight;
        [Tooltip("턱 끝 잡기에서 옆면 안쪽으로 들어가 바닥을 찾는 거리(m).")]
        [Min(0.01f)] public float ledgeSnapInset = 0.3f;
        [Tooltip("놓을 자리가 캐릭터 발보다 이만큼(m) 넘게 높으면 못 놓는다. 0 = 제한 없음.")]
        [Min(0f)] public float maxRiseHeight;
        [Tooltip("켜면 캐릭터가 설 공간(standRadius·standHeight 캡슐 크기 빈 곳 + 바닥)이 있어야 놓는다. 벽에 너무 붙은 자리는 빈 곳으로 살짝 옮긴다. 캐릭터·트리거는 공간을 막지 않는다.")]
        public bool requireStandSpace;
        [Min(0.05f)] public float standRadius = 0.35f, standHeight = 1.8f;
        [Tooltip("켜면 가리킨 곳이 놓을 수 없는 면(벽 등)일 때 그 광선을 벽 너머로 이어 처음으로 설 공간이 있는 바닥에 놓는다(보이지 않는 곳도 된다). 입력에 광선 출발점(HasAimRay)이 있어야 한다.")]
        public bool seeThroughWalls;
        [Min(1f)] public float seeThroughDistance = 150f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly RaycastHit[] BeyondHits = new RaycastHit[64];
        private static readonly Collider[] SpaceHits = new Collider[16];
        private static readonly float[] NudgeScales = { 0.6f, 1.2f };
        private static readonly Vector3[] NudgeDirs =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(0.7071f, 0f, 0.7071f), new Vector3(-0.7071f, 0f, 0.7071f), new Vector3(0.7071f, 0f, -0.7071f), new Vector3(-0.7071f, 0f, -0.7071f),
        };
        private bool set, viewLocked;
        private Vector3 viewLocal;
        private float lastAimYaw;
        private bool dragInit; private float dragDistance, dragAngle;
        private bool started; // 이번 고르기의 첫 Update를 했는지

        /// <summary>이번 고르기를 시점을 돌리는 중(우클릭, 커서 아님)에 시작했는지.</summary>
        public bool StartedWhileTurning { get; private set; }

        public Vector3 Point { get; private set; }
        public bool Valid { get; private set; }
        public bool Cancels { get; private set; }

        /// <summary>새로 고르기 시작(지난 자리는 버린다).</summary>
        public void Begin() { Valid = false; Cancels = false; set = false; viewLocked = false; dragInit = false; started = false; StartedWhileTurning = false; }

        /// <summary>고르기를 끝냄(놓았거나 취소).</summary>
        public void Clear() { Valid = false; Cancels = false; }

        public void Update(Transform owner, in CharacterInputFrame input)
        {
            Quaternion view = Quaternion.Euler(0f, input.LookYaw, 0f);
            bool turning = !(input.HasAimPoint && input.AimFromCursor);
            if (!started) { started = true; StartedWhileTurning = turning; }
            if (dragWhileTurning && StartedWhileTurning && turning) { Drag(owner, input); return; }
            dragInit = false;
            if (!orbitWhileTurning && input.HasAimPoint)
            {
                // 자유롭게: 커서든 화면 가운데든 가리키는 땅을 그대로 따라간다(거리 고정 없음)
                EvaluateAim(owner, input);
                return;
            }
            if (!(input.HasAimPoint && input.AimFromCursor))
            {
                if (!set)
                {
                    if (input.HasAimPoint) EvaluateAim(owner, input);
                    else
                    {
                        Vector3 ahead = owner.position + view * Vector3.forward * defaultDistance;
                        if (GroundAt(owner, ahead, out Vector3 gp, out Vector3 gn)) Evaluate(owner, gp, gn);
                        else return;
                    }
                    lastAimYaw = input.LookYaw;
                    viewLocked = false;
                    return;
                }
                if (!viewLocked)
                {
                    Vector3 off = Point - owner.position; off.y = 0f;
                    viewLocal = Quaternion.Inverse(Quaternion.Euler(0f, lastAimYaw, 0f)) * off; // 마지막으로 겨눴을 때 시야 기준
                    viewLocked = true;
                }
                Vector3 p = owner.position + view * viewLocal;
                p.y = Point.y; // 고른 자리 높이에서 바닥을 찾는다(위층에 둔 자리가 시점을 돌리면 아래층으로 떨어지지 않게)
                if (GroundAt(owner, p, out Vector3 point, out Vector3 normal)) Evaluate(owner, point, normal);
                else { Point = p; Cancels = false; Valid = false; } // 아래에 땅이 없음
                return;
            }
            viewLocked = false;
            lastAimYaw = input.LookYaw;
            EvaluateAim(owner, input);
        }

        /// <summary>
        /// 가리킨 곳(input.AimPoint)으로 고른다. 놓을 수 없는 면이면 ① 위층 바닥 끝 모서리(SnapToLedge) ② 벽 너머 설 공간이 있는 바닥(seeThroughWalls) 순으로 찾는다.
        /// 보이는지(가려짐)는 따지지 않는다 — 설 공간만 있으면 된다.
        /// </summary>
        private void EvaluateAim(Transform owner, in CharacterInputFrame input)
        {
            Vector3 point = input.AimPoint, normal = input.AimNormal;
            if (!Placeable(owner, ref point, normal))
            {
                Vector3 sp = point, sn = normal;
                if (SnapToLedge(owner, ref sp, ref sn) && SnapSeen(owner, input, sp)) { point = sp; normal = sn; }
                else if (seeThroughWalls && input.HasAimRay && FindBeyond(owner, input.AimRayOrigin, input.AimPoint, out Vector3 bp, out Vector3 bn)) { point = bp; normal = bn; }
            }
            Evaluate(owner, point, normal);
        }

        /// <summary>땅(위를 향함)이고 설 공간이 있는지. 벽에 붙어 공간이 모자라면 point를 빈 곳으로 살짝 옮긴다.</summary>
        private bool Placeable(Transform owner, ref Vector3 point, Vector3 normal)
            => normal.y >= minGroundNormalY && FindStandSpot(owner, ref point);

        [Tooltip("벽 너머 놓기: 이보다 두꺼운 벽(m)은 뒷면을 찾지 않는다.")]
        [Min(0.1f)] public float seeThroughMaxWall = 3f;

        /// <summary>
        /// 광선(origin → aimPoint)을 벽 너머로 이어 가까운 것부터 처음으로 놓을 수 있는 자리(위를 향한 바닥 + 설 공간 + 높이차 안)를 찾는다.
        /// - 광선이 바닥에 닿으면 그 자리.
        /// - 광선이 벽(옆면)에 닿으면 그 벽 뒷면 바로 너머에서 아래로 내려 바닥을 찾는다(올려다보며 벽을 가리켜도 벽 뒤 바닥).
        /// 천장(밑면)은 그냥 지나간다(천장 위 위층에 놓지 않음).
        /// </summary>
        private bool FindBeyond(Transform owner, Vector3 origin, Vector3 aimPoint, out Vector3 point, out Vector3 normal)
        {
            point = aimPoint; normal = Vector3.up;
            Vector3 dir = aimPoint - origin;
            float toAim = dir.magnitude;
            if (toAim < 1e-3f) return false;
            dir /= toAim;
            int n = Physics.RaycastNonAlloc(origin, dir, BeyondHits, toAim + seeThroughDistance, ~0, QueryTriggerInteraction.Ignore);
            // 가까운 것부터
            for (int i = 1; i < n; i++)
            {
                var h = BeyondHits[i]; int j = i - 1;
                while (j >= 0 && BeyondHits[j].distance > h.distance) { BeyondHits[j + 1] = BeyondHits[j]; j--; }
                BeyondHits[j + 1] = h;
            }
            Vector3 flat = new(dir.x, 0f, dir.z);
            bool hasFlat = flat.sqrMagnitude > 1e-4f;
            if (hasFlat) flat.Normalize();
            for (int i = 0; i < n; i++)
            {
                var h = BeyondHits[i];
                if (h.distance < toAim - 0.05f || h.collider.transform.IsChildOf(owner) || IsCharacter(h.collider)) continue; // 가리킨 곳 앞(카메라와 사이)은 보지 않는다
                if (h.normal.y >= minGroundNormalY)
                {
                    if (Candidate(owner, h.point, h.normal, out point, out normal)) return true;
                    continue;
                }
                if (!hasFlat || Mathf.Abs(h.normal.y) > 0.5f) continue; // 천장·밑면은 지나간다
                // 벽: 뒷면(거꾸로 쏴서 찾음) 바로 너머에서 아래로 바닥을 찾는다.
                Vector3 far = h.point + flat * seeThroughMaxWall;
                if (!h.collider.Raycast(new Ray(far, -flat), out var back, seeThroughMaxWall)) continue; // 너무 두꺼움
                Vector3 behind = back.point + flat * (standRadius + 0.05f);
                if (!Probe(owner, behind + Vector3.up * 0.2f, Vector3.down, groundProbeDown, out Vector3 gp, out Vector3 gn)) continue;
                if (Candidate(owner, gp, gn, out point, out normal)) return true;
            }
            return false;
        }

        /// <summary>p가 놓을 수 있는 바닥 자리인지(위를 향함, 높이차 안, 설 공간 — 벽에 붙으면 살짝 옮김).</summary>
        private bool Candidate(Transform owner, Vector3 p, Vector3 n, out Vector3 point, out Vector3 normal)
        {
            point = p; normal = n;
            if (n.y < minGroundNormalY) return false;
            if (maxRiseHeight > 0f && p.y - owner.position.y > maxRiseHeight) return false;
            if (!FindStandSpot(owner, ref point)) return false;
            return true;
        }

        private static bool IsCharacter(Collider c) => c is CharacterController || c.GetComponentInParent<CharacterIdentity>() != null;

        /// <summary>
        /// point에 캐릭터가 설 공간(캡슐)이 있는지. 없으면 둘레로 조금씩(반지름의 0.6·1.2배) 옮겨 같은 바닥에서 빈 곳을 찾는다. requireStandSpace가 꺼져 있으면 늘 된다.
        /// </summary>
        private bool FindStandSpot(Transform owner, ref Vector3 point)
        {
            if (!requireStandSpace || HasStandSpace(owner, point)) return true;
            foreach (float k in NudgeScales)
            {
                foreach (var d in NudgeDirs)
                {
                    Vector3 c = point + d * (standRadius * k);
                    if (!Probe(owner, c + Vector3.up * 0.5f, Vector3.down, 1f, out Vector3 gp, out Vector3 gn) || gn.y < minGroundNormalY) continue;
                    if (!HasStandSpace(owner, gp)) continue;
                    point = gp;
                    return true;
                }
            }
            return false;
        }

        /// <summary>발이 point에 있을 때 캐릭터 캡슐(standRadius·standHeight) 자리가 비었는지(자기 몸·캐릭터·트리거·판정은 무시).</summary>
        public bool HasStandSpace(Transform owner, Vector3 point)
        {
            float r = standRadius;
            Vector3 a = point + Vector3.up * (r + 0.12f), b = point + Vector3.up * Mathf.Max(r + 0.12f, standHeight - r);
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            int n = Physics.OverlapCapsuleNonAlloc(a, b, r, SpaceHits, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = SpaceHits[i];
                if (c.transform.IsChildOf(owner) || IsCharacter(c)) continue;
                return false;
            }
            return true;
        }

        /// <summary>시점을 돌리는 동안: 마우스 위·아래로 멀리·가까이, 좌·우로 시점보다 많이 돌려 자리를 직접 옮긴다(카메라 상하는 거의 안 움직이게 따로 줄임).</summary>
        private void Drag(Transform owner, in CharacterInputFrame input)
        {
            if (!dragInit)
            {
                // 시작 자리: 이미 고른 자리 → 화면 가운데가 가리키는 땅 → 앞쪽 defaultDistance
                Vector3 start;
                if (set) start = Point;
                else if (input.HasAimPoint) start = input.AimPoint;
                else start = owner.position + Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward * defaultDistance;
                Vector3 off = start - owner.position; off.y = 0f;
                dragDistance = Mathf.Clamp(off.magnitude, 0f, maxPlaceDistance);
                dragAngle = off.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward, off, Vector3.up) : 0f;
                dragInit = true;
            }
            else
            {
                dragDistance = Mathf.Clamp(dragDistance - input.LookDelta.y * dragDistancePerDegree, 0f, maxPlaceDistance); // 마우스를 올리면 멀리
                dragAngle += input.LookDelta.x * (dragLateralGain - 1f); // 시점이 도는 것에 더해 더 돈다
            }
            Vector3 p = owner.position + Quaternion.Euler(0f, input.LookYaw + dragAngle, 0f) * Vector3.forward * dragDistance;
            if (GroundAt(owner, p, out Vector3 gp, out Vector3 gn)) Evaluate(owner, gp, gn);
            else { Point = p; set = true; Cancels = false; Valid = false; }
        }

        private void Evaluate(Transform owner, Vector3 point, Vector3 normal)
        {
            Vector3 flat = point - owner.position; flat.y = 0f;
            if (clampToMax && flat.magnitude > maxPlaceDistance)
            {
                // 너무 먼 곳: 그 방향 최대 거리 자리의 땅으로 당긴다.
                Vector3 p = owner.position + flat.normalized * maxPlaceDistance;
                p.y = point.y;
                if (GroundAt(owner, p, out Vector3 gp, out Vector3 gn)) { point = gp; normal = gn; }
                else { point = p; normal = Vector3.zero; } // 그 자리 아래에 바닥이 없음 = 못 놓음
                flat = point - owner.position; flat.y = 0f;
            }
            bool space = normal.y >= minGroundNormalY && FindStandSpot(owner, ref point); // 설 공간(벽에 붙으면 빈 곳으로 살짝 옮김)
            Point = point;
            set = true;
            Cancels = flat.magnitude <= cancelRadius;
            bool tooHigh = maxRiseHeight > 0f && point.y - owner.position.y > maxRiseHeight;
            Valid = !Cancels && space && flat.magnitude <= maxPlaceDistance && !tooHigh;
        }

        /// <summary>
        /// 가리킨 곳이 바닥 판의 옆면·밑면(위를 향하지 않음)이면: 면 안쪽(옆면이면 면 반대 방향, 밑면이면 캐릭터에서 멀어지는 방향)으로
        /// ledgeSnapInset만큼 들어가 ledgeSnapHeight 위에서 내려 그 사이에 있는 윗면을 찾아 거기로 올린다(바닥·턱 끝 모서리).
        /// 벽 한가운데처럼 위 가까이에 윗면이 없거나, 면 바깥쪽 위가 막혀 있거나(방 안 벽 꼭대기 = 천장 밑), 설 공간이 없으면 false(그대로 둠).
        /// </summary>
        private bool SnapToLedge(Transform owner, ref Vector3 point, ref Vector3 normal)
        {
            if (ledgeSnapHeight <= 0f || normal.y >= minGroundNormalY) return false;
            Vector3 inward = -new Vector3(normal.x, 0f, normal.z);
            bool underside = inward.sqrMagnitude < 0.09f; // 거의 아래를 향함
            if (underside) { inward = point - owner.position; inward.y = 0f; }
            if (inward.sqrMagnitude < 1e-4f) return false;
            inward.Normalize();
            // 끝 모서리 안쪽으로 조금씩 더 들어가며 설 공간이 있는 윗면을 찾는다(위층 바깥벽이 끝에 서 있으면 그 벽 너머 위층 바닥).
            foreach (float extra in LedgeDepths)
            {
                Vector3 origin = point + inward * (ledgeSnapInset + extra) + Vector3.up * ledgeSnapHeight;
                if (!Probe(owner, origin, Vector3.down, ledgeSnapHeight + 0.05f, out Vector3 gp, out Vector3 gn)) continue;
                if (gn.y < minGroundNormalY || gp.y < point.y - 0.05f) continue;
                // 끝 모서리인지: 면 바깥쪽(밑면이면 캐릭터 쪽으로 ledgeSnapInset)에서 윗면 높이까지 위가 트여 있어야 한다.
                // 옆면이면 0.25m 바깥에서 본다 — 끝에 선 난간(두께 0.15m, 가장자리에 걸침)을 천장으로 잘못 보지 않게. 방 안 벽 꼭대기(천장 밑)는 그대로 막힌다.
                Vector3 outside = point - inward * (underside ? ledgeSnapInset : 0.25f) + Vector3.down * 0.02f;
                if (Probe(owner, outside, Vector3.up, gp.y - outside.y + 0.1f, out _, out _)) return false;
                if (!FindStandSpot(owner, ref gp)) continue;
                point = gp; normal = gn;
                return true;
            }
            return false;
        }

        private static readonly float[] LedgeDepths = { 0f, 0.5f, 1f };

        [Tooltip("턱 끝 잡기를 보일 때만(안토니오): 올려 붙인 자리 위 이 높이들(m) 중 하나라도 시점(광선 출발점)에서 가리지 않고 보여야 붙인다. 안 보이면 지금처럼 못 놓는다. 비우면 검사 안 함(붉은나비).")]
        public float[] ledgeSnapSightHeights = Array.Empty<float>();

        /// <summary>턱 끝에 올려 붙인 자리가 시점에서 일부라도 보이는지(ledgeSnapSightHeights 중 하나). 검사를 안 쓰면 늘 true.</summary>
        private bool SnapSeen(Transform owner, in CharacterInputFrame input, Vector3 snapped)
        {
            if (ledgeSnapSightHeights == null || ledgeSnapSightHeights.Length == 0) return true;
            if (!input.HasAimRay) return false;
            foreach (float h in ledgeSnapSightHeights)
                if (Clear(owner, input.AimRayOrigin, snapped + Vector3.up * h)) return true;
            return false;
        }

        /// <summary>from → to 사이에 막는 것이 없는지(owner 몸·캐릭터·트리거 제외).</summary>
        private static bool Clear(Transform owner, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            int n = Physics.RaycastNonAlloc(from, d / len, Hits, len - 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (c.transform.IsChildOf(owner) || IsCharacter(c)) continue;
                return false;
            }
            return true;
        }

        /// <summary>p 바로 위에서 아래로 내려 처음 닿는 바닥(owner 몸·트리거 제외).</summary>
        public bool GroundAt(Transform owner, Vector3 p, out Vector3 point, out Vector3 normal)
            => Probe(owner, p + Vector3.up * groundProbeUp, Vector3.down, groundProbeUp + groundProbeDown, out point, out normal);

        /// <summary>origin에서 dir로 distance만큼 쏴 처음 닿는 면(owner 몸·트리거 제외).</summary>
        private static bool Probe(Transform owner, Vector3 origin, Vector3 dir, float distance, out Vector3 point, out Vector3 normal)
        {
            point = origin; normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider.transform.IsChildOf(owner) || h.distance >= best) continue;
                best = h.distance; point = h.point; normal = h.normal; found = true;
            }
            return found;
        }
    }
}

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

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
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
                Evaluate(owner, input.AimPoint, input.AimNormal);
                return;
            }
            if (!(input.HasAimPoint && input.AimFromCursor))
            {
                if (!set)
                {
                    if (input.HasAimPoint) Evaluate(owner, input.AimPoint, input.AimNormal);
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
            Evaluate(owner, input.AimPoint, input.AimNormal);
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
            if (normal.y < minGroundNormalY) SnapToLedge(owner, ref point, ref normal);
            Vector3 flat = point - owner.position; flat.y = 0f;
            if (clampToMax && flat.magnitude > maxPlaceDistance)
            {
                // 너무 먼 곳: 그 방향 최대 거리 자리의 땅으로 당긴다.
                Vector3 p = owner.position + flat.normalized * maxPlaceDistance;
                p.y = point.y;
                if (GroundAt(owner, p, out Vector3 gp, out Vector3 gn)) { point = gp; normal = gn; }
                else point = p;
                flat = point - owner.position; flat.y = 0f;
            }
            Point = point;
            set = true;
            Cancels = flat.magnitude <= cancelRadius;
            bool tooHigh = maxRiseHeight > 0f && point.y - owner.position.y > maxRiseHeight;
            Valid = !Cancels && normal.y >= minGroundNormalY && flat.magnitude <= maxPlaceDistance && !tooHigh;
        }

        /// <summary>
        /// 가리킨 곳이 바닥 판의 옆면·밑면(위를 향하지 않음)이면: 면 안쪽(옆면이면 면 반대 방향, 밑면이면 캐릭터에서 멀어지는 방향)으로
        /// ledgeSnapInset만큼 들어가 ledgeSnapHeight 위에서 내려 그 사이에 있는 윗면을 찾아 거기로 올린다(바닥·턱 끝 모서리).
        /// 벽 한가운데처럼 위 가까이에 윗면이 없거나, 면 바깥쪽 위가 막혀 있으면(방 안 벽 꼭대기 = 천장 밑) 그대로 둔다(못 놓음).
        /// </summary>
        private void SnapToLedge(Transform owner, ref Vector3 point, ref Vector3 normal)
        {
            if (ledgeSnapHeight <= 0f) return;
            Vector3 inward = -new Vector3(normal.x, 0f, normal.z);
            bool underside = inward.sqrMagnitude < 0.09f; // 거의 아래를 향함
            if (underside) { inward = point - owner.position; inward.y = 0f; }
            if (inward.sqrMagnitude < 1e-4f) return;
            inward.Normalize();
            Vector3 origin = point + inward * ledgeSnapInset + Vector3.up * ledgeSnapHeight;
            if (!Probe(owner, origin, Vector3.down, ledgeSnapHeight + 0.05f, out Vector3 gp, out Vector3 gn)) return;
            if (gn.y < minGroundNormalY || gp.y < point.y - 0.05f) return;
            // 끝 모서리인지: 면 바깥쪽(밑면이면 캐릭터 쪽으로 ledgeSnapInset)에서 윗면 높이까지 위가 트여 있어야 한다.
            Vector3 outside = point - inward * (underside ? ledgeSnapInset : 0.05f) + Vector3.down * 0.02f;
            if (Probe(owner, outside, Vector3.up, gp.y - outside.y + 0.1f, out _, out _)) return;
            point = gp; normal = gn;
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

using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 좌클릭을 꾹 눌러 땅 위에 무언가를 놓을 자리 고르기(미리보기) — 붉은나비 나비, 안토니오 음표가 같이 쓴다(BE).
    /// - 마우스 커서가 가리키는 곳을 따라간다. 땅 위만 된다(면이 위를 향함, 공중·벽 불가), 너무 멀면 안 됨.
    /// - 몸 근처(cancelRadius)로 끌어다 놓으면 "취소" 자리(Cancels).
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

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private bool set, viewLocked;
        private Vector3 viewLocal;
        private float lastAimYaw;

        public Vector3 Point { get; private set; }
        public bool Valid { get; private set; }
        public bool Cancels { get; private set; }

        /// <summary>새로 고르기 시작(지난 자리는 버린다).</summary>
        public void Begin() { Valid = false; Cancels = false; set = false; viewLocked = false; }

        /// <summary>고르기를 끝냄(놓았거나 취소).</summary>
        public void Clear() { Valid = false; Cancels = false; }

        public void Update(Transform owner, in CharacterInputFrame input)
        {
            Quaternion view = Quaternion.Euler(0f, input.LookYaw, 0f);
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
                if (GroundAt(owner, p, out Vector3 point, out Vector3 normal)) Evaluate(owner, point, normal);
                else { Point = p; Cancels = false; Valid = false; } // 아래에 땅이 없음
                return;
            }
            viewLocked = false;
            lastAimYaw = input.LookYaw;
            Evaluate(owner, input.AimPoint, input.AimNormal);
        }

        private void Evaluate(Transform owner, Vector3 point, Vector3 normal)
        {
            Point = point;
            set = true;
            Vector3 flat = point - owner.position; flat.y = 0f;
            Cancels = flat.magnitude <= cancelRadius;
            Valid = !Cancels && normal.y >= minGroundNormalY && flat.magnitude <= maxPlaceDistance;
        }

        /// <summary>p 바로 위에서 아래로 내려 처음 닿는 바닥(owner 몸·트리거 제외).</summary>
        public bool GroundAt(Transform owner, Vector3 p, out Vector3 point, out Vector3 normal)
        {
            point = p; normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * groundProbeUp, Vector3.down, Hits, groundProbeUp + groundProbeDown, ~0, QueryTriggerInteraction.Ignore);
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

using System.Collections.Generic;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 순간이동 거울(BE): 맵 구석의 큰 전신거울. 거울 앞(앞 = +Z)에서 거울로 걸어 들어가면(MirrorTraveler) 같은 pairId를 가진 반대 거울 앞으로 나온다.
    /// 거울 오브젝트 루트(앞 +Z, 바닥 가운데 = 위치)에 붙인다(MapDresser가 놓는다). 충돌은 거울 모델 쪽 상자.
    /// </summary>
    [DisallowMultipleComponent]
    public class MirrorPortal : MonoBehaviour
    {
        [Tooltip("같은 값을 가진 다른 거울로 나간다(두 개씩 짝).")]
        [SerializeField] private string pairId = "A";
        [Tooltip("들어가는 자리: 거울 면 앞 이 거리(m) 안, 좌우로 halfWidth 안.")]
        [SerializeField, Min(0.3f)] private float enterReach = 1.3f;
        [SerializeField, Min(0.2f)] private float halfWidth = 0.75f;
        [Tooltip("나오는 자리: 거울 면 앞 이 거리(m).")]
        [SerializeField, Min(0.5f)] private float exitDistance = 1.4f;

        private static readonly List<MirrorPortal> All = new();

        public string PairId => pairId;
        public Vector3 Front => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 ExitPoint => transform.position + Front * exitDistance;
        public float ExitYaw => Mathf.Atan2(Front.x, Front.z) * Mathf.Rad2Deg;

        /// <summary>MapDresser가 놓을 때 짝을 정한다.</summary>
        public void Configure(string pair) => pairId = pair;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() => All.Remove(this);

        /// <summary>짝 거울(같은 pairId, 자기 말고). 없으면 null.</summary>
        public MirrorPortal Partner
        {
            get
            {
                foreach (var m in All) if (m != this && m != null && m.pairId == pairId) return m;
                return null;
            }
        }

        /// <summary>pos가 이 거울 앞 들어가는 자리 안인지.</summary>
        public bool InEnterZone(Vector3 pos)
        {
            Vector3 to = Vector3.ProjectOnPlane(pos - transform.position, Vector3.up);
            float ahead = Vector3.Dot(to, Front), side = Mathf.Abs(Vector3.Dot(to, Vector3.Cross(Vector3.up, Front)));
            return ahead > 0f && ahead < enterReach && side < halfWidth && Mathf.Abs(pos.y - transform.position.y) < 1.5f;
        }

        /// <summary>pos가 들어가는 자리 안에 있고, dir(걸어가는·보는 방향)이 거울 쪽을 향하면 그 거울.</summary>
        public static bool TryFind(Vector3 pos, Vector3 dir, out MirrorPortal mirror)
        {
            mirror = null;
            dir = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            foreach (var m in All)
            {
                if (m == null || !m.InEnterZone(pos) || m.Partner == null) continue;
                if (Vector3.Dot(dir, -m.Front) < 0.6f) continue; // 거울 쪽으로 걸어 들어갈 때만
                mirror = m;
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position + Front * (enterReach / 2f) + Vector3.up, Quaternion.LookRotation(Front), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, 2f, enterReach));
        }
    }
}

using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;
using UnityEngine.AI;

namespace Project1.UI
{
    /// <summary>
    /// 대전 시작 자리(임시 규칙): 1층 바닥에서 서로 멀리 떨어진 두 자리를 고른다 — 매번 조금씩 다르게(무작위로 한쪽을 고르고, 거기서 가장 먼 쪽 몇 곳 중 하나).
    /// 걸어서 닿을 수 있는 곳(NavMesh 길이 이어진 곳)만, 거울 앞·물건 속은 뺀다. 고르지 못하면 false(원래처럼 플레이어 앞에 세운다).
    /// </summary>
    public static class BattleSpawnPicker
    {
        private const float Half = 42f;        // 맵 안쪽 범위(m, 가운데에서)
        private const float Step = 3f;         // 후보 간격
        private const float MaxFloorY = 0.6f;  // 1층 바닥만
        private const float MirrorClear = 4f;  // 거울 앞은 피함(걸어 들어가면 순간이동)
        private const float MinApart = 45f;    // 이보다 가까운 짝은 쓰지 않음

        public static bool TryPick(out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            if (!RuntimeNavMesh.Ensure()) return false;
            var filter = RuntimeNavMesh.Filter;
            var mirrors = Object.FindObjectsByType<MirrorPortal>();
            var spots = new List<Vector3>();
            for (float x = -Half; x <= Half; x += Step)
                for (float z = -Half; z <= Half; z += Step)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out var hit, 1f, filter) || hit.position.y > MaxFloorY) continue;
                    Vector3 p = hit.position;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    bool nearMirror = false;
                    foreach (var m in mirrors) if (m != null && (m.transform.position - p).sqrMagnitude < MirrorClear * MirrorClear) { nearMirror = true; break; }
                    if (!nearMirror) spots.Add(p);
                }
            if (spots.Count < 2) return false;

            var path = new NavMeshPath();
            for (int tries = 0; tries < 12; tries++)
            {
                Vector3 first = spots[Random.Range(0, spots.Count)];
                // 거기서 먼 순서로 몇 곳 중 하나(늘 같은 짝이 되지 않게)
                spots.Sort((p, q) => (q - first).sqrMagnitude.CompareTo((p - first).sqrMagnitude));
                int pool = Mathf.Max(1, Mathf.Min(6, spots.Count / 20));
                for (int k = 0; k < pool * 2 && k < spots.Count; k++)
                {
                    Vector3 second = spots[Random.Range(0, pool)];
                    if (Vector3.Distance(first, second) < MinApart) break;
                    if (!NavMesh.CalculatePath(first, second, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    a = first; b = second;
                    return true;
                }
            }
            return false;
        }

        /// <summary>캐릭터를 pos에 세우고 lookAt 쪽을 보게 한다(카메라 시야도 바로).</summary>
        public static void Place(GameObject character, Vector3 pos, Vector3 lookAt)
        {
            Vector3 dir = Vector3.ProjectOnPlane(lookAt - pos, Vector3.up);
            if (dir.sqrMagnitude < 0.01f) dir = character.transform.forward;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var cc = character.GetComponent<CharacterController>();
            bool had = cc != null && cc.enabled;
            if (had) cc.enabled = false;
            character.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, rot);
            Physics.SyncTransforms();
            if (had) cc.enabled = true;
            if (character.TryGetComponent(out ILookSnap look)) look.SnapLook(rot.eulerAngles.y);
        }
    }
}

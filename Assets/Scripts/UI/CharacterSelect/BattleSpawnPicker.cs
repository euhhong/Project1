using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;
using UnityEngine.AI;

namespace Project1.UI
{
    /// <summary>
    /// 대전 시작 자리: 한 명은 1층, 다른 한 명은 3층(누가 어디인지는 무작위). 층마다 바닥 자리를 훑어 매번 조금씩 다르게 고르고,
    /// 수평으로 MinApart 넘게 떨어지고 걸어서 닿을 수 있는(NavMesh 길이 이어진 — 계단·사다리) 짝만. 거울 앞·물건 속은 뺀다.
    /// 고르지 못하면 false(원래처럼 플레이어 앞에 세운다).
    /// </summary>
    public static class BattleSpawnPicker
    {
        private const float Half = 42f;        // 맵 안쪽 범위(m, 가운데에서)
        private const float Step = 3f;         // 후보 간격
        private const float Floor1Y = 0f, Floor3Y = 12.15f; // 1층·3층 바닥 높이
        private const float FloorTolerance = 0.8f;          // 층 바닥에서 이만큼 안만(지붕·계단 중간은 아님)
        private const float MirrorClear = 4f;  // 거울 앞은 피함(걸어 들어가면 순간이동)
        private const float MinApart = 15f;    // 수평으로 이보다 가까운 짝은 쓰지 않음

        /// <summary>a = 플레이어 자리, b = 봇 자리 — 둘 중 하나는 1층, 하나는 3층.</summary>
        public static bool TryPick(out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            if (!RuntimeNavMesh.Ensure()) return false;
            var filter = RuntimeNavMesh.Filter;
            var mirrors = Object.FindObjectsByType<MirrorPortal>();
            var floor1 = Spots(Floor1Y, filter, mirrors);
            var floor3 = Spots(Floor3Y, filter, mirrors);
            if (floor1.Count == 0 || floor3.Count == 0) return false;

            var path = new NavMeshPath();
            bool playerUp = Random.value < 0.5f; // 누가 3층인지
            for (int tries = 0; tries < 30; tries++)
            {
                Vector3 low = floor1[Random.Range(0, floor1.Count)], high = floor3[Random.Range(0, floor3.Count)];
                Vector3 d = high - low; d.y = 0f;
                if (d.magnitude < MinApart) continue;
                if (!NavMesh.CalculatePath(low, high, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                a = playerUp ? high : low;
                b = playerUp ? low : high;
                return true;
            }
            return false;
        }

        /// <summary>그 층 바닥(높이 y ± FloorTolerance)의 빈 자리들(물건 속·거울 앞 빼고).</summary>
        private static List<Vector3> Spots(float y, NavMeshQueryFilter filter, MirrorPortal[] mirrors)
        {
            var spots = new List<Vector3>();
            for (float x = -Half; x <= Half; x += Step)
                for (float z = -Half; z <= Half; z += Step)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, 1f, filter) || Mathf.Abs(hit.position.y - y) > FloorTolerance) continue;
                    Vector3 p = hit.position;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    bool nearMirror = false;
                    foreach (var m in mirrors) if (m != null && (m.transform.position - p).sqrMagnitude < MirrorClear * MirrorClear) { nearMirror = true; break; }
                    if (!nearMirror) spots.Add(p);
                }
            return spots;
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

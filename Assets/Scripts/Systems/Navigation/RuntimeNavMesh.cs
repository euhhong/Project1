using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Project1.Systems
{
    /// <summary>
    /// 런타임 NavMesh(BE). 처음 길을 물을 때 지금 씬의 Collider(맵 블록·계단 램프·지형)를 모아 한 번 굽는다. 사다리(Ladder)는 위아래를 잇는 링크로 단다.
    /// - 맵은 에디터 생성기(MapBlockoutBuilder)로 씬에 들어가 있거나 나중에 런타임에 만들어질 수 있으니, 미리 구운 데이터에 기대지 않는다.
    /// - 캐릭터(CharacterIdentity 아래)·트리거·움직이는 Rigidbody는 넣지 않는다. 맵이 바뀌면 Invalidate()로 다시 굽게 한다.
    /// - 씬이 바뀌면 저절로 버린다.
    /// </summary>
    public static class RuntimeNavMesh
    {
        // 캐릭터 몸 기준(키 약 2.1m, CharacterController stepOffset 0.3, slopeLimit 45). 계단은 램프 콜라이더(약 34°)로 오른다.
        private const float AgentRadius = 0.4f;
        private const float AgentHeight = 2f;
        private const float AgentClimb = 0.35f;
        private const float AgentSlope = 45f;
        private const float VoxelSize = 0.15f;

        private static NavMeshDataInstance instance;
        private static bool built;
        private static Scene builtScene;
        private const float RetryDelay = 2f;
        private static float retryAt;
        private static readonly List<NavMeshBuildSource> sources = new();
        private static readonly List<NavMeshBuildMarkup> noMarkups = new();
        private static readonly List<NavMeshLinkInstance> links = new();
        private const float LadderCost = 2f; // 사다리 링크 비용 배율(링크 길이 × 이 값) — 계단이 비슷하게 가까우면 계단으로

        public static int AgentTypeId => 0;
        /// <summary>NavMesh를 구운 몸 반지름(m) — 가장자리는 벽에서 이만큼 떨어져 있다.</summary>
        public static float Radius => AgentRadius;

        /// <summary>NavMesh가 없으면 굽는다. 걸을 바닥이 하나도 없으면 false.</summary>
        public static bool Ensure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (built && builtScene == scene)
            {
                if (instance.valid) return true;
                if (Time.realtimeSinceStartup < retryAt) return false; // 굽기 실패: 매 틱 다시 굽지 않고 잠깐 뒤에(맵이 나중에 생길 수 있다)
            }
            Clear();
            built = true;
            builtScene = scene;
            retryAt = Time.realtimeSinceStartup + RetryDelay;

            var bigBounds = new Bounds(Vector3.zero, Vector3.one * 20000f);
            NavMeshBuilder.CollectSources(bigBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, noMarkups, sources);
            Bounds bounds = default;
            bool any = false;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (!Keep(sources[i].component, out Bounds b)) { sources.RemoveAt(i); continue; }
                if (any) bounds.Encapsulate(b); else { bounds = b; any = true; }
            }
            if (!any) { sources.Clear(); return false; }
            bounds.Expand(1f);

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(AgentTypeId);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = AgentClimb;
            settings.agentSlope = AgentSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = VoxelSize;

            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            sources.Clear();
            if (data == null) return false;
            data.name = "RuntimeNavMesh";
            instance = NavMesh.AddNavMeshData(data);
            if (instance.valid) AddLadderLinks();
            return instance.valid;
        }

        /// <summary>
        /// 사다리마다 아래 바닥(사다리 앞) ↔ 위층 바닥(턱 너머)을 잇는 NavMesh 링크를 단다 — 길 찾기가 사다리도 길로 쓴다.
        /// 길의 꺾는 점에 링크 양 끝이 나오므로, 따라가는 쪽은 아래 끝에서 벽 쪽(위 끝)으로 밀면 잡고 오르고(CharacterTraversal),
        /// 위에서는 턱 끝 바깥쪽(아래 끝)으로 걸어 나가면 잡고 내려간다.
        /// </summary>
        private static void AddLadderLinks()
        {
            var filter = Filter;
            foreach (var l in Project1.Core.Ladder.All)
            {
                if (l == null || !l.isActiveAndEnabled) continue;
                Vector3 n = l.Normal;
                Vector3 bottom = l.Bottom + n * (l.Depth + AgentRadius + 0.25f);
                Vector3 top = l.Bottom - n * 0.8f + Vector3.up * l.Height;
                if (!NavMesh.SamplePosition(bottom, out var b, 1f, filter) || Mathf.Abs(b.position.y - l.BottomY) > 0.6f) continue;
                if (!NavMesh.SamplePosition(top, out var t, 1f, filter) || Mathf.Abs(t.position.y - l.TopY) > 0.6f) continue;
                var link = NavMesh.AddLink(new NavMeshLinkData
                {
                    startPosition = b.position,
                    endPosition = t.position,
                    width = 0f,
                    bidirectional = true,
                    costModifier = LadderCost,
                    area = LadderArea,
                    agentTypeID = AgentTypeId,
                });
                if (NavMesh.IsLinkValid(link)) links.Add(link);
            }
        }

        /// <summary>p가 사다리 링크 끝(아래 끝·위 끝) 가까이인지 — 그 사다리를 돌려준다.</summary>
        public static Project1.Core.Ladder LadderAt(Vector3 p, float radius = 1.5f)
        {
            foreach (var l in Project1.Core.Ladder.All)
            {
                if (l == null || !l.isActiveAndEnabled) continue;
                Vector3 n = l.Normal;
                Vector3 bottom = l.Bottom + n * (l.Depth + AgentRadius + 0.25f);
                Vector3 top = l.Bottom - n * 0.8f + Vector3.up * l.Height;
                if ((p - bottom).sqrMagnitude < radius * radius || (p - top).sqrMagnitude < radius * radius) return l;
            }
            return null;
        }

        /// <summary>맵이 바뀌었을 때: 다음에 물을 때 다시 굽는다.</summary>
        public static void Invalidate() => Clear();

        public static NavMeshQueryFilter Filter => new() { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };
        /// <summary>사다리 링크를 빼고 찾는 필터(사다리를 못 타는 것 — 분신 등).</summary>
        public static NavMeshQueryFilter FilterNoLadders => new() { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas & ~(1 << LadderArea) };
        private const int LadderArea = 2; // 내장 영역 "Jump" — 사다리 링크만 이 영역

        private static bool Keep(Component c, out Bounds bounds)
        {
            bounds = default;
            if (c is Terrain t && t.terrainData != null)
            {
                bounds = new Bounds(t.transform.position + t.terrainData.size / 2f, t.terrainData.size);
                return true;
            }
            if (c is not Collider col || !col.enabled || col.isTrigger) return false;
            if (col is CharacterController) return false;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) return false;
            if (col.GetComponentInParent<CharacterIdentity>() != null) return false;
            bounds = col.bounds;
            return true;
        }

        private static void Clear()
        {
            if (instance.valid) NavMesh.RemoveNavMeshData(instance);
            instance = default;
            foreach (var link in links) if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link);
            links.Clear();
            built = false;
            builtScene = default;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = default; // 도메인 리로드 없이 플레이할 때: 지난 플레이의 인스턴스는 이미 사라졌다
            links.Clear();
            built = false;
            builtScene = default;
            retryAt = 0f;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private static void OnSceneChanged(Scene from, Scene to) => Clear();
    }
}

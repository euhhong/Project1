using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Project1.Systems
{
    /// <summary>
    /// 런타임 NavMesh(BE). 처음 길을 물을 때 지금 씬의 Collider(맵 블록·계단 램프·지형)를 모아 한 번 굽는다.
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
        private static int builtScene = -1;
        private static readonly List<NavMeshBuildSource> sources = new();
        private static readonly List<NavMeshBuildMarkup> noMarkups = new();

        public static int AgentTypeId => 0;

        /// <summary>NavMesh가 없으면 굽는다. 걸을 바닥이 하나도 없으면 false.</summary>
        public static bool Ensure()
        {
            int scene = SceneManager.GetActiveScene().handle;
            if (built && builtScene == scene && instance.valid) return true;
            Clear();
            built = true; // 실패해도 매 틱 다시 굽지 않는다(Invalidate로 다시)
            builtScene = scene;

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
            return instance.valid;
        }

        /// <summary>맵이 바뀌었을 때: 다음에 물을 때 다시 굽는다.</summary>
        public static void Invalidate() => Clear();

        public static NavMeshQueryFilter Filter => new() { agentTypeID = AgentTypeId, areaMask = NavMesh.AllAreas };

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
            built = false;
            builtScene = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = default; // 도메인 리로드 없이 플레이할 때: 지난 플레이의 인스턴스는 이미 사라졌다
            built = false;
            builtScene = -1;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private static void OnSceneChanged(Scene from, Scene to) => Clear();
    }
}

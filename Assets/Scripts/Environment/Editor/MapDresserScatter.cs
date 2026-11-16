using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// MapDresser 추가분(BG) — 술래잡기용 가구·소품 흩뿌리기와 맵 가운데 정리.
    /// - 돌아 도망칠 수 있는 가구(장롱·서랍장·소파·피아노·쌓인 상자·괘종시계·넘어진 식탁 / 야외: 수레·자루 더미·장작더미·넘어진 석상·부서진 울타리):
    ///   바닥을 2m 간격으로 훑어, 둘레(반지름 loopClear)가 비어 빙 돌 수 있는 자리에만 서로 7m 넘게 떨어뜨려 놓는다(충돌 = 모델 경계 상자).
    ///   벽·장애물에 너무 가까워 끼이는 자리, 판자 바로 옆(3m)은 뺀다.
    /// - 벽에 붙는 작은 소품(트렁크·양동이·등잔·꽃병·거울·관 / 야외: 가로등·돌 벤치·화로·비석·돌 십자가·마른 덤불·외바퀴 수레):
    ///   벽에서 0.6~1.3m 떨어진 빈 자리에 벽을 등지고 4m 넘게 떨어뜨려 놓는다.
    /// - 맵 가운데(예전 연습용 벽·우물 자리): 긴 무너진 벽(LongWall) – 판자 – 긴 무너진 벽(판자는 PalletPlacer). 우물은 맵 구석(비어 있는 첫 자리)으로 옮긴다.
    /// 자리 고르기는 이름·좌표 해시로 정해져 여러 번 실행해도 같다.
    /// </summary>
    public static partial class MapDresser
    {
        public const string PracticeSpotName = "PracticeSpot";
        // 가운데 긴 벽 두 개(가운데 x, z, 길이) — 틈 x -1 ~ 1(2m)에 판자
        private static readonly (float x, float z, float len)[] CenterWalls = { (-3.25f, 12f, 4.5f), (3.25f, 12f, 4.5f) };
        private static readonly Vector2[] WellCandidates =
        {
            // 맵 구석 옆(담장 안쪽) — 구석 대각선(순간이동 거울과 그 앞 길)은 비우고 옆으로 비켜 선 첫 빈자리
            new(-41.5f, -35f), new(35f, -41.5f), new(41.5f, -35f), new(-35f, -41.5f), new(-41.5f, 31f), new(41.5f, 31f),
        };

        private static readonly (string key, float h, bool indoor)[] LoopProps =
        {
            ("Wardrobe", 2.1f, true), ("Dresser", 1.1f, true), ("Sofa", 0.95f, true), ("Piano", 1.4f, true), ("CrateStack", 2.0f, true),
            ("Clock", 2.2f, true), ("TableBroken", 1.0f, true),
            ("RockingChair", 1.1f, true), ("TeaCart", 1.0f, true), ("WineRack", 1.9f, true), ("RockingHorse", 1.1f, true), ("SpinningWheel", 1.1f, true),
            ("Cart", 1.3f, false), ("SackPile", 1.0f, false), ("FirewoodPile", 1.1f, false), ("StatueFallen", 1.2f, false), ("WoodFence", 1.2f, false), ("CrateStack", 2.0f, false),
            ("HayBale", 1.1f, false), ("TreeStump", 0.9f, false), ("IronBench", 0.95f, false), ("BirdBath", 1.0f, false), ("WaterPump", 1.4f, false), ("GardenTrellis", 2.4f, false),
        };
        private static readonly (string key, float h, bool indoor)[] WallProps =
        {
            ("Trunk", 0.6f, true), ("Bucket", 0.45f, true), ("Lantern", 0.45f, true), ("Vase", 1.0f, true), ("Mirror", 1.8f, true), ("Coffin", 0.6f, true),
            ("Barrel", 1.05f, true),
            ("Gramophone", 1.2f, true), ("Birdcage", 1.9f, true), ("BookPile", 0.6f, true), ("CoatRack", 1.9f, true), ("SuitcasePile", 1.0f, true),
            ("Lamppost", 3.2f, false), ("StoneBench", 0.6f, false), ("Brazier", 1.0f, false), ("Gravestone", 1.1f, false), ("GraveCross", 1.3f, false),
            ("DeadBush", 1.2f, false), ("Wheelbarrow", 0.8f, false), ("Bucket", 0.45f, false),
            ("StoneUrn", 1.3f, false), ("Signpost", 2.0f, false), ("WagonWheel", 1.2f, false), ("Scarecrow", 2.0f, false),
        };
        private const float LoopClear = 2.0f, LoopSpacing = 7f, WallSpacing = 4f;
        private const int MaxLoopIndoor = 22, MaxLoopOutdoor = 18, MaxWallIndoor = 46, MaxWallOutdoor = 40;

        private static void BuildScatter(Transform g)
        {
            var root = new GameObject($"{ModelPrefix}Scatter").transform;
            root.SetParent(g, false);
            var placedAt = new List<Vector3>();
            var windowSpots = new List<Vector3>(); // 창문 구멍 아래 가운데(MapBlockoutBuilder WinOpening_)
            foreach (var t in g.root.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("WinOpening_")) { windowSpots.Add(t.position); windowSpots.Add(t.position - Vector3.up * 1.0f); }
            foreach (var p in Object.FindObjectsByType<Project1.Systems.Pallet>()) placedAt.Add(p.transform.position);
            placedAt.AddRange(MirrorSpots); // 순간이동 거울과 그 앞(들어가는·나오는 자리)은 비워 둔다
            int loopIn = 0, loopOut = 0, wallIn = 0, wallOut = 0;
            var loopSpots = new List<Vector3>();
            var wallSpots = new List<Vector3>();
            foreach (float level in new[] { 0.15f, 6.15f, 12.15f })
                for (float x = -46f; x <= 46f; x += 2f)
                    for (float z = -46f; z <= 46f; z += 2f)
                    {
                        float jx = (Hash(x, z + level) - 0.5f) * 1.2f, jz = (Hash(z, x - level) - 0.5f) * 1.2f;
                        var top = new Vector3(x + jx, level + 2.2f, z + jz);
                        if (!Physics.Raycast(top, Vector3.down, out var hit, 2.6f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.95f) continue;
                        if (Mathf.Abs(hit.point.y - level) > 1.2f && level > 1f) continue;
                        Vector3 p = hit.point;
                        bool indoor = Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore);
                        if (!indoor && level > 1f) continue; // 지붕 위·테라스 위는 빼고 1층 바깥만 야외
                        if (Near(p, placedAt, 3f)) continue; // 판자·다른 소품 근처
                        if (!IsFloorHit(hit)) continue;      // 진짜 바닥·땅에만(상자·작업대·난간·창틀·분수대 위 등은 아님)
                        if (Near(p, windowSpots, 1.4f)) continue; // 창문 앞·창틀 위는 비움
                        // 1) 빙 돌 수 있는 자리(둘레가 비었음)
                        if (!Near(p, loopSpots, LoopSpacing) && Clear(p, LoopClear))
                        {
                            int lim = indoor ? MaxLoopIndoor : MaxLoopOutdoor;
                            if ((indoor ? loopIn : loopOut) < lim && Hash(p.x * 0.37f, p.z * 0.71f) < 0.55f)
                            {
                                var key = Pick(LoopProps, indoor, p);
                                if (key.key != null && PlaceProp(root, key.key, p, Hash(p.z, p.x) * 360f, key.h, true) != null)
                                {
                                    loopSpots.Add(p); placedAt.Add(p);
                                    if (indoor) loopIn++; else loopOut++;
                                    continue;
                                }
                            }
                        }
                        // 2) 벽 앞 작은 소품(벽을 등짐)
                        if ((indoor ? wallIn : wallOut) >= (indoor ? MaxWallIndoor : MaxWallOutdoor) || Near(p, wallSpots, WallSpacing)) continue;
                        if (!WallBehind(p, out Vector3 away)) continue;
                        if (!Clear(p, 0.55f) || Hash(p.x * 1.9f, p.z * 0.3f) > 0.5f) continue;
                        var wk = Pick(WallProps, indoor, p);
                        if (wk.key == null) continue;
                        float yaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                        bool col = wk.h >= 0.55f;
                        if (PlaceProp(root, wk.key, p, yaw, wk.h, col) != null)
                        {
                            wallSpots.Add(p); placedAt.Add(p);
                            if (indoor) wallIn++; else wallOut++;
                        }
                    }
            Debug.Log($"[BG] 흩뿌리기: 돌 수 있는 가구 실내 {loopIn}·야외 {loopOut}, 벽 앞 소품 실내 {wallIn}·야외 {wallOut}");
        }

        private static (string key, float h) Pick((string key, float h, bool indoor)[] table, bool indoor, Vector3 p)
        {
            var list = new List<(string, float)>();
            foreach (var e in table) if (e.indoor == indoor && LoadModel(e.key) != null) list.Add((e.key, e.h));
            if (list.Count == 0) return (null, 0f);
            return list[Mathf.Clamp(Mathf.FloorToInt(Hash(p.x * 2.3f + 1f, p.z * 1.7f + 2f) * list.Count), 0, list.Count - 1)];
        }

        /// <summary>바닥·땅인지: 지형(Environment), 층 바닥판(…Floor…·Floors 아래), 옥상·지붕 테라스, 연결동 옥상 길. 가구·상자·난간·창틀·분수대 위는 아니다.</summary>
        private static bool IsFloorHit(RaycastHit hit)
        {
            var t = hit.collider.transform;
            if (t.root.name == "Environment") return true;
            string n = t.name, p = t.parent != null ? t.parent.name : "";
            return n.Contains("Floor") || p.Contains("Floor") || p == "Floors" || p.Contains("Roof") || n.Contains("Walkway") || p.Contains("Walkway") || n.Contains("Terrace");
        }

        private static bool Near(Vector3 p, List<Vector3> list, float d)
        {
            foreach (var q in list) if ((q - p).sqrMagnitude < d * d) return true;
            return false;
        }

        /// <summary>p 둘레 반지름 r 안(높이 0.3~2m)에 맵 블록·소품이 없는지.</summary>
        private static bool Clear(Vector3 p, float r)
        {
            foreach (var c in Physics.OverlapCapsule(p + Vector3.up * (r + 0.3f), p + Vector3.up * Mathf.Max(r + 0.3f, 2f), r, ~0, QueryTriggerInteraction.Ignore))
            {
                string root = c.transform.root.name;
                if (root == "MapBlockout" || root == "Pallets" || root == PracticeSpotName || root == PracticeWellName) return false;
            }
            return true;
        }

        /// <summary>네 방향 중 0.6~1.3m 안에 벽이 있으면 true, away = 벽 반대(방 안) 방향.</summary>
        private static bool WallBehind(Vector3 p, out Vector3 away)
        {
            away = Vector3.forward;
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(p + Vector3.up * 1f, d, out var h, 1.3f, ~0, QueryTriggerInteraction.Ignore) && h.distance > 0.6f && h.collider.transform.root.name == "MapBlockout" && Mathf.Abs(h.normal.y) < 0.2f)
                { away = -d; return true; }
            return false;
        }

        private static GameObject PlaceProp(Transform parent, string key, Vector3 p, float yaw, float height, bool collider)
        {
            var go = PlaceAt(parent, key, p, yaw, height, collider);
            if (go != null) Physics.SyncTransforms(); // 다음 자리 검사가 이 소품을 보게
            return go;
        }

        /// <summary>
        /// 맵 가운데: 예전 연습용 벽·우물을 없애고, 같은 자리(PracticeSpot)에 긴 무너진 벽 – 판자 – 긴 무너진 벽을 세운다.
        /// 우물(PracticeWell)은 비어 있는 다른 빈터로 옮긴다. 훈련 상대는 PracticeSpot 옆에 선다.
        /// </summary>
        private static void BuildCenter(Scene scene)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == "Wall" || r.name == PracticeWellName || r.name == PracticeSpotName) Object.DestroyImmediate(r);
            Physics.SyncTransforms();
            var spot = new GameObject(PracticeSpotName);
            SceneManager.MoveGameObjectToScene(spot, scene);
            spot.transform.position = Ground(new Vector3(0f, 0f, 12f));
            foreach (var w in CenterWalls)
            {
                var p = Ground(new Vector3(w.x, 0f, w.z));
                var go = Place("LongWall", spot.transform, Quaternion.identity, spot.transform.InverseTransformPoint(p), new Vector3(w.len, 2.1f, 0.7f), false, false, $"{ModelPrefix}LongWall_{w.x:0.#}")
                         ?? Place("RuinWall", spot.transform, Quaternion.identity, spot.transform.InverseTransformPoint(p), new Vector3(w.len, 2.1f, 0.7f), false, false, $"{ModelPrefix}LongWall_{w.x:0.#}");
                if (go == null) continue;
                var col = go.AddComponent<BoxCollider>();
                var s = go.transform.lossyScale;
                col.center = go.transform.InverseTransformPoint(p + Vector3.up * 1.05f);
                col.size = new Vector3(w.len / Mathf.Max(Mathf.Abs(s.x), 1e-3f), 2.1f / Mathf.Max(Mathf.Abs(s.y), 1e-3f), 0.7f / Mathf.Max(Mathf.Abs(s.z), 1e-3f));
            }
            Physics.SyncTransforms();
            // 우물: 맵 구석으로
            foreach (var c in WellCandidates)
            {
                var p = Ground(new Vector3(c.x, 0f, c.y));
                if (!Clear(p, 1.8f) || Blocked(p, new Vector3(3f, 2.5f, 3f), 0f) || Near(p, MirrorSpots, 3.5f)) continue;
                var well = new GameObject(PracticeWellName);
                SceneManager.MoveGameObjectToScene(well, scene);
                well.transform.position = p;
                if (PlaceAt(well.transform, "Well", p, Hash(c.x, c.y) * 360f, 2.6f, collider: true) == null) Object.DestroyImmediate(well);
                else Debug.Log($"[BG] 우물을 {p} 로 옮겼습니다.");
                break;
            }
        }
    }
}

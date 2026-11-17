using System.Linq;
using Project1.Presentation;
using Project1.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 판자(생존자 전용 기물)를 메인 씬에 배치한다(씬에 저장되는 진짜 오브젝트 — 편집 화면에서도 보이고 손으로 옮길 수 있다). 메뉴: Tools/BG/판자 다시 배치.
    /// 자리 = 모두 벽 – 판자 – 벽: 양쪽이 사람 키 높이까지 막히고(벽·문틀·교각·큰 가구) 지나가는 쪽은 앞뒤로 트인 1.4~2.4m 틈.
    /// 맵 전체(1·2·3층)를 0.5m 간격으로 훑어(흩뿌린 소품·계단·병풍은 벽으로 안 침) 층·구역이 겹치지 않게 골랐다 — 문틀, 연결동 입구, 교각 옆, 3층 가구 사이 등.
    /// 판자는 한쪽 면에 기대 서서, 내리면 틈을 가로질러 반대쪽 면까지 넘어간다(길이 = 틈). 다시 실행하면 지난 판자를 지우고 다시 놓는다.
    /// </summary>
    public static class PalletPlacer
    {
        private const string RootName = "Pallets";

        // (기댈 면 바로 앞 바닥 x, y, z, 넘어가는 방향 x, z, 자리, 판자 길이 m = 틈 — 0이면 기본 2.1)
        private static readonly (float x, float y, float z, float dx, float dz, string by, float len)[] Spots =
        {
            // (영묘 문(묘지)·서쪽 연결동 2층 입구 판자는 뺌 — 어색한 자리)
            (-3.05f, 12.15f, -8.0f, 1f, 0f, "3층 하프 – 파이프 오르간", 1.55f),
            (-10.0f, 12.15f, -27.0f, 0f, 1f, "3층 서쪽 계단실 문", 2.4f),
            (13.0f, 6.15f, -27.0f, 0f, 1f, "2층 동쪽 계단실 문", 2.4f),
            (-13.0f, 0.15f, -27.0f, 0f, 1f, "1층 서쪽 계단실 문", 2.4f),
            (-22.0f, 12.15f, 9.5f, 0f, 1f, "서관 3층 복도 문", 2.4f),
            (-22.0f, 0.15f, 18.0f, 0f, 1f, "서관 1층 복도 문", 2.4f),
            // 판자 구역(야외 동쪽, MapDresser.BuildPalletZone): 무너진 벽 – 판자 – 무너진 벽. 판자는 한쪽 벽 끝에 기대 틈(2m)을 가로질러 반대쪽 벽까지 넘어간다.
            (31.68f, 0f, -21.0f, 1f, 0f, "판자 구역 북", 2.0f),
            (32.32f, 0f, -26.0f, -1f, 0f, "판자 구역 남", 2.0f),
            // 야외 구석 ㄱ자 판자 구역 4곳(MapDresser.ZoneWalls·ZoneWallsZ와 짝): x로 긴 벽 끝(cx+1)에 기대 모서리 틈(2m)을 +x로 가로질러 세로 벽까지. 건물 안에는 더 넣지 않는다
            (17.18f, 0f, 32f, 1f, 0f, "ㄱ자 판자 구역 북동", 2.0f),
            (33.18f, 0f, -42f, 1f, 0f, "ㄱ자 판자 구역 남동", 2.0f),
            (-10.82f, 0f, 40f, 1f, 0f, "ㄱ자 판자 구역 북서", 2.0f),
            (-30.82f, 0f, -4f, 1f, 0f, "ㄱ자 판자 구역 서", 2.0f),
            // (1층 사물함 방 가운데 통로를 가로지르던 판자는 뺌 — 사물함 줄 틈 판자 둘과 겹쳐 어색했음)
            // 맵 가운데(예전 연습용 벽·우물 자리): 긴 무너진 벽 – 판자 – 긴 무너진 벽(MapDresser.BuildCenter)
            (-0.82f, 0f, 12.0f, 1f, 0f, "가운데 긴 벽 사이", 2.0f),
        };

        [MenuItem("Tools/BG/판자 다시 배치")]
        public static void Place()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[BG] Play 중에는 판자를 배치하지 않습니다."); return; }
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            for (int i = 0; i < Spots.Length; i++)
            {
                var s = Spots[i];
                Vector3 d = new Vector3(s.dx, 0f, s.dz).normalized;
                var go = new GameObject($"Pallet_{i + 1:00} ({s.by})");
                go.transform.SetParent(root, false);
                Vector3 p = new(s.x, s.y, s.z);
                if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y; // 바닥(잡동사니 위로 튀어나온 조각에 걸리지 않게 낮은 데서 찾음)
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(d, Vector3.up)); // 앞(+Z) = 넘어가는 쪽(잡동사니 바깥)
                var pallet = go.AddComponent<Pallet>();
                if (s.len > 0f) pallet.Configure(s.len);
                go.AddComponent<PalletVisual>();
            }
            int auto = PlaceInGaps(root);
            Debug.Log($"[BG] 긴 가구 틈 판자 {auto}개");
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log($"[BG] 판자 {Spots.Length}개를 배치하고 씬을 저장했습니다.");
        }

        // 긴 가구를 끊은 틈(MapBlockoutBuilder): 사물함 줄(칸 사이가 1.4m 넘게 벌어진 곳), 둘로 나눈 책장·창고 선반 사이, 예배실 장의자 사이 통로.
        // 판자를 놓지 않는 사물함 줄 틈: 동관 1층 무너진 잔해 바로 옆(Lockers_B 3번 틈)
        private static readonly string[] SkipGaps = { "Lockers_B_3" };
        private static readonly string[] LockerRows ={ "Locker1F_A", "Locker1F_B", "Locker2F", "Lockers_A", "Lockers_B" };
        private static readonly (string a, string b)[] SplitPairs = { ("Shelf_7a", "Shelf_7b"), ("Storage_Rack_A", "Storage_Rack_B"), ("Pew_3_1.5", "Pew_3_7.1") }; // 마지막 = 예배실 가운데 통로(장의자 – 판자 – 장의자)

        /// <summary>긴 가구 틈마다 판자: 앞 칸 끝면에 기대 서서 틈을 가로질러 다음 칸까지 넘어간다(길이 = 틈).</summary>
        private static int PlaceInGaps(Transform root)
        {
            var map = GameObject.Find("MapBlockout");
            if (map == null) return 0;
            int n = 0;
            var all = map.GetComponentsInChildren<Transform>(true);
            foreach (var row in all.Where(t => LockerRows.Contains(t.name)))
            {
                var cells = row.Cast<Transform>().Where(c => c.name.StartsWith("Locker_") && c.GetComponent<Collider>() != null)
                               .OrderBy(c => int.TryParse(c.name.Substring(7), out int i) ? i : 0).ToList();
                for (int i = 0; i + 1 < cells.Count; i++)
                {
                    var b0 = cells[i].GetComponent<Collider>().bounds; var b1 = cells[i + 1].GetComponent<Collider>().bounds;
                    Vector3 d = b1.center - b0.center; d.y = 0f;
                    Vector3 dir = d.normalized;
                    float half0 = Mathf.Abs(Vector3.Dot(b0.extents, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z))));
                    float half1 = Mathf.Abs(Vector3.Dot(b1.extents, new Vector3(Mathf.Abs(dir.x), 0f, Mathf.Abs(dir.z))));
                    float gap = d.magnitude - half0 - half1;
                    if (gap < 1.4f) continue;
                    if (SkipGaps.Contains($"{row.name}_{i}")) continue;
                    Vector3 face = b0.center + dir * half0; face.y = b0.min.y;
                    Add(root, $"Pallet_Gap_{row.name}_{i}", face, dir, gap, "사물함 줄 틈"); n++;
                }
            }
            foreach (var (an, bn) in SplitPairs)
            {
                var ta = all.FirstOrDefault(t => t.name == an); var tb = all.FirstOrDefault(t => t.name == bn);
                if (ta != null && ta.GetComponent<Collider>() == null) ta = all.FirstOrDefault(t => t.name == an + "_0"); // 서재 책장 = 그룹(층마다 _0, _1 …) — 1층 칸
                if (tb != null && tb.GetComponent<Collider>() == null) tb = all.FirstOrDefault(t => t.name == bn + "_0");
                if (ta == null || tb == null || ta.GetComponent<Collider>() == null || tb.GetComponent<Collider>() == null) continue;
                var b0 = ta.GetComponent<Collider>().bounds; var b1 = tb.GetComponent<Collider>().bounds;
                Vector3 d = b1.center - b0.center; d.y = 0f;
                bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
                Vector3 dir = alongX ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
                float gap = alongX ? (d.x > 0 ? b1.min.x - b0.max.x : b0.min.x - b1.max.x) : (d.z > 0 ? b1.min.z - b0.max.z : b0.min.z - b1.max.z);
                if (gap < 1.2f) continue;
                Vector3 face = new(alongX ? (d.x > 0 ? b0.max.x : b0.min.x) : b0.center.x, b0.min.y, alongX ? b0.center.z : (d.z > 0 ? b0.max.z : b0.min.z));
                Add(root, $"Pallet_Gap_{an}", face, dir, gap, "긴 가구 틈"); n++;
            }
            return n;
        }

        private static void Add(Transform root, string name, Vector3 face, Vector3 dir, float gap, string by)
        {
            var go = new GameObject($"{name} ({by})");
            go.transform.SetParent(root, false);
            var pallet = go.AddComponent<Pallet>();
            pallet.Configure(Mathf.Max(1.2f, gap));
            Vector3 p = face + dir * (pallet.Thickness * 0.5f + 0.03f);
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(dir, Vector3.up));
            go.AddComponent<PalletVisual>();
        }
    }
}

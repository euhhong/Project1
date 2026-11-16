using Project1.Presentation;
using Project1.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 판자(생존자 전용 기물)를 메인 씬에 배치한다(씬에 저장되는 진짜 오브젝트 — 편집 화면에서도 보이고 손으로 옮길 수 있다). 메뉴: Tools/BG/판자 다시 배치.
    /// 자리 = 밋밋한 마당이나 방 안에 덩그러니 놓인 잡동사니(상자 무더기·찬장·바위·무너진 잔해·안뜰 받침돌) 옆 11곳 — 1층 바닥에 놓인 잡동사니 둘레가
    /// 비어 있고(16방향 평균 3m 넘게 트임), 기댈 면 바깥으로 2.4m·옆으로 1.1m씩 트인 곳을 맵에서 훑어 골랐다.
    /// 판자는 잡동사니 면에 기대 서서, 내리면 잡동사니 바깥쪽(앞 +Z)으로 넘어간다. 다시 실행하면 지난 판자를 지우고 다시 놓는다.
    /// </summary>
    public static class PalletPlacer
    {
        private const string RootName = "Pallets";

        // (기댈 면 바로 앞 바닥 x, y, z, 넘어가는 방향 x, z, 옆 잡동사니, 판자 길이 m — 0이면 기본 2.1)
        private static readonly (float x, float y, float z, float dx, float dz, string by, float len)[] Spots =
        {
            (39.22f, -0.22f, -8.00f, -1f, 0f, "상자", 0f),
            (-26.78f, -0.13f, 40.00f, -1f, 0f, "상자 무더기(북서)", 0f),
            (-32.78f, -0.11f, -42.00f, -1f, 0f, "상자 무더기(서)", 0f),
            (29.22f, -0.19f, -42.00f, -1f, 0f, "상자 무더기(동)", 0f),
            (-21.98f, 0.15f, -8.90f, -1f, 0f, "부엌 찬장(방 안)", 0f),
            (-45.06f, 0.02f, -17.08f, -0.05f, -1f, "정원 바위", 0f),
            (15.93f, 0.15f, -4.20f, 1f, 0f, "무너진 잔해", 0f),
            (22.67f, 0.15f, 11.50f, -1f, 0f, "무너진 잔해", 0f),
            (-10.82f, 0.07f, 26.00f, 1f, 0f, "안뜰 받침돌", 0f),
            (10.82f, 0.00f, 26.00f, -1f, 0f, "안뜰 받침돌", 0f),
            (-10.82f, 0.02f, 10.00f, 1f, 0f, "안뜰 받침돌", 0f),
            // 판자 구역(야외 동쪽, MapDresser.BuildPalletZone): 무너진 벽 – 판자 – 무너진 벽. 판자는 한쪽 벽 끝에 기대 틈(2m)을 가로질러 반대쪽 벽까지 넘어간다.
            (31.68f, 0f, -21.0f, 1f, 0f, "판자 구역 북", 2.0f),
            (32.32f, 0f, -26.0f, -1f, 0f, "판자 구역 남", 2.0f),
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
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Debug.Log($"[BG] 판자 {Spots.Length}개를 배치하고 씬을 저장했습니다.");
        }
    }
}

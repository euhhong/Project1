using Project1.Presentation;
using Project1.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map
{
    /// <summary>
    /// 판자 배치(맵): 메인 맵(MapBlockout)의 좁은 통로·상자 사이·빈 문틀(양쪽이 막히고 지나가는 쪽은 트인 너비 1.4~2.4m 틈 10곳 —
    /// 바닥을 0.75m 간격으로 훑어 고름)의 한쪽 벽에 판자(Pallet + PalletVisual)를 기대 세운다. 판자는 틈을 가로질러 반대쪽 벽까지 넘어가
    /// 길을 막도록 길이를 틈 너비에 맞춘다. 실행 중에 만든다(씬을 고치지 않음, 씬을 다시 불러도 다시 세움).
    /// </summary>
    public static class PalletSpawner
    {
        private const string MapRootName = "MapBlockout", PalletsRootName = "Pallets";

        // (한쪽 벽 면 x, z, 바닥 높이, 넘어가는 방향 x, z, 틈 너비)
        private static readonly (float ax, float az, float y, float dx, float dz, float gap)[] Spots =
        {
            (16.80f, -14.50f, 0.60f, 1f, 0f, 1.90f),    // 통로
            (-31.20f, 11.75f, 0.15f, 1f, 0f, 1.90f),    // 통로
            (-19.00f, -20.20f, 0.15f, 0f, -1f, 1.90f),  // 통로
            (37.00f, -16.00f, 1.00f, 1f, 0f, 2.00f),    // 문틀·좁은 틈
            (-22.00f, -2.20f, 0.15f, 0f, -1f, 2.00f),
            (33.50f, 38.95f, 0.15f, 0f, -1f, 1.78f),
            (2.00f, -20.23f, 0.15f, 0f, -1f, 1.70f),
            (21.50f, 5.80f, -0.08f, 0f, -1f, 1.55f),
            (-40.00f, 40.70f, 0.15f, 0f, -1f, 2.40f),
            (31.25f, 14.80f, 0.15f, 0f, -1f, 1.38f),
        };

        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHook() => hooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!hooked) { hooked = true; SceneManager.sceneLoaded += (s, m) => Spawn(); }
            Spawn();
        }

        private static void Spawn()
        {
            if (GameObject.Find(MapRootName) == null || GameObject.Find(PalletsRootName) != null) return;
            var root = new GameObject(PalletsRootName).transform;
            for (int i = 0; i < Spots.Length; i++)
            {
                var s = Spots[i];
                Vector3 d = new Vector3(s.dx, 0f, s.dz).normalized;
                var go = new GameObject($"Pallet_{i + 1}");
                go.transform.SetParent(root, false);
                var pallet = go.AddComponent<Pallet>();
                pallet.Configure(Mathf.Clamp(s.gap, 1.6f, 2.4f)); // 누우면 틈을 가로질러 반대쪽 벽에 닿는 길이
                Vector3 p = new Vector3(s.ax, s.y, s.az) + d * (pallet.Thickness * 0.5f + 0.03f); // 한쪽 벽에 붙여 세움
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(d, Vector3.up)); // 앞(+Z) = 넘어가는 쪽
                go.AddComponent<PalletVisual>();
            }
        }
    }
}

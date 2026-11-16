using Project1.Presentation;
using Project1.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map
{
    /// <summary>
    /// 판자 배치(맵): 메인 맵(MapBlockout)의 밋밋한 빈터(장애물에서 5~9m 떨어진 넓은 마당 10곳 — 바닥을 1.5m 간격으로 훑어 고름)에
    /// 판자(Pallet + PalletVisual)를 하나씩 세운다. 판자 면은 맵 가운데를 본다. 실행 중에 만든다(씬을 고치지 않음, 씬을 다시 불러도 다시 세움).
    /// </summary>
    public static class PalletSpawner
    {
        private const string MapRootName = "MapBlockout", PalletsRootName = "Pallets";

        // 빈터 자리(x, z). 높이는 바닥을 찾아 맞춘다.
        private static readonly Vector2[] Spots =
        {
            new(32f, -23.5f), new(-8.5f, 18.5f), new(32f, -4f), new(8f, 18.5f), new(-34f, -23.5f),
            new(-34f, -8.5f), new(-37f, -37f), new(36.5f, -37f), new(-19f, 38f), new(18.5f, 36.5f),
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
                Vector3 p = new(Spots[i].x, 0f, Spots[i].y);
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) p = hit.point;
                Vector3 face = Vector3.ProjectOnPlane(-p, Vector3.up);
                var go = new GameObject($"Pallet_{i + 1}");
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(face.sqrMagnitude > 1e-3f ? face.normalized : Vector3.forward, Vector3.up));
                go.AddComponent<Pallet>();
                go.AddComponent<PalletVisual>();
            }
        }
    }
}

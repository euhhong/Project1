using Project1.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Presentation
{
    /// <summary>
    /// 게임 중 탐색 배경음악(FE): 플레이를 시작하면 저절로 생긴다(붙일 것 없음).
    /// 내 캐릭터(LocalPlayerInput) 가까이(NearRange m) 적이 없을 때:
    /// - 실내(카메라 CameraIndoorSensor — 머리 위 천장) = Resources/Bgm/Indoor
    /// - 야외 = Resources/Bgm/Outdoor
    /// 적이 가까이 오면(조우) 끈다. 적이 멀어진 뒤에는 ClearDelay초 지나야 다시 튼다. 실내·야외는 IndoorHold초 이어져야 바꾼다(문턱에서 깜빡이지 않게).
    /// 내 캐릭터가 없는 씬(타이틀·캐릭터 선택)에서는 건드리지 않는다. Suppressed = true면(게임 종료 창) 쉰다 — 씬이 바뀌면 풀린다.
    /// </summary>
    public class ExploreBgm : MonoBehaviour
    {
        public const string IndoorTrack = "Indoor", OutdoorTrack = "Outdoor";
        private const float CheckInterval = 0.25f;
        private const float NearRange = 20f;   // 이 안에 적이 있으면 '조우'
        private const float ClearDelay = 3f;   // 적이 멀어진 뒤 다시 틀기까지(초)
        private const float IndoorHold = 1f;   // 실내·야외가 이만큼 이어져야 곡을 바꾼다(초)
        private const float IndoorThreshold = 0.5f;

        private static ExploreBgm instance;

        /// <summary>true면 탐색 음악을 쉰다(예: 게임 종료 창의 음악을 틀 때).</summary>
        public static bool Suppressed { get; set; }

        private float checkAt, enemySeenAt = float.NegativeInfinity, indoorChangedAt;
        private bool indoor, rawIndoor;
        private string playing; // 이 스크립트가 튼 곡(null = 안 틂)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("ExploreBgm");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ExploreBgm>();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            Suppressed = false;
            playing = null; // 새 씬이 제 음악을 정한다
            enemySeenAt = float.NegativeInfinity;
        }

        private void Update()
        {
            if (Time.unscaledTime < checkAt) return;
            checkAt = Time.unscaledTime + CheckInterval;

            var input = FindAnyObjectByType<LocalPlayerInput>();
            if (input == null || Suppressed) { playing = null; return; } // 게임 씬이 아니거나 종료 창 — 다른 쪽이 음악을 정한다

            bool nowIndoor = IsIndoor();
            if (nowIndoor != rawIndoor) { rawIndoor = nowIndoor; indoorChangedAt = Time.unscaledTime; }
            if (playing == null || Time.unscaledTime - indoorChangedAt >= IndoorHold) indoor = rawIndoor;

            if (EnemyNear(input.gameObject)) enemySeenAt = Time.unscaledTime;
            bool calm = Time.unscaledTime - enemySeenAt >= ClearDelay;
            string want = calm ? (indoor ? IndoorTrack : OutdoorTrack) : null;
            if (want == playing) return;
            playing = want;
            if (want != null) BgmPlayer.Play(want);
            else BgmPlayer.Stop();
        }

        private static bool IsIndoor()
        {
            var cam = Camera.main;
            return cam != null && cam.TryGetComponent(out CameraIndoorSensor sensor) && sensor.Indoor01 >= IndoorThreshold;
        }

        private static bool EnemyNear(GameObject player)
        {
            if (!player.TryGetComponent(out CharacterIdentity me)) return false;
            Vector3 p = player.transform.position;
            foreach (var other in FindObjectsByType<CharacterIdentity>())
            {
                if (other == me || !other.isActiveAndEnabled || !me.CanHit(other)) continue;
                if ((other.transform.position - p).sqrMagnitude <= NearRange * NearRange) return true;
            }
            return false;
        }
    }
}

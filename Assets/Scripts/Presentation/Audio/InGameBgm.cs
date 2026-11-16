using Project1.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Presentation
{
    /// <summary>
    /// 게임 중 배경음악(FE): 플레이를 시작하면 저절로 생긴다(붙일 것 없음). 내 캐릭터(LocalPlayerInput)와 가장 가까운 적의 거리로 고른다:
    /// - 적이 가까이(NearRange m 안, 조우) = Resources/Bgm/Chase (실내·야외 모두, 모든 캐릭터 같은 곡).
    /// - 3층(실내·테라스 모두, 지붕 위는 빼고) = Resources/Bgm/Floor3
    /// - 실내(카메라 CameraIndoorSensor — 머리 위 천장) = Resources/Bgm/Indoor
    /// - 야외 = Resources/Bgm/Outdoor, 야외에서 적이 완전히 멀면(FarRange m 넘게, 적이 없어도) = Resources/Bgm/OutdoorFar
    /// 적이 멀어진 뒤에도 ClearDelay초는 Chase를 이어 간다. 실내·야외는 IndoorHold초 이어져야 바꾼다(문턱에서 깜빡이지 않게).
    /// FixedTrack이 있으면(연습 모드) 그 곡만 튼다.
    /// 내 캐릭터가 없는 씬(타이틀·캐릭터 선택)에서는 건드리지 않는다. Suppressed = true면(게임 종료 창) 쉰다 — 씬이 바뀌면 풀린다.
    /// </summary>
    public class InGameBgm : MonoBehaviour
    {
        public const string IndoorTrack = "Indoor", OutdoorTrack = "Outdoor", OutdoorFarTrack = "OutdoorFar", ChaseTrack = "Chase", Floor3Track = "Floor3";
        // 3층(바닥 높이 12.15m, MapBlockoutBuilder 층고 6m): 발이 Floor3Enter 위면 3층, Floor3Leave 밑으로 내려가면 풀림. 지붕(RoofHeight, RoofSunBurn과 같음) 위는 3층 아님.
        private const float Floor3Enter = 11.5f, Floor3Leave = 10.5f, RoofHeight = 16.5f;
        private const float CheckInterval = 0.25f;
        private const float NearRange = 20f;   // 이 안에 적이 있으면 '조우'
        private const float FarRange = 40f;    // 가장 가까운 적이 이보다 멀면 '완전히 멂'(돌아올 땐 FarRange - FarMargin 안으로 와야 풀림)
        private const float FarMargin = 5f;
        private const float ClearDelay = 3f;   // 적이 멀어진 뒤 Chase를 이어 가는 시간(초)
        private const float IndoorHold = 1f;   // 실내·야외가 이만큼 이어져야 곡을 바꾼다(초)
        private const float IndoorThreshold = 0.5f;

        private static InGameBgm instance;

        /// <summary>true면 게임 중 음악을 쉰다(예: 게임 종료 창의 음악을 틀 때).</summary>
        public static bool Suppressed { get; set; }

        /// <summary>null이 아니면 상황과 상관없이 이 곡만 튼다(예: 연습 모드 = Practice). 게임 씬을 열 때 CharacterSelectBootstrap이 정한다.</summary>
        public static string FixedTrack { get; set; }

        private float checkAt, enemySeenAt = float.NegativeInfinity, indoorChangedAt;
        private bool indoor, rawIndoor, far = true, floor3;
        private string playing; // 이 스크립트가 튼 곡(null = 안 틂)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("InGameBgm");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<InGameBgm>();
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

            float d = NearestEnemy(input.gameObject);
            if (d <= NearRange) enemySeenAt = Time.unscaledTime;
            if (far && d < FarRange - FarMargin) far = false;
            else if (!far && d > FarRange) far = true;
            bool calm = Time.unscaledTime - enemySeenAt >= ClearDelay;
            float y = input.transform.position.y;
            if (!floor3 && y >= Floor3Enter && y < RoofHeight) floor3 = true;
            else if (floor3 && (y < Floor3Leave || y >= RoofHeight)) floor3 = false;
            string want = FixedTrack ?? (!calm ? ChaseTrack : floor3 ? Floor3Track : indoor ? IndoorTrack : far ? OutdoorFarTrack : OutdoorTrack);
            if (want == playing) return;
            playing = want;
            BgmPlayer.Play(want);
        }

        private static bool IsIndoor()
        {
            var cam = Camera.main;
            return cam != null && cam.TryGetComponent(out CameraIndoorSensor sensor) && sensor.Indoor01 >= IndoorThreshold;
        }

        /// <summary>가장 가까운 적까지 거리(m). 적이 없으면 무한대.</summary>
        private static float NearestEnemy(GameObject player)
        {
            if (!player.TryGetComponent(out CharacterIdentity me)) return float.PositiveInfinity;
            Vector3 p = player.transform.position;
            float best = float.PositiveInfinity;
            foreach (var other in FindObjectsByType<CharacterIdentity>())
            {
                if (other == me || !other.isActiveAndEnabled || !me.CanHit(other)) continue;
                float d = Vector3.Distance(other.transform.position, p);
                if (d < best) best = d;
            }
            return best;
        }
    }
}

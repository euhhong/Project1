using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 기절할 수 있는 모든 캐릭터(CharacterStun — 분신·훈련 상대처럼 CharacterLoadout을 거치지 않은 것, 맞는 순간 기절이 붙는 것 포함)에
    /// StunDizzyIndicator를 붙인다. 게임이 시작되면 스스로 생겨 잠깐마다 살핀다. 씬에 놓을 필요 없음.
    /// </summary>
    public class StunDizzyAttacher : MonoBehaviour
    {
        private const float ScanInterval = 0.2f;
        private float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<StunDizzyAttacher>() != null) return;
            var go = new GameObject("StunDizzyAttacher") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<StunDizzyAttacher>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanInterval;
            foreach (var mb in FindObjectsByType<CharacterStun>(FindObjectsSortMode.None))
            {
                if (!mb.TryGetComponent(out StunDizzyIndicator _))
                    mb.gameObject.AddComponent<StunDizzyIndicator>();
            }
        }
    }
}

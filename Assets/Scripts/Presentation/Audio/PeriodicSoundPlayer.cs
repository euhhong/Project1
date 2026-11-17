using System.Collections;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 일정 시간마다 같은 소리를 그 자리에서 낸다(FE) — 예: 도둑 도발 소리(Resources/Sfx/Characters/Thief/Thief_Taunt_01·02).
    /// 캐릭터 루트에 붙이면 끝(인스펙터 연결 없음). 소리 목록은 Resources 경로로 정하고, 여러 개면 같은 소리가 연달아 나오지 않게 고른다.
    /// 앞 소리가 끝나야 다음 간격을 센다. 음량 = 효과음 음량(AudioVolume.Sfx). 소리 파일이 없으면 조용히 넘어간다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PeriodicSoundPlayer : MonoBehaviour
    {
        [Tooltip("Resources 안의 소리 경로(확장자 없이). 여러 개면 번갈아 고른다.")]
        [SerializeField] private string[] clipResources = { "Sfx/Characters/Thief/Thief_Taunt_01", "Sfx/Characters/Thief/Thief_Taunt_02", "Sfx/Characters/Thief/Thief_Emote_Other_01" };

        [Tooltip("한 번 나고 다음이 날 때까지(초). 소리 길이는 빼고 센다.")]
        [Min(0.1f)] [SerializeField] private float interval = 10f;

        [Tooltip("간격을 매번 ± 이만큼 흔든다(초). 0이면 정확히.")]
        [Min(0f)] [SerializeField] private float intervalJitter = 0f;

        [Tooltip("처음 소리까지 기다리는 시간(초).")]
        [Min(0f)] [SerializeField] private float initialDelay = 0f;

        [Range(0f, 1f)] [SerializeField] private float volume = 1f;

        private AudioSource source;
        private AudioClip[] clips;
        private Coroutine loop;
        private int lastIndex = -1;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0.85f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f; source.maxDistance = 40f;
            source.dopplerLevel = 0f;

            clips = new AudioClip[clipResources.Length];
            for (int i = 0; i < clipResources.Length; i++)
                clips[i] = Resources.Load<AudioClip>(clipResources[i]);
        }

        private void OnEnable() => loop = StartCoroutine(Loop());

        private void OnDisable()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
            if (source != null) source.Stop();
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source);
        }

        private IEnumerator Loop()
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);
            while (true)
            {
                PlayOnce();
                while (source.isPlaying) { if (InGameBgm.Suppressed) source.Stop(); yield return null; } // 종료 음악이 시작되면 하던 소리도 끊는다
                yield return new WaitForSeconds(Mathf.Max(0.05f, interval + Random.Range(-intervalJitter, intervalJitter)));
            }
        }

        /// <summary>지금 바로 한 번 낸다(다른 스크립트·애니메이션 이벤트에서도 부를 수 있다).</summary>
        public void PlayOnce()
        {
            if (clips == null || clips.Length == 0) return;
            if (InGameBgm.Suppressed) { source.Stop(); return; } // 게임 종료 음악이 나오는 동안은 내지 않는다(겹침)
            int i = Random.Range(0, clips.Length);
            if (clips.Length > 1 && i == lastIndex) i = (i + 1) % clips.Length;
            lastIndex = i;
            if (clips[i] == null) return;
            source.clip = clips[i];
            source.volume = volume * AudioVolume.Sfx;
            source.Play();
        }
    }
}

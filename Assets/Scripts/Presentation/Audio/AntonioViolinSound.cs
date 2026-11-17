using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 안토니오 바이올린 연주 소리(FE): 바이올린을 켜는 동안(AntonioViolin.Playing01 — 대기·음표 놓기 전후) Resources/Sfx/violin_play를 그 자리에서 튼다.
    /// 켜기를 멈추면 줄어들며 잠깐 멈추고, 곧 다시 켜면 이어서, RestartAfter초 넘게 쉬었으면 처음부터 튼다.
    /// AntonioViolin이 붙일 때 같이 붙인다(따로 붙일 것 없음). 바이올린이 없어지면 스스로 빠진다. 음량 = 효과음 음량.
    /// </summary>
    [DisallowMultipleComponent]
    public class AntonioViolinSound : MonoBehaviour
    {
        private const string ClipResource = "Sfx/violin_play";
        private const float Volume = 0.8f;
        private const float RestartAfter = 4f;

        private AntonioViolin violin;
        private AudioSource source;
        private float silentSince = float.NegativeInfinity;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(ClipResource);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.85f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 3f; source.maxDistance = 30f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private void OnDisable()
        {
            if (source != null) { source.Pause(); source.volume = 0f; }
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source);
        }

        private void Update()
        {
            if (violin == null && !TryGetComponent(out violin)) { Destroy(this); return; }
            if (source.clip == null) return;
            if (InGameBgm.Suppressed) { if (source.isPlaying) { source.Pause(); silentSince = Time.time; } return; } // 게임 종료 음악이 나오는 동안은 바이올린 소리를 내지 않는다(겹침)

            float w = violin.Playing01;
            source.volume = w * Volume * AudioVolume.Sfx;
            if (w > 0.01f)
            {
                if (!source.isPlaying)
                {
                    if (Time.time - silentSince > RestartAfter) { source.Stop(); source.time = 0f; source.Play(); }
                    else source.UnPause();
                }
                silentSince = float.PositiveInfinity;
            }
            else if (source.isPlaying)
            {
                source.Pause();
                silentSince = Time.time;
            }
        }
    }
}

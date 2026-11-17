using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 긴 기절의 병아리 소리(FE): 기절 시간이 MinStun초 이상이면 기절한 동안 머리 위(병아리가 도는 자리)에서
    /// Resources/Sfx/stun_chick(삐약 소리 묶음)을 작게 반복한다. 기절이 풀리면 멈춘다.
    /// SfxPlayer가 캐릭터마다 저절로 붙인다(붙일 것 없음). 음량 = 효과음 음량.
    /// </summary>
    [DisallowMultipleComponent]
    public class StunChickSound : MonoBehaviour
    {
        private const string ClipResource = "Sfx/stun_chick";
        private const float MinStun = 1.5f, Volume = 0.22f;

        private AudioSource source;
        private CharacterStun stun;

        private void Awake()
        {
            var holder = new GameObject("StunChickSound");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = Vector3.up * 2.2f; // 머리 위 병아리 언저리
            source = holder.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(ClipResource);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.85f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f; source.maxDistance = 25f;
            source.dopplerLevel = 0f;
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source.gameObject);
        }

        private void Update()
        {
            if (source.clip == null) return;
            if (stun == null && !TryGetComponent(out stun)) return; // 기절은 처음 맞을 때 붙는다
            bool on = stun.IsStunned && stun.StunDuration >= MinStun;
            if (on)
            {
                source.volume = Volume * AudioVolume.Sfx;
                if (!source.isPlaying) { source.time = 0f; source.Play(); }
            }
            else if (source.isPlaying) source.Stop();
        }
    }
}

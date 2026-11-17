using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 심장 소리(FE): 플레이어가 생존자일 때 상대 감시자가 가까이 오면 Resources/Sfx/Characters/Common/Survivor_Heartbeat_01을 반복해 들려준다.
    /// Range m 안에서 가까울수록 크고 조금 빠르게(높낮이). 듣는 사람에게만(2D). CharacterLoadout이 플레이어 생존자에게만 붙인다.
    /// 감시자는 CharacterSoundSet.IsHunter로 알아본다. 음량 = 효과음 음량.
    /// </summary>
    [DisallowMultipleComponent]
    public class SurvivorHeartbeat : MonoBehaviour
    {
        private const string ClipResource = "Sfx/Characters/Common/Survivor_Heartbeat_01";
        private const float Range = 32f, Volume = 0.9f;

        private AudioSource source;
        private CharacterIdentity identity;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>(ClipResource);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            identity = GetComponent<CharacterIdentity>();
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source);
        }

        private void Update()
        {
            if (source.clip == null) return;
            float nearest = float.PositiveInfinity;
            foreach (var other in CharacterSoundSet.All)
            {
                if (!other.IsHunter || other.gameObject == gameObject) continue;
                if (identity != null && other.TryGetComponent(out CharacterIdentity o) && !identity.CanHit(o) && !o.CanHit(identity)) continue; // 같은 편
                nearest = Mathf.Min(nearest, Vector3.Distance(transform.position, other.transform.position));
            }
            float w = Mathf.Clamp01(1f - nearest / Range);
            w *= w * (3f - 2f * w); // 멀리서는 아주 작게, 가까우면 빨리 커짐
            source.volume = w * Volume * AudioVolume.Sfx;
            source.pitch = Mathf.Lerp(1f, 1.15f, w);
            if (w > 0.01f) { if (!source.isPlaying) source.Play(); }
            else if (source.isPlaying) source.Stop();
        }
    }
}

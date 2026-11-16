using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 효과음 재생(FE): 플레이를 시작하면 저절로 생긴다(붙일 것 없음). 소리는 Resources/Sfx/{이름}(Tools/FE/효과음 만들기로 합성한 WAV).
    /// - 전역 이벤트: 맞음(가벼움·무거움) / 막기·패링 / 벽 치기 / 충격파.
    /// - 캐릭터마다(1초마다 새 캐릭터를 찾아 CharacterSfx를 붙인다): 휘두르기·점프·착지·기절·스킬·판자 쓰러짐·거울 순간이동.
    /// 소리는 그 자리(3D, 40m까지 들림)에서 나고, 같은 소리는 조금씩 높낮이를 흔든다. 소리 파일이 없으면 조용히 넘어간다.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        private const int Voices = 24;
        private static SfxPlayer instance;
        private readonly Dictionary<string, AudioClip> clips = new();
        private readonly List<AudioSource> pool = new();
        private int next;
        private float scanAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("SfxPlayer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SfxPlayer>();
        }

        /// <summary>name 소리를 pos에서. volume 0~1, pitchJitter = 높낮이 흔들기 폭(0.06 = ±6%).</summary>
        public static void Play(string name, Vector3 pos, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            if (instance == null) return;
            instance.PlayAt(name, pos, volume, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)));
        }

        private void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var s = new GameObject($"Voice_{i}").AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 0.85f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f; s.maxDistance = 40f;
                s.dopplerLevel = 0f;
                pool.Add(s);
            }
            HitEvents.HitOccurred += OnHit;
            HitEvents.HitBlocked += OnBlocked;
            WallBounceEvents.Struck += OnWall;
            ShockwaveEvents.Emitted += OnShockwave;
        }

        private void OnDestroy()
        {
            HitEvents.HitOccurred -= OnHit;
            HitEvents.HitBlocked -= OnBlocked;
            WallBounceEvents.Struck -= OnWall;
            ShockwaveEvents.Emitted -= OnShockwave;
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (Time.unscaledTime < scanAt) return;
            scanAt = Time.unscaledTime + 1f;
            foreach (var c in FindObjectsByType<CharacterIdentity>())
                if (!c.TryGetComponent(out CharacterSfx _)) c.gameObject.AddComponent<CharacterSfx>();
        }

        private void PlayAt(string name, Vector3 pos, float volume, float pitch)
        {
            var clip = Load(name);
            if (clip == null) return;
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.transform.position = pos;
            s.pitch = pitch;
            s.volume = Mathf.Clamp01(volume);
            s.clip = clip;
            s.Play();
        }

        private AudioClip Load(string name)
        {
            if (!clips.TryGetValue(name, out var c)) { c = Resources.Load<AudioClip>($"Sfx/{name}"); clips[name] = c; }
            return c;
        }

        private static void OnHit(HitResult hit)
        {
            bool heavy = hit.Knockdown || hit.PowerScale > 1.25f || hit.Charge > 0.5f; // 쓰러뜨림·센 공격·모아 친 공격
            Play(heavy ? "hit_heavy" : "hit_light", hit.Point, heavy ? 1f : 0.8f);
        }

        private static void OnBlocked(HitResult hit) => Play("block", hit.Point, 0.9f, 0.05f);
        private static void OnWall(GameObject attacker, Vector3 point, Vector3 normal) => Play("wall", point, 0.85f);
        private static void OnShockwave(GameObject source, Vector3 center, float radius) => Play("shockwave", center, 1f, 0.04f);
    }
}

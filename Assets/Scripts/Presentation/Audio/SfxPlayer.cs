using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 효과음 재생(FE): 플레이를 시작하면 저절로 생긴다(붙일 것 없음). 소리는 Resources/Sfx/{이름}.wav(VARCO Sound로 만들어 앞뒤 무음을 자르고 크기를 맞춘 것).
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

        /// <summary>화면(UI) 소리: name 소리를 듣는 자리(AudioListener)에서 — 메뉴 고르기·캐릭터 넘기기 등. 높낮이는 조금씩 흔든다.</summary>
        public static void PlayUi(string name, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            if (instance == null) return;
            var listener = FindAnyObjectByType<AudioListener>();
            Vector3 at = listener != null ? listener.transform.position : Vector3.zero;
            instance.PlayAt(name, at, volume, pitch * (1f + Random.Range(-pitchJitter, pitchJitter)), 0f);
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
                if (!c.TryGetComponent(out CharacterSfx _)) { c.gameObject.AddComponent<CharacterSfx>(); c.gameObject.AddComponent<StunChickSound>(); } // 긴 기절 병아리 소리도
        }

        private void PlayAt(string name, Vector3 pos, float volume, float pitch, float spatial = 0.85f)
        {
            var clip = Load(name);
            if (clip == null) return;
            var s = pool[next]; next = (next + 1) % pool.Count;
            s.transform.position = pos;
            s.spatialBlend = spatial; // 화면(UI) 소리는 0 = 어디서든 같은 크기(씬이 바뀌어도 끝까지 들림)
            s.pitch = pitch;
            s.volume = Mathf.Clamp01(volume) * AudioVolume.Sfx; // 환경설정의 효과음 음량
            s.clip = clip;
            s.Play();
        }

        private AudioClip Load(string name)
        {
            if (!clips.TryGetValue(name, out var c)) { c = Resources.Load<AudioClip>($"Sfx/{name}"); clips[name] = c; }
            return c;
        }

        // 벽 치기 소리: 두 음절로 길고 청량한 "뽀-익" — 짧고 낮게 "뽀"(40ms) 뒤 밝고 길게 "이익"(400ms)인 사냥터지기 소리(제5인격 녹화). 벽에 부딪혔을 때만 난다.
        // → 사람 몸을 치는 소리처럼 들려 바꿈: 돌벽에 부딪히는 둔탁한 소리(VARCO wall)
        public const string WallHitSound = "wall";
        // 캐릭터 피격음: 높게 울리는 타격음 — 안토니오 평타 맞힘 소리(Antonio_Attack_Hit_01)의 앞 0.56초만(hit_char). 뒤에 남자 목소리가 섞여 있어 잘라 냄. 누가 맞든 이것 하나
        public const string CharacterHitSound = "hit_char";

        private static void OnHit(HitResult hit)
        {
            if (hit.Silent) return; // 타격음 없는 히트(카우보이 올가미 피해)
            bool heavy = hit.Knockdown || hit.PowerScale > 1.25f || hit.Charge > 0.5f; // 쓰러뜨림·센 공격·모아 친 공격
            Play(CharacterHitSound, hit.Point, heavy ? 1f : 0.8f, 0.05f, heavy ? 0.95f : 1.03f); // 센 공격은 조금 크고 낮게
        }

        private static void OnBlocked(HitResult hit) => Play("block", hit.Point, 0.9f, 0.05f);
        private static void OnWall(GameObject attacker, Vector3 point, Vector3 normal) => Play(WallHitSound, point, 1f, 0.05f); // 벽에 부딪혔을 때만 "뽀-익"
        private static void OnShockwave(GameObject source, Vector3 center, float radius) => Play("shockwave", center, 1f, 0.04f);
    }
}

using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 배경음악 재생(FE): 처음 부를 때 저절로 생기고 씬이 바뀌어도 남는다(붙일 것 없음). 곡은 Resources/Bgm/{이름}.
    /// - Play(이름, 반복): 그 곡을 재생(기본 반복). 다른 곡이 나오던 중이면 서로 겹치며 바뀐다(크로스페이드). 같은 곡이면 그대로 둔다.
    /// - Stop(): 천천히 줄이며 끈다.
    /// 음량 = 배경음악 음량(AudioVolume.Bgm, 옵션에서 바꾸면 바로 적용). 2D 소리.
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        private const float FadeTime = 1f;
        private static BgmPlayer instance;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly float[] levels = new float[2]; // 페이드 정도 0~1
        private int current; // 지금 커지는 쪽(나머지는 줄어든다)
        private string currentName;

        private static BgmPlayer Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("BgmPlayer");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<BgmPlayer>();
                }
                return instance;
            }
        }

        /// <summary>name 곡(Resources/Bgm/name)을 재생(loop = false면 한 번만). 이미 나오는 곡이면 아무것도 안 함.</summary>
        public static void Play(string name, bool loop = true) => Instance.Switch(name, loop);

        /// <summary>지금 곡을 천천히 줄이며 끈다.</summary>
        public static void Stop()
        {
            if (instance != null) instance.Switch(null, true);
        }

        private void Awake()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.volume = 0f;
                sources[i] = s;
            }
        }

        private void Switch(string name, bool loop)
        {
            if (name == currentName) return;
            currentName = name;
            var clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>($"Bgm/{name}");
            if (!string.IsNullOrEmpty(name) && clip == null) Debug.LogWarning($"[BGM] Resources/Bgm/{name} 곡이 없습니다.");

            current = 1 - current; // 새 곡은 다른 소스에서 0부터 키운다
            var s = sources[current];
            s.Stop();
            s.clip = clip;
            s.loop = loop;
            levels[current] = 0f;
            if (clip != null) s.Play();
        }

        private void OnEnable()
        {
            if (instance == null) instance = this; // Play 중 다시 컴파일된 뒤에도 이 플레이어를 쓴다(새로 만들지 않게)
        }

        /// <summary>붙어 있는 AudioSource 둘을 다시 찾는다(없으면 만든다).</summary>
        private void Rebind()
        {
            var found = GetComponents<AudioSource>();
            if (found.Length < sources.Length) { Awake(); return; }
            for (int i = 0; i < sources.Length; i++) sources[i] = found[i];
        }

        private void Update()
        {
            if (sources[0] == null) Rebind(); // Play 중 다시 컴파일되면 배열이 비어 있다
            float step = Time.unscaledDeltaTime / FadeTime;
            for (int i = 0; i < sources.Length; i++)
            {
                var s = sources[i];
                bool rising = i == current && s.clip != null;
                levels[i] = Mathf.MoveTowards(levels[i], rising ? 1f : 0f, step);
                s.volume = levels[i] * AudioVolume.Bgm;
                if (!rising && levels[i] <= 0f && s.isPlaying) s.Stop();
            }
        }
    }
}

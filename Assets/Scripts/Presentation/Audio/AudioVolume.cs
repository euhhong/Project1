using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 음량 설정(전체·효과음·배경음, 0~1). PlayerPrefs에 저장하고 게임을 켤 때 불러온다.
    /// 전체 = AudioListener.volume(모든 소리), 효과음 = SfxPlayer가 곱해 쓴다, 배경음 = 배경음악을 넣을 때 쓰도록 미리 둔 값.
    /// </summary>
    public static class AudioVolume
    {
        /// <summary>처음 켤 때(저장된 값이 없을 때) 음량 — 모두 50.</summary>
        public const float Default = 0.5f;
        private const string MasterKey = "Volume.Master", SfxKey = "Volume.Sfx", BgmKey = "Volume.Bgm";

        public static float Master { get; private set; } = Default;
        public static float Sfx { get; private set; } = Default;
        public static float Bgm { get; private set; } = Default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Master = PlayerPrefs.GetFloat(MasterKey, Default);
            Sfx = PlayerPrefs.GetFloat(SfxKey, Default);
            Bgm = PlayerPrefs.GetFloat(BgmKey, Default);
            AudioListener.volume = Master;
        }

        public static void SetMaster(float v) { Master = Mathf.Clamp01(v); AudioListener.volume = Master; PlayerPrefs.SetFloat(MasterKey, Master); }
        public static void SetSfx(float v) { Sfx = Mathf.Clamp01(v); PlayerPrefs.SetFloat(SfxKey, Sfx); }
        public static void SetBgm(float v) { Bgm = Mathf.Clamp01(v); PlayerPrefs.SetFloat(BgmKey, Bgm); }

        public static void Save() => PlayerPrefs.Save();
    }
}

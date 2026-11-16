using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 음량 설정(전체·효과음·배경음, 0~1). PlayerPrefs에 저장하고 게임을 켤 때 불러온다.
    /// 전체 = AudioListener.volume(모든 소리), 효과음 = SfxPlayer가 곱해 쓴다, 배경음 = 배경음악을 넣을 때 쓰도록 미리 둔 값.
    /// </summary>
    public static class AudioVolume
    {
        private const string MasterKey = "Volume.Master", SfxKey = "Volume.Sfx", BgmKey = "Volume.Bgm";

        public static float Master { get; private set; } = 1f;
        public static float Sfx { get; private set; } = 1f;
        public static float Bgm { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            Master = PlayerPrefs.GetFloat(MasterKey, 1f);
            Sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            Bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
            AudioListener.volume = Master;
        }

        public static void SetMaster(float v) { Master = Mathf.Clamp01(v); AudioListener.volume = Master; PlayerPrefs.SetFloat(MasterKey, Master); }
        public static void SetSfx(float v) { Sfx = Mathf.Clamp01(v); PlayerPrefs.SetFloat(SfxKey, Sfx); }
        public static void SetBgm(float v) { Bgm = Mathf.Clamp01(v); PlayerPrefs.SetFloat(BgmKey, Bgm); }

        public static void Save() => PlayerPrefs.Save();
    }
}

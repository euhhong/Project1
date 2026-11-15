using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 도둑 손전등에 정면으로 지져져 기절하면(LightExposure.Blinded) 내 화면이 하얗게 번쩍여 시야를 가린다(FE).
    /// 내가 조종하는 캐릭터(LocalPlayerInput)가 기절했을 때만 보인다. 처음 holdTime초는 거의 하얗게, 그 뒤 기절이 끝날 즈음까지 서서히 걷힌다.
    /// 씬이 시작될 때 스스로 하나 만들어진다(인스펙터 연결 없음).
    /// </summary>
    public class LightBlindFlash : MonoBehaviour
    {
        [SerializeField] private Color flashColor = new(1f, 0.98f, 0.9f, 1f);
        [Tooltip("번쩍인 직후 가장 밝게 유지하는 시간(초) / 그때 불투명도.")]
        [SerializeField, Min(0f)] private float holdTime = 0.3f;
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.95f;
        [Tooltip("걷히는 시간 = 기절 시간 × 이 비율(나머지 동안은 조금 뿌옇게 남았다가 사라짐).")]
        [SerializeField, Range(0.1f, 2f)] private float fadeScale = 1f;

        private float startTime = -100f, fadeTime = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(LightBlindFlash)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<LightBlindFlash>();
        }

        private void OnEnable() => LightExposure.Blinded += OnBlinded;
        private void OnDisable() => LightExposure.Blinded -= OnBlinded;

        private void OnBlinded(LightExposure who, float stunTime)
        {
            if (who == null || who.GetComponent<LocalPlayerInput>() == null) return; // 내 캐릭터만
            startTime = Time.time;
            fadeTime = Mathf.Max(0.2f, stunTime * fadeScale);
        }

        private void OnGUI()
        {
            float t = Time.time - startTime;
            if (t < 0f || t > holdTime + fadeTime) return;
            float a = t <= holdTime ? peakAlpha : peakAlpha * (1f - Mathf.SmoothStep(0f, 1f, (t - holdTime) / fadeTime));
            if (a <= 0.001f) return;
            GUI.depth = -1000; // 다른 HUD 위
            var c = flashColor; c.a = a;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

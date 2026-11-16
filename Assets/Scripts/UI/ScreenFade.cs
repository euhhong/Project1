using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 화면 넘어가기 페이드(UI): 검게 어두워진 뒤 할 일(씬 열기)을 하고, 도착한 씬이 열리면 다시 밝아진다.
    /// 씬이 바뀌어도 남는 맨 위 검은 판(UI Toolkit, 정렬 1000) — 다 밝아지면 스스로 없어진다. 붙일 것 없음.
    /// 예: ScreenFade.Out(() => SceneManager.LoadScene("Title"), "Title") — 중간에 다른 씬을 거쳐 가도 "Title"이 열릴 때 밝아진다.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        public const float DefaultTime = 0.3f;
        private const int SortingOrder = 1000;
        private const string PanelName = "ScreenFadeUI";

        private static ScreenFade active;

        private RuntimeUiPanel panel;
        private VisualElement cover;
        private Action then;
        private string arriveScene;
        private float time, alpha, darkAt;
        private const float MaxWait = 15f; // 도착할 씬이 이만큼 안 열리면 그냥 밝아진다(검은 화면에 갇히지 않게)
        private bool darkening = true, arrived;

        /// <summary>지금 어두워지거나 밝아지는 중인지.</summary>
        public static bool Busy => active != null;

        /// <summary>time초 동안 어두워진 뒤 then을 부르고, arriveScene(null = 다음에 열리는 아무 씬)이 열리면 time초 동안 밝아진다.</summary>
        public static void Out(Action then, string arriveScene = null, float time = DefaultTime)
        {
            if (active != null) return;
            var go = new GameObject("ScreenFade");
            DontDestroyOnLoad(go);
            active = go.AddComponent<ScreenFade>();
            active.then = then;
            active.arriveScene = arriveScene;
            active.time = Mathf.Max(0.01f, time);
        }

        private void Awake()
        {
            panel = RuntimeUiPanel.Create(PanelName, SortingOrder);
            DontDestroyOnLoad(panel.gameObject);
            cover = new VisualElement { pickingMode = PickingMode.Ignore };
            cover.style.position = Position.Absolute;
            cover.style.left = 0; cover.style.top = 0; cover.style.right = 0; cover.style.bottom = 0;
            cover.style.backgroundColor = Color.black;
            cover.style.opacity = 0f;
            panel.Root.pickingMode = PickingMode.Ignore;
            panel.Root.Add(cover);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (panel != null) Destroy(panel.gameObject);
            if (active == this) active = null;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (!darkening && (arriveScene == null || s.name == arriveScene)) arrived = true;
        }

        private void Update()
        {
            if (cover == null) { var left = GameObject.Find(PanelName); if (left != null) Destroy(left); Destroy(gameObject); return; } // Play 중 다시 컴파일되면 판이 비어 있다 — 그냥 없앤다
            float step = Time.unscaledDeltaTime / time;
            if (darkening)
            {
                alpha = Mathf.MoveTowards(alpha, 1f, step);
                cover.style.opacity = alpha;
                if (alpha >= 1f)
                {
                    darkening = false;
                    darkAt = Time.unscaledTime;
                    var a = then; then = null;
                    a?.Invoke();
                }
                return;
            }
            if (!arrived && Time.unscaledTime - darkAt < MaxWait) return; // 도착할 씬이 열릴 때까지 검은 채로
            alpha = Mathf.MoveTowards(alpha, 0f, step);
            cover.style.opacity = alpha;
            if (alpha <= 0f) Destroy(gameObject);
        }
    }
}

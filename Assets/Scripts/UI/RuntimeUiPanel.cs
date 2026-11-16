using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 실행 중에 UI Toolkit 화면 하나를 만든다(PanelSettings·UIDocument를 코드로 — 씬·인스펙터 연결 없음).
    /// 기준 해상도 1920×1080, 화면 높이에 맞춰 늘고 준다. 씬에 EventSystem이 없으면 잠깐 만들고, 지워질 때 같이 지운다.
    /// </summary>
    public class RuntimeUiPanel : MonoBehaviour
    {
        public const string ThemeResource = "UI/RuntimeTheme";
        public static readonly Vector2Int ReferenceResolution = new(1920, 1080);

        private PanelSettings settings;
        private GameObject ownEventSystem;

        public UIDocument Document { get; private set; }
        public VisualElement Root => Document.rootVisualElement;

        public static RuntimeUiPanel Create(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var panel = go.AddComponent<RuntimeUiPanel>();

            panel.settings = ScriptableObject.CreateInstance<PanelSettings>();
            panel.settings.name = name;
            panel.settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            panel.settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.settings.referenceResolution = ReferenceResolution;
            panel.settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.settings.match = 1f;
            panel.settings.sortingOrder = sortingOrder;

            panel.Document = go.AddComponent<UIDocument>();
            panel.Document.panelSettings = panel.settings;

            if (FindAnyObjectByType<EventSystem>() == null)
                panel.ownEventSystem = new GameObject(name + " EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            go.SetActive(true);
            var root = panel.Root;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.top = 0; root.style.right = 0; root.style.bottom = 0;
            return panel;
        }

        private void OnDestroy()
        {
            if (ownEventSystem != null) Destroy(ownEventSystem);
            if (settings != null) Destroy(settings);
        }
    }
}

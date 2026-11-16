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

        /// <summary>씬에 이미 있는 UIDocument(예: 캐릭터 선택 씬의 CharacterSelectUI — PanelSettings 에셋 연결)를 그대로 쓴다. EventSystem이 없으면 잠깐 만든다.</summary>
        public static RuntimeUiPanel Attach(UIDocument document)
        {
            var panel = document.TryGetComponent(out RuntimeUiPanel p) ? p : document.gameObject.AddComponent<RuntimeUiPanel>();
            panel.Document = document;
            if (panel.ownEventSystem == null && FindAnyObjectByType<EventSystem>() == null)
                panel.ownEventSystem = new GameObject(document.name + " EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
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

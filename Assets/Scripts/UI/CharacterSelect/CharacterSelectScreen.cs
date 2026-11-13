using System.Collections.Generic;
using Project1.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면. 게임 씬을 시작하면 먼저 뜬다(CharacterSelectBootstrap).
    /// - W/↑ = 감시자, S/↓ = 생존자, A·D/←·→ = 캐릭터 바꾸기, Space/Enter = 시작
    /// - 선택 중에는 플레이어 캐릭터를 꺼 두고, 멀리 떨어진 곳에 임시 무대(바닥·카메라·대기 모델)를 만든다.
    /// - 시작하면 무대를 지우고, 플레이어 캐릭터 루트의 모델을 고른 캐릭터로 바꾼 뒤 다시 켠다.
    /// 씬에 모델을 늘어놓지 않는다. 무대는 실행 중에만 만든다.
    /// </summary>
    public class CharacterSelectScreen : MonoBehaviour
    {
        private static readonly Vector3 StageOrigin = new(10000f, 0f, 10000f);
        private const float Spacing = 3.5f;
        private const float CameraDistance = 7.5f, CameraHeight = 2.2f, LookHeight = 1.5f;
        private static readonly string[] HiddenInSelect = { "Gamekeeper_Grab_Weapon" };

        private CharacterRoster roster;
        private GameObject player;
        private Camera mainCamera;
        private GameObject stage;
        private Camera stageCamera;
        private readonly List<GameObject> lineup = new();
        private List<CharacterRoster.Entry> shown;
        private CharacterRole role = CharacterRole.Survivor;
        private readonly Dictionary<CharacterRole, int> index = new() { [CharacterRole.Survivor] = 0, [CharacterRole.Hunter] = 0 };
        private Vector3 cameraVelocity;
        private GUIStyle titleStyle, nameStyle, tabStyle, hintStyle;

        public void Init(CharacterRoster roster, GameObject player)
        {
            this.roster = roster;
            this.player = player;
            // 지금 플레이어 모델과 같은 캐릭터를 처음에 고른 상태로 둔다.
            var current = player.GetComponentInChildren<Animator>();
            if (current != null && current.avatar != null)
                foreach (var r in new[] { CharacterRole.Survivor, CharacterRole.Hunter })
                {
                    int i = roster.Of(r).FindIndex(e => current.avatar.name.StartsWith(e.id));
                    if (i >= 0) { role = r; index[r] = i; }
                }

            player.SetActive(false);
            mainCamera = Camera.main;
            if (mainCamera != null) mainCamera.enabled = false;
            BuildStage();
            ShowRole(role, snapCamera: true);
        }

        private void BuildStage()
        {
            stage = new GameObject("CharacterSelectStage");
            stage.transform.position = StageOrigin;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(stage.transform, false);
            floor.transform.localScale = new Vector3(8f, 1f, 2f);
            floor.transform.localPosition = new Vector3(-Spacing * 4f, 0f, 0f);
            Destroy(floor.GetComponent<Collider>());

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.6f;

            var camGo = new GameObject("SelectCamera");
            camGo.transform.SetParent(stage.transform, false);
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.fieldOfView = 35f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        }

        private void ShowRole(CharacterRole newRole, bool snapCamera)
        {
            role = newRole;
            foreach (var m in lineup) Destroy(m);
            lineup.Clear();
            shown = roster.Of(role);
            for (int i = 0; i < shown.Count; i++)
            {
                var e = shown[i];
                var m = Instantiate(e.model, stage.transform);
                m.name = e.id;
                m.transform.localPosition = new Vector3(-i * Spacing, 0f, 0f); // 카메라가 앞에서 보므로 -X 쪽이 화면 오른쪽
                m.transform.localRotation = Quaternion.identity; // 모델은 +Z(카메라 쪽)를 본다
                m.transform.localScale = Vector3.one * e.scale;
                // 판정 상자는 선택 화면에서 필요 없다.
                foreach (var c in m.GetComponentsInChildren<Collider>()) c.enabled = false;
                // 선택 화면에서는 들지 않는 소품(예: 사냥터지기 그랩)을 감춘다. 게임에서는 그대로 든다.
                foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    if (System.Array.IndexOf(HiddenInSelect, r.name) >= 0) r.enabled = false;
                var anim = m.GetComponent<Animator>();
                if (anim != null) { anim.runtimeAnimatorController = e.selectController; anim.applyRootMotion = false; }
                lineup.Add(m);
            }
            index[role] = Mathf.Clamp(index[role], 0, Mathf.Max(shown.Count - 1, 0));
            if (snapCamera) stageCamera.transform.position = CameraTarget();
            cameraVelocity = Vector3.zero;
        }

        private Vector3 CameraTarget() =>
            StageOrigin + new Vector3(-index[role] * Spacing, CameraHeight, CameraDistance);

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || shown == null) return;

            if ((kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) && role != CharacterRole.Hunter)
                ShowRole(CharacterRole.Hunter, snapCamera: true);
            else if ((kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) && role != CharacterRole.Survivor)
                ShowRole(CharacterRole.Survivor, snapCamera: true);

            if (shown.Count > 0)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                    index[role] = (index[role] - 1 + shown.Count) % shown.Count;
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                    index[role] = (index[role] + 1) % shown.Count;
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    StartGame(shown[index[role]]);
                    return;
                }
            }

            var cam = stageCamera.transform;
            cam.position = Vector3.SmoothDamp(cam.position, CameraTarget(), ref cameraVelocity, 0.12f);
            cam.LookAt(new Vector3(cam.position.x, StageOrigin.y + LookHeight, StageOrigin.z));
        }

        /// <summary>고른 캐릭터로 게임을 시작한다(키 입력 또는 나중에 UI 버튼에서 부른다).</summary>
        public void StartGame(CharacterRoster.Entry entry)
        {
            Destroy(stage);
            if (mainCamera != null) mainCamera.enabled = true;
            player.SetActive(true);
            CharacterLoadout.Apply(player, entry);
            Debug.Log($"[UI] {entry.displayName}({(entry.role == CharacterRole.Hunter ? "감시자" : "생존자")})로 게임을 시작합니다.");
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            if (shown == null) return;
            float u = Screen.height / 1080f;
            titleStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
            nameStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tabStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            hintStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter };
            titleStyle.fontSize = Mathf.RoundToInt(44 * u);
            nameStyle.fontSize = Mathf.RoundToInt(56 * u);
            tabStyle.fontSize = Mathf.RoundToInt(30 * u);
            hintStyle.fontSize = Mathf.RoundToInt(26 * u);

            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, 30 * u, w, 60 * u), "캐릭터 선택", titleStyle);

            var on = new Color(1f, 0.85f, 0.3f);
            var off = new Color(1f, 1f, 1f, 0.45f);
            tabStyle.normal.textColor = role == CharacterRole.Hunter ? on : off;
            GUI.Label(new Rect(w * 0.5f - 320 * u, 100 * u, 300 * u, 50 * u), "▲ W  감시자", tabStyle);
            tabStyle.normal.textColor = role == CharacterRole.Survivor ? on : off;
            GUI.Label(new Rect(w * 0.5f + 20 * u, 100 * u, 300 * u, 50 * u), "▼ S  생존자", tabStyle);

            if (shown.Count == 0)
            {
                GUI.Label(new Rect(0, h * 0.75f, w, 80 * u), "고를 캐릭터가 없습니다", nameStyle);
                return;
            }
            var e = shown[index[role]];
            GUI.Label(new Rect(0, h * 0.78f, w, 80 * u), $"◀  {e.displayName}  ▶", nameStyle);
            GUI.Label(new Rect(0, h * 0.78f + 70 * u, w, 40 * u), $"{index[role] + 1} / {shown.Count}", tabStyle);
            GUI.Label(new Rect(0, h - 70 * u, w, 50 * u), "A·D  캐릭터 바꾸기      W·S  감시자/생존자      Space·Enter  시작", hintStyle);
        }

        private void OnDestroy()
        {
            if (stage != null) Destroy(stage);
        }
    }
}

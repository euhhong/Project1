using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면(캐릭터 선택 씬 Assets/Scenes/CharacterSelect.unity의 CharacterSelect 오브젝트). 씬을 열면 스스로 시작한다.
    /// - W/↑ = 감시자, S/↓ = 생존자, A·D/←·→ = 캐릭터 넘기기, 1·Space·Enter = 연습, 2·G = 게임 시작(AI 봇). 화면 버튼·화살표로도 된다.
    /// - 화면은 CharacterSelectView(씬의 CharacterSelectUI), 3D 모델은 CharacterSelectStage(씬의 SelectStage 카메라·조명)가 맡는다.
    /// - 고르면 CharacterPick에 적어 두고 게임 씬(SampleScene, 미리 불러 둠)으로 넘어간다 — 게임 씬에서 CharacterSelectBootstrap이 캐릭터를 입힌다.
    /// 씬에 모델을 늘어놓지 않는다. 모델은 실행 중에만 늘어놓는다.
    /// </summary>
    public class CharacterSelectScreen : MonoBehaviour
    {
        public const string SceneName = "CharacterSelect";
        public const string GameSceneName = "SampleScene";
        private const float DragDegreesPerPixel = 0.3f; // 모델은 끌어서 조금만 돌려 볼 수 있다

        private CharacterRoster roster;
        private CharacterSelectStage stage;
        private CharacterSelectView view;
        private List<CharacterRoster.Entry> shown;
        private CharacterRole role = CharacterRole.Survivor;
        private readonly Dictionary<CharacterRole, int> index = new() { [CharacterRole.Survivor] = 0, [CharacterRole.Hunter] = 0 };
        private bool started;

        private void Start()
        {
            roster = CharacterRoster.Load();
            if (roster == null || roster.entries.Count == 0) { Debug.LogWarning("[UI] CharacterRoster가 없어 캐릭터 선택 화면을 띄우지 못했습니다."); return; }

            // 지난번에 고른 캐릭터부터 보여 준다.
            string lastId = CharacterPick.LastId;
            CharacterPick.Clear();
            if (!string.IsNullOrEmpty(lastId))
                foreach (var r in new[] { CharacterRole.Survivor, CharacterRole.Hunter })
                {
                    int i = roster.Of(r).FindIndex(e => e.id == lastId);
                    if (i >= 0) { role = r; index[r] = i; }
                }

            float s = Mathf.Max(0.75f, Screen.height / (float)RuntimeUiPanel.ReferenceResolution.y);
            var size = CharacterSelectView.ModelCanvasSize * s; // 모델 칸보다 크게 그려 튀어나온 부분(꼬리·촉수)도 보이게
            var stageRoot = GameObject.Find(CharacterSelectStage.RootName);
            stage = new CharacterSelectStage(new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)),
                CharacterSelectView.ModelCanvasSize.y / CharacterSelectView.ModelPanelSize.y, stageRoot != null ? stageRoot.transform : null);
            view = new CharacterSelectView(stage.Texture, FindAnyObjectByType<UIDocument>());
            view.StepRequested += Step;
            view.RoleRequested += r => { if (r != role) ShowRole(r); };
            view.StartRequested += battle => StartCurrent(battle);
            view.ModelDragged += dx => stage.Rotate(-dx * DragDegreesPerPixel);
            ShowRole(role, sound: false);
        }

        private void ShowRole(CharacterRole newRole, bool sound = true)
        {
            role = newRole;
            if (sound) Project1.Presentation.SfxPlayer.PlayUi("ui_flip", 0.8f, 0.05f, 0.85f); // 진영 바꾸기: 조금 낮은 넘기기 소리
            shown = roster.Of(role);
            index[role] = Mathf.Clamp(index[role], 0, Mathf.Max(shown.Count - 1, 0));
            stage.Show(shown, index[role]);
            Refresh(0);
            // 보고 있는 진영의 선택 음악(생존자 / 감시자)
            Project1.Presentation.BgmPlayer.Play(role == CharacterRole.Survivor ? "SurvivorSelect" : "HunterSelect");
        }

        private void Step(int dir)
        {
            if (shown == null || shown.Count == 0) return;
            index[role] = (index[role] + dir + shown.Count) % shown.Count;
            stage.Focus(index[role]);
            Project1.Presentation.SfxPlayer.PlayUi("ui_flip", 0.75f, 0.08f); // 다른 캐릭터로 넘기기: 카드 휙
            Refresh(dir);
        }

        private void Refresh(int dir)
        {
            if (shown.Count == 0) view.ShowEmpty(role);
            else view.Show(shown[index[role]], role, index[role], shown.Count, dir);
        }

        private void StartCurrent(bool battle)
        {
            if (!started && shown != null && shown.Count > 0) StartGame(shown[index[role]], battle);
        }

        private void Update()
        {
            if (started || shown == null || stage == null || view == null) return; // Play 중 다시 컴파일되면 무대·화면이 비어 있다
            var kb = Keyboard.current;
            if (kb != null)
            {
                if ((kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) && role != CharacterRole.Hunter)
                    ShowRole(CharacterRole.Hunter);
                else if ((kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) && role != CharacterRole.Survivor)
                    ShowRole(CharacterRole.Survivor);

                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Step(-1);
                if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Step(+1);
                // 1·Space·Enter = 연습 모드(훈련 상대), 2·G = 게임 시작(AI 봇과 전투).
                if (kb.digit1Key.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    StartCurrent(battle: false);
                    return;
                }
                if (kb.digit2Key.wasPressedThisFrame || kb.gKey.wasPressedThisFrame)
                {
                    StartCurrent(battle: true);
                    return;
                }
                if (kb.escapeKey.wasPressedThisFrame && BackToTitle()) return; // ESC = 뒤로(타이틀)
            }

            stage.Tick();
            view.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>고른 캐릭터로 게임 씬을 연다(검게 어두워진 뒤 열고, 열리면 밝아짐). battle = false: 연습 모드(훈련 상대·쿨타임 없음), true: 게임 시작(AI 봇과 전투, BattleMatch).</summary>
        public void StartGame(CharacterRoster.Entry entry, bool battle = false)
        {
            started = true;
            Project1.Presentation.BgmPlayer.Stop(); // 게임에 들어가면 선택 음악을 끈다
            CharacterPick.Set(entry, battle);
            // 게임 씬을 미리 불러 두지 않는다: 미리 부르는 중엔 다른 씬을 열 수 없어 ESC(타이틀로)가 게임 씬을 다 부를 때까지 멈췄다
            ScreenFade.Out(() => SceneManager.LoadSceneAsync(GameSceneName), GameSceneName);
        }

        /// <summary>타이틀 씬으로 돌아간다(검게 어두워졌다가 타이틀이 열리면 밝아짐). 빌드에 타이틀이 없으면 아무것도 안 함.</summary>
        private bool BackToTitle()
        {
            if (!Application.CanStreamedLevelBeLoaded(TitleScreen.SceneName)) return false;
            started = true;
            CharacterPick.Clear();
            ScreenFade.Out(() => SceneManager.LoadSceneAsync(TitleScreen.SceneName), TitleScreen.SceneName);
            return true;
        }

        private void OnDestroy()
        {
            view?.Dispose();
            view = null;
            stage?.Dispose();
            stage = null;
        }
    }
}

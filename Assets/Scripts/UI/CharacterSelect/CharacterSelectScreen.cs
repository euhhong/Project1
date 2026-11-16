using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면. 게임 씬을 시작하면 먼저 뜬다(CharacterSelectBootstrap).
    /// - W/↑ = 감시자, S/↓ = 생존자, A·D/←·→ = 캐릭터 넘기기, 1·Space·Enter = 연습, 2·G = 게임 시작(AI 봇). 화면 버튼·화살표로도 된다.
    /// - 선택 중에는 플레이어 캐릭터를 꺼 둔다. 화면은 CharacterSelectView(UI), 3D 모델은 CharacterSelectStage(멀리 떨어진 임시 무대)가 맡는다.
    /// - 시작하면 화면과 무대를 지우고, 플레이어 캐릭터 루트의 모델을 고른 캐릭터로 바꾼 뒤 다시 켠다.
    /// 씬에 모델을 늘어놓지 않는다. 무대는 실행 중에만 만든다.
    /// </summary>
    public class CharacterSelectScreen : MonoBehaviour
    {
        private const float DragDegreesPerPixel = 0.45f;

        private CharacterRoster roster;
        private GameObject player;
        private Camera mainCamera;
        private CharacterSelectStage stage;
        private CharacterSelectView view;
        private List<CharacterRoster.Entry> shown;
        private CharacterRole role = CharacterRole.Survivor;
        private readonly Dictionary<CharacterRole, int> index = new() { [CharacterRole.Survivor] = 0, [CharacterRole.Hunter] = 0 };
        private bool started;

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

            float s = Mathf.Max(0.75f, Screen.height / (float)RuntimeUiPanel.ReferenceResolution.y);
            var size = CharacterSelectView.ModelPanelSize * s;
            stage = new CharacterSelectStage(new Vector2Int(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y)));
            view = new CharacterSelectView(stage.Texture);
            view.StepRequested += Step;
            view.RoleRequested += r => { if (r != role) ShowRole(r); };
            view.StartRequested += battle => StartCurrent(battle);
            view.ModelDragged += dx => stage.Rotate(-dx * DragDegreesPerPixel);
            ShowRole(role);
        }

        private void ShowRole(CharacterRole newRole)
        {
            role = newRole;
            shown = roster.Of(role);
            index[role] = Mathf.Clamp(index[role], 0, Mathf.Max(shown.Count - 1, 0));
            stage.Show(shown, index[role]);
            Refresh(0);
        }

        private void Step(int dir)
        {
            if (shown == null || shown.Count == 0) return;
            index[role] = (index[role] + dir + shown.Count) % shown.Count;
            stage.Focus(index[role]);
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
            }

            stage.Tick();
            view.Tick(Time.unscaledDeltaTime);
        }

        /// <summary>고른 캐릭터로 시작한다. battle = false: 연습 모드(지금 테스트 화면 — 훈련 상대), true: 게임 시작(AI 봇과 전투, BattleMatch).</summary>
        public void StartGame(CharacterRoster.Entry entry, bool battle = false)
        {
            started = true;
            Teardown();
            if (mainCamera != null) mainCamera.enabled = true;
            player.SetActive(true);
            CharacterLoadout.Apply(player, entry);
            Project1.Systems.SkillCooldowns.Disabled = !battle; // 연습 모드 = 쿨타임 없이 스킬, 게임 시작 = 쿨타임 그대로
            if (battle) BattleMatch.Begin(roster, player, entry);
            Debug.Log($"[UI] {entry.displayName}({(entry.role == CharacterRole.Hunter ? "감시자" : "생존자")})로 {(battle ? "게임(AI 봇과 전투)" : "연습 모드")}를 시작합니다.");
            Destroy(gameObject);
        }

        private void Teardown()
        {
            view?.Dispose();
            view = null;
            stage?.Dispose();
            stage = null;
        }

        private void OnDestroy() => Teardown();
    }
}

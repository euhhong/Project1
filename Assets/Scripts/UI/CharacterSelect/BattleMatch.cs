using Project1.Presentation;
using Project1.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.UI
{
    /// <summary>
    /// 게임 시작 모드(임시, UI는 대충): 훈련 상대를 치우고 AI 봇 하나와 싸운다.
    /// - 봇 = 플레이어 캐릭터 루트를 복제(입력만 AiBotInput으로)해 상대 진영 캐릭터 중 무작위 하나를 입혀 플레이어 앞에 세운다.
    /// - 맞으면 체력이 준다(BattleDamage, 임시 규칙). 어느 쪽이든 체력이 0이 되면 승리/패배를 보여 주고 R로 다시(씬을 다시 불러 캐릭터 선택부터).
    /// - 화면 위에 봇 이름·체력 막대(플레이어 체력은 원래 HUD).
    /// </summary>
    public class BattleMatch : MonoBehaviour
    {
        private const float SpawnDistance = 8f;
        private const float BaseDamage = 10f;

        private GameObject player, bot;
        private CharacterHealth playerHealth, botHealth;
        private string botName;
        private string result;
        private GUIStyle big, small;

        public static BattleMatch Begin(CharacterRoster roster, GameObject player, CharacterRoster.Entry playerEntry)
        {
            var m = new GameObject("BattleMatch").AddComponent<BattleMatch>();
            m.Setup(roster, player, playerEntry);
            return m;
        }

        private void Setup(CharacterRoster roster, GameObject p, CharacterRoster.Entry playerEntry)
        {
            player = p;
            var dummy = GameObject.Find(TrainingDummySpawner.DummyName);
            if (dummy != null) Destroy(dummy);

            // 상대: 반대 진영에서 무작위(없으면 아무나)
            var other = roster.Of(playerEntry.role == CharacterRole.Hunter ? CharacterRole.Survivor : CharacterRole.Hunter);
            if (other.Count == 0) other = roster.entries;
            var entry = other[Random.Range(0, other.Count)];
            botName = entry.displayName;

            // 플레이어 루트를 복제(꺼진 채 복제해 입력 컴포넌트를 바꾼 뒤 켠다)
            bool wasActive = player.activeSelf;
            player.SetActive(false);
            bot = Instantiate(player);
            player.SetActive(wasActive);
            bot.name = "AIBot_" + entry.id;
            foreach (var c in bot.GetComponents<LocalPlayerInput>()) DestroyImmediate(c);
            foreach (var c in bot.GetComponents<HealthDebugKeys>()) DestroyImmediate(c);
            var ai = bot.AddComponent<AiBotInput>();
            ai.SetTarget(player.transform);
            Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            Vector3 pos = player.transform.position + fwd * SpawnDistance;
            if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore)) pos = hit.point + Vector3.up * 0.05f;
            bot.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-fwd, Vector3.up));
            bot.SetActive(true);
            CharacterLoadout.Apply(bot, entry, isPlayer: false);

            playerHealth = player.GetComponent<CharacterHealth>();
            botHealth = bot.GetComponent<CharacterHealth>();
            BattleDamage.Enable(BaseDamage);
        }

        private void Update()
        {
            if (result == null)
            {
                if (botHealth != null && botHealth.Current <= 0f) End("승리!");
                else if (playerHealth != null && playerHealth.Current <= 0f) End("패배...");
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (result != null && kb != null && kb.rKey.wasPressedThisFrame)
            {
                BattleDamage.Disable();
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
            }
        }

        private void End(string text)
        {
            result = text;
            if (bot != null && bot.TryGetComponent(out AiBotInput ai)) ai.enabled = false;
            if (bot != null && bot.TryGetComponent(out AiBotInput a2)) a2.SetTarget(null);
        }

        private void OnDestroy() => BattleDamage.Disable();

        private void OnGUI()
        {
            float u = Screen.height / 1080f;
            big ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            big.fontSize = Mathf.RoundToInt(90 * u); small.fontSize = Mathf.RoundToInt(28 * u);
            float w = Screen.width;
            // 봇 체력(화면 위 가운데)
            if (botHealth != null)
            {
                float bw = 500f * u, bh = 22f * u, x = (w - bw) * 0.5f, y = 40f * u;
                GUI.Label(new Rect(0, y - 34f * u, w, 32f * u), $"상대: {botName}", small);
                GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x, y, bw, bh), Texture2D.whiteTexture);
                GUI.color = new Color(0.9f, 0.2f, 0.2f); GUI.DrawTexture(new Rect(x, y, bw * Mathf.Clamp01(botHealth.Current / botHealth.Max), bh), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (result != null)
            {
                GUI.Label(new Rect(0, Screen.height * 0.35f, w, 120f * u), result, big);
                GUI.Label(new Rect(0, Screen.height * 0.35f + 120f * u, w, 40f * u), "R  다시 하기(캐릭터 선택으로)", small);
            }
        }
    }
}

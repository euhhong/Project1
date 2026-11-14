using Project1.Core;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 게임 중 HUD(임시): 왼쪽 아래 체력 막대, 가운데 아래 고유 스킬 칸([E] 이름 / 시전 막대 / 재사용 대기).
    /// 캐릭터를 고르면 CharacterLoadout이 만든다. 나중에 UI 디자인이 정해지면 이 화면만 바꾼다.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private ICharacterHealth health;
        private ICharacterSkillState skill;
        private ICharacterStun stun;
        private ICharacterStamina stamina;
        private string characterName;
        private GUIStyle label, small;
        private float blockedMessageUntil;
        private string blockedMessage;
        private bool skillKeyWasDown;

        public void SetTarget(GameObject player, string displayName)
        {
            health = player.GetComponent<ICharacterHealth>();
            skill = player.GetComponent<ICharacterSkillState>();
            stun = player.GetComponent<ICharacterStun>();
            stamina = player.GetComponent<ICharacterStamina>();
            characterName = displayName;
        }

        private void Update()
        {
            // 못 쓰는 상태에서 E를 누르면 이유를 잠깐 보여 준다.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool down = kb != null && kb.eKey.isPressed;
            if (down && !skillKeyWasDown && skill != null && skill.State == SkillState.Ready && !skill.CanUse && !string.IsNullOrEmpty(skill.BlockedReason))
            {
                blockedMessage = skill.BlockedReason;
                blockedMessageUntil = Time.time + 1.2f;
            }
            skillKeyWasDown = down;
        }

        private void OnGUI()
        {
            float u = Screen.height / 1080f;
            label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            label.fontSize = Mathf.RoundToInt(26 * u);
            small.fontSize = Mathf.RoundToInt(22 * u);

            // 체력
            if (health != null)
            {
                var r = new Rect(40 * u, Screen.height - 90 * u, 420 * u, 30 * u);
                GUI.Label(new Rect(r.x, r.y - 40 * u, r.width, 36 * u), characterName, label);
                Bar(r, health.Current / Mathf.Max(health.Max, 1f), new Color(0.85f, 0.2f, 0.2f));
                small.normal.textColor = Color.white;
                GUI.Label(r, $"{Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Max)}", small);
            }

            // 스태미나(체력 막대 아래, 지치면 주황색)
            if (stamina != null)
            {
                var r = new Rect(40 * u, Screen.height - 52 * u, 420 * u, 12 * u);
                Bar(r, stamina.Current / Mathf.Max(stamina.Max, 1f),
                    stamina.IsExhausted ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.95f, 0.85f, 0.3f));
            }

            // 기절
            if (stun != null && stun.IsStunned)
            {
                small.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 40 * u), $"기절  {stun.StunRemaining:0.0}초", small);
            }

            // 고유 스킬
            if (skill != null && !string.IsNullOrEmpty(skill.SkillId))
            {
                float w = 300 * u, h = 70 * u;
                var box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 30 * u, w, h);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = Color.white;
                string status = skill.State switch
                {
                    SkillState.Casting => "사용 중",
                    SkillState.Recovering => "넘어짐",
                    SkillState.Cooldown => $"{skill.CooldownRemaining:0.0}초",
                    _ => skill.CanUse ? "준비" : (skill.BlockedReason ?? ""),
                };
                if (skill.StatusText != null) status = skill.StatusText; // 스킬이 정한 글(예: 용병 아대 = 재사용 대기 남은 초만)
                small.normal.textColor = skill.State == SkillState.Ready && skill.CanUse ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(new Rect(box.x, box.y + 4 * u, w, 34 * u), $"[E]  {skill.SkillName}", small);
                small.normal.textColor = Color.white;
                GUI.Label(new Rect(box.x, box.y + 34 * u, w, 30 * u), status, small);

                var bar = new Rect(box.x, box.yMax - 6 * u, w, 6 * u);
                if (skill.State == SkillState.Casting)
                {
                    float fill = skill.StatusBarFill; // 스킬이 정한 막대(예: 용병 아대 = 도약 재사용 대기, 준비되면 없음)
                    if (float.IsNaN(fill)) Bar(bar, skill.CastProgress, new Color(0.4f, 0.9f, 0.5f));
                    else if (fill >= 0f) Bar(bar, fill, new Color(0.5f, 0.6f, 0.9f));
                }
                else if (skill.State == SkillState.Cooldown) Bar(bar, 1f - skill.CooldownRemaining / Mathf.Max(skill.CooldownDuration, 0.01f), new Color(0.5f, 0.6f, 0.9f));

                if (Time.time < blockedMessageUntil)
                {
                    small.normal.textColor = new Color(1f, 0.5f, 0.4f);
                    GUI.Label(new Rect(box.x, box.y - 40 * u, w, 36 * u), blockedMessage, small);
                }
            }
        }

        private static void Bar(Rect r, float fill, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

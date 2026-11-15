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
        private ICharacterStamina stamina;
        private GameObject target;
        private readonly System.Collections.Generic.List<IChargeCancelHint> cancelHints = new();
        private string characterName;
        private GUIStyle label, small;
        private float blockedMessageUntil;
        private string blockedMessage;
        private bool skillKeyWasDown;

        public void SetTarget(GameObject player, string displayName)
        {
            health = player.GetComponent<ICharacterHealth>();
            skill = player.GetComponent<ICharacterSkillState>();
            stamina = player.GetComponent<ICharacterStamina>();
            target = player;
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

            // E 스킬 칸이 없는 캐릭터(예: 하스터 — 촉수가 E를 직접 받는다)도 꾹 눌러 고르는 중이면 같은 자리에 취소 방법을 띄운다.
            bool hasSkillBox = skill != null && !string.IsNullOrEmpty(skill.SkillId);
            if (!hasSkillBox)
            {
                string hint = CancelHint();
                if (hint != null)
                {
                    float w = 300 * u, h = 40 * u;
                    var box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 30 * u, w, h);
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(box, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    small.normal.textColor = new Color(1f, 0.6f, 0.5f);
                    GUI.Label(box, hint, small);
                }
            }

            // 고유 스킬
            if (hasSkillBox)
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
                string cancelHint = CancelHint(); // 꾹 눌러 자리를 고르는 중이면 이 줄에 취소 방법(따로 창을 띄우지 않는다)
                if (cancelHint != null) status = cancelHint;
                small.normal.textColor = skill.State == SkillState.Ready && skill.CanUse ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.7f);
                GUI.Label(new Rect(box.x, box.y + 4 * u, w, 34 * u), $"[E]  {skill.SkillName}", small);
                small.normal.textColor = cancelHint != null ? new Color(1f, 0.6f, 0.5f) : Color.white;
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

        /// <summary>캐릭터의 차징(자리 고르기) 기능 중 지금 고르는 중인 것의 취소 안내. 캐릭터를 바꾸면 붙는 기능이 바뀌어 매번 찾는다.</summary>
        private string CancelHint()
        {
            if (target == null) return null;
            target.GetComponents(cancelHints);
            foreach (var h in cancelHints)
            {
                if (h is Behaviour { isActiveAndEnabled: false }) continue;
                string s = h.ChargeCancelHint;
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return null;
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

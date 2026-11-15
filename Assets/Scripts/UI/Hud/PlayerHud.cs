using Project1.Core;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 게임 중 HUD(임시): 왼쪽 아래 체력 막대, 가운데 아래 고유 스킬 칸([E] 이름 / 시전 막대 / 재사용 대기), 오른쪽 아래 재사용 대기 게이지(ICooldownGauge — 기술마다 돌면서 채워지는 원).
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
            shownGauges.Clear(); readyFlashes.Clear(); // 캐릭터가 바뀜: 재사용 대기 게이지 새로
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

            DrawCooldowns(u); // 오른쪽 아래 재사용 대기 게이지

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
                    SkillState.Cooldown => "재사용 대기", // 남은 시간은 오른쪽 아래 게이지로
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

        // ───────── 오른쪽 아래: 재사용 대기 게이지(ICooldownGauge) ─────────
        private readonly System.Collections.Generic.List<ICooldownGauge> gauges = new();
        private readonly System.Collections.Generic.Dictionary<ICooldownGauge, string> shownGauges = new();
        private readonly System.Collections.Generic.List<(string label, float until)> readyFlashes = new();
        private readonly System.Collections.Generic.List<ICooldownGauge> gaugeScratch = new();
        private const float ReadyFlashTime = 0.5f;

        /// <summary>
        /// 재사용 대기 중인 기술마다 원 하나(오른쪽 아래 구석부터 왼쪽으로): 원이 12시 방향에서 시계 방향으로 돌면서 채워지고(파랑), 다 차면 쓸 수 있다.
        /// 줄어드는 게이지(주황, 예: 그랩 다시 쏘기)는 채워진 부분이 돌면서 줄어든다. 원 아래에 이름.
        /// 다 차서 쓸 수 있게 되면 잠깐(ReadyFlashTime) 금색으로 꽉 찬 원을 보여 주고 사라진다. 남은 초는 글로 쓰지 않는다.
        /// </summary>
        private void DrawCooldowns(float u)
        {
            if (target == null) return;
            target.GetComponents(gauges);
            float r = 34 * u, gap = 22 * u, labelH = 26 * u;
            float cx = Screen.width - 40 * u - r, cy = Screen.height - 40 * u - labelH - r;

            gaugeScratch.Clear();
            foreach (var g in gauges)
            {
                if (g is Behaviour { isActiveAndEnabled: false }) continue;
                if (!g.TryGetCooldown(out string name, out float fill, out bool draining)) continue;
                gaugeScratch.Add(g);
                shownGauges[g] = draining ? null : name; // 줄어드는 게이지는 끝나도 '준비' 표시 안 함
                DrawGaugeCircle(new Vector2(cx, cy), r, labelH, name, Mathf.Clamp01(fill), draining ? new Color(1f, 0.6f, 0.2f) : new Color(0.35f, 0.65f, 1f));
                cx -= r * 2f + gap;
            }
            // 방금 다 찬 게이지 → 잠깐 금색으로
            var ended = new System.Collections.Generic.List<ICooldownGauge>();
            foreach (var kv in shownGauges) if (!gaugeScratch.Contains(kv.Key)) ended.Add(kv.Key);
            foreach (var g in ended) { if (shownGauges[g] != null) readyFlashes.Add((shownGauges[g], Time.time + ReadyFlashTime)); shownGauges.Remove(g); }
            for (int i = readyFlashes.Count - 1; i >= 0; i--)
            {
                if (Time.time > readyFlashes[i].until) { readyFlashes.RemoveAt(i); continue; }
                DrawGaugeCircle(new Vector2(cx, cy), r, labelH, readyFlashes[i].label, 1f, new Color(1f, 0.85f, 0.3f));
                cx -= r * 2f + gap;
            }
        }

        private const int PieSegments = 90; // 원 한 바퀴를 나누는 조각 수(4도씩)

        /// <summary>원 게이지 하나: 테두리 → 어두운 바탕 원 → 12시에서 시계 방향으로 fill만큼 채운 부채꼴 → 아래 이름.</summary>
        private void DrawGaugeCircle(Vector2 c, float r, float labelH, string name, float fill, Color color)
        {
            DrawPie(c, r + 2f, 1f, new Color(0f, 0f, 0f, 0.7f));      // 테두리
            DrawPie(c, r, 1f, new Color(0.12f, 0.12f, 0.14f, 0.85f)); // 바탕
            if (fill > 0f) DrawPie(c, r - 1f, fill, color);           // 채워진 부분
            small.normal.textColor = Color.white;
            GUI.Label(new Rect(c.x - r * 1.6f, c.y + r + 2f, r * 3.2f, labelH), name, small);
        }

        /// <summary>가운데 c, 반지름 r 원을 12시 방향에서 시계 방향으로 fill(0~1)만큼 칠한다(가운데에서 바깥으로 뻗는 얇은 조각을 돌려 그림).</summary>
        private static void DrawPie(Vector2 c, float r, float fill, Color color)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(PieSegments * Mathf.Clamp01(fill) - 0.001f), 0, PieSegments);
            if (n == 0) return;
            float step = 360f / PieSegments, thick = r * (2f * Mathf.PI / PieSegments) * 1.6f; // 바깥 끝에서 이웃 조각과 겹치게
            Matrix4x4 saved = GUI.matrix;
            GUI.color = color;
            for (int i = 0; i < n; i++)
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(-90f + (i + 0.5f) * step, c); // 12시(-90도)부터 시계 방향
                GUI.DrawTexture(new Rect(c.x, c.y - thick * 0.5f, r, thick), Texture2D.whiteTexture);
            }
            GUI.matrix = saved;
            GUI.color = Color.white;
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

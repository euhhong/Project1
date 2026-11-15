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

            DrawCooldowns(u); // 오른쪽 아래 재사용 대기 게이지
            DrawSkillBox(u);  // 가운데 아래 스킬 칸
        }

        /// <summary>
        /// 가운데 아래 스킬 칸 — 모든 캐릭터에 늘 뜬다(칸은 늘 남음). 규칙:
        /// - E를 쓸 수 있으면 "[E] 이름"만(준비·개수 같은 글은 없음).
        /// - 못 쓰면(재사용 대기·막힘·이미 씀) 글이 하나도 없다(재사용 대기 진행은 오른쪽 아래 원 게이지).
        /// - 시간이 가는 단계(스킬 시전 진행, 안토니오 둘째 음표까지 5초 등)는 칸 안에 차오르는 막대.
        /// - 꾹 눌러 고르는·쓰는 동안의 조작 안내(취소: E, 멈춤: E 등)만 둘째 줄에.
        /// 보통 스킬(CharacterSkill)이 없고 E를 직접 받는 캐릭터(하스터 촉수·안토니오 음표)는 ISkillSlotInfo로 채운다.
        /// </summary>
        private void DrawSkillBox(float u)
        {
            bool hasSkill = skill != null && !string.IsNullOrEmpty(skill.SkillId);
            ISkillSlotInfo slot = null;
            if (!hasSkill && target != null) target.TryGetComponent(out slot);
            if (!hasSkill && slot == null) return;

            string name; bool ready; string hint = null; float fill = -1f; Color fillColor = new(0.4f, 0.9f, 0.5f);
            if (hasSkill)
            {
                name = skill.SkillName;
                ready = skill.State == SkillState.Ready && skill.CanUse;
                if (skill.State == SkillState.Casting)
                {
                    if (!string.IsNullOrEmpty(skill.StatusText)) hint = skill.StatusText; // 쓰는 동안 조작 안내(예: 멈춤: E, 넣기: E)
                    float own = skill.StatusBarFill;
                    if (float.IsNaN(own)) fill = skill.CastProgress;
                    else if (own >= 0f) { fill = own; fillColor = new Color(0.5f, 0.6f, 0.9f); }
                }
            }
            else
            {
                name = slot.SlotName; ready = slot.SlotReady; fill = slot.SlotFill;
            }
            string cancelHint = CancelHint(); // 꾹 눌러 자리를 고르는 중이면 그 취소 방법
            if (cancelHint != null) hint = cancelHint;

            float w = 300 * u, h = 70 * u;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 30 * u, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (ready)
            {
                small.normal.textColor = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(box.x, box.y + 4 * u, w, 34 * u), $"[E]  {name}", small);
            }
            if (!string.IsNullOrEmpty(hint))
            {
                small.normal.textColor = new Color(1f, 0.6f, 0.5f);
                GUI.Label(new Rect(box.x, box.y + 34 * u, w, 30 * u), hint, small);
            }
            else if (target != null && target.TryGetComponent(out IHudChargeIcons icons) && icons.IconMax > 0)
                DrawChargeIcons(icons, new Rect(box.x, box.y + 36 * u, w, 28 * u), u); // 예: 붉은나비 — 쥔 나비를 나비 그림으로

            // 시간이 가는 단계: 칸 안 아래쪽에 차오르는 막대
            if (fill >= 0f) Bar(new Rect(box.x + 12 * u, box.yMax - 18 * u, w - 24 * u, 10 * u), Mathf.Clamp01(fill), fillColor);

            if (Time.time < blockedMessageUntil)
            {
                small.normal.textColor = new Color(1f, 0.5f, 0.4f);
                GUI.Label(new Rect(box.x, box.y - 40 * u, w, 36 * u), blockedMessage, small);
            }
        }

        // ───────── 오른쪽 아래: 재사용 대기 게이지(ICooldownGauge) ─────────
        private readonly System.Collections.Generic.List<ICooldownGauge> gauges = new();

        /// <summary>
        /// 재사용 대기 중인 기술마다 원 하나(오른쪽 아래 구석부터 왼쪽으로): 원이 12시 방향에서 시계 방향으로 돌면서 채워지고(파랑), 다 차면 바로 사라진다.
        /// 줄어드는 게이지(주황, 예: 그랩 다시 쏘기)는 채워진 부분이 돌면서 줄어든다. 원 아래에 이름. 남은 초는 글로 쓰지 않는다.
        /// </summary>
        private void DrawCooldowns(float u)
        {
            if (target == null) return;
            target.GetComponents(gauges);
            float r = 34 * u, gap = 22 * u, labelH = 26 * u;
            float cx = Screen.width - 40 * u - r, cy = Screen.height - 40 * u - labelH - r;
            foreach (var g in gauges)
            {
                if (g is Behaviour { isActiveAndEnabled: false }) continue;
                if (!g.TryGetCooldown(out string name, out float fill, out bool draining)) continue;
                DrawGaugeCircle(new Vector2(cx, cy), r, labelH, name, Mathf.Clamp01(fill), draining ? new Color(1f, 0.6f, 0.2f) : new Color(0.35f, 0.65f, 1f));
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

        // ───────── 스킬 칸 안 "쥔 개수" 그림(IHudChargeIcons) ─────────
        private static Texture2D butterflyIcon;

        /// <summary>그림을 가운데 줄에 나란히: 가진 만큼 진하게, 다음 하나는 차는 만큼 아래에서 위로 채워지고, 나머지는 흐리게.</summary>
        private static void DrawChargeIcons(IHudChargeIcons icons, Rect area, float u)
        {
            Texture2D tex = icons.IconKind == "butterfly" ? ButterflyIcon() : Texture2D.whiteTexture;
            Color full = icons.IconKind == "butterfly" ? new Color(0.95f, 0.2f, 0.25f) : Color.white;
            Color dim = new(1f, 1f, 1f, 0.18f);
            float size = area.height, gap = 8 * u;
            float total = icons.IconMax * size + (icons.IconMax - 1) * gap;
            float x = area.center.x - total * 0.5f;
            for (int i = 0; i < icons.IconMax; i++, x += size + gap)
            {
                var r = new Rect(x, area.y, size, size);
                GUI.color = dim;
                GUI.DrawTexture(r, tex);
                float f = i < icons.IconCount ? 1f : i == icons.IconCount ? Mathf.Clamp01(icons.IconNextFill) : 0f;
                if (f > 0f)
                {
                    // 아래에서 위로 f만큼만 진하게(차는 중인 나비)
                    GUI.color = i < icons.IconCount ? full : new Color(full.r, full.g, full.b, 0.75f);
                    GUI.DrawTextureWithTexCoords(new Rect(r.x, r.yMax - r.height * f, r.width, r.height * f), tex, new Rect(0f, 0f, 1f, f));
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>나비 기호(코드로 한 번 그려 둠): 위 날개 둘(크게)·아래 날개 둘(작게)·가운데 몸통·더듬이. 흰색 — 칠할 때 GUI.color로 색을 입힌다.</summary>
        private static Texture2D ButterflyIcon()
        {
            if (butterflyIcon != null) return butterflyIcon;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // 0~1 좌표(아래 → 위), 가운데 기준 좌우 대칭
                    float fx = Mathf.Abs((x + 0.5f) / N - 0.5f), fy = (y + 0.5f) / N;
                    bool upper = Ellipse(fx, fy, 0.22f, 0.62f, 0.2f, 0.2f, 35f);  // 위 날개
                    bool lower = Ellipse(fx, fy, 0.17f, 0.33f, 0.15f, 0.13f, -30f); // 아래 날개
                    bool body = fx < 0.035f && fy > 0.22f && fy < 0.78f;
                    bool antenna = Mathf.Abs(fx - (fy - 0.78f) * 0.6f) < 0.018f && fy > 0.78f && fy < 0.95f;
                    float a = upper || lower || body || antenna ? 1f : 0f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return butterflyIcon = tex;
        }

        /// <summary>(fx, fy)가 가운데(cx, cy)·반지름(rx, ry)·기울기(도) 타원 안인지.</summary>
        private static bool Ellipse(float fx, float fy, float cx, float cy, float rx, float ry, float tiltDeg)
        {
            float t = tiltDeg * Mathf.Deg2Rad, dx = fx - cx, dy = fy - cy;
            float lx = dx * Mathf.Cos(t) + dy * Mathf.Sin(t), ly = -dx * Mathf.Sin(t) + dy * Mathf.Cos(t);
            return (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry) <= 1f;
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

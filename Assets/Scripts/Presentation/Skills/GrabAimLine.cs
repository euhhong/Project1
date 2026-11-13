using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩 조준 보조선. 내가 조종하는 사냥터지기(LocalPlayerInput)에만 보인다.
    /// - 점선(- - -): 그랩이 나가는 손 자리(GrabShotSkill.Origin)에서 던질 방향(몸 = 시야)으로.
    /// - 점선·흰색 일자선 모두 확실히 잡히는 거리(최대 사거리 - endBarInset)까지만, 벽이 더 가까우면 벽 앞까지. 흰 선은 점선 끝에 조준 방향과 수직으로 짧게.
    /// - 쓸 수 있을 때는 옅게, E를 눌러 조준하는 동안(준비·꾹 누르기)은 진하게. 그랩이 날아간 뒤·재사용 대기 중에는 숨긴다.
    /// 연출 전용(판정은 BE GrabShotSkill). GrabShotVisual이 붙을 때 같이 붙는다(RequireComponent).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabAimLine : MonoBehaviour
    {
        [Tooltip("선 굵기(m).")]
        [SerializeField, Min(0.005f)] private float width = 0.045f;
        [Tooltip("점선 한 칸(선 + 빈칸) 길이(m).")]
        [SerializeField, Min(0.05f)] private float dashPeriod = 0.45f;
        [Tooltip("한 칸 중 선이 차지하는 비율.")]
        [SerializeField, Range(0.1f, 0.9f)] private float dashFill = 0.6f;
        [SerializeField] private Color idleColor = new(0.85f, 0.75f, 1f, 0.3f);
        [SerializeField] private Color aimColor = new(0.85f, 0.6f, 1f, 0.9f);

        [Header("리치 끝 표시 (흰색 일자선)")]
        [Tooltip("그랩이 닿는 끝에 조준 방향과 수직으로 긋는 흰 선의 길이(m).")]
        [SerializeField, Min(0.05f)] private float endBarLength = 0.7f;
        [Tooltip("흰 선을 그랩 최대 사거리보다 이만큼(m) 앞에 긋는다. 땅이 울퉁불퉁하거나 상대가 움직여도 확실히 닿는 거리를 보여 주려고(최대 사거리 끝은 겨우 닿는 거리).")]
        [SerializeField, Min(0f)] private float endBarInset = 3f;
        [SerializeField, Min(0.005f)] private float endBarWidth = 0.05f;
        [SerializeField] private Color endBarIdleColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color endBarAimColor = new(1f, 1f, 1f, 0.95f);

        private CharacterSkill skill;
        private bool isLocal;
        private LineRenderer line, endBar;
        private Texture2D dashTexture;

        private void Awake()
        {
            skill = GetComponent<CharacterSkill>();
            isLocal = GetComponent<LocalPlayerInput>() != null;
        }

        private void OnDestroy()
        {
            if (line != null) { Destroy(line.material); Destroy(line.gameObject); }
            if (endBar != null) { Destroy(endBar.material); Destroy(endBar.gameObject); }
            if (dashTexture != null) Destroy(dashTexture);
        }

        private void OnDisable() => Hide();

        private void Hide()
        {
            if (line != null) line.enabled = false;
            if (endBar != null) endBar.enabled = false;
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<CharacterSkill>();
            var grab = skill != null ? skill.Skill as GrabShotSkill : null;
            bool aiming = grab != null && grab.IsAiming(skill);
            bool show = isLocal && grab != null && (skill.State == SkillState.Ready || aiming);
            if (!show) { Hide(); return; }

            Vector3 dir = aiming ? skill.Direction : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < 0.0001f) { Hide(); return; }
            Vector3 origin = grab.Origin(transform, dir);
            // 점선과 흰 선 모두 '확실히 닿는 거리'(최대 사거리 - endBarInset)까지만. 벽이 더 가까우면 벽 앞까지.
            float length = Mathf.Min(grab.AimLength(origin, dir), Mathf.Max(0.5f, grab.range - endBarInset));
            Vector3 end = origin + dir * length;

            EnsureLines();
            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, end);
            Color c = aiming ? aimColor : idleColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, c.a * 0.6f); // 끝으로 갈수록 살짝 옅게
            line.textureScale = new Vector2(1f / dashPeriod, 1f);

            // 점선 끝: 조준 방향과 수직인 흰 일자선.
            Vector3 barAt = end;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * (endBarLength * 0.5f);
            endBar.enabled = true;
            endBar.SetPosition(0, barAt - side);
            endBar.SetPosition(1, barAt + side);
            Color b = aiming ? endBarAimColor : endBarIdleColor;
            endBar.startColor = b;
            endBar.endColor = b;
        }

        private void EnsureLines()
        {
            if (line != null) return;
            // 점선 무늬: 한 칸 = 선(dashFill) + 빈칸, 길이 방향으로 반복.
            const int px = 16;
            dashTexture = new Texture2D(px, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "GrabAimDash" };
            int filled = Mathf.Clamp(Mathf.RoundToInt(px * dashFill), 1, px - 1);
            for (int i = 0; i < px; i++) dashTexture.SetPixel(i, 0, i < filled ? Color.white : Color.clear);
            dashTexture.Apply();

            line = NewLine("GrabAimLine", width);
            line.textureMode = LineTextureMode.Tile;
            line.material.mainTexture = dashTexture;
            endBar = NewLine("GrabReachEnd", endBarWidth);
        }

        private LineRenderer NewLine(string name, float lineWidth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = lineWidth;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            return lr;
        }
    }
}

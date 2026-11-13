using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩 조준 보조선(- - - 점선). 내가 조종하는 사냥터지기(LocalPlayerInput)에만 보인다.
    /// - 그랩이 나가는 손 자리(GrabShotSkill.Origin)에서 던질 방향(몸 = 시야)으로 사거리 끝까지, 벽·물건에 먼저 닿으면 거기까지 긋는다.
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

        private CharacterSkill skill;
        private bool isLocal;
        private LineRenderer line;
        private Texture2D dashTexture;

        private void Awake()
        {
            skill = GetComponent<CharacterSkill>();
            isLocal = GetComponent<LocalPlayerInput>() != null;
        }

        private void OnDestroy()
        {
            if (line != null) { Destroy(line.material); Destroy(line.gameObject); }
            if (dashTexture != null) Destroy(dashTexture);
        }

        private void OnDisable()
        {
            if (line != null) line.enabled = false;
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<CharacterSkill>();
            var grab = skill != null ? skill.Skill as GrabShotSkill : null;
            bool aiming = grab != null && grab.IsAiming(skill);
            bool show = isLocal && grab != null && (skill.State == SkillState.Ready || aiming);
            if (!show) { if (line != null) line.enabled = false; return; }

            Vector3 dir = aiming ? skill.Direction : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (dir.sqrMagnitude < 0.0001f) { if (line != null) line.enabled = false; return; }
            Vector3 origin = grab.Origin(transform, dir);
            float length = grab.AimLength(origin, dir);

            EnsureLine();
            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + dir * length);
            Color c = aiming ? aimColor : idleColor;
            line.startColor = c;
            line.endColor = new Color(c.r, c.g, c.b, c.a * 0.6f); // 끝으로 갈수록 살짝 옅게
            line.textureScale = new Vector2(1f / dashPeriod, 1f);
        }

        private void EnsureLine()
        {
            if (line != null) return;
            // 점선 무늬: 한 칸 = 선(dashFill) + 빈칸, 길이 방향으로 반복.
            const int px = 16;
            dashTexture = new Texture2D(px, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, name = "GrabAimDash" };
            int filled = Mathf.Clamp(Mathf.RoundToInt(px * dashFill), 1, px - 1);
            for (int i = 0; i < px; i++) dashTexture.SetPixel(i, 0, i < filled ? Color.white : Color.clear);
            dashTexture.Apply();

            var go = new GameObject("GrabAimLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default")) { mainTexture = dashTexture };
        }
    }
}

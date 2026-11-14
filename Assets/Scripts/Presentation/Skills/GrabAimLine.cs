using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩 조준선. 내가 조종하는 사냥터지기(LocalPlayerInput)에만 보인다.
    /// - 던질 방향(몸 = 시야)으로 확실히 잡히는 거리(최대 사거리 - endBarInset, 벽이 더 가까우면 벽 앞)에
    ///   조준 방향과 수직인 흰 일자선을 긋되, 가운데에 구멍을 뚫어(— —) 조준선처럼 보이게 한다. 높이는 그랩이 나가는 손 높이.
    /// - 지금 던지면 맞을 때(상대 판정 또는 끌려갈 벽 — GrabShotSkill.PredictsHit)는 흰 선이 맞는 자리(그랩 타격 지점)로 따라가고,
    ///   구멍 바로 위에 세로 가늠쇠 선을 하나 더 세운다. 가늠쇠가 없으면 빗나간다.
    /// - 쓸 수 있을 때는 옅게, E를 눌러 조준하는 동안(준비·꾹 누르기)은 진하게. 그랩이 날아간 뒤·재사용 대기 중에는 숨긴다.
    /// 연출 전용(판정은 BE GrabShotSkill). GrabShotVisual이 붙을 때 같이 붙는다(RequireComponent).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabAimLine : MonoBehaviour
    {
        [Tooltip("흰 선 전체 길이(가운데 구멍 포함, m).")]
        [SerializeField, Min(0.05f)] private float barLength = 0.8f;
        [Tooltip("가운데 구멍 너비(m).")]
        [SerializeField, Min(0f)] private float gap = 0.22f;
        [Tooltip("흰 선을 그랩 최대 사거리보다 이만큼(m) 앞에 긋는다. 땅이 울퉁불퉁하거나 상대가 움직여도 확실히 닿는 거리를 보여 주려고(최대 사거리 끝은 겨우 닿는 거리).")]
        [SerializeField, Min(0f)] private float endBarInset = 5f;
        [Tooltip("선 굵기(m).")]
        [SerializeField, Min(0.005f)] private float barWidth = 0.05f;
        [Tooltip("맞을 때 구멍 위에 세우는 가늠쇠 선 높이(m).")]
        [SerializeField, Min(0.01f)] private float sightHeight = 0.22f;
        [Tooltip("가늠쇠 선을 흰 선보다 이만큼(m) 위에서 시작한다.")]
        [SerializeField, Min(0f)] private float sightLift = 0.08f;
        [Tooltip("맞을 때 흰 선을 맞는 자리보다 이만큼(m) 앞에 긋는다(벽·몸에 묻히지 않게).")]
        [SerializeField, Min(0f)] private float hitBarPullBack = 0.1f;
        [SerializeField] private Color idleColor = new(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color aimColor = new(1f, 1f, 1f, 0.95f);

        private CharacterSkill skill;
        private bool isLocal;
        private LineRenderer left, right, sight;

        private void Awake()
        {
            skill = GetComponent<CharacterSkill>();
            isLocal = GetComponent<LocalPlayerInput>() != null;
        }

        private void OnDestroy()
        {
            Destroy(ref left);
            Destroy(ref right);
            Destroy(ref sight);
        }

        private void OnDisable() => Hide();

        private void Hide()
        {
            if (left != null) left.enabled = false;
            if (right != null) right.enabled = false;
            if (sight != null) sight.enabled = false;
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
            // 지금 던지면 맞는다(상대 판정 또는 끌려갈 벽) → 흰 선을 맞는 자리로 옮기고 그 위에 가늠쇠 선. 아니면 확실히 닿는 거리(벽이 가까우면 벽 앞).
            bool hit = grab.PredictsHit(transform, origin, dir, out float hitAt);
            float length = hit ? Mathf.Max(0.3f, hitAt - hitBarPullBack)
                               : Mathf.Min(grab.AimLength(origin, dir), Mathf.Max(0.5f, grab.range - endBarInset));
            Vector3 at = origin + dir * length;

            if (left == null) { left = NewLine("GrabAimBar_L"); right = NewLine("GrabAimBar_R"); }
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float half = barLength * 0.5f, inner = Mathf.Min(gap * 0.5f, half - 0.01f);
            Color c = aiming ? aimColor : idleColor;
            Set(left, at - side * half, at - side * inner, c);
            Set(right, at + side * inner, at + side * half, c);

            if (hit)
            {
                if (sight == null) sight = NewLine("GrabAimSight");
                Set(sight, at + Vector3.up * sightLift, at + Vector3.up * (sightLift + sightHeight), c); // 구멍 바로 위에 세운다
            }
            else if (sight != null) sight.enabled = false;
        }

        private static void Set(LineRenderer lr, Vector3 a, Vector3 b, Color c)
        {
            lr.enabled = true;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startColor = c;
            lr.endColor = c;
        }

        private LineRenderer NewLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.widthMultiplier = barWidth;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            return lr;
        }

        private static void Destroy(ref LineRenderer lr)
        {
            if (lr == null) return;
            Object.Destroy(lr.material);
            Object.Destroy(lr.gameObject);
            lr = null;
        }
    }
}

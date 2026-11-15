using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 카우보이 올가미 연출. 판정은 BE GrabShotSkill(사냥터지기 그랩과 같은 스킬, 카우보이 설정).
    /// - 올가미가 나가 있으면(ICharacterSkillState.Reach &gt; 0) 오른손에서 끝(ReachOrigin + Direction × Reach)까지 밧줄을 긋고, 끝에 고리를 그린다.
    ///   밧줄은 살짝 처진다(나가는 동안은 팽팽하게, 되감을 때 조금 더 처짐).
    /// - 상대를 뛰어넘는 동안은 밧줄 끝이 문 자리(GrabShotSkill.VaultHook)에 걸려 있고, 고리가 상대를 감싼다.
    /// 조준 보조선은 GrabAimLine(사냥터지기와 같음)이 같이 붙는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 카우보이만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)] // 줄 돌리는 팔 자세(RopeSpinPose) 뒤에 — 손 자리를 따라 그린다
    [RequireComponent(typeof(GrabAimLine))]
    public class LassoVisual : MonoBehaviour
    {
        private const int RopePoints = 16, LoopPoints = 20;

        [SerializeField, Min(0.005f)] private float ropeWidth = 0.035f;
        [Tooltip("올가미 고리 반지름(m) — 날아갈 때 / 상대를 문 때.")]
        [SerializeField, Min(0.05f)] private float loopRadius = 0.45f, loopBiteRadius = 0.35f;
        [Tooltip("밧줄이 처지는 정도(길이 대비).")]
        [SerializeField, Range(0f, 0.2f)] private float sag = 0.04f;
        [SerializeField] private Color ropeColor = new(0.55f, 0.38f, 0.2f, 1f);

        private CharacterSkill skill;
        private Animator animator;
        private Transform hand;
        private LineRenderer rope, loop;
        private float lastReach;

        private void Awake() => skill = GetComponent<CharacterSkill>();

        private void OnDestroy()
        {
            if (rope != null) { Destroy(rope.material); Destroy(rope.gameObject); }
            if (loop != null) { Destroy(loop.material); Destroy(loop.gameObject); }
        }

        private void OnDisable() => Hide();

        private void Hide()
        {
            if (rope != null) rope.enabled = false;
            if (loop != null) loop.enabled = false;
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<CharacterSkill>();
            var grab = skill != null ? skill.Skill as GrabShotSkill : null;
            float reach = skill != null ? skill.Reach : 0f;
            bool vaulting = grab != null && grab.IsVaulting(skill);
            if (grab != null && reach <= 0.01f && grab.IsAiming(skill)) { if (ResolveHand()) { Ensure(); DrawSpin(); } lastReach = 0f; return; } // 조준 중: 머리 위에서 고리를 붕붕 돌린다
            if (grab == null || (reach <= 0.01f && !vaulting)) { Hide(); lastReach = 0f; return; }
            if (!ResolveHand()) return;
            Ensure();

            Vector3 start = hand.position;
            Vector3 dir = skill.Direction.sqrMagnitude > 0.01f ? skill.Direction.normalized : transform.forward;
            Vector3 end = vaulting ? grab.VaultHook(skill) : skill.ReachOrigin + dir * reach;
            if (vaulting && reach <= 0.01f) { Hide(); return; } // 상대를 지나치면 놓는다

            bool retracting = reach < lastReach - 1e-4f;
            lastReach = reach;
            float length = Vector3.Distance(start, end);
            float droop = vaulting ? 0f : length * sag * (retracting ? 2f : 1f);

            rope.enabled = true;
            rope.positionCount = RopePoints;
            for (int i = 0; i < RopePoints; i++)
            {
                float t = i / (RopePoints - 1f);
                rope.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.down * (droop * 4f * t * (1f - t)));
            }

            // 고리: 끝에서 밧줄 방향에 수직인 원(누운 고리 — 상대를 위에서 감싼다).
            Vector3 along = (end - start).sqrMagnitude > 1e-6f ? (end - start).normalized : dir;
            Vector3 side = Vector3.Cross(Vector3.up, along); side = side.sqrMagnitude > 1e-6f ? side.normalized : transform.right;
            Vector3 fwd = Vector3.Cross(side, Vector3.up).normalized;
            float r = vaulting || retracting ? loopBiteRadius : loopRadius;
            Vector3 center = end + fwd * r;
            loop.enabled = true;
            loop.positionCount = LoopPoints + 1;
            for (int i = 0; i <= LoopPoints; i++)
            {
                float a = i / (float)LoopPoints * Mathf.PI * 2f;
                loop.SetPosition(i, center - fwd * (Mathf.Cos(a) * r) + side * (Mathf.Sin(a) * r));
            }
        }

        [Tooltip("조준 중 붕붕 돌리기: 고리 반지름(m) / 손에서 고리 가운데까지 거리(m) / 손 위 높이(m).")]
        [SerializeField, Min(0.05f)] private float spinLoopRadius = 0.5f;
        [SerializeField, Min(0f)] private float spinOffset = 0.65f, spinHeight = 0.12f;

        /// <summary>조준 중(E 꾹): 손 위에서 누운 고리가 손 둘레를 돌고(RopeSpin 박자), 손에서 고리 가장 가까운 쪽까지 짧은 밧줄.</summary>
        private void DrawSpin()
        {
            float a = RopeSpin.Angle;
            Vector3 handPos = hand.position;
            Vector3 outward = RopeSpin.Around(transform, a);
            Vector3 center = handPos + Vector3.up * spinHeight + outward * spinOffset;
            Vector3 near = center - outward * spinLoopRadius; // 고리에서 손 쪽 끝
            rope.enabled = true;
            rope.positionCount = 2;
            rope.SetPosition(0, handPos);
            rope.SetPosition(1, near);
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            loop.enabled = true;
            loop.positionCount = LoopPoints + 1;
            for (int i = 0; i <= LoopPoints; i++)
            {
                float t = i / (float)LoopPoints * Mathf.PI * 2f;
                // 살짝 기울어진 고리(바깥쪽이 조금 낮게 — 도는 힘에 끌려 처짐)
                Vector3 p = center + outward * (Mathf.Cos(t) * spinLoopRadius) + side * (Mathf.Sin(t) * spinLoopRadius);
                p += Vector3.down * (Mathf.Cos(t) * spinLoopRadius * 0.15f);
                loop.SetPosition(i, p);
            }
        }

        private bool ResolveHand()
        {
            if (animator != null && animator.isActiveAndEnabled && hand != null) return true;
            animator = GetComponentInChildren<Animator>();
            hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            return hand != null;
        }

        private void Ensure()
        {
            if (rope != null) return;
            rope = MakeLine("LassoRope", ropeWidth);
            loop = MakeLine("LassoLoop", ropeWidth);
            loop.loop = false;
        }

        private LineRenderer MakeLine(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2; lr.numCornerVertices = 2;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ropeColor);
            mat.color = ropeColor;
            lr.material = mat;
            lr.startColor = lr.endColor = ropeColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return lr;
        }
    }
}

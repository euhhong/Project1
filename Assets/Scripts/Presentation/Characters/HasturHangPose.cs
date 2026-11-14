using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터가 턱(난간·바닥 끝)에 매달릴 때의 자세(연출 전용). 하스터 모델은 Generic 리그라 생존자 매달리기 모션(Mixamo)이 재생되지 않는다.
    /// - 두 팔(소매)을 머리 위 턱 쪽으로 뻗고, 땅까지 닿는 두 촉수 팔은 턱 모서리를 넘어 위로 말아 감아 쥔다
    ///   (바닥이 이어지면 윗면을 따라 눕고, 얇은 난간이면 너머로 늘어진다).
    /// - 다리 대신 치마 밑 촉수 다섯 가닥은 벽 쪽으로 눕혀 벽에 붙인다(벽이 없으면 아래로 늘어진다).
    /// 매달림(ICharacterTraversalState: HangGrab·Hang)에서 켜지고, 매달려 올라가는 동안(HangClimb·HangClimbRail·HangClimbOver)은
    /// 손을 턱에 둔 채 몸이 올라가다 끝 무렵 놓는다. 턱 위치는 매달리기 시작할 때 레이로 찾는다.
    /// 뼈는 그 뼈를 맡은 컴포넌트가 이 자세를 불러 적용한다(팔 = HasturArmWalk, 다리 촉수 = HasturLegWriggle) — 한 뼈를 두 곳에서 쓰지 않게.
    /// HasturLegWriggle이 붙인다(하스터만).
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturHangPose : MonoBehaviour
    {
        [Tooltip("두 촉수 팔이 턱을 잡는 자리 사이 간격(캐릭터 키 대비, 한쪽).")]
        [SerializeField, Range(0f, 0.5f)] private float gripSpread = 0.14f;
        [Tooltip("소매 끝(손)이 가는 자리: 턱 모서리 아래(키 대비) / 벽 앞(키 대비).")]
        [SerializeField, Range(0f, 1f)] private float handBelow = 0.3f, handOut = 0.1f;
        [Tooltip("매달릴 때 어깨를 들어 올리는 각도(도).")]
        [SerializeField, Range(0f, 60f)] private float shoulderRaise = 22f;
        [Tooltip("다리 촉수를 벽 쪽으로 눕히는 정도(아래 1 기준 앞으로) / 옆으로 벌리는 정도.")]
        [SerializeField, Range(0f, 2f)] private float legToWall = 0.6f, legSpread = 0.35f;
        [Tooltip("자세가 켜지는/꺼지는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.12f, fadeOut = 0.2f;

        private ICharacterTraversalState traversal;
        private CharacterController cc;
        private TraversalAction lastAction;
        private bool hasLedge, floorOnTop;
        private Vector3 edge, normal; // 턱 모서리(가운데, 월드) / 벽 바깥 방향(캐릭터 쪽)
        private float unit = 1f;      // 캐릭터 키(m)
        private readonly RaycastHit[] hits = new RaycastHit[16];

        /// <summary>매달리기 자세 크기(0~1).</summary>
        public float Weight { get; private set; }

        /// <summary>턱 아래에 벽이 있어 다리 촉수를 벽에 붙이는지.</summary>
        public bool Braced => previewBraced ?? (traversal != null && traversal.BracedHang);
        private bool? previewBraced;

        /// <summary>미리보기·확인용(에디터): 턱 모서리 edge(월드)·바깥 방향 outward·캐릭터 키 height로 매달린 자세를 바로 켠다(Update가 다시 정한다).</summary>
        public void Preview(Vector3 edgeWorld, Vector3 outward, bool floorBeyond, float height, bool braced)
        {
            edge = edgeWorld; normal = outward.normalized; floorOnTop = floorBeyond; unit = height;
            previewBraced = braced; hasLedge = true; Weight = 1f;
        }

        private void Update()
        {
            if (!TryGetComponent(out HasturLegWriggle _)) { Destroy(this); return; } // 하스터가 아니게 됨
            traversal ??= GetComponent<ICharacterTraversalState>();
            if (cc == null) TryGetComponent(out cc);
            var a = traversal != null ? traversal.Action : TraversalAction.None;
            if ((a == TraversalAction.HangGrab || a == TraversalAction.Hang) && a != lastAction) FindLedge(); // 잡을 때 + 몸이 자리 잡은 뒤 다시
            else if (IsHang(a) && !IsHang(lastAction)) FindLedge();
            lastAction = a;

            float target = 0f;
            if (hasLedge)
            {
                float p = traversal.ActionProgress;
                target = a switch
                {
                    TraversalAction.HangGrab or TraversalAction.Hang => 1f,
                    TraversalAction.HangClimb or TraversalAction.HangClimbRail => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.85f, p)), // 올라선 뒤 놓음
                    TraversalAction.HangClimbOver => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, p)),
                    _ => 0f,
                };
            }
            Weight = Mathf.MoveTowards(Weight, target, Time.deltaTime / (target > Weight ? fadeIn : fadeOut));
            if (Weight <= 0f && !IsHang(a)) hasLedge = false;
        }

        private static bool IsHang(TraversalAction a) => a is TraversalAction.HangGrab or TraversalAction.Hang or TraversalAction.HangClimb
            or TraversalAction.HangClimbRail or TraversalAction.HangClimbOver;

        /// <summary>매달린 턱 모서리를 찾는다: 캐릭터는 벽을 보고 있다(앞 = 벽 쪽). 위에서 아래로 윗면, 옆에서 앞으로 벽면.</summary>
        private void FindLedge()
        {
            unit = cc != null ? Mathf.Max(cc.height, 0.5f) : 2f;
            float radius = cc != null ? cc.radius : 0.4f;
            Vector3 up = Vector3.up, fwd = transform.forward;
            fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            normal = -fwd;
            float hu = unit / 1.75f; // CharacterTraversal: Humanoid가 아니면 키 / 1.75
            float guessTop = transform.position.y + ((traversal != null && traversal.BracedHang) ? 2.28f : 2.05f) * hu;
            float top = guessTop;
            Vector3 probe = transform.position + fwd * (radius + 0.03f + 0.12f);
            probe.y = guessTop + 0.6f * hu;
            if (Cast(probe, Vector3.down, 1.2f * hu, out var topHit)) top = topHit.point.y;
            Vector3 facePt = transform.position; facePt.y = top - 0.06f;
            float faceDist = radius + 0.03f;
            if (Cast(facePt, fwd, radius + 0.6f, out var faceHit)) faceDist = faceHit.distance;
            edge = transform.position + fwd * faceDist; edge.y = top;
            // 턱 너머로 바닥이 이어지는지(이어지면 촉수 팔이 윗면을 따라 눕고, 얇은 난간이면 너머로 늘어진다)
            Vector3 beyond = edge + fwd * (0.3f * unit) + up * 0.3f;
            floorOnTop = Cast(beyond, Vector3.down, 0.45f, out _);
            hasLedge = true;
        }

        private bool Cast(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
        {
            best = default;
            int n = Physics.RaycastNonAlloc(origin, dir, hits, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= bestD) continue; // 자기 몸은 뺀다
                best = h; bestD = h.distance;
            }
            return bestD < float.MaxValue;
        }

        /// <summary>
        /// 한 팔에 매달리기 자세를 Weight만큼 섞는다(HasturArmWalk가 자기 자세를 잡은 뒤 부른다).
        /// side: 왼쪽 -1 / 오른쪽 +1. chain: 촉수 팔 마디(밑동부터, 아래팔에 붙음).
        /// </summary>
        public void ApplyArm(Transform clavicle, Transform upper, Transform fore, Transform hand, Transform[] chain, float side)
        {
            if (Weight <= 0f || !hasLedge || upper == null || fore == null || hand == null) return;
            float w = Weight;
            Vector3 up = Vector3.up, fwd = -normal;
            Vector3 right = Vector3.Cross(up, fwd).normalized;

            if (clavicle != null)
            {
                Vector3 along = upper.position - clavicle.position;
                Vector3 axis = Vector3.Cross(along, up);
                if (axis.sqrMagnitude > 1e-10f) Blend(clavicle, Quaternion.AngleAxis(shoulderRaise, axis.normalized) * clavicle.rotation, w);
            }

            // 소매: 턱 모서리 아래 벽 앞으로 손을 뻗는다(팔꿈치는 바깥·아래로).
            Vector3 handGoal = edge + right * (side * gripSpread * 1.6f * unit) + Vector3.down * (handBelow * unit) + normal * (handOut * unit);
            Vector3 pole = right * side + Vector3.down * 0.5f + normal * 0.5f;
            SolveArm(upper, fore, hand, handGoal, pole, w);

            // 촉수 팔: 모서리 앞으로 올라가 턱을 넘어 위로 말아 감는다 → 윗면을 따라 눕거나(바닥) 너머로 늘어진다(얇은 난간).
            if (chain == null || chain.Length == 0) return;
            Transform tip = chain[^1];
            while (tip.childCount > 0) tip = tip.GetChild(0);
            Vector3 grip = edge + right * (side * gripSpread * unit);
            Vector3 a = grip + normal * (0.035f * unit) + up * (0.06f * unit);  // 모서리 앞 위
            Vector3 b = grip - normal * (0.06f * unit) + up * (0.035f * unit);  // 모서리 너머 윗면 위
            Vector3 c = floorOnTop ? b - normal * (0.8f * unit) + up * (0.01f * unit) // 윗면을 따라 눕힘
                                   : b - normal * (0.05f * unit) + Vector3.down * (0.8f * unit); // 난간 너머로 늘어짐
            Vector3[] path = { chain[0].position, a, b, c };
            float along2 = 0f;
            for (int i = 0; i < chain.Length; i++)
            {
                Transform bone = chain[i], next = i + 1 < chain.Length ? chain[i + 1] : tip;
                if (next == bone) break;
                path[0] = i == 0 ? chain[0].position : path[0];
                along2 += Vector3.Distance(bone.position, next.position);
                Vector3 goal = PointOnPath(path, along2);
                Vector3 from = next.position - bone.position, to = goal - bone.position;
                if (from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) continue;
                Blend(bone, Quaternion.FromToRotation(from, to) * bone.rotation, w);
            }
        }

        /// <summary>
        /// 다리 촉수 한 가닥에 매달리기 자세를 Weight만큼 섞는다(HasturLegWriggle이 꿈틀거림을 넣은 뒤 부른다): 밑동을 돌려 가닥이
        /// 벽 쪽 아래로(벽이 있으면 벽에 붙게, 없으면 아래로 늘어지게) 향하게 한다. 가닥 모양은 그대로.
        /// </summary>
        public void ApplyLeg(Transform root, Transform tip)
        {
            if (Weight <= 0f || !hasLedge || root == null || tip == null || tip == root) return;
            Vector3 fwd = -normal;
            Vector3 dir = tip.position - root.position;
            Vector3 flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            Vector3 lateral = Vector3.ProjectOnPlane(flat, fwd);
            lateral = lateral.sqrMagnitude > 1e-8f ? lateral.normalized : Vector3.zero;
            Vector3 goal = Vector3.down + lateral * legSpread + (Braced ? fwd * legToWall : Vector3.zero);
            Blend(root, Quaternion.FromToRotation(dir, goal.normalized * dir.magnitude) * root.rotation, Weight);
        }

        private static Vector3 PointOnPath(Vector3[] p, float s)
        {
            for (int i = 0; i + 1 < p.Length; i++)
            {
                float len = Vector3.Distance(p[i], p[i + 1]);
                if (s <= len || i + 2 == p.Length) return p[i] + (p[i + 1] - p[i]).normalized * s;
                s -= len;
            }
            return p[^1];
        }

        private static void SolveArm(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 pole, float w)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float c = Vector3.Distance(fore.position, hand.position);
            if (a < 1e-5f || c < 1e-5f) return;
            Vector3 s = upper.position, d = target - s;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(a - c) + 1e-4f, (a + c) * 0.999f);
            Vector3 dir = d.normalized;
            float cosA = Mathf.Clamp((a * a + dist * dist - c * c) / (2f * a * dist), -1f, 1f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            Vector3 elbow = s + dir * (a * cosA) + bend.normalized * (a * Mathf.Sqrt(1f - cosA * cosA));
            Aim(upper, fore.position, elbow, w);
            Aim(fore, hand.position, s + dir * dist, w);
        }

        private static void Aim(Transform bone, Vector3 childPos, Vector3 goal, float w)
        {
            Vector3 from = childPos - bone.position, to = goal - bone.position;
            if (from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) return;
            Blend(bone, Quaternion.FromToRotation(from, to) * bone.rotation, w);
        }

        private static void Blend(Transform t, Quaternion target, float w) =>
            t.rotation = w >= 1f ? target : Quaternion.Slerp(t.rotation, target, w);
    }
}

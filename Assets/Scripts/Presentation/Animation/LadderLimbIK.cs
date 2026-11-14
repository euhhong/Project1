using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사다리 손발 IK(FE, 모델의 Animator 오브젝트 — CharacterTraversalAnimator가 붙인다). 사다리를 타는 동안(TraversalAction.Ladder)
    /// 두 손·두 발을 실제 가로대(Ladder, 0.8m 간격)에 붙여 크게 오르는 것처럼 보이게 한다. 모션(사다리 오르기)은 몸통만 맡는다.
    /// - 한 사이클(ActionProgress 0→1) = 가로대 두 칸(Ladder.CycleRise). 손은 머리 위 높이(handReach)의 가로대를 잡고 몸이 오르는 동안 쥐고 있다가(반 사이클)
    ///   두 칸 위 가로대로 뻗는다. 왼손과 오른손은 반 사이클 어긋나 늘 한 손은 쥐고 있다. 발은 무릎을 높이 올린 높이(footReach)의 가로대를 디뎠다가 펴며 밀고(holdFoot) 두 칸 위로.
    ///   발은 대각선 손과 같이(왼손 + 오른발).
    /// - 진행도가 오른 높이에서 나오므로(BE) 멈추면 손발도 멈추고, 내려가면 거꾸로 움직인다.
    /// - 높이는 Humanoid 단위(키가 다른 캐릭터도 같은 자세). Traversal 레이어에 IK Pass가 켜져 있어야 불린다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class LadderLimbIK : MonoBehaviour
    {
        [Tooltip("손이 가로대를 잡는 높이(루트 위, Humanoid 단위) — 머리 위로 크게 뻗음.")]
        [SerializeField] private float handReach = 2.6f;
        [Tooltip("발이 가로대를 딛는 높이(루트 위, Humanoid 단위) — 무릎을 높이 올림.")]
        [SerializeField] private float footReach = 1.55f;
        [Tooltip("한 사이클 중 손 / 발이 가로대를 쥐고(딛고) 있는 몫. 나머지 동안 두 칸 위로 옮긴다.")]
        [SerializeField, Range(0.2f, 0.9f)] private float holdHand = 0.5f, holdFoot = 0.4f;
        [Tooltip("손 / 발의 좌우 벌림(사다리 가운데에서, m).")]
        [SerializeField] private float handSpread = 0.24f, footSpread = 0.16f;
        [Tooltip("옮기는 동안 사다리에서 떨어지는 거리(m) — 가로대를 넘어 뻗는 호.")]
        [SerializeField] private float swingOut = 0.18f;
        [Tooltip("IK 켜고 끄는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fade = 0.12f;

        private Animator animator;
        private ICharacterTraversalState traversal;
        private Transform root;
        private Ladder ladder;
        private float weight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            traversal = GetComponentInParent<ICharacterTraversalState>();
            root = traversal is Component c ? c.transform : transform.root;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (traversal == null || !animator.isHuman) return;
            bool on = traversal.Action == TraversalAction.Ladder;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, Time.deltaTime / fade);
            if (on) ladder = NearestLadder();
            if (weight <= 0f || ladder == null) { Clear(); return; }

            float hu = animator.humanScale * transform.lossyScale.y;
            float p = traversal.ActionProgress;
            float cycle = ladder.CycleRise;
            float rootY = root.position.y;
            Vector3 n = ladder.Normal, right = Vector3.Cross(Vector3.up, n);

            Limb(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, p, 0f, holdHand, handReach * hu, -handSpread);
            Limb(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, p, 0.5f, holdHand, handReach * hu, handSpread);
            Limb(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, p, 0f, holdFoot, footReach * hu, footSpread);
            Limb(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, p, 0.5f, holdFoot, footReach * hu, -footSpread);

            void Limb(AvatarIKGoal goal, AvatarIKHint hint, float phase, float offset, float hold, float reach, float side)
            {
                float q = Mathf.Repeat(phase + offset, 1f);
                float grabRoot = rootY - q * cycle; // 이 사이클에 이 손(발)이 가로대를 잡은 순간의 루트 높이
                float from = ladder.RungY(ladder.NearestRung(grabRoot + reach));
                Vector3 basePoint = ladder.Bottom + n * ladder.Depth + right * side;
                Vector3 target;
                if (q < hold) target = new Vector3(basePoint.x, from, basePoint.z); // 쥐고(딛고) 있음
                else
                {
                    float to = ladder.RungY(ladder.NearestRung(grabRoot + cycle + reach)); // 두 칸 위
                    float u = Mathf.SmoothStep(0f, 1f, (q - hold) / (1f - hold));
                    target = new Vector3(basePoint.x, Mathf.Lerp(from, to, u), basePoint.z) + n * (Mathf.Sin(u * Mathf.PI) * swingOut);
                }
                bool hand = goal == AvatarIKGoal.LeftHand || goal == AvatarIKGoal.RightHand;
                if (hand) target += n * 0.04f + Vector3.down * 0.03f; // 손목 = 가로대 바로 앞·아래
                else target += n * 0.06f + Vector3.up * 0.05f;       // 발목 = 가로대 위
                animator.SetIKPositionWeight(goal, weight);
                animator.SetIKPosition(goal, target);
                // 팔꿈치는 옆·뒤로, 무릎은 사다리 바깥(몸 앞)으로
                Vector3 hintPos = hand ? target + right * (Mathf.Sign(side) * 0.45f) + n * 0.35f + Vector3.down * 0.35f
                                       : target + n * 0.55f + Vector3.up * 0.25f + right * (Mathf.Sign(side) * 0.1f);
                animator.SetIKHintPositionWeight(hint, weight);
                animator.SetIKHintPosition(hint, hintPos);
            }
        }

        private void Clear()
        {
            foreach (var g in new[] { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
                animator.SetIKPositionWeight(g, 0f);
            foreach (var h in new[] { AvatarIKHint.LeftElbow, AvatarIKHint.RightElbow, AvatarIKHint.LeftKnee, AvatarIKHint.RightKnee })
                animator.SetIKHintPositionWeight(h, 0f);
        }

        /// <summary>몸에서 가장 가까운 사다리(타는 동안만 찾는다).</summary>
        private Ladder NearestLadder()
        {
            Ladder best = null;
            float bestSq = 4f; // 2m 안
            Vector3 pos = root.position;
            foreach (var l in Ladder.All)
            {
                if (l == null) continue;
                Vector3 d = l.Bottom - pos; d.y = 0f;
                if (pos.y < l.BottomY - 3f || pos.y > l.TopY + 1f || d.sqrMagnitude >= bestSq) continue;
                bestSq = d.sqrMagnitude; best = l;
            }
            return best;
        }
    }
}

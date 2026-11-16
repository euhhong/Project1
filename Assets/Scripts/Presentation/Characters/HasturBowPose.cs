using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 노란옷의 왕이 촉수를 내려찍는 동안(TentacleState.SlamWindup·SlamActive) 에스코트하듯 정중하게 인사하는 자세(연출 전용 — Generic 리그라 런타임으로 뼈를 돌린다).
    /// - 움직이는 것은 오른 어깨 라인뿐: 오른 어깨(biped R Clavicle)를 앞·아래로 살짝 떨구고, 오른 촉수팔(R UpperArm·R Forearm)을 젠틀하게 내린다
    ///   (위팔은 아래·살짝 앞으로, 아래팔은 허리 앞에서 안쪽으로 — 집사가 인사하듯). 고개는 인사하듯 앞으로 확실히 숙인다(biped Head, 정면에서도 보이게 — headDir). 오른 어깨가 앞으로 말려 인사를 이끈다.
    /// - 등·목은 숙이지 않는다(두 어깨가 목에 달려 있어 숙이면 왼팔도 따라 움직이므로). 왼팔·왼손은 모션 그대로 고정.
    ///   뼈 축 방향을 몰라도 되게 "뼈가 가리키는 방향"을 캐릭터 기준 목표 방향으로 돌린다(shoulderDir·armUpperDir·armLowerDir).
    /// 그동안 평타·촉수 세우기는 BE(TentacleField — IActionLock)가 막는다.
    /// 모션이 쓰는 뼈면 모션 자세 위에 더하고, 쓰지 않는 뼈면 쉬는 자세에서 더한다(매 프레임 쌓이지 않음).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 노란옷의 왕만).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // 걷기 팔(HasturArmWalk) 뒤에: 인사 자세가 걷기 흔들림 위에 덮여 오른팔이 몸 앞으로 내려온다
    public class HasturBowPose : MonoBehaviour
    {
        private static readonly string[] BowBoneNames = { "biped Head", "biped HeadNub" };
        private static readonly string[] ArmBoneNames = { "biped R Clavicle", "biped R UpperArm", "biped R Forearm", "biped R Hand" };

        [Tooltip("숙인 머리(머리 → 정수리)가 가리킬 방향(캐릭터 기준 x = 오른쪽, y = 위, z = 앞) — 인사하듯 앞으로 확실히 숙여 정면에서도 보이게(약 70도, 오른 어깨 쪽으로 아주 살짝).")]
        [SerializeField] private Vector3 headDir = new(0.08f, 0.3f, 0.95f);
        [Tooltip("오른 어깨(쇄골 → 위팔)가 가리킬 방향(캐릭터 기준 x = 오른쪽, y = 위, z = 앞) — 앞·아래로 살짝 떨굼 / 그쪽으로 돌리는 정도(0~1).")]
        [SerializeField] private Vector3 shoulderDir = new(0.85f, -0.4f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float shoulderAmount = 0.85f;
        [Tooltip("오른 위팔이 가리킬 방향(캐릭터 기준) — 아래로 내리고 살짝 앞.")]
        [SerializeField] private Vector3 armUpperDir = new(0.05f, -0.95f, 0.3f);
        [Tooltip("오른 아래팔이 가리킬 방향(캐릭터 기준) — 허리 앞에서 안쪽(왼쪽)·아래로 비스듬히 접는다(촉수팔이 길어 너무 가로지르지 않게).")]
        [SerializeField] private Vector3 armLowerDir = new(-0.5f, -0.7f, 0.5f);
        [Tooltip("숙이고 펴는 시간(초).")]
        [SerializeField, Min(0.01f)] private float bowIn = 0.15f, bowOut = 0.3f;

        private class Bone { public Transform T; }
        private ITentacleField field;
        private Animator animator;
        private Bone[] bow, arm;
        private float weight;

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            field ??= GetComponent<ITentacleField>();
            if (field == null) return;
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); Resolve(); }
            if (bow == null) return;

            bool slamming = false;
            foreach (var t in field.Tentacles) if (t.State == TentacleState.SlamWindup || t.State == TentacleState.SlamActive) { slamming = true; break; }
            weight = Mathf.MoveTowards(weight, slamming ? 1f : 0f, Time.deltaTime / (slamming ? bowIn : bowOut));
            Apply(Mathf.SmoothStep(0f, 1f, weight));
        }

        /// <summary>에디터 미리보기용: 지금 자세에 인사 자세를 w(0~1)만큼 바로 더한다(뼈를 다시 찾는다).</summary>
        public void PreviewPose(float w)
        {
            Resolve();
            if (bow != null) Apply(Mathf.Clamp01(w));
        }

        /// <summary>w(0~1)만큼 인사 자세를 더한다. 모션이 안 쓰는 뼈는 바탕 자세로 되돌린 뒤 더한다.</summary>
        private void Apply(float w)
        {
            foreach (var b in bow) Rebase(b);
            foreach (var b in arm) Rebase(b);

            // 1) 고개만 오른쪽 앞으로 확실히 숙여 인사(머리 → 정수리 방향을 headDir로 — 손은 움직이지 않는다).
            if (w > 0f) Aim(bow[0], bow[1], headDir, w);

            // 2) 오른 어깨 라인만: 어깨를 살짝 떨구고, 위팔(→ 아래팔)·아래팔(→ 손)이 가리키는 방향을 목표 방향 쪽으로 w만큼 돌린다. 왼쪽은 건드리지 않는다.
            if (w > 0f)
            {
                Aim(arm[0], arm[1], shoulderDir, w * shoulderAmount);
                Aim(arm[1], arm[2], armUpperDir, w);
                Aim(arm[2], arm[3], armLowerDir, w);
            }

            foreach (var b in bow) Mark(b);
            foreach (var b in arm) Mark(b);
        }

        private void Aim(Bone bone, Bone child, Vector3 localDir, float w)
        {
            if (bone == null || bone.T == null || child == null || child.T == null) return;
            Vector3 cur = child.T.position - bone.T.position;
            Vector3 target = transform.TransformDirection(localDir.normalized);
            if (cur.sqrMagnitude < 1e-8f) return;
            Quaternion delta = Quaternion.FromToRotation(cur.normalized, target);
            bone.T.rotation = Quaternion.Slerp(Quaternion.identity, delta, w) * bone.T.rotation;
        }

        // 바탕 자세는 걷기 팔(HasturArmWalk)과 같은 뼈를 쓰므로 공용(SharedBonePose) — 이 프레임 앞 스크립트가 더한 자세 위에 더한다.
        private static void Rebase(Bone b) { if (b != null) SharedBonePose.Begin(b.T); }

        private static void Mark(Bone b) { if (b != null) SharedBonePose.End(b.T); }

        private void Resolve()
        {
            bow = arm = null;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            var all = animator.GetComponentsInChildren<Transform>(true);
            bow = Find(all, BowBoneNames);
            arm = Find(all, ArmBoneNames);
        }

        private static Bone[] Find(Transform[] all, string[] names)
        {
            var result = new Bone[names.Length];
            for (int i = 0; i < names.Length; i++)
                foreach (var t in all) if (t.name == names[i]) { result[i] = new Bone { T = t }; break; }
            return result;
        }

        private void Restore()
        {
            foreach (var set in new[] { bow, arm })
            {
                if (set == null) continue;
                foreach (var b in set) if (b != null) SharedBonePose.Restore(b.T);
            }
            weight = 0f;
        }
    }
}

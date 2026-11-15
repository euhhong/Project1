using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터가 촉수를 내려찍는 동안(TentacleState.SlamWindup·SlamActive) 에스코트하듯 정중하게 인사하는 자세(연출 전용 — Generic 리그라 런타임으로 뼈를 돌린다).
    /// - 움직이는 것은 오른 어깨 라인뿐: 오른 어깨(biped R Clavicle)를 앞·아래로 살짝 떨구고, 오른 촉수팔(R UpperArm·R Forearm)을 젠틀하게 내린다
    ///   (위팔은 아래·살짝 앞으로, 아래팔은 허리 앞에서 안쪽으로 — 집사가 인사하듯). 고개만 살짝 숙인다(biped Head).
    /// - 등·목은 숙이지 않는다(두 어깨가 목에 달려 있어 숙이면 왼팔도 따라 움직이므로). 왼팔·왼손은 모션 그대로 고정.
    ///   뼈 축 방향을 몰라도 되게 "뼈가 가리키는 방향"을 캐릭터 기준 목표 방향으로 돌린다(shoulderDir·armUpperDir·armLowerDir).
    /// 그동안 평타·촉수 세우기는 BE(TentacleField — IActionLock)가 막는다.
    /// 모션이 쓰는 뼈면 모션 자세 위에 더하고, 쓰지 않는 뼈면 쉬는 자세에서 더한다(매 프레임 쌓이지 않음).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 하스터만).
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturBowPose : MonoBehaviour
    {
        private static readonly string[] BowBoneNames = { "biped Head" };
        private static readonly string[] ArmBoneNames = { "biped R Clavicle", "biped R UpperArm", "biped R Forearm", "biped R Hand" };

        [Tooltip("고개를 숙이는 각도(도) — 머리.")]
        [SerializeField] private float[] angles = { 18f };
        [Tooltip("오른 어깨(쇄골 → 위팔)가 가리킬 방향(캐릭터 기준 x = 오른쪽, y = 위, z = 앞) — 앞·아래로 살짝 떨굼 / 그쪽으로 돌리는 정도(0~1).")]
        [SerializeField] private Vector3 shoulderDir = new(1f, -0.35f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float shoulderAmount = 0.6f;
        [Tooltip("오른 위팔이 가리킬 방향(캐릭터 기준) — 아래로 내리고 살짝 앞.")]
        [SerializeField] private Vector3 armUpperDir = new(0.05f, -0.95f, 0.3f);
        [Tooltip("오른 아래팔이 가리킬 방향(캐릭터 기준) — 허리 앞에서 안쪽(왼쪽)·아래로 비스듬히 접는다(촉수팔이 길어 너무 가로지르지 않게).")]
        [SerializeField] private Vector3 armLowerDir = new(-0.5f, -0.7f, 0.5f);
        [Tooltip("숙이고 펴는 시간(초).")]
        [SerializeField, Min(0.01f)] private float bowIn = 0.15f, bowOut = 0.3f;

        private class Bone { public Transform T; public Quaternion Base, Written; public bool Has; }
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

            // 1) 고개만 숙여 인사(머리 — 손은 움직이지 않는다).
            Vector3 right = transform.right;
            for (int i = 0; i < bow.Length; i++)
            {
                var b = bow[i];
                if (b == null || b.T == null || w <= 0f) continue;
                b.T.rotation = Quaternion.AngleAxis(angles[Mathf.Min(i, angles.Length - 1)] * w, right) * b.T.rotation;
            }

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

        private static void Rebase(Bone b)
        {
            if (b == null || b.T == null) return;
            // 지난 프레임에 우리가 쓴 그대로면 모션이 안 쓰는 뼈 → 바탕 자세로 되돌린 뒤 더한다
            if (b.Has && Quaternion.Dot(b.T.localRotation, b.Written) > 0.999999f) b.T.localRotation = b.Base;
            else b.Base = b.T.localRotation;
        }

        private static void Mark(Bone b)
        {
            if (b == null || b.T == null) return;
            b.Written = b.T.localRotation; b.Has = true;
        }

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
                foreach (var b in set) if (b != null && b.T != null && b.Has) { b.T.localRotation = b.Base; b.Has = false; }
            }
            weight = 0f;
        }
    }
}

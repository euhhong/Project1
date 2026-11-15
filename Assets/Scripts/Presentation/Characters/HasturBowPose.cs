using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터가 촉수를 내려찍는 동안(TentacleState.SlamWindup·SlamActive) 허리를 숙여 인사하는 자세(연출 전용 — Generic 리그라 런타임으로 뼈를 돌린다).
    /// 등뼈(biped Spine·Spine1)와 목(biped Neck)을 몸 앞쪽으로 숙인다. 그동안 평타·촉수 세우기는 BE(TentacleField — IActionLock)가 막는다.
    /// 모션이 쓰는 뼈면 모션 자세 위에 더하고, 쓰지 않는 뼈면 쉬는 자세에서 더한다(매 프레임 쌓이지 않음).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 하스터만).
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturBowPose : MonoBehaviour
    {
        private static readonly string[] BoneNames = { "biped Spine", "biped Spine1", "biped Neck" };

        [Tooltip("숙이는 각도(도) — 등뼈 아래 / 위 / 목.")]
        [SerializeField] private float[] angles = { 22f, 20f, 12f };
        [Tooltip("숙이고 펴는 시간(초).")]
        [SerializeField, Min(0.01f)] private float bowIn = 0.15f, bowOut = 0.3f;

        private class Bone { public Transform T; public Quaternion Base, Written; public bool Has; }
        private ITentacleField field;
        private Animator animator;
        private Bone[] bones;
        private float weight;

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            field ??= GetComponent<ITentacleField>();
            if (field == null) return;
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); Resolve(); }
            if (bones == null) return;

            bool slamming = false;
            foreach (var t in field.Tentacles) if (t.State == TentacleState.SlamWindup || t.State == TentacleState.SlamActive) { slamming = true; break; }
            weight = Mathf.MoveTowards(weight, slamming ? 1f : 0f, Time.deltaTime / (slamming ? bowIn : bowOut));

            Vector3 right = transform.right;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b.T == null) continue;
                // 지난 프레임에 우리가 쓴 그대로면 모션이 안 쓰는 뼈 → 바탕 자세로 되돌린 뒤 더한다
                if (b.Has && Quaternion.Dot(b.T.localRotation, b.Written) > 0.999999f) b.T.localRotation = b.Base;
                else b.Base = b.T.localRotation;
                if (weight > 0f) b.T.rotation = Quaternion.AngleAxis(angles[Mathf.Min(i, angles.Length - 1)] * Mathf.SmoothStep(0f, 1f, weight), right) * b.T.rotation; // 몸 앞쪽으로 숙임
                b.Written = b.T.localRotation; b.Has = true;
            }
        }

        private void Resolve()
        {
            bones = null;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            var all = animator.GetComponentsInChildren<Transform>(true);
            bones = new Bone[BoneNames.Length];
            for (int i = 0; i < BoneNames.Length; i++)
                foreach (var t in all) if (t.name == BoneNames[i]) { bones[i] = new Bone { T = t }; break; }
        }

        private void Restore()
        {
            if (bones == null) return;
            foreach (var b in bones) if (b != null && b.T != null && b.Has) { b.T.localRotation = b.Base; b.Has = false; }
            weight = 0f;
        }
    }
}

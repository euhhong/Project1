using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터 두 촉수 팔 끝(손톱)에 때리는 판정을 붙인다. 하스터 모델은 Generic 리그라 모델을 들여올 때 판정이 자동으로 생기지 않는다
    /// (BodyPartColliderPostprocessor는 Humanoid만). 실제로 내려치는 부분은 소매(biped Hand)가 아니라 아래팔에 붙어 땅까지 닿는 촉수 팔이다
    /// (왼쪽 biped_bone_00 → 01 → 02 → 03 → 끝, 오른쪽 biped_bone_27 → 26 → 24 → 23 → 끝 — HasturArmWalk·HasturSlamMotionBuilder와 같은 사슬).
    /// 둘째 마디부터 끝까지 잇는 상자(Hitbox_Claw, 왼손·오른손 자리 — CharacterLoadout이 하스터 공격 자리를 두 손으로 정함)를 둘째 마디 아래에 둬서
    /// 공격 모션(두 손 모아 머리 위 → 아래로 내려치기)을 따라 움직인다. 판정은 AttackHitDetector가 공격 판정 구간에만 한다.
    /// HasturLegWriggle이 붙인다(하스터만) — 하스터가 아니게 되면 상자를 지우고 없어진다. 모델이 바뀌면 다시 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturClawHitboxes : MonoBehaviour
    {
        private static readonly (string from, HitboxSlot slot)[] Claws =
        {
            ("biped_bone_01", HitboxSlot.LeftHand),
            ("biped_bone_26", HitboxSlot.RightHand),
        };

        [Tooltip("상자 굵기(m, 월드).")]
        [SerializeField, Min(0.05f)] private float thickness = 0.5f;
        [Tooltip("손톱 끝 너머로 더 늘리는 길이(m, 월드).")]
        [SerializeField, Min(0f)] private float tipExtra = 0.15f;

        private readonly List<GameObject> boxes = new();
        private Animator builtFor;

        private void LateUpdate()
        {
            if (!TryGetComponent(out HasturLegWriggle _)) { Destroy(this); return; } // 하스터가 아니게 됨(OnDestroy가 상자를 지움)
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || animator == builtFor) return;
            Clear();
            builtFor = animator;
            var all = animator.GetComponentsInChildren<Transform>(true);
            foreach (var (from, slot) in Claws)
            {
                Transform a = null;
                foreach (var t in all) if (t.name == from) { a = t; break; }
                if (a == null) continue; // 하스터 모델이 아님
                Transform tip = a;
                while (tip.childCount > 0) tip = tip.GetChild(0);
                Vector3 d = tip.position - a.position;
                if (d.sqrMagnitude < 1e-6f) continue;
                float len = d.magnitude + tipExtra;
                var go = new GameObject("Hitbox_Claw");
                go.transform.SetParent(a, false);
                go.transform.SetPositionAndRotation(a.position + d.normalized * (len * 0.5f), Quaternion.LookRotation(d, transform.up));
                var col = go.AddComponent<BoxCollider>();
                Vector3 s = go.transform.lossyScale;
                col.size = new Vector3(thickness / Mathf.Max(s.x, 1e-4f), thickness / Mathf.Max(s.y, 1e-4f), len / Mathf.Max(s.z, 1e-4f));
                go.AddComponent<Hitbox>().SetSlot(slot);
                boxes.Add(go);
            }
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void OnDestroy()
        {
            Clear();
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void Clear()
        {
            foreach (var b in boxes) if (b != null) { b.SetActive(false); Destroy(b); }
            boxes.Clear();
            builtFor = null;
        }
    }
}

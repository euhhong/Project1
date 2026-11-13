using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 손 뼈보다 길게 뻗은 손톱·팔(사람 뼈가 아닌 추가 뼈)에 때리는 판정을 붙인다. 예: 하스터 — 손 뼈(biped L/R Hand)는 팔 가운데쯤이고
    /// 실제 손톱은 아래팔에서 갈라진 다른 뼈 사슬(왼쪽 biped_bone_01 → nub_25, 오른쪽 biped_bone_26 → nub_24) 끝까지 간다.
    /// 사슬 시작 뼈 ~ 끝 뼈를 잇는 상자(Hitbox_Claw, 왼손·오른손 자리)를 시작 뼈 아래에 둬서 모션을 따라 움직인다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout). 모델이 바뀌면 다시 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClawHitboxes : MonoBehaviour
    {
        [System.Serializable]
        public struct Claw { public string from, to; public HitboxSlot slot; }

        [SerializeField] private Claw[] claws =
        {
            new() { from = "biped_bone_01", to = "nub_25", slot = HitboxSlot.LeftHand },
            new() { from = "biped_bone_26", to = "nub_24", slot = HitboxSlot.RightHand },
        };
        [Tooltip("상자 굵기(m, 월드).")]
        [SerializeField, Min(0.05f)] private float thickness = 0.55f;
        [Tooltip("끝 뼈 너머로 더 늘리는 길이(m, 월드).")]
        [SerializeField, Min(0f)] private float tipExtra = 0.2f;

        private readonly List<GameObject> boxes = new();
        private Animator builtFor;

        private void LateUpdate()
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator == null || animator == builtFor) return;
            Clear();
            builtFor = animator;
            var all = animator.GetComponentsInChildren<Transform>(true);
            foreach (var c in claws)
            {
                Transform a = Find(all, c.from), b = Find(all, c.to);
                if (a == null || b == null) continue;
                var go = new GameObject("Hitbox_Claw");
                go.transform.SetParent(a, false);
                Vector3 d = b.position - a.position;
                float len = d.magnitude + tipExtra;
                go.transform.SetPositionAndRotation(a.position + d.normalized * (len * 0.5f), Quaternion.LookRotation(d, transform.up));
                var col = go.AddComponent<BoxCollider>();
                Vector3 s = go.transform.lossyScale;
                col.size = new Vector3(thickness / Mathf.Max(s.x, 1e-4f), thickness / Mathf.Max(s.y, 1e-4f), len / Mathf.Max(s.z, 1e-4f));
                go.AddComponent<Hitbox>().SetSlot(c.slot);
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
            foreach (var b in boxes) if (b != null) Destroy(b);
            boxes.Clear();
            builtFor = null;
        }

        private static Transform Find(Transform[] all, string name)
        {
            foreach (var t in all) if (t.name == name) return t;
            return null;
        }
    }
}

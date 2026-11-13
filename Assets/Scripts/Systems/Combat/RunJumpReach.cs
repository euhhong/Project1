using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 감시자 달리기 점프 공격의 긴 판정: 몸 앞쪽으로 길게 뻗은 판정 상자(Hitbox_RunJumpReach, RunJumpReach 자리)를 둔다.
    /// 달리기 점프 공격(무기 내려치기, Run Jump Attack)의 판정 구간에만 켜진다(CharacterAttack.SetRunJumpSlots).
    /// 모션은 제자리로 재생돼(앞으로 날아드는 이동은 버림) 무기가 몸 가까이에만 내려오므로, 앞으로 날아드는 만큼 멀리까지 맞게 한다.
    /// 높이는 몸(루트) 기준 — 공중에서 내려치면 몸과 함께 올라가고, 아래(-)까지 덮어 땅에 선 상대의 몸·머리에 닿는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 감시자만).
    /// </summary>
    [DisallowMultipleComponent]
    public class RunJumpReach : MonoBehaviour
    {
        [Tooltip("몸 앞으로 닿는 거리(m).")]
        [SerializeField, Min(0.2f)] private float reach = 3.6f;
        [Tooltip("판정 상자가 시작되는 거리(몸 바로 앞, m).")]
        [SerializeField, Min(0f)] private float start = 0.2f;
        [Tooltip("옆으로 넓이(m).")]
        [SerializeField, Min(0.1f)] private float width = 1.4f;
        [Tooltip("판정 상자 아래·위 높이(몸 루트 기준, m). 아래를 -로 두어 공중에서 내려칠 때 땅에 선 상대까지 덮는다.")]
        [SerializeField] private float bottom = -0.6f, top = 2.2f;

        private GameObject box;

        private void Start()
        {
            box = new GameObject("Hitbox_RunJumpReach");
            box.transform.SetParent(transform, false);
            var col = box.AddComponent<BoxCollider>();
            float length = Mathf.Max(reach - start, 0.1f);
            float h = Mathf.Max(top - bottom, 0.1f);
            col.center = new Vector3(0f, bottom + h * 0.5f, start + length * 0.5f);
            col.size = new Vector3(width, h, length);
            box.AddComponent<Hitbox>().SetSlot(HitboxSlot.RunJumpReach);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void OnDestroy()
        {
            if (box != null) Destroy(box);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }
    }
}

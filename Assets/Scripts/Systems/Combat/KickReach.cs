using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 생존자 발차기의 긴 판정: 몸 앞쪽으로 길게 뻗은 판정 상자(Hitbox_KickReach, 왼발 자리)를 둔다.
    /// 왼발 판정을 쓰는 공격(강공격 머리 노리는 돌려차기, 달리기 점프 공격, 두 번째 날아 차기)의 판정 구간에 같이 켜져서,
    /// 발이 실제로 닿는 곳보다 훨씬 멀리까지 맞는다. 높이는 몸(루트) 기준 — 서서 찰 때는 머리 높이, 날아 찰 때는 몸과 함께 올라간다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 생존자만).
    /// </summary>
    [DisallowMultipleComponent]
    public class KickReach : MonoBehaviour
    {
        [Tooltip("몸 앞으로 닿는 거리(m).")]
        [SerializeField, Min(0.2f)] private float reach = 3.6f;
        [Tooltip("판정 상자가 시작되는 거리(몸 바로 앞, m).")]
        [SerializeField, Min(0f)] private float start = 0.2f;
        [Tooltip("옆으로 넓이(m).")]
        [SerializeField, Min(0.1f)] private float width = 1.2f;
        [Tooltip("판정 상자 아래·위 높이(몸 루트 기준, m). 서서 차면 머리 높이를 덮고, 날아 차면 발 높이를 덮는다.")]
        [SerializeField] private float bottom = 0.6f, top = 1.9f;

        private GameObject box;

        private void Start()
        {
            box = new GameObject("Hitbox_KickReach");
            box.transform.SetParent(transform, false);
            var col = box.AddComponent<BoxCollider>();
            float length = Mathf.Max(reach - start, 0.1f);
            float h = Mathf.Max(top - bottom, 0.1f);
            col.center = new Vector3(0f, bottom + h * 0.5f, start + length * 0.5f);
            col.size = new Vector3(width, h, length);
            var hb = box.AddComponent<Hitbox>();
            hb.SetSlot(HitboxSlot.LeftFoot);
            hb.SetPrefersHead(true); // 머리를 노리는 발차기: 머리가 판정 안에 있으면 머리를 맞힌다
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void OnDestroy()
        {
            if (box != null) Destroy(box);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }
    }
}

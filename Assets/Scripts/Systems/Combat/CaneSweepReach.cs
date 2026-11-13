using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맹인 지팡이 휘두르기의 넓은 판정: 몸 앞쪽에 옆으로 아주 넓은 판정 상자(Hitbox_CaneSweep, 무기 자리)를 둔다.
    /// 무기(지팡이) 판정을 쓰는 공격(약공격·강공격·좌우로 그어 후리기)의 판정 구간에 같이 켜져서, 지팡이가 실제로 지나간 곳보다 훨씬 넓게 맞는다.
    /// E 찌르기는 따로(CaneThrustSkill). 캐릭터 루트에 붙인다(CharacterLoadout, 맹인만).
    /// </summary>
    [DisallowMultipleComponent]
    public class CaneSweepReach : MonoBehaviour
    {
        [Tooltip("옆으로 넓이(m).")]
        [SerializeField, Min(0.2f)] private float width = 3.6f;
        [Tooltip("몸 앞으로 닿는 거리(m).")]
        [SerializeField, Min(0.2f)] private float reach = 2.6f;
        [Tooltip("판정 상자가 시작되는 거리(몸 바로 앞, m).")]
        [SerializeField, Min(0f)] private float start = 0.1f;
        [SerializeField, Min(0.1f)] private float height = 1.6f;
        [Tooltip("판정 상자 높이(캐릭터 키 대비).")]
        [SerializeField, Range(0.1f, 1f)] private float heightRatio = 0.5f;

        private GameObject box;

        private void Start()
        {
            var cc = GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            box = new GameObject("Hitbox_CaneSweep");
            box.transform.SetParent(transform, false);
            var col = box.AddComponent<BoxCollider>();
            float length = Mathf.Max(reach - start, 0.1f);
            col.center = new Vector3(0f, h * heightRatio, start + length * 0.5f);
            col.size = new Vector3(width, height, length);
            box.AddComponent<Hitbox>().SetSlot(HitboxSlot.Weapon);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }

        private void OnDestroy()
        {
            if (box != null) Destroy(box);
            if (TryGetComponent(out AttackHitDetector detector)) detector.RefreshHitboxes();
        }
    }
}

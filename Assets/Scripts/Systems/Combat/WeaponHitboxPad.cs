using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 무기 판정(Hitbox_Weapon) 상자를 키운다(예: 재앙의 도마뱀 마체테 — 칼날이 얇아 빠른 가로 베기가 틱 사이에 상대를 지나쳐 빗나가던 것을 줄임).
    /// 날 두께·폭(상자 x·y)을 thicknessScale배, 길이(z)를 lengthScale배. 모델을 바꾸면(새 판정 상자) 다시 키운다. 원래 크기는 기억해 두고 한 번만 키운다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 재앙의 도마뱀만).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponHitboxPad : MonoBehaviour
    {
        [Tooltip("날 두께·폭 배율(휘두르는 방향으로 두꺼워져 틱 사이 빈틈을 메움) / 길이 배율.")]
        [SerializeField, Min(1f)] private float thicknessScale = 3f, lengthScale = 1.25f;

        private BoxCollider padded;

        private void LateUpdate()
        {
            if (padded != null) return;
            foreach (var h in GetComponentsInChildren<Hitbox>(true))
            {
                if ((h.Slot & HitboxSlot.Weapon) == 0 || !h.TryGetComponent(out BoxCollider box)) continue;
                Vector3 s = box.size;
                box.size = new Vector3(s.x * thicknessScale, s.y * thicknessScale, s.z * lengthScale);
                padded = box;
                return;
            }
        }
    }
}

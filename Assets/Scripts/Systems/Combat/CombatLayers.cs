using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 판정용 레이어 이름과 마스크. 두 레이어는 충돌 매트릭스에서 모든 레이어와 꺼져 있어
    /// 물리 충돌·트리거를 일으키지 않고, Hitbox의 겹침 검사로만 쓰인다.
    /// </summary>
    public static class CombatLayers
    {
        public const string HitboxName = "Hitbox";
        public const string HurtboxName = "Hurtbox";

        public static int Hitbox => LayerMask.NameToLayer(HitboxName);
        public static int Hurtbox => LayerMask.NameToLayer(HurtboxName);
        public static int HurtboxMask => LayerMask.GetMask(HurtboxName);

        /// <summary>오브젝트를 판정 레이어로 옮기고 트리거로 만든다. 레이어가 없으면 false.</summary>
        public static bool Apply(Collider collider, int layer)
        {
            if (layer < 0)
            {
                Debug.LogError($"판정 레이어가 없습니다. Project Settings > Tags and Layers에 '{HitboxName}', '{HurtboxName}'를 추가하세요.", collider);
                return false;
            }
            collider.gameObject.layer = layer;
            collider.isTrigger = true;
            return true;
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 때리는 판정의 모양. 손·발·소품 뼈의 Collider(Sphere·Capsule·Box)와 같은 오브젝트에 붙인다.
    /// 스스로 판정하지 않고, 캐릭터 루트의 AttackHitDetector가 공격 중에만 Overlap을 요청한다.
    /// 실행 시 자동으로 Hitbox 레이어 + 트리거가 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] private HitboxSlot slot = HitboxSlot.RightHand;

        public HitboxSlot Slot => slot;
        public Collider Collider { get; private set; }

        /// <summary>자동 배치 도구용: 자리를 코드로 정한다.</summary>
        public void SetSlot(HitboxSlot value)
        {
            slot = value;
        }

        private void Awake()
        {
            Collider = GetComponent<Collider>();
            CombatLayers.Apply(Collider, CombatLayers.Hitbox);
        }

        /// <summary>현재 모양과 겹치는 Collider를 results에 채우고 개수를 돌려준다.</summary>
        /// <param name="offset">모양을 이만큼(월드) 옮겨 검사한다(예: 앞으로 달리며 공격할 때 이동할 자리까지).</param>
        public int Overlap(Collider[] results, int layerMask, Vector3 offset = default)
        {
            const QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;
            Transform t = Collider.transform;
            Vector3 scale = Abs(t.lossyScale);

            switch (Collider)
            {
                case SphereCollider sphere:
                {
                    Vector3 center = t.TransformPoint(sphere.center) + offset;
                    float radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                    return Physics.OverlapSphereNonAlloc(center, radius, results, layerMask, triggers);
                }
                case CapsuleCollider capsule:
                {
                    Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                    float axisScale = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
                    float radiusScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z)
                                      : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z)
                                      : Mathf.Max(scale.x, scale.y);
                    float radius = capsule.radius * radiusScale;
                    float half = Mathf.Max(capsule.height * axisScale * 0.5f - radius, 0f);
                    Vector3 center = t.TransformPoint(capsule.center) + offset;
                    Vector3 dir = t.TransformDirection(axis).normalized;
                    return Physics.OverlapCapsuleNonAlloc(center - dir * half, center + dir * half, radius, results, layerMask, triggers);
                }
                case BoxCollider box:
                {
                    Vector3 center = t.TransformPoint(box.center) + offset;
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, scale);
                    return Physics.OverlapBoxNonAlloc(center, halfExtents, results, t.rotation, layerMask, triggers);
                }
                default:
                {
                    Bounds b = Collider.bounds;
                    return Physics.OverlapBoxNonAlloc(b.center + offset, b.extents, results, Quaternion.identity, layerMask, triggers);
                }
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }
    }
}

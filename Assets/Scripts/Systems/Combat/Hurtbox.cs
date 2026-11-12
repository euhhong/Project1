using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맞는 판정. 부위 뼈의 Collider(Sphere·Capsule·Box)와 같은 오브젝트에 붙인다.
    /// 어느 캐릭터(부모의 CharacterIdentity)의 어느 부위인지 알려 준다.
    /// 실행 시 자동으로 Hurtbox 레이어 + 트리거가 된다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class Hurtbox : MonoBehaviour
    {
        [SerializeField] private BodyPart part = BodyPart.Body;

        private CharacterIdentity owner;

        public BodyPart Part => part;
        public Collider Collider { get; private set; }

        /// <summary>이 부위의 주인. 모델이 교체돼도 맞도록 실행 중에 부모에서 찾는다.</summary>
        public CharacterIdentity Owner
        {
            get
            {
                if (owner == null)
                {
                    owner = GetComponentInParent<CharacterIdentity>();
                }
                return owner;
            }
        }

        /// <summary>자동 배치 도구용: 부위를 코드로 정한다.</summary>
        public void SetPart(BodyPart value)
        {
            part = value;
        }

        private void Awake()
        {
            Collider = GetComponent<Collider>();
            CombatLayers.Apply(Collider, CombatLayers.Hurtbox);
        }

        private void OnTransformParentChanged()
        {
            owner = null;
        }
    }
}

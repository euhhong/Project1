using Project1.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project1.Presentation
{
    /// <summary>
    /// 개발용: F4를 누르면 플레이어 체력을 25 깎는다(회복 스킬 시험용). 에디터·개발 빌드에서만 붙인다.
    /// 아직 데미지 규칙(M3-5)이 없어서 맞아도 체력이 줄지 않기 때문이다.
    /// </summary>
    public class HealthDebugKeys : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 25f;

        private CharacterHealth health;

        private void Awake() => health = GetComponent<CharacterHealth>();

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f4Key.wasPressedThisFrame && health != null) health.Damage(damage);
        }
    }
}

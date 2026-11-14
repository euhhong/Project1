using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 계단(가파른 오르막)을 앞으로 오를 때 Animator "Stairs" 레이어(전신, 이동 바로 위)에 계단 오르기 모션(Stairs_Up — Running Up Stairs)을 켠다.
    /// - 땅에 있고, 앞으로 움직이고, 앞으로 가는 만큼 몸이 충분히 올라가면(오르는 속도 / 앞 속도 ≥ enterSlope) 켠다. 평지·내리막이 되면 끈다.
    /// - 모션 빠르기는 앞으로 가는 속도에 맞춘다(걸어서 오르면 느리게).
    /// 연출 전용(게임 상태는 바꾸지 않는다). 공격·스킬·오르기·피격 레이어가 더 위에 있어 그 모션들이 덮는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout). Animator는 자식 모델에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterStairsAnimator : MonoBehaviour
    {
        private const string LayerName = "Stairs";
        private static readonly int SpeedId = Animator.StringToHash("StairsSpeed");

        [Tooltip("앞으로 이 속도(m/s) 넘게 움직여야 계단 모션을 켠다.")]
        [SerializeField, Min(0f)] private float minForwardSpeed = 1f;
        [Tooltip("오르는 속도 / 앞 속도가 이 값 이상이면 켠다(계단 기울기).")]
        [SerializeField, Min(0f)] private float enterSlope = 0.3f;
        [Tooltip("이 값 아래로 내려가면 끈다(켜고 끄기가 깜빡이지 않게 enterSlope보다 작게).")]
        [SerializeField, Min(0f)] private float exitSlope = 0.12f;
        [Tooltip("오르는 속도를 부드럽게 하는 시간(초) — 계단 한 칸마다 툭툭 오르는 것을 고르게.")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;
        [Tooltip("모션 원래 빠르기에 맞는 앞 속도(m/s) — Running Up Stairs는 달리기.")]
        [SerializeField, Min(0.1f)] private float clipSpeed = 6f;
        [SerializeField, Min(0.01f)] private float fade = 0.15f;

        private ICharacterState state;
        private Animator animator;
        private int layer = -1;
        private float lastY, rise, weight;
        private bool hasLast, onStairs;

        private void Awake() => state = GetComponent<ICharacterState>();

        private void FixedUpdate()
        {
            // 오르는 속도는 물리 틱마다 잰다(이동이 FixedUpdate에서 일어난다).
            float y = transform.position.y;
            float dt = Time.fixedDeltaTime;
            float v = hasLast ? (y - lastY) / dt : 0f;
            lastY = y; hasLast = true;
            rise = Mathf.Lerp(rise, v, 1f - Mathf.Exp(-dt / smoothTime));
        }

        private bool Resolve()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            return layer >= 0;
        }

        private void LateUpdate()
        {
            if (state == null) state = GetComponent<ICharacterState>();
            if (state == null || !Resolve()) return;
            float forward = state.LocalPlanarVelocity.y;
            bool climbing = state.IsGrounded && forward > minForwardSpeed;
            float slope = forward > 0.01f ? rise / forward : 0f;
            onStairs = climbing && slope >= (onStairs ? exitSlope : enterSlope);
            weight = Mathf.MoveTowards(weight, onStairs ? 1f : 0f, Time.deltaTime / fade);
            animator.SetLayerWeight(layer, weight);
            if (onStairs) animator.SetFloat(SpeedId, Mathf.Clamp(forward / clipSpeed, 0.5f, 1.4f));
        }

        private void OnDisable()
        {
            weight = 0f; onStairs = false; hasLast = false; rise = 0f;
            if (animator != null && layer >= 0) animator.SetLayerWeight(layer, 0f);
        }
    }
}

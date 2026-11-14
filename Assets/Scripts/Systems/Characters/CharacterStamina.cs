using System;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 스태미나. 달리는 동안(달리기 키 + 방향키 + 실제로 달리는 중) 초당 drainPerSecond씩 줄고,
    /// 달리지 않을 때는(멈춤·걷기·공중·점프 포함) 초당 regenPerSecond씩 계속 회복한다.
    /// 0이 되면 지쳐서 recoverThreshold까지 회복할 때까지 걷기 속도로 제한한다(IMoveSpeedLimit).
    /// 스킬이 이동을 정하는 동안(돌진 등)은 달리기 키를 눌러도 줄지 않는다(달리기 키 입력이 아니므로).
    /// 달리기 말고도 스태미나를 쓰는 동작(IStaminaDrain — 사다리 타기)이 있으면 그동안 회복하지 않고 그만큼 줄어든다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterStamina : MonoBehaviour, ICharacterStamina, IMoveSpeedLimit
    {
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [Tooltip("달리는 동안 초당 줄어드는 양. 100 / 20 = 가득 찬 상태에서 5초 달릴 수 있다.")]
        [SerializeField, Min(0f)] private float drainPerSecond = 20f;
        [Tooltip("달리지 않을 때 초당 회복하는 양. 100 / 12.5 = 0에서 8초면 가득 찬다.")]
        [SerializeField, Min(0f)] private float regenPerSecond = 12.5f;
        [Tooltip("다 써서 지쳤을 때, 이만큼 회복해야 다시 달릴 수 있다(달리기가 켜졌다 꺼졌다 깜빡이지 않게).")]
        [SerializeField, Min(0f)] private float recoverThreshold = 25f;

        private ICharacterState state;
        private ICharacterInputSource input;
        private ICharacterHealth health;
        private ICharacterSkillState skill;
        private IStaminaDrain[] drains;
        private float current = -1f;

        public float Current => current < 0f ? maxStamina : current;
        public float Max => maxStamina;
        public bool IsExhausted { get; private set; }

        public event Action<float, float> Changed;

        /// <summary>진영별 달리기 소모량(초당): 감시자 20(가득 차면 5초), 생존자 14(약 7초) — 생존자가 더 오래 달린다.</summary>
        public static float DrainFor(bool hunter) => hunter ? 20f : 14f;

        /// <summary>달리는 동안 초당 소모량을 바꾼다(진영별 — CharacterLoadout).</summary>
        public void SetDrainPerSecond(float perSecond) => drainPerSecond = Mathf.Max(0f, perSecond);

        /// <summary>가득 채운다(라운드 시작·캐릭터 교체 등).</summary>
        public void Refill()
        {
            current = maxStamina;
            IsExhausted = false;
            Changed?.Invoke(current, maxStamina);
        }

        private void Awake()
        {
            if (current < 0f) current = maxStamina;
        }

        public bool TryGetSpeedLimit(out float maxSpeed)
        {
            state ??= GetComponent<ICharacterState>();
            maxSpeed = state != null ? state.WalkSpeed : 0f;
            return IsExhausted && state != null;
        }

        private void FixedUpdate()
        {
            state ??= GetComponent<ICharacterState>();
            input ??= GetComponent<ICharacterInputSource>();
            if (health == null) TryGetComponent(out health);
            if (skill == null) TryGetComponent(out skill);
            if (state == null) return;

            bool dead = health != null && health.IsDead;
            bool skillMoving = skill != null && skill.State == SkillState.Casting; // 스킬 이동(돌진 등)은 달리기가 아니다
            CharacterInputFrame frame = input != null ? input.ReadInput() : default;
            bool running = !dead && !skillMoving && frame.Run && frame.Move.sqrMagnitude > 0.0001f
                           && state.MoveState == CharacterMoveState.Run;

            // 달리기 말고 스태미나를 쓰는 동작(사다리 타기 등 — IStaminaDrain): 그동안 회복하지 않는다.
            drains ??= GetComponents<IStaminaDrain>();
            float extra = 0f;
            foreach (var d in drains) if (!(d is Behaviour { isActiveAndEnabled: false })) extra += Mathf.Max(0f, d.StaminaDrainPerSecond);

            float before = current;
            float dt = Time.fixedDeltaTime;
            if (running || extra > 0f)
            {
                current = Mathf.Max(0f, current - ((running ? drainPerSecond : 0f) + extra) * dt);
                if (current <= 0f) IsExhausted = true;
            }
            else
            {
                current = Mathf.Min(maxStamina, current + regenPerSecond * dt);
            }
            if (IsExhausted && current >= Mathf.Min(recoverThreshold, maxStamina)) IsExhausted = false;

            if (!Mathf.Approximately(before, current)) Changed?.Invoke(current, maxStamina);
        }
    }
}

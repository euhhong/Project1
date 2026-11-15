using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 손전등 빛에 지져진 정도(BE, 빛을 받는 쪽). 도둑 손전등(FlashlightSkill)이 처음 비출 때 상대에게 붙인다.
    /// - Exposure(머리 위 아래 칸): 어디로 받든 빛을 받는 동안 Shine이 채우고, 받지 않으면 decayRate로 계속 떨어진다.
    ///   다 차면 E가 봉인되고, 봉인은 게이지가 0까지 다 떨어질 때까지 간다.
    ///   한 번 다 찬(봉인된) 뒤에는 빛이 끊기고 sealedDecayDelay(2초)가 지나야 떨어지기 시작한다.
    /// - FrontExposure(위 칸): 정면으로 받을 때만 차는 따로 된 게이지(받지 않거나 등·옆으로 받으면 떨어진다).
    ///   다 차면 stunTime초 기절 + 피해(손전등이 정한 양)를 받고 0으로. 그 순간 Blinded를 알린다(FE: 내 화면이 하얗게 번쩍).
    /// </summary>
    [DisallowMultipleComponent]
    public class LightExposure : MonoBehaviour, ILightExposure
    {
        private static readonly List<LightExposure> all = new();
        /// <summary>지금 있는 모든 게이지(FE가 머리 위에 그린다).</summary>
        public static IReadOnlyList<LightExposure> All => all;

        /// <summary>누군가 정면 게이지가 다 차 기절했을 때(그 캐릭터, 기절 시간). FE가 화면을 번쩍이게 한다.</summary>
        public static event System.Action<LightExposure, float> Blinded;

        [Tooltip("빛을 받지 않을 때 1초에 떨어지는 양(게이지 전체 = 1) — 지져짐 / 정면.")]
        [SerializeField, Min(0f)] private float decayRate = 0.3f, frontDecayRate = 0.35f;
        [Tooltip("지져짐 게이지가 한 번 다 찬(E 봉인) 뒤: 빛이 끊기고 이 시간(초)이 지나야 떨어지기 시작한다.")]
        [SerializeField, Min(0f)] private float sealedDecayDelay = 2f;
        [Tooltip("정면 게이지가 다 찼을 때 기절 시간(초).")]
        [SerializeField, Min(0f)] private float stunTime = 1.5f;

        private int litTick = int.MinValue, frontTick = int.MinValue;
        private bool sealedSkill; // 게이지가 다 찬 뒤 0까지 떨어질 때까지 E 봉인
        private float unlitTime; // 빛이 끊긴 뒤 지난 시간(초)

        public float Exposure { get; private set; }
        public float FrontExposure { get; private set; }
        public bool Lit => SimulationTick.Current - litTick <= 1;
        public bool LitFront => SimulationTick.Current - frontTick <= 1;
        public bool SkillLocked => isActiveAndEnabled && sealedSkill;
        public string LockReason => "손전등 봉인";

        private void OnEnable() => all.Add(this);
        private void OnDisable() { all.Remove(this); Exposure = 0f; FrontExposure = 0f; sealedSkill = false; }

        /// <summary>이번 틱 빛을 받는다. front = 정면으로 받음. fill·frontFill = 1초에 차는 양. stunDamage = 정면 게이지가 다 차 기절할 때 받는 피해.</summary>
        public void Shine(float dt, bool front, float fill, float frontFill, float stunDamage = 0f)
        {
            int tick = SimulationTick.Current;
            if (litTick == tick) return; // 한 틱에 한 번(손전등 여러 개여도)
            litTick = tick;
            Exposure = Mathf.Min(1f, Exposure + fill * dt);
            if (Exposure >= 1f) sealedSkill = true;
            if (!front) return;
            frontTick = tick;
            FrontExposure += frontFill * dt;
            if (FrontExposure >= 1f)
            {
                FrontExposure = 0f;
                var stun = GetComponent<CharacterStun>();
                if (stun == null) stun = gameObject.AddComponent<CharacterStun>();
                stun.Stun(stunTime);
                if (stunDamage > 0f && TryGetComponent(out CharacterHealth health) && !health.IsDead) health.Damage(stunDamage);
                Blinded?.Invoke(this, stunTime);
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            unlitTime = Lit ? 0f : unlitTime + dt;
            bool hold = sealedSkill && unlitTime < sealedDecayDelay; // 다 찬 뒤엔 빛이 끊기고 잠깐 그대로
            if (!Lit && !hold) Exposure = Mathf.Max(0f, Exposure - decayRate * dt);
            if (Exposure <= 0f) sealedSkill = false;
            if (!LitFront) FrontExposure = Mathf.Max(0f, FrontExposure - frontDecayRate * dt);
        }
    }
}

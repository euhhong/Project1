using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 손전등 빛에 지져진 정도(BE, 빛을 받는 쪽). 도둑 손전등(FlashlightSkill)이 처음 비출 때 상대에게 붙인다.
    /// - Exposure: 빛을 받는 동안 Shine이 채우고, 받지 않으면 decayRate로 계속 떨어진다. lockThreshold 이상이면 E가 잠긴다.
    /// - FrontExposure: 정면으로 받을 때만 차는 따로 된 게이지(받지 않거나 등·옆으로 받으면 떨어진다). 다 차면 stunTime초 기절하고 0으로.
    /// 체력은 다루지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LightExposure : MonoBehaviour, ILightExposure
    {
        private static readonly List<LightExposure> all = new();
        /// <summary>지금 있는 모든 게이지(FE가 머리 위에 그린다).</summary>
        public static IReadOnlyList<LightExposure> All => all;

        [Tooltip("이 이상 지져지면 E가 잠긴다(0~1).")]
        [SerializeField, Range(0.05f, 1f)] private float lockThreshold = 0.6f;
        [Tooltip("빛을 받지 않을 때 1초에 떨어지는 양(게이지 전체 = 1) — 지져짐 / 정면.")]
        [SerializeField, Min(0f)] private float decayRate = 0.3f, frontDecayRate = 0.35f;
        [Tooltip("정면 게이지가 다 찼을 때 기절 시간(초).")]
        [SerializeField, Min(0f)] private float stunTime = 1.5f;

        private int litTick = int.MinValue, frontTick = int.MinValue;

        public float Exposure { get; private set; }
        public float LockThreshold => lockThreshold;
        public float FrontExposure { get; private set; }
        public bool Lit => SimulationTick.Current - litTick <= 1;
        public bool LitFront => SimulationTick.Current - frontTick <= 1;
        public bool SkillLocked => isActiveAndEnabled && Exposure >= lockThreshold;
        public string LockReason => "손전등에 지져짐";

        private void OnEnable() => all.Add(this);
        private void OnDisable() { all.Remove(this); Exposure = 0f; FrontExposure = 0f; }

        /// <summary>이번 틱 빛을 받는다. front = 정면으로 받음. fill·frontFill = 1초에 차는 양.</summary>
        public void Shine(float dt, bool front, float fill, float frontFill)
        {
            int tick = SimulationTick.Current;
            if (litTick == tick) return; // 한 틱에 한 번(손전등 여러 개여도)
            litTick = tick;
            Exposure = Mathf.Min(1f, Exposure + fill * dt);
            if (!front) return;
            frontTick = tick;
            FrontExposure += frontFill * dt;
            if (FrontExposure >= 1f)
            {
                FrontExposure = 0f;
                var stun = GetComponent<CharacterStun>();
                if (stun == null) stun = gameObject.AddComponent<CharacterStun>();
                stun.Stun(stunTime);
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!Lit) Exposure = Mathf.Max(0f, Exposure - decayRate * dt);
            if (!LitFront) FrontExposure = Mathf.Max(0f, FrontExposure - frontDecayRate * dt);
        }
    }
}

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 손전등(도둑 E): 손전등을 꺼내 몸 앞쪽을 비춘다. E를 다시 누르면 넣는다. 배터리(batteryTime 6초)를 나눠 써서 재사용 대기 없이 껐다 켰다 할 수 있고,
    /// 다 쓰면 꺼지며 가득 다시 찬 뒤 emptyCooldown(1초) 뒤에 다시 켤 수 있다. 비추는 동안에도 달릴 수 있고, 걷기·달리기 속도만 moveSpeedScale배로 조금 느려진다.
    /// - 빛(원뿔: range m, 반각 halfAngle도, 벽에 가리면 안 닿음)에 든 상대는 LightExposure로 지져진다:
    ///   지져짐 게이지가 차서 일정 이상이면 그 상대의 E가 잠기고, 정면(얼굴 쪽)으로 받으면 따로 정면 게이지도 차서 다 차면 기절 + 피해(stunDamage).
    ///   비추지 않으면 두 게이지는 떨어진다(LightExposure — 지져짐이 다 찼던 뒤에는 2초 뒤부터).
    /// - 빛이 나가는 자리·방향·길이는 CharacterSkill.ReachOrigin / Direction / Reach로 알린다(FE가 손전등 빛을 그린다).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Flashlight")]
    public class FlashlightSkill : SkillDefinition
    {
        [Tooltip("빛이 닿는 거리(m) / 원뿔 반각(도).")]
        [Min(1f)] public float range = 14f;
        [Range(1f, 60f)] public float halfAngle = 16f;
        [Tooltip("빛을 받을 때 1초에 차는 양(게이지 전체 = 1) — 아래 칸(어디로든, 다 차면 E 봉인, 빠르게) / 위 칸(정면, 다 차면 기절, 느리게).")]
        [Min(0f)] public float fillRate = 0.6f, frontFillRate = 0.25f;
        [Tooltip("정면 게이지가 다 차 기절할 때 상대가 받는 피해.")]
        [Min(0f)] public float stunDamage = DamageRules.SurvivorSkillDamage;
        [Tooltip("상대가 빛 쪽을 이 각도(도) 안으로 보고 있으면 정면으로 받는다.")]
        [Range(1f, 180f)] public float frontAngle = 70f;
        [Tooltip("빛이 나가는 자리(캐릭터 키 대비 높이 / 몸 앞 m / 오른쪽 m) — 오른손에 든 손전등.")]
        [Range(0.1f, 1f)] public float heightRatio = 0.78f;
        public float forwardOffset = 0.55f, sideOffset = 0.2f;
        [Tooltip("켠 직후 이 시간(초) 동안은 E를 다시 눌러도 끄지 않는다(누른 키가 두 번 읽히지 않게).")]
        [Min(0f)] public float toggleGrace = 0.15f;

        [Tooltip("비추는 동안 걷기·달리기 속도 배율(달리기는 그대로 됨, 조금만 느려짐).")]
        [Range(0.1f, 1f)] public float moveSpeedScale = 0.85f;

        public override bool SyncAnimationToCast => false;
        public override bool LimitsMoveSpeed => false; // 걷기 속도로 묶지 않는다
        public override float CastSpeedScale => moveSpeedScale;

        [Header("배터리 — 나눠 쓰기")]
        [Tooltip("배터리(초): 켜 있는 동안만 줄고, 껐다 켰다 해도 재사용 대기 없이 남은 만큼 이어 쓴다.")]
        [Min(0.5f)] public float batteryTime = 6f;
        [Tooltip("배터리를 다 쓰면 꺼지고 가득 다시 찬 뒤 이 시간(초)이 지나야 다시 켤 수 있다.")]
        [Min(0f)] public float emptyCooldown = 1f;

        private static readonly System.Collections.Generic.Dictionary<CharacterSkill, float> battery = new();
        private static readonly System.Collections.Generic.HashSet<CharacterSkill> emptied = new();

        /// <summary>남은 배터리(초) — 처음이면 가득.</summary>
        private float Battery(CharacterSkill ctx) => battery.TryGetValue(ctx, out float b) ? b : batteryTime;

        public override bool NoTimeLimit => true; // 배터리가 다 될 때까지(E로 끌 수 있음)

        /// <summary>배터리를 다 써서 꺼졌을 때만 emptyCooldown, 직접 껐으면 재사용 대기 없음.</summary>
        public override float CooldownFor(CharacterSkill ctx) => emptied.Remove(ctx) ? emptyCooldown : 0f;

        public override void OnStart(CharacterSkill ctx)
        {
            if (!battery.ContainsKey(ctx)) battery[ctx] = batteryTime;
            ctx.Reach = range;
            UpdateBeam(ctx);
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (input.Skill && elapsed >= toggleGrace) { ctx.Reach = 0f; return true; } // E 다시 → 넣음(남은 배터리는 그대로)
            float left = Battery(ctx) - Time.fixedDeltaTime;
            if (left <= 0f)
            {
                // 배터리를 다 씀: 꺼지고 가득 다시 차며, emptyCooldown 뒤에 다시 켤 수 있다
                battery[ctx] = batteryTime;
                emptied.Add(ctx);
                ctx.Reach = 0f;
                return true;
            }
            battery[ctx] = left;
            UpdateBeam(ctx);
            ShineTargets(ctx);
            return false;
        }

        /// <summary>HUD 오른쪽 아래: 꺼져 있고 배터리가 덜 찼으면 남은 배터리 원 게이지(재사용 대기 중에는 보통 게이지).</summary>
        public override bool TryGetCooldownGauge(CharacterSkill ctx, out string label, out float fill)
        {
            label = "손전등";
            fill = Mathf.Clamp01(Battery(ctx) / batteryTime);
            return ctx.State == SkillState.Ready && fill < 0.999f;
        }

        public override void Complete(GameObject owner)
        {
            if (owner.TryGetComponent(out CharacterSkill ctx)) ctx.Reach = 0f;
        }

        private void UpdateBeam(CharacterSkill ctx)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(ctx.transform.forward, Vector3.up);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward; // 몸 = 시야 방향
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            var cc = ctx.GetComponent<CharacterController>();
            float h = cc != null ? cc.height : 1.7f;
            ctx.ReachOrigin = ctx.transform.position + Vector3.up * h * heightRatio + fwd * forwardOffset + right * sideOffset;
            ctx.Direction = fwd;
            ctx.Reach = range;
        }

        /// <summary>원뿔 안에 들고 벽에 가리지 않은, 맞힐 수 있는 상대를 지진다.</summary>
        private void ShineTargets(CharacterSkill ctx)
        {
            var self = ctx.GetComponent<CharacterIdentity>();
            Vector3 origin = ctx.ReachOrigin, dir = ctx.Direction;
            float cos = Mathf.Cos(halfAngle * Mathf.Deg2Rad), frontCos = Mathf.Cos(frontAngle * Mathf.Deg2Rad);
            int wallMask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (c == self || (self != null && !self.CanHit(c))) continue;
                Vector3 target = HomingProjectile.AimPoint(c);
                Vector3 to = target - origin;
                float dist = to.magnitude;
                if (dist > range || dist < 1e-3f || Vector3.Dot(to / dist, dir) < cos) continue;
                if (Blocked(origin, to / dist, dist, wallMask)) continue;
                Vector3 toLight = origin - c.transform.position; toLight.y = 0f;
                Vector3 face = c.transform.forward; face.y = 0f;
                bool front = toLight.sqrMagnitude > 1e-6f && Vector3.Dot(face.normalized, toLight.normalized) >= frontCos;
                var ex = c.GetComponent<LightExposure>();
                if (ex == null) ex = c.gameObject.AddComponent<LightExposure>();
                ex.Shine(Time.fixedDeltaTime, front, fillRate, frontFillRate, stunDamage);
            }
        }

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>origin에서 dist 안에 벽(캐릭터 몸 제외)이 있는지.</summary>
        private static bool Blocked(Vector3 origin, Vector3 dir, float dist, int mask)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, dist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (Hits[i].collider.GetComponentInParent<CharacterIdentity>() == null) return true;
            return false;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => "넣기: E";

        /// <summary>HUD: 켜 있는 동안 남은 배터리 막대.</summary>
        public override float StatusBarFill(CharacterSkill ctx) => Mathf.Clamp01(Battery(ctx) / batteryTime);
    }
}

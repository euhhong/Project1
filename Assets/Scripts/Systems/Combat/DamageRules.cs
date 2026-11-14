using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 데미지·경직·넉백 규칙 표(BE). Docs/DamageSystem.md(2026-11-14 사용자 확정, 감시자 피해 조금 낮춤)의 값을 한곳에 둔다.
    /// - 감시자 평타 = 한 방이 아픔(약 30 · 강 45~54 · 달리기 점프 36), 짧은 경직, 작은 넉백.
    /// - 생존자 평타 = 모기 딜(3~5) + 경직·넉백 위주(넉백으로 벽에 박으면 벽꿍 피해·추가 경직).
    /// - 피해 = 기본 피해 × 부위 배율 × 공격 세기(Power) × 등 뒤 배율 × 창틀 넘기 배율. 스킬은 HitResult.BaseDamage(직접 정함) 또는 분류별 기본값.
    /// 쓰는 곳: BattleDamage(피해), DefaultHitKnockback(넉백), HitStunRules(경직·패링 반격), CharacterLoadout(최대 체력).
    /// </summary>
    public static class DamageRules
    {
        /// <summary>평타 한 줄: 피해, 맞은 쪽 경직(초), 넉백 처음 속도(m/s)·시간(초), 벽꿍 켬.</summary>
        public readonly struct Spec
        {
            public readonly float Damage, Stun, KnockSpeed, KnockTime;
            public readonly bool WallSlam;
            public Spec(float damage, float stun, float knockSpeed, float knockTime, bool wallSlam)
            { Damage = damage; Stun = stun; KnockSpeed = knockSpeed; KnockTime = knockTime; WallSlam = wallSlam; }
        }

        // ── 최대 체력
        public const float SurvivorMaxHealth = 100f, HunterMaxHealth = 150f;

        // ── 평타 표 (넉백 거리 ≈ 속도 × 시간 ÷ 2)
        private static readonly Spec HunterLight = new(30f, 0.25f, 6f, 0.25f, false);    // 약 0.75 m
        private static readonly Spec HunterHeavy = new(45f, 0.45f, 10f, 0.3f, false);    // 약 1.5 m, 모으면 피해 최대 +9
        private static readonly Spec HunterRunJump = new(36f, 0.35f, 8f, 0.3f, false);   // 약 1.2 m
        private static readonly Spec SurvivorLight = new(3f, 0.4f, 8f, 0.3f, true);      // 약 1.2 m
        private static readonly Spec SurvivorHeavy = new(3f, 0.4f, 8f, 0.3f, true);      // (생존자 좌클릭 꾹은 주먹 패링 — 강공격이 나가면 약공격과 같게)
        private static readonly Spec SurvivorRunJump = new(5f, 0.5f, 12f, 0.4f, true);   // 약 2.4 m — 벽꿍 주 수단
        private static readonly Spec SurvivorFollowUp = new(4f, 0.8f, 10f, 0.3f, true);  // 약 1.5 m (경직 0.8은 FollowUpKickStun과 같음)

        /// <summary>강공격 모으기: 이 시간(초)을 다 모으면 피해 +HeavyChargeBonus.</summary>
        public const float HeavyFullCharge = 1f, HeavyChargeBonus = 9f;

        // ── 스킬(직접 정하지 않은 것)의 기본 피해, 기본 넉백
        public const float HunterSkillDamage = 22f, SurvivorSkillDamage = 5f;
        public const float SkillKnockSpeed = 6f, SkillKnockTime = 0.25f;
        // 스킬이 직접 정하는 피해(HitResult.BaseDamage)
        public const float LeapSlamDamage = 27f, NoteWireDamage = 8f, GrabDamage = 10f;

        // ── 배율·겹침
        public const float HeadScale = 1.25f, BodyScale = 1f, LimbScale = 0.8f;
        public const float BehindScale = 1.2f, BehindDot = -0.7071f; // 맞은 쪽 정면과 '맞은 쪽 → 때린 쪽'의 내적이 이보다 작으면 등 뒤(뒤쪽 90°)
        /// <summary>창틀을 넘는 동작(WindowVault·WindowDrop·WindowJump) 중에 맞으면 피해 배율(무방비 — 크게 다친다).</summary>
        public const float VaultScale = 2f;
        /// <summary>생존자 평타가 같은 상대를 경직시킨 뒤 이 시간(초) 동안은 다시 경직을 걸지 못한다(넉백·피해는 들어감).</summary>
        public const float SurvivorRestunWindow = 1f;
        /// <summary>패링(주먹 막기)에 막힌 쪽(때린 쪽) 경직(초).</summary>
        public const float ParryCounterStun = 0.5f;
        /// <summary>벽꿍: 피해 = 이 값 × 박힌 순간 속도(m/s), 박히면 추가 경직(초).</summary>
        public const float WallSlamDamagePerSpeed = 1.5f, WallSlamStun = 0.6f;

        /// <summary>감시자인지(캐릭터를 고를 때 정한 분류 — 이동의 HunterStyle).</summary>
        public static bool IsHunter(GameObject character)
            => character != null && character.TryGetComponent(out ICharacterTraversalState t) && t.HunterStyle;

        public static float MaxHealth(bool hunter) => hunter ? HunterMaxHealth : SurvivorMaxHealth;

        /// <summary>평타 표. 스킬(Kind = Skill)이면 false.</summary>
        public static bool TryGetBasic(bool hunter, AttackKind kind, out Spec spec)
        {
            switch (kind)
            {
                case AttackKind.Light: spec = hunter ? HunterLight : SurvivorLight; return true;
                case AttackKind.Heavy: spec = hunter ? HunterHeavy : SurvivorHeavy; return true;
                case AttackKind.RunJump: spec = hunter ? HunterRunJump : SurvivorRunJump; return true;
                case AttackKind.FollowUp: spec = hunter ? HunterRunJump : SurvivorFollowUp; return true;
                default: spec = default; return false;
            }
        }

        public static float PartScale(BodyPart part) => part switch
        {
            BodyPart.Head => HeadScale,
            BodyPart.Body => BodyScale,
            _ => LimbScale,
        };

        /// <summary>때린 쪽이 맞은 쪽 등 뒤(뒤쪽 90°)에 있는지.</summary>
        public static bool FromBehind(Transform attacker, Transform victim)
        {
            if (attacker == null || victim == null) return false;
            Vector3 toAttacker = Vector3.ProjectOnPlane(attacker.position - victim.position, Vector3.up);
            if (toAttacker.sqrMagnitude < 1e-4f) return false;
            Vector3 fwd = Vector3.ProjectOnPlane(victim.forward, Vector3.up).normalized;
            return Vector3.Dot(fwd, toAttacker.normalized) < BehindDot;
        }

        /// <summary>캐릭터가 창틀을 넘는 동작 중인지(창틀 넘기·창틀 위에서 뛰어내리기·점프 중 짚고 넘기).</summary>
        public static bool IsVaulting(GameObject character)
            => character != null && character.TryGetComponent(out ICharacterTraversalState t)
               && t.Action is TraversalAction.WindowVault or TraversalAction.WindowDrop or TraversalAction.WindowJump;

        /// <summary>이 히트의 피해. 데미지 없는 히트(NoDamage)는 0.</summary>
        public static float Damage(in HitResult hit, GameObject attacker, GameObject victim)
        {
            if (hit.NoDamage) return 0f;
            bool hunter = IsHunter(attacker);
            float baseDamage;
            if (hit.BaseDamage > 0f) baseDamage = hit.BaseDamage;
            else if (TryGetBasic(hunter, hit.Kind, out Spec spec))
            {
                baseDamage = spec.Damage;
                if (hit.Kind == AttackKind.Heavy && hunter) baseDamage += HeavyChargeBonus * Mathf.Clamp01(hit.Charge / HeavyFullCharge);
            }
            else baseDamage = hunter ? HunterSkillDamage : SurvivorSkillDamage;

            float damage = baseDamage * PartScale(hit.Part) * hit.PowerScale;
            if (FromBehind(attacker != null ? attacker.transform : null, victim != null ? victim.transform : null)) damage *= BehindScale;
            if (IsVaulting(victim)) damage *= VaultScale;
            return damage;
        }

        /// <summary>id로 캐릭터를 찾는다(없으면 null).</summary>
        public static CharacterIdentity Find(int id)
        {
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
                if (c.Id == id) return c;
            return null;
        }
    }
}

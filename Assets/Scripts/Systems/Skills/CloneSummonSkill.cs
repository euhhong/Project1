using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 분신(공장장 E): 옆에 분신을 하나 불러낸다. 분신은 불러낼 때 가장 가까이 있던 상대를 향해 저절로 다가가,
    /// 공격 범위에 들면 멈춰 서서 공격한다(CloneBrain). lifetime초 뒤 사라진다. 한 번에 하나 — 다시 부르면 지난 분신은 사라진다.
    /// 분신의 몸·이동·공격·맞는 판정은 여기(BE)서 만들고, 모델·겉모습(검은 몸 + 용암 균열)·무기(갈고리 낫)는 FE가 붙인다(CloneEvents.Spawned).
    /// 분신과 불러낸 캐릭터는 서로 맞지 않는다(CharacterIdentity.Summoner). 체력은 다루지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Clone Summon")]
    public class CloneSummonSkill : SkillDefinition
    {
        [Tooltip("분신이 있다가 사라지기까지(초).")]
        [Min(0.5f)] public float lifetime = 10f;
        [Tooltip("이 거리(m, 몸 가운데끼리) 안에 들면 멈춰 서서 공격한다.")]
        [Min(0.5f)] public float attackRange = 1.7f;
        [Tooltip("목표가 이보다 멀면 달려서, 가까우면 걸어서 다가간다(m).")]
        [Min(0f)] public float runDistance = 4f;
        [Tooltip("공격이 끝나고 다음 공격까지(초).")]
        [Min(0f)] public float attackGap = 0.35f;
        [Tooltip("분신이 나타나는 자리(불러낸 캐릭터 기준 오른쪽 m / 앞 m).")]
        public float spawnSide = 1.1f, spawnForward = 0.4f;
        [Tooltip("목표로 고를 상대의 최대 거리(m). 없으면 분신은 그 자리에 서 있다.")]
        [Min(1f)] public float targetRange = 60f;

        private static readonly Dictionary<int, CloneBrain> current = new();

        public override bool SyncAnimationToCast => false;

        public override void OnStart(CharacterSkill ctx)
        {
            var owner = ctx.GetComponent<CharacterIdentity>();
            if (owner == null) return;
            if (current.TryGetValue(owner.Id, out var old) && old != null) old.Despawn(); // 한 번에 하나
            current[owner.Id] = Summon(ctx, owner);
        }

        public override void Complete(GameObject owner) { }

        public override string StatusText(CharacterSkill ctx) => "분신!";

        public override string IdleStatusText(CharacterSkill ctx)
        {
            var owner = ctx.GetComponent<CharacterIdentity>();
            if (owner == null || !current.TryGetValue(owner.Id, out var c) || c == null) return null;
            return $"분신 {Mathf.Max(0f, c.Lifetime - c.Age):0}초 — {(c.Target != null ? (c.InRange ? "공격 중" : "다가가는 중") : "목표 없음")}";
        }

        private CloneBrain Summon(CharacterSkill ctx, CharacterIdentity owner)
        {
            Transform t = ctx.transform;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 pos = t.position + right * spawnSide + fwd * spawnForward;
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName)), QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<CharacterIdentity>() == null)
                pos = hit.point;
            var target = Nearest(owner, pos);
            Vector3 look = target != null ? Vector3.ProjectOnPlane(target.transform.position - pos, Vector3.up) : fwd;
            if (look.sqrMagnitude < 1e-4f) look = fwd;

            var go = new GameObject($"Clone_{owner.name}");
            go.SetActive(false);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.02f, Quaternion.LookRotation(look.normalized, Vector3.up));
            var cc = go.AddComponent<CharacterController>();
            if (ctx.TryGetComponent(out CharacterController ownerCc)) { cc.height = ownerCc.height; cc.radius = ownerCc.radius; cc.center = ownerCc.center; }
            var brain = go.AddComponent<CloneBrain>();
            brain.Init(target, lifetime, attackRange, runDistance, attackGap);
            go.AddComponent<CharacterIdentity>().SetSummoner(owner);
            go.AddComponent<CharacterMotor>();
            go.AddComponent<AttackHitDetector>();
            var atk = go.AddComponent<CharacterAttack>();
            atk.SetAttackSlots(HitboxSlot.RightHand | HitboxSlot.Weapon, HitboxSlot.RightHand | HitboxSlot.Weapon); // 낫(무기) + 오른손
            go.AddComponent<CharacterStun>();
            go.SetActive(true);
            CloneEvents.RaiseSpawned(go, owner); // FE: 모델·겉모습·낫
            if (go.TryGetComponent(out CharacterMotor motor)) motor.RefreshSpeedLimits();
            if (go.TryGetComponent(out AttackHitDetector det)) det.RefreshHitboxes();
            return brain;
        }

        /// <summary>pos에서 가장 가까운, 불러낸 캐릭터가 맞힐 수 있는 상대(분신 제외).</summary>
        private CharacterIdentity Nearest(CharacterIdentity owner, Vector3 pos)
        {
            CharacterIdentity best = null;
            float bestSq = targetRange * targetRange;
            foreach (var c in Object.FindObjectsByType<CharacterIdentity>())
            {
                if (!c.isActiveAndEnabled || !owner.CanHit(c)) continue;
                float d = (c.transform.position - pos).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = c; }
            }
            return best;
        }
    }
}

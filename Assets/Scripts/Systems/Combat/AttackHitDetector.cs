using System;
using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 캐릭터 루트에 붙어 공격 구간 동안 자기 Hitbox들로 히트를 판정한다.
    /// - BeginAttack(slots)~EndAttack() 사이에만 FixedUpdate마다 겹침 검사.
    /// - 한 공격에서 같은 대상은 한 번만 맞힌다. 자기 자신·같은 팀은 맞지 않는다.
    /// - 내 몸과 상대 사이가 벽·지형으로 막혀 있으면 맞지 않는다(CombatLineOfSight).
    /// - 맞으면 HitResult를 Hit 이벤트와 HitEvents.HitOccurred로 낸다. 데미지는 처리하지 않는다.
    /// Hitbox는 자식(모델 뼈) 어디에 있어도 되고, 모델이 바뀌면 RefreshHitboxes()로 다시 찾는다.
    /// </summary>
    [RequireComponent(typeof(CharacterIdentity))]
    [DisallowMultipleComponent]
    public class AttackHitDetector : MonoBehaviour
    {
        private const int MaxOverlaps = 32;

        private readonly List<Hitbox> hitboxes = new();
        private readonly HashSet<int> hitVictims = new();

        [Tooltip("땅에서 하는 일반 공격(약·강)은, 점프해 발이 이만큼(m) 넘게 높이 뜬 상대(점프 꼭대기 근처)를 맞히지 못한다.")]
        [SerializeField, Min(0f)] private float jumpDodgeHeight = 1.15f;
        [Tooltip("앞으로 달리며 하는 일반 공격: 판정을 지금 자리에서 앞으로 (속도 × 이 시간)만큼까지 훑는다 — 달려가며 칠 때 이동한 뒤의 자리까지 맞는 긴 리치.")]
        [SerializeField, Min(0f)] private float moveLeadTime = 0.25f;
        [Tooltip("맨주먹으로 치는 캐릭터인지(생존자 주먹 기본 공격). 켜면 무기 없는 공격의 손 판정 히트가 주먹(HitResult.Fist)이 되어 주먹 막기(FistParry)에 막힌다. 캐릭터를 고를 때 SetUnarmed로 정한다.")]
        [SerializeField] private bool unarmed;
        private ICharacterAttackState attackState;

        /// <summary>맨주먹 캐릭터인지 정한다(캐릭터를 고를 때). 감시자처럼 무기를 손 판정으로 휘두르는 캐릭터는 끈다.</summary>
        public void SetUnarmed(bool on) => unarmed = on;
        public bool Unarmed => unarmed;
        private readonly Dictionary<int, Candidate> bestThisTick = new();
        private readonly Collider[] overlapBuffer = new Collider[MaxOverlaps];

        private CharacterIdentity identity;
        private HitboxSlot activeSlots;
        private int attackCounter;

        /// <summary>이 캐릭터가 누군가를 맞혔을 때.</summary>
        public event Action<HitResult> Hit;

        public bool IsAttacking => activeSlots != HitboxSlot.None;
        public int CurrentAttackId => attackCounter;

        /// <summary>공격 시작: 지정한 자리의 Hitbox를 켠다. 새 공격 번호를 돌려준다.</summary>
        public int BeginAttack(HitboxSlot slots)
        {
            attackCounter++;
            activeSlots = slots;
            hitVictims.Clear();
            return attackCounter;
        }

        /// <summary>공격 끝: 모든 Hitbox를 끈다.</summary>
        public void EndAttack()
        {
            activeSlots = HitboxSlot.None;
        }

        /// <summary>자식의 Hitbox 목록을 다시 모은다 (모델 교체 후 호출).</summary>
        public void RefreshHitboxes()
        {
            hitboxes.Clear();
            GetComponentsInChildren(true, hitboxes);
        }

        private void Awake()
        {
            identity = GetComponent<CharacterIdentity>();
            RefreshHitboxes();
        }

        private void OnTransformChildrenChanged()
        {
            RefreshHitboxes();
        }

        private void FixedUpdate()
        {
            if (!IsAttacking)
            {
                return;
            }

            // 애니메이션으로 움직인 뼈 위치를 물리 쿼리에 반영.
            Physics.SyncTransforms();

            bestThisTick.Clear();
            if (attackState == null) TryGetComponent(out attackState);
            bool groundNormal = attackState != null && !attackState.IsAirAttack
                && (attackState.AttackState == CharacterAttackState.Light || attackState.AttackState == CharacterAttackState.Heavy);
            // 앞으로 달리며 치면: 판정을 앞으로 이동할 자리까지 늘인다(지금·가운데·끝 세 자리를 훑는다).
            Vector3 lead = Vector3.zero; int leadSamples = 1;
            if (groundNormal && TryGetComponent(out ICharacterState mover))
            {
                Vector3 v = transform.TransformDirection(new Vector3(mover.LocalPlanarVelocity.x, 0f, mover.LocalPlanarVelocity.y));
                float fwd = Vector3.Dot(v, transform.forward);
                if (fwd > mover.WalkSpeed * 0.6f) { lead = transform.forward * (fwd * moveLeadTime); leadSamples = 3; }
            }
            int mask = CombatLayers.HurtboxMask;

            foreach (Hitbox hitbox in hitboxes)
            {
                if (hitbox == null || !hitbox.isActiveAndEnabled || (hitbox.Slot & activeSlots) == 0)
                {
                    continue;
                }

                Vector3 origin = hitbox.Collider.bounds.center;
                for (int s = 0; s < leadSamples; s++)
                {
                int count = hitbox.Overlap(overlapBuffer, mask, leadSamples > 1 ? lead * (s / (float)(leadSamples - 1)) : Vector3.zero);
                for (int i = 0; i < count; i++)
                {
                    if (!overlapBuffer[i].TryGetComponent(out Hurtbox hurtbox) || !hurtbox.isActiveAndEnabled)
                    {
                        continue;
                    }

                    CharacterIdentity victim = hurtbox.Owner;
                    if (!identity.CanHit(victim) || hitVictims.Contains(victim.Id))
                    {
                        continue;
                    }
                    if (groundNormal && JumpedOver(victim)) continue; // 점프 꼭대기 근처의 상대는 땅 일반 공격에 맞지 않는다

                    // 같은 틱에 한 대상의 여러 부위가 겹치면 Hitbox에 가장 가까운 부위 하나만.
                    Vector3 point = hurtbox.Collider.ClosestPoint(origin);
                    if (!CombatLineOfSight.Clear(gameObject, victim.gameObject, point)) continue; // 벽 너머: 무기·팔다리가 벽을 뚫어도 맞지 않는다
                    float distSq = (point - origin).sqrMagnitude;
                    if (hitbox.PrefersHead && hurtbox.Part == BodyPart.Head) distSq = -1f; // 머리를 노리는 판정(발차기): 머리가 겹치면 머리
                    if (!bestThisTick.TryGetValue(victim.Id, out Candidate best) || distSq < best.DistSq)
                    {
                        bestThisTick[victim.Id] = new Candidate(hurtbox.Part, hitbox.Slot, point, distSq, victim.gameObject);
                    }
                }
                }
            }

            int tick = SimulationTick.Current;
            foreach (KeyValuePair<int, Candidate> pair in bestThisTick)
            {
                hitVictims.Add(pair.Key);
                var hit = new HitResult
                {
                    AttackerId = identity.Id,
                    VictimId = pair.Key,
                    Part = pair.Value.Part,
                    Slot = pair.Value.Slot,
                    AttackId = attackCounter,
                    Tick = tick,
                    Point = pair.Value.Point,
                    // 주먹: 맨주먹 캐릭터가 무기 없는 공격을 손 판정으로 맞힘
                    Fist = unarmed && (pair.Value.Slot & (HitboxSlot.LeftHand | HitboxSlot.RightHand)) != 0 && (activeSlots & HitboxSlot.Weapon) == 0,
                    Kind = CurrentKind(),                                  // 데미지·경직·넉백 규칙(DamageRules)이 표를 고른다
                    Charge = attackState != null ? attackState.ChargeTime : 0f,
                };
                // 맞는 쪽이 막으면(예: 정면 주먹 막기) 히트를 내지 않는다 — 데미지·경직·넉백·피격 모션 없음, 막기 이펙트만.
                if (HitBlockers.Blocks(pair.Value.Victim, hit, gameObject)) { HitEvents.RaiseBlocked(hit); continue; }
                HitModifiers.Apply(gameObject, ref hit, pair.Value.Victim); // 예: 맹인 지팡이 = 데미지 없이 넉백
                Hit?.Invoke(hit);
                HitEvents.Raise(hit);
            }
        }

        /// <summary>지금 평타의 종류(두 번째 날아 차기 → 달리기 점프 → 강 → 약).</summary>
        private AttackKind CurrentKind()
        {
            if (attackState == null) return AttackKind.Light;
            if (attackState.IsFollowUpKick) return AttackKind.FollowUp;
            if (attackState.IsRunJumpAttack) return AttackKind.RunJump;
            return attackState.AttackState == CharacterAttackState.Heavy ? AttackKind.Heavy : AttackKind.Light;
        }

        /// <summary>상대가 점프해 발이 내 발보다 jumpDodgeHeight 넘게 높이 떠 있는지.</summary>
        private bool JumpedOver(CharacterIdentity victim)
        {
            if (!victim.TryGetComponent(out ICharacterState vs) || vs.IsGrounded) return false;
            return victim.transform.position.y - transform.position.y > jumpDodgeHeight;
        }

        private readonly struct Candidate
        {
            public readonly BodyPart Part;
            public readonly HitboxSlot Slot;
            public readonly Vector3 Point;
            public readonly float DistSq;
            public readonly GameObject Victim;

            public Candidate(BodyPart part, HitboxSlot slot, Vector3 point, float distSq, GameObject victim)
            {
                Victim = victim;
                Part = part;
                Slot = slot;
                Point = point;
                DistSq = distSq;
            }
        }
    }
}

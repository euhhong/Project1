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
            int mask = CombatLayers.HurtboxMask;

            foreach (Hitbox hitbox in hitboxes)
            {
                if (hitbox == null || !hitbox.isActiveAndEnabled || (hitbox.Slot & activeSlots) == 0)
                {
                    continue;
                }

                Vector3 origin = hitbox.Collider.bounds.center;
                int count = hitbox.Overlap(overlapBuffer, mask);
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

                    // 같은 틱에 한 대상의 여러 부위가 겹치면 Hitbox에 가장 가까운 부위 하나만.
                    Vector3 point = hurtbox.Collider.ClosestPoint(origin);
                    float distSq = (point - origin).sqrMagnitude;
                    if (!bestThisTick.TryGetValue(victim.Id, out Candidate best) || distSq < best.DistSq)
                    {
                        bestThisTick[victim.Id] = new Candidate(hurtbox.Part, hitbox.Slot, point, distSq, victim.gameObject);
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
                };
                HitModifiers.Apply(gameObject, ref hit, pair.Value.Victim); // 예: 맹인 지팡이 = 데미지 없이 넉백
                Hit?.Invoke(hit);
                HitEvents.Raise(hit);
            }
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

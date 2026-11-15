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
        [Tooltip("감시자 평타(약·강·공중·달리기 점프 등 기본 공격) 사거리 보정(m): 판정을 몸 정면으로만 이만큼까지 밀어 한 번 더 훑는다(앞 리치만 늘고 좌우 폭은 그대로, 모델·판정 크기도 그대로). 스킬 판정은 아님.")]
        [SerializeField, Min(0f)] private float hunterReachBonus = 0.5f;
        private float extraReach;

        /// <summary>캐릭터별 평타 앞 리치 더하기(m) — 캐릭터를 고를 때 정한다(예: 붉은나비 0.5). 감시자 보정 위에 더하고, 정면으로만 늘린다(좌우 폭은 그대로).</summary>
        public void SetExtraReach(float meters) => extraReach = Mathf.Max(0f, meters);

        private float extraWidth;

        /// <summary>캐릭터별 평타 좌우 폭 더하기(m) — 판정을 몸 좌우로 이만큼 밀어서도 훑는다. 캐릭터를 고를 때 정한다(예: 붉은나비 0.15 — 기본은 0, 감시자 보정은 앞 리치로만).</summary>
        public void SetExtraWidth(float meters) => extraWidth = Mathf.Max(0f, meters);
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

        /// <summary>평타 판정이 벽을 정면으로 확실히 쳤을 때(벽에 닿은 자리, 벽 면 바깥 방향). 한 공격에 한 번. CharacterAttack이 튕겨 나오기로 바꾼다.</summary>
        public event Action<Vector3, Vector3> WallStruck;

        [Header("벽 치기(튕겨 나옴)")]
        [Tooltip("벽으로 칠 판정: 몸 가운데에서 이 거리(m) 안에 벽 면이 있어야 한다.")]
        [SerializeField, Min(0.1f)] private float wallMaxDistance = 1.0f;
        [Tooltip("판정(무기·손·발 가운데)이 벽 면을 이만큼(m) 넘게 파고들어야 '확실히 벽에 대고 친' 것으로 본다.")]
        [SerializeField, Min(0f)] private float wallMinPenetration = 0.12f;
        [Tooltip("벽이 몸 정면에서 이 각도(도) 안에 있어야 한다(옆으로 스치는 건 아님).")]
        [SerializeField, Range(0f, 90f)] private float wallMaxAngle = 35f;
        private bool wallStruckThisAttack;
        private static readonly RaycastHit[] wallHits = new RaycastHit[8];

        public bool IsAttacking => activeSlots != HitboxSlot.None;
        public int CurrentAttackId => attackCounter;

        /// <summary>공격 시작: 지정한 자리의 Hitbox를 켠다. 새 공격 번호를 돌려준다.</summary>
        public int BeginAttack(HitboxSlot slots)
        {
            attackCounter++;
            activeSlots = slots;
            hitVictims.Clear();
            wallStruckThisAttack = false;
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
            // 감시자 평타: 판정을 몸 정면으로 hunterReachBonus까지 밀어 더 훑는다(지금·가운데·끝) — 사거리만 조금 길게.
            Vector3 reach = Vector3.zero; int reachSamples = 1;
            float reachLength = (DamageRules.IsHunter(gameObject) ? hunterReachBonus : 0f) + extraReach;
            if (reachLength > 0f && attackState != null && attackState.AttackState != CharacterAttackState.None)
            {
                reach = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * reachLength;
                reachSamples = 3;
            }
            // 캐릭터별 좌우 폭 보정(예: 붉은나비): 판정을 몸 오른쪽·왼쪽으로 extraWidth만큼 밀어서도 훑는다.
            Vector3 side = Vector3.zero; int widthSamples = 1;
            if (extraWidth > 0f && attackState != null && attackState.AttackState != CharacterAttackState.None)
            {
                side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized) * extraWidth;
                widthSamples = 3;
            }
            int mask = CombatLayers.HurtboxMask;

            foreach (Hitbox hitbox in hitboxes)
            {
                if (hitbox == null || !hitbox.isActiveAndEnabled || (hitbox.Slot & activeSlots) == 0)
                {
                    continue;
                }

                Vector3 origin = hitbox.Collider.bounds.center;
                for (int sample = 0; sample < leadSamples * reachSamples * widthSamples; sample++)
                {
                int s = sample / (reachSamples * widthSamples), k = sample / widthSamples % reachSamples, sw = sample % widthSamples;
                Vector3 offset = (leadSamples > 1 ? lead * (s / (float)(leadSamples - 1)) : Vector3.zero)
                               + (reachSamples > 1 ? reach * (k / (float)(reachSamples - 1)) : Vector3.zero)
                               + (widthSamples > 1 ? side * (sw - 1) : Vector3.zero); // -1 왼쪽, 0 가운데, +1 오른쪽
                int count = hitbox.Overlap(overlapBuffer, mask, offset);
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
                    if (hurtbox.Assist) distSq += 1000f; // 보정 판정(창틀·매달리기 중 넓힘): 진짜 부위가 같이 겹치면 그쪽
                    if (!bestThisTick.TryGetValue(victim.Id, out Candidate best) || distSq < best.DistSq)
                    {
                        bestThisTick[victim.Id] = new Candidate(hurtbox.Part, hitbox.Slot, point, distSq, victim.gameObject);
                    }
                }
                }
            }

            // 아직 아무도 맞히지 않았는데 판정이 앞의 벽을 확실히 쳤으면: 튕겨 나온다(한 공격에 한 번).
            if (bestThisTick.Count == 0 && hitVictims.Count == 0 && !wallStruckThisAttack && WallEligible()
                && TryFindWallStrike(out Vector3 wallPoint, out Vector3 wallNormal))
            {
                wallStruckThisAttack = true;
                WallStruck?.Invoke(wallPoint, wallNormal);
                WallBounceEvents.Raise(gameObject, wallPoint, wallNormal);
                return;
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

        /// <summary>벽 치기를 보는 공격: 약·강 평타(땅·공중). 달리기 점프 공격·두 번째 날아 차기는 빼고, 스킬이 판정을 쓰는 경우(AttackState None)도 뺀다.</summary>
        private bool WallEligible()
        {
            if (attackState == null) return false;
            if (attackState.IsRunJumpAttack || attackState.IsFollowUpKick) return false;
            return attackState.AttackState == CharacterAttackState.Light || attackState.AttackState == CharacterAttackState.Heavy;
        }

        /// <summary>
        /// 확실히 벽에 대고 쳤는지: 켜진 판정마다 몸 가운데 → 판정 가운데로 쏴서, 그 사이에 벽(세운 면, 캐릭터·트리거·판정 아님)이 있고
        /// - 벽 면이 몸에서 wallMaxDistance 안, - 벽이 몸 정면 wallMaxAngle 안을 마주 보고, - 판정 가운데가 벽 면을 wallMinPenetration 넘게 파고들었으면.
        /// 바닥·천장·스치기는 아니다.
        /// </summary>
        private bool TryFindWallStrike(out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            Vector3 body = transform.position + Vector3.up * 1f;
            if (TryGetComponent(out CharacterController cc)) body = transform.TransformPoint(cc.center);
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            float bestPen = 0f; bool found = false;
            foreach (Hitbox hitbox in hitboxes)
            {
                if (hitbox == null || !hitbox.isActiveAndEnabled || (hitbox.Slot & activeSlots) == 0) continue;
                Vector3 tip = hitbox.Collider.bounds.center;
                Vector3 d = tip - body;
                float len = d.magnitude;
                if (len < 0.05f) continue;
                int n = Physics.RaycastNonAlloc(body, d / len, wallHits, len, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var h = wallHits[i];
                    if (h.collider == null || h.collider.GetComponentInParent<CharacterIdentity>() != null) continue;
                    if (Mathf.Abs(h.normal.y) > 0.35f) continue;                       // 바닥·천장·경사 아님
                    if (h.distance > wallMaxDistance) continue;                        // 몸 가까이의 벽만
                    Vector3 wallFace = Vector3.ProjectOnPlane(-h.normal, Vector3.up).normalized;
                    if (Vector3.Angle(fwd, wallFace) > wallMaxAngle) continue;         // 정면을 마주 본 벽
                    float pen = Vector3.Dot(tip - h.point, -h.normal);                 // 판정이 벽 속으로 들어간 깊이
                    if (pen < wallMinPenetration || pen <= bestPen) continue;
                    bestPen = pen; point = h.point; normal = h.normal; found = true;
                }
            }
            return found;
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

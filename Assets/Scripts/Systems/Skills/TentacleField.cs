using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 하스터의 소환 촉수(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신) 마우스가 가리키는 땅에 촉수를 세울 자리를 원으로 고르고(GroundPlacementPreview), 떼면 그 자리에서 촉수가 솟아오른다.
    ///   몸 근처로 끌어다 떼거나, 누른 채 E를 누르면 취소(좌클릭을 뗄 때까지 무시).
    /// - 촉수는 최대 maxTentacles개, 넘으면 가장 오래된 것이 땅속으로 들어간다. 세운 뒤 lifetime초가 지나도 들어간다.
    /// - E를 꾹 누르면 바라보는 방향(시야)에 가장 맞는 서 있는 촉수를 고른다(TargetIndex). E를 떼면 그 촉수가 내려찍는다:
    ///   뒤로 젖혀 말아 올림(slamWindup) → 앞으로 내리쳐 땅에 눕는다(slamActive, 이 동안 밑동에서 그 방향으로 키만큼 굵기 slamRadius로 훑어 닿는 상대를 한 번씩 맞힘) → 다시 섬(slamRecovery) → 땅속으로(sink).
    ///   내려찍는 방향 = 촉수 키 안의 가장 가까운 상대 쪽, 없으면 하스터가 바라보는 방향.
    /// - 쓰는 동안 하스터는 자유롭게 움직인다. 맞히기만 하고 체력·데미지는 다루지 않는다(HitEvents).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 하스터만). 그리기는 FE(TentacleVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class TentacleField : MonoBehaviour, ITentacleField, IAttackHoldOverride, ILookSensitivity
    {
        [Tooltip("촉수를 세울 자리 고르기(땅 위만, 몸 근처 = 취소).")]
        [SerializeField] private GroundPlacementPreview placement = new() { orbitWhileTurning = false, maxPlaceDistance = 25f, defaultDistance = 8f, clampToMax = true };
        [SerializeField, Min(1)] private int maxTentacles = 3;
        [Tooltip("세운 뒤 저절로 들어가기까지(초).")]
        [SerializeField, Min(1f)] private float lifetime = 20f;
        [Tooltip("촉수 키(m) = 내려찍으면 닿는 거리.")]
        [SerializeField, Min(0.5f)] private float tentacleHeight = 3.2f;
        [Tooltip("내려찍기 판정 굵기(반지름, m).")]
        [SerializeField, Min(0.1f)] private float slamRadius = 0.75f;
        [Header("시간(초) — 모션(HasturTentacle.fbx)과 맞춘다")]
        [SerializeField, Min(0.05f)] private float riseTime = 0.6f;
        [SerializeField, Min(0.05f)] private float slamWindup = 0.4f;
        [SerializeField, Min(0.05f)] private float slamActive = 0.17f;
        [SerializeField, Min(0.05f)] private float slamRecovery = 0.43f;
        [SerializeField, Min(0.05f)] private float sinkTime = 0.5f;
        [Header("고르기 (E 꾹)")]
        [SerializeField, Range(1f, 90f)] private float maxTargetAngle = 40f;
        [SerializeField, Min(1f)] private float maxTargetDistance = 40f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.7f;
        [Tooltip("시점을 돌리는 중에 자리를 고르기 시작했을 때 카메라 상하 감도 배율(안토니오 음표와 같음).")]
        [SerializeField, Range(0f, 1f)] private float placingPitchScale = 0.1f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        private readonly List<TentacleInfo> tentacles = new();
        private readonly Dictionary<int, HashSet<int>> hitBySlam = new(); // 촉수 Id → 이번 내려찍기에 맞은 상대
        private ICharacterInputSource inputSource;
        private ICharacterSkillState skillState;
        private ICharacterStun stun;
        private CharacterIdentity self;
        private bool waitRelease, skillWasHeld;
        private int nextId = 1;

        public bool Placing { get; private set; }
        public Vector3 PreviewPoint => placement.Point;
        public bool PreviewValid => Placing && placement.Valid;
        public bool PreviewCancels => Placing && placement.Cancels;
        public bool Selecting { get; private set; }
        public int TargetIndex { get; private set; } = -1;
        public IReadOnlyList<TentacleInfo> Tentacles => tentacles;
        public float TentacleHeight => tentacleHeight;
        public float SlamRadius => slamRadius;
        public bool OverridesHeavyHold => isActiveAndEnabled;
        public float PitchScale => Placing && placement.StartedWhileTurning ? placingPitchScale : 1f;

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            self = GetComponent<CharacterIdentity>();
        }

        private void OnDisable()
        {
            Placing = false; placement.Clear(); waitRelease = false; Selecting = false; TargetIndex = -1;
            tentacles.Clear(); hitBySlam.Clear();
        }

        private void FixedUpdate()
        {
            if (inputSource == null) Awake();
            if (inputSource == null) return;
            CharacterInputFrame input = inputSource.ReadInput();
            if (skillState == null) TryGetComponent(out skillState);
            if (stun == null) TryGetComponent(out stun);
            bool blocked = (skillState != null && skillState.LocksActions) || (stun != null && stun.IsStunned);
            float dt = Time.fixedDeltaTime;

            // 좌클릭 꾹 → 세울 자리 고르기, 떼면 세운다.
            if (waitRelease)
            {
                if (!input.HeavyCharging) waitRelease = false;
            }
            else if (input.HeavyCharging && !blocked)
            {
                if (!Placing) placement.Begin();
                Placing = true;
                placement.Update(transform, input);
                if (input.Skill) { Placing = false; placement.Clear(); waitRelease = true; } // 누른 채 E → 취소
            }
            else if (Placing)
            {
                if (!placement.Cancels && placement.Valid && !blocked) Summon(placement.Point);
                Placing = false; placement.Clear();
            }

            // E 꾹 → 고르기, 떼면 고른 촉수가 내려찍는다. 자리를 고르는 중(또는 취소 직후)에는 쓰지 않는다.
            bool held = input.SkillHeld && !Placing && !waitRelease && !blocked;
            Selecting = held;
            UpdateTarget(input);
            if (skillWasHeld && !input.SkillHeld && !blocked && TargetIndex >= 0) StartSlam(TargetIndex, input);
            skillWasHeld = input.SkillHeld && !Placing && !waitRelease;
            if (!Selecting && !skillWasHeld) TargetIndex = -1;

            Step(dt);
        }

        private void Summon(Vector3 point)
        {
            // 넘치면 가장 오래된 서 있는 촉수를 들여보낸다.
            int standing = 0;
            foreach (var t in tentacles) if (t.State != TentacleState.Sinking) standing++;
            for (int i = 0; i < tentacles.Count && standing >= maxTentacles; i++)
                if (tentacles[i].State != TentacleState.Sinking) { Set(i, TentacleState.Sinking); standing--; }
            Vector3 away = Flat(point - transform.position);
            tentacles.Add(new TentacleInfo
            {
                Id = nextId++, Position = point, State = TentacleState.Rising, StateTime = 0f, StateDuration = riseTime,
                Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Flat(transform.forward).normalized,
            });
        }

        private void StartSlam(int index, in CharacterInputFrame input)
        {
            var t = tentacles[index];
            if (t.State != TentacleState.Idle) return;
            t.Direction = SlamDirection(t.Position, input);
            tentacles[index] = t;
            Set(index, TentacleState.SlamWindup);
            hitBySlam[t.Id] = new HashSet<int>();
        }

        /// <summary>촉수 키 안의 가장 가까운 상대 쪽, 없으면 하스터가 바라보는 방향(수평).</summary>
        private Vector3 SlamDirection(Vector3 basePoint, in CharacterInputFrame input)
        {
            Vector3 best = Vector3.zero; float bestD = tentacleHeight + slamRadius;
            var hits = Physics.OverlapSphere(basePoint + Vector3.up, tentacleHeight + slamRadius, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            foreach (var c in hits)
            {
                if (!c.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var owner = hurt.Owner;
                if (owner == null || (self != null && !self.CanHit(owner))) continue;
                Vector3 to = Flat(owner.transform.position - basePoint);
                if (to.magnitude < bestD && to.sqrMagnitude > 1e-4f) { bestD = to.magnitude; best = to.normalized; }
            }
            if (best != Vector3.zero) return best;
            Vector3 look = Quaternion.Euler(0f, input.LookYaw, 0f) * Vector3.forward;
            return Flat(look).normalized;
        }

        private void Step(float dt)
        {
            for (int i = tentacles.Count - 1; i >= 0; i--)
            {
                var t = tentacles[i];
                t.StateTime += dt;
                tentacles[i] = t;
                switch (t.State)
                {
                    case TentacleState.Rising: if (t.StateTime >= riseTime) Set(i, TentacleState.Idle); break;
                    case TentacleState.Idle: if (t.StateTime >= lifetime) Set(i, TentacleState.Sinking); break;
                    case TentacleState.SlamWindup: if (t.StateTime >= slamWindup) Set(i, TentacleState.SlamActive); break;
                    case TentacleState.SlamActive:
                        HitAlong(t);
                        if (t.StateTime >= slamActive) Set(i, TentacleState.SlamRecovery);
                        break;
                    case TentacleState.SlamRecovery: if (t.StateTime >= slamRecovery) { hitBySlam.Remove(t.Id); Set(i, TentacleState.Sinking); } break;
                    case TentacleState.Sinking: if (t.StateTime >= sinkTime) { if (TargetIndex == i) TargetIndex = -1; tentacles.RemoveAt(i); } break;
                }
            }
        }

        private void Set(int i, TentacleState state)
        {
            var t = tentacles[i];
            t.State = state; t.StateTime = 0f;
            t.StateDuration = state switch
            {
                TentacleState.Rising => riseTime,
                TentacleState.Idle => lifetime,
                TentacleState.SlamWindup => slamWindup,
                TentacleState.SlamActive => slamActive,
                TentacleState.SlamRecovery => slamRecovery,
                _ => sinkTime,
            };
            tentacles[i] = t;
        }

        /// <summary>내리치는 동안: 밑동에서 내려찍는 방향으로 키만큼, 땅 가까이(무릎~허리 높이)를 훑는다.</summary>
        private void HitAlong(in TentacleInfo t)
        {
            if (!hitBySlam.TryGetValue(t.Id, out var done)) hitBySlam[t.Id] = done = new HashSet<int>();
            Vector3 start = t.Position + Vector3.up * 0.6f;
            var hits = Physics.SphereCastAll(start, slamRadius, t.Direction, tentacleHeight, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            foreach (var hc in hits)
            {
                if (!hc.collider.TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var victim = hurt.Owner;
                if (victim == null || (self != null && !self.CanHit(victim)) || !done.Add(victim.Id)) continue;
                HitEvents.Raise(new HitResult
                {
                    AttackerId = self != null ? self.Id : 0,
                    VictimId = victim.Id,
                    Part = hurt.Part,
                    Slot = slot,
                    AttackId = -1000 - t.Id,
                    Tick = SimulationTick.Current,
                    Point = hc.distance > 0f ? hc.point : hurt.Collider.ClosestPoint(start),
                });
            }
        }

        /// <summary>E를 꾹 누르는 동안: 바라보는 방향과 가장 가까운 각도의 서 있는 촉수를 고른다.</summary>
        private void UpdateTarget(in CharacterInputFrame input)
        {
            if (!Selecting) return;
            TargetIndex = -1;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 look = Quaternion.Euler(input.LookPitch, input.LookYaw, 0f) * Vector3.forward;
            float bestAngle = maxTargetAngle;
            for (int i = 0; i < tentacles.Count; i++)
            {
                if (tentacles[i].State != TentacleState.Idle) continue;
                Vector3 to = tentacles[i].Position + Vector3.up * (tentacleHeight * 0.5f) - eye;
                if (to.magnitude > maxTargetDistance) continue;
                float angle = Vector3.Angle(look, to);
                if (angle <= bestAngle) { bestAngle = angle; TargetIndex = i; }
            }
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

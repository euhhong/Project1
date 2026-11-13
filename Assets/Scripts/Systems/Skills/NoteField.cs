using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 안토니오의 붉은 음표(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신) 붉은나비 나비처럼 마우스가 가리키는 땅에 놓을 자리를 고르고, 떼면 음표를 놓는다(GroundPlacementPreview).
    /// - E를 누르면 바로 앞(instantDistance)에 즉시 음표를 놓는다. 좌클릭을 꾹 눌러 놓을 자리를 고르는 중이면 E = 고르기 취소(음표를 놓지 않고, 좌클릭을 뗄 때까지 무시).
    /// - 음표는 두 개가 한 쌍이다: 첫 음표를 놓고 linkWindow(5초) 안에 둘째를 놓아야 한다. 못 놓으면 끊기고 첫 음표는 사라진다.
    ///   둘째를 놓으면 두 음표가 붉은 전류선으로 이어진다. 다시 음표를 놓으면 지난 쌍은 사라지고 새 첫 음표가 된다.
    /// - 이어진 동안 전류선(두 음표 사이, 음표까지 포함 — 후한 굵기 wireRadius)에 닿은 상대는 맞는다(HitEvents).
    ///   계속 닿아 있으면 rehitInterval마다 다시 맞는다. 체력·데미지는 다루지 않는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 안토니오만). 그리기는 FE(NoteVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteField : MonoBehaviour, INoteField, IAttackHoldOverride, ILookSensitivity
    {
        [Tooltip("놓을 자리 고르기(땅 위만, 몸 근처 = 취소). 시점을 돌리는 동안에도 붉은나비처럼 거리를 지키지 않고 화면 가운데가 가리키는 땅을 자유롭게 따라간다.")]
        [SerializeField] private GroundPlacementPreview placement = new() { orbitWhileTurning = false };
        [Tooltip("첫 음표를 놓은 뒤 둘째를 놓아야 하는 시간(초).")]
        [SerializeField, Min(0.1f)] private float linkWindow = 5f;
        [Tooltip("E로 바로 놓을 때 몸 앞 거리(m).")]
        [SerializeField, Min(0f)] private float instantDistance = 1.6f;
        [Tooltip("음표·전류선이 떠 있는 높이(땅에서, m).")]
        [SerializeField, Min(0f)] private float noteHeight = 1.0f;
        [Tooltip("전류선 판정 굵기(반지름, m) — 후하게. 양 끝(음표)까지 포함한다.")]
        [SerializeField, Min(0.05f)] private float wireRadius = 0.75f;
        [Tooltip("계속 닿아 있을 때 다시 맞는 간격(초).")]
        [SerializeField, Min(0.05f)] private float rehitInterval = 1f;
        public HitboxSlot slot = HitboxSlot.Weapon;

        private readonly List<Vector3> notes = new();
        private readonly Collider[] overlaps = new Collider[32];
        private readonly Dictionary<int, float> lastHitAt = new();
        private readonly HashSet<int> hitThisTick = new();
        private ICharacterInputSource inputSource;
        private ICharacterSkillState skillState;
        private ICharacterStun stun;
        private CharacterIdentity self;
        private bool waitRelease;
        private int hitCounter;

        public bool Placing { get; private set; }
        public Vector3 PreviewPoint => placement.Point;
        public bool PreviewValid => Placing && placement.Valid;
        public bool PreviewCancels => Placing && placement.Cancels;
        public IReadOnlyList<Vector3> Notes => notes;
        public bool Linked { get; private set; }
        public float PendingTimeLeft { get; private set; }
        public float PendingDuration => linkWindow;
        public float NoteHeight => noteHeight;
        public Vector3 LastHitPoint { get; private set; }
        public float LastHitTime { get; private set; } = -1f;
        public bool OverridesHeavyHold => isActiveAndEnabled;

        [Tooltip("음표 자리를 고르는 동안 카메라 상하 감도 배율(아주 작게 — 마우스 위·아래는 음표를 멀리·가까이 미는 데 쓴다).")]
        [SerializeField, Range(0f, 1f)] private float placingPitchScale = 0.1f;
        public float PitchScale => Placing ? placingPitchScale : 1f;

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            self = GetComponent<CharacterIdentity>();
            placement.orbitWhileTurning = false; // 안토니오: 자유롭게 놓기
            placement.dragWhileTurning = true;   // 시점을 돌리며 고를 때: 마우스로 음표 자리를 직접 민다(감도 높게)
        }

        private void OnDisable()
        {
            Placing = false; placement.Clear(); waitRelease = false;
            notes.Clear(); Linked = false; PendingTimeLeft = 0f;
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

            if (input.Skill && !blocked)
            {
                // 좌클릭을 꾹 눌러 놓을 자리를 고르는 중(차징)이면 E = 고르기 취소(음표를 놓지 않는다, 뗄 때까지 무시).
                // 고르는 중이 아니면 E = 바로 앞에 즉시.
                if (Placing) { Placing = false; placement.Clear(); waitRelease = true; }
                else PlaceInFront();
            }
            else if (waitRelease)
            {
                if (!input.HeavyCharging) waitRelease = false;
            }
            else if (input.HeavyCharging && !blocked)
            {
                if (!Placing) placement.Begin();
                Placing = true;
                placement.Update(transform, input);
            }
            else if (Placing)
            {
                // 뗐다 → 놓을 수 있는 자리면 놓고, 몸 근처면 취소
                if (!placement.Cancels && placement.Valid && !blocked) AddNote(placement.Point);
                Placing = false; placement.Clear();
            }

            // 첫 음표만 있으면 시간이 간다. 다 되면 끊기고 첫 음표가 사라진다.
            if (notes.Count == 1 && !Linked)
            {
                PendingTimeLeft -= dt;
                if (PendingTimeLeft <= 0f) { notes.Clear(); PendingTimeLeft = 0f; }
            }

            if (Linked && notes.Count == 2) CheckWireHits();
        }

        private void PlaceInFront()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 p = transform.position + fwd * instantDistance;
            if (placement.GroundAt(transform, p, out Vector3 point, out Vector3 normal) && normal.y >= placement.minGroundNormalY)
                AddNote(point);
        }

        private void AddNote(Vector3 point)
        {
            if (notes.Count == 0 || Linked)
            {
                // 새 쌍의 첫 음표(지난 쌍은 사라진다)
                notes.Clear(); Linked = false;
                notes.Add(point);
                PendingTimeLeft = linkWindow;
                lastHitAt.Clear();
            }
            else
            {
                // 둘째 음표 → 전류선으로 이어진다
                notes.Add(point);
                Linked = true;
                PendingTimeLeft = 0f;
            }
        }

        /// <summary>두 음표 사이(양 끝 음표 포함) 굵은 캡슐에 닿은 상대 Hurtbox → 맞힘. 상대마다 rehitInterval에 한 번.</summary>
        private void CheckWireHits()
        {
            Vector3 a = notes[0] + Vector3.up * noteHeight, b = notes[1] + Vector3.up * noteHeight;
            int n = Physics.OverlapCapsuleNonAlloc(a, b, wireRadius, overlaps, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            hitThisTick.Clear();
            for (int i = 0; i < n; i++)
            {
                if (!overlaps[i].TryGetComponent(out Hurtbox hurt) || !hurt.isActiveAndEnabled) continue;
                var victim = hurt.Owner;
                if (victim == null || (self != null && !self.CanHit(victim)) || hitThisTick.Contains(victim.Id)) continue;
                if (lastHitAt.TryGetValue(victim.Id, out float t) && Time.time - t < rehitInterval) continue;
                hitThisTick.Add(victim.Id);
                lastHitAt[victim.Id] = Time.time;
                Vector3 c = overlaps[i].bounds.center;
                Vector3 ab = b - a;
                Vector3 onWire = a + ab * Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                LastHitPoint = onWire; LastHitTime = Time.time;
                HitEvents.Raise(new HitResult
                {
                    AttackerId = self != null ? self.Id : 0,
                    VictimId = victim.Id,
                    Part = hurt.Part,
                    Slot = slot,
                    AttackId = -(200000 + ++hitCounter),
                    Tick = SimulationTick.Current,
                    Point = onWire,
                });
            }
        }
    }
}

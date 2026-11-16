using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 판자(생존자 전용 기물, BE): 맵에 세워져 있다가 생존자가 옆에서 스페이스바를 누르면(PalletUser) 생존자 반대쪽으로 넘어간다.
    /// - 세워진 판자: 루트 = 바닥 가운데, 위(+Y) = 판자 길이, 옆(+X) = 폭, 앞(+Z) = 판자 면. 판자 몸(Board 자식)에 막는 상자 판정.
    /// - 넘어가기: dropDelay초 뒤(밀어 넘기는 모션의 미는 순간) 바닥 모서리를 축으로 fallTime초 동안 점점 빠르게 넘어가 눕고, 살짝 튀었다 가라앉는다.
    /// - 넘어가는 판자에 깔린 감시자(눕는 자리 + 넘어가는 동안 판자 끝이 지나가는 자리): 피해(DamageRules.PalletDamage)와 경직(DamageRules.PalletStun).
    ///   한 번 넘어간 판자는 누운 채로 남는다(다시 못 씀). 생존자는 깔리지 않는다.
    /// 모양(모델)은 PalletVisual(FE)이 판자 몸에 붙인다. 배치는 PalletSpawner(맵).
    /// </summary>
    [DisallowMultipleComponent]
    public class Pallet : MonoBehaviour
    {
        public enum PalletState { Up, Falling, Down }

        [Header("크기(m)")]
        [SerializeField, Min(0.3f)] private float height = 2.1f;
        [SerializeField, Min(0.3f)] private float width = 1.5f;
        [SerializeField, Min(0.05f)] private float thickness = 0.3f;

        [Header("넘어가기")]
        [Tooltip("스페이스바를 누른 뒤 판자가 넘어가기 시작할 때까지(초) — 두 손으로 잡았다 미는 순간.")]
        [SerializeField, Min(0f)] private float dropDelay = 0.3f;
        [Tooltip("넘어가는 시간(초). 점점 빨라진다(떨어지듯).")]
        [SerializeField, Min(0.05f)] private float fallTime = 0.32f;
        [Tooltip("땅에 닿은 뒤 살짝 튀는 각도(도)와 시간(초).")]
        [SerializeField, Min(0f)] private float bounceAngle = 6f, bounceTime = 0.18f;
        [Tooltip("판자가 이 각도(도)를 넘어 기울면 깔림 판정을 시작한다.")]
        [SerializeField, Range(10f, 90f)] private float hitFromAngle = 45f;

        [Header("생존자가 내릴 수 있는 거리")]
        [Tooltip("판자 면에서 앞·뒤로 이 거리(m) 안이면 내릴 수 있다.")]
        [SerializeField, Min(0.3f)] private float useRange = 1.6f;

        private static readonly List<Pallet> all = new();
        private static readonly Collider[] buffer = new Collider[32];

        private Transform board;
        private BoxCollider boardCollider;
        private float sign = 1f;         // +1 = 앞(+Z)으로 넘어감, -1 = 뒤로
        private float startTime;
        private CharacterIdentity dropper;
        private readonly HashSet<int> hitIds = new();

        public PalletState State { get; private set; } = PalletState.Up;
        public float Height => height;
        public float Width => width;
        public float Thickness => thickness;
        /// <summary>판자 몸(넘어갈 때 도는 자식). 모델은 여기에 붙인다.</summary>
        public Transform Board { get { if (board == null) Build(); return board; } }
        /// <summary>넘어가는 쪽(월드, 수평). 세워져 있으면 앞(+Z).</summary>
        public Vector3 FallDirection => transform.forward * sign;
        public static IReadOnlyList<Pallet> All => all;

        private void Awake() { if (board == null) Build(); }
        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Build()
        {
            var go = new GameObject("Board");
            go.transform.SetParent(transform, false);
            board = go.transform;
            boardCollider = go.AddComponent<BoxCollider>();
            boardCollider.center = new Vector3(0f, height * 0.5f, 0f);
            boardCollider.size = new Vector3(width, height, thickness);
        }

        /// <summary>
        /// position(생존자 발)에서 forward를 보고 있을 때 내릴 수 있는 세워진 판자 중 가장 가까운 것. 판자는 생존자 반대쪽으로 넘어간다(fallSign).
        /// </summary>
        public static bool TryFind(Vector3 position, Vector3 forward, out Pallet pallet, out float fallSign)
        {
            pallet = null; fallSign = 1f;
            float best = float.MaxValue;
            foreach (var p in all)
            {
                if (p == null || p.State != PalletState.Up) continue;
                Vector3 local = p.transform.InverseTransformPoint(position);
                if (Mathf.Abs(local.x) > p.width * 0.5f + 0.4f || local.y < -1f || local.y > p.height) continue;
                float side = Mathf.Abs(local.z);
                if (side > p.useRange + p.thickness * 0.5f) continue;
                float s = local.z >= 0f ? -1f : 1f;               // 생존자 반대쪽으로 넘어간다
                Vector3 toBoard = p.transform.forward * s;         // 생존자 → 판자 방향
                Vector3 f = Vector3.ProjectOnPlane(forward, Vector3.up);
                if (f.sqrMagnitude > 1e-4f && Vector3.Dot(f.normalized, toBoard) < 0.2f) continue; // 판자 쪽을 대충이라도 봐야 한다
                if (side < best) { best = side; pallet = p; fallSign = s; }
            }
            return pallet != null;
        }

        /// <summary>판자를 내린다(넘어가는 쪽 = fallSign). 이미 내렸으면 false.</summary>
        public bool BeginDrop(CharacterIdentity by, float fallSign)
        {
            if (State != PalletState.Up) return false;
            dropper = by;
            sign = fallSign >= 0f ? 1f : -1f;
            startTime = Time.time;
            hitIds.Clear();
            State = PalletState.Falling;
            return true;
        }

        private void FixedUpdate()
        {
            if (State != PalletState.Falling) return;
            float t = Time.time - startTime - dropDelay;
            float angle;
            if (t <= 0f) angle = 0f;
            else if (t < fallTime) { float u = t / fallTime; angle = 90f * u * u; }   // 점점 빠르게
            else
            {
                float b = (t - fallTime) / Mathf.Max(bounceTime, 1e-3f);
                angle = b < 1f ? 90f - bounceAngle * Mathf.Sin(b * Mathf.PI) : 90f; // 쿵 — 살짝 튀었다 가라앉음
                if (b >= 1f) State = PalletState.Down;
            }
            SetAngle(angle);
            if (angle >= hitFromAngle && t <= fallTime + 0.02f) CrushUnder(angle);
        }

        /// <summary>바닥 모서리(넘어가는 쪽)를 축으로 angle도 기울인다.</summary>
        private void SetAngle(float angle)
        {
            Vector3 hinge = new(0f, 0f, sign * thickness * 0.5f);
            Quaternion r = Quaternion.Euler(sign * angle, 0f, 0f);
            board.localRotation = r;
            board.localPosition = hinge + r * -hinge;
        }

        /// <summary>판자가 지나가는(눕는) 자리의 감시자를 맞힌다. 판자 끝 쪽 상자로 본다.</summary>
        private void CrushUnder(float angle)
        {
            // 판자 몸 기준: 판자 위쪽 절반을 조금 넓고 두껍게 덮는 상자(쓰러지며 내리치는 부분)
            Vector3 center = board.TransformPoint(new Vector3(0f, height * 0.65f, 0f));
            Vector3 half = new(width * 0.5f + 0.25f, height * 0.35f + 0.2f, thickness * 0.5f + 0.45f);
            int n = Physics.OverlapBoxNonAlloc(center, half, buffer, board.rotation, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                if (!buffer[i].TryGetComponent(out Hurtbox hb) || !hb.isActiveAndEnabled) continue;
                var victim = hb.Owner;
                if (victim == null || victim == dropper || !hitIds.Add(victim.Id)) continue;
                if (!DamageRules.IsHunter(victim.gameObject)) { hitIds.Remove(victim.Id); continue; } // 생존자는 깔리지 않는다
                if (victim.TryGetComponent(out ICharacterHealth hp) && hp.IsDead) continue;
                var hit = new HitResult
                {
                    AttackerId = dropper != null ? dropper.Id : 0,
                    VictimId = victim.Id,
                    Part = BodyPart.Body,
                    Slot = HitboxSlot.Weapon,
                    AttackId = -(400000 + GetInstanceID() % 100000),
                    Tick = SimulationTick.Current,
                    Point = hb.Collider.ClosestPoint(center),
                    Kind = AttackKind.Skill,
                    BaseDamage = DamageRules.PalletDamage,
                };
                HitEvents.Raise(hit);
                CharacterStun.Apply(victim.gameObject, DamageRules.PalletStun, FallDirection);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, thickness));
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.9f, 0f), new Vector3(width + 0.8f, 1.8f, (useRange + thickness * 0.5f) * 2f));
        }
    }
}

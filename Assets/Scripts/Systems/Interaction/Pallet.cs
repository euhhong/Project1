using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 판자(생존자 전용 기물, BE): 통로·상자 사이·빈 문틀 한쪽에 기대 세워져 있다가, 생존자가 옆(통로 안)에서 스페이스바를 누르면(PalletUser)
    /// 틈을 가로질러 앞(+Z)으로 넘어가 길을 막는다(넘어가는 쪽은 늘 같음 — 생존자가 어디 서 있든).
    /// - 세워진 판자: 루트 = 바닥 가운데(틈 한쪽 벽에 붙음), 위(+Y) = 판자 길이(= 틈 너비, Configure), 옆(+X) = 폭(통로 방향), 앞(+Z) = 넘어가는 쪽. 판자 몸(Board 자식)에 막는 상자 판정.
    /// - 넘어가기: dropDelay초 뒤(밀어 넘기는 모션의 미는 순간) 바닥 모서리를 축으로 fallTime초 동안 점점 빠르게 넘어가, 바닥에 눕지 않고 끝이 반대편 벽(틈 건너편)
    ///   restHeight 높이에 걸려 비스듬히 멈춘다(판자 길이 = √(틈² + restHeight²), Configure). 걸친 판자는 틈을 blockHeight까지 막고, 스페이스바로 넘어간다(창틀 넘기 모션 — CharacterTraversal).
    /// - 넘어가는 동안 틈 전체(바닥 ~ hitHeight 3.5m 공중까지)에 있는 감시자: 피해(DamageRules.PalletDamage)와 경직(DamageRules.PalletStun) — 뛰어오른 감시자도 맞는다.
    ///   밀려나지 않고, 판자 반대편(내린 생존자 반대쪽) 통로 정가운데로 옮겨져 판자를 마주 본 채 고정되어 기절한다(PinOnFarSide).
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
        [SerializeField, Min(0f)] private float dropDelay = 0.18f;
        [Tooltip("넘어가는 시간(초). 점점 빨라진다(떨어지듯).")]
        [SerializeField, Min(0.05f)] private float fallTime = 0.22f;
        [Tooltip("땅에 닿은 뒤 살짝 튀는 각도(도)와 시간(초).")]
        [SerializeField, Min(0f)] private float bounceAngle = 6f, bounceTime = 0.18f;
        [Tooltip("넘어간 판자 끝이 반대편 벽(틈 건너편 면)에 걸리는 높이(m) — 바닥에 완전히 눕지 않고 비스듬히 걸쳐 멈춘다.")]
        [SerializeField, Min(0.2f)] private float restHeight = 0.9f;
        [Tooltip("틈 너비(m) = 넘어가 걸치는 가로 거리. Configure가 정한다(판자 길이 = √(틈² + restHeight²)).")]
        [SerializeField, Min(0f)] private float gap;
        [Tooltip("넘어가는 동안 깔림 판정 높이(m, 바닥부터) — 틈 위 공중(뛰어오른 감시자)까지 다 맞힌다.")]
        [SerializeField, Min(0.5f)] private float hitHeight = 3.5f;
        [Tooltip("넘어간 판자가 틈을 막는 높이(m) — 이보다 낮게는 지나갈 수 없고 넘어가기(스페이스바)로 넘는다.")]
        [SerializeField, Min(0.3f)] private float blockHeight = 1.0f;

        [Header("생존자가 내릴 수 있는 거리")]
        [Tooltip("판자 면에서 앞·뒤로 이 거리(m) 안이면 내릴 수 있다.")]
        [SerializeField, Min(0.3f)] private float useRange = 1.6f;
        [Tooltip("판자 옆(통로 방향)으로 이 거리(m) 안이면 내릴 수 있다 — 판자를 지나쳐 달아나는 중(판자 뒤)에도 닿게 넉넉히.")]
        [SerializeField, Min(0.3f)] private float sideRange = 2.4f;

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

        /// <summary>틈 너비(m)·넘어가 걸친 각도(세운 자리에서, 도).</summary>
        public float Gap => gap > 0.01f ? gap : height * 0.9f;
        private float RestAngle => Mathf.Asin(Mathf.Clamp01(Gap / Mathf.Max(height, 0.01f))) * Mathf.Rad2Deg;

        /// <summary>틈 너비(m)를 정한다(배치할 때). 판자 길이 = √(틈² + restHeight²) — 넘어가면 끝이 반대편 벽 restHeight 높이에 걸린다.</summary>
        public void Configure(float gapWidth)
        {
            gap = Mathf.Max(0.3f, gapWidth);
            height = Mathf.Sqrt(gap * gap + restHeight * restHeight);
            if (board == null) return; // 아직 판자 몸이 없으면(편집 화면에서 배치할 때) 길이만 — 게임을 시작하면 Awake가 이 길이로 만든다
            boardCollider.center = new Vector3(0f, height * 0.5f, 0f);
            boardCollider.size = new Vector3(width, height, thickness);
        }

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
        /// position(생존자 발)에서 내릴 수 있는 세워진 판자 중 가장 가까운 것: 판자 옆(통로 쪽 sideRange m)이나 앞(틈 안).
        /// 판자가 몸 앞쪽 반원(앞 180°, forward 기준) 안에 있을 때만 — 등진 판자는 내리지 않는다. 넘어가는 쪽은 늘 앞(fallSign = +1).
        /// 이 범위 안에서는 생존자가 점프하지 않는다(스페이스바 = 판자 내리기 — PalletUser.BlocksJump). 등진 판자 옆에서는 그냥 점프.
        /// </summary>
        public static bool TryFind(Vector3 position, Vector3 forward, out Pallet pallet, out float fallSign)
        {
            pallet = null; fallSign = 1f;
            float best = float.MaxValue;
            forward.y = 0f;
            foreach (var p in all)
            {
                if (p == null || p.State != PalletState.Up) continue;
                Vector3 local = p.transform.InverseTransformPoint(position);
                if (Mathf.Abs(local.x) > p.width * 0.5f + p.sideRange || local.y < -1f || local.y > p.height) continue;
                if (local.z < -0.35f || local.z > Mathf.Max(p.height, p.useRange) + 0.6f) continue; // 벽 뒤는 안 됨, 틈 안·판자 옆(앞뒤)만
                // 앞 180°: 판자(서 있는 판 바닥 자리)에서 가장 가까운 점이 몸 앞쪽(바라보는 방향과 내적 ≥ 0)이어야 — 바짝 붙어 있으면 된다
                Vector3 near = p.transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, -p.width * 0.5f, p.width * 0.5f), 0f, Mathf.Clamp(local.z, 0f, p.thickness)));
                Vector3 to = near - position; to.y = 0f;
                if (to.sqrMagnitude > 0.25f * 0.25f && forward.sqrMagnitude > 1e-4f && Vector3.Dot(forward, to) < 0f) continue;
                float dist = new Vector2(local.x, local.z - 0.3f).magnitude;
                if (dist < best) { best = dist; pallet = p; fallSign = 1f; }
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
            crushTop = MeasureCeiling();
            State = PalletState.Falling;
            return true;
        }

        private float crushTop = AirCrushHeight;
        private static readonly RaycastHit[] ceilingHits = new RaycastHit[16];

        /// <summary>틈 가운데 위로 천장(위층 바닥)까지 높이 — 깔림 판정이 위층의 감시자까지 닿지 않게. 캐릭터·이 판자는 무시. 없으면 AirCrushHeight.</summary>
        private float MeasureCeiling()
        {
            Vector3 from = transform.TransformPoint(new Vector3(0f, 0.3f, sign * Gap * 0.5f));
            int n = Physics.RaycastNonAlloc(from, Vector3.up, ceilingHits, AirCrushHeight, ~CombatLayers.HurtboxMask, QueryTriggerInteraction.Ignore);
            float top = AirCrushHeight;
            for (int i = 0; i < n; i++)
            {
                var c = ceilingHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<CharacterIdentity>() != null) continue;
                top = Mathf.Min(top, ceilingHits[i].distance + 0.3f);
            }
            return Mathf.Max(top, hitHeight);
        }

        private void FixedUpdate()
        {
            if (State != PalletState.Falling) return;
            float t = Time.time - startTime - dropDelay;
            float rest = RestAngle; // 바닥까지 눕지 않고 반대편 벽에 걸리는 각도
            float angle;
            if (t <= 0f) angle = 0f;
            else if (t < fallTime) { float u = t / fallTime; angle = rest * u * u; }   // 점점 빠르게
            else
            {
                float b = (t - fallTime) / Mathf.Max(bounceTime, 1e-3f);
                angle = b < 1f ? rest - bounceAngle * 0.5f * Mathf.Sin(b * Mathf.PI) : rest; // 탁 — 벽에 걸려 살짝 튀었다 멈춤
                if (b >= 1f) { State = PalletState.Down; BuildBlocker(); }
            }
            SetAngle(angle);
            if (t > -0.05f && t <= fallTime + bounceTime + 0.05f) CrushUnder(angle); // 넘어가는 내내(튀는 동안까지): 틈 위 공중까지 다 맞힌다
        }

        /// <summary>넘어간 판자가 틈을 막는 상자(바닥 ~ blockHeight, 틈 전체) — 넘어가기(CharacterTraversal)로만 지나간다.</summary>
        private void BuildBlocker()
        {
            var go = new GameObject("Blocker");
            go.transform.SetParent(transform, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, blockHeight * 0.5f, sign * Gap * 0.5f);
            box.size = new Vector3(width, blockHeight, Mathf.Max(0.2f, Gap - 0.1f));
        }

        /// <summary>
        /// 넘어간(걸친) 판자를 넘어갈 수 있으면 그 판자와 반대편 착지 자리: 발(position)이 틈 안(판자 옆, 통로 쪽 1.3m 안)이고 판자 쪽으로 움직일 때.
        /// 착지 = 판자 건너편 통로(판자 폭 + 몸 반지름 + 0.35m).
        /// </summary>
        public static bool TryFindVault(Vector3 position, Vector3 moveDir, float bodyRadius, out Pallet pallet, out Vector3 landing)
        {
            pallet = null; landing = default;
            float best = float.MaxValue;
            foreach (var p in all)
            {
                if (p == null || p.State != PalletState.Down) continue;
                Vector3 local = p.transform.InverseTransformPoint(position);
                float across = local.z * p.sign;                        // 틈을 가로지르는 자리(0 = 판자를 세웠던 벽, Gap = 반대편 벽)
                if (across < 0.05f || across > p.Gap - 0.05f || local.y < -1f || local.y > 1.5f) continue;
                float side = Mathf.Abs(local.x) - p.width * 0.5f;      // 판자 옆으로 떨어진 거리
                if (side < -0.2f || side > 1.3f) continue;
                Vector3 toward = p.transform.TransformDirection(new Vector3(-Mathf.Sign(local.x), 0f, 0f));
                if (Vector3.Dot(Vector3.ProjectOnPlane(moveDir, Vector3.up).normalized, toward) < 0.5f) continue; // 판자 쪽으로 갈 때만
                if (side >= best) continue;
                best = side; pallet = p;
                landing = p.transform.TransformPoint(new Vector3(Mathf.Sign(local.x) * -(p.width * 0.5f + bodyRadius + 0.35f), 0f, local.z));
            }
            return pallet != null;
        }

        /// <summary>바닥 모서리(넘어가는 쪽)를 축으로 angle도 기울인다.</summary>
        private void SetAngle(float angle)
        {
            Vector3 hinge = new(0f, 0f, sign * thickness * 0.5f);
            Quaternion r = Quaternion.Euler(sign * angle, 0f, 0f);
            board.localRotation = r;
            board.localPosition = hinge + r * -hinge;
        }

        /// <summary>넘어가는 판자가 쓸고 지나가는 자리의 감시자를 맞힌다: 틈 전체(판자를 세웠던 벽 ~ 반대편 벽) × 판자 폭, 바닥부터 hitHeight까지(공중 포함).</summary>
        /// <summary>깔림 판정 높이: 틈 위 천장(위층 바닥)까지, 천장이 없으면 AirCrushHeight까지 — 공중의 감시자(점프·도약·내려찍기 중)도 맞는다. 씬의 hitHeight보다 낮아지지는 않는다. 바닥 0.5m 아래부터.</summary>
        private float CrushHeight => Mathf.Max(hitHeight, crushTop) + 0.5f;
        private const float AirCrushHeight = 15f;

        private void CrushUnder(float angle)
        {
            float h = CrushHeight; // 공중(점프·도약 중)의 감시자도 맞도록 높게
            Vector3 center = transform.TransformPoint(new Vector3(0f, h * 0.5f - 0.5f, sign * Gap * 0.5f));
            Vector3 half = new(width * 0.5f + 0.25f, h * 0.5f, Gap * 0.5f + 0.25f);
            int n = Physics.OverlapBoxNonAlloc(center, half, buffer, transform.rotation, CombatLayers.HurtboxMask, QueryTriggerInteraction.Collide);
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
                    AttackId = -(400000 + (GetEntityId().GetHashCode() & 0x7fffffff) % 100000),
                    Tick = SimulationTick.Current,
                    Point = hb.Collider.ClosestPoint(center),
                    Kind = AttackKind.Skill,
                    BaseDamage = DamageRules.PalletDamage,
                    NoKnockback = true, // 판자에 맞아도 밀려나지 않는다 — 대신 판자 반대편 가운데로 끌려가 고정된 채 기절
                };
                HitEvents.Raise(hit);
                PinOnFarSide(victim.gameObject);
                CharacterStun.Apply(victim.gameObject, DamageRules.PalletStun, Vector3.zero);
            }
        }

        /// <summary>
        /// 깔린 감시자를 판자 반대편(내린 생존자 반대쪽 통로) 정가운데로 옮긴다 — 판자를 마주 보게. 내린 사람을 모르면 지금 있던 쪽.
        /// 자리가 막혀 있으면(몸이 들어갈 곳이 없으면) 옮기지 않는다.
        /// </summary>
        private void PinOnFarSide(GameObject victim)
        {
            Vector3 gapCenter = transform.TransformPoint(new Vector3(0f, 0f, sign * Gap * 0.5f));
            Vector3 lane = transform.right; lane.y = 0f; lane.Normalize();
            float side = dropper != null ? -Mathf.Sign(Vector3.Dot(dropper.transform.position - gapCenter, lane))
                                         : Mathf.Sign(Vector3.Dot(victim.transform.position - gapCenter, lane));
            if (side == 0f) side = 1f;
            var cc = victim.GetComponent<CharacterController>();
            float r = cc != null ? cc.radius : 0.4f;
            Vector3 spot = gapCenter + lane * side * (width * 0.5f + r + 0.15f);
            spot.y = transform.position.y;
            if (cc != null)
            {
                Vector3 p1 = spot + Vector3.up * (r + 0.1f), p2 = spot + Vector3.up * Mathf.Max(r + 0.1f, cc.height - r);
                foreach (var c in Physics.OverlapCapsule(p1, p2, r * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.GetComponentInParent<CharacterIdentity>() == null && c.GetComponentInParent<Pallet>() == null) return; // 자리가 막힘
                cc.enabled = false;
            }
            victim.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(-lane * side, Vector3.up)); // 판자를 마주 봄
            if (cc != null) cc.enabled = true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, thickness));
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.25f); // 넘어갈 때 맞는 자리(공중 포함)
            Gizmos.DrawWireCube(new Vector3(0f, CrushHeight * 0.5f - 0.5f, Gap * 0.5f), new Vector3(width + 0.5f, CrushHeight, Gap + 0.5f));
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
            Gizmos.DrawWireCube(new Vector3(0f, 0.9f, 0f), new Vector3(width + 0.8f, 1.8f, (useRange + thickness * 0.5f) * 2f));
        }
    }
}

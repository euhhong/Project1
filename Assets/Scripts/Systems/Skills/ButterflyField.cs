using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코)의 나비 놓기·겨누기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신) 마우스가 가리키는 곳에 놓을 자리를 보여 준다(Placing, PreviewPoint).
    ///   땅 위만 된다(면이 위를 향함, 공중·벽 불가). 떼면 그 자리에 나비를 놓는다(재사용 대기 placeCooldown, 테스트 0초).
    ///   취소: 누른 채 E를 누르거나(뗄 때까지 무시), 몸 근처(cancelRadius)로 끌어다 놓으면 놓지 않는다 — 취소는 재사용 대기가 생기지 않는다.
    ///   누른 채 우클릭으로 시점을 돌리면: 캐릭터를 원의 가운데로, 나비까지 거리를 반지름으로 두고 시야 기준 방향을 지켜 시점을 따라 돈다(그 자리 바닥에 붙음).
    ///   우클릭으로 시점을 돌리는 중에 좌클릭을 꾹 눌러도 꺼낼 수 있다(화면 가운데가 가리키는 땅, 없으면 앞쪽 defaultDistance).
    /// - 나비는 최대 maxButterflies마리, 넘으면 가장 오래된 것이 사라진다.
    /// - 늘 바라보는 방향(시야)에 가장 맞는 나비를 겨눈다(TargetIndex). E를 누르면 ButterflyGlideSkill이 그 나비로 간다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 붉은나비만). 그리기는 FE(ButterflyVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class ButterflyField : MonoBehaviour, IButterflyField, IAttackHoldOverride
    {
        [Tooltip("놓아 둘 수 있는 나비 수. 넘으면 가장 오래된 나비가 사라진다.")]
        [SerializeField, Min(1)] private int maxButterflies = 3;
        [Tooltip("땅으로 볼 면의 기울기(면 방향의 위쪽 성분). 이보다 낮으면(벽 등) 못 놓는다.")]
        [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.7f;
        [Tooltip("놓을 수 있는 최대 거리(m, 수평).")]
        [SerializeField, Min(1f)] private float maxPlaceDistance = 40f;
        [Tooltip("겨눌 수 있는 시야 각도(도, 바라보는 방향에서).")]
        [SerializeField, Range(1f, 90f)] private float maxTargetAngle = 35f;
        [Tooltip("겨눌 수 있는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float maxTargetDistance = 60f;
        [Tooltip("이보다 가까운 나비는 겨누지 않는다(이미 거기 있음, m).")]
        [SerializeField, Min(0f)] private float minTargetDistance = 1.2f;
        [Tooltip("시야를 재는 눈 높이(m).")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.7f;
        [Tooltip("몸 근처(수평 거리, m)로 끌어다 놓으면 놓기를 취소한다(재사용 대기도 생기지 않는다).")]
        [SerializeField, Min(0f)] private float cancelRadius = 1.5f;
        [Tooltip("나비를 놓은 뒤 다음 나비를 놓기까지 재사용 대기(초). 테스트 단계 0초. 취소하면 생기지 않는다.")]
        [SerializeField, Min(0f)] private float placeCooldown = 0f;

        private readonly List<Vector3> butterflies = new();
        private ICharacterInputSource inputSource;
        private ICharacterSkillState skillState;
        private ICharacterStun stun;
        private bool waitRelease;
        // 시점을 돌리는 동안: 시야 기준 나비 자리(캐릭터에서, 수평)를 지켜 시점을 따라 돌린다.
        [Tooltip("시점을 따라 돈 자리에서 바닥을 찾을 때 위·아래로 살피는 거리(m).")]
        [SerializeField, Min(0.1f)] private float groundProbeUp = 3f, groundProbeDown = 8f;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private bool previewSet, viewLocked;
        private Vector3 viewLocal;
        private float lastAimYaw;
        [Tooltip("시점을 돌리는 중에 새로 꺼냈는데 화면 가운데가 땅을 가리키지 않으면(하늘 등) 앞쪽 이 거리(m)에 꺼낸다.")]
        [SerializeField, Min(0f)] private float defaultDistance = 6f;

        public IReadOnlyList<Vector3> Butterflies => butterflies;
        public bool Placing { get; private set; }
        public Vector3 PreviewPoint { get; private set; }
        public bool PreviewValid { get; private set; }
        public bool PreviewCancels { get; private set; }
        public float PlaceCooldownRemaining { get; private set; }
        public float PlaceCooldownDuration => placeCooldown;
        public int TargetIndex { get; private set; } = -1;
        public bool HasTarget => TargetIndex >= 0 && TargetIndex < butterflies.Count;
        public Vector3 TargetPoint => HasTarget ? butterflies[TargetIndex] : Vector3.zero;
        /// <summary>놓기를 E로 취소한 틱. 같은 틱의 E로 나비 스킬이 나가지 않게 ButterflyGlideSkill이 본다.</summary>
        public int CancelTick { get; private set; } = int.MinValue;
        /// <summary>놓기를 E로 취소한 뒤 좌클릭을 아직 떼지 않았다(이 동안 E로 나비 스킬이 나가지 않는다).</summary>
        public bool WaitingRelease => waitRelease;
        public bool OverridesHeavyHold => isActiveAndEnabled;

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
        }

        private void OnDisable()
        {
            Placing = false; PreviewValid = false; PreviewCancels = false; waitRelease = false;
        }

        private void FixedUpdate()
        {
            if (inputSource == null) Awake();
            if (inputSource == null) return;
            CharacterInputFrame input = inputSource.ReadInput();
            if (skillState == null) TryGetComponent(out skillState);
            if (stun == null) TryGetComponent(out stun);
            bool blocked = (skillState != null && skillState.LocksActions) || (stun != null && stun.IsStunned);
            if (PlaceCooldownRemaining > 0f) PlaceCooldownRemaining = Mathf.Max(0f, PlaceCooldownRemaining - Time.fixedDeltaTime);

            if (waitRelease)
            {
                if (!input.HeavyCharging) waitRelease = false; // 취소한 뒤 키를 뗄 때까지 무시
            }
            else if (input.HeavyCharging && !blocked && (Placing || PlaceCooldownRemaining <= 0f))
            {
                if (!Placing) { PreviewValid = false; PreviewCancels = false; previewSet = false; viewLocked = false; } // 새로 누르기 시작: 지난 자리는 버린다
                Placing = true;
                UpdatePreview(input);
                if (input.Skill)
                {
                    // 누른 채 E → 취소
                    Placing = false; PreviewValid = false; PreviewCancels = false; waitRelease = true;
                    CancelTick = SimulationTick.Current;
                }
            }
            else if (Placing)
            {
                // 뗐다(또는 막힘) → 몸 근처면 취소(재사용 대기 없음), 놓을 수 있는 자리면 놓고 재사용 대기
                if (!PreviewCancels && PreviewValid && !blocked)
                {
                    Place(PreviewPoint);
                    PlaceCooldownRemaining = placeCooldown;
                }
                Placing = false; PreviewValid = false; PreviewCancels = false;
            }

            UpdateTarget(input);
        }

        private void UpdatePreview(in CharacterInputFrame input)
        {
            Quaternion view = Quaternion.Euler(0f, input.LookYaw, 0f);
            if (!(input.HasAimPoint && input.AimFromCursor))
            {
                // 커서가 가리키는 곳이 아님(우클릭으로 시점을 돌리는 중 등).
                if (!previewSet)
                {
                    // 시점을 돌리는 중에 새로 꺼냄: 화면 가운데가 가리키는 땅, 없으면(하늘 등) 앞쪽 defaultDistance m 바닥.
                    if (input.HasAimPoint) Evaluate(input.AimPoint, input.AimNormal);
                    else
                    {
                        Vector3 ahead = transform.position + view * Vector3.forward * defaultDistance;
                        if (GroundAt(ahead, out Vector3 gp, out Vector3 gn)) Evaluate(gp, gn);
                        else return;
                    }
                    lastAimYaw = input.LookYaw;
                    viewLocked = false;
                    return;
                }
                // 이미 꺼낸 자리: 캐릭터를 원의 가운데로, 나비까지 거리를 반지름으로 보고 시야 기준 방향을 그대로 지킨 채 시점을 따라 돌린다.
                if (!viewLocked)
                {
                    Vector3 off = PreviewPoint - transform.position; off.y = 0f;
                    viewLocal = Quaternion.Inverse(Quaternion.Euler(0f, lastAimYaw, 0f)) * off; // 마지막으로 겨눴을 때 시야 기준(돌리기 시작한 틱의 회전도 놓치지 않게)
                    viewLocked = true;
                }
                Vector3 p = transform.position + view * viewLocal;
                if (GroundAt(p, out Vector3 point, out Vector3 normal)) Evaluate(point, normal);
                else { PreviewPoint = p; PreviewCancels = false; PreviewValid = false; } // 아래에 땅이 없음
                return;
            }
            viewLocked = false;
            lastAimYaw = input.LookYaw;
            Evaluate(input.AimPoint, input.AimNormal);
        }

        private void Evaluate(Vector3 point, Vector3 normal)
        {
            PreviewPoint = point;
            previewSet = true;
            Vector3 flat = point - transform.position; flat.y = 0f;
            PreviewCancels = flat.magnitude <= cancelRadius; // 몸 근처 → 떼면 취소
            PreviewValid = !PreviewCancels && normal.y >= minGroundNormalY && flat.magnitude <= maxPlaceDistance;
        }

        /// <summary>p 바로 위에서 아래로 내려 처음 닿는 바닥(자기 몸·트리거 제외).</summary>
        private bool GroundAt(Vector3 p, out Vector3 point, out Vector3 normal)
        {
            point = p; normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(p + Vector3.up * groundProbeUp, Vector3.down, groundHits, groundProbeUp + groundProbeDown, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = groundHits[i];
                if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;
                best = h.distance; point = h.point; normal = h.normal; found = true;
            }
            return found;
        }

        private void Place(Vector3 point)
        {
            butterflies.Add(point);
            while (butterflies.Count > maxButterflies) butterflies.RemoveAt(0);
        }

        /// <summary>바라보는 방향(시야 Yaw·Pitch)과 가장 가까운 각도의 나비를 고른다(시야 각도·거리 안에서).</summary>
        private void UpdateTarget(in CharacterInputFrame input)
        {
            TargetIndex = -1;
            if (butterflies.Count == 0) return;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 look = Quaternion.Euler(input.LookPitch, input.LookYaw, 0f) * Vector3.forward;
            float bestAngle = maxTargetAngle;
            for (int i = 0; i < butterflies.Count; i++)
            {
                Vector3 to = butterflies[i] - eye;
                Vector3 flat = butterflies[i] - transform.position; flat.y = 0f;
                float d = to.magnitude;
                if (flat.magnitude < minTargetDistance || d > maxTargetDistance) continue;
                float angle = Vector3.Angle(look, to);
                if (angle <= bestAngle) { bestAngle = angle; TargetIndex = i; }
            }
        }
    }
}

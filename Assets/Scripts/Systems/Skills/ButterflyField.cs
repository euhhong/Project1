using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코)의 나비 놓기·겨누기(BE).
    /// - 좌클릭을 꾹 누르면(강공격 모으기 대신) 마우스가 가리키는 곳에 놓을 자리를 보여 준다(Placing, PreviewPoint — GroundPlacementPreview).
    ///   누르지 않을 때도 놓으면 갈 자리를 계산해 둔다(AimPoint — FE 조준 보조 고리). 최대 100m, 더 먼 곳을 가리키면 그 방향 100m로 당긴다.
    ///   땅 위만 된다(면이 위를 향함, 공중·벽 불가). 아래층에서 위층 바닥 끝(옆면·밑면)을 가리키면 그 윗면 모서리에 놓는다(ledgeSnapHeight),
    ///   캐릭터 발보다 maxRiseHeight(7m)를 넘게 높은 곳은 못 놓는다(1층 → 2층 바닥은 되고 지붕은 안 됨). 떼면 그 자리에 나비를 놓는다(재사용 대기 placeCooldown, 테스트 0초).
    ///   취소: 누른 채 E를 누르거나(뗄 때까지 무시), 몸 근처로 끌어다 놓으면 놓지 않는다 — 취소는 재사용 대기가 생기지 않는다.
    ///   누른 채 우클릭으로 시점을 돌리면: 캐릭터를 원의 가운데로, 나비까지 거리를 반지름으로 두고 시야 기준 방향을 지켜 시점을 따라 돈다.
    ///   우클릭으로 시점을 돌리는 중에 좌클릭을 꾹 눌러도 꺼낼 수 있다(화면 가운데가 가리키는 땅, 없으면 앞쪽).
    /// - 나비는 최대 maxButterflies마리, 넘으면 가장 오래된 것이 사라진다.
    ///   캐릭터가 설 공간(몸 크기 캡슐 빈 곳 + 바닥)만 있으면 벽 너머 안 보이는 곳에도 놓는다(가리킨 벽 너머로 광선을 이어 찾음, placeBeyondWalls).
    /// - 늘 바라보는 방향(시야)에 가장 맞는 나비를 겨눈다(TargetIndex). 벽 등에 가려 안 보이는 나비는 겨누지 않는다(targetNeedsSight — 도약 표적만 안 뜸).
    ///   단 나비를 놓은 직후 followJustThrownTime(0.5초) 안에는 방금 날린 나비가 무조건 표적(벽 너머여도 E로 따라감, 벽에 막히면 그대로 멈춤). E를 누르면 ButterflyGlideSkill이 그 나비로 간다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 붉은나비만). 그리기는 FE(ButterflyVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class ButterflyField : MonoBehaviour, IButterflyField, IAttackHoldOverride
    {
        [Tooltip("놓아 둘 수 있는 나비 수. 넘으면 가장 오래된 나비가 사라진다.")]
        [SerializeField, Min(1)] private int maxButterflies = 3;
        [Tooltip("놓을 자리 고르기(땅 위만, 몸 근처 = 취소, 시점을 돌리면 캐릭터 둘레로 돎).")]
        [SerializeField] private GroundPlacementPreview placement = new();
        [Tooltip("겨눌 수 있는 시야 각도(도, 바라보는 방향에서).")]
        [SerializeField, Range(1f, 90f)] private float maxTargetAngle = 35f;
        [Tooltip("겨눌 수 있는 최대 거리(m).")]
        [SerializeField, Min(1f)] private float maxTargetDistance = 60f;
        [Tooltip("이보다 가까운 나비는 겨누지 않는다(이미 거기 있음, m).")]
        [SerializeField, Min(0f)] private float minTargetDistance = 1.2f;
        [Tooltip("시야를 재는 눈 높이(m).")]
        [SerializeField, Min(0f)] private float eyeHeight = 1.7f;
        [Tooltip("나비를 놓은 뒤 다음 나비를 놓기까지 재사용 대기(초). 테스트 단계 0초. 취소하면 생기지 않는다.")]
        [SerializeField, Min(0f)] private float placeCooldown = 0f;

        private readonly List<Vector3> butterflies = new();
        private ICharacterInputSource inputSource;
        private ICharacterSkillState skillState;
        private ICharacterStun stun;
        private bool waitRelease;

        public IReadOnlyList<Vector3> Butterflies => butterflies;
        public bool Placing { get; private set; }
        public Vector3 PreviewPoint => placement.Point;
        public bool PreviewValid => Placing && placement.Valid;
        public bool PreviewCancels => Placing && placement.Cancels;
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

        [Tooltip("놓을 수 있는 최대 거리(m) / 시점을 돌리는 중 화면 가운데가 땅을 못 가리킬 때 앞쪽 거리(m).")]
        [SerializeField, Min(1f)] private float placeDistance = 100f, turnDefaultDistance = 25f;
        [Tooltip("위층 바닥 끝 모서리: 가리킨 곳이 바닥 판의 옆면·밑면이면(아래층에서는 윗면이 안 보임) 그 위 이 높이(m) 안의 윗면에 놓는다. 0 = 끔.")]
        [SerializeField, Min(0f)] private float ledgeSnapHeight = 1f;
        [Tooltip("놓을 자리가 캐릭터 발보다 이만큼(m) 넘게 높으면 못 놓는다(층고 5.5m — 1층에서 2층 바닥은 되고 그 위 지붕은 안 됨). 0 = 제한 없음.")]
        [SerializeField, Min(0f)] private float maxRiseHeight = 7f;
        [Tooltip("벽 너머 놓기: 가리킨 곳이 벽 등 놓을 수 없는 면이면 그 너머로 이어 처음으로 캐릭터가 설 공간(캡슐 크기 빈 곳 + 바닥)이 있는 자리에 놓는다. 보이는지는 따지지 않는다(도약 표적만 보이는 나비).")]
        [SerializeField] private bool placeBeyondWalls = true;
        [Tooltip("도약 표적: 눈에서 나비까지 벽 등에 가려지면 겨누지 않는다(E 표적 표시가 안 뜸). 나비 위 이 높이들(m) 중 하나라도 보이면 보인다.")]
        [SerializeField] private bool targetNeedsSight = true;
        [SerializeField] private float[] sightHeights = { 0.55f, 1.3f };
        [Tooltip("방금 날린 나비 따라가기: 나비를 놓은 직후 이 시간(초) 안에는 방금 날린 나비가 벽 너머(안 보임)·시야 밖·먼 곳이어도 도약 표적이 된다(E로 바로 따라감). 가다가 벽에 막히면 지금처럼 멈춘다.")]
        [SerializeField] private bool followJustThrown = true;
        [SerializeField, Min(0f)] private float followJustThrownTime = 0.5f;
        private float justThrownLeft;
        private static readonly RaycastHit[] SightHits = new RaycastHit[16];

        // 좌클릭을 누르지 않을 때도 지금 겨누는 대로 놓으면 나비가 갈 자리(조준 보조 고리).
        [SerializeField] private GroundPlacementPreview aim = new();
        public int PlacedCount { get; private set; }
        public bool AimShown { get; private set; }
        public Vector3 AimPoint => aim.Point;
        public bool AimValid => AimShown && aim.Valid;

        private void Awake()
        {
            inputSource = GetComponent<ICharacterInputSource>();
            ApplyPlacementSettings();
        }

        private void OnValidate() => ApplyPlacementSettings();

        /// <summary>놓기(placement)와 조준 보조(aim)가 같은 기준으로 판정하게 설정을 맞춘다.</summary>
        private void ApplyPlacementSettings()
        {
            // 멀리까지 날린다: 최대 거리를 크게, 더 먼 곳을 가리키면 그 방향 최대 거리로 당긴다.
            // 설 공간 = 이 캐릭터 몸(CharacterController) 크기.
            bool hasCc = TryGetComponent(out CharacterController cc);
            foreach (var p in new[] { placement, aim })
            {
                if (p == null) continue;
                p.maxPlaceDistance = placeDistance; p.defaultDistance = turnDefaultDistance; p.clampToMax = true;
                p.ledgeSnapHeight = ledgeSnapHeight; p.maxRiseHeight = maxRiseHeight;
                p.requireStandSpace = true; p.seeThroughWalls = placeBeyondWalls;
                if (hasCc) { p.standRadius = cc.radius * transform.lossyScale.x; p.standHeight = cc.height * transform.lossyScale.y; }
            }
        }

        private void OnDisable()
        {
            Placing = false; placement.Clear(); waitRelease = false; AimShown = false;
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
                if (!Placing) placement.Begin(); // 새로 누르기 시작: 지난 자리는 버린다
                Placing = true;
                placement.Update(transform, input);
                if (input.Skill)
                {
                    // 누른 채 E → 취소
                    Placing = false; placement.Clear(); waitRelease = true;
                    CancelTick = SimulationTick.Current;
                }
            }
            else if (Placing)
            {
                // 뗐다(또는 막힘) → 몸 근처면 취소(재사용 대기 없음), 놓을 수 있는 자리면 놓고 재사용 대기
                if (!placement.Cancels && placement.Valid && !blocked)
                {
                    Place(placement.Point);
                    PlaceCooldownRemaining = placeCooldown;
                }
                Placing = false; placement.Clear();
            }

            // 조준 보조: 누르지 않을 때도 지금 꾹 눌렀다 떼면 나비가 갈 자리를 미리 계산한다(FE가 고리로 보여 줌).
            AimShown = !Placing && !blocked;
            if (AimShown) { aim.Begin(); aim.Update(transform, input); }

            UpdateTarget(input);
        }

        private void Place(Vector3 point)
        {
            PlacedCount++;
            butterflies.Add(point);
            while (butterflies.Count > maxButterflies) butterflies.RemoveAt(0);
            justThrownLeft = followJustThrown ? followJustThrownTime : 0f; // 방금 날린 나비(맨 끝)는 잠깐 무조건 표적
        }

        /// <summary>바라보는 방향(시야 Yaw·Pitch)과 가장 가까운 각도의 나비를 고른다(시야 각도·거리 안에서).</summary>
        private void UpdateTarget(in CharacterInputFrame input)
        {
            TargetIndex = -1;
            if (justThrownLeft > 0f) justThrownLeft = Mathf.Max(0f, justThrownLeft - Time.fixedDeltaTime);
            if (butterflies.Count == 0) return;
            if (followJustThrown && justThrownLeft > 0f)
            {
                // 방금 날린 나비: 벽 너머·시야 밖·먼 곳이어도 표적(바로 E를 누르면 따라감). 이미 거기 있으면 아님.
                Vector3 last = butterflies[^1] - transform.position; last.y = 0f;
                if (last.magnitude >= minTargetDistance) { TargetIndex = butterflies.Count - 1; return; }
            }
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
                if (angle > bestAngle) continue;
                if (targetNeedsSight && !Visible(eye, butterflies[i])) continue; // 벽 너머(안 보이는) 나비는 도약 표적이 아니다
                bestAngle = angle; TargetIndex = i;
            }
        }

        /// <summary>눈에서 나비(위 sightHeights 중 한 높이)까지 가리는 것이 없는지(자기 몸·캐릭터·트리거·판정은 무시).</summary>
        public bool Visible(Vector3 eye, Vector3 butterfly)
        {
            if (sightHeights == null || sightHeights.Length == 0) return true;
            int mask = ~(CombatLayers.HurtboxMask | LayerMask.GetMask(CombatLayers.HitboxName));
            foreach (float h in sightHeights)
            {
                Vector3 to = butterfly + Vector3.up * h - eye;
                float dist = to.magnitude;
                if (dist < 0.01f) return true;
                int n = Physics.RaycastNonAlloc(eye, to / dist, SightHits, dist - 0.05f, mask, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int k = 0; k < n && !blocked; k++)
                {
                    var c = SightHits[k].collider;
                    blocked = !c.transform.IsChildOf(transform) && c is not CharacterController && c.GetComponentInParent<CharacterIdentity>() == null;
                }
                if (!blocked) return true;
            }
            return false;
        }
    }
}

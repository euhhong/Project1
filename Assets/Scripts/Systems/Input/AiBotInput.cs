using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI 봇 입력(게임 시작 모드의 상대). 키보드·마우스를 흉내 낸 입력(CharacterInputFrame)을 틱마다 한 번 만든다.
    /// 실제 판단은 나눠 맡긴다: 눈 = AiOpponentView(상대 상태, 반응 지연), 머리 = AiMeleeTactics(동작 고르기 — 여러 공격 커맨드·반응·포워드 유도 박치기),
    /// 손 = AiKeyPresser(꾹 누르기·짧게 누르기). 여기는 목표를 받아 이어 주기만 한다. 노란옷의 왕은 촉수 쓰기(AiTentacleUser), 생존자는 판자 쓰기(AiPalletUser)를 그 위에 덮는다.
    /// 조절 값은 인스펙터의 Settings(AiMeleeSettings). 목표가 없거나 꺼져 있으면 가만히 있는다.
    /// </summary>
    public class AiBotInput : MonoBehaviour, ICharacterInputSource, ILedgeGrabSuppress
    {
        /// <summary>
        /// 상대가 가까우면(ClimbNearRange 안) 벽을 붙잡거나(매달리기) 턱에 오르지 않는다 — 싸우다 벽에 매달리지 않게. 상대가 멀면 허용
        /// (무너진 잔해처럼 올라서거나 붙잡고 넘어야 하는 지형을 지나 쫓아가게). 창틀·판자 넘기·사다리는 늘 된다(CharacterTraversal이 본다).
        /// </summary>
        public bool SuppressLedgeGrab => isActiveAndEnabled && !ClimbAllowed;

        public const float ClimbNearRange = 6f;

        /// <summary>지금 벽 붙잡기·턱 오르기를 해도 되는지(상대가 ClimbNearRange보다 멀거나 없음) — 길 찾기(AiRouteFinder)도 본다.</summary>
        public bool ClimbAllowed => target == null || (target.position - transform.position).sqrMagnitude > ClimbNearRange * ClimbNearRange;
        [SerializeField] private Transform target;
        [SerializeField] private AiMeleeSettings settings = new();
        [Tooltip("화면 왼쪽 위에 지금 동작·고른 동작 수를 보여 준다(조절할 때).")]
        [SerializeField] private bool debugOverlay;

        private readonly AiOpponentView view = new();
        private AiMeleeTactics tactics;
        private int cachedTick = int.MinValue;
        private float lastThink = -1f;
        private CharacterInputFrame cachedFrame;
        private ITentacleField tentacleField;
        private AiTentacleUser tentacles;
        private ButterflyField butterflyField;
        private AiButterflyUser butterflies;
        private AiPalletUser pallets;
        private AiRetreat retreat;

        public AiMeleeTactics Tactics => tactics ??= new AiMeleeTactics(gameObject, settings, view);
        public AiMeleeSettings Settings => settings;

        public void SetTarget(Transform t)
        {
            target = t;
            view.SetTarget(t);
        }

        public void SetDebugOverlay(bool on) => debugOverlay = on;

        public CharacterInputFrame ReadInput()
        {
            int tick = SimulationTick.Current;
            if (tick == cachedTick) return cachedFrame;
            cachedTick = tick;
            float now = Time.time;
            float dt = lastThink < 0f ? Time.fixedDeltaTime : Mathf.Max(now - lastThink, 0f);
            lastThink = now;

            if (!isActiveAndEnabled || target == null || !target.gameObject.activeInHierarchy)
            {
                cachedFrame = new CharacterInputFrame { LookYaw = transform.eulerAngles.y };
                return cachedFrame;
            }
            view.SetTarget(target); // 인스펙터에서 바꿨을 때
            cachedFrame = Tactics.Tick(now, dt);
            // 노란옷의 왕: 촉수 세우기·내려찍기(TentacleField — E 스킬이 아니라 근접 AI가 쓰지 않음)
            // 캐릭터를 다시 입히면 TentacleField가 지워지고 새로 붙는다 — 지워진 것을 붙잡고 있지 않게, 없거나 지워졌으면 다시 찾는다.
            if (tentacleField as Object == null)
            {
                if (TryGetComponent(out ITentacleField tf)) { tentacleField = tf; tentacles = new AiTentacleUser(transform, tf); }
                else { tentacleField = null; tentacles = null; }
            }
            if (tentacles != null && tentacleField is Behaviour { isActiveAndEnabled: true }) tentacles.Apply(ref cachedFrame, now, target);
            // 붉은나비: 쿨이 돌면 상대 앞에 나비를 놓고 바로 날아간다(근접 AI는 나비를 놓지 않아 E를 거의 못 썼다)
            if (butterflyField == null)
            {
                if (TryGetComponent(out ButterflyField bf)) { butterflyField = bf; butterflies = new AiButterflyUser(transform, bf); }
                else butterflies = null;
            }
            if (butterflies != null && butterflyField.isActiveAndEnabled) butterflies.Apply(ref cachedFrame, now, target);
            // 생존자(판자를 내릴 수 있는 캐릭터): 쫓기면 판자로 달려가 내리고, 내린 판자를 끼고 싸운다
            if (TryGetComponent(out PalletUser pu) && pu.isActiveAndEnabled)
            {
                // 불리하면(크게 다침·주변 판자 다 씀·스킬 열세) 다른 층·다른 판자 구간으로 도주 — 도주 중에도 감시자가 판자 자리에 들어오면 바로 내린다
                retreat ??= new AiRetreat(gameObject);
                retreat.Apply(ref cachedFrame, now, target);
                (pallets ??= new AiPalletUser(transform)).Apply(ref cachedFrame, now, target, retreat.Fleeing);
            }
            return cachedFrame;
        }

        private void OnGUI()
        {
            if (!debugOverlay || tactics == null) return;
            var sb = new System.Text.StringBuilder();
            sb.Append(name).Append(" : ").Append(tactics.Current).Append("   돌진 ").Append(tactics.ChargesFired).Append('\n');
            for (int i = 1; i < tactics.Counts.Length; i++) sb.Append(AiMeleeTactics.NameOf(i)).Append(' ').Append(tactics.Counts[i]).Append("  ");
            GUI.Label(new Rect(10, 10, 900, 60), sb.ToString());
        }
    }
}

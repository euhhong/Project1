using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI 봇 입력(게임 시작 모드의 상대). 키보드·마우스를 흉내 낸 입력(CharacterInputFrame)을 틱마다 한 번 만든다.
    /// 실제 판단은 나눠 맡긴다: 눈 = AiOpponentView(상대 상태, 반응 지연), 머리 = AiMeleeTactics(동작 고르기 — 여러 공격 커맨드·반응·전위 유도 박치기),
    /// 손 = AiKeyPresser(꾹 누르기·짧게 누르기). 여기는 목표를 받아 이어 주기만 한다.
    /// 조절 값은 인스펙터의 Settings(AiMeleeSettings). 목표가 없거나 꺼져 있으면 가만히 있는다.
    /// </summary>
    public class AiBotInput : MonoBehaviour, ICharacterInputSource
    {
        [SerializeField] private Transform target;
        [SerializeField] private AiMeleeSettings settings = new();
        [Tooltip("화면 왼쪽 위에 지금 동작·고른 동작 수를 보여 준다(조절할 때).")]
        [SerializeField] private bool debugOverlay;

        private readonly AiOpponentView view = new();
        private AiMeleeTactics tactics;
        private int cachedTick = int.MinValue;
        private float lastThink = -1f;
        private CharacterInputFrame cachedFrame;

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

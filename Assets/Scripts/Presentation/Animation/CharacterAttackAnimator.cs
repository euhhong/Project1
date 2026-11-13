using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공격 상태(ICharacterAttackState, BE)를 Animator 공격 레이어로 보여 준다.
    /// - 땅: 상체 레이어(Attack) — 약공격 잽 / 강공격 모으기 / 강공격 내려치기
    /// - 공중(IsAirAttack): 전신 레이어(AirAttack) — 공중 모으기 / 발 내려찍기
    ///   앞으로 달리다 점프한 공격(BE IsRunJumpAttack)은 RunJump(생존자 Flying Kick, 감시자는 Override Controller의 모션).
    ///   그 밖의 공중 공격은 전용 모션 파일(AirStomp)이 있을 때만 그것을, 없으면 땅과 같은 공격 모션을 쓴다.
    /// 약·강공격은 BE 구간(준비/판정/후딜레이) 진행도에 맞춰 재생 위치를 매 프레임 맞춘다.
    /// 그래서 구간 길이가 바뀌어도 손·발이 맞는 순간이 항상 판정 구간 안에 온다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAttackAnimator : MonoBehaviour
    {
        private const string GroundLayerName = "Attack";
        private const string AirLayerName = "AirAttack";

        // 모션 클립마다 준비가 끝나는 지점 / 판정이 끝나는 지점(클립 전체 0~1 기준). 프레임은 잘라 낸 클립 기준(30fps).
        private static readonly Dictionary<string, Vector2> Timing = new()
        {
            ["Attack_Light"] = new Vector2(4f / 28f, 9f / 28f),             // Mutant Punch(원본 2~30): 오른손 훅 6~11
            ["Attack_Light_HookPunch"] = new Vector2(16f / 42f, 22f / 42f), // Hook Punch(원본 14~56): 훅 30~36
            ["Attack_Light_Hunter"] = new Vector2(14f / 44f, 22f / 44f),    // Melee Attack Downward(원본 6~50): 내려치기 20~28
            ["Attack_Light_Luchino"] = new Vector2(18f / 56f, 25f / 56f),   // Melee Attack Horizontal(원본 4~60): 가로 베기 22~29
            ["Attack_Light_Geisha"] = new Vector2(19f / 54f, 31f / 54f),    // Torch Melee Attack Stab 좌우 뒤집음(7~61): 오른팔 길게 찌르기 26~38
            ["Attack_Light_Hastur"] = new Vector2(22f / 45f, 30f / 45f),    // Zombie Attack(원본 19~64): 두 손 내려찍기 41~49
            ["Attack_Light_MindsEye"] = new Vector2(14f / 38f, 21f / 38f),  // Melee Combo Attack Ver. 3(원본 10~48): 첫 타 24~31
            ["Attack_SwipeL"] = new Vector2(7f / 20f, 10f / 20f),           // 맹인 왼쪽 후리기(원본 22~42): 앞 29~32
            ["Attack_SwipeR"] = new Vector2(8f / 20f, 11f / 20f),           // 맹인 오른쪽 후리기(23~43): 앞 31~34
            ["Attack_RunJump_Survivor"] = new Vector2(8f / 30f, 19f / 30f), // Flying Kick(0~30): 왼발 뻗기 8~19
            ["Attack_RunJump_Hunter"] = new Vector2(11f / 34f, 19f / 34f),  // Run Jump Attack(원본 36~70): 내려치기 47~55
            ["Attack_Light_Joker"] = new Vector2(11f / 34f, 19f / 34f),     // 광대 두 손 내려치기(같은 클립)
            ["Attack_Heavy_Joker"] = new Vector2(11f / 34f, 19f / 34f),
            ["Attack_Light_Ripper"] = new Vector2(14f / 44f, 22f / 44f),    // 리퍼: 감시자 내려치기를 좌우 뒤집음(왼손)
            ["Attack_Heavy_Ripper"] = new Vector2(14f / 44f, 22f / 44f),
            ["Attack_RunJump_Ripper"] = new Vector2(11f / 34f, 19f / 34f),
            // 아직 임시 모션(_Motions/Procedural)
            ["Attack_Heavy"] = new Vector2(0.25f / 0.9f, 0.4f / 0.9f),
            ["Attack_AirStomp"] = new Vector2(0.25f, 0.45f),
        };
        private static readonly Vector2 DefaultTiming = new(0.3f, 0.6f);

        [Tooltip("공격 레이어가 켜지는/꺼지는 속도(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.06f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.15f;

        private static readonly int LightId = Animator.StringToHash("Light");
        private static readonly int SwipeLId = Animator.StringToHash("SwipeL");
        private static readonly int SwipeRId = Animator.StringToHash("SwipeR");
        private static readonly int ChargeId = Animator.StringToHash("HeavyCharge");
        private static readonly int HeavyId = Animator.StringToHash("Heavy");
        private static readonly int StompId = Animator.StringToHash("AirStomp");
        private static readonly int AirChargeId = Animator.StringToHash("AirCharge");
        private static readonly int RunJumpId = Animator.StringToHash("RunJump");

        private ICharacterAttackState attack;
        private Animator animator;
        private int groundLayer = -1, airLayer = -1;
        private float groundWeight, airWeight;
        private CharacterAttackState shown = CharacterAttackState.None;
        private bool shownAir;
        private RuntimeAnimatorController timingFor;
        private readonly Dictionary<string, Vector2> slotTiming = new();

        private void Awake()
        {
            attack = GetComponent<ICharacterAttackState>();
            animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                // 모델이 바뀌었으면 새 Animator를 찾고, 레이어 번호도 다시 찾는다.
                animator = GetComponentInChildren<Animator>();
                groundLayer = airLayer = -1;
                groundWeight = airWeight = 0f;
            }
            if (attack == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (groundLayer < 0) groundLayer = animator.GetLayerIndex(GroundLayerName);
            if (airLayer < 0) airLayer = animator.GetLayerIndex(AirLayerName);
            if (groundLayer < 0) return;

            var state = attack.AttackState;
            bool active = state != CharacterAttackState.None;
            // 공중 전용 모션(전신 AirAttack 레이어)은 있을 때만 쓴다: 달리기 점프 공격(플라잉킥 등), 또는 제자리 공중 공격 모션 파일이 있을 때.
            // 없으면 공중에서도 땅과 같은 공격 모션(상체 Attack 레이어)을 쓴다.
            bool runJump = attack.IsRunJumpAttack && airLayer >= 0 && animator.HasState(airLayer, RunJumpId);
            bool stomp = !runJump && attack.IsAirAttack && airLayer >= 0 && animator.HasState(airLayer, StompId);
            bool air = runJump || stomp;
            groundWeight = Fade(groundWeight, active && !air);
            airWeight = Fade(airWeight, active && air);
            animator.SetLayerWeight(groundLayer, groundWeight);
            if (airLayer >= 0) animator.SetLayerWeight(airLayer, airWeight);

            if (air)
            {
                switch (state)
                {
                    case CharacterAttackState.Light:
                    case CharacterAttackState.Heavy:
                        if (runJump) animator.Play(RunJumpId, airLayer, Normalized(SlotTiming("Attack_RunJump")));
                        else animator.Play(StompId, airLayer, Normalized(SlotTiming("Attack_AirStomp")));
                        break;
                    case CharacterAttackState.HeavyCharge:
                        if (shown != state || !shownAir) animator.CrossFadeInFixedTime(AirChargeId, 0.1f, airLayer);
                        break;
                }
            }
            else
            {
                switch (state)
                {
                    case CharacterAttackState.Light:
                        // 좌우로 그어 후리기(맹인): 그 방향 모션이 있으면 그걸로
                        int swipeState = attack.Swipe < 0 ? SwipeLId : attack.Swipe > 0 ? SwipeRId : 0;
                        if (swipeState != 0 && animator.HasState(groundLayer, swipeState))
                            animator.Play(swipeState, groundLayer, Normalized(SlotTiming(attack.Swipe < 0 ? "Attack_SwipeL" : "Attack_SwipeR")));
                        else
                            animator.Play(LightId, groundLayer, Normalized(SlotTiming("Attack_Light")));
                        break;
                    case CharacterAttackState.Heavy:
                        animator.Play(HeavyId, groundLayer, Normalized(SlotTiming("Attack_Heavy")));
                        break;
                    case CharacterAttackState.HeavyCharge:
                        if (shown != state || shownAir) animator.CrossFadeInFixedTime(ChargeId, 0.12f, groundLayer);
                        break;
                }
            }
            shown = state; shownAir = air;
        }

        /// <summary>
        /// 자리(예: Attack_RunJump)에 실제로 들어간 클립의 판정 비율. 감시자는 Override Controller가 클립을 바꾸므로
        /// Controller에 든 클립 이름(Attack_RunJump_Hunter 등)으로 찾는다.
        /// </summary>
        private Vector2 SlotTiming(string slot)
        {
            var rc = animator.runtimeAnimatorController;
            if (timingFor != rc) { slotTiming.Clear(); timingFor = rc; }
            if (slotTiming.TryGetValue(slot, out var t)) return t;
            t = DefaultTiming;
            foreach (var clip in rc.animationClips)
            {
                if (clip == null || !(clip.name == slot || clip.name.StartsWith(slot + "_"))) continue;
                if (Timing.TryGetValue(clip.name, out var found)) { t = found; break; }
            }
            slotTiming[slot] = t;
            return t;
        }

        private float Fade(float w, bool on)
        {
            float target = on ? 1f : 0f;
            return Mathf.MoveTowards(w, target, Time.deltaTime / (target > w ? fadeIn : fadeOut));
        }

        /// <summary>현재 구간과 진행도를 모션 전체(0~1) 위치로 바꾼다. timing = (준비 끝, 판정 끝).</summary>
        private float Normalized(Vector2 timing)
        {
            float p = Mathf.Clamp01(attack.PhaseProgress);
            switch (attack.AttackPhase)
            {
                case AttackPhase.Windup: return Mathf.Lerp(0f, timing.x, p);
                case AttackPhase.Active: return Mathf.Lerp(timing.x, timing.y, p);
                case AttackPhase.Recovery: return Mathf.Lerp(timing.y, 0.999f, p);
                default: return 0f;
            }
        }
    }
}

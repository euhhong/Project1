using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공격 상태(ICharacterAttackState, BE)를 Animator 공격 레이어로 보여 준다.
    /// - 땅: 상체 레이어(Attack) — 약공격 잽 / 강공격 모으기 / 강공격 내려치기
    /// - 공중(IsAirAttack): 전신 레이어(AirAttack) — 공중 모으기 / 발 내려찍기
    ///   앞으로 달리다 점프한 공격은 RunJump(생존자 Flying Kick, 감시자는 Override Controller의 모션)
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
            ["Attack_Light"] = new Vector2(6f / 25f, 12f / 25f),            // Boxing 잽(원본 5~30): 뻗기 11, 당기기 17
            ["Attack_Light_Hunter"] = new Vector2(14f / 44f, 22f / 44f),    // Melee Attack Downward(원본 6~50): 내려치기 20~28
            ["Attack_Light_Luchino"] = new Vector2(18f / 56f, 25f / 56f),   // Melee Attack Horizontal(원본 4~60): 가로 베기 22~29
            ["Attack_Light_Geisha"] = new Vector2(8f / 54f, 19f / 54f),     // Torch Melee Attack Stab(원본 6~60): 찌르기 14~25
            ["Attack_Light_Hastur"] = new Vector2(20f / 36f, 28f / 36f),    // 2H Magic Attack 03(원본 8~44): 내뻗기 28~36
            ["Attack_RunJump_Survivor"] = new Vector2(8f / 30f, 19f / 30f), // Flying Kick(0~30): 왼발 뻗기 8~19
            ["Attack_RunJump_Hunter"] = new Vector2(11f / 34f, 19f / 34f),  // Run Jump Attack(원본 36~70): 내려치기 47~55
            // 아직 임시 모션(_Motions/Procedural)
            ["Attack_Heavy"] = new Vector2(0.25f / 0.9f, 0.4f / 0.9f),
            ["Attack_AirStomp"] = new Vector2(0.25f, 0.45f),
        };
        private static readonly Vector2 DefaultTiming = new(0.3f, 0.6f);

        [Tooltip("공격 레이어가 켜지는/꺼지는 속도(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.06f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.15f;

        [Tooltip("공중 공격을 시작할 때 앞으로 이 속도 비율(걷기 0 ~ 달리기 1) 이상이면 달리기 점프 공격(RunJump)을 쓴다.")]
        [SerializeField, Range(0f, 1f)] private float runJumpThreshold = 0.5f;

        private static readonly int LightId = Animator.StringToHash("Light");
        private static readonly int ChargeId = Animator.StringToHash("HeavyCharge");
        private static readonly int HeavyId = Animator.StringToHash("Heavy");
        private static readonly int StompId = Animator.StringToHash("AirStomp");
        private static readonly int AirChargeId = Animator.StringToHash("AirCharge");
        private static readonly int RunJumpId = Animator.StringToHash("RunJump");

        private ICharacterAttackState attack;
        private ICharacterState body;
        private Animator animator;
        private int groundLayer = -1, airLayer = -1;
        private float groundWeight, airWeight;
        private CharacterAttackState shown = CharacterAttackState.None;
        private bool shownAir, runJump;
        private RuntimeAnimatorController timingFor;
        private readonly Dictionary<string, Vector2> slotTiming = new();

        private void Awake()
        {
            attack = GetComponent<ICharacterAttackState>();
            body = GetComponent<ICharacterState>();
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
            bool air = attack.IsAirAttack && airLayer >= 0;
            bool active = state != CharacterAttackState.None;
            if (active && shown == CharacterAttackState.None) runJump = air && IsRunningForward();
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

        private bool IsRunningForward()
        {
            if (body == null) return false;
            Vector2 v = body.LocalPlanarVelocity;
            float walk = body.WalkSpeed, run = Mathf.Max(body.RunSpeed, walk + 0.01f);
            return v.y > Mathf.Abs(v.x) && v.y >= Mathf.Lerp(walk, run, runJumpThreshold);
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

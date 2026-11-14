using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 오르기·매달리기·낙하·착지(ICharacterTraversalState, BE)를 Animator "Traversal" 레이어(전신, 기본 레이어 바로 위)로 보여 준다.
    /// - 시간이 정해진 동작(올라서기·창틀 넘기·매달려 올라가기·착지 등)은 재생 위치를 BE 진행도에 맞춘다(BE가 루트를 그 모션의 발 높이대로 옮긴다).
    /// - 매달림·떨어지는 중은 한 번 섞어 시작해 그냥 돈다(반복).
    /// - 물건 밀기(ICharacterPushState, BE CharacterPush)도 여기서: 두 손 대며 기울임(PushStart) → 밀며 걷기(Push 반복), 막히면 기울인 채 버팀.
    /// 공격·피격·스킬 레이어가 위에 있어 그 모션이 나오면 덮는다. 상태가 없으면(모션 파일이 아직 없으면) 아무것도 하지 않는다.
    /// 모션: `_Motions/Mixamo/Traversal_*.fbx` → CharacterMotionControllerBuilder가 상태를 만든다. 캐릭터 루트에 붙인다(CharacterLoadout).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterTraversalAnimator : MonoBehaviour
    {
        private const string LayerName = "Traversal";

        [SerializeField, Min(0.01f)] private float fadeIn = 0.08f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.18f;
        [Tooltip("반복 상태로 바뀔 때 섞는 시간(초).")]
        [SerializeField, Min(0f)] private float crossFade = 0.15f;
        [Tooltip("감시자(HunterStyle): 넘기·오르기 모션으로 들어가기/나오기/동작 사이 섞는 시간(초) — 생존자보다 길게 섞어 덜 끊겨 보이게.")]
        [SerializeField, Min(0.01f)] private float hunterFadeIn = 0.16f, hunterFadeOut = 0.32f;
        [SerializeField, Min(0f)] private float hunterCrossFade = 0.24f;

        // CharacterPush.pushLeanTime(기본 0.4초)과 같게 — 기울이는 모션을 이 시간에 맞춰 재생한다.
        private const float PushLeanTime = 0.4f;

        private ICharacterTraversalState traversal;
        private ICharacterPushState push;
        private Animator animator;
        private int layer = -1, playing;
        private float weight, blendLeft;

        /// <summary>상태의 모션 길이(초, 배속 반영). 섞어 넘어갈 때 시작 위치를 초로 바꾸는 데 쓴다. 모르면 1.</summary>
        private float StateLength(int state)
        {
            var info = animator.GetNextAnimatorStateInfo(layer);
            if (info.shortNameHash == state && info.length > 0f) return info.length;
            info = animator.GetCurrentAnimatorStateInfo(layer);
            return info.shortNameHash == state && info.length > 0f ? info.length : 1f;
        }

        private void LateUpdate()
        {
            traversal ??= GetComponent<ICharacterTraversalState>();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f; playing = 0;
            }
            if (traversal == null || animator == null || animator.runtimeAnimatorController == null) return;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (layer < 0) return;

            push ??= GetComponent<ICharacterPushState>();
            // 물건 밀기: 두 손을 대며 기울임(PushStart, 붙는 시간에 맞춤) → 밀며 걷기(Push 반복). 막히면 기울인 자세로 버틴다.
            bool pushing = traversal.Action == TraversalAction.None && push != null && push.IsPushing;
            bool leaning = pushing && (push.PushTime < PushLeanTime || push.PushBlocked);
            string name = pushing ? (leaning ? "PushStart" : "Push") : StateName(traversal);
            int state = name != null ? Animator.StringToHash(name) : 0;
            bool active = state != 0 && animator.HasState(layer, state);
            bool hunter = traversal.HunterStyle && !pushing;
            float fade = active ? (hunter ? hunterFadeIn : fadeIn) : (hunter ? hunterFadeOut : fadeOut);
            float cross = hunter ? hunterCrossFade : crossFade;
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime / fade);
            animator.SetLayerWeight(layer, weight);
            if (!active) { playing = 0; return; }

            bool synced = leaning || (!pushing && Synced(traversal.Action));
            float at = leaning ? (push.PushBlocked ? 0.999f : Mathf.Min(push.PushTime / PushLeanTime, 0.999f))
                     : Mathf.Min(traversal.ActionProgress * (traversal.Action == TraversalAction.HangClimbOver ? Project1.Systems.CharacterTraversal.OverClipShare : 1f), 0.999f);
            if (playing != state)
            {
                // 동작이 바뀌면 앞 모션에서 섞어 넘어간다(예: 매달림 → 올라가기가 한 프레임에 확 바뀌지 않게). 레이어를 막 켰을 때는 바로.
                if (playing == 0 || weight < 0.5f) animator.Play(state, layer, synced ? at : 0f);
                else animator.CrossFadeInFixedTime(state, cross, layer, synced ? at * StateLength(state) : 0f);
                blendLeft = playing == 0 ? 0f : cross;
            }
            else if (synced && blendLeft <= 0f)
                animator.Play(state, layer, at); // 재생 위치를 BE 진행도에 맞춘다(섞는 동안은 그대로 재생)
            blendLeft -= Time.deltaTime;
            playing = state;
        }

        private static string StateName(ICharacterTraversalState t) => t.Action switch
        {
            // 창틀 뚫고 지나가기(붉은나비 — 하스터처럼): 창틀 넘기 모션 없이 걷기·달리기 모션 그대로 지나간다.
            TraversalAction.WindowVault or TraversalAction.WindowJump or TraversalAction.WindowDrop when t.PassesThroughWindows => null,
            TraversalAction.ClimbUp => "ClimbUp",
            TraversalAction.WindowVault => t.HunterStyle ? "WindowVaultHunter" : "WindowJump", // 걸어서 넘기도 점프 넘기와 같은 모션(BE가 더 길게 = 느리게)
            TraversalAction.WindowJump => t.HunterStyle ? "WindowVaultHunter" : "WindowJump",
            TraversalAction.WindowDrop => "WindowDrop",
            TraversalAction.HangGrab => t.BracedHang ? "HangBraced_Grab" : "Hang_Grab",
            TraversalAction.Hang => t.BracedHang ? "HangBraced" : "Hang",
            TraversalAction.HangHop => t.HopRight ? "HangHopRight" : "HangHop", // 매달린 채 옆으로 건너 잡기(Braced Hang Hop Left, 오른쪽은 좌우 뒤집은 상태)
            TraversalAction.HangClimb => "HangClimb",
            TraversalAction.HangClimbRail => "HangClimbRail",
            TraversalAction.HangFlip => "HangFlip", // 생존자: 벽을 박차고 뒤로 공중제비(Backflip) → 아래 바닥에 내려섬
            TraversalAction.HangClimbOver => "HangClimb",// 매달려 올라가기(Freehang Climb)의 앞부분(꼭대기에 올라섬까지)만
            TraversalAction.Ladder => t.HunterStyle ? "LadderHunter" : "Ladder", // 사다리 오르기(생존자 Climbing Ladder / 감시자 Climbing) — 재생 위치 = 오른 높이(내리면 거꾸로, 멈추면 멈춤)
            TraversalAction.Fall => t.FallBackward ? "FallBack" : "Fall",
            TraversalAction.Land => "Land",
            TraversalAction.LandHigh => "LandHigh",
            TraversalAction.LandRoll => "LandRoll",
            TraversalAction.LandHard => "LandHard",
            _ => null,
        };

        private static bool Synced(TraversalAction a) => a != TraversalAction.Hang && a != TraversalAction.Fall;
    }
}

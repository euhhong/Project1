using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 오르기·매달리기·낙하·착지(ICharacterTraversalState, BE)를 Animator "Traversal" 레이어(전신, 기본 레이어 바로 위)로 보여 준다.
    /// - 시간이 정해진 동작(올라서기·창틀 넘기·매달려 올라가기·착지 등)은 재생 위치를 BE 진행도에 맞춘다(BE가 루트를 그 모션의 발 높이대로 옮긴다).
    /// - 매달림·떨어지는 중은 한 번 섞어 시작해 그냥 돈다(반복).
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

        private ICharacterTraversalState traversal;
        private Animator animator;
        private int layer = -1, playing;
        private float weight;

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

            string name = StateName(traversal);
            int state = name != null ? Animator.StringToHash(name) : 0;
            bool active = state != 0 && animator.HasState(layer, state);
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime / (active ? fadeIn : fadeOut));
            animator.SetLayerWeight(layer, weight);
            if (!active) { playing = 0; return; }

            if (Synced(traversal.Action))
                animator.Play(state, layer, Mathf.Min(traversal.ActionProgress, 0.999f));
            else if (playing != state)
                animator.CrossFadeInFixedTime(state, playing == 0 ? 0f : crossFade, layer);
            playing = state;
        }

        private static string StateName(ICharacterTraversalState t) => t.Action switch
        {
            TraversalAction.ClimbUp => "ClimbUp",
            TraversalAction.WindowVault => t.HunterStyle ? "WindowVaultHunter" : "WindowVault",
            TraversalAction.WindowJump => t.HunterStyle ? "WindowVaultHunter" : "WindowJump",
            TraversalAction.WindowDrop => "WindowDrop",
            TraversalAction.HangGrab => t.BracedHang ? "HangBraced_Grab" : "Hang_Grab",
            TraversalAction.Hang => t.BracedHang ? "HangBraced" : "Hang",
            TraversalAction.HangClimb => "HangClimb",
            TraversalAction.HangClimbRail => "HangClimbRail",
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

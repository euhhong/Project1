using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 이 캐릭터가 맞으면(HitEvents, BE) Animator "HitReact" 레이어(상체)에 피격 모션을 한 번 재생한다.
    /// - 머리: Hit_Head
    /// - 몸통: 맞은 위치가 몸 뒤쪽이면 Hit_Body_Back(Big Kidney Hit), 아니면 Hit_Body_Side(Big Side Hit)
    /// - 팔·다리: Hit_Limb(Side Hit)
    /// 상체만 움직이므로 걷기·달리기 다리 모션은 그대로 이어진다. 연출 전용이고 게임 상태는 바꾸지 않는다.
    /// 쓰러지며 나가떨어지는 피격(HitResult.Knockdown — 헤드샷 킥)은 "HitReactFull" 레이어(전신)에 Hit_HeadKick(Corkscrew Evade)을 재생한다.
    /// 공격 중(약·강공격)에는 피격 모션을 얹지 않는다 — 둘이 바짝 붙어 서로 때려도 공격 모션이 끝까지 제대로 나온다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다(모델이 바뀌면 다시 찾는다).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterHitReactionAnimator : MonoBehaviour
    {
        private const string LayerName = "HitReact";
        private static readonly int HeadId = Animator.StringToHash("Hit_Head");
        private static readonly int BodySideId = Animator.StringToHash("Hit_Body_Side");
        private static readonly int BodyBackId = Animator.StringToHash("Hit_Body_Back");
        private static readonly int LimbId = Animator.StringToHash("Hit_Limb");

        [Tooltip("모션이 이 비율(0~1)까지 재생되면 레이어를 줄이기 시작한다.")]
        [SerializeField, Range(0.3f, 1f)] private float fadeOutStart = 0.75f;
        [SerializeField, Min(0.01f)] private float fadeOut = 0.2f;

        private CharacterIdentity identity;
        private Animator animator;
        private int layer = -1;
        private float weight;
        private bool playing;
        private ICharacterStun stun;
        private ICharacterAttackState attack;

        /// <summary>지금 약·강공격 중인지(피격 모션이 공격 모션을 덮지 않게).</summary>
        private bool Attacking
        {
            get
            {
                if (attack == null) TryGetComponent(out attack);
                return attack != null && (attack.AttackState == CharacterAttackState.Light || attack.AttackState == CharacterAttackState.Heavy);
            }
        }
        private static readonly int StunId = Animator.StringToHash("Stun");

        // 전신 피격(쓰러지며 나가떨어짐 — HitResult.Knockdown, 헤드샷 킥 = Corkscrew Evade)
        private const string FullLayerName = "HitReactFull";
        private static readonly int KnockdownId = Animator.StringToHash("Hit_HeadKick");
        private int fullLayer = -1;
        private float fullWeight;
        private bool knockdown;

        private void Awake() => identity = GetComponent<CharacterIdentity>();

        private void OnEnable() => HitEvents.HitOccurred += OnHit;

        private void OnDisable() => HitEvents.HitOccurred -= OnHit;

        private bool Resolve()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f; playing = false;
                fullLayer = -1; fullWeight = 0f; knockdown = false;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            if (fullLayer < 0) fullLayer = animator.GetLayerIndex(FullLayerName);
            return layer >= 0;
        }

        private void OnHit(HitResult hit)
        {
            if (identity == null || hit.VictimId != identity.Id || !Resolve()) return;
            if (hit.Knockdown && fullLayer >= 0 && animator.HasState(fullLayer, KnockdownId))
            {
                // 쓰러지며 나가떨어짐(헤드샷 킥): 전신 모션. 공격 중이어도 끊긴다(BE가 기절시킨다).
                animator.Play(KnockdownId, fullLayer, 0f);
                fullWeight = 1f;
                knockdown = true;
                weight = 0f; playing = false;
                animator.SetLayerWeight(layer, 0f);
                return;
            }
            if (Attacking) return; // 공격 중: 공격 모션을 그대로 둔다
            int state;
            switch (hit.Part)
            {
                case BodyPart.Head:
                    state = HeadId;
                    break;
                case BodyPart.Body:
                    Vector3 toHit = hit.Point - transform.position;
                    state = Vector3.Dot(transform.forward, toHit) < 0f ? BodyBackId : BodySideId;
                    break;
                case BodyPart.Arms:
                case BodyPart.Legs:
                    state = LimbId;
                    break;
                default:
                    return;
            }
            if (!animator.HasState(layer, state)) return;
            animator.Play(state, layer, 0f);
            weight = 1f;
            playing = true;
        }

        private void LateUpdate()
        {
            WatchStun();
            if (!Resolve()) return;
            if (knockdown)
            {
                // 나가떨어지는 전신 모션이 끝나 갈 때 풀고, 그동안 상체 피격·기절 모션은 얹지 않는다.
                var full = animator.GetCurrentAnimatorStateInfo(fullLayer);
                if (full.shortNameHash == KnockdownId && full.normalizedTime >= 0.9f) fullWeight = Mathf.MoveTowards(fullWeight, 0f, Time.deltaTime / fadeOut);
                if (fullWeight <= 0f) knockdown = false;
                animator.SetLayerWeight(fullLayer, fullWeight);
                animator.SetLayerWeight(layer, 0f);
                return;
            }
            if (Attacking)
            {
                // 공격하는 동안은 피격·기절 상체 모션을 끈다(공격 레이어 위에 얹히지 않게).
                weight = 0f; playing = false;
                animator.SetLayerWeight(layer, 0f);
                return;
            }

            // 기절 중: Stun 모션이 있으면 기절이 풀릴 때까지 반복해서 유지(고개 숙임).
            if (stun != null && stun.IsStunned && animator.HasState(layer, StunId))
            {
                if (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != StunId) animator.CrossFadeInFixedTime(StunId, 0.1f, layer);
                weight = 1f;
                playing = true;
                animator.SetLayerWeight(layer, weight);
                return;
            }

            if (!playing) return;
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            bool hold = stun != null && stun.IsStunned && info.shortNameHash == HeadId; // 기절 중엔 머리 숙인 자세를 오래 둔다
            if (info.normalizedTime >= fadeOutStart && !hold) weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / fadeOut);
            if (weight <= 0f) playing = false;
            animator.SetLayerWeight(layer, weight);
        }

        // 기절(BE CharacterStun)은 실행 중에 붙을 수 있어 매 프레임 확인하고 한 번만 구독한다.
        private void WatchStun()
        {
            if (stun != null || !TryGetComponent(out stun)) return;
            stun.Stunned += OnStunned;
        }

        // 기절 시작: 고개를 숙인다. 전용 Stun 모션이 없으면 머리 피격 모션(Head Hit)으로 숙이고 기절 동안 그 자세를 유지한다.
        private void OnStunned(float seconds)
        {
            if (Attacking) return;
            if (!Resolve() || animator.HasState(layer, StunId) || !animator.HasState(layer, HeadId)) return;
            animator.Play(HeadId, layer, 0f);
            weight = 1f;
            playing = true;
        }

        private void OnDestroy()
        {
            if (stun != null) stun.Stunned -= OnStunned;
        }
    }
}

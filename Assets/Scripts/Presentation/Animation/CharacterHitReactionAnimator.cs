using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 이 캐릭터가 맞으면(HitEvents, BE) Animator "HitReact" 레이어(상체)에 피격 모션을 한 번 재생한다.
    /// - 머리: 세기별 — 작은 데미지 Hit_Head_Light(Light Hit To Head), 약공격 Hit_Head_Medium(Medium Hit To Head), 강공격·점프 공격·스킬 Hit_Head / Hit_Head2(Head Hit 두 판, 번갈아)
    /// - 몸통: 맞은 위치가 몸 앞(약 ±60°)이면 Hit_Body_Front(Standing React Large From Front), 옆이면 Hit_Body_Side(Big Side Hit)
    /// - 팔·다리: Hit_Limb(Side Hit). 몸통·팔·다리를 등 쪽에서 맞으면 Hit_Body_Back(Standing React Large From Left)
    /// - 작은 데미지(세기 0.75 미만, 예: 리퍼 E): 부위·방향과 상관없이 Hit_Small(Standing React Small From Headshot)
    /// - 뒤에서 맞아 1초 이상 기절(전위 돌진·신호탄): "HitReactFull"(전신)에 Stun_Back(Turn To Knocked Unconscious — 휘청이며 쓰러져 누움)을 기절 시간에 맞춰 재생
    /// 상체만 움직이므로 걷기·달리기 다리 모션은 그대로 이어진다. 연출 전용이고 게임 상태는 바꾸지 않는다.
    /// 쓰러지며 나가떨어지는 피격(HitResult.Knockdown — 헤드샷 킥)은 "HitReactFull" 레이어(전신)에 Hit_HeadKick(Corkscrew Evade)을 재생한다.
    /// 공격 중(약·강공격)에는 피격 모션을 얹지 않는다 — 둘이 바짝 붙어 서로 때려도 공격 모션이 끝까지 제대로 나온다.
    /// 캐릭터 루트에 붙인다. Animator는 자식 모델에서 자동으로 찾는다(모델이 바뀌면 다시 찾는다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterShockReactionAnimator))] // 데미지 없는 충격파에 맞으면 휘청이는 모션(Edge Slip)도 같이
    public class CharacterHitReactionAnimator : MonoBehaviour
    {
        private const string LayerName = "HitReact";
        private static readonly int HeadId = Animator.StringToHash("Hit_Head");
        private static readonly int Head2Id = Animator.StringToHash("Hit_Head2");
        private static readonly int HeadLightId = Animator.StringToHash("Hit_Head_Light");
        private static readonly int HeadMediumId = Animator.StringToHash("Hit_Head_Medium");
        private bool headAlt;
        private static readonly int BodyFrontId = Animator.StringToHash("Hit_Body_Front");
        private const float FrontDot = 0.5f; // 몸 앞쪽 약 ±60° 안에서 맞으면 앞에서 맞은 것
        private static readonly int SmallId = Animator.StringToHash("Hit_Small");
        private const float SmallPower = 0.75f; // 세기(HitResult.PowerScale)가 이보다 작으면 작은 데미지(예: 리퍼 E 0.5)

        // 뒤에서 맞아 기절(전위 돌진·신호탄 — CharacterStun.StunnedFromBehind): 전신 "HitReactFull" 레이어에 Stun_Back(휘청이며 돌아 쓰러져 누움)
        private static readonly int StunBackId = Animator.StringToHash("Stun_Back");
        [Tooltip("뒤에서 맞은 기절이 이 시간(초) 이상일 때만 쓰러지는 모션을 쓴다(짧으면 보통 기절 모션).")]
        [SerializeField, Min(0f)] private float stunBackMin = 1f;
        [Tooltip("Stun_Back 모션에서 바닥에 쓰러지는 때(초, 원래 빠르기). 기절이 짧으면 기절 시간 절반 안에 쓰러지게 빨리 재생한다.")]
        [SerializeField, Min(0.1f)] private float stunBackFallAt = 0.9f;
        [Tooltip("기절이 풀릴 때 쓰러진 자세에서 돌아오는 시간(초).")]
        [SerializeField, Min(0.01f)] private float stunBackFadeOut = 0.35f;
        private bool stunBack;
        private float stunBackTime, stunBackSpeed;
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
                fullLayer = -1; fullWeight = 0f; knockdown = false; stunBack = false;
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
                stunBack = false;
                weight = 0f; playing = false;
                animator.SetLayerWeight(layer, 0f);
                return;
            }
            if (Attacking || stunBack) return; // 공격 중: 공격 모션을 그대로 둔다 / 뒤에서 맞아 쓰러지는 중: 그 모션 그대로
            if (hit.Part != BodyPart.Head && hit.Part != BodyPart.Body && hit.Part != BodyPart.Arms && hit.Part != BodyPart.Legs) return;
            Vector3 toHit = hit.Point - transform.position; toHit.y = 0f;
            float front = toHit.sqrMagnitude > 1e-4f ? Vector3.Dot(transform.forward, toHit.normalized) : 1f;
            int state;
            if (hit.Part == BodyPart.Head) state = HeadState(hit);
            else if (hit.PowerScale < SmallPower && animator.HasState(layer, SmallId)) state = SmallId; // 작은 데미지: 살짝 움찔
            else if (front < 0f) state = BodyBackId;                                                   // 등 쪽(몸통·팔·다리): 크게 휘청(Large From Left)
            else if (hit.Part == BodyPart.Body) state = front >= FrontDot && animator.HasState(layer, BodyFrontId) ? BodyFrontId : BodySideId; // 앞(약 ±60°) = 크게 뒤로 젖힘, 옆 = Side
            else state = LimbId;
            if (!animator.HasState(layer, state)) return;
            animator.Play(state, layer, 0f);
            weight = 1f;
            playing = true;
        }

        /// <summary>
        /// 머리 피격 세기별 모션: 작은 데미지 = Hit_Head_Light(Light Hit To Head), 약공격 = Hit_Head_Medium(Medium Hit To Head),
        /// 강공격·점프 공격·스킬 = 크게 젖힘(Hit_Head / Hit_Head2 — Head Hit 두 판 중 하나를 번갈아). 없는 모션은 다음 후보로.
        /// </summary>
        private int HeadState(HitResult hit)
        {
            if (hit.PowerScale < SmallPower)
                return animator.HasState(layer, HeadLightId) ? HeadLightId : animator.HasState(layer, SmallId) ? SmallId : HeadId;
            if (hit.Kind == AttackKind.Light && animator.HasState(layer, HeadMediumId)) return HeadMediumId;
            headAlt = !headAlt;
            return headAlt && animator.HasState(layer, Head2Id) ? Head2Id : HeadId;
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
            if (stunBack)
            {
                // 뒤에서 맞아 기절: 기절 시간에 맞춰 휘청이며 쓰러지고(짧으면 빨리) 누운 채 있다가, 풀리면 섞어 돌아온다.
                bool down = stun != null && stun.IsStunned;
                stunBackTime += Time.deltaTime;
                var backInfo = animator.GetCurrentAnimatorStateInfo(fullLayer);
                float length = backInfo.shortNameHash == StunBackId && backInfo.length > 0f ? backInfo.length : 2.5f;
                animator.Play(StunBackId, fullLayer, Mathf.Min(stunBackTime * stunBackSpeed / length, 0.999f));
                fullWeight = Mathf.MoveTowards(fullWeight, down ? 1f : 0f, Time.deltaTime / (down ? 0.1f : stunBackFadeOut));
                if (!down && fullWeight <= 0f) stunBack = false;
                animator.SetLayerWeight(fullLayer, fullWeight);
                weight = 0f; playing = false;
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
            bool hold = stun != null && stun.IsStunned && (info.shortNameHash == HeadId || info.shortNameHash == Head2Id || info.shortNameHash == HeadMediumId); // 기절 중엔 머리 숙인 자세를 오래 둔다
            if (info.normalizedTime >= fadeOutStart && !hold) weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / fadeOut);
            if (weight <= 0f) playing = false;
            animator.SetLayerWeight(layer, weight);
        }

        // 기절(BE CharacterStun)은 실행 중에 붙을 수 있어 매 프레임 확인하고 한 번만 구독한다.
        private void WatchStun()
        {
            if (stun != null || !TryGetComponent(out stun)) return;
            stun.Stunned += OnStunned;
            if (stun.IsStunned) OnStunned(stun.StunRemaining); // 맞는 순간 기절이 붙고 바로 기절함(돌진·신호탄) — 놓친 첫 기절을 따라잡는다
        }

        // 기절 시작: 고개를 숙인다. 전용 Stun 모션이 없으면 머리 피격 모션(Head Hit)으로 숙이고 기절 동안 그 자세를 유지한다.
        private void OnStunned(float seconds)
        {
            if (!Resolve() || knockdown) return;
            // 뒤에서 맞아 기절(돌진·신호탄): 전신으로 휘청이며 쓰러진다. 기절이 길어질 때는 이어서.
            if (stun != null && stun.StunnedFromBehind && seconds >= stunBackMin && fullLayer >= 0 && animator.HasState(fullLayer, StunBackId))
            {
                if (!stunBack)
                {
                    stunBack = true;
                    stunBackTime = 0f;
                    stunBackSpeed = Mathf.Max(1f, stunBackFallAt / (seconds * 0.5f)); // 기절 시간 절반 안에 쓰러진다
                    animator.Play(StunBackId, fullLayer, 0f);
                }
                return;
            }
            if (Attacking) return;
            if (animator.HasState(layer, StunId) || !animator.HasState(layer, HeadId)) return;
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

using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 고유 스킬 데이터. 캐릭터마다 하나(예: 의사 = 자가치료). CharacterSkill이 시전·재사용 대기를 돌리고,
    /// 시전을 끝까지 마치면 Complete를 부른다. 새 스킬은 이 클래스를 상속해 Complete(효과)를 만든다.
    /// 이 데이터는 여러 캐릭터가 같이 쓰므로 캐릭터별 상태는 여기에 두지 않고 CharacterSkill(ctx)의 Phase·Area 등에 둔다.
    /// </summary>
    public abstract class SkillDefinition : ScriptableObject
    {
        [Tooltip("스킬 ID. 모션 자리 이름(Skill_<ID>)과 맞춘다.")]
        public string id;
        public string displayName;

        [Tooltip("시전 시간(초). 이 시간 동안 끊기지 않아야 효과가 난다. 끝나는 조건이 따로 있는 스킬은 최대 시간.")]
        [Min(0f)] public float castTime = 1f;

        [Tooltip("재사용 대기(초). 시전을 마친 뒤부터 센다.")]
        [Min(0f)] public float cooldown = 10f;

        [Tooltip("시전 중 최대 이동 속도(m/s). 0이면 제자리.")]
        [Min(0f)] public float castMoveSpeed = 0f;

        /// <summary>지금 쓸 수 있는지. 못 쓰면 이유를 돌려준다.</summary>
        public virtual bool CanStart(GameObject owner, out string reason)
        {
            reason = null;
            return true;
        }

        /// <summary>
        /// 시전 중 다른 행동(공격)을 막는지. false면 시전 중 공격하면 시전이 끊긴다(효과 없음).
        /// true면 공격 입력은 스킬이 받는다(Tick의 input).
        /// </summary>
        public virtual bool LocksActions => false;

        /// <summary>시전 중 공격하면 시전이 끊기는지. 기본: 공격을 막지 않는 스킬(자가치료)은 끊긴다. 켜 두는 스킬(용병 아대)은 false.</summary>
        public virtual bool CancelledByAttack => !LocksActions;

        /// <summary>시전 중 castMoveSpeed로 이동 속도를 제한할지. 켜 두는 스킬(용병 아대)은 false.</summary>
        public virtual bool LimitsMoveSpeed => true;

        /// <summary>켜져(시전 중) 있어도 창틀 넘기·턱 오르기·매달리기를 막지 않는지(예: 용병 아대 — 켠 채 다님). 기본 false.</summary>
        public virtual bool AllowsTraversal(CharacterSkill ctx) => false;

        /// <summary>지금 잔상을 남길지(예: 용병 초고속 도약 중).</summary>
        public virtual bool Afterimage(CharacterSkill ctx) => false;

        /// <summary>HUD에 보여 줄 상태 글(예: "벽에 닿음 → 이동하면 도약"). null이면 HUD 기본 글.</summary>
        public virtual string StatusText(CharacterSkill ctx) => null;

        /// <summary>시전 중이 아닐 때 HUD에 보여 줄 글(예: 리퍼 안개 칸 ●○). null이면 HUD 기본 글(준비/재사용 대기).</summary>
        public virtual string IdleStatusText(CharacterSkill ctx) => null;

        /// <summary>시전 중 HUD 막대: NaN = 기본(시전 진행도), 0~1 = 이 값으로 채움, 음수 = 막대 없음. 예: 용병 아대 = 도약 재사용 대기.</summary>
        public virtual float StatusBarFill(CharacterSkill ctx) => float.NaN;

        /// <summary>스킬 모션을 시전 진행도에 맞출지(true, 예: 주사기 꽂기) / 그냥 재생할지(false, 예: 돌진·도약).</summary>
        public virtual bool SyncAnimationToCast => true;

        /// <summary>
        /// 스킬 동작 구간(준비 Windup → 판정 Active → 마무리 Recovery)과 그 구간 진행도(0~1)를 알릴지(예: 그랩 = 휘두르기 → 그랩이 나가 있는 동안 → 마무리).
        /// 알리면 FE가 모션 재생 위치를 구간에 맞춘다(판정 동안은 멈춘 자세). 기본은 알리지 않음.
        /// </summary>
        /// <summary>시전 중 몸이 볼 좌우 각도를 시야 대신 정할지(예: 붉은나비 나비에게 — 시작할 때 각도 유지). 시야·카메라는 그대로 돈다.</summary>
        public virtual bool TryGetFacingYaw(CharacterSkill ctx, out float yaw)
        {
            yaw = 0f;
            return false;
        }

        public virtual bool TryGetMotionPhase(CharacterSkill ctx, out AttackPhase phase, out float progress)
        {
            phase = AttackPhase.None; progress = 0f;
            return false;
        }

        /// <summary>시전을 시작할 때(예: 도약 — 위로 띄운다).</summary>
        public virtual void OnStart(CharacterSkill ctx) { }

        /// <summary>시전 중 이동을 대신 정할지(예: 돌진). 기본은 정하지 않음 → castMoveSpeed로 속도만 제한.</summary>
        public virtual bool TryGetMoveOverride(GameObject owner, out Vector2 localMove, out float speed, out bool allowJump)
        {
            localMove = Vector2.zero; speed = 0f; allowJump = true;
            return false;
        }

        /// <summary>단계가 있는 스킬용(ctx.Phase를 본다). 기본은 위의 owner 버전을 부른다.</summary>
        public virtual bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump) =>
            TryGetMoveOverride(ctx.gameObject, out localMove, out speed, out allowJump);

        /// <summary>
        /// 시간이 다 되기 전에 끝낼지(예: 돌진 중 장애물에 닿음). true면 Complete를 부르고 끝낸다.
        /// recovery = 끝난 뒤 못 움직이는 시간(초, 예: 벽에 부딪혀 넘어짐). 0이면 바로 재사용 대기.
        /// </summary>
        public virtual bool ShouldEndEarly(GameObject owner, float elapsed, out float recovery)
        {
            recovery = 0f;
            return false;
        }

        /// <summary>시전 중 매 틱(입력을 받는 스킬용). 기본은 ShouldEndEarly를 부른다.</summary>
        public virtual bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery) =>
            ShouldEndEarly(ctx.gameObject, elapsed, out recovery);

        /// <summary>Skill 레이어에서 재생할 상태 이름. 기본: 시전 중 = ID, 넘어짐 = ID_End.</summary>
        public virtual string AnimationState(CharacterSkill ctx) =>
            ctx.State == SkillState.Recovering ? id + "_End" : id;

        /// <summary>시전을 마쳤을 때의 효과(시간이 다 됐거나 ShouldEndEarly·Tick으로 끝남).</summary>
        public abstract void Complete(GameObject owner);
    }
}

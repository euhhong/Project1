using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 붉은나비(미치코) 나비에게로(E): 지금 겨누는 나비(ButterflyField.TargetIndex) 자리로 곧게 빠르게 미끄러져 간다(glideSpeed).
    /// - 겨누는 나비가 없거나 나비를 놓는 중이면(놓기를 E로 취소하고 좌클릭을 아직 안 뗀 동안 포함) 쓰지 않는다.
    /// - 가는 동안 장애물(벽·다른 캐릭터 등)에 부딪히면 거기서 멈추고 툭 떨어진다(잠깐 못 움직임, bumpRecovery).
    /// - 이동·공격 불가. 중력은 그대로라 턱에서 벗어나면 떨어진다.
    /// 목표 자리는 시전 동안 CharacterSkill.AreaCenter에 둔다(ShowArea는 끔).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Skills/Butterfly Glide")]
    public class ButterflyGlideSkill : SkillDefinition
    {
        [Tooltip("미끄러지는 속도(m/s). 용병 아대 도약(35m/s)의 70%.")]
        [Min(0.1f)] public float glideSpeed = 24.5f;
        [Tooltip("이 거리(m, 수평) 안에 들어오면 도착.")]
        [Min(0.05f)] public float arriveDistance = 0.3f;
        [Tooltip("가다가 장애물에 부딪혀 멈춘 뒤 못 움직이는 시간(초, 툭 떨어짐).")]
        [Min(0f)] public float bumpRecovery = 0.35f;

        public override bool LocksActions => true;
        public override bool SyncAnimationToCast => false;

        public override bool CanStart(GameObject owner, out string reason)
        {
            var field = owner.GetComponent<ButterflyField>();
            if (field == null) { reason = "나비 없음"; return false; }
            if (field.Placing || field.WaitingRelease || field.CancelTick == SimulationTick.Current) { reason = "나비 놓는 중"; return false; }
            if (!field.HasTarget) { reason = "겨눈 나비 없음"; return false; }
            reason = null;
            return true;
        }

        public override void OnStart(CharacterSkill ctx)
        {
            var field = ctx.GetComponent<ButterflyField>();
            ctx.AreaCenter = field != null ? field.TargetPoint : ctx.transform.position;
            ctx.ShowArea = false;
            ctx.SetPhase(0);
        }

        private static Vector3 FlatTo(CharacterSkill ctx)
        {
            Vector3 to = ctx.AreaCenter - ctx.transform.position;
            to.y = 0f;
            return to;
        }

        public override bool TryGetMoveOverride(CharacterSkill ctx, out Vector2 localMove, out float speed, out bool allowJump)
        {
            allowJump = false;
            Vector3 to = FlatTo(ctx);
            float dist = to.magnitude;
            if (ctx.State != SkillState.Casting || dist < 0.001f) { localMove = Vector2.zero; speed = 0f; return true; }
            // 몸(시야) 기준 방향으로 바꾼다(몸은 시야를 따라 돌므로 매 틱 다시 계산).
            Vector3 local = Quaternion.Inverse(ctx.transform.rotation) * (to / dist);
            localMove = new Vector2(local.x, local.z);
            speed = Mathf.Min(glideSpeed, dist / Time.fixedDeltaTime); // 마지막 틱에 지나치지 않게
            return true;
        }

        public override bool Tick(CharacterSkill ctx, in CharacterInputFrame input, float elapsed, out float recovery)
        {
            recovery = 0f;
            if (FlatTo(ctx).magnitude <= arriveDistance) return true; // 도착
            var motor = ctx.GetComponent<CharacterMotor>();
            if (motor != null && motor.HitObstacle && elapsed > 0.04f)
            {
                recovery = bumpRecovery; // 장애물 → 멈추고 툭 떨어짐
                return true;
            }
            return false;
        }

        public override string AnimationState(CharacterSkill ctx) => ctx.State == SkillState.Casting ? id : string.Empty;

        public override string StatusText(CharacterSkill ctx) => "나비에게!";

        public override void Complete(GameObject owner) { }
    }
}

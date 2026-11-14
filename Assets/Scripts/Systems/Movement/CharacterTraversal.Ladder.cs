using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 사다리 타기(CharacterTraversal의 한 부분 — 매달리기·턱 오르기와 상태를 나눠 쓴다).
    /// - 잡기: 사다리(Ladder 표시) 앞에서 사다리 쪽으로 방향키(땅·공중 모두). 위층에서는 사다리 꼭대기 쪽 턱 끝으로 걸어 나가면 내려가는 사다리를 잡는다.
    ///   지쳐 있으면(스태미나 IsExhausted) 잡지 못한다.
    /// - 타는 중(TraversalAction.Ladder): 방향키가 벽 쪽 = 오르기, 벽 바깥쪽 = 내리기(시야 기준 — 보통 W/S), 점프 = 손을 놓고 뒤로 떨어짐. 모션은 벽을 짚고 매달린 자세(Braced Hang)를 쓴다.
    ///   스태미나를 초당 ladderStaminaDrain씩 쓰고(회복 없음 — IStaminaDrain), 다 쓰면 손이 풀려 뒤로(등으로) 떨어진다(보통 낙하 데미지).
    ///   기절·쓰러짐·넉백도 손이 풀려 떨어진다.
    /// - 꼭대기: 위 끝에 닿으면 그 턱에 매달린 상태(Hang)로 넘어가고, 앞을 누르고 있으면 그대로 턱 위로 올라간다(HangClimb). 거기서 뒤(S) = 다시 사다리로.
    /// - 아래 끝에서 뒤(S) = 바닥에 내려선다.
    /// </summary>
    public partial class CharacterTraversal : IStaminaDrain
    {
        [Header("사다리")]
        [Tooltip("오르는 / 내려가는 속도(m/s).")]
        [SerializeField, Min(0.1f)] private float ladderUpSpeed = 1.9f, ladderDownSpeed = 2.6f;
        [Tooltip("사다리에 있는 동안 초당 쓰는 스태미나(달리기 20). 다 쓰면 뒤로 떨어진다.")]
        [SerializeField, Min(0f)] private float ladderStaminaDrain = 4f;
        [Tooltip("손이 풀려 떨어질 때 벽 바깥으로 밀리는 속도(m/s).")]
        [SerializeField, Min(0f)] private float ladderFallPush = 2.5f;
        [Tooltip("사다리에 붙는 시간(초).")]
        [SerializeField, Min(0.01f)] private float ladderSnapTime = 0.12f;

        private const float LadderFirstRung = 0.3f; // 아래 끝에서 첫 발판 높이(m)

        private Ladder ladder;      // 타는 중인 사다리(꼭대기 턱에 매달려 있는 동안도 기억 — 뒤로 다시 내려온다)
        private float ladderRoot;   // 사다리 위 루트 높이
        private ICharacterStamina stamina;

        public float StaminaDrainPerSecond => action == TraversalAction.Ladder ? ladderStaminaDrain : 0f;

        private float LadderBottomRoot(Ladder l, float hu) => l.BottomY + LadderFirstRung - BracedFeet * hu;
        private float LadderTopRoot(Ladder l, float hu) => Mathf.Max(LadderBottomRoot(l, hu), l.TopY - BracedHangTop * hu);
        private Vector3 LadderPoint(Ladder l) => l.Bottom + l.Normal * (l.Depth + cc.radius + Skin);

        private bool Exhausted()
        {
            if (stamina == null) TryGetComponent(out stamina);
            return stamina != null && stamina.IsExhausted;
        }

        /// <summary>사다리 앞에서 사다리 쪽으로(아래·중간), 또는 꼭대기 턱 끝에서 바깥쪽으로 가면 잡는다.</summary>
        private bool TryStartLadder(Vector3 dir, bool grounded)
        {
            var all = Ladder.All;
            if (all.Count == 0 || regrab > 0f || Exhausted()) return false;
            float hu = HumanUnit();
            Vector3 pos = transform.position;
            foreach (var l in all)
            {
                if (l == null) continue;
                Vector3 n = l.Normal, right = Vector3.Cross(Vector3.up, n);
                Vector3 rel = pos - l.Bottom;
                if (Mathf.Abs(Vector3.Dot(rel, right)) > l.Width * 0.5f + 0.25f) continue;
                float outward = Vector3.Dot(rel, n);
                float lo = LadderBottomRoot(l, hu), hi = LadderTopRoot(l, hu);
                // 아래·중간: 사다리 바깥쪽 가까이서 사다리(벽) 쪽으로
                if (outward > -0.05f && outward < l.Depth + cc.radius + 0.55f && Vector3.Dot(dir, -n) > 0.7f
                    && pos.y >= l.BottomY - 0.3f && pos.y < l.TopY - 0.6f)
                {
                    StartLadder(l, Mathf.Clamp(Mathf.Max(pos.y, l.BottomY) + LadderFirstRung - BracedFeet * hu, lo, hi));
                    return true;
                }
                // 꼭대기: 위층 바닥에 서서 턱 끝(사다리 위) 바깥쪽으로 걸어 나감 → 내려가는 사다리를 잡는다
                if (grounded && Mathf.Abs(pos.y - l.TopY) < 0.35f && outward < 0.1f && outward > -(cc.radius + 0.9f) && Vector3.Dot(dir, n) > 0.7f)
                {
                    StartLadder(l, hi);
                    return true;
                }
            }
            return false;
        }

        private void StartLadder(Ladder l, float root)
        {
            StartAction(TraversalAction.Ladder, 0f);
            ladder = l;
            ladderRoot = root;
            braced = true;
            faceYaw = YawOf(-l.Normal);
            airborne = false;
            jumpedFlag = false;
        }

        private void HandleLadder(in CharacterInputFrame input, bool dead, bool stunned)
        {
            if (ladder == null || !ladder.isActiveAndEnabled) { FallOffLadder(false); return; }
            if (dead || stunned || (stamina != null && stamina.Current <= 0f)) { FallOffLadder(true); return; } // 스태미나를 다 씀: 손이 풀려 뒤로
            if (input.Jump) { FallOffLadder(true); hangDrop = true; return; } // 스스로 놓음: 놓은 낙하(아주 높지 않으면 다치지 않음)

            float hu = HumanUnit(), dt = Time.fixedDeltaTime;
            float lo = LadderBottomRoot(ladder, hu), hi = LadderTopRoot(ladder, hu);
            // 오르내리기는 방향키가 가리키는 쪽(시야 기준)으로: 벽 쪽 = 오르기, 벽 바깥쪽 = 내리기 — 위에서 바깥으로 걸어 나와 잡으면 그대로 내려간다.
            float climb = Vector3.Dot(MoveDirection(input), -ladder.Normal);
            if (climb > 0.3f)
            {
                ladderRoot = Mathf.Min(hi, ladderRoot + ladderUpSpeed * dt);
                if (ladderRoot >= hi - 1e-3f && Mathf.Abs(transform.position.y - hi) < 0.05f) TryLadderTop();
            }
            else if (climb < -0.3f)
            {
                ladderRoot = Mathf.Max(lo, ladderRoot - ladderDownSpeed * dt);
                if (ladderRoot <= lo + 1e-3f && Mathf.Abs(transform.position.y - lo) < 0.05f) StepOffLadder();
            }
        }

        /// <summary>위 끝: 그 턱에 매달린 상태로 넘어간다(앞을 누르고 있으면 바로 올라간다). 잡을 턱이 없으면 거기 머문다.</summary>
        private void TryLadderTop()
        {
            float hu = HumanUnit();
            Vector3 n = ladder.Normal;
            float above = ladder.TopY - transform.position.y;
            if (!FindLedge(transform.position, -n, above - 0.4f * hu, above + 0.4f * hu, ladder.Depth + 0.3f, out var l)) return;
            ledge = l;
            braced = true;
            hangPos = l.Face + l.Normal * (cc.radius + Skin);
            hangPos.y = l.Top - BracedHangTop * hu;
            faceYaw = YawOf(-l.Normal);
            StartAction(TraversalAction.Hang, 0f);
            TryStartHangClimb(); // 오르던 그대로 턱 위로
        }

        /// <summary>꼭대기 턱에 매달린 채 뒤(S): 올라온 사다리가 바로 아래 있으면 다시 사다리로.</summary>
        private bool TryReturnToLadder()
        {
            if (ladder == null || !ladder.isActiveAndEnabled || Exhausted()) return false;
            Vector3 rel = transform.position - ladder.Bottom;
            if (Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, ladder.Normal))) > ladder.Width * 0.5f + 0.3f) return false;
            if (Mathf.Abs(ladder.TopY - (transform.position.y + BracedHangTop * HumanUnit())) > 0.5f) return false;
            StartLadder(ladder, LadderTopRoot(ladder, HumanUnit()));
            return true;
        }

        /// <summary>아래 끝에서 뒤(S): 사다리 앞 바닥에 내려선다.</summary>
        private void StepOffLadder()
        {
            Vector3 p = LadderPoint(ladder);
            p.y = ladder.BottomY + 1f;
            if (!Cast(p, Vector3.down, 1.6f, out var floor) || floor.normal.y < 0.7f) return; // 아래가 바닥이 아니면(허공) 그대로 매달려 있다
            cc.enabled = false;
            transform.position = floor.point;
            cc.enabled = true;
            action = TraversalAction.None;
            actionTime = actionDuration = 0f;
            ladder = null;
            regrab = regrabDelay;
            airborne = false;
        }

        /// <summary>손이 풀려 떨어진다(backward = 하늘을 보며 등으로). 벽 바깥으로 조금 밀린다. 떨어진 높이는 여기부터 잰다.</summary>
        private void FallOffLadder(bool backward)
        {
            Vector3 n = ladder != null ? ladder.Normal : -transform.forward;
            ladder = null;
            LiftOutOfFloor();
            StartAction(TraversalAction.Fall, 0f);
            fallBackward = backward;
            airborne = true;
            airStartY = peakY = transform.position.y;
            jumped = jumpedFlag = false;
            backAtStart = backward;
            safeJump = false;
            regrab = knockOffRegrab;
            if (ladderFallPush > 0f) Knockback.Push(gameObject, n * ladderFallPush, 0.35f, 0f, stun: false, impactDamage: false);
        }

        /// <summary>사다리에서 넉백되면 손이 풀려 벽 바깥으로 떨어진다(미는 방향은 벽 바깥쪽으로).</summary>
        private bool LadderKnockback(ref Vector3 velocity)
        {
            if (action != TraversalAction.Ladder || ladder == null) return false;
            Vector3 n = ladder.Normal;
            Vector3 v = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float speed = Mathf.Max(v.magnitude, knockOffMinSpeed);
            Vector3 side = Vector3.ProjectOnPlane(v, n);
            Vector3 dir = side + n * Mathf.Max(Vector3.Dot(v, n), speed * knockOffOutward);
            velocity = (dir.sqrMagnitude > 1e-4f ? dir.normalized : n) * speed;
            float push = ladderFallPush;
            ladderFallPush = 0f; // 넉백이 이미 민다
            FallOffLadder(true);
            ladderFallPush = push;
            return true;
        }

        private bool LadderVelocity(out Vector3 worldVelocity)
        {
            worldVelocity = Vector3.zero;
            if (action != TraversalAction.Ladder || ladder == null) return false;
            Vector3 target = LadderPoint(ladder);
            target.y = ladderRoot;
            worldVelocity = Vector3.ClampMagnitude((target - transform.position) / ladderSnapTime, 25f);
            return true;
        }
    }
}

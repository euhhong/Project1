using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI 생존자의 판자 쓰기(AiBotInput이 근접 AI가 만든 입력 위에 덮는다 — 판자를 내릴 수 있는 캐릭터(PalletUser)만).
    /// 1) 판자로: 감시자가 engageRange 안에서 쫓아오면, 감시자보다 내가 더 가까운 세워진 판자(벽에 가리지 않은 것) 틈을 감시자 반대쪽으로 달려 빠져나간다.
    /// 2) 내리기: 틈을 지나 감시자 반대편에 섰고 감시자가 틈 안·바로 앞까지 오면 스페이스바(= 판자 내리기, PalletUser) — 틈 안이면 깔려 기절.
    /// 3) 끼고 싸우기: 내린 판자를 사이에 두고 늘 감시자 반대편을 잡는다 — 감시자가 넘어오거나 돌아와 같은 편이 되면 판자를 넘어(스페이스바) 다시 반대편으로.
    ///    반대편에서 감시자가 바짝 붙으면(closeRange) 근접 AI에 맡긴다.
    /// 판자 좌표: 앞(+Z) = 넘어가는 쪽(틈을 가로지름), 옆(X) = 사람이 지나가는 통로.
    /// </summary>
    public sealed class AiPalletUser
    {
        private const float EngageRange = 12f;      // 감시자가 이 안이면 판자를 찾는다
        private const float SeekRange = 14f;        // 이 안의 판자만
        private const float SeekTimeout = 6f;       // 판자까지 이만큼 못 가면 포기
        private const float DropHunterRange = 3.2f; // 감시자가 틈 가운데에서 이 안이면 내린다
        private const float HoldDistance = 2.2f;    // 내린 판자에서 이만큼 떨어진 반대편에 선다
        private const float CloseRange = 2.4f;      // 감시자가 이 안이면 근접 AI에 맡긴다
        private const float HoldTime = 12f;         // 내린 뒤 판자를 끼고 있는 최대 시간
        private const float RetryDelay = 3f;        // 포기한 뒤 다시 찾기까지
        private const float VaultBackMinRange = 1.2f; // 감시자가 같은 편이어도 이보다 가까우면(넘다 맞음) 근접 AI에 맡긴다
        private const float VaultReach = 1.1f;      // 판자 가장자리에서 이 거리 안이면 스페이스바(넘기)
        private const float PunishMinStun = 0.6f;   // 감시자 기절이 이만큼(초) 넘게 남았으면 판자를 넘어가 때린다

        private readonly Transform self;
        private Pallet pallet;
        private float seekStart, holdUntil, nextSeek;

        private AiRouteFinder route;
        private ICharacterState state;

        public AiPalletUser(Transform self) { this.self = self; }

        /// <summary>길 찾기(NavMesh·창틀 넘기)를 따라 target으로 달린다 — 벽을 돌아 판자 통로 입구까지.</summary>
        private void RouteSteer(ref CharacterInputFrame f, float now, Vector3 target)
        {
            route ??= new AiRouteFinder(self.gameObject);
            if (state == null) self.TryGetComponent(out state);
            Vector3 dir = route.Steer(target, now, true, state == null || state.IsGrounded, out bool jump);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = target - self.position;
            Steer(ref f, dir, run: true);
            f.Jump = jump;
        }

        /// <summary>근접 AI가 만든 이번 틱 입력 f 위에 판자 쓰기를 덮는다. hunter = 상대 루트.</summary>
        public void Apply(ref CharacterInputFrame f, float now, Transform hunter, bool fleeing = false)
        {
            if (hunter == null) { pallet = null; return; }
            Vector3 me = self.position, opp = hunter.position;
            // 판자 내리기 범위 안의 스페이스바 = 판자 내리기(PalletUser) — 근접 AI의 점프(달리기 점프 공격·깡충)로 엉뚱하게 내리지 않게 막는다.
            // 판자는 아래 '내리기'에서 정한 때에만 내린다.
            if (f.Jump && Pallet.TryFind(me, Quaternion.Euler(0f, f.LookYaw, 0f) * Vector3.forward, out _, out _)) f.Jump = false;
            if (TrySnapDrop(ref f, me, opp, now)) return; // 무엇을 하던 중이든: 감시자가 판자가 넘어지는 자리에 들어오면 바로 내린다
            if (fleeing) { pallet = null; return; } // 도주 중(AiRetreat)에는 판자를 찾거나 끼고 버티지 않는다(바로 내리기만)
            float hunterDist = Flat(opp - me).magnitude;
            if (AiSkillYield.Now(self, hunterDist)) return; // 가는 길에 스킬각(재사용 대기 끝 + 가까움): 근접 AI가 스킬을 쓰게 맡긴다

            if (pallet == null || pallet.State == Pallet.PalletState.Falling)
            {
                if (pallet == null && (now < nextSeek || hunterDist > EngageRange || hunterDist < CloseRange)) return;
                if (pallet == null) { pallet = Choose(me, opp); seekStart = now; if (pallet == null) { nextSeek = now + 1f; return; } }
                else return; // 넘어가는 중 — 근접 AI 그대로
            }

            Transform p = pallet.transform;
            Vector3 center = p.position + p.forward * (pallet.Gap * 0.5f); // 틈 가운데(바닥)
            Vector3 lane = p.right;                                          // 지나가는 통로 방향
            float hunterSide = Mathf.Sign(Vector3.Dot(opp - center, lane));  // 감시자가 있는 통로 쪽
            if (hunterSide == 0f) hunterSide = 1f;

            if (pallet.State == Pallet.PalletState.Up)
            {
                // 2) 내리기: 감시자 반대편(통로)에 섰고, 감시자가 틈 가운데 가까이 왔으면
                float mySide = Vector3.Dot(me - center, lane);
                bool pastGap = mySide * hunterSide < -0.3f;
                float hunterToGap = Flat(opp - center).magnitude;
                // 판자는 몸 앞 반원 안에 있어야 내려진다(Pallet.TryFind) — 달아나며 등진 채면 판자 쪽으로 돌아서서 내린다(같은 틱에 시야를 판자로)
                Vector3 toPallet = Flat(p.position - me);
                if (pastGap && hunterToGap < DropHunterRange && Pallet.TryFind(me, toPallet, out Pallet near, out _) && near == pallet)
                {
                    Face(ref f, toPallet);
                    f.Move = Vector2.zero;
                    f.Jump = true; // PalletUser: 판자 범위 안의 스페이스바 = 내리기(시야 방향 LookYaw로 판자를 찾음)
                    ClearAttack(ref f);
                    holdUntil = now + HoldTime;
                    return;
                }
                if (now - seekStart > SeekTimeout || hunterDist > EngageRange + 4f) { Give(now); return; }
                // 1) 판자로: 최종 목표 = 감시자 반대편 통로 끝. 통로 밖이면 먼저 내 쪽 통로 입구로(길 찾기로 벽을 돌아) 가서, 통로에 들어서면 곧게 틈을 지난다
                //    (감시자 쪽에서 와도 판자 면에 붙어 서지 않고 반드시 틈을 지나 반대편으로)
                Vector3 goal = center - lane * hunterSide * (pallet.Width * 0.5f + 0.9f);
                if (pastGap && Flat(goal - me).magnitude < 0.6f) { Face(ref f, opp - me); f.Move = Sway(now); ClearAttack(ref f); return; } // 감시자를 기다리며 좌우로 살짝(서 있지 않게)
                Vector3 local = p.InverseTransformPoint(me);
                bool inLane = local.z >= -0.4f && local.z <= pallet.Gap + 0.4f && Mathf.Abs(local.x) <= pallet.Width * 0.5f + 2.2f; // 통로 띠 안
                Vector3 target = goal;
                if (!inLane && !pastGap) target = center + lane * Mathf.Sign(mySide == 0f ? hunterSide : mySide) * (pallet.Width * 0.5f + 1.8f); // 내 쪽 입구
                if (inLane) Steer(ref f, target - me, run: true);
                else RouteSteer(ref f, now, target);
                ClearAttack(ref f);
                // 판자 내리기 범위에서 길 찾기 점프가 판자를 엉뚱하게 내리지 않게
                if (f.Jump && Pallet.TryFind(me, Quaternion.Euler(0f, f.LookYaw, 0f) * Vector3.forward, out _, out _)) f.Jump = false;
                return;
            }

            // 3) 끼고 싸우기(내려진 판자): 늘 감시자 반대편을 잡는다 — 감시자가 넘어오거나 돌아와 같은 편이 되면 판자를 넘어 다시 반대편으로(심리전)
            if (now > holdUntil || hunterDist > EngageRange + 4f) { Give(now); return; }
            float mySideNow = Vector3.Dot(me - center, lane);
            bool sameSide = mySideNow * hunterSide > 0f;
            // 감시자가 기절(판자에 깔림 등): 판자를 넘어 건너가 붙어서 때린다 — 같은 편이 되면 근접 AI(기절한 상대 몰아치기)에 맡긴다
            if (hunter.TryGetComponent(out ICharacterStun hs) && hs.IsStunned && hs.StunRemaining > PunishMinStun)
            {
                holdUntil = Mathf.Max(holdUntil, now + 3f);
                if (sameSide) return;
                Vector3 overDir = -lane * Mathf.Sign(mySideNow == 0f ? -hunterSide : mySideNow); // 판자를 가로질러 감시자 쪽으로
                Vector3 toward = (overDir * 1.2f + Flat(opp - me).normalized * 0.5f).normalized;
                Steer(ref f, toward, run: true);
                ClearAttack(ref f);
                if (Mathf.Abs(mySideNow) < pallet.Width * 0.5f + VaultReach) f.Jump = true; // 쓰러진 판자 넘기(또는 올라섬)
                return;
            }
            if (sameSide && hunterDist > VaultBackMinRange)
            {
                // 판자 쪽으로 곧장 달려가 넘는다(스페이스바 — 쓰러진 판자 넘기, CharacterTraversal)
                Vector3 across = -lane * Mathf.Sign(mySideNow == 0f ? hunterSide : mySideNow);
                Vector3 toBoard = center - me; toBoard.y = 0f;
                Vector3 go = (across * 1.2f + toBoard.normalized * 0.4f).normalized;
                Steer(ref f, go, run: true);
                ClearAttack(ref f);
                if (Mathf.Abs(mySideNow) < pallet.Width * 0.5f + VaultReach) f.Jump = true;
                holdUntil = Mathf.Max(holdUntil, now + 4f); // 넘는 동안 그만두지 않게
                return;
            }
            if (hunterDist < CloseRange) return; // 반대편이지만 바짝 붙음 — 근접 AI(공격·막기)
            Vector3 hold = center - lane * hunterSide * HoldDistance;
            if (Flat(hold - me).magnitude < 0.5f) { Face(ref f, opp - me); f.Move = Sway(now); return; } // 판자 너머 감시자를 보며 좌우로 살짝(심리전, 서 있지 않게)
            Steer(ref f, hold - me, run: false, faceTarget: opp - me);
        }

        /// <summary>
        /// 바로 내리기: 내가 내릴 수 있는 세워진 판자(판자 쪽으로 돌아서면 앞 반원)가 있고, 감시자가 그 판자가 넘어지는 자리(틈) 안에 있거나
        /// 내 반대편 통로에서 막 들어오려 하면 — 판자 쪽을 보고 스페이스바. 판자를 찾던 중이 아니어도(가까이서 싸우던 중이어도) 한다.
        /// </summary>
        private bool TrySnapDrop(ref CharacterInputFrame f, Vector3 me, Vector3 opp, float now)
        {
            foreach (var p in Pallet.All)
            {
                if (p == null || p.State != Pallet.PalletState.Up) continue;
                Vector3 toPallet = Flat(p.transform.position - me);
                if (toPallet.sqrMagnitude > 16f) continue; // 4m 밖
                if (!Pallet.TryFind(me, toPallet, out Pallet near, out _) || near != p) continue;
                Vector3 hl = p.transform.InverseTransformPoint(opp), ml = p.transform.InverseTransformPoint(me);
                if (Mathf.Abs(hl.y) > 3f) continue; // 다른 층
                float halfW = p.Width * 0.5f;
                bool inGap = Mathf.Abs(hl.x) <= halfW + 0.35f && hl.z >= -0.3f && hl.z <= p.Gap + 0.3f;              // 깔리는 자리
                bool coming = Mathf.Abs(hl.x) <= halfW + 1.6f && hl.z >= -0.3f && hl.z <= p.Gap + 0.3f              // 통로로 막 들어옴
                              && hl.x * ml.x < 0f && Mathf.Abs(ml.x) > halfW * 0.5f;                                  // 내가 반대편
                if (!inGap && !coming) continue;
                Face(ref f, toPallet);
                f.Move = Vector2.zero;
                f.Jump = true;
                ClearAttack(ref f);
                pallet = p; holdUntil = now + HoldTime; seekStart = now;
                return true;
            }
            return false;
        }

        /// <summary>감시자보다 내가 더 가깝고, 벽에 가리지 않은 가장 가까운 세워진 판자.</summary>
        private static Pallet Choose(Vector3 me, Vector3 hunter)
        {
            Pallet best = null; float bestD = float.MaxValue;
            foreach (var p in Pallet.All)
            {
                if (p == null || p.State != Pallet.PalletState.Up) continue;
                Vector3 c = p.transform.position + p.transform.forward * (p.Gap * 0.5f);
                if (Mathf.Abs(c.y - me.y) > 1.5f) continue; // 다른 층
                float d = Flat(c - me).magnitude;
                if (d > SeekRange || d >= bestD) continue;
                if (Flat(c - hunter).magnitude < d * 0.8f) continue; // 감시자 쪽에 더 가까움
                if (Blocked(me, c)) continue;
                best = p; bestD = d;
            }
            return best;
        }

        private static readonly RaycastHit[] hits = new RaycastHit[8];

        private static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 a = from + Vector3.up * 1f, b = to + Vector3.up * 1f, d = b - a;
            int n = Physics.RaycastNonAlloc(a, d.normalized, hits, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || c.GetComponentInParent<CharacterIdentity>() != null || c.GetComponentInParent<Pallet>() != null) continue;
                return true;
            }
            return false;
        }

        private void Give(float now) { pallet = null; nextSeek = now + RetryDelay; }

        private readonly float swayPhase = Random.Range(0f, 6.28f);

        /// <summary>제자리에 서 있지 않게: 바라보는 쪽 기준 좌우로 살짝 오가는 이동 입력.</summary>
        private Vector2 Sway(float now) => new(Mathf.Sin(now * 2.6f + swayPhase) >= 0f ? 0.55f : -0.55f, 0f);

        private static void Steer(ref CharacterInputFrame f, Vector3 worldDir, bool run, Vector3? faceTarget = null)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) { f.Move = Vector2.zero; return; }
            Face(ref f, faceTarget ?? worldDir);
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, f.LookYaw, 0f)) * worldDir.normalized;
            f.Move = new Vector2(local.x, local.z);
            f.Run = run;
        }

        private static void Face(ref CharacterInputFrame f, Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) f.LookYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private static void ClearAttack(ref CharacterInputFrame f)
        {
            f.LightAttack = false; f.AttackPressed = false; f.AttackHeld = false; f.HeavyCharging = false; f.HeavyRelease = false;
            f.Skill = false; f.SkillHeld = false;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}

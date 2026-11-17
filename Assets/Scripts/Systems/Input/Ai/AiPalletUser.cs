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

        private readonly Transform self;
        private Pallet pallet;
        private float seekStart, holdUntil, nextSeek;

        public AiPalletUser(Transform self) { this.self = self; }

        /// <summary>근접 AI가 만든 이번 틱 입력 f 위에 판자 쓰기를 덮는다. hunter = 상대 루트.</summary>
        public void Apply(ref CharacterInputFrame f, float now, Transform hunter)
        {
            if (hunter == null) { pallet = null; return; }
            Vector3 me = self.position, opp = hunter.position;
            float hunterDist = Flat(opp - me).magnitude;

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
                if (pastGap && hunterToGap < DropHunterRange && Pallet.TryFind(me, self.forward, out Pallet near, out _) && near == pallet)
                {
                    f.Jump = true; // PalletUser: 판자 범위 안의 스페이스바 = 내리기
                    ClearAttack(ref f);
                    holdUntil = now + HoldTime;
                    return;
                }
                if (now - seekStart > SeekTimeout || hunterDist > EngageRange + 4f) { Give(now); return; }
                // 1) 판자로: 틈을 지나 감시자 반대편 통로 끝으로 달린다(이미 지났으면 그 자리에서 감시자를 기다림)
                Vector3 goal = center - lane * hunterSide * (pallet.Width * 0.5f + 0.9f);
                if (pastGap && Flat(goal - me).magnitude < 0.6f) { Face(ref f, opp - me); f.Move = Vector2.zero; ClearAttack(ref f); return; }
                Steer(ref f, goal - me, run: true);
                ClearAttack(ref f);
                return;
            }

            // 3) 끼고 싸우기(내려진 판자): 늘 감시자 반대편을 잡는다 — 감시자가 넘어오거나 돌아와 같은 편이 되면 판자를 넘어 다시 반대편으로(심리전)
            if (now > holdUntil || hunterDist > EngageRange + 4f) { Give(now); return; }
            float mySideNow = Vector3.Dot(me - center, lane);
            bool sameSide = mySideNow * hunterSide > 0f;
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
            if (Flat(hold - me).magnitude < 0.5f) { Face(ref f, opp - me); f.Move = Vector2.zero; return; }
            Steer(ref f, hold - me, run: false, faceTarget: opp - me);
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

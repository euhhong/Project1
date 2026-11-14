using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// AI의 머리(전술): 상대 모습(AiOpponentView, 반응 지연 포함)과 내 상태를 보고 다음 동작을 고르고, 동작을 틱마다 이동·키 입력으로 풀어 낸다(키는 AiKeyPresser).
    /// - 교전 거리(engageRadius) 밖: 다가간다(멀면 달림 — 스태미나를 보며), 원거리 스킬, 돌진형이면 유도 시작.
    /// - 교전 거리 안: 약공격 · 꾹 눌러 강공격(생존자는 주먹 막기 → 떼면 돌려차기) · 달리기 점프 공격(+생존자 두 번째 날아 차기) · 고유 스킬 ·
    ///   옆걸음 · 뒤로 빠지기 · 들어갔다 빠지는 페인트 · 돌진형 유도(AiChargeBaiter)를 거리·상대 상태·재사용 대기로 가중치를 둬 무작위로 고른다.
    ///   같은 동작을 연달아 고르면 가중치를 깎는다(repeatPenalty).
    /// - 반응(지금 하던 이동을 끊음): 상대 돌진이 나를 향함 → 옆으로 피함. 정면에서 상대 공격 준비 → 막기/뒤로/옆으로/맞받아 치기.
    ///   상대가 후딜레이·기절·스킬로 묶임 → 파고들어 응징. 한 동작에는 한 번만 반응한다.
    /// - 상대가 돌진형(전위·광대)이면: 사거리 끝에서 평타를 내밀지 않고(헛치면 박힘) 옆걸음을 늘리고, 바짝 붙어 돌진 거리를 주지 않거나 돌진을 옆으로 피한다.
    /// </summary>
    public sealed class AiMeleeTactics
    {
        private enum Maneuver { None, Approach, Strafe, BackOff, Feint, Hold, Light, Heavy, Parry, RunJump, Skill, Dodge, Bait }

        private const float ReattackGap = 0.55f; // 내 공격이 끝난 뒤 다시 누르기까지(CharacterAttack의 다시 공격 대기보다 조금 길게)

        private readonly GameObject self;
        private readonly Transform tr;
        private readonly AiMeleeSettings cfg;
        private readonly AiOpponentView view;
        private readonly AiKeyPresser keys = new();
        private readonly AiChargeBaiter baiter = new();

        private ICharacterAttackState attack;
        private ICharacterState state;
        private ICharacterStun stun;
        private ICharacterStamina stamina;
        private ICharacterHealth health;
        private ICharacterSkillState skill;
        private ICharacterParryState parry;
        private ICharacterTraversalState traversal;

        private Maneuver maneuver, lastChosen;
        private float mStart, mUntil, mDir, mValue, mTime;
        private int mStep;
        private bool mFlag;
        private float nextDecision, reaction = 0.2f, yaw;
        private bool yawInit;
        private float lastOwnAttack = -9f, lastSkillUse = -9f, lastDodgeEnd = -9f;
        private float selfSkillStart = -1f;
        private bool selfSkillRepressed, selfSlamSent, chargeCancelSent;
        private SkillState prevSelfSkill;

        // 반응을 한 동작에 한 번만: 상대가 새 동작을 시작할 때마다 늘어나는 번호
        private int oppActionKey, reactedKey = -1, punishedKey = -1;
        private CharacterAttackState prevSeenAttack;
        private SkillState prevSeenSkill;
        private bool prevSeenStunned;

        /// <summary>디버그: 지금 하고 있는 동작 이름.</summary>
        public string Current => maneuver.ToString();

        /// <summary>디버그: 지금까지 고른 동작 수(동작별).</summary>
        public readonly int[] Counts = new int[System.Enum.GetValues(typeof(Maneuver)).Length];
        public static string NameOf(int i) => ((Maneuver)i).ToString();
        public int ChargesFired { get; private set; }

        public AiMeleeTactics(GameObject self, AiMeleeSettings settings, AiOpponentView view)
        {
            this.self = self;
            tr = self.transform;
            cfg = settings;
            this.view = view;
        }

        private void RefreshComponents()
        {
            if (attack == null) self.TryGetComponent(out attack);
            if (state == null) self.TryGetComponent(out state);
            if (stun == null) self.TryGetComponent(out stun);
            if (stamina == null) self.TryGetComponent(out stamina);
            if (health == null) self.TryGetComponent(out health);
            if (skill == null) self.TryGetComponent(out skill);
            if (parry == null) self.TryGetComponent(out parry);
            if (traversal == null) self.TryGetComponent(out traversal);
            baiter.WhiffChance = cfg.chargeOnWhiff;
            baiter.OutOfRangeSwingChance = cfg.chargeOnOutOfRangeSwing;
            baiter.ParryChance = cfg.chargeOnParry;
            baiter.SkillBusyChance = cfg.chargeOnSkillBusy;
            baiter.AfterDodgeChance = cfg.chargeAfterDodge;
            baiter.MinPatience = cfg.chargePatienceMin;
            baiter.MaxPatience = Mathf.Max(cfg.chargePatienceMin, cfg.chargePatienceMax);
            baiter.TurnRate = cfg.chargeTurnRate;
        }

        private bool SelfHunter => traversal != null && traversal.HunterStyle;
        private float MyReach => SelfHunter ? cfg.hunterReach : cfg.survivorReach;
        private float ReachOf(in AiOpponentSnapshot s) => s.IsHunter ? cfg.hunterReach : cfg.survivorReach;
        private bool Grounded => state == null || state.IsGrounded;
        private bool CanRun => stamina == null || (!stamina.IsExhausted && stamina.Current > 8f);
        private AiSkillProfile Profile => skill != null ? AiSkillProfile.For(skill.SkillId) : AiSkillProfile.Default;
        private bool SkillReady => skill != null && !string.IsNullOrEmpty(skill.SkillId) && skill.State == SkillState.Ready && skill.CanUse;
        private bool CanAttack(float now) => attack != null && attack.AttackState == CharacterAttackState.None && !keys.AttackBusy && now - lastOwnAttack > ReattackGap;

        /// <summary>이번 틱 입력을 만든다(틱마다 한 번).</summary>
        public CharacterInputFrame Tick(float now, float dt)
        {
            RefreshComponents();
            if (!yawInit) { yaw = tr.eulerAngles.y; yawInit = true; }
            var f = new CharacterInputFrame { LookYaw = yaw, LookPitch = 5f };

            view.Capture(now);
            var me = view.Latest; // 위치는 몸으로 안다(지연 없음)
            if (!me.Valid || me.Dead || (health != null && health.IsDead))
            {
                keys.Clear(); maneuver = Maneuver.None; baiter.Stop();
                return f;
            }

            if (attack != null && attack.AttackState != CharacterAttackState.None) lastOwnAttack = now;
            TrackSelfSkill(now);

            Vector3 flat = me.Position - tr.position; flat.y = 0f;
            float dist = flat.magnitude;
            float targetYaw = dist > 0.01f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : yaw;
            var prof = Profile;
            bool chargeActive = prof.Charge && skill != null && skill.State == SkillState.Casting && skill.MotionPhase != AttackPhase.Windup;

            // 시야: 평소엔 목표를 본다. 돌진 중에는 사람이 마우스로 트는 속도까지만(앞질러 겨눔).
            if (chargeActive)
            {
                Vector3 lead = me.Position + me.Velocity * Mathf.Clamp(dist / 16f, 0f, 0.4f) - tr.position;
                float leadYaw = Mathf.Atan2(lead.x, lead.z) * Mathf.Rad2Deg;
                yaw = baiter.SteerYaw(yaw, leadYaw, dt);
            }
            else yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 900f * dt);
            f.LookYaw = yaw;
            f.HasAimPoint = true; f.AimFromCursor = true; f.AimPoint = me.Position; f.AimNormal = Vector3.up;

            if (stun != null && stun.IsStunned)
            {
                keys.Clear(); maneuver = Maneuver.None;
                nextDecision = now + Random.Range(cfg.decisionGapMin, cfg.decisionGapMax);
                return f;
            }

            var seen = view.Seen(now, reaction);
            TrackOpponentActions(seen);

            // 스킬이 몸을 묶는 동안(돌진·도약·시전·넘어짐)은 스킬만 다룬다. 켜 두는 스킬(아대)·걸으며 쓰는 스킬은 평소대로 싸우며 곁가지만 처리.
            bool casting = skill != null && skill.State == SkillState.Casting;
            if (skill != null && (skill.State == SkillState.Recovering || (casting && (skill.LocksActions || prof.Charge))))
            {
                RunOwnSkill(ref f, now, dist, prof, chargeActive, targetYaw);
                keys.Apply(ref f, now);
                return f;
            }
            if (casting) CastingExtras(now, dist, prof);

            React(now, dist, seen, prof);

            if (maneuver == Maneuver.None && now >= nextDecision) Choose(now, dist, seen, prof);

            Vector2 move = Vector2.zero; bool run = false;
            Execute(now, dist, seen, prof, ref move, ref run);
            f.Move = Vector2.ClampMagnitude(move, 1f);
            f.Run = run && CanRun && f.Move.y > 0.3f;
            keys.Apply(ref f, now);
            return f;
        }

        // ───────────────────────── 내 스킬 진행 ─────────────────────────

        private void TrackSelfSkill(float now)
        {
            var s = skill != null ? skill.State : SkillState.Ready;
            if (s == SkillState.Casting && prevSelfSkill != SkillState.Casting)
            {
                selfSkillStart = now; selfSkillRepressed = false; selfSlamSent = false; chargeCancelSent = false;
                if (Profile.Charge) { baiter.OnChargeStarted(); ChargesFired++; }
            }
            prevSelfSkill = s;
        }

        private void RunOwnSkill(ref CharacterInputFrame f, float now, float dist, in AiSkillProfile prof, bool chargeActive, float targetYaw)
        {
            if (maneuver != Maneuver.None) End(now);
            float elapsed = selfSkillStart >= 0f ? now - selfSkillStart : 0f;
            if (skill.State == SkillState.Recovering) return;
            if (chargeActive)
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(yaw, targetYaw));
                if (!chargeCancelSent && baiter.ShouldCancel(elapsed, off, dist)) { keys.TapSkill(); chargeCancelSent = true; }
                return;
            }
            if (prof.Charge) // 로켓을 들고 다니는 중: 목표 쪽으로 걷는다(떼면 돌진)
            {
                f.Move = dist > 3f ? Vector2.up : Vector2.zero;
                return;
            }
            CastingExtras(now, dist, prof);
        }

        /// <summary>시전 중 곁가지: 루키노 공중 내려찍기, 손전등 넣기.</summary>
        private void CastingExtras(float now, float dist, in AiSkillProfile prof)
        {
            float elapsed = selfSkillStart >= 0f ? now - selfSkillStart : 0f;
            if (prof.AirSlam && !selfSlamSent && !Grounded && dist < 2.2f) { keys.TapAttack(); selfSlamSent = true; }
            if (prof.RepressAfter > 0f && !selfSkillRepressed && elapsed > prof.RepressAfter) { keys.TapSkill(); selfSkillRepressed = true; }
        }

        // ───────────────────────── 상대 동작 읽기 · 반응 ─────────────────────────

        private void TrackOpponentActions(in AiOpponentSnapshot seen)
        {
            bool swingStart = seen.Swinging && prevSeenAttack is not (CharacterAttackState.Light or CharacterAttackState.Heavy);
            bool skillStart = seen.Skill == SkillState.Casting && prevSeenSkill != SkillState.Casting;
            bool stunStart = seen.Stunned && !prevSeenStunned;
            if (swingStart || skillStart || stunStart) oppActionKey++;
            prevSeenAttack = seen.Attack; prevSeenSkill = seen.Skill; prevSeenStunned = seen.Stunned;
        }

        private bool Interruptible => maneuver is Maneuver.None or Maneuver.Approach or Maneuver.Strafe or Maneuver.BackOff or Maneuver.Feint or Maneuver.Hold or Maneuver.Bait;

        private float FacingMe(in AiOpponentSnapshot s)
        {
            Vector3 toMe = tr.position - s.Position; toMe.y = 0f;
            return toMe.sqrMagnitude < 1e-4f ? 0f : Vector3.Angle(s.Forward, toMe);
        }

        private void React(float now, float dist, in AiOpponentSnapshot seen, in AiSkillProfile prof)
        {
            if (!seen.Valid || !Interruptible || reactedKey == oppActionKey) return;
            float facing = FacingMe(seen);

            // 1) 상대 돌진이 나를 향해 온다 → 옆으로 피한다(막기로는 못 막음).
            if (seen.Skill == SkillState.Casting && AiSkillProfile.For(seen.SkillId).Charge && seen.Velocity.magnitude > 7f && facing < 30f && dist < 12f)
            {
                reactedKey = oppActionKey;
                Vector3 cross = Vector3.Cross(seen.Forward, tr.position - seen.Position);
                StartDodge(now, cross.y >= 0f ? 1f : -1f, Random.value < 0.25f);
                return;
            }

            // 2) 정면에서 상대 공격이 온다(준비·판정 구간, 사거리 안) → 막기 / 뒤로 / 옆으로 / 맞받아 치기.
            float oppReach = ReachOf(seen) + (seen.RunJumpAttack ? 2f : 0.6f);
            if (seen.Threatening && dist < oppReach && facing < 60f)
            {
                reactedKey = oppActionKey;
                bool canParry = parry != null && !SelfHunter && CanAttack(now);
                bool chargeReady = prof.Charge && SkillReady;
                float wParry = canParry ? cfg.parryReact * (chargeReady ? 0.3f : 1f) : 0f;
                float wBack = cfg.backOffReact * (chargeReady ? 1.4f : 1f);
                float wDodge = cfg.dodgeReact * (chargeReady ? 2f : 1f);
                float wTrade = SelfHunter && CanAttack(now) && dist <= MyReach ? cfg.tradeReact : 0f;
                float wIgnore = 0.1f;
                int pick = Pick(Random.value * (wParry + wBack + wDodge + wTrade + wIgnore), wParry, wBack, wDodge, wTrade);
                switch (pick)
                {
                    case 0: Begin(now, Maneuver.Parry, 1.6f); break;
                    case 1: Begin(now, Maneuver.BackOff, Random.Range(0.3f, 0.55f)); mDir = Random.Range(-0.5f, 0.5f); break;
                    case 2: StartDodge(now, Random.value < 0.5f ? -1f : 1f, false); break;
                    case 3: Begin(now, Maneuver.Light, 0.8f); break;
                }
                return;
            }

            // 3) 틈: 상대가 후딜레이 · 기절 · 스킬로 묶임 → 파고들어 응징(돌진형은 유도 쪽에서 박치기로 처리).
            bool opening = seen.Recovering || seen.Stunned || seen.SkillBusy;
            if (opening && punishedKey != oppActionKey && dist < MyReach + 1.6f && CanAttack(now) && !(prof.Charge && SkillReady && maneuver == Maneuver.Bait && !seen.Stunned))
            {
                punishedKey = oppActionKey;
                reactedKey = oppActionKey;
                bool heavy = seen.Stunned ? Random.value < 0.45f : Random.value < 0.2f;
                Begin(now, heavy ? Maneuver.Heavy : Maneuver.Light, 0.9f);
                if (heavy) mValue = Random.Range(0.4f, 0.7f);
            }
        }

        private static int Pick(float r, params float[] w)
        {
            for (int i = 0; i < w.Length; i++)
            {
                if (r < w[i]) return i;
                r -= w[i];
            }
            return -1;
        }

        // ───────────────────────── 다음 동작 고르기 ─────────────────────────

        private void Choose(float now, float dist, in AiOpponentSnapshot seen, in AiSkillProfile prof)
        {
            reaction = Random.Range(cfg.reactionMin, Mathf.Max(cfg.reactionMin, cfg.reactionMax));
            bool skillReady = SkillReady && now - lastSkillUse > cfg.skillGap;
            float hp = health != null && health.Max > 0f ? health.Current / health.Max : 1f;

            // 상황에 따라 먼저 하는 스킬: 켜 두는 스킬, 안전할 때 자가치료
            if (skillReady && prof.Toggle) { Begin(now, Maneuver.Skill, 0.3f); return; }
            if (skillReady && prof.SelfHeal && hp < 0.65f && (dist > 4.5f || seen.Stunned)) { Begin(now, Maneuver.Skill, 0.3f); return; }

            bool oppCharger = AiSkillProfile.For(seen.SkillId).Charge && seen.Skill == SkillState.Ready;
            bool skillInRange = skillReady && !prof.Charge && prof.Weight > 0f && dist >= prof.MinRange && dist <= prof.MaxRange;
            bool canAttack = CanAttack(now);
            float reach = MyReach;

            if (dist > cfg.engageRadius)
            {
                if (prof.Charge && skillReady && dist < 9.5f && Random.value < 0.6f) { Begin(now, Maneuver.Bait, Random.Range(2f, 4f)); return; }
                if (skillInRange && Random.value < 0.35f * prof.Weight * cfg.skillWeightScale) { Begin(now, Maneuver.Skill, 0.3f); return; }
                Begin(now, Maneuver.Approach, Random.Range(0.8f, 1.8f));
                mDir = Random.value < 0.3f ? (Random.value < 0.5f ? -0.6f : 0.6f) : 0f; // 가끔 비스듬히 다가감
                return;
            }

            float oppReach = ReachOf(seen);
            bool frontParry = seen.Parrying && FacingMe(seen) < 75f;
            var w = new float[Counts.Length];
            // 약공격: 닿는 거리에서. 막는 정면엔 거의 안 씀(막히면 내가 경직). 상대가 돌진형이면 사거리 끝 찌르기를 피한다.
            if (canAttack && dist <= reach + 0.4f)
            {
                float edge = oppCharger && dist > reach * 0.85f ? 0.25f : 1f;
                w[(int)Maneuver.Light] = cfg.lightWeight * edge * (frontParry ? 0.1f : 1f) * (seen.Recovering || seen.Stunned ? 2f : 1f);
            }
            // 꾹: 감시자 = 강공격(무겁고 느림), 생존자 = 막기 → 돌려차기(상대가 다가올 때 좋음)
            if (canAttack && dist <= reach + 1.2f)
                w[(int)Maneuver.Heavy] = cfg.heavyWeight * (frontParry ? 0.2f : 1f) * (seen.Stunned ? 2f : 1f) * (!SelfHunter && Vector3.Dot(seen.Velocity, (tr.position - seen.Position).normalized) > 1f ? 1.6f : 1f);
            // 달리기 점프 공격: 조금 떨어져 있고 스태미나가 있을 때
            if (canAttack && Grounded && dist >= 2.6f && dist <= cfg.engageRadius + 0.5f && (stamina == null || (!stamina.IsExhausted && stamina.Current >= 25f)))
                w[(int)Maneuver.RunJump] = cfg.runJumpWeight * (frontParry ? 0.4f : 1f) * (oppCharger ? 1.3f : 1f);
            if (skillInRange) w[(int)Maneuver.Skill] = prof.Weight * cfg.skillWeightScale * (seen.Recovering || seen.Stunned ? 1.5f : 1f);
            if (prof.Charge && skillReady && dist >= 1.2f) w[(int)Maneuver.Bait] = cfg.baitWeight;
            w[(int)Maneuver.Strafe] = cfg.strafeWeight * (frontParry ? 2f : 1f) * (oppCharger ? 1.8f : 1f);
            if (dist < oppReach + 0.6f) w[(int)Maneuver.BackOff] = cfg.backOffWeight * (hp < 0.35f ? 2f : 1f) * (seen.Attack == CharacterAttackState.HeavyCharge && !seen.Parrying ? 2f : 1f);
            if (dist >= reach * 0.8f && dist <= oppReach + 2.2f) w[(int)Maneuver.Feint] = cfg.feintWeight * (oppCharger ? 0.5f : 1f);
            if (dist > reach) w[(int)Maneuver.Approach] = 1.2f + (dist - reach) * 0.6f * (oppCharger ? 1.5f : 1f); // 돌진형 상대엔 바짝 붙어 돌진 거리를 안 줌
            w[(int)Maneuver.Hold] = frontParry ? 1f : 0.35f; // 막는 상대는 기다리면 먼저 뗀다

            if (lastChosen != Maneuver.None) w[(int)lastChosen] *= cfg.repeatPenalty;
            float sum = 0f;
            foreach (var x in w) sum += x;
            int pick = sum > 0f ? Pick(Random.value * sum, w) : (int)Maneuver.Hold;
            var m = pick < 0 ? Maneuver.Hold : (Maneuver)pick;

            switch (m)
            {
                case Maneuver.Light: Begin(now, m, 0.9f); break;
                case Maneuver.Heavy: Begin(now, m, 2f); mValue = SelfHunter ? Random.Range(0.4f, 0.9f) : Random.Range(0.45f, 1.1f); break;
                case Maneuver.RunJump: Begin(now, m, 2.6f); mValue = Random.Range(2.4f, 3.4f); mFlag = !SelfHunter && Random.value < 0.55f; break;
                case Maneuver.Skill: Begin(now, m, 0.3f); break;
                case Maneuver.Bait: Begin(now, m, Random.Range(1.5f, 3.5f)); break;
                case Maneuver.Strafe: Begin(now, m, Random.Range(0.4f, 1.1f)); mDir = Random.value < 0.5f ? -1f : 1f; mValue = Random.Range(reach, oppReach + 1.5f); break;
                case Maneuver.BackOff: Begin(now, m, Random.Range(0.3f, 0.7f)); mDir = Random.Range(-0.6f, 0.6f); break;
                case Maneuver.Feint: Begin(now, m, Random.Range(0.6f, 0.9f)); mTime = now + Random.Range(0.22f, 0.38f); mDir = Random.Range(-0.4f, 0.4f); break;
                case Maneuver.Approach: Begin(now, m, Random.Range(0.5f, 1.2f)); mDir = Random.value < 0.4f ? Random.Range(-0.7f, 0.7f) : 0f; break;
                default: Begin(now, Maneuver.Hold, Random.Range(0.2f, 0.5f)); break;
            }
        }

        private void Begin(float now, Maneuver m, float duration)
        {
            maneuver = m; lastChosen = m;
            mStart = now; mUntil = now + duration; mStep = 0; mFlag = false; mValue = 0f; mTime = now;
            Counts[(int)m]++;
        }

        private void End(float now)
        {
            if (maneuver == Maneuver.Dodge) lastDodgeEnd = now;
            maneuver = Maneuver.None;
            nextDecision = now + Random.Range(cfg.decisionGapMin, Mathf.Max(cfg.decisionGapMin, cfg.decisionGapMax));
        }

        private void StartDodge(float now, float side, bool jump)
        {
            Begin(now, Maneuver.Dodge, Random.Range(0.4f, 0.65f));
            mDir = side; mFlag = jump;
        }

        // ───────────────────────── 동작 풀어 내기 ─────────────────────────

        private void Execute(float now, float dist, in AiOpponentSnapshot seen, in AiSkillProfile prof, ref Vector2 move, ref bool run)
        {
            float reach = MyReach;
            switch (maneuver)
            {
                case Maneuver.None:
                    if (dist > reach) move = new Vector2(0f, 0.5f); // 고르는 사이에도 살짝 다가감
                    break;

                case Maneuver.Approach:
                    move = new Vector2(dist < cfg.runDistance ? mDir : 0f, 1f);
                    run = dist > cfg.runDistance;
                    if (dist <= reach * 0.95f || now >= mUntil) End(now);
                    break;

                case Maneuver.Strafe:
                    move = new Vector2(mDir, Mathf.Clamp((dist - mValue) * 0.8f, -0.6f, 0.6f));
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.BackOff:
                    move = new Vector2(mDir, -1f);
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Feint: // 쑥 들어갔다가(상대 공격 유도) 빠진다
                    move = now < mTime ? new Vector2(mDir, 1f) : new Vector2(-mDir, -1f);
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Hold:
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Dodge:
                    move = new Vector2(mDir, -0.15f);
                    run = true;
                    if (mFlag && now - mStart > 0.1f) { keys.Jump(); mFlag = false; }
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Light:
                    if (mStep == 0)
                    {
                        if (dist > reach * 0.9f && now - mStart < 0.7f) { move = Vector2.up; run = dist > reach + 1.5f; break; }
                        if (!CanAttack(now)) { if (now >= mUntil) End(now); break; }
                        int swipe = traversal != null && skill != null && skill.SkillId == "MindsEye_Thrust" && Random.value < 0.6f ? (Random.value < 0.5f ? -1 : 1) : 0;
                        keys.TapAttack(swipe);
                        mStep = 1; mTime = now;
                        break;
                    }
                    if (now - mTime > 0.15f && attack.AttackState == CharacterAttackState.None) End(now);
                    else if (now - mTime > 1.5f) End(now);
                    break;

                case Maneuver.Heavy:
                    if (mStep == 0)
                    {
                        if (mValue <= 0f) mValue = 0.6f;
                        if (dist > reach + 0.4f && now - mStart < 0.8f) { move = Vector2.up; break; }
                        if (!CanAttack(now)) { if (now - mStart > 1f) End(now); break; }
                        keys.HoldAttack(now, mValue + AiKeyPresser.HeavyHoldTime);
                        mStep = 1; mTime = now;
                        break;
                    }
                    if (keys.AttackHeld) move = new Vector2(0f, dist > reach * 0.8f ? 0.7f : 0f); // 모으는(막는) 동안 걷기 속도로 다가감
                    else if (attack.AttackState == CharacterAttackState.None && now - mTime > 0.2f) End(now);
                    if (now - mTime > 3f) { keys.ReleaseAttack(now); End(now); }
                    break;

                case Maneuver.Parry: // 생존자: 막는 자세로 상대 공격을 받아 내고, 지나가면 떼서 돌려차기
                    if (mStep == 0)
                    {
                        if (!CanAttack(now)) { End(now); break; }
                        keys.HoldAttack(now, 1.6f);
                        mStep = 1; mTime = now;
                        break;
                    }
                    if (keys.AttackHeld)
                    {
                        float held = keys.HeldFor(now);
                        bool passed = !seen.Threatening && held > AiKeyPresser.HeavyHoldTime + 0.1f;
                        if (passed && !mFlag) { mFlag = true; mValue = now + Random.Range(0.03f, 0.15f); } // 지나간 걸 보고 살짝 뒤에 뗌
                        if (mFlag && now >= mValue) keys.ReleaseAttack(now);
                    }
                    else if (attack.AttackState == CharacterAttackState.None && now - mTime > 0.3f) End(now);
                    if (now - mTime > 3f) End(now);
                    break;

                case Maneuver.RunJump:
                    move = Vector2.up; run = true;
                    switch (mStep)
                    {
                        case 0: // 달려서 속도를 붙인다
                        {
                            float fwd = state != null ? state.LocalPlanarVelocity.y : 0f;
                            float need = state != null ? Mathf.Lerp(state.WalkSpeed, state.RunSpeed, 0.75f) : 0f;
                            if (fwd >= need && dist <= mValue) { keys.Jump(); mStep = 1; mTime = now; }
                            else if (dist < 1.6f || now - mStart > 1.6f || !CanRun) End(now); // 너무 붙었거나 속도가 안 붙음 → 그만
                            break;
                        }
                        case 1: // 떠오르면 찬다(같은 틱에 뗌 = 짧게 누름)
                            if (!Grounded && now - mTime > 0.1f) { keys.TapAttack(); mStep = 2; mTime = now; }
                            else if (now - mTime > 0.5f) End(now);
                            break;
                        case 2: // 생존자: 한 번 더 눌러 두 번째 날아 차기(머리)
                            if (mFlag && !Grounded && now - mTime > 0.22f && dist < 3.5f) { keys.TapAttack(); mStep = 3; }
                            else if (Grounded && now - mTime > 0.2f) mStep = 3;
                            break;
                        default:
                            move = Vector2.zero; run = false;
                            if (Grounded && attack.AttackState == CharacterAttackState.None) End(now);
                            break;
                    }
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Skill:
                    if (mStep == 0)
                    {
                        if (!SkillReady) { End(now); break; }
                        if (prof.HoldSeconds > 0f) keys.HoldSkill(now, prof.HoldSeconds); else keys.TapSkill();
                        lastSkillUse = now; mStep = 1;
                        break;
                    }
                    if (now >= mUntil) End(now);
                    break;

                case Maneuver.Bait:
                    if (!(prof.Charge && SkillReady)) { End(now); break; }
                    float oppReach = ReachOf(seen);
                    move = baiter.Move(now, dist, oppReach);
                    Vector3 toT = view.Latest.Position - tr.position;
                    bool aligned = Mathf.Abs(Mathf.DeltaAngle(yaw, Mathf.Atan2(toT.x, toT.z) * Mathf.Rad2Deg)) < 10f;
                    if (aligned && baiter.ShouldFire(now, dist, oppReach, seen, FacingMe(seen), LineClear(), now - lastDodgeEnd))
                    {
                        if (prof.HoldSeconds > 0f) keys.HoldSkill(now, prof.HoldSeconds); else keys.TapSkill();
                        lastSkillUse = now;
                        End(now);
                        break;
                    }
                    if (now >= mUntil) End(now); // 다음 고르기에서 다시 유도할 수도(기다린 시간은 이어 감)
                    break;
            }
        }

        /// <summary>나 → 목표 사이(가슴 높이)가 벽에 막히지 않았는지.</summary>
        private bool LineClear()
        {
            var target = view.Target;
            if (target == null) return false;
            Vector3 from = tr.position + Vector3.up;
            Vector3 to = target.position + Vector3.up;
            if (!Physics.Raycast(from, to - from, out var hit, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.transform.IsChildOf(target) || hit.collider.transform.IsChildOf(tr);
        }
    }
}

using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 전용 효과음(FE): Resources/Sfx/Characters/{폴더}/{폴더}_{종류}_{번호}.wav 를 종류별로 묶어 그 캐릭터의 동작에 낸다.
    /// CharacterLoadout이 모든 캐릭터에 붙이고 Configure(id, 감시자?)로 폴더를 정한다(인스펙터 연결 없음). 소리 파일이 없는 종류는 조용히 넘어간다.
    /// - 공통: Attack(평타 휘두르기 — 있으면 일반 swing 대신) · Attack_Hit(평타 맞힘) · Attack_Wall(평타 벽 치기) · Footstep(발소리) · Vault(창틀 넘기)
    ///         · Jump/Land(있으면 일반 jump/land 대신) · PalletDrop(판자 내리기).
    /// - 스킬: 사냥터지기 Hook/Hook_Hit · 카우보이 LassoSpin(조준 중 반복)/Lasso · 공군 FlareGun/FlareGun_Hit · 광대 Dash(돌진하는 동안 점화→분사, 끊기지 않게 이어 감)
    ///         · 공장장 Shadow(자리 바꾸기·분신) · 붉은나비 ButterflyThrow/Leap · 맹인 Cane · 용병 ElbowPad · 안토니오 String_Start/Aim/Release · 재앙의 도마뱀 Land_Ice(도약 착지).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSoundSet : MonoBehaviour
    {
        private static readonly Dictionary<string, string> Folders = new()
        {
            ["Survivor_Doctor"] = "Doctor", ["Survivor_Coordinator"] = "Coordinator", ["Survivor_Cowboy"] = "Cowboy",
            ["Survivor_Forward"] = "Forward", ["Survivor_Mercenary"] = "Mercenary", ["Survivor_MindsEye"] = "MindsEye", ["Survivor_Thief"] = "Thief",
            ["Hunter_Antonio"] = "Antonio", ["Hunter_Gamekeeper"] = "Gamekeeper", ["Hunter_Geisha"] = "Michiko", ["Hunter_HellEmber"] = "Leo",
            ["Hunter_Luchino"] = "Luchino", ["Hunter_Ripper"] = "Ripper", ["Hunter_SmileyFace"] = "Joker", ["Hunter_Hastur"] = "Hastur",
        };

        // 스킬 단계 번호(각 스킬 클래스의 Phase 상수와 같아야 한다)
        private const int LeapSlamming = 1, LeapDropping = 2; // LeapSkill(재앙의 도마뱀 도약)
        private const int GrabWindup = 0, GrabExtending = 1, GrabRetracting = 2, GrabDragging = 4, GrabTowing = 5; // GrabShotSkill
        private const int FlareFired = 1;                                                      // FlareShotSkill
        private const int RocketCharging = 1;                                                  // RocketChargeSkill
        private const int WallDashing = 2;                                                     // WallDashSkill

        private static readonly string[] FootstepClips = { "footstep_01", "footstep_02", "footstep_03" }; // VARCO Sound 나무 바닥 한 걸음(작게)
        private const float FootVolume = 0.22f, VaultVolume = 0.8f, AttackVolume = 0.75f, SkillVolume = 0.85f;

        // 캐릭터 녹화의 반복음(발소리 Footstep, 올가미 돌리기)은 다른 소리(뛰는 소리·칙칙·꾸왁)로 들려 쓰지 않는다.
        // 발소리는 VARCO 공통 발소리(FootstepClips)를 작게, 광대 로켓 분사는 따로(RocketThrust).

        public static readonly List<CharacterSoundSet> All = new();

        /// <summary>감시자인지(심장 소리 SurvivorHeartbeat가 쓴다).</summary>
        public bool IsHunter { get; private set; }
        public string CharacterId { get; private set; }

        private readonly Dictionary<string, List<string>> clips = new();
        private readonly Dictionary<string, int> lastPick = new();
        private string folder;

        private CharacterAttack attack;
        private CharacterMotor motor;
        private CharacterSkill skill;
        private CharacterIdentity identity;
        private ICharacterTraversalState traversal;
        private IPalletUser pallet;
        private DollThrow doll;
        private ButterflyField butterflies;
        private NoteField notes;
        private TentacleField tentacleField;
        private readonly Dictionary<int, TentacleState> tentacleStates = new();

        private TraversalAction lastAction;
        private string lastSkillState;
        private int lastPhase = -1, palletCount, swapCount, throwCount, butterflyCount;
        private float lastNotePlace = -1f, stepDistance, lastLand;
        private bool notePlacing;
        private Vector3 lastPos;
        private AudioSource loopSource;
        private string loopKind;

        private Vector3 Body => transform.position + Vector3.up;

        /// <summary>CharacterLoadout이 캐릭터를 입힐 때 부른다.</summary>
        public void Configure(string id, bool hunter)
        {
            CharacterId = id;
            IsHunter = hunter;
            string f = id != null && Folders.TryGetValue(id, out var name) ? name : null;
            if (f == folder) return;
            folder = f;
            clips.Clear();
            lastPick.Clear();
            StopLoop();
            if (folder == null) return;
            LoadFolder(folder, null);
            if (Renamed.TryGetValue(folder, out var rename))
                foreach (var (from, to) in rename)
                    if (clips.Remove(from, out var moved)) clips[to] = moved; // 이름과 다른 소리가 든 파일은 맞는 종류로 옮기고, 빈 자리는 빌려 쓴다
            // 자기 소리가 없는 종류는 비슷한 캐릭터 것을 빌려 쓴다.
            if (Borrowed.TryGetValue(folder, out var borrow))
                foreach (var (kind, donor) in borrow)
                    if (!clips.ContainsKey(kind)) LoadFolder(donor, kind);
            foreach (var list in clips.Values) list.Sort();
        }

        /// <summary>
        /// 빌려 쓰는 소리: 캐릭터 폴더 → (종류, 빌려 올 캐릭터 폴더). 자기 폴더에 그 종류가 생기면 자기 것을 쓴다.
        /// 발소리 — 감시자는 체격이 비슷한 감시자, 생존자는 생존자. 판자 내리기 — 생존자는 의사·용병. 평타 — 무기를 휘두르는 감시자끼리.
        /// </summary>
        private static readonly Dictionary<string, (string Kind, string Donor)[]> Borrowed = new()
        {
            ["Doctor"] = new[] { ("Footstep", "Coordinator") },
            ["Coordinator"] = new[] { ("PalletDrop", "Doctor") },
            ["Cowboy"] = new[] { ("PalletDrop", "Mercenary") },
            ["Forward"] = new[] { ("PalletDrop", "Mercenary") },
            ["MindsEye"] = new[] { ("PalletDrop", "Doctor") },
            ["Thief"] = new[] { ("Footstep", "Forward"), ("Vault", "Forward"), ("PalletDrop", "Mercenary") },
            ["Antonio"] = new[] { ("Footstep", "Ripper") },
            ["Gamekeeper"] = new[] { ("Attack", "Leo") },          // 닻 ↔ 망치: 무겁게 휘두르는 무기
            ["Joker"] = new[] { ("Footstep", "Leo") },             // 덩치 큰 감시자
            ["Michiko"] = new[] { ("Footstep", "Ripper") },
            ["Hastur"] = new[] { ("Footstep", "Luchino"), ("Vault", "Luchino") }, // 촉수 다리 = 괴물 발소리
        };

        /// <summary>자기 폴더의 종류를 다른 종류로 옮겨 쓴다(파일 이름과 실제 소리가 다름). 빠진 자리는 Borrowed로 채운다.</summary>
        private static readonly Dictionary<string, (string From, string To)[]> Renamed = new()
        {
            // (없음) — 예: ["Folder"] = new[] { ("Footstep", "ChargeStep") }
        };

        private void LoadFolder(string from, string onlyKind)
        {
            foreach (var clip in Resources.LoadAll<AudioClip>($"Sfx/Characters/{from}"))
            {
                string kind = KindOf(from, clip.name);
                if (kind == null || (onlyKind != null && kind != onlyKind)) continue;
                if (!clips.TryGetValue(kind, out var list)) clips[kind] = list = new List<string>();
                list.Add($"Characters/{from}/{clip.name}");
            }
        }

        /// <summary>이 종류 소리가 있는지(CharacterSfx가 일반 소리를 대신할지 정한다).</summary>
        public bool Has(string kind) => clips.ContainsKey(kind);

        /// <summary>"Joker_Vault_01_WithTail" → "Vault_WithTail", "Cowboy_LassoSpin_Loop" → "LassoSpin_Loop".</summary>
        private static string KindOf(string folder, string clipName)
        {
            if (!clipName.StartsWith(folder + "_")) return null;
            var parts = clipName.Substring(folder.Length + 1).Split('_');
            var kept = new List<string>();
            foreach (var p in parts) if (!int.TryParse(p, out _)) kept.Add(p);
            return kept.Count > 0 ? string.Join("_", kept) : null;
        }

        private void OnEnable()
        {
            All.Add(this);
            HitEvents.HitOccurred += OnHit;
            WallBounceEvents.Struck += OnWallStruck;
            ProjectileEvents.Ended += OnProjectileEnded;
            ProjectileEvents.Launched += OnProjectileLaunched;
            CloneEvents.Spawned += OnCloneSpawned;
            ChargeSkill.Rammed += OnRammed;
        }

        /// <summary>럭비공 돌진이 상대를 박음: 따로 소리 없음(예전엔 용병 아대 소리를 빌려 냈으나 어울리지 않아 뺌).</summary>
        private void OnRammed(GameObject owner, Vector3 point) { }

        private void OnDisable()
        {
            All.Remove(this);
            HitEvents.HitOccurred -= OnHit;
            WallBounceEvents.Struck -= OnWallStruck;
            ProjectileEvents.Ended -= OnProjectileEnded;
            ProjectileEvents.Launched -= OnProjectileLaunched;
            CloneEvents.Spawned -= OnCloneSpawned;
            ChargeSkill.Rammed -= OnRammed;
            StopLoop();
        }

        private void Start()
        {
            identity = GetComponent<CharacterIdentity>();
            if (TryGetComponent(out attack)) attack.AttackStarted += OnAttack;
            if (TryGetComponent(out motor)) { motor.Jumped += OnJump; motor.Landed += OnLand; }
            lastPos = transform.position;
        }

        private void OnDestroy()
        {
            if (attack != null) attack.AttackStarted -= OnAttack;
            if (motor != null) { motor.Jumped -= OnJump; motor.Landed -= OnLand; }
            if (loopSource != null) Destroy(loopSource);
        }

        private void Update()
        {
            if (folder == null || clips.Count == 0) return;
            // 캐릭터를 바꿀 때 붙고 빠지는 컴포넌트는 매번 다시 확인한다.
            if (skill == null) TryGetComponent(out skill);
            if (traversal == null) traversal = GetComponent<ICharacterTraversalState>();
            if (pallet == null && (pallet = GetComponent<IPalletUser>()) != null) palletCount = pallet.DropCount;
            if (doll == null && TryGetComponent(out doll)) { swapCount = doll.SwapCount; throwCount = doll.ThrowCount; }
            if (butterflies == null && TryGetComponent(out butterflies)) butterflyCount = butterflies.PlacedCount;
            if (notes == null && TryGetComponent(out notes)) lastNotePlace = notes.LastPlaceTime;
            if (tentacleField == null) TryGetComponent(out tentacleField);

            UpdateFootsteps(); // 발소리: 모든 캐릭터가 같은 VARCO 나무 바닥 발소리(footstep_01~03)를 작게
            UpdateTraversal();
            UpdateSkill();
            UpdateFields();
            UpdateTentacles();
            if (loopSource != null && loopSource.isPlaying) loopSource.volume = SkillVolume * AudioVolume.Sfx;
        }

        // ── 발소리: 땅 위에서 움직인 거리가 보폭을 넘을 때마다 ──
        private void UpdateFootsteps()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos; delta.y = 0f;
            lastPos = pos;
            // 캐릭터 녹화 발소리(Footstep)는 다른 소리(뛰는 소리 등)로 들려 쓰지 않는다 — 모든 캐릭터가 VARCO 나무 바닥 발소리를 작게.
            // 돌진(포워드·광대)·도약 중에는 돌진 소리가 따로 나므로 발소리 없음.
            if (Time.deltaTime <= 0f) return;
            bool dashing = skill != null && skill.State == SkillState.Casting && skill.SkillId is "Forward_Charge" or "SmileyFace_RocketCharge" or "Mercenary_Guard";
            bool grounded = motor == null || motor.IsGrounded;
            bool busy = traversal != null && traversal.Action != TraversalAction.None;
            float speed = delta.magnitude / Time.deltaTime;
            if (!grounded || busy || speed < 0.8f || speed > 12f || (dashing && speed > 7.5f)) { stepDistance = 0f; return; } // 12 넘음 = 돌진·순간이동·도약 등
            stepDistance += delta.magnitude;
            float stride = Mathf.Clamp(speed * 0.36f, 1.1f, 2.6f); // 빠를수록 보폭이 길지만 발소리 간격은 짧아진다
            if (stepDistance < stride) return;
            stepDistance = 0f;
            SfxPlayer.Play(FootstepClips[Random.Range(0, FootstepClips.Length)], transform.position, FootVolume * Mathf.Lerp(0.7f, 1f, speed / 7f), 0.08f);
        }

        // ── 창틀 넘기 ──
        private void UpdateTraversal()
        {
            var action = traversal != null ? traversal.Action : TraversalAction.None;
            if (action != lastAction && (action == TraversalAction.WindowVault || action == TraversalAction.WindowJump || action == TraversalAction.WindowDrop))
            {
                // 길게 울리는 꼬리가 붙은 소리(_WithTail)는 내려가는 넘기(WindowDrop)에
                string kind = action == TraversalAction.WindowDrop && Has("Vault_WithTail") ? "Vault_WithTail" : "Vault";
                Play(kind, Body, VaultVolume, 0.04f);
            }
            lastAction = action;
        }

        // ── 스킬(E) 단계 ──
        private void UpdateSkill()
        {
            if (skill == null) { StopLoop(); return; }
            string id = skill.SkillId;
            bool casting = skill.State == SkillState.Casting;
            int phase = casting ? skill.Phase : -1;
            string state = casting ? id : null;
            bool started = state != null && state != lastSkillState;
            bool phaseChanged = phase != lastPhase || started;
            lastSkillState = state; lastPhase = phase;

            switch (id)
            {
                case "Luchino_Leap":
                    // 떨어지는 커맨드(좌클릭 = 내려찍기 Slamming, E = 빨리 떨어지기 Dropping)를 누른 그 프레임에 '헛하' 기합 — 공중에서 바로 시작한 경우도(started)
                    if (phaseChanged && (phase == LeapSlamming || phase == LeapDropping)) Play("DropGrunt", Body, 1f, 0.03f);
                    break;
                case "Gamekeeper_Grab":
                    // 그랩 소리 세 가지(노루.mp4 녹화) — 셋 다 같은 '뻗는' 소리로 시작하므로, 뻗는 동안 Hook_Extend를 내다가
                    // 물면 같은 재생 위치에서 그 결과의 음원으로 바꿔 이어 간다(끊김 없이).
                    // - 빗나감(Retracting·취소): 뻗는 소리만 — 그 자리에서 짧게 줄여 끈다.
                    // - 벽을 물고 끌려감(Towing): Hook_Wall — 뻗음 + 이동 소리, 뒤는 조용히 끝남.
                    // - 상대를 물어 끌어옴(Dragging): Hook_Catch — 뻗음 + '뻐억' + 끌려오는 소리 + 신음.
                    if (phaseChanged && phase == GrabExtending) { PlayStoppable("Hook_Extend", SkillVolume, 0.03f); hookLatched = false; }
                    else if (phaseChanged && phase == GrabTowing) { hookLatched = true; SwitchStoppable("Hook_Wall", SkillVolume); }
                    else if (phaseChanged && phase == GrabDragging) { hookLatched = true; SwitchStoppable("Hook_Catch", SkillVolume); }
                    else if (phaseChanged && !hookLatched && (phase == GrabRetracting || !casting)) FadeStoppable(); // 빗나감 = 뻗는 소리만
                    break;
                case "Cowboy_Lasso":
                    StopLoop(); // 조준 중 반복 소리(올가미 돌리기)는 끔
                    if (phaseChanged && phase == GrabExtending) Play("Lasso", Body, SkillVolume, 0.05f);
                    break;
                case "Coordinator_Flare":
                    if (phaseChanged && phase == FlareFired) Play("FlareGun", Body + transform.forward * 0.5f, SkillVolume, 0.04f);
                    break;
                case "Forward_Charge":
                    // 럭비공 돌진: 달리는 동안 바람 가르며 뛰는 소리(Forward_ChargeRun_Loop)를 반복, 끝나면 멈춤. 박치기 소리는 OnRammed.
                    if (casting) StartLoop("ChargeRun_Loop", "ChargeRun_Loop"); else if (loopKind == "ChargeRun_Loop") StopLoop();
                    break;
                case "SmileyFace_RocketCharge":
                    // 로켓 돌진(녹화에서 잘라 낸 Joker_Dash): 돌진하는 동안만 점화 → 분사 소리가 끊기지 않게 이어지고(시간 제한 없음), 끝나면 멈춤. 들고 다니는 동안은 조용.
                    if (casting && phase == RocketCharging) RocketThrust();
                    else if (loopKind == RocketThrustKind) StopLoop();
                    break;
                case "Mercenary_Guard":
                    if (phaseChanged && phase == WallDashing) Play("ElbowPad", Body, SkillVolume, 0.04f);
                    break;
                case "MindsEye_Thrust":
                case "Geisha_Butterfly":
                    if (started) Play(id == "MindsEye_Thrust" ? "Cane" : "Leap", Body, SkillVolume, 0.04f);
                    break;
                default:
                    StopLoop();
                    break;
            }
        }

        // ── 스킬 말고 따로 붙는 능력(판자·인형·나비·음표) ──
        private void UpdateFields()
        {
            if (pallet != null && pallet.DropCount != palletCount) { palletCount = pallet.DropCount; Play("PalletDrop", Body, VaultVolume, 0.03f); }
            if (doll != null && doll.SwapCount != swapCount) { swapCount = doll.SwapCount; Play("Swap", Body, SkillVolume, 0.04f); } // 인형과 자리 바꿈(공장장 녹화 38.0·43.2초 검은 줄 순간이동 소리)
            if (doll != null && doll.ThrowCount != throwCount) { throwCount = doll.ThrowCount; Play("DollThrow", Body, SkillVolume, 0.04f); } // 떼서 던진 순간 — 풍선에 공기 들어가는 듯한 쉬이익(공장장 녹화 33.3·49.8초), 겨누는 동안은 소리 없음
            if (butterflies != null && butterflies.PlacedCount != butterflyCount) { butterflyCount = butterflies.PlacedCount; Play("ButterflyThrow", Body, SkillVolume, 0.05f); }
            if (notes != null)
            {
                if (notes.Placing && !notePlacing) Play("String_Start", Body, SkillVolume, 0.03f); // 좌클릭 꾹 = 활을 긋기 시작
                if (notes.LastPlaceTime != lastNotePlace)
                {
                    if (!notePlacing) Play("String_Aim", Body, SkillVolume, 0.03f); // E로 바로 앞에 놓으면 짧게 긋는 소리를 먼저
                    Play("String_Release", Body, SkillVolume, 0.03f);
                    lastNotePlace = notes.LastPlaceTime;
                }
                notePlacing = notes.Placing;
            }
        }

        // ── 노란옷의 왕 촉수: 솟아오를 때 Tentacle_Rise, 내리칠 때 Tentacle_Slam(그 촉수 자리에서) ──
        private void UpdateTentacles()
        {
            if (tentacleField == null) { tentacleStates.Clear(); return; }
            var list = tentacleField.Tentacles;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                bool known = tentacleStates.TryGetValue(t.Id, out var before);
                if (!known || before != t.State)
                {
                    Vector3 at = t.Position + Vector3.up * 1.5f;
                    if (t.State == TentacleState.Rising) Play("Tentacle_Rise", at, SkillVolume, 0.05f);
                    else if (t.State == TentacleState.SlamActive) Play("Tentacle_Slam", at, 1f, 0.04f);
                }
                tentacleStates[t.Id] = t.State;
            }
            if (tentacleStates.Count > 32) // 사라진 촉수 정리
            {
                var alive = new HashSet<int>();
                for (int i = 0; i < list.Count; i++) alive.Add(list[i].Id);
                var gone = new List<int>();
                foreach (var id in tentacleStates.Keys) if (!alive.Contains(id)) gone.Add(id);
                foreach (var id in gone) tentacleStates.Remove(id);
            }
        }

        private void OnAttack(CharacterAttackState s)
        {
            // 휘두를 때 기합 목소리(있는 캐릭터만 — 의사: 창틀 넘기 녹화 앞의 짧은 여자 목소리 '핫', Doctor_AttackVoice)
            if (s != CharacterAttackState.None) Play("AttackVoice", Body, 0.7f, 0.04f);
            // 휘두를 때는 일반 휘두르기 소리(swing, CharacterSfx)만 — 캐릭터 전용 Attack 파일에는 맞는 소리가 섞여 있어 허공에서도 타격음처럼 들렸다. 타격음은 벽·캐릭터에 맞을 때만(SfxPlayer).
        }

        /// <summary>평타 휘두르기 소리를 이 캐릭터가 따로 갖는지(CharacterSfx가 일반 swing을 뺀다).</summary>
        public bool OverridesSwing => false; // 모든 캐릭터가 일반 휘두르기 소리(swing)를 쓴다

        private void OnJump() => Play("Jump", transform.position, 0.6f, 0.05f);

        private void OnLand()
        {
            if (Time.time - lastLand < 0.25f) return;
            lastLand = Time.time;
            bool leap = skill != null && skill.SkillId == "Luchino_Leap" && skill.State == SkillState.Casting;
            Play(leap && Has("Land_Ice") ? "Land_Ice" : "Land", transform.position, leap ? 1f : 0.6f, 0.05f);
        }

        private void OnHit(HitResult hit)
        {
            if (identity == null || hit.AttackerId != identity.Id || hit.Kind == AttackKind.Skill) return;
            // 캐릭터 전용 평타 맞힘 소리는 내지 않음 — 피격음은 SfxPlayer.CharacterHitSound(Antonio_Attack_Hit_01) 하나로 고정, "뽀-익"은 벽 치기만
        }

        private void OnWallStruck(GameObject attacker, Vector3 point, Vector3 normal)
        {
            // 캐릭터 전용 벽 치기 소리는 내지 않음 — 벽 치기는 소리 없음(파란 리본만)
        }

        private void OnProjectileEnded(IProjectile p)
        {
            if (identity == null || p.OwnerId != identity.Id || p.Kind != "Flare" || !p.EndedOnCharacter) return;
            Play("FlareGun_Hit", p.EndPoint, SkillVolume, 0.03f);
        }

        /// <summary>리퍼 안개 칼날을 날림(평타 2칸·E 1칸 모두): 허공에 휘두른 뒤 낮고 스산하게 날아가는 스아아악(리퍼 녹화 5.5·22.4·26.7초의 꼬리).</summary>
        private void OnProjectileLaunched(IProjectile p)
        {
            if (identity == null || p.OwnerId != identity.Id || p.Kind != MistCharge.BladeKind) return;
            Play("MistBlade", p.Position, SkillVolume, 0.05f);
        }

        private void OnCloneSpawned(GameObject clone, CharacterIdentity summoner)
        {
            if (summoner != null && summoner.gameObject == gameObject) Play("Shadow", clone.transform.position + Vector3.up, SkillVolume, 0.04f);
        }

        private void Play(string kind, Vector3 pos, float volume, float pitchJitter, float pitch = 1f)
        {
            string clip = Pick(kind);
            if (clip != null) SfxPlayer.Play(clip, pos, volume, pitchJitter, pitch);
        }

        /// <summary>그 종류에서 하나 — 같은 소리가 연달아 나오지 않게.</summary>
        private string Pick(string kind)
        {
            if (kind == null || !clips.TryGetValue(kind, out var list) || list.Count == 0) return null;
            int i = Random.Range(0, list.Count);
            if (list.Count > 1 && lastPick.TryGetValue(kind, out int last) && i == last) i = (i + 1) % list.Count;
            lastPick[kind] = i;
            return list[i];
        }

        /// <summary>계속 나는 소리: 처음 한 번은 kind(여러 개 중 하나)를, 끝나면 loopKind를 반복. 이미 같은 소리가 나는 중이면 그대로.</summary>
        private void EnsureLoopSource()
        {
            if (loopSource != null) return;
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 0.85f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = 3f; loopSource.maxDistance = 40f;
            loopSource.dopplerLevel = 0f;
        }

        private void StartLoop(string kind, string loopKindName)
        {
            EnsureLoopSource();
            if (loopKind == kind)
            {
                if (!loopSource.isPlaying) PlayLoopClip(loopKindName, true); // 첫 소리가 끝났으면 반복 소리로
                return;
            }
            loopKind = kind;
            if (!PlayLoopClip(kind, false)) PlayLoopClip(loopKindName, true);
        }

        // ── 광대 로켓 분사: 가장 긴 Dash 녹화(Joker_Dash_01, 11.5초) — 앞 0.35초 무음을 건너 점화부터, 끝의 부딪힘 소리(마지막 0.7초) 전에 2초 자리로 돌아가 분사만 이어 간다 ──
        private const string RocketThrustKind = "RocketThrust";
        private const float ThrustStart = 0.35f, ThrustLoopFrom = 2f, ThrustTailCut = 0.7f;

        private void RocketThrust()
        {
            if (loopKind != RocketThrustKind || loopSource == null || loopSource.clip == null)
            {
                if (!clips.TryGetValue("Dash", out var list) || list.Count == 0) return;
                AudioClip longest = null;
                foreach (var path in list)
                {
                    var c = Resources.Load<AudioClip>($"Sfx/{path}");
                    if (c != null && (longest == null || c.length > longest.length)) longest = c;
                }
                if (longest == null) return;
                EnsureLoopSource();
                loopKind = RocketThrustKind;
                loopSource.clip = longest;
                loopSource.loop = false;
                loopSource.pitch = 1f;
                loopSource.volume = SkillVolume * AudioVolume.Sfx;
                loopSource.Play();
                loopSource.time = Mathf.Min(ThrustStart, longest.length * 0.1f);
                return;
            }
            var clip = loopSource.clip;
            float loopFrom = Mathf.Min(ThrustLoopFrom, clip.length * 0.3f);
            if (!loopSource.isPlaying) { loopSource.Play(); loopSource.time = loopFrom; }
            else if (loopSource.time >= clip.length - ThrustTailCut) loopSource.time = loopFrom; // 끝의 부딪힘 소리 전에 분사 자리로
        }

        private bool PlayLoopClip(string kind, bool loop)
        {
            string path = Pick(kind);
            var clip = path != null ? Resources.Load<AudioClip>($"Sfx/{path}") : null;
            if (clip == null) return false;
            loopSource.clip = clip;
            loopSource.loop = loop;
            loopSource.volume = SkillVolume * AudioVolume.Sfx;
            loopSource.Play();
            return true;
        }

        // ── 도중에 끌 수 있는 한 번 소리(사냥터지기 그랩: 못 물면 뒷부분을 끈다) ──
        private AudioSource stoppableSource;
        private float stoppableFade = -1f;
        private bool hookLatched; // 사냥터지기 그랩이 벽·상대를 물었는지

        private void PlayStoppable(string kind, float volume, float pitchJitter)
        {
            string path = Pick(kind);
            var clip = path != null ? Resources.Load<AudioClip>($"Sfx/{path}") : null;
            if (clip == null) return;
            if (stoppableSource == null)
            {
                stoppableSource = gameObject.AddComponent<AudioSource>();
                stoppableSource.playOnAwake = false;
                stoppableSource.spatialBlend = 0.85f;
                stoppableSource.rolloffMode = AudioRolloffMode.Linear;
                stoppableSource.minDistance = 3f; stoppableSource.maxDistance = 40f;
                stoppableSource.dopplerLevel = 0f;
            }
            stoppableFade = -1f;
            stoppableSource.clip = clip;
            stoppableSource.loop = false;
            stoppableSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            stoppableSource.volume = volume * AudioVolume.Sfx;
            stoppableSource.Play();
        }

        /// <summary>
        /// 지금 나는 끌 수 있는 소리를 다른 음원(kind)으로 바꾸되, 같은 재생 위치부터 이어 간다(같은 시작을 가진 변형 음원끼리 — 사냥터지기 그랩).
        /// 아무것도 안 나고 있으면 처음부터 낸다.
        /// </summary>
        private void SwitchStoppable(string kind, float volume)
        {
            string path = Pick(kind);
            var clip = path != null ? Resources.Load<AudioClip>($"Sfx/{path}") : null;
            if (clip == null) return;
            float at = stoppableSource != null && stoppableSource.isPlaying ? stoppableSource.time : 0f;
            float pitch = stoppableSource != null ? stoppableSource.pitch : 1f;
            PlayStoppable(kind, volume, 0f);
            if (stoppableSource == null) return;
            stoppableSource.clip = clip; // PlayStoppable이 고른 것과 같게(같은 묶음 중 하나)
            stoppableSource.pitch = pitch;
            stoppableSource.Play();
            stoppableSource.time = Mathf.Min(at, Mathf.Max(0f, clip.length - 0.05f)); // 재생을 시작한 뒤 위치를 옮긴다
        }

        /// <summary>도중에 끌 소리를 0.08초 동안 줄여 끈다.</summary>
        private void FadeStoppable()
        {
            if (stoppableSource != null && stoppableSource.isPlaying && stoppableFade < 0f) stoppableFade = 0.08f;
        }

        private void LateUpdate()
        {
            if (stoppableSource == null || stoppableFade < 0f) return;
            stoppableFade -= Time.deltaTime;
            if (stoppableFade <= 0f) { stoppableSource.Stop(); stoppableFade = -1f; return; }
            stoppableSource.volume *= Mathf.Clamp01(stoppableFade / 0.08f + 0.2f);
        }

        private void StopLoop()
        {
            loopKind = null;
            if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
        }
    }
}

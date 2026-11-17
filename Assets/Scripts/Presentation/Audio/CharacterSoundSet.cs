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
    /// - 스킬: 사냥터지기 Hook/Hook_Hit · 카우보이 LassoSpin(조준 중 반복)/Lasso · 공군 FlareGun/FlareGun_Hit · 광대 Dash/Dash_Loop
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
        private const int GrabWindup = 0, GrabExtending = 1, GrabDragging = 4, GrabTowing = 5; // GrabShotSkill
        private const int FlareFired = 1;                                                      // FlareShotSkill
        private const int RocketCharging = 1;                                                  // RocketChargeSkill
        private const int WallDashing = 2;                                                     // WallDashSkill

        private const float FootVolume = 0.4f, VaultVolume = 0.8f, AttackVolume = 0.75f, SkillVolume = 0.85f;

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

        private TraversalAction lastAction;
        private string lastSkillState;
        private int lastPhase = -1, palletCount, swapCount, butterflyCount;
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
            foreach (var clip in Resources.LoadAll<AudioClip>($"Sfx/Characters/{folder}"))
            {
                string kind = KindOf(clip.name);
                if (kind == null) continue;
                if (!clips.TryGetValue(kind, out var list)) clips[kind] = list = new List<string>();
                list.Add($"Characters/{folder}/{clip.name}");
            }
            foreach (var list in clips.Values) list.Sort();
        }

        /// <summary>이 종류 소리가 있는지(CharacterSfx가 일반 소리를 대신할지 정한다).</summary>
        public bool Has(string kind) => clips.ContainsKey(kind);

        /// <summary>"Joker_Vault_01_WithTail" → "Vault_WithTail", "Cowboy_LassoSpin_Loop" → "LassoSpin_Loop".</summary>
        private string KindOf(string clipName)
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
            CloneEvents.Spawned += OnCloneSpawned;
        }

        private void OnDisable()
        {
            All.Remove(this);
            HitEvents.HitOccurred -= OnHit;
            WallBounceEvents.Struck -= OnWallStruck;
            ProjectileEvents.Ended -= OnProjectileEnded;
            CloneEvents.Spawned -= OnCloneSpawned;
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
            if (doll == null && TryGetComponent(out doll)) swapCount = doll.SwapCount;
            if (butterflies == null && TryGetComponent(out butterflies)) butterflyCount = butterflies.PlacedCount;
            if (notes == null && TryGetComponent(out notes)) lastNotePlace = notes.LastPlaceTime;

            UpdateFootsteps();
            UpdateTraversal();
            UpdateSkill();
            UpdateFields();
            if (loopSource != null && loopSource.isPlaying) loopSource.volume = SkillVolume * AudioVolume.Sfx;
        }

        // ── 발소리: 땅 위에서 움직인 거리가 보폭을 넘을 때마다 ──
        private void UpdateFootsteps()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - lastPos; delta.y = 0f;
            lastPos = pos;
            if (!Has("Footstep") || Time.deltaTime <= 0f) return;
            bool grounded = motor == null || motor.IsGrounded;
            bool busy = traversal != null && traversal.Action != TraversalAction.None;
            float speed = delta.magnitude / Time.deltaTime;
            if (!grounded || busy || speed < 0.8f || speed > 14f) { stepDistance = 0f; return; }
            stepDistance += delta.magnitude;
            float stride = Mathf.Clamp(speed * 0.36f, 1.1f, 2.6f); // 빠를수록 보폭이 길지만 발소리 간격은 짧아진다
            if (stepDistance < stride) return;
            stepDistance = 0f;
            Play("Footstep", transform.position, FootVolume * Mathf.Lerp(0.7f, 1f, speed / 7f), 0.08f);
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
                case "Gamekeeper_Grab":
                    if (phaseChanged && phase == GrabExtending) Play("Hook", Body, SkillVolume, 0.03f);
                    if (phaseChanged && (phase == GrabDragging || phase == GrabTowing)) Play("Hook_Hit", Body, SkillVolume, 0.03f);
                    break;
                case "Cowboy_Lasso":
                    if (phase == GrabWindup) StartLoop("LassoSpin", "LassoSpin_Loop"); else StopLoop();
                    if (phaseChanged && phase == GrabExtending) Play("Lasso", Body, SkillVolume, 0.05f);
                    break;
                case "Coordinator_Flare":
                    if (phaseChanged && phase == FlareFired) Play("FlareGun", Body + transform.forward * 0.5f, SkillVolume, 0.04f);
                    break;
                case "SmileyFace_RocketCharge":
                    if (phase == RocketCharging) StartLoop("Dash", "Dash_Loop"); else StopLoop();
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
            if (doll != null && doll.SwapCount != swapCount) { swapCount = doll.SwapCount; Play("Shadow", Body, SkillVolume, 0.04f); }
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

        private void OnAttack(CharacterAttackState s)
        {
            if (s == CharacterAttackState.HeavyCharge || s == CharacterAttackState.None) return;
            string kind = Has("Attack") ? "Attack" : CharacterId == "Survivor_MindsEye" ? "Cane" : null; // 맹인: 지팡이 휘두르기
            if (kind != null) Play(kind, Body, AttackVolume, 0.05f, s == CharacterAttackState.Heavy ? 0.93f : 1f);
        }

        /// <summary>평타 휘두르기 소리를 이 캐릭터가 따로 갖는지(CharacterSfx가 일반 swing을 뺀다).</summary>
        public bool OverridesSwing => Has("Attack") || (CharacterId == "Survivor_MindsEye" && Has("Cane"));

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
            Play("Attack_Hit", hit.Point, 0.8f, 0.05f);
        }

        private void OnWallStruck(GameObject attacker, Vector3 point, Vector3 normal)
        {
            if (attacker == gameObject) Play("Attack_Wall", point, 0.85f, 0.04f);
        }

        private void OnProjectileEnded(IProjectile p)
        {
            if (identity == null || p.OwnerId != identity.Id || p.Kind != "Flare" || !p.EndedOnCharacter) return;
            Play("FlareGun_Hit", p.EndPoint, SkillVolume, 0.03f);
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
        private void StartLoop(string kind, string loopKindName)
        {
            if (loopSource == null)
            {
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.spatialBlend = 0.85f;
                loopSource.rolloffMode = AudioRolloffMode.Linear;
                loopSource.minDistance = 3f; loopSource.maxDistance = 40f;
                loopSource.dopplerLevel = 0f;
            }
            if (loopKind == kind)
            {
                if (!loopSource.isPlaying) PlayLoopClip(loopKindName, true); // 첫 소리가 끝났으면 반복 소리로
                return;
            }
            loopKind = kind;
            if (!PlayLoopClip(kind, false)) PlayLoopClip(loopKindName, true);
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

        private void StopLoop()
        {
            loopKind = null;
            if (loopSource != null && loopSource.isPlaying) loopSource.Stop();
        }
    }
}

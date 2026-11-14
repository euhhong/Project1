using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 모션 파일로 캐릭터 Animator Controller를 다시 만든다. 모션을 코드로 생성하지 않는다.
    /// 자리마다 `_Motions/Mixamo/<자리>.fbx`가 있으면 그것을 쓰고, 아직 없으면 예전에 만든 임시 모션
    /// `_Motions/Procedural/<자리>.anim`을 그대로 쓴다(더 만들거나 고치지 않는다).
    /// Mixamo 폴더에 FBX를 넣으면 자동으로 다시 만든다. 메뉴: Tools > FE > 모션 컨트롤러 다시 만들기.
    /// </summary>
    public static class CharacterMotionControllerBuilder
    {
        public const string ControllerPath = "Assets/Models/IdentityV/_Motions/CharacterLocomotion.controller";
        public const string LegacyFolder = "Assets/Models/IdentityV/_Motions/Procedural";
        public const string MaskPath = "Assets/Models/IdentityV/_Motions/UpperBodyMask.mask";
        public const string AttackLayer = "Attack";
        public const string AirAttackLayer = "AirAttack";
        private const float BorrowedWalkSpeed = 1.7f;
        // 게임 점프의 공중 시간(초). CharacterMotor 기본값 jumpHeight 1.2, gravity -20 → 2·√(2·1.2·20)/20 ≈ 0.69초.
        // 점프 모션(도약~착지)을 이 시간에 맞춰 재생한다.
        private const float JumpAirTime = 0.69f;
        // 점프할 때 MoveZ(걷기 = 1, 달리기 = RunBlendValue)가 이 값보다 크면 걸어서 점프(Air_F) / 뛰면서 점프(Air_Run)를 쓴다.
        private const float ForwardJumpMoveZ = 0.5f;
        private const float RunJumpMoveZ = (1f + CharacterAnimatorDriver.RunBlendValue) * 0.5f;

        // 스킬 모션의 재생 배속. 전위 돌진은 이동 속도를 2배(16m/s)로 올린 만큼 달리기 모션도 2배 빠르게.
        // 광대 로켓 돌진: 다리는 달리기 모션(6.5m/s용) — 돌진 8m/s에 맞춰 1.2배.
        private static readonly Dictionary<string, float> SkillLoopSpeed = new() { ["Forward_Charge"] = 2f, ["SmileyFace_RocketCharge"] = 1.2f }; // 사냥터지기 그랩은 재생 위치를 BE 구간에 맞춘다(CharacterSkillAnimator.MotionTiming)

        // 스킬이 도중에 막혀 끝날 때(넘어짐) 쓰는 모션. 전위 돌진이 벽에 부딪힘 → Fall_1(Rifle Hit To Back).
        private static readonly (string skillId, string slot)[] SkillEndMotions = { ("Forward_Charge", "Fall_1"), ("SmileyFace_RocketCharge", "Fall_1") };

        // 상체만 쓰는 스킬 모션(SkillUpper 레이어, 다리는 걷기·달리기 그대로): 광대 로켓 치켜들고 걷기, 용병 벽 짚기(다리는 계속 달림).
        private static readonly HashSet<string> UpperBodySkillClips = new() { "Skill_SmileyFace_RocketCharge_Raise", "Skill_Mercenary_Guard_Wall", "Skill_Thief_Flashlight" /* 도둑 손전등: 오른팔을 앞으로 뻗어 비춤(Tools/Blender/make_flashlight_hold.py), 다리는 걷기 */ };

        // 스킬 상태에 다른 자리 모션을 쓴다(스킬 ID, 자리, 배속) — Mixamo/Skill_<스킬 ID>.fbx가 없을 때만.
        // 사냥터지기 그랩 쏘기는 이제 전용 파일 Skill_Gamekeeper_Grab.fbx(감시자 내려치기 + 머리는 앞을 봄, Blender로 수정)를 쓴다.
        private static readonly (string skillId, string slot, float speed)[] SkillClipAliases = { ("Gamekeeper_Grab", "Attack_Light_Hunter", 1.6f), ("Ripper_MistBlade", "Attack_Light_Ripper", 1f) /* 리퍼 E: 위에서 아래로 내려치기(기본 공격과 같은 왼손 내려치기) */, ("MindsEye_Thrust", "Attack_Light_Geisha", 1f) /* 맹인 E: 오른팔로 앞으로 길게 찌르기(붉은나비 찌르기와 같은 모션) */ };

        private static readonly string[] Slots =
        {
            "Idle", "Idle_Hunter", "Idle_Thief", "Idle_Luchino", "Idle_HellEmber", "Idle_Antonio", "Move_Hastur", "Air", "Air_F", "Air_Run", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
            "Attack_Light", "Attack_HeavyCharge", "Attack_HeavyCharge_Hunter", "Attack_Heavy", "Attack_HeavyKick", "Attack_AirStomp", "Attack_AirCharge", "Down",
            "Attack_RunJump_Survivor", "Attack_RunJump2_Survivor", "Attack_RunJump_Hunter", "Attack_Light_Hunter",
            "Attack_Light_Luchino", "Attack_Light_Geisha", "Attack_Light_Hastur", "Attack_Light_HasturSlam", "Attack_Light_MindsEye", "Attack_Light_HookPunch", "Attack_SwipeL", "Attack_SwipeR",
            "Attack_Light_Joker", "Attack_Heavy_Joker", "Carry_Joker", "Attack_Light_Ripper", "Attack_Heavy_Ripper", "Attack_RunJump_Ripper",
            "Hit_Head", "Hit_Head2", "Hit_Head_Light", "Hit_Head_Medium", "Hit_Head_Big", "Hit_EdgeSlip", "Stairs_Up", "Hit_Body_Front", "Hit_Body_Side", "Hit_Body_Back", "Hit_Limb", "Hit_Small", "Stun", "Stun_Back", "Fall_1", "Hit_HeadKick", "GetUp",
        };

        // Override Controller: 생존자 Controller(기본)에서 표의 자리만 다른 모션으로 바꾼다. 뒤에 적은 것이 이긴다.
        // 파일: _Motions/<이름>Locomotion.overrideController
        public const string MotionsFolder = "Assets/Models/IdentityV/_Motions";
        public const string HunterControllerPath = MotionsFolder + "/HunterLocomotion.overrideController";
        private static readonly (string from, string to)[] HunterOverrides =
        {
            ("Attack_RunJump_Survivor", "Attack_RunJump_Hunter"),
            ("Attack_Light", "Attack_Light_Hunter"),
            ("Idle", "Idle_Hunter"), // 생존자 대기(Idle (1))는 생존자만. 감시자는 감시자 대기
            ("Attack_HeavyCharge", "Attack_HeavyCharge_Hunter"), // 생존자 강공격 모으기(Bouncing Fight Idle 격투 자세)는 생존자만. 감시자는 예전 모으기
        };
        // 유령처럼 이동: 걷기·달리기에서도 다리를 움직이지 않고 대기 자세 그대로 미끄러진다.
        // (옆·뒤 달리기는 같은 방향 걷기 클립을 빌려 쓰므로 걷기만 바꾸면 같이 바뀐다.)
        private static readonly (string from, string to)[] GhostGlide =
        {
            ("Walk_F", "Idle_Hunter"), ("Walk_B", "Idle_Hunter"), ("Walk_L", "Idle_Hunter"), ("Walk_R", "Idle_Hunter"),
            ("Run_F", "Idle_Hunter"), ("Run_B", "Idle_Hunter"), ("Run_L", "Idle_Hunter"), ("Run_R", "Idle_Hunter"),
            ("Stairs_Up", "Idle_Hunter"), // 계단도 미끄러져 오른다
        };
        // 하스터: 발이 없어 걷기·달리기 모두 꾸물대며 미끄러지는 모션 하나로
        private static readonly (string from, string to)[] HasturCrawl =
        {
            ("Walk_F", "Move_Hastur"), ("Walk_B", "Move_Hastur"), ("Walk_L", "Move_Hastur"), ("Walk_R", "Move_Hastur"),
            ("Run_F", "Move_Hastur"), ("Run_B", "Move_Hastur"), ("Run_L", "Move_Hastur"), ("Run_R", "Move_Hastur"),
            ("Stairs_Up", "Move_Hastur"),
        };
        // 하스터 공격: 하스터 모델은 Humanoid가 아닌 Generic 리그라 Mixamo 모션이 재생되지 않는다 → 모델 뼈에 직접 키를 넣은 모션
        // (HasturSlamMotionBuilder → _Motions/Generic/Attack_Light_HasturSlam.anim). 약·강·달리기 점프 공격 모두 이것으로.
        public static readonly string[] HasturSlamSlots = { "Attack_Light", "Attack_Heavy", "Attack_RunJump_Survivor" };
        private static readonly (string from, string to)[] HasturSlam =
            System.Array.ConvertAll(HasturSlamSlots, s => (s, HasturSlamMotionBuilder.ClipName));
        private static readonly (string name, (string from, string to)[][] tables)[] OverrideControllers =
        {
            // 감시자 공통: 사냥터지기·안토니오
            ("Hunter", new[] { HunterOverrides }),
            // 아래는 감시자 공통 + 기본 공격만 바꾼다
            // 루키노: 가로 베기. 대기 = 사람보다 큰 돌연변이라 상체를 편하게 앞으로 구부린 대기(Idle_Luchino, Blender make_hunched_idle.py)
            ("Luchino", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Luchino"), ("Idle", "Idle_Luchino") } }),
            // 공장장: 대기 = 자연스러운 숨쉬기 대기(Mixamo Idle, 걷기처럼 몸이 굳지 않게)
            ("HellEmber", new[] { HunterOverrides, new[] { ("Idle", "Idle_HellEmber") } }),
            // 안토니오: 대기 = 바이올린 켜기(Mixamo Playing The Violin의 활 긋기 부분만, 1.5배속 — Tools/Blender/make_loop_clip.py)
            ("Antonio", new[] { HunterOverrides, new[] { ("Idle", "Idle_Antonio") } }),
            ("Geisha", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Geisha") }, GhostGlide }), // 붉은나비: 오른손 찌르기, 유령처럼 미끄러져 이동
            // 하스터: 발이 없어 걷기·달리기 모두 몸을 숙였다 펴며 꾸물대는 모션(Zombie Attack 앞부분 왕복, Tools/Blender/make_pingpong.py)으로 미끄러져 간다.
            // 공격 = 두 손을 모아 머리 위 → 아래로 내려치기(HasturSlam — Generic 모델 전용 모션, 뒤에 적어 이긴다)
            ("Hastur", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Hastur") }, HasturCrawl, HasturSlam }),
            // 광대: 어떤 공격이든 두 손 무기(로켓)로 머리 위에서 아래로 내려친다. 강공격을 모으는 동안은 로켓을 받쳐 든 자세.
            ("SmileyFace", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Joker"), ("Attack_Heavy", "Attack_Heavy_Joker"), ("Attack_HeavyCharge", "Carry_Joker") } }),
            // 리퍼: 무기가 없고 왼손 자체가 무기 → 모든 공격을 왼손으로(감시자 공격을 Blender로 좌우 뒤집은 모션)
            ("Ripper", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Ripper"), ("Attack_Heavy", "Attack_Heavy_Ripper"), ("Attack_RunJump_Survivor", "Attack_RunJump_Ripper") } }),
            // 생존자: 생존자 Controller + 기본 공격만 바꾼다
            ("MindsEye", new[] { new[] { ("Attack_Light", "Attack_Light_MindsEye") } }),                 // 맹인: 돌며 가로 베기
            // 손에 아무것도 들지 않은 생존자(용병·도둑·카우보이 — 카우보이 소품은 ModelPropFixer가 없앰): 훅 펀치. 무언가 든 생존자(공군·의사·전위·맹인)는 그대로
            ("Mercenary", new[] { new[] { ("Attack_Light", "Attack_Light_HookPunch") } }),
            ("Thief", new[] { new[] { ("Attack_Light", "Attack_Light_HookPunch") } }),
            ("Cowboy", new[] { new[] { ("Attack_Light", "Attack_Light_HookPunch") } }),
        };

        [MenuItem("Tools/FE/모션 컨트롤러 다시 만들기")]
        public static void Build()
        {
            var clips = new Dictionary<string, AnimationClip>();
            var fromFile = new List<string>();
            var legacy = new List<string>();
            var missing = new List<string>();
            foreach (var slot in Slots)
            {
                var clip = LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/{slot}.fbx");
                // Generic 모델 전용 모션(하스터 내려치기)은 _Motions/Generic/<자리>.anim
                if (clip == null) clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{HasturSlamMotionBuilder.Folder}/{slot}.anim");
                // 옆·뒤 달리기 파일이 없으면 같은 방향 걷기 파일을 빠르게 돌려 쓴다(임시 모션보다 자연스럽다).
                string walkSlot = slot.StartsWith("Run_") ? "Walk_" + slot.Substring(4) : null;
                var walkClip = clip == null && walkSlot != null ? LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/{walkSlot}.fbx") : null;
                if (clip != null) fromFile.Add(slot);
                else if (walkClip != null) { clip = walkClip; fromFile.Add($"{slot}(←{walkSlot})"); }
                else
                {
                    // 감시자 대기(Idle_Hunter)는 파일이 없으면 예전 임시 대기(Procedural/Idle.anim)를 쓴다.
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{LegacyFolder}/{(slot == "Idle_Hunter" ? "Idle" : slot == "Attack_HeavyCharge_Hunter" ? "Attack_HeavyCharge" : slot)}.anim"); // 감시자 강공격 모으기도 예전 임시 모션
                    (clip != null ? legacy : missing).Add(slot);
                }
                clips[slot] = clip;
            }

            var ac = BuildController(clips);
            foreach (var (name, tables) in OverrideControllers)
                BuildOverride($"{MotionsFolder}/{name}Locomotion.overrideController", tables, ac, clips);
            foreach (var (path, slot) in SelectControllers) BuildSingleClip(path, LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/{slot}.fbx"));
            AssetDatabase.SaveAssets();
            Debug.Log($"[FE] 모션 컨트롤러를 다시 만들었습니다. 모션 파일: {string.Join(", ", fromFile)}"
                      + $" / 임시 모션(교체 대기): {string.Join(", ", legacy)}"
                      + (missing.Count > 0 ? $" / 없음: {string.Join(", ", missing)}" : ""));
        }

        private static bool HasMotionFile(string slot) => LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/{slot}.fbx") != null;

        private static AnimationClip LoadFbxClip(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        // 캐릭터 선택 화면용: 모션 하나만 반복하는 Controller. 선택 화면 모델의 Animator에 넣는다.
        private static readonly (string path, string slot)[] SelectControllers =
        {
            (MotionsFolder + "/HunterSelect.controller", "SelectIdle_Hunter"),
            (MotionsFolder + "/SurvivorSelect.controller", "SelectIdle_Survivor"),
        };

        private static void BuildSingleClip(string path, AnimationClip clip)
        {
            if (clip == null) return;
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ac.layers[0].stateMachine;
            foreach (var s in sm.states) sm.RemoveState(s.state);
            sm.defaultState = sm.AddState("Idle");
            sm.defaultState.motion = clip;
            EditorUtility.SetDirty(ac);
        }

        private static void BuildOverride(string path, (string from, string to)[][] tables, AnimatorController ac, Dictionary<string, AnimationClip> clips)
        {
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(ac);
                AssetDatabase.CreateAsset(aoc, path);
            }
            aoc.runtimeAnimatorController = ac;
            var map = new Dictionary<AnimationClip, AnimationClip>();
            foreach (var table in tables)
                foreach (var (from, to) in table)
                    if (clips[from] != null && clips[to] != null) map[clips[from]] = clips[to];
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, map.TryGetValue(pairs[i].Key, out var to) ? to : null);
            aoc.ApplyOverrides(pairs);
            EditorUtility.SetDirty(aoc);
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            // 기존 Controller는 지우지 않고 비워서 다시 채운다(GUID 유지 → 모델 연결이 끊기지 않음).
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ac == null)
            {
                ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                ac.parameters = new AnimatorControllerParameter[0];
                var oldSm = ac.layers[0].stateMachine;
                foreach (var s in oldSm.states) oldSm.RemoveState(s.state);
                foreach (var t in oldSm.anyStateTransitions) oldSm.RemoveAnyStateTransition(t);
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                    if (sub is BlendTree bt) Object.DestroyImmediate(bt, true);
                while (ac.layers.Length > 1) ac.RemoveLayer(ac.layers.Length - 1);
            }
            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("IsDown", AnimatorControllerParameterType.Bool);
            var ps = ac.parameters;
            for (int i = 0; i < ps.Length; i++) if (ps[i].name == "IsGrounded") ps[i].defaultBool = true;
            ac.parameters = ps;

            var sm = ac.layers[0].stateMachine;

            var locomotion = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            const float runR = CharacterAnimatorDriver.RunBlendValue; // 드라이버가 달리기 속도를 이 값으로 맞춘다
            tree.AddChild(clips["Idle"], Vector2.zero);
            tree.AddChild(clips["Walk_F"], new Vector2(0, 1));
            tree.AddChild(clips["Walk_B"], new Vector2(0, -1));
            tree.AddChild(clips["Walk_L"], new Vector2(-1, 0));
            tree.AddChild(clips["Walk_R"], new Vector2(1, 0));
            tree.AddChild(clips["Run_F"], new Vector2(0, runR));
            tree.AddChild(clips["Run_B"], new Vector2(0, -runR));
            tree.AddChild(clips["Run_L"], new Vector2(-runR, 0));
            tree.AddChild(clips["Run_R"], new Vector2(runR, 0));
            // 걷기 모션을 달리기 자리에 빌려 쓴 경우 빨리 재생한다.
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
                if (children[i].position.magnitude > 1.5f && children[i].motion != null && children[i].motion.name.StartsWith("Walk_"))
                    children[i].timeScale = BorrowedWalkSpeed;
            tree.children = children;
            sm.defaultState = locomotion;

            var air = sm.AddState("Air");
            air.motion = clips["Air"];
            if (clips["Air"] != null && !clips["Air"].isLooping) air.speed = clips["Air"].length / JumpAirTime;

            // 앞으로 움직이며 점프: 뛰면서 = Air_Run, 걸으면서 = Air_F.
            // 전환은 위에서부터 검사하므로 빠른 쪽 → 느린 쪽 → 제자리 점프 순서로 넣는다.
            AddForwardJump(sm, locomotion, clips["Air_Run"], "Air_Run", RunJumpMoveZ);
            AddForwardJump(sm, locomotion, clips["Air_F"], "Air_F", ForwardJumpMoveZ);

            var toAirJump = locomotion.AddTransition(air);
            toAirJump.hasExitTime = false; toAirJump.duration = 0.1f;
            toAirJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

            var toAirFall = locomotion.AddTransition(air);
            toAirFall.hasExitTime = false; toAirFall.duration = 0.15f;
            toAirFall.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");

            var toGround = air.AddTransition(locomotion);
            toGround.hasExitTime = false; toGround.duration = 0.12f;
            toGround.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

            // 쓰러짐(사망): IsDown이 켜지면(체력 0, CharacterDeathAnimator) 어디서든 쓰러지고(Down = Mixamo Zombie Dying), 마지막 자세(누운 자세)로 멈춘다. 꺼지면 이동으로 돌아간다.
            if (clips["Down"] != null)
            {
                var down = sm.AddState("Down");
                down.motion = clips["Down"];
                var toDown = sm.AddAnyStateTransition(down);
                toDown.hasExitTime = false; toDown.duration = 0.1f; toDown.canTransitionToSelf = false;
                toDown.AddCondition(AnimatorConditionMode.If, 0, "IsDown");
                var getUp = down.AddTransition(locomotion);
                getUp.hasExitTime = false; getUp.duration = 0.3f;
                getUp.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDown");
            }

            // 계단 오르기 레이어(전신, 이동 바로 위 — 다른 레이어가 모두 덮는다): 앞으로 계단을 오를 때 CharacterStairsAnimator가 무게를 켠다.
            // 빠르기는 StairsSpeed(앞 속도에 맞춤).
            if (clips["Stairs_Up"] != null)
            {
                ac.AddParameter("StairsSpeed", AnimatorControllerParameterType.Float);
                var sp = ac.parameters;
                for (int i = 0; i < sp.Length; i++) if (sp[i].name == "StairsSpeed") sp[i].defaultFloat = 1f;
                ac.parameters = sp;
                ac.AddLayer("Stairs");
                var stl = ac.layers;
                var stairs = stl[stl.Length - 1];
                stairs.defaultWeight = 0f; stairs.blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = stl;
                var stsm = ac.layers[ac.layers.Length - 1].stateMachine;
                stsm.defaultState = stsm.AddState("Stairs_Up");
                stsm.defaultState.motion = clips["Stairs_Up"];
                stsm.defaultState.speedParameterActive = true;
                stsm.defaultState.speedParameter = "StairsSpeed";
            }

            AddTraversalLayer(ac);

            // 들고 다니기 레이어(두 팔만, 공격보다 아래): 무기를 두 손으로 받쳐 든 자세(광대 = Walk With Rifle의 팔).
            // 다리는 걷기·달리기 그대로. 무게는 CharacterCarryPose가 켠다(기본 0 → 다른 캐릭터는 영향 없음).
            if (HasMotionFile("Carry_Joker"))
            {
                ac.AddLayer("Carry");
                var cl = ac.layers;
                var carry = cl[cl.Length - 1];
                carry.avatarMask = LoadArmsMask(); carry.defaultWeight = 0f; carry.blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = cl;
                var csm = ac.layers[ac.layers.Length - 1].stateMachine;
                csm.defaultState = csm.AddState("Carry");
                csm.defaultState.motion = LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/Carry_Joker.fbx");
            }

            // 바이올린 켜기 레이어(상체만, 공격보다 아래): 안토니오가 음표를 놓을 자리를 고르는 동안과 놓은 뒤 잠깐 바이올린을 켠다(대기와 같은 Idle_Antonio).
            // 다리는 걷기·달리기 그대로. 무게는 AntonioViolin이 켠다(기본 0 → 다른 캐릭터는 영향 없음).
            if (HasMotionFile("Idle_Antonio"))
            {
                ac.AddLayer("Violin");
                var vl = ac.layers;
                var vlayer = vl[vl.Length - 1];
                vlayer.avatarMask = LoadUpperBodyMask(); vlayer.defaultWeight = 0f; vlayer.blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = vl;
                var vsm = ac.layers[ac.layers.Length - 1].stateMachine;
                vsm.defaultState = vsm.AddState("Violin");
                vsm.defaultState.motion = LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/Idle_Antonio.fbx");
            }

            // 땅 공격 레이어(상체만). 상태 전환과 재생 위치는 CharacterAttackAnimator가 코드로 정한다.
            var mask = LoadUpperBodyMask();
            ac.AddLayer(AttackLayer);
            var layers = ac.layers;
            var atk = layers[layers.Length - 1];
            atk.avatarMask = mask; atk.defaultWeight = 0f; atk.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            var asm = ac.layers[ac.layers.Length - 1].stateMachine;
            asm.defaultState = asm.AddState("Empty");
            asm.AddState("Light").motion = clips["Attack_Light"];
            // 좌우로 그어 후리기(맹인만 씀): 왼쪽 = 오른손 앞치기(오른쪽 → 앞 → 왼쪽), 오른쪽 = 그 모션을 Blender로 거꾸로(Tools/Blender/reverse_motion.py)
            if (clips["Attack_SwipeL"] != null) asm.AddState("SwipeL").motion = clips["Attack_SwipeL"];
            if (clips["Attack_SwipeR"] != null) asm.AddState("SwipeR").motion = clips["Attack_SwipeR"];
            asm.AddState("HeavyCharge").motion = clips["Attack_HeavyCharge"];
            asm.AddState("Heavy").motion = clips["Attack_Heavy"];
            // 생존자 주먹 막기(좌클릭 꾹, BE FistParry): 막는 자세(Mixamo Standing Block Idle). 걸으면 상체만 — 서 있으면 아래 전신 레이어의 같은 상태.
            var blockClip = LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/Block.fbx");
            if (blockClip != null) asm.AddState("Block").motion = blockClip;

            // 공중 공격 레이어(전신, 마스크 없음): 발 내려찍기
            ac.AddLayer(AirAttackLayer);
            var layers2 = ac.layers;
            var air2 = layers2[layers2.Length - 1];
            air2.defaultWeight = 0f; air2.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers2;
            var airSm = ac.layers[ac.layers.Length - 1].stateMachine;
            airSm.defaultState = airSm.AddState("Empty");
            // 제자리 공중 공격·공중 모으기는 모션 파일이 있을 때만 상태를 만든다(없으면 공중에서도 땅 공격 모션을 쓴다).
            if (HasMotionFile("Attack_AirStomp")) airSm.AddState("AirStomp").motion = clips["Attack_AirStomp"];
            if (HasMotionFile("Attack_AirCharge")) airSm.AddState("AirCharge").motion = clips["Attack_AirCharge"];
            // 앞으로 달리다 점프해서 공격: 생존자 Flying Kick (감시자는 Override Controller로 바꾼다)
            if (clips["Attack_RunJump_Survivor"] != null) airSm.AddState("RunJump").motion = clips["Attack_RunJump_Survivor"];
            // 생존자 강공격(땅): 머리를 노리는 왼발 돌려차기(Mixamo Martelo 2). 다리까지 움직이므로 전신 레이어에 둔다. 쓸지는 CharacterAttackAnimator.HeavyKick(생존자만 켬)이 정한다.
            if (clips["Attack_HeavyKick"] != null) airSm.AddState("HeavyKick").motion = clips["Attack_HeavyKick"];
            // 생존자 강공격 모으기(서 있을 때): 통통 뛰는 격투 자세(Mixamo Bouncing Fight Idle) 전신. 걸으면 땅 공격 레이어(상체)의 같은 모션 + 다리는 걷기.
            if (clips["Attack_HeavyCharge"] != null) airSm.AddState("ChargeStance").motion = clips["Attack_HeavyCharge"];
            // 생존자 주먹 막기(서 있을 때): 막는 자세 전신.
            if (blockClip != null) airSm.AddState("Block").motion = blockClip;
            // 생존자 두 번째 점프 공격(달리기 점프 공격 중 한 번 더): 다시 뛰어오르며 머리를 노리는 날아 차기. 쓸지는 BE(CharacterAttack.IsFollowUpKick, 생존자만)가 정한다.
            if (clips["Attack_RunJump2_Survivor"] != null) airSm.AddState("RunJump2").motion = clips["Attack_RunJump2_Survivor"];

            // 피격 레이어(상체만): 맞은 부위·방향에 맞는 모션을 CharacterHitReactionAnimator가 한 번 재생한다.
            ac.AddLayer("HitReact");
            var layers4 = ac.layers;
            var hitLayer = layers4[layers4.Length - 1];
            hitLayer.avatarMask = mask; hitLayer.defaultWeight = 0f; hitLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers4;
            var hitSm = ac.layers[ac.layers.Length - 1].stateMachine;
            hitSm.defaultState = hitSm.AddState("Empty");
            foreach (var slot in new[] { "Hit_Head", "Hit_Head2", "Hit_Head_Light", "Hit_Head_Medium", "Hit_Body_Front", "Hit_Body_Side", "Hit_Body_Back", "Hit_Limb", "Hit_Small", "Stun" })
                if (clips[slot] != null) hitSm.AddState(slot).motion = clips[slot];
            // 큰 데미지(CharacterHitReactionAnimator.BigDamage 이상 — 감시자 강공격·창틀 넘기 중 2배 등): 크게 꺾였다 젖혀짐(Big Hit To Head). 1.63초 → 약 1.3초
            if (clips["Hit_Head_Big"] != null) { var big = hitSm.AddState("Hit_Head_Big"); big.motion = clips["Hit_Head_Big"]; big.speed = 1.25f; }

            // 전신 피격 레이어: 쓰러지며 나가떨어지는 피격(헤드샷 킥 = Corkscrew Evade). CharacterHitReactionAnimator가 무게를 켠다.
            ac.AddLayer("HitReactFull");
            var fullLayers = ac.layers;
            var fullHit = fullLayers[fullLayers.Length - 1];
            fullHit.defaultWeight = 0f; fullHit.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = fullLayers;
            var fullSm = ac.layers[ac.layers.Length - 1].stateMachine;
            fullSm.defaultState = fullSm.AddState("Empty");
            if (clips["Hit_HeadKick"] != null) { var kd = fullSm.AddState("Hit_HeadKick"); kd.motion = clips["Hit_HeadKick"]; kd.speed = 1.27f; } // 나가떨어져 눕기까지(약 0.8초). 일어나기는 GetUp 레이어(Kip Up)가 이어 받는다(기절 1.5초 안에)
            // 뒤에서 맞아 기절(전위 돌진·신호탄): 휘청이며 돌아 쓰러져 누움(Turn To Knocked Unconscious). 재생 위치는 CharacterHitReactionAnimator가 기절 시간에 맞춘다.
            if (clips["Stun_Back"] != null) fullSm.AddState("Stun_Back").motion = clips["Stun_Back"];

            // 데미지 없는 충격파에 맞음(사냥터지기 그랩이 벽에 도착): 전신으로 휘청임(Edge Slip). CharacterShockReactionAnimator가 무게를 켠다.
            if (clips["Hit_EdgeSlip"] != null)
            {
                ac.AddLayer("ShockReact");
                var shockLayers = ac.layers;
                var shock = shockLayers[shockLayers.Length - 1];
                shock.defaultWeight = 0f; shock.blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = shockLayers;
                var shockSm = ac.layers[ac.layers.Length - 1].stateMachine;
                shockSm.defaultState = shockSm.AddState("Empty");
                var slip = shockSm.AddState("Hit_EdgeSlip"); slip.motion = clips["Hit_EdgeSlip"]; slip.speed = 1.5f; // 2.5초 → 약 1.65초(감속 1.5초에 맞춤)
            }

            // 고유 스킬 레이어(전신): Mixamo/Skill_<스킬 ID>.fbx마다 상태 하나. 재생 위치는 CharacterSkillAnimator가 정한다.
            // 반복 모션(예: 돌진 달리기)은 SkillLoopSpeed 배속으로 계속 돌고, 아니면 시전 진행도에 맞춘다.
            // 스킬이 도중에 막혀 넘어질 때는 <스킬 ID>_End 상태(SkillEndMotions)를 쓴다.
            ac.AddLayer("Skill");
            var layers3 = ac.layers;
            var skillLayer = layers3[layers3.Length - 1];
            skillLayer.defaultWeight = 0f; skillLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers3;
            var skillSm = ac.layers[ac.layers.Length - 1].stateMachine;
            skillSm.defaultState = skillSm.AddState("Empty");
            // 상체 스킬 레이어: UpperBodySkillClips의 상태는 여기(상체 마스크) — 다리는 아래 레이어의 걷기·달리기를 그대로 쓴다.
            ac.AddLayer("SkillUpper");
            var upperLayers = ac.layers;
            var upperLayer = upperLayers[upperLayers.Length - 1];
            upperLayer.avatarMask = mask; upperLayer.defaultWeight = 0f; upperLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = upperLayers;
            var upperSm = ac.layers[ac.layers.Length - 1].stateMachine;
            upperSm.defaultState = upperSm.AddState("Empty");
            foreach (var guid in AssetDatabase.FindAssets("Skill_ t:Model", new[] { MixamoMotionPostprocessor.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("Skill_")) continue;
                // 한 파일에 클립이 여럿이면(예: Skill_Luchino_Leap, Skill_Luchino_Leap_Slam) 클립마다 상태를 만든다.
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is not AnimationClip clip || clip.name.StartsWith("__preview") || !clip.name.StartsWith("Skill_")) continue;
                    // 자세를 고친 모션(MotionPoseAdjuster)이 있으면 그것을 쓴다. 몸을 낮춘 모션이라 Foot IK로 발을 땅에 붙인다.
                    var adjusted = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{MotionPoseAdjuster.Folder}/{clip.name}.anim");
                    string stateName = clip.name.Substring("Skill_".Length);
                    var state = (UpperBodySkillClips.Contains(clip.name) ? upperSm : skillSm).AddState(stateName);
                    state.motion = adjusted != null ? adjusted : clip;
                    state.iKOnFeet = adjusted != null;
                    if (SkillLoopSpeed.TryGetValue(stateName, out float loopSpeed)) state.speed = loopSpeed;
                }
            }
            // 다른 자리의 모션을 그대로 쓰는 스킬(예: 사냥터지기 그랩 쏘기 = 감시자 기본 공격 내려치기 모션).
            foreach (var (skillId, slot, speed) in SkillClipAliases)
                if (!HasMotionFile("Skill_" + skillId) && clips.TryGetValue(slot, out var aliasClip) && aliasClip != null)
                {
                    var s = skillSm.AddState(skillId);
                    s.motion = aliasClip;
                    s.speed = speed;
                }
            foreach (var (skillId, slot) in SkillEndMotions)
                if (clips.TryGetValue(slot, out var endClip) && endClip != null)
                    skillSm.AddState(skillId + "_End").motion = endClip;

            // 일어나기 레이어(전신, 맨 위): 누워 있다가 일어날 때는 상황·캐릭터와 상관없이 모두 Kip Up(GetUp). 재생 위치는 CharacterGetUpAnimator가 행동 불가 시간에 맞춘다.
            if (clips["GetUp"] != null)
            {
                ac.AddLayer("GetUp");
                var getUpLayers = ac.layers;
                var getUpLayer = getUpLayers[getUpLayers.Length - 1];
                getUpLayer.defaultWeight = 0f; getUpLayer.blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = getUpLayers;
                var getUpSm = ac.layers[ac.layers.Length - 1].stateMachine;
                getUpSm.defaultState = getUpSm.AddState("Empty");
                getUpSm.AddState("GetUp").motion = clips["GetUp"];
            }

            EditorUtility.SetDirty(ac);
            return ac;
        }

        public const string TraversalLayer = "Traversal";

        /// <summary>
        /// 오르기·매달리기·낙하 레이어(전신, 기본 레이어 바로 위 — 공격·피격·스킬 레이어가 그 위를 덮는다).
        /// Mixamo/Traversal_*.fbx 안의 클립(이름 Traversal_X)마다 상태 X 하나. 무게·재생 위치는 CharacterTraversalAnimator가 정한다.
        /// </summary>
        private static void AddTraversalLayer(AnimatorController ac)
        {
            ac.AddLayer(TraversalLayer);
            var layers = ac.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 0f; layer.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            var sm = ac.layers[ac.layers.Length - 1].stateMachine;
            sm.defaultState = sm.AddState("Empty");
            foreach (var guid in AssetDatabase.FindAssets("Traversal_ t:Model", new[] { MixamoMotionPostprocessor.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("Traversal_")) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (o is not AnimationClip clip || clip.name.StartsWith("__preview") || !clip.name.StartsWith("Traversal_")) continue;
                    string stateName = clip.name.Substring("Traversal_".Length);
                    if (sm.states.Any(s => s.state.name == stateName)) continue;
                    var st = sm.AddState(stateName);
                    st.motion = clip;
                    if (stateName == "Push") st.speed = 2.2f; // 모션은 아주 느리게 밀며 걷는다(약 0.25m/s) → 게임 미는 빠르기(CharacterPush 1m/s)에 맞춰 빠르게
                    if (stateName == "HangHop" && !sm.states.Any(s => s.state.name == "ClimbUpRun"))
                    {
                        // 달리며 책상 등 낮은 턱에 올라서기(ClimbUpRun): 다리는 달리기 모션 그대로(Run_F — 감시자·붉은나비 등은 Override Controller가 바꾼 달리기).
                        var runClip = LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/Run_F.fbx");
                        if (runClip != null) sm.AddState("ClimbUpRun").motion = runClip;
                    }
                    if (stateName == "HangHop")
                    {
                        // 오른쪽으로 건너 잡기: 같은 모션을 좌우 뒤집어(Humanoid mirror) 쓴다.
                        var mirrored = sm.AddState("HangHopRight");
                        mirrored.motion = clip;
                        mirrored.mirror = true;
                    }
                }
            }
        }

        private static void AddForwardJump(AnimatorStateMachine sm, AnimatorState locomotion, AnimationClip clip, string name, float minMoveZ)
        {
            if (clip == null) return;
            var state = sm.AddState(name);
            state.motion = clip;
            state.speed = clip.length / JumpAirTime;
            var enter = locomotion.AddTransition(state);
            enter.hasExitTime = false; enter.duration = 0.1f;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            enter.AddCondition(AnimatorConditionMode.Greater, minMoveZ, "MoveZ");
            var land = state.AddTransition(locomotion);
            land.hasExitTime = false; land.duration = 0.12f;
            land.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        }

        public const string ArmsMaskPath = "Assets/Models/IdentityV/_Motions/ArmsMask.mask";

        private static AvatarMask LoadArmsMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ArmsMaskPath);
            if (mask != null) return mask;
            mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            AssetDatabase.CreateAsset(mask, ArmsMaskPath);
            return mask;
        }

        private static AvatarMask LoadUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask != null) return mask;
            mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                       || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                       || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }
    }
}

using System.Collections.Generic;
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

        // 반복하는 스킬 모션의 재생 배속. 전위 돌진은 이동 속도를 2배(16m/s)로 올린 만큼 달리기 모션도 2배 빠르게.
        private static readonly Dictionary<string, float> SkillLoopSpeed = new() { ["Forward_Charge"] = 2f };

        // 스킬이 도중에 막혀 끝날 때(넘어짐) 쓰는 모션. 전위 돌진이 벽에 부딪힘 → Fall_1(Rifle Hit To Back).
        private static readonly (string skillId, string slot)[] SkillEndMotions = { ("Forward_Charge", "Fall_1") };

        // 스킬 상태에 다른 자리 모션을 쓴다(스킬 ID, 자리, 배속). 사냥터지기 그랩 던지기 = Standing Melee Attack Downward(사용자 지정),
        // 그랩이 뻗어 나가는 순간(준비 0.3초)에 맞춰 1.6배속.
        private static readonly (string skillId, string slot, float speed)[] SkillClipAliases = { ("Gamekeeper_Grab", "Attack_Light_Hunter", 1.6f) };

        private static readonly string[] Slots =
        {
            "Idle", "Idle_Hunter", "Idle_Thief", "Idle_Luchino", "Air", "Air_F", "Air_Run", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
            "Attack_Light", "Attack_HeavyCharge", "Attack_Heavy", "Attack_AirStomp", "Attack_AirCharge", "Down",
            "Attack_RunJump_Survivor", "Attack_RunJump_Hunter", "Attack_Light_Hunter",
            "Attack_Light_Luchino", "Attack_Light_Geisha", "Attack_Light_Hastur", "Attack_Light_MindsEye",
            "Hit_Head", "Hit_Body_Side", "Hit_Body_Back", "Hit_Limb", "Stun", "Fall_1",
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
        };
        // 유령처럼 이동: 걷기·달리기에서도 다리를 움직이지 않고 대기 자세 그대로 미끄러진다.
        // (옆·뒤 달리기는 같은 방향 걷기 클립을 빌려 쓰므로 걷기만 바꾸면 같이 바뀐다.)
        private static readonly (string from, string to)[] GhostGlide =
        {
            ("Walk_F", "Idle_Hunter"), ("Walk_B", "Idle_Hunter"), ("Walk_L", "Idle_Hunter"), ("Walk_R", "Idle_Hunter"),
            ("Run_F", "Idle_Hunter"), ("Run_B", "Idle_Hunter"), ("Run_L", "Idle_Hunter"), ("Run_R", "Idle_Hunter"),
        };
        private static readonly (string name, (string from, string to)[][] tables)[] OverrideControllers =
        {
            // 감시자 공통: 공장장·사냥터지기·안토니오·리퍼·광대
            ("Hunter", new[] { HunterOverrides }),
            // 아래는 감시자 공통 + 기본 공격만 바꾼다
            ("Luchino", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Luchino"), ("Idle", "Idle_Luchino") } }), // 루키노: 가로 베기, 웅크린 대기(Crouch Idle)
            ("Geisha", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Geisha") }, GhostGlide }), // 붉은나비: 오른손 찌르기, 유령처럼 미끄러져 이동
            ("Hastur", new[] { HunterOverrides, new[] { ("Attack_Light", "Attack_Light_Hastur") } }),   // 하스터: 두 손 마법
            // 생존자: 생존자 Controller + 기본 공격만 바꾼다
            ("MindsEye", new[] { new[] { ("Attack_Light", "Attack_Light_MindsEye") } }),                 // 맹인: 돌며 가로 베기
            ("Thief", new[] { new[] { ("Idle", "Idle_Thief") } }),                                     // 도둑: 가만히 있을 때 Ninja Idle
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
                // 옆·뒤 달리기 파일이 없으면 같은 방향 걷기 파일을 빠르게 돌려 쓴다(임시 모션보다 자연스럽다).
                string walkSlot = slot.StartsWith("Run_") ? "Walk_" + slot.Substring(4) : null;
                var walkClip = clip == null && walkSlot != null ? LoadFbxClip($"{MixamoMotionPostprocessor.Folder}/{walkSlot}.fbx") : null;
                if (clip != null) fromFile.Add(slot);
                else if (walkClip != null) { clip = walkClip; fromFile.Add($"{slot}(←{walkSlot})"); }
                else
                {
                    // 감시자 대기(Idle_Hunter)는 파일이 없으면 예전 임시 대기(Procedural/Idle.anim)를 쓴다.
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{LegacyFolder}/{(slot == "Idle_Hunter" ? "Idle" : slot)}.anim");
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

            // 맞아서 쓰러짐: IsDown이 켜지면 어디서든 쓰러지고, 마지막 자세(누운 자세)로 멈춘다. 꺼지면 이동으로 돌아간다.
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
            asm.AddState("HeavyCharge").motion = clips["Attack_HeavyCharge"];
            asm.AddState("Heavy").motion = clips["Attack_Heavy"];

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

            // 피격 레이어(상체만): 맞은 부위·방향에 맞는 모션을 CharacterHitReactionAnimator가 한 번 재생한다.
            ac.AddLayer("HitReact");
            var layers4 = ac.layers;
            var hitLayer = layers4[layers4.Length - 1];
            hitLayer.avatarMask = mask; hitLayer.defaultWeight = 0f; hitLayer.blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers4;
            var hitSm = ac.layers[ac.layers.Length - 1].stateMachine;
            hitSm.defaultState = hitSm.AddState("Empty");
            foreach (var slot in new[] { "Hit_Head", "Hit_Body_Side", "Hit_Body_Back", "Hit_Limb", "Stun" })
                if (clips[slot] != null) hitSm.AddState(slot).motion = clips[slot];

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
                    var state = skillSm.AddState(stateName);
                    state.motion = adjusted != null ? adjusted : clip;
                    state.iKOnFeet = adjusted != null;
                    if (SkillLoopSpeed.TryGetValue(stateName, out float loopSpeed)) state.speed = loopSpeed;
                }
            }
            // 다른 자리의 모션을 그대로 쓰는 스킬(예: 사냥터지기 그랩 쏘기 = 감시자 기본 공격 내려치기 모션).
            foreach (var (skillId, slot, speed) in SkillClipAliases)
                if (clips.TryGetValue(slot, out var aliasClip) && aliasClip != null)
                {
                    var s = skillSm.AddState(skillId);
                    s.motion = aliasClip;
                    s.speed = speed;
                }
            foreach (var (skillId, slot) in SkillEndMotions)
                if (clips.TryGetValue(slot, out var endClip) && endClip != null)
                    skillSm.AddState(skillId + "_End").motion = endClip;

            EditorUtility.SetDirty(ac);
            return ac;
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

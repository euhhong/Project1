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

        private static readonly string[] Slots =
        {
            "Idle", "Air", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
            "Attack_Light", "Attack_HeavyCharge", "Attack_Heavy", "Attack_AirStomp", "Attack_AirCharge", "Down",
            "Attack_RunJump_Survivor", "Attack_RunJump_Hunter",
        };

        // 감시자용 Override Controller: 생존자 Controller에서 아래 자리만 감시자 모션으로 바꾼다.
        public const string HunterControllerPath = "Assets/Models/IdentityV/_Motions/HunterLocomotion.overrideController";
        private static readonly (string survivor, string hunter)[] HunterOverrides =
        {
            ("Attack_RunJump_Survivor", "Attack_RunJump_Hunter"),
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
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{LegacyFolder}/{slot}.anim");
                    (clip != null ? legacy : missing).Add(slot);
                }
                clips[slot] = clip;
            }

            var ac = BuildController(clips);
            BuildHunterOverride(ac, clips);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FE] 모션 컨트롤러를 다시 만들었습니다. 모션 파일: {string.Join(", ", fromFile)}"
                      + $" / 임시 모션(교체 대기): {string.Join(", ", legacy)}"
                      + (missing.Count > 0 ? $" / 없음: {string.Join(", ", missing)}" : ""));
        }

        private static AnimationClip LoadFbxClip(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        private static void BuildHunterOverride(AnimatorController ac, Dictionary<string, AnimationClip> clips)
        {
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(HunterControllerPath);
            if (aoc == null)
            {
                aoc = new AnimatorOverrideController(ac);
                AssetDatabase.CreateAsset(aoc, HunterControllerPath);
            }
            aoc.runtimeAnimatorController = ac;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            foreach (var (survivor, hunter) in HunterOverrides)
                if (clips[survivor] != null && clips[hunter] != null)
                    pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(clips[survivor], clips[hunter]));
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
            airSm.AddState("AirStomp").motion = clips["Attack_AirStomp"];
            airSm.AddState("AirCharge").motion = clips["Attack_AirCharge"];
            // 앞으로 달리다 점프해서 공격: 생존자 Flying Kick (감시자는 Override Controller로 바꾼다)
            airSm.AddState("RunJump").motion = clips["Attack_RunJump_Survivor"] ?? clips["Attack_AirStomp"];

            EditorUtility.SetDirty(ac);
            return ac;
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

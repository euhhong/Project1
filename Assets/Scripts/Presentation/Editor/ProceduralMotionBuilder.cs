using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// Humanoid 근육 커브로 기본 이동 모션(대기·걷기·달리기 4방향·공중)을 만들고,
    /// 이 모션을 쓰는 Animator Controller를 만든다. VARCO 모션이 들어오기 전 임시 모션이다.
    /// 메뉴: Tools > FE > 기본 모션 다시 만들기
    /// </summary>
    public static class ProceduralMotionBuilder
    {
        public const string Folder = "Assets/Models/IdentityV/_Motions/Procedural";
        public const string ControllerPath = "Assets/Models/IdentityV/_Motions/CharacterLocomotion.controller";
        public const string AttackLayer = "Attack";
        public const string AirAttackLayer = "AirAttack";

        private const int Samples = 24;

        // 이동 모션 한 주기 설정
        private struct Gait
        {
            public float duration;   // 한 주기(초)
            public float legSwing;   // 허벅지 앞뒤 흔들기
            public float kneeBend;   // 다리를 앞으로 낼 때 무릎 굽힘
            public float armSwing;   // 팔 앞뒤 흔들기
            public float forearm;    // 팔꿈치 굽힘(1 = 폄)
            public float lean;       // 상체 숙임
            public float bounce;     // 상체 위아래 출렁임(근육값)
            public float armDown;    // 팔 내림(-1 = 몸에 붙임). 0이면 기본값 사용
        }

        // 걷기: 가볍게 뛰는 정도(조깅)
        private static readonly Gait Walk = new Gait { duration = 0.56f, legSwing = 0.6f, kneeBend = 0.55f, armSwing = 0.4f, forearm = 0.15f, lean = 0.18f, bounce = 0.1f };
        // 달리기: 전력질주. 팔을 크게 흔들고 팔꿈치를 굽히고 상체를 숙인다.
        private static readonly Gait Run = new Gait { duration = 0.42f, legSwing = 0.85f, kneeBend = 0.85f, armSwing = 0.62f, forearm = -0.3f, lean = 0.38f, bounce = 0.13f, armDown = -0.45f };

        private const float ArmDown = -0.62f;   // 팔을 몸 옆으로 내림 (A포즈 → 차렷에 가깝게)

        // 서 있을 때 몸 중심 높이(Humanoid 정규화 단위). BuildAll에서 씬 캐릭터 아바타로 계산한다.
        private static float bodyHeight = 1f;

        // 기본 자세(바인드 포즈)의 근육 값. 다리·몸통 커브는 이 값에 더하는 변화량으로 쓴다.
        private static readonly Dictionary<string, float> baseMuscles = new();

        // 팔은 A포즈 기준이 아니라 절대값으로 정한다(몸 옆으로 내린 자세). 비틀림(Twist)은 기본 자세 값을 쓴다.
        private static bool IsAbsolute(string muscle) =>
            (muscle.Contains("Arm") || muscle.Contains("Shoulder")) && !muscle.Contains("Twist");

        private static float Base(string muscle) =>
            !IsAbsolute(muscle) && baseMuscles.TryGetValue(muscle, out var v) ? v : 0f;

        [MenuItem("Tools/FE/기본 모션 다시 만들기")]
        public static void BuildAll()
        {
            bodyHeight = MeasureBodyHeight();
            EnsureFolder(Folder);

            var clips = new Dictionary<string, AnimationClip>
            {
                ["Idle"] = Save("Idle", BuildIdle()),
                ["Air"] = Save("Air", BuildAir()),
            };
            foreach (var (name, gait) in new[] { ("Walk", Walk), ("Run", Run) })
            {
                clips[name + "_F"] = Save(name + "_F", BuildGait(gait, Direction.Forward));
                clips[name + "_B"] = Save(name + "_B", BuildGait(gait, Direction.Backward));
                clips[name + "_L"] = Save(name + "_L", BuildGait(gait, Direction.Left));
                clips[name + "_R"] = Save(name + "_R", BuildGait(gait, Direction.Right));
            }

            clips["Attack_Light"] = Save("Attack_Light", BuildLightAttack());
            clips["Attack_HeavyCharge"] = Save("Attack_HeavyCharge", BuildHeavyCharge());
            clips["Attack_Heavy"] = Save("Attack_Heavy", BuildHeavyAttack());
            clips["Attack_AirStomp"] = Save("Attack_AirStomp", BuildAirStomp());
            clips["Attack_AirCharge"] = Save("Attack_AirCharge", BuildAirCharge());

            BuildController(clips);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FE] 기본 모션 {clips.Count}개와 Animator Controller를 만들었습니다: {ControllerPath}");
        }

        private enum Direction { Forward, Backward, Left, Right }

        // ---------- 클립 생성 ----------

        private static AnimationClip BuildIdle()
        {
            var c = new CurveSet(2.4f);
            c.Add("Spine Front-Back", p => 0.03f + 0.03f * Mathf.Sin(p));
            c.Add("Chest Front-Back", p => 0.03f * Mathf.Sin(p));
            c.Add("Head Nod Down-Up", p => -0.05f + 0.02f * Mathf.Sin(p + 1f));
            AddRelaxedArms(c, p => 0.02f * Mathf.Sin(p), 0.9f);
            c.Add("Left Upper Leg In-Out", _ => 0.05f);
            c.Add("Right Upper Leg In-Out", _ => 0.05f);
            c.Add("Left Lower Leg Stretch", _ => 0.95f);
            c.Add("Right Lower Leg Stretch", _ => 0.95f);
            return c.ToClip(true);
        }

        private static AnimationClip BuildAir()
        {
            var c = new CurveSet(1f);
            c.Add("Spine Front-Back", _ => 0.15f);
            c.Add("Left Upper Leg Front-Back", _ => 0.45f);
            c.Add("Right Upper Leg Front-Back", _ => 0.15f);
            c.Add("Left Lower Leg Stretch", _ => -0.2f);
            c.Add("Right Lower Leg Stretch", _ => 0.2f);
            c.Add("Left Arm Down-Up", _ => -0.25f);
            c.Add("Right Arm Down-Up", _ => -0.25f);
            c.Add("Left Arm Front-Back", _ => 0.2f);
            c.Add("Right Arm Front-Back", _ => 0.2f);
            c.Add("Left Forearm Stretch", _ => 0.3f);
            c.Add("Right Forearm Stretch", _ => 0.3f);
            return c.ToClip(true);
        }

        private static AnimationClip BuildGait(Gait g, Direction dir)
        {
            var c = new CurveSet(g.duration, g.bounce * 0.25f);
            bool sideways = dir == Direction.Left || dir == Direction.Right;
            float sign = dir == Direction.Backward ? -1f : 1f;
            Func<float, float> phase = p => sign * p;   // 뒷걸음은 주기를 거꾸로 돌린다

            if (!sideways)
            {
                // 왼다리와 오른다리를 반대로 흔들고, 앞으로 내미는 다리의 무릎을 굽힌다.
                c.Add("Left Upper Leg Front-Back", p => g.legSwing * Mathf.Sin(phase(p)));
                c.Add("Right Upper Leg Front-Back", p => -g.legSwing * Mathf.Sin(phase(p)));
                c.Add("Left Lower Leg Stretch", p => Knee(g, Mathf.Cos(phase(p)) * sign));
                c.Add("Right Lower Leg Stretch", p => Knee(g, -Mathf.Cos(phase(p)) * sign));
                c.Add("Left Foot Up-Down", p => 0.2f * Mathf.Sin(phase(p)));
                c.Add("Right Foot Up-Down", p => -0.2f * Mathf.Sin(phase(p)));
                c.Add("Spine Front-Back", p => g.lean * sign + g.bounce * Mathf.Cos(2f * p));
                c.Add("Spine Left-Right", _ => 0f);
                c.Add("Chest Twist Left-Right", p => 0.08f * Mathf.Sin(phase(p)));
                // 팔은 다리와 반대로 흔든다.
                AddRelaxedArms(c, p => -g.armSwing * Mathf.Sin(phase(p)), g.forearm, g.armDown);
            }
            else
            {
                // 옆걸음: 두 다리를 벌렸다 모으기를 반복하고, 번갈아 무릎을 굽힌다.
                float s = dir == Direction.Right ? 1f : -1f;
                c.Add("Left Upper Leg In-Out", p => 0.05f + g.legSwing * 0.5f * (0.5f + 0.5f * Mathf.Sin(p)));
                c.Add("Right Upper Leg In-Out", p => 0.05f + g.legSwing * 0.5f * (0.5f + 0.5f * Mathf.Sin(p)));
                c.Add("Left Lower Leg Stretch", p => Knee(g, Mathf.Cos(p) * s));
                c.Add("Right Lower Leg Stretch", p => Knee(g, -Mathf.Cos(p) * s));
                c.Add("Left Upper Leg Front-Back", p => 0.15f * Mathf.Max(0f, Mathf.Cos(p) * s));
                c.Add("Right Upper Leg Front-Back", p => 0.15f * Mathf.Max(0f, -Mathf.Cos(p) * s));
                c.Add("Spine Front-Back", p => g.lean * 0.6f + g.bounce * Mathf.Cos(2f * p));
                c.Add("Spine Left-Right", _ => -0.05f * s);
                AddRelaxedArms(c, p => 0.1f * g.armSwing * Mathf.Sin(p), g.forearm, g.armDown);
            }

            c.Add("Head Nod Down-Up", _ => -0.05f);
            return c.ToClip(true);
        }

        private static float MeasureBodyHeight()
        {
            var root = GameObject.Find("Survivor_Doctor");
            var anim = root != null ? root.GetComponentInChildren<Animator>() : null;
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman) return 1f;
            var handler = new HumanPoseHandler(anim.avatar, anim.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            handler.Dispose();
            baseMuscles.Clear();
            for (int i = 0; i < pose.muscles.Length; i++) baseMuscles[HumanTrait.MuscleName[i]] = pose.muscles[i];
            // A포즈 기준 높이. 걷기 때 살짝 낮아지도록 약간 줄인다.
            float scale = anim.humanScale * anim.transform.lossyScale.y;
            float h = (pose.bodyPosition.y - anim.transform.position.y) / Mathf.Max(scale, 1e-4f);
            Debug.Log($"[FE] bodyHeight={h:0.000} (raw {pose.bodyPosition.y:0.000}, humanScale {anim.humanScale:0.000})");
            return h > 0.2f && h < 3f ? h * 0.99f : 1f;
        }

        private static float Knee(Gait g, float swing)
        {
            // swing > 0 인 반주기에 무릎을 굽힌다. 1 = 쭉 폄, -1 = 최대로 굽힘
            float bend = Mathf.Max(0f, swing);
            return 0.9f - g.kneeBend * 2f * bend;
        }

        private static void AddRelaxedArms(CurveSet c, Func<float, float> leftFrontBack, float forearm, float armDown = 0f)
        {
            float down = armDown != 0f ? armDown : ArmDown;
            c.Add("Left Arm Down-Up", _ => down);
            c.Add("Right Arm Down-Up", _ => down);
            c.Add("Left Arm Front-Back", p => -0.05f + leftFrontBack(p));
            c.Add("Right Arm Front-Back", p => -0.05f - leftFrontBack(p));
            c.Add("Left Forearm Stretch", _ => forearm);
            c.Add("Right Forearm Stretch", _ => forearm);
            c.Add("Left Shoulder Down-Up", _ => -0.1f);
            c.Add("Right Shoulder Down-Up", _ => -0.1f);
            AddRightGrip(c);
        }

        // 오른손은 주사기 같은 소품을 쥔 손 모양으로 둔다.
        private const float RightGrip = 0.7f;

        private static void AddRightGrip(CurveSet c)
        {
            foreach (var finger in new[] { "Index", "Middle", "Ring", "Little" })
                for (int j = 1; j <= 3; j++)
                    c.Add($"RightHand.{finger}.{j} Stretched", _ => RightGrip);
            c.Add("RightHand.Thumb.1 Stretched", _ => RightGrip * 0.5f);
            c.Add("RightHand.Thumb.2 Stretched", _ => RightGrip * 0.5f);
            c.Add("RightHand.Thumb.3 Stretched", _ => RightGrip * 0.5f);
        }

        // ---------- 공격 모션 ----------
        // 시간은 0~1(정규화). 판정 구간 비율은 CharacterAttackAnimator의 값과 같다:
        //   약공격 준비 0~0.222, 판정 0.222~0.444, 후딜레이 0.444~1 (0.1/0.1/0.25초)
        //   강공격 준비 0~0.278, 판정 0.278~0.444, 후딜레이 0.444~1 (0.25/0.15/0.5초)
        public const float LightLength = 0.45f, HeavyLength = 0.9f, ChargeLength = 1.2f;

        /// <summary>(시간, 값) 쌍을 부드럽게 잇는 함수. 입력은 CurveSet의 위상(0~2π).</summary>
        private static Func<float, float> Keys(params float[] tv)
        {
            return p =>
            {
                float t = Mathf.Repeat(p / (Mathf.PI * 2f), 1.0001f);
                for (int i = 0; i + 3 < tv.Length; i += 2)
                {
                    float t0 = tv[i], v0 = tv[i + 1], t1 = tv[i + 2], v1 = tv[i + 3];
                    if (t <= t1) return Mathf.Lerp(v0, v1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(t0, t1, t)));
                }
                return tv[tv.Length - 1];
            };
        }

        private static void AddLeftArmGuard(CurveSet c)
        {
            c.Add("Left Arm Down-Up", _ => -0.45f);
            c.Add("Left Arm Front-Back", _ => -0.2f);
            c.Add("Left Forearm Stretch", _ => 0.4f);
            c.Add("Left Shoulder Down-Up", _ => -0.1f);
        }

        private static AnimationClip BuildLightAttack()
        {
            var c = new CurveSet(LightLength);
            // 오른손 찌르기: 뒤로 당겼다가(준비) 판정 구간 가운데(0.33)에 앞으로 쭉 뻗는다.
            c.Add("Right Arm Front-Back", Keys(0f, -0.05f, 0.2f, 0.4f, 0.33f, -0.95f, 0.45f, -0.85f, 1f, -0.05f)); // 이 아바타는 - 가 앞쪽
            c.Add("Right Arm Down-Up", Keys(0f, -0.62f, 0.2f, -0.45f, 0.33f, -0.3f, 0.45f, -0.32f, 1f, -0.62f));
            c.Add("Right Forearm Stretch", Keys(0f, 0.9f, 0.2f, -0.4f, 0.33f, 1f, 0.45f, 0.95f, 1f, 0.9f));
            c.Add("Right Shoulder Front-Back", Keys(0f, 0f, 0.2f, 0.3f, 0.33f, -0.6f, 0.45f, -0.5f, 1f, 0f));
            c.Add("Chest Twist Left-Right", Keys(0f, 0f, 0.2f, -0.35f, 0.33f, 0.35f, 0.45f, 0.3f, 1f, 0f));
            c.Add("Spine Front-Back", Keys(0f, 0.03f, 0.2f, 0f, 0.33f, 0.15f, 0.45f, 0.12f, 1f, 0.03f));
            c.Add("Head Nod Down-Up", _ => -0.05f);
            AddLeftArmGuard(c);
            AddRightGrip(c);
            return c.ToClip(false);
        }

        private static AnimationClip BuildHeavyCharge()
        {
            var c = new CurveSet(ChargeLength);
            // 오른손(소품)을 머리 위로 치켜들고 힘을 모은다. 살짝 떨림.
            c.Add("Right Arm Down-Up", p => 0.85f + 0.03f * Mathf.Sin(p * 3f));
            c.Add("Right Arm Front-Back", _ => -0.25f);
            c.Add("Right Forearm Stretch", p => 0.05f + 0.03f * Mathf.Sin(p * 3f + 1f));
            c.Add("Right Shoulder Down-Up", _ => 0.4f);
            c.Add("Chest Twist Left-Right", _ => -0.25f);
            c.Add("Spine Front-Back", p => -0.1f + 0.02f * Mathf.Sin(p));
            c.Add("Head Nod Down-Up", _ => -0.05f);
            AddLeftArmGuard(c);
            AddRightGrip(c);
            return c.ToClip(true);
        }

        private static AnimationClip BuildHeavyAttack()
        {
            var c = new CurveSet(HeavyLength);
            // 모은 자세에서 더 높이 들었다가(준비) 판정 구간 가운데(0.36)에 앞으로 내려친다.
            c.Add("Right Arm Down-Up", Keys(0f, 0.85f, 0.25f, 1f, 0.36f, -0.62f, 0.47f, -0.6f, 1f, -0.62f));
            c.Add("Right Arm Front-Back", Keys(0f, -0.25f, 0.25f, -0.1f, 0.36f, -0.4f, 0.47f, -0.38f, 1f, -0.05f));
            c.Add("Right Forearm Stretch", Keys(0f, 0.05f, 0.25f, -0.2f, 0.36f, 1f, 0.47f, 0.95f, 1f, 0.9f));
            c.Add("Right Shoulder Down-Up", Keys(0f, 0.4f, 0.25f, 0.6f, 0.36f, -0.2f, 0.47f, -0.2f, 1f, -0.1f));
            c.Add("Chest Twist Left-Right", Keys(0f, -0.25f, 0.25f, -0.4f, 0.36f, 0.35f, 0.47f, 0.3f, 1f, 0f));
            c.Add("Spine Front-Back", Keys(0f, -0.1f, 0.25f, -0.2f, 0.36f, 0.5f, 0.47f, 0.48f, 1f, 0.03f));
            c.Add("Chest Front-Back", Keys(0f, 0f, 0.25f, -0.15f, 0.36f, 0.3f, 0.47f, 0.28f, 1f, 0f));
            c.Add("Head Nod Down-Up", Keys(0f, -0.05f, 0.36f, -0.25f, 1f, -0.05f));
            AddLeftArmGuard(c);
            AddRightGrip(c);
            return c.ToClip(false);
        }

        // 공중 내려찍기: 준비 0~0.25(무릎을 가슴으로 당김), 판정 0.25~0.45(두 발을 아래로 쭉 뻗음), 후딜레이 0.45~1
        public const float AirStompLength = 0.6f, AirChargeLength = 1.0f;

        private static void AddAirArms(CurveSet c, Func<float, float> up)
        {
            foreach (var side in new[] { "Left", "Right" })
            {
                c.Add($"{side} Arm Down-Up", up);
                c.Add($"{side} Arm Front-Back", _ => 0.15f); // 살짝 뒤로(균형)
                c.Add($"{side} Forearm Stretch", _ => 0.5f);
            }
        }

        private static AnimationClip BuildAirStomp()
        {
            var c = new CurveSet(AirStompLength);
            // 다리 값은 기본 자세에 더하는 변화량(+ = 다리를 앞/위로, 무릎 Stretch - = 굽힘)
            foreach (var side in new[] { "Left", "Right" })
            {
                c.Add($"{side} Upper Leg Front-Back", Keys(0f, 0.15f, 0.22f, 0.45f, 0.33f, -0.05f, 0.45f, -0.05f, 1f, 0.15f));
                c.Add($"{side} Lower Leg Stretch", Keys(0f, -0.4f, 0.22f, -1.6f, 0.33f, 0f, 0.45f, 0f, 1f, -0.4f));
                c.Add($"{side} Foot Up-Down", Keys(0f, 0f, 0.22f, 0.3f, 0.33f, -0.4f, 0.45f, -0.4f, 1f, 0f));
            }
            c.Add("Spine Front-Back", Keys(0f, 0.1f, 0.22f, 0.35f, 0.33f, -0.05f, 0.45f, 0f, 1f, 0.1f));
            c.Add("Chest Front-Back", Keys(0f, 0f, 0.22f, 0.2f, 0.33f, -0.1f, 1f, 0f));
            c.Add("Head Nod Down-Up", Keys(0f, -0.1f, 0.33f, -0.35f, 1f, -0.1f));
            AddAirArms(c, Keys(0f, -0.2f, 0.22f, 0.25f, 0.33f, 0.45f, 0.45f, 0.4f, 1f, -0.2f));
            AddRightGrip(c);
            return c.ToClip(false);
        }

        private static AnimationClip BuildAirCharge()
        {
            var c = new CurveSet(AirChargeLength);
            // 공중에서 무릎을 바짝 당기고 팔을 들어 힘을 모은다.
            foreach (var side in new[] { "Left", "Right" })
            {
                c.Add($"{side} Upper Leg Front-Back", p => 0.4f + 0.03f * Mathf.Sin(p * 2f));
                c.Add($"{side} Lower Leg Stretch", _ => -1.5f);
                c.Add($"{side} Foot Up-Down", _ => 0.3f);
            }
            c.Add("Spine Front-Back", p => 0.3f + 0.02f * Mathf.Sin(p * 2f));
            c.Add("Head Nod Down-Up", _ => -0.25f);
            AddAirArms(c, p => 0.35f + 0.03f * Mathf.Sin(p * 3f));
            AddRightGrip(c);
            return c.ToClip(true);
        }

        private static void BuildUpperBodyMask(out AvatarMask mask)
        {
            string path = "Assets/Models/IdentityV/_Motions/UpperBodyMask.mask";
            mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, path); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                       || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                       || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            EditorUtility.SetDirty(mask);
        }

        // ---------- Animator Controller ----------

        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            // 기존 Controller가 있으면 지우지 않고 비워서 다시 채운다(GUID 유지 → 씬 연결이 끊기지 않음).
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
                    if (sub is BlendTree bt) UnityEngine.Object.DestroyImmediate(bt, true);
                while (ac.layers.Length > 1) ac.RemoveLayer(ac.layers.Length - 1);
            }
            ac.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ac.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            var ps = ac.parameters;
            for (int i = 0; i < ps.Length; i++) if (ps[i].name == "IsGrounded") ps[i].defaultBool = true;
            ac.parameters = ps;

            var sm = ac.layers[0].stateMachine;

            var locomotion = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            const float runR = Project1.Presentation.CharacterAnimatorDriver.RunBlendValue; // 드라이버가 달리기 속도를 이 값으로 맞춘다
            tree.AddChild(clips["Idle"], Vector2.zero);
            tree.AddChild(clips["Walk_F"], new Vector2(0, 1));
            tree.AddChild(clips["Walk_B"], new Vector2(0, -1));
            tree.AddChild(clips["Walk_L"], new Vector2(-1, 0));
            tree.AddChild(clips["Walk_R"], new Vector2(1, 0));
            tree.AddChild(clips["Run_F"], new Vector2(0, runR));
            tree.AddChild(clips["Run_B"], new Vector2(0, -runR));
            tree.AddChild(clips["Run_L"], new Vector2(-runR, 0));
            tree.AddChild(clips["Run_R"], new Vector2(runR, 0));
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

            // 공격 레이어(상체만). 상태 전환과 재생 위치는 CharacterAttackAnimator가 코드로 정한다.
            BuildUpperBodyMask(out var mask);
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

            EditorUtility.SetDirty(ac);
        }

        // ---------- 유틸 ----------

        private static AnimationClip Save(string name, AnimationClip clip)
        {
            string path = $"{Folder}/{name}.anim";
            clip.name = name;
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        /// <summary>한 주기(0~2π)를 받는 함수들로 근육 커브를 만든다.</summary>
        private class CurveSet
        {
            private readonly float duration;
            private readonly List<(string muscle, Func<float, float> f)> items = new();

            public float Bob;

            public CurveSet(float duration, float bob = 0f) { this.duration = duration; Bob = bob; }

            private void SetConst(AnimationClip clip, string prop, float v) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), AnimationCurve.Constant(0f, duration, v));

            public void Add(string muscle, Func<float, float> f) => items.Add((muscle, f));

            public AnimationClip ToClip(bool loop)
            {
                var clip = new AnimationClip { frameRate = 30 };
                // 몸 위치·방향: 제자리에 서 있게 고정하고 위아래 출렁임만 준다.
                SetConst(clip, "RootT.x", 0f); SetConst(clip, "RootT.z", 0f);
                SetConst(clip, "RootQ.x", 0f); SetConst(clip, "RootQ.y", 0f); SetConst(clip, "RootQ.z", 0f); SetConst(clip, "RootQ.w", 1f);
                var bob = new Keyframe[Samples + 1];
                for (int i = 0; i <= Samples; i++) { float t = (float)i / Samples; bob[i] = new Keyframe(t * duration, bodyHeight + Bob * Mathf.Cos(t * Mathf.PI * 4f)); }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), new AnimationCurve(bob));
                foreach (var (muscle, f) in items)
                {
                    var keys = new Keyframe[Samples + 1];
                    for (int i = 0; i <= Samples; i++)
                    {
                        float t = (float)i / Samples;
                        keys[i] = new Keyframe(t * duration, Mathf.Clamp(Base(muscle) + f(t * Mathf.PI * 2f), -1f, 1f));
                    }
                    var curve = new AnimationCurve(keys);
                    for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
                }
                // 따로 움직이지 않는 다리·몸통·머리 근육은 기본 자세 값으로 고정한다.
                var used = new HashSet<string>();
                foreach (var (m, _) in items) used.Add(m);
                foreach (var kv in baseMuscles)
                {
                    if (used.Contains(kv.Key) || IsAbsolute(kv.Key) || kv.Key.Contains("Hand") || kv.Key.Contains("Finger") || kv.Key.Contains(" Index") || kv.Key.Contains(" Middle") || kv.Key.Contains(" Ring") || kv.Key.Contains(" Little") || kv.Key.Contains(" Thumb")) continue;
                    SetConst(clip, kv.Key, kv.Value);
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                return clip;
            }
        }
    }
}

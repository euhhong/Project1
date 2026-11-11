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
        }

        private static readonly Gait Walk = new Gait { duration = 1.0f, legSwing = 0.4f, kneeBend = 0.35f, armSwing = 0.22f, forearm = 0.8f, lean = 0.05f, bounce = 0.05f };
        private static readonly Gait Run = new Gait { duration = 0.62f, legSwing = 0.6f, kneeBend = 0.55f, armSwing = 0.4f, forearm = 0.15f, lean = 0.2f, bounce = 0.1f };

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
                AddRelaxedArms(c, p => -g.armSwing * Mathf.Sin(phase(p)), g.forearm);
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
                AddRelaxedArms(c, p => 0.1f * g.armSwing * Mathf.Sin(p), g.forearm);
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

        private static void AddRelaxedArms(CurveSet c, Func<float, float> leftFrontBack, float forearm)
        {
            c.Add("Left Arm Down-Up", _ => ArmDown);
            c.Add("Right Arm Down-Up", _ => ArmDown);
            c.Add("Left Arm Front-Back", p => -0.05f + leftFrontBack(p));
            c.Add("Right Arm Front-Back", p => -0.05f - leftFrontBack(p));
            c.Add("Left Forearm Stretch", _ => forearm);
            c.Add("Right Forearm Stretch", _ => forearm);
            c.Add("Left Shoulder Down-Up", _ => -0.1f);
            c.Add("Right Shoulder Down-Up", _ => -0.1f);
        }

        // ---------- Animator Controller ----------

        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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
            const float runR = 2.5f; // 달리기 5 m/s / 걷기 2 m/s
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

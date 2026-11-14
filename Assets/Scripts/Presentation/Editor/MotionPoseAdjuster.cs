using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 받은 모션 파일(Mixamo)을 바탕으로 자세만 조금 고친 모션을 만든다. 모션을 처음부터 만들지 않는다.
    /// - Add: 원래 근육 값에 더한다(예: 무릎을 더 굽힘). Set: 그 근육을 일정한 값으로 고정한다(예: 두 손으로 공을 듦).
    /// - RootY: 몸 높이(Humanoid 단위)를 더한다. - = 낮게.
    /// 결과: `_Motions/Adjusted/<자리>.anim`. CharacterMotionControllerBuilder는 같은 자리에 이 파일이 있으면 원본 대신 쓴다.
    /// 메뉴: Tools > FE > 모션 자세 고치기. 원본 FBX를 다시 받으면 이 메뉴를 다시 누르면 된다(Mixamo 임포트 때도 자동).
    /// </summary>
    public static class MotionPoseAdjuster
    {
        public const string Folder = "Assets/Models/IdentityV/_Motions/Adjusted";

        public class Adjustment
        {
            public string slot;
            public float rootY;
            public float rootPitch;
            public Dictionary<string, float> add = new();
            public Dictionary<string, float> set = new();
            /// <summary>그 근육의 흔들림(곡선 평균에서 벗어난 만큼)을 이 배율로 키운다(-1~1로 자름). 예: 사다리 오르기 보폭을 넓힘.</summary>
            public Dictionary<string, float> amplify = new();
        }

        // 전위 럭비공 돌진: 무게중심을 낮추고(무릎을 더 굽히고 상체를 앞으로 숙임), 두 손으로 가슴 앞에 공을 안는다.
        // 사다리 오르기(생존자 Climbing Ladder / 감시자 Climbing): 팔·다리를 더 크게 뻗고 당겨 한 사이클에 큰 칸(1.2m)을 오르는 것처럼 보이게.
        public static readonly Adjustment[] Adjustments =
        {
            new Adjustment { slot = "Traversal_Ladder", amplify = LadderStride.Amplify },
            new Adjustment { slot = "Traversal_LadderHunter", amplify = LadderStride.Amplify },
            new Adjustment
            {
                slot = "Skill_Forward_Charge",
                rootY = ForwardCharge.RootY,
                rootPitch = ForwardCharge.RootPitch,
                add = ForwardCharge.Add,
                set = ForwardCharge.Set,
            },
        };

        /// <summary>사다리 보폭: 손을 더 높이 뻗고 더 아래까지 당기고, 무릎을 더 높이 올렸다가 더 쭉 편다. 배율을 올리면 보폭이 더 커진다.</summary>
        public static class LadderStride
        {
            public const float Arm = 1.6f, Leg = 1.6f;
            public static Dictionary<string, float> Amplify = new()
            {
                ["Left Shoulder Down-Up"] = Arm, ["Right Shoulder Down-Up"] = Arm,
                ["Left Arm Down-Up"] = Arm, ["Right Arm Down-Up"] = Arm,
                ["Left Arm Front-Back"] = Arm, ["Right Arm Front-Back"] = Arm,
                ["Left Forearm Stretch"] = Arm, ["Right Forearm Stretch"] = Arm,
                ["Left Upper Leg Front-Back"] = Leg, ["Right Upper Leg Front-Back"] = Leg,
                ["Left Lower Leg Stretch"] = Leg, ["Right Lower Leg Stretch"] = Leg,
                ["Left Foot Up-Down"] = Leg, ["Right Foot Up-Down"] = Leg,
            };
        }

        // 값은 의사 모델이 아니라 전위 모델로 재서 맞췄다(2026-11-12): 골반 0.213 → 0.170(약 20% 낮게), 발은 땅 높이 유지,
        // 몸 앞으로 약 15도, 두 손은 가슴 앞으로 모은다. 이 아바타의 팔 Front-Back은 - 가 앞쪽이다.
        public static class ForwardCharge
        {
            public static float RootY = -0.18f;     // 몸을 낮춘다
            public static float RootPitch = 18f;    // 몸 전체를 앞으로 숙인다(도)
            public static Dictionary<string, float> Add = new()
            {
                // 몸을 낮춘 만큼 발 IK 목표를 올려 발이 땅에 붙게 한다(무릎은 Foot IK가 굽힌다).
                ["LeftFootT.y"] = 0.18f, ["RightFootT.y"] = 0.18f,
            };
            public static Dictionary<string, float> Set = new()
            {
                // 두 손으로 가슴 앞에 럭비공을 안는다(팔 흔들기 없음).
                ["Left Arm Front-Back"] = -0.45f, ["Right Arm Front-Back"] = -0.45f,
                ["Left Arm Down-Up"] = -0.1f, ["Right Arm Down-Up"] = -0.1f,
                ["Left Forearm Stretch"] = -0.3f, ["Right Forearm Stretch"] = -0.3f,
                ["Left Arm Twist In-Out"] = 0f, ["Right Arm Twist In-Out"] = 0f,
                ["Left Forearm Twist In-Out"] = 0f, ["Right Forearm Twist In-Out"] = 0f,
                ["Left Shoulder Down-Up"] = 0f, ["Right Shoulder Down-Up"] = 0f,
                ["Left Hand Down-Up"] = 0f, ["Right Hand Down-Up"] = 0f,
                ["Left Hand In-Out"] = 0f, ["Right Hand In-Out"] = 0f,
            };
        }

        [MenuItem("Tools/FE/모션 자세 고치기")]
        public static void BuildAll()
        {
            foreach (var a in Adjustments) Build(a);
            AssetDatabase.SaveAssets();
        }

        public static AnimationClip Build(Adjustment a)
        {
            var source = LoadSource(a.slot);
            if (source == null) { Debug.LogWarning($"[FE] 자세 고치기: 원본 모션 {a.slot}이 없습니다."); return null; }
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Models/IdentityV/_Motions", "Adjusted");

            var clip = new AnimationClip { name = a.slot, frameRate = source.frameRate };
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                var curve = AnimationUtility.GetEditorCurve(source, binding);
                string p = binding.propertyName;
                if (a.set.TryGetValue(p, out float value))
                {
                    curve = AnimationCurve.Constant(0f, source.length, value);
                }
                else if (a.amplify.TryGetValue(p, out float gain))
                {
                    var keys = curve.keys;
                    float mean = 0f;
                    foreach (var k in keys) mean += k.value;
                    mean /= Mathf.Max(1, keys.Length);
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value = Mathf.Clamp(mean + (keys[i].value - mean) * gain, -1f, 1f);
                        keys[i].inTangent *= gain; keys[i].outTangent *= gain;
                    }
                    curve.keys = keys;
                }
                else
                {
                    float delta = a.add.TryGetValue(p, out float d) ? d : (p == "RootT.y" ? a.rootY : 0f);
                    if (delta != 0f)
                    {
                        var keys = curve.keys;
                        for (int i = 0; i < keys.Length; i++) keys[i].value += delta;
                        curve.keys = keys;
                    }
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            if (a.rootPitch != 0f) PitchRoot(clip, Quaternion.Euler(a.rootPitch, 0f, 0f));
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(source));

            string path = $"{Folder}/{a.slot}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); return existing; }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        /// <summary>몸(Root) 회전에 앞뒤 기울기를 곱한다. 네 성분 키는 같은 시간에 있다(Mixamo는 프레임마다 키).</summary>
        private static void PitchRoot(AnimationClip clip, Quaternion pitch)
        {
            var b = new EditorCurveBinding[4];
            var c = new AnimationCurve[4];
            string[] axes = { "x", "y", "z", "w" };
            for (int i = 0; i < 4; i++)
            {
                b[i] = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axes[i]);
                c[i] = AnimationUtility.GetEditorCurve(clip, b[i]);
                if (c[i] == null) return;
            }
            var keys = new Keyframe[4][];
            for (int i = 0; i < 4; i++) keys[i] = c[i].keys;
            for (int k = 0; k < keys[0].Length; k++)
            {
                float t = keys[0][k].time;
                var q = new Quaternion(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t)) * pitch;
                float[] v = { q.x, q.y, q.z, q.w };
                for (int i = 0; i < 4; i++) if (k < keys[i].Length) keys[i][k].value = v[i];
            }
            for (int i = 0; i < 4; i++) { c[i].keys = keys[i]; AnimationUtility.SetEditorCurve(clip, b[i], c[i]); }
        }

        private static AnimationClip LoadSource(string slot)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath($"{MixamoMotionPostprocessor.Folder}/{slot}.fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }
    }
}

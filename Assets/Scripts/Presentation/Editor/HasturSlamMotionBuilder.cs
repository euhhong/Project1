using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 하스터 공격 모션(두 손을 모아 머리 위로 들었다가 위에서 아래로 내려치기)을 만든다.
    /// 하스터 모델(Hunter_Hastur)은 사람 모양(Humanoid)이 아닌 원본 뼈대(Generic) 리그라 Mixamo 모션이 재생되지 않는다.
    /// 그래서 모델 뼈(biped Spine1·Neck·Head·Clavicle·UpperArm·Forearm·Hand)에 직접 키를 넣은 Generic 모션을 만든다:
    /// 자세마다 몸통 기울기·어깨 들기·두 손 목표 위치를 정하고, 팔은 두 마디 IK(어깨 → 팔꿈치 → 손)로 손을 목표에 보낸 뒤 뼈 회전을 기록한다.
    /// 결과: `_Motions/Generic/Attack_Light_HasturSlam.anim`. HasturLocomotion의 약공격·강공격·달리기 점프 공격 자리에 들어간다
    /// (CharacterMotionControllerBuilder 표 + 이 메뉴가 바로 넣음). 판정 타이밍은 CharacterAttackAnimator 표(Attack_Light_HasturSlam).
    /// 메뉴: Tools > FE > 하스터 내려치기 모션 만들기.
    /// </summary>
    public static class HasturSlamMotionBuilder
    {
        public const string Folder = "Assets/Models/IdentityV/_Motions/Generic";
        public const string ClipName = "Attack_Light_HasturSlam";
        private const string ModelPath = "Assets/Models/IdentityV/Hunter_Hastur/Hunter_Hastur.fbx";
        private const string OverridePath = "Assets/Models/IdentityV/_Motions/HasturLocomotion.overrideController";
        private const float Length = 1f;

        // 준비 끝(두 손이 머리 위 꼭대기) / 판정 끝(두 손이 몸 앞 아래까지 내려옴) — CharacterAttackAnimator 표와 같게.
        public const float WindupEnd = 0.42f, ActiveEnd = 0.6f;

        /// <summary>한 자세. 위치는 모델 루트 기준(m, 모델 크기 1 — 키 약 0.57). x = 오른쪽, y = 위, z = 앞.</summary>
        private struct Key
        {
            public float t;            // 0~1
            public bool rest;          // 쉬는 자세 그대로
            public float bend;         // 몸통 앞 숙임(도, 허리 Spine 60% + 가슴 Spine1 40%), - = 뒤로 젖힘
            public float head;         // 머리 앞 숙임(도, 몸통 위에 더함)
            public float shoulder;     // 어깨(Clavicle) 들기(도)
            public Vector3 hand;       // 오른손(소매 끝) 목표(왼손은 x를 뒤집음)
            public Vector3 elbow;      // 팔꿈치가 향할 쪽(오른팔 기준)
            public Vector3 claw;       // 두 촉수 팔 끝(손톱)이 모이는 쪽 — 두 아래팔 사이에서 이 방향으로, 끝이 서로 맞닿는 자리
        }

        private static readonly Key[] Keys =
        {
            new Key { t = 0f, rest = true },
            // 두 손을 가슴 앞으로 모으며 들기 시작(손톱은 앞 위로)
            new Key { t = 0.18f, bend = -4f, head = -4f, shoulder = 8f, hand = new Vector3(0.04f, 0.44f, 0.1f), elbow = new Vector3(1f, -0.6f, 0f), claw = new Vector3(0f, 0.6f, 1f) },
            // 꼭대기: 두 손을 맞잡아 머리 위로(손톱은 하늘로, 살짝 뒤로), 몸을 뒤로 젖히고 위를 봄
            new Key { t = WindupEnd, bend = -12f, head = -8f, shoulder = 24f, hand = new Vector3(0.02f, 0.62f, -0.01f), elbow = new Vector3(1f, 0.2f, 0.5f), claw = new Vector3(0f, 1f, -0.25f) },
            // 내려치기 시작: 머리 위에서 앞으로 넘어옴
            new Key { t = 0.47f, bend = 4f, head = -6f, shoulder = 18f, hand = new Vector3(0.02f, 0.58f, 0.12f), elbow = new Vector3(1f, 0f, 0.3f), claw = new Vector3(0f, 0.7f, 0.7f) },
            // 맞는 순간: 손톱이 몸 앞을 지나며 몸을 숙임
            new Key { t = 0.53f, bend = 26f, head = -16f, shoulder = 6f, hand = new Vector3(0.03f, 0.38f, 0.2f), elbow = new Vector3(1f, -0.3f, 0f), claw = new Vector3(0f, -0.15f, 1f) },
            // 바닥 쪽까지 내려침(판정 끝)
            new Key { t = ActiveEnd, bend = 40f, head = -26f, shoulder = 0f, hand = new Vector3(0.04f, 0.24f, 0.2f), elbow = new Vector3(1f, -0.2f, -0.3f), claw = new Vector3(0f, -0.7f, 1f) },
            // 잠깐 버팀(살짝 튕겨 오름)
            new Key { t = 0.75f, bend = 34f, head = -22f, shoulder = 2f, hand = new Vector3(0.05f, 0.26f, 0.19f), elbow = new Vector3(1f, -0.3f, -0.3f), claw = new Vector3(0f, -0.62f, 1f) },
            new Key { t = 1f, rest = true },
        };

        // 촉수 팔(소매 아래로 땅까지 닿는 검은 팔 — 아래팔에 붙음, HasturArmWalk와 같은 사슬)의 밑동 뼈. 밑동만 돌려 사슬 모양은 그대로 둔다.
        private static readonly string[] Bones =
        {
            "biped Spine", "biped Spine1", "biped Neck", "biped Head",
            "biped L Clavicle", "biped L UpperArm", "biped L Forearm", "biped L Hand", "biped_bone_00",
            "biped R Clavicle", "biped R UpperArm", "biped R Forearm", "biped R Hand", "biped_bone_27",
        };
        private const int LeftArm = 4, RightArm = 9; // Bones에서 각 팔(Clavicle부터 5개)이 시작하는 자리


        [MenuItem("Tools/FE/하스터 내려치기 모션 만들기")]
        public static void BuildMenu()
        {
            var clip = Build();
            if (clip != null) ApplyToOverride(clip);
            AssetDatabase.SaveAssets();
        }

        public static AnimationClip Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null) { Debug.LogWarning($"[FE] 하스터 모델이 없습니다: {ModelPath}"); return null; }
            var go = Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var root = go.transform;
                root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.localScale = Vector3.one;
                var bones = new Transform[Bones.Length];
                var rest = new Quaternion[Bones.Length];
                var all = go.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < Bones.Length; i++)
                {
                    foreach (var t in all) if (t.name == Bones[i]) { bones[i] = t; break; }
                    if (bones[i] == null) { Debug.LogWarning($"[FE] 하스터 뼈 {Bones[i]}가 없습니다."); return null; }
                    rest[i] = bones[i].localRotation;
                }

                var samples = new Quaternion[Keys.Length][];
                for (int k = 0; k < Keys.Length; k++)
                {
                    for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rest[i];
                    if (!Keys[k].rest) Pose(root, bones, Keys[k]);
                    samples[k] = new Quaternion[bones.Length];
                    for (int i = 0; i < bones.Length; i++) samples[k][i] = bones[i].localRotation;
                }

                var clip = new AnimationClip { name = ClipName, frameRate = 30f };
                for (int i = 0; i < bones.Length; i++)
                {
                    string path = AnimationUtility.CalculateTransformPath(bones[i], root);
                    var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                    Quaternion prev = samples[0][i];
                    for (int k = 0; k < Keys.Length; k++)
                    {
                        var q = samples[k][i];
                        if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // 같은 쪽 반구(사이를 짧게 돌게)
                        prev = q;
                        float time = Keys[k].t * Length;
                        curves[0].AddKey(time, q.x); curves[1].AddKey(time, q.y); curves[2].AddKey(time, q.z); curves[3].AddKey(time, q.w);
                    }
                    string[] axes = { "x", "y", "z", "w" };
                    for (int c = 0; c < 4; c++)
                    {
                        for (int k = 0; k < curves[c].length; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curves[c], k, AnimationUtility.TangentMode.ClampedAuto);
                            AnimationUtility.SetKeyRightTangentMode(curves[c], k, AnimationUtility.TangentMode.ClampedAuto);
                        }
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axes[c]), curves[c]);
                    }
                }
                clip.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Models/IdentityV/_Motions", "Generic");
                string assetPath = $"{Folder}/{ClipName}.anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (existing != null) { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); clip = existing; }
                else AssetDatabase.CreateAsset(clip, assetPath);
                EditorUtility.SetDirty(clip);
                return clip;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>HasturLocomotion의 약공격·강공격·달리기 점프 공격 자리를 이 모션으로 바꾼다(컨트롤러 전체를 다시 만들지 않음).</summary>
        public static void ApplyToOverride(AnimationClip clip)
        {
            var aoc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
            if (aoc == null) return;
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
                if (pairs[i].Key != null && System.Array.IndexOf(CharacterMotionControllerBuilder.HasturSlamSlots, pairs[i].Key.name) >= 0)
                    pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
            aoc.ApplyOverrides(pairs);
            EditorUtility.SetDirty(aoc);
        }

        private static void Pose(Transform root, Transform[] b, Key k)
        {
            Vector3 right = root.right, up = root.up;
            // 몸통 → 머리 → 어깨 → 팔 → 촉수 팔 순서(부모부터).
            b[0].rotation = Quaternion.AngleAxis(k.bend * 0.6f, right) * b[0].rotation;
            b[1].rotation = Quaternion.AngleAxis(k.bend * 0.4f, right) * b[1].rotation;
            b[3].rotation = Quaternion.AngleAxis(k.head, right) * b[3].rotation;
            for (int side = 0; side < 2; side++) // 0 = 왼쪽, 1 = 오른쪽
            {
                int o = side == 0 ? LeftArm : RightArm;
                float sx = side == 0 ? -1f : 1f;
                Transform clav = b[o], upper = b[o + 1], fore = b[o + 2], hand = b[o + 3];
                Vector3 along = upper.position - clav.position;
                Vector3 axis = Vector3.Cross(along, up);
                if (axis.sqrMagnitude > 1e-10f) clav.rotation = Quaternion.AngleAxis(k.shoulder, axis.normalized) * clav.rotation;
                Vector3 target = root.TransformPoint(new Vector3(k.hand.x * sx, k.hand.y, k.hand.z));
                Vector3 pole = root.TransformDirection(new Vector3(k.elbow.x * sx, k.elbow.y, k.elbow.z));
                SolveArm(upper, fore, hand, target, pole);
            }

            // 두 촉수 팔 끝(손톱)을 한데 모은다: 두 밑동 사이 가운데에서 claw 방향으로, 밑동 → 끝 길이만큼 떨어진 자리(양쪽이 맞닿음).
            Transform rootL = b[LeftArm + 4], rootR = b[RightArm + 4];
            Vector3 mid = (rootL.position + rootR.position) * 0.5f;
            Vector3 dir = root.TransformDirection(k.claw).normalized;
            foreach (var (chain, sx) in new[] { (rootL, -1f), (rootR, 1f) })
            {
                var tip = Tip(chain);
                float len = Vector3.Distance(chain.position, tip.position);
                Vector3 rel = mid - chain.position;
                float bq = Vector3.Dot(rel, dir), cq = rel.sqrMagnitude - len * len;
                float s = -bq + Mathf.Sqrt(Mathf.Max(bq * bq - cq, 0f));
                Vector3 goal = mid + dir * s + right * (sx * 0.012f); // 서로 파고들지 않게 살짝 떨어뜨림
                Aim(chain, tip.position, goal);
            }
        }

        private static Transform Tip(Transform t)
        {
            while (t.childCount > 0) t = t.GetChild(0);
            return t;
        }

        /// <summary>두 마디 IK: 손이 target에 오게 위팔·아래팔을 돌린다. 팔꿈치는 pole 쪽으로 굽는다. 닿지 않으면 그쪽으로 곧게 뻗는다.</summary>
        private static void SolveArm(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float c = Vector3.Distance(fore.position, hand.position);
            Vector3 s = upper.position;
            Vector3 d = target - s;
            float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(a - c) + 1e-4f, (a + c) * 0.999f);
            Vector3 dir = d.normalized;
            float cosA = Mathf.Clamp((a * a + dist * dist - c * c) / (2f * a * dist), -1f, 1f);
            Vector3 bend = Vector3.ProjectOnPlane(pole, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            bend.Normalize();
            Vector3 elbow = s + dir * (a * cosA) + bend * (a * Mathf.Sqrt(1f - cosA * cosA));
            Aim(upper, fore.position, elbow);
            Aim(fore, hand.position, s + dir * dist);
        }

        private static void Aim(Transform bone, Vector3 childPos, Vector3 goal)
        {
            Vector3 from = childPos - bone.position, to = goal - bone.position;
            if (from.sqrMagnitude < 1e-10f || to.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(from, to) * bone.rotation;
        }
    }
}

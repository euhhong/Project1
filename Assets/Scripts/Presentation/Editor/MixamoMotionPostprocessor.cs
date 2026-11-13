using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// `_Motions/Mixamo/` 폴더에 넣은 모션 FBX를 Humanoid 모션으로 자동 임포트한다.
    /// - 파일 이름이 곧 모션 자리 이름이다(예: Attack_Light.fbx). 클립 이름도 파일 이름으로 맞춘다.
    /// - 반복 모션(대기·걷기·달리기·공중·모으기)은 Loop를 켠다.
    /// - 루트 회전·높이·수평 이동을 모두 포즈에 굽는다. 이동은 BE CharacterMotor가 하기 때문이다.
    /// - 필요한 모션만 잘라 쓴다(Trims). 단위는 원본 FBX의 프레임(30fps).
    /// - 뼈가 뒤틀리지 않게: Humanoid 근육 값을 모두 정해진 범위(-1~1, 아바타 관절 한계) 안으로 자른다(ClampMuscles).
    ///   Mixamo 모션은 손가락 벌림·발 비틀기·어깨가 한계를 몇 배씩 넘는 것이 있어(예: Jumping (1) 손가락 12배) 그대로면 손·발·어깨가 꼬여 모델이 일그러진다.
    /// </summary>
    public class MixamoMotionPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/Models/IdentityV/_Motions/Mixamo";

        /// <summary>근육 값 한계(Humanoid 근육 1 = 아바타 관절 한계 끝). 조금 넘는 것은 자연스러워 그대로 둔다.</summary>
        private const float MuscleLimit = 1f;

        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsMotion) return;
            int clamped = ClampMuscles(clip);
            if (clamped > 0) Debug.Log($"[FE] {clip.name}: 관절 한계를 넘은 근육 키 {clamped}개를 한계 안으로 잘랐습니다(뼈 뒤틀림 막기).");
        }

        /// <summary>
        /// Humanoid 클립의 근육 곡선 키를 -MuscleLimit~MuscleLimit로 자른다. 루트·IK 목표(RootT/RootQ, …T.x, …Q.w 등)는 건드리지 않는다. 자른 키 수를 돌려준다.
        /// </summary>
        public static int ClampMuscles(AnimationClip clip)
        {
            int count = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Animator) || !IsMuscleCurve(binding.propertyName)) continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null) continue;
                var keys = curve.keys;
                bool changed = false;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (Mathf.Abs(keys[i].value) <= MuscleLimit) continue;
                    keys[i].value = Mathf.Clamp(keys[i].value, -MuscleLimit, MuscleLimit);
                    changed = true;
                    count++;
                }
                if (!changed && !Overshoots(curve)) continue;
                curve.keys = keys;
                // 키 사이 곡선이 키 값을 넘어 출렁이지 않게(ClampedAuto = 이웃 키 사이를 넘지 않는 부드러운 곡선) — 한계 밖으로 새지 않는다.
                for (int i = 0; i < keys.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
            return count;
        }

        /// <summary>키는 한계 안인데 키 사이 곡선이 한계 밖으로 출렁이는지.</summary>
        private static bool Overshoots(AnimationCurve curve)
        {
            int n = curve.length;
            if (n < 2) return false;
            float t0 = curve.keys[0].time, t1 = curve.keys[n - 1].time;
            int samples = Mathf.Max(60, n * 4);
            for (int s = 0; s <= samples; s++)
                if (Mathf.Abs(curve.Evaluate(Mathf.Lerp(t0, t1, s / (float)samples))) > MuscleLimit) return true;
            return false;
        }

        private static bool IsMuscleCurve(string property)
        {
            if (property.StartsWith("Root") || property.StartsWith("Motion")) return false;                 // RootT.x, RootQ.w, MotionT …
            int dot = property.LastIndexOf('.');
            if (dot > 0 && dot == property.Length - 2 && (property[dot - 1] == 'T' || property[dot - 1] == 'Q')) return false; // LeftFootT.x, LeftHandQ.w(IK 목표)
            return true; // 근육(예: "Spine Front-Back", "LeftHand.Middle.Spread")
        }

        private static readonly HashSet<string> Looping = new()
        {
            "Idle", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
            "Attack_HeavyCharge", "Attack_AirCharge", "Block", "Idle_Thief", "Idle_Luchino", "Idle_HellEmber", "Idle_Antonio", "Move_Hastur", "SelectIdle_Hunter", "SelectIdle_Survivor", "Skill_Forward_Charge", "Stun", "Stairs_Up", "Carry_Joker", "Skill_SmileyFace_RocketCharge", "Skill_SmileyFace_RocketCharge_Raise", "Skill_Thief_Flashlight",
            "Traversal_Hang", "Traversal_HangBraced", "Traversal_FallBack", "Traversal_Push", // 매달린 채 대기, 허우적대며 떨어짐, 물건 밀기
        };
        // Air(Standing Jump)는 반복하지 않는다: 도약 → 착지 직전 자세에서 멈추고, 땅에 닿으면 이동으로 돌아간다.

        // 좌우를 뒤집어 쓰는 모션(왼손 동작을 BE 판정 손인 오른손으로).
        private static readonly HashSet<string> Mirrored = new(); // 예전 Boxing(왼손 잽)은 뒤집어 썼다. Mutant Punch는 오른손이라 그대로.

        // 원본에서 쓸 구간(프레임). 없으면 전체를 쓴다.
        // 판정 비율은 CharacterAttackAnimator의 Timing 표.
        // Attack_RunJump_Survivor: Flying Kick 도약~착지(0~30).
        private static readonly Dictionary<string, Vector2> Trims = new()
        {
            ["Attack_Light"] = new Vector2(2, 30),             // Mutant Punch(생존자 기본 공격): 오른손 훅 6~11
            ["Attack_Light_HookPunch"] = new Vector2(14, 56),  // Hook Punch(빈손 생존자 — 용병·도둑 기본 공격): 오른손 뒤로 당김(14~30) → 훅이 앞으로 꽂힘(30~36) → 되돌리기
            ["Attack_RunJump_Survivor"] = new Vector2(0, 30),
            ["Attack_RunJump2_Survivor"] = new Vector2(0, 30), // 두 번째 날아 차기(생존자, Flying Kick 다른 판): 뛰어오르며 왼발을 머리 높이로 뻗음(4~16) → 착지(~23) → 일어섬
            ["Attack_Light_Hunter"] = new Vector2(6, 50),      // Standing Melee Attack Downward: 오른손 들기 → 내려치기(20~28) → 몸 되돌리기
            ["Attack_Light_Luchino"] = new Vector2(4, 60),     // Standing Melee Attack Horizontal: 오른쪽 뒤로 당김 → 가로 베기(22~29) → 몸 되돌리기
            ["Attack_Light_Geisha"] = new Vector2(7, 61),      // Standing Torch Melee Attack Stab을 Blender로 좌우 뒤집음(Tools/Blender/mirror_motion.py, 프레임 1 밀림): 오른팔로 길게 찌르기
            ["Attack_Light_Hastur"] = new Vector2(19, 64),     // Zombie Attack: 두 손 높이 들기(19~41) → 앞으로 내려찍기(41~49) → 거두기
            ["Attack_Light_MindsEye"] = new Vector2(10, 48),   // Standing Melee Combo Attack Ver. 3: 첫 타 가로 베기(24~31) → 한 바퀴 돌아 앞을 봄(48). 두 번째 타는 뺌
            ["Attack_SwipeL"] = new Vector2(22, 42),          // 맹인 왼쪽 후리기(Melee Combo Attack Ver. 3 첫 타만): 오른쪽 뒤 → 앞(29~32) → 왼쪽
            ["Attack_SwipeR"] = new Vector2(23, 43),          // 맹인 오른쪽 후리기(위를 Blender로 거꾸로, 프레임 1 밀림): 왼쪽 → 앞(31~34) → 오른쪽
            ["Attack_RunJump_Hunter"] = new Vector2(36, 70),   // 무기를 든 자세 → 내려치기 → 마무리(앞의 달리기·마지막 대기는 뺌)
            // 광대: 무조건 두 손으로 머리 위에서 아래로 내려치기(같은 Run Jump Attack의 내려치기 부분, 상체만)
            ["Attack_Light_Joker"] = new Vector2(36, 70),
            ["Attack_Heavy_Joker"] = new Vector2(36, 70),
            // 리퍼: 감시자 공격을 Blender로 좌우 뒤집어 왼손(손 자체가 무기)으로(Tools/Blender/mirror_motion.py, 프레임 1 밀림)
            ["Attack_Light_Ripper"] = new Vector2(7, 51),
            ["Attack_Heavy_Ripper"] = new Vector2(7, 51),
            ["Attack_RunJump_Ripper"] = new Vector2(37, 71),
            ["Stun_Back"] = new Vector2(62, 138),              // 뒤에서 맞아 기절(Turn To Knocked Unconscious): 가만히 선 앞부분(0~47)·뒤돌기 앞부분은 빼고 휘청이며 돎(62~84) → 쓰러짐(88) → 누운 채(~138)
            ["Hit_HeadKick"] = new Vector2(19, 76),           // 헤드샷 킥에 맞음(Corkscrew Evade): 뒤로 회오리치며 뛰어오름(19~34) → 땅에 떨어짐(43) → 엎드림 → 일어섬(76)
            ["Air"] = new Vector2(0, 22),                     // Jump (1)(제자리 점프): 바로 도약(0) → 꼭대기(10) → 착지(22)
            ["Air_F"] = new Vector2(25, 44),                   // Jumping(걸어서 점프): 발이 떨어짐(25) → 꼭대기(34) → 착지(44)
            ["Air_Run"] = new Vector2(0, 20),                  // Running Jump(뛰면서 점프): 도약(0) → 꼭대기(10) → 착지(20)
        };

        // 공중에서 쓰는 모션은 모션 자체의 뛰어오름을 빼서 버린다. 높이는 게임의 점프 궤적이 정한다.
        private static readonly HashSet<string> NoRootHeight = new() { "Air", "Air_F", "Air_Run", "Attack_RunJump_Survivor", "Attack_RunJump2_Survivor", "Attack_RunJump_Hunter", "Attack_RunJump_Ripper" };
        // 몸 중심을 서 있을 때 골반 높이로 올리는 값(Humanoid 단위, - = 위로). 의사 기준으로 맞춰 발이 땅 높이에 온다.
        private const float AirHeightOffset = -0.91f;
        // 발을 땅에 붙이는 모션(몸 높이는 발 기준): 광대 로켓 돌진(다리를 벌려 디딘 자세라 몸이 낮아진다).
        private static readonly HashSet<string> FeetOnGround = new(); // (광대 로켓 돌진은 다리·엉덩이 높이를 달리기 모션에서 가져와 Bake로 충분)
        // 몸이 누운 모션은 몸 방향으로 맞추면 앞이 흔들린다 → 원래 방향 그대로(Root Rotation: Original).
        private static readonly HashSet<string> OriginalOrientation = new() { "Traversal_FallBack" };

        private bool IsMotion => assetPath.Replace('\\', '/').StartsWith(Folder + "/")
                                 && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsMotion) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsMotion) return;
            var importer = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            var defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0) return;

            // 한 파일을 여러 클립으로 나누는 모션(예: 루키노 도약 = 떠오름 + 내려찍기)
            if (Splits.TryGetValue(name, out var parts))
            {
                var clips = new ModelImporterClipAnimation[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                {
                    var (clipName, range, height) = parts[i];
                    clips[i] = Configure(CopyOf(defaults[0]), clipName, height);
                    clips[i].firstFrame = range.x;
                    clips[i].lastFrame = range.y;
                }
                importer.clipAnimations = clips;
                return;
            }

            var clip = Configure(defaults[0], name, NoRootHeight.Contains(name) ? Height.Body : FeetOnGround.Contains(name) ? Height.Feet : Height.Bake);
            if (Trims.TryGetValue(name, out var trim))
            {
                clip.firstFrame = trim.x;
                clip.lastFrame = trim.y;
            }
            importer.clipAnimations = new[] { clip };
        }

        /// <summary>몸 높이를 어떻게 다룰지. Bake = 모션 높이 그대로(쓰러짐), Body = 몸 높이 고정(공중 모션),
        /// Feet = 발 기준(발은 땅에, 몸만 웅크림 — 내려찍고 착지).</summary>
        private enum Height { Bake, Body, Feet }

        // 파일 이름 → 나눌 클립(이름, 프레임 구간, 높이 방식).
        private static readonly Dictionary<string, (string clip, Vector2 range, Height height)[]> Splits = new()
        {
            // Jump Attack: 웅크림(6~15) → 도약(16) → 꼭대기·팔 들기(30) → 내리치며 하강(39~48). 착지는 Hard Landing을 쓴다.
            ["Skill_Luchino_Leap"] = new[]
            {
                ("Skill_Luchino_Leap", new Vector2(16, 30), Height.Body),
                ("Skill_Luchino_Leap_Slam", new Vector2(39, 48), Height.Feet),
            },
            // Hard Landing(루키노 내려찍기 착지): 발 닿음(3) → 손 짚고 웅크림(9~30) → 일어서기(~45)
            ["Skill_Luchino_Leap_Land"] = new[] { ("Skill_Luchino_Leap_Land", new Vector2(2, 45), Height.Feet) },
            // Big Jump(카우보이 올가미로 상대 뛰어넘기): 도움닫기(1~14)는 빼고 발이 떨어짐(15) → 꼭대기(30) → 발 닿음(47) / 착지해 웅크렸다 일어서기(47~72)
            ["Skill_Cowboy_Lasso_Vault"] = new[]
            {
                ("Skill_Cowboy_Lasso_Vault", new Vector2(15, 47), Height.Body),
                ("Skill_Cowboy_Lasso_Land", new Vector2(47, 72), Height.Feet),
            },

            // ── 오르기·매달리기·낙하(CharacterTraversal / CharacterTraversalAnimator, 레이어 Traversal) ──
            // 매달리기 1(Jump To Freehang, 턱 아래가 비었을 때): 손이 턱에 닿음(25) → 몸이 흔들리다 섬(43) / 매달린 채 대기(43~75). 높이는 모션 그대로(손 ≈ 2.0).
            ["Traversal_Hang"] = new[]
            {
                ("Traversal_Hang_Grab", new Vector2(25, 43), Height.Bake),
                ("Traversal_Hang", new Vector2(43, 75), Height.Bake),
            },
            // 매달리기 2(Idle To Braced Hang, 턱 아래가 벽일 때 — 발로 벽을 짚음): 손이 턱에 닿음(17) → 발이 벽에 붙음(34) / 그 자세 유지(34~39). 손 ≈ 2.17.
            ["Traversal_HangBraced"] = new[]
            {
                ("Traversal_HangBraced_Grab", new Vector2(17, 34), Height.Bake),
                ("Traversal_HangBraced", new Vector2(34, 39), Height.Bake),
            },
            // 매달려 있다가 올라가기(Freehang Climb, 난간 없음): 매달림(1) → 무릎을 턱에 올림(34) → 턱 위에 섬(97) → 허리 폄(115). 발 기준 — 루트 높이는 BE가 발 높이 곡선대로 올린다.
            ["Traversal_HangClimb"] = new[] { ("Traversal_HangClimb", new Vector2(1, 115), Height.Feet) },
            // 난간 넘어 올라가기(Climbing의 뒷부분): 두 손으로 난간을 잡고 발로 벽을 짚음(52) → 넘어감(79) → 일어섬(112). 도착 높이는 BE가 난간 뒤 바닥으로 낮춘다.
            ["Traversal_HangClimbRail"] = new[] { ("Traversal_HangClimbRail", new Vector2(52, 112), Height.Feet) },
            // 중간 높이로 올라서기(Running Jump): 도약(16) → 높은 바닥에 발이 닿음(25) → 일어섬(40).
            ["Traversal_ClimbUp"] = new[] { ("Traversal_ClimbUp", new Vector2(14, 40), Height.Feet) },
            // 걸어서 높은 곳에서 떨어짐(Jumping): 떨어지는 자세(31~43) / 높은 곳 착지 — 깊이 웅크려 한 손 짚기(43~67).
            ["Traversal_FallHigh"] = new[]
            {
                ("Traversal_Fall", new Vector2(31, 43), Height.Body),
                ("Traversal_LandHigh", new Vector2(43, 67), Height.Feet),
            },
            // 낮은 곳 착지(Jumping Down의 착지): 살짝 웅크렸다 일어섬(45~66).
            ["Traversal_FallLow"] = new[] { ("Traversal_Land", new Vector2(45, 66), Height.Feet) },
            // 고공 낙하 착지(Hard Landing): 발 닿음(3) → 두 손 짚고 웅크림(10~28) → 일어섬(55).
            ["Traversal_LandHard"] = new[] { ("Traversal_LandHard", new Vector2(2, 55), Height.Feet) },
            // 사다리 꼭대기 도착(Climbing To Top) — 사다리 기능이 생기면 쓴다(지금은 상태만 있음).
            ["Traversal_LadderTop"] = new[] { ("Traversal_LadderTop", new Vector2(1, 121), Height.Bake) },
            // 사다리 타기 시작(Start Climbing Ladder): 가만히 선 앞부분은 빼고 손을 뻗음(10) → 한 손·한 발씩 올라 두 번째 칸(61). 사다리 기능이 생기면 쓴다.
            ["Traversal_LadderStart"] = new[] { ("Traversal_LadderStart", new Vector2(10, 61), Height.Bake) },
            // 점프해서 높은 곳에서 떨어짐 → 앞구르기 착지(Falling To Roll): 발 닿기 직전(13) → 구름(19~43) → 일어섬(58).
            ["Traversal_LandRoll"] = new[] { ("Traversal_LandRoll", new Vector2(13, 58), Height.Feet) },
            // 걸어서 창틀 넘기(Entry): 손을 창턱에 짚음(22~46) → 왼다리 넘김(55~67) → 몸이 빠져나감(79~109) → 섬(127). 높이는 모션 그대로(양쪽 바닥 같은 높이).
            ["Traversal_WindowVault"] = new[] { ("Traversal_WindowVault", new Vector2(22, 127), Height.Bake) },
            // 창틀 위에서 반대편 낮은 바닥으로 뛰어내림(Jumping Down (2)): 턱 위에 서서 한 손 짚음(16~40) → 앞으로 뛰어내림(43~50). 발 기준 — 떨어지는 동안은 낙하 모션이 이어 받는다.
            ["Traversal_WindowDrop"] = new[] { ("Traversal_WindowDrop", new Vector2(16, 50), Height.Feet) },
            // 점프 중 창틀을 넘음 — 한 손으로 창틀을 짚고 다리를 접어 넘김(Jumping (1)): 손 짚음(7~16) → 다리 넘김(16~22) → 착지(28) → 달려 나감(34).
            ["Traversal_WindowJump"] = new[] { ("Traversal_WindowJump", new Vector2(7, 34), Height.Bake) },
            // 감시자 창틀 넘기(Jump): 제자리에서 두 발 모아 뛰어(4~13) 넘고 착지(22) → 섬(28). 넘어가는 앞 이동은 BE가 한다.
            ["Traversal_WindowVaultHunter"] = new[] { ("Traversal_WindowVaultHunter", new Vector2(1, 28), Height.Bake) },
            // 물건 밀기(Push Start): 가만히 선 앞부분은 빼고 두 손을 대며 몸을 기울임(10~34) / 기울인 채 천천히 밀며 걷기 반복(34~142).
            ["Traversal_Push"] = new[]
            {
                ("Traversal_PushStart", new Vector2(10, 34), Height.Bake),
                ("Traversal_Push", new Vector2(34, 142), Height.Bake),
            },
            // 넉백당하거나 뒤로 걷다가 떨어짐(Falling을 Tools/Blender/flip_on_back.py로 뒤집음): 하늘을 보며 등으로 떨어짐(머리가 뒤쪽), 허우적대는 자세 반복.
            ["Traversal_FallBack"] = new[] { ("Traversal_FallBack", new Vector2(1, 136), Height.Body) },
        };

        private ModelImporterClipAnimation Configure(ModelImporterClipAnimation clip, string name, Height height)
        {
            clip.name = name;
            clip.loopTime = Looping.Contains(name);
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = OriginalOrientation.Contains(name); // 몸 방향 기준: 옆으로 선 자세(복싱 등)도 몸이 앞(+Z)을 보게 맞춘다. 누운 자세는 원래 방향 그대로
            bool bake = height == Height.Bake;
            clip.lockRootHeightY = bake;          // 높이는 포즈에 굽는다(쓰러질 때 몸이 바닥으로 내려간다)
            clip.keepOriginalPositionY = bake;
            // Body: 몸 중심 기준으로 몸 높이를 고정하고 서 있을 때 높이만큼 올린다(발차기처럼 두 발이 들려도 몸이 안 꺼진다).
            // Feet: 발 기준. 발이 땅에 붙고 몸만 웅크린다.
            clip.heightFromFeet = height == Height.Feet;
            clip.heightOffset = height == Height.Body ? AirHeightOffset : 0f;
            clip.lockRootPositionXZ = false;      // 수평 이동은 루트로 빼서 버린다 → 제자리 모션(Apply Root Motion 꺼짐)
            clip.keepOriginalPositionXZ = false;
            clip.mirror = Mirrored.Contains(name);
            return clip;
        }

        private static ModelImporterClipAnimation CopyOf(ModelImporterClipAnimation src) => new()
        {
            takeName = src.takeName, name = src.name, firstFrame = src.firstFrame, lastFrame = src.lastFrame,
        };

        // 모션 FBX가 들어오거나 바뀌면 Animator Controller를 다시 만든다.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;
            foreach (var list in new[] { imported, deleted, moved })
                foreach (var p in list)
                    if (p.StartsWith(Folder + "/") && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) changed = true;
            if (!changed) return;
            EditorApplication.delayCall -= RebuildMotions;
            EditorApplication.delayCall += RebuildMotions;
        }

        // 자세를 고친 모션을 먼저 다시 만들고(원본이 바뀌었을 수 있다), Controller를 다시 만든다.
        private static void RebuildMotions()
        {
            MotionPoseAdjuster.BuildAll();
            CharacterMotionControllerBuilder.Build();
        }
    }
}

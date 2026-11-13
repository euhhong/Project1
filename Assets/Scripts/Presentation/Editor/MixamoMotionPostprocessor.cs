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
    /// </summary>
    public class MixamoMotionPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/Models/IdentityV/_Motions/Mixamo";

        private static readonly HashSet<string> Looping = new()
        {
            "Idle", "Walk_F", "Walk_B", "Walk_L", "Walk_R", "Run_F", "Run_B", "Run_L", "Run_R",
            "Attack_HeavyCharge", "Attack_AirCharge", "Block", "Idle_Thief", "Idle_Luchino", "Idle_HellEmber", "Idle_Antonio", "SelectIdle_Hunter", "SelectIdle_Survivor", "Skill_Forward_Charge", "Stun", "Stairs_Up", "Carry_Joker", "Skill_SmileyFace_RocketCharge", "Skill_SmileyFace_RocketCharge_Raise", "Skill_Thief_Flashlight",
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
            ["Attack_Light_Hunter"] = new Vector2(6, 50),      // Standing Melee Attack Downward: 오른손 들기 → 내려치기(20~28) → 몸 되돌리기
            ["Attack_Light_Luchino"] = new Vector2(4, 60),     // Standing Melee Attack Horizontal: 오른쪽 뒤로 당김 → 가로 베기(22~29) → 몸 되돌리기
            ["Attack_Light_Geisha"] = new Vector2(7, 61),      // Standing Torch Melee Attack Stab을 Blender로 좌우 뒤집음(Tools/Blender/mirror_motion.py, 프레임 1 밀림): 오른팔로 길게 찌르기
            ["Attack_Light_Hastur"] = new Vector2(8, 44),      // Standing 2H Magic Attack 03: 두 손 들기 → 앞으로 내뻗기(28~36). 뒤의 긴 유지 자세는 뺌
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
            ["Air"] = new Vector2(0, 22),                      // Jump (1)(제자리 점프): 바로 도약(0) → 꼭대기(10) → 착지(22)
            ["Air_F"] = new Vector2(25, 44),                   // Jumping(걸어서 점프): 발이 떨어짐(25) → 꼭대기(34) → 착지(44)
            ["Air_Run"] = new Vector2(0, 20),                  // Running Jump(뛰면서 점프): 도약(0) → 꼭대기(10) → 착지(20)
        };

        // 공중에서 쓰는 모션은 모션 자체의 뛰어오름을 빼서 버린다. 높이는 게임의 점프 궤적이 정한다.
        private static readonly HashSet<string> NoRootHeight = new() { "Air", "Air_F", "Air_Run", "Attack_RunJump_Survivor", "Attack_RunJump_Hunter", "Attack_RunJump_Ripper" };
        // 몸 중심을 서 있을 때 골반 높이로 올리는 값(Humanoid 단위, - = 위로). 의사 기준으로 맞춰 발이 땅 높이에 온다.
        private const float AirHeightOffset = -0.91f;
        // 발을 땅에 붙이는 모션(몸 높이는 발 기준): 광대 로켓 돌진(다리를 벌려 디딘 자세라 몸이 낮아진다).
        private static readonly HashSet<string> FeetOnGround = new(); // (광대 로켓 돌진은 다리·엉덩이 높이를 달리기 모션에서 가져와 Bake로 충분)

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
        };

        private ModelImporterClipAnimation Configure(ModelImporterClipAnimation clip, string name, Height height)
        {
            clip.name = name;
            clip.loopTime = Looping.Contains(name);
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = false;  // 몸 방향 기준: 옆으로 선 자세(복싱 등)도 몸이 앞(+Z)을 보게 맞춘다
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

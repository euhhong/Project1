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
            "Attack_HeavyCharge", "Attack_AirCharge", "Block", "SelectIdle_Hunter", "SelectIdle_Survivor", "Skill_Forward_Charge", "Stun",
        };
        // Air(Standing Jump)는 반복하지 않는다: 도약 → 착지 직전 자세에서 멈추고, 땅에 닿으면 이동으로 돌아간다.

        // 좌우를 뒤집어 쓰는 모션. Boxing(약공격)은 왼손 잽이라, BE 판정(오른손)에 맞춰 뒤집는다.
        private static readonly HashSet<string> Mirrored = new() { "Attack_Light" };

        // 원본에서 쓸 구간(프레임). 없으면 전체를 쓴다.
        // Attack_Light: Boxing 잽 한 번(5~30). 판정 비율은 CharacterAttackAnimator.LightWindupEnd/ActiveEnd.
        // Attack_RunJump_Survivor: Flying Kick 도약~착지(0~30).
        private static readonly Dictionary<string, Vector2> Trims = new()
        {
            ["Attack_Light"] = new Vector2(5, 30),
            ["Attack_RunJump_Survivor"] = new Vector2(0, 30),
            ["Attack_Light_Hunter"] = new Vector2(6, 50),      // Standing Melee Attack Downward: 오른손 들기 → 내려치기(20~28) → 몸 되돌리기
            ["Attack_Light_Luchino"] = new Vector2(4, 60),     // Standing Melee Attack Horizontal: 오른쪽 뒤로 당김 → 가로 베기(22~29) → 몸 되돌리기
            ["Attack_Light_Geisha"] = new Vector2(6, 60),      // Standing Torch Melee Attack Stab: 오른손 찌르기(14~25), 왼손은 횃불
            ["Attack_Light_Hastur"] = new Vector2(8, 44),      // Standing 2H Magic Attack 03: 두 손 들기 → 앞으로 내뻗기(28~36). 뒤의 긴 유지 자세는 뺌
            ["Attack_Light_MindsEye"] = new Vector2(10, 48),   // Standing Melee Combo Attack Ver. 3: 첫 타 가로 베기(24~31) → 한 바퀴 돌아 앞을 봄(48). 두 번째 타는 뺌
            ["Attack_RunJump_Hunter"] = new Vector2(36, 70),   // 무기를 든 자세 → 내려치기 → 마무리(앞의 달리기·마지막 대기는 뺌)
            ["Air"] = new Vector2(14, 34),                     // Standing Jump: 발이 떨어짐(14) → 꼭대기(23) → 착지(34). 0.67초 ≈ 게임 점프 0.69초
            ["Air_F"] = new Vector2(25, 44),                   // Jumping(걸어서 점프): 발이 떨어짐(25) → 꼭대기(34) → 착지(44)
            ["Air_Run"] = new Vector2(0, 20),                  // Running Jump(뛰면서 점프): 도약(0) → 꼭대기(10) → 착지(20)
        };

        // 공중에서 쓰는 모션은 모션 자체의 뛰어오름을 빼서 버린다. 높이는 게임의 점프 궤적이 정한다.
        private static readonly HashSet<string> NoRootHeight = new() { "Air", "Air_F", "Air_Run", "Attack_RunJump_Survivor", "Attack_RunJump_Hunter" };
        // 몸 중심을 서 있을 때 골반 높이로 올리는 값(Humanoid 단위, - = 위로). 의사 기준으로 맞춰 발이 땅 높이에 온다.
        private const float AirHeightOffset = -0.91f;

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

            var clip = defaults[0];
            clip.name = name;
            clip.loopTime = Looping.Contains(name);
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = false;  // 몸 방향 기준: 옆으로 선 자세(복싱 등)도 몸이 앞(+Z)을 보게 맞춘다
            bool keepHeight = !NoRootHeight.Contains(name);
            clip.lockRootHeightY = keepHeight;    // 높이는 포즈에 굽는다(쓰러질 때 몸이 바닥으로 내려간다)
            clip.keepOriginalPositionY = keepHeight;
            // 높이를 뺄 때는 몸 중심 기준으로 몸 높이를 고정하고, 서 있을 때 높이만큼 올린다.
            // (발 기준으로 하면 발차기처럼 두 발이 들릴 때 몸이 땅으로 꺼진다.)
            clip.heightFromFeet = false;
            clip.heightOffset = keepHeight ? 0f : AirHeightOffset;
            clip.lockRootPositionXZ = false;      // 수평 이동은 루트로 빼서 버린다 → 제자리 모션(Apply Root Motion 꺼짐)
            clip.keepOriginalPositionXZ = false;
            clip.mirror = Mirrored.Contains(name);
            if (Trims.TryGetValue(name, out var range))
            {
                clip.firstFrame = range.x;
                clip.lastFrame = range.y;
            }
            importer.clipAnimations = new[] { clip };
        }

        // 모션 FBX가 들어오거나 바뀌면 Animator Controller를 다시 만든다.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool changed = false;
            foreach (var list in new[] { imported, deleted, moved })
                foreach (var p in list)
                    if (p.StartsWith(Folder + "/") && p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) changed = true;
            if (!changed) return;
            EditorApplication.delayCall -= CharacterMotionControllerBuilder.Build;
            EditorApplication.delayCall += CharacterMotionControllerBuilder.Build;
        }
    }
}

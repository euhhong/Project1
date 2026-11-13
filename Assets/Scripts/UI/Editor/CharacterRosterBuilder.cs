using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.UI.EditorTools
{
    /// <summary>
    /// `Assets/Models/IdentityV/`의 Survivor_*·Hunter_* 폴더를 읽어 캐릭터 선택 목록(CharacterRoster)을 만든다.
    /// 메뉴: Tools > FE > 캐릭터 목록 다시 만들기. 결과: Assets/UI/Resources/CharacterRoster.asset
    /// 크기: 모든 캐릭터 모델은 위치 0, 회전 0, 크기 4(캐릭터 루트의 Model 자식).
    /// </summary>
    public static class CharacterRosterBuilder
    {
        private const string ModelsFolder = "Assets/Models/IdentityV";
        private const string MotionsFolder = ModelsFolder + "/_Motions";
        private const string AssetPath = "Assets/UI/Resources/CharacterRoster.asset";
        private const float ModelScale = 4f;
        private const string SkillsFolder = "Assets/Data/Skills";

        // 캐릭터별 고유 스킬(E). 데이터 파일이 없으면 기본값으로 만든다. 값은 파일(인스펙터)에서 바꾼다.
        private static readonly Dictionary<string, System.Func<Project1.Systems.SkillDefinition>> Skills = new()
        {
            ["Survivor_Doctor"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.SelfHealSkill>(); s.id = "Doctor_SelfHeal"; s.displayName = "자가치료"; s.castTime = 1.5f; s.cooldown = 0f /* 테스트 단계 0초, 원래 12 */; s.castMoveSpeed = 0f; s.healFraction = 0.4f; return s; },
            ["Hunter_Luchino"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.LeapSkill>(); s.id = "Luchino_Leap"; s.displayName = "도약 내려찍기"; s.castTime = 5f; s.cooldown = 0f /* 테스트 단계 0초, 원래 12 */; s.leapHeight = 12f; s.forwardSpeed = 9f; return s; },
            ["Hunter_Gamekeeper"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.GrabShotSkill>(); s.id = "Gamekeeper_Grab"; s.displayName = "그랩"; s.castTime = 3f; s.cooldown = 0f; return s; },
            ["Survivor_Mercenary"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.WallDashSkill>(); s.id = "Mercenary_Guard"; s.displayName = "아대"; s.castTime = 99999f; /* 켜 두는 스킬: 다시 누를 때까지 */ s.cooldown = 0f; return s; },
            ["Hunter_Geisha"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.ButterflyGlideSkill>(); s.id = "Geisha_Butterfly"; s.displayName = "나비에게"; s.castTime = 3f; s.cooldown = 0f; return s; },
            ["Survivor_Coordinator"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.FlareShotSkill>(); s.id = "Coordinator_Flare"; s.displayName = "신호총"; s.castTime = 2f; s.cooldown = 0f; return s; },
            ["Hunter_SmileyFace"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.RocketChargeSkill>(); s.id = "SmileyFace_RocketCharge"; s.displayName = "로켓 돌진"; s.castTime = 12f; s.cooldown = 0f; s.castMoveSpeed = 3.6f; s.chargeSpeed = 8f /* 전위(16)의 0.5배 */; return s; },
            ["Hunter_Ripper"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.MistBladeSkill>(); s.id = "Ripper_MistBlade"; s.displayName = "안개 칼날"; s.castTime = 3f; s.cooldown = 0f; /* 안개 1칸을 쓴다(7초마다 1칸, 최대 2칸) */ return s; },
            ["Survivor_Thief"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.FlashlightSkill>(); s.id = "Thief_Flashlight"; s.displayName = "손전등"; s.castTime = 6f; /* 최대 — E를 다시 누르면 넣음 */ s.cooldown = 0f; s.castMoveSpeed = 2.5f; return s; },
            ["Survivor_Forward"] = () => { var s = ScriptableObject.CreateInstance<Project1.Systems.ChargeSkill>(); s.id = "Forward_Charge"; s.displayName = "럭비공 돌진"; s.castTime = 6f; s.cooldown = 0f /* 테스트 단계 0초, 원래 15 */; s.chargeSpeed = 16f; return s; },
        };

        private static readonly Dictionary<string, string> Names = new()
        {
            ["Survivor_Doctor"] = "의사", ["Survivor_Coordinator"] = "공군", ["Survivor_Cowboy"] = "카우보이",
            ["Survivor_Forward"] = "전위", ["Survivor_Mercenary"] = "용병", ["Survivor_MindsEye"] = "맹인",
            ["Survivor_Thief"] = "도둑",
            ["Hunter_Antonio"] = "안토니오", ["Hunter_Gamekeeper"] = "사냥터지기", ["Hunter_Geisha"] = "붉은나비",
            ["Hunter_Hastur"] = "하스터", ["Hunter_HellEmber"] = "공장장", ["Hunter_Luchino"] = "루키노",
            ["Hunter_Ripper"] = "리퍼", ["Hunter_SmileyFace"] = "광대",
        };

        // 캐릭터별 게임 Controller(없으면 생존자 CharacterLocomotion / 감시자 HunterLocomotion). CharacterMotionControllerBuilder의 표와 같다.
        private static readonly Dictionary<string, string> Controllers = new()
        {
            ["Hunter_Luchino"] = "LuchinoLocomotion", ["Hunter_Geisha"] = "GeishaLocomotion", ["Hunter_Hastur"] = "HasturLocomotion", ["Hunter_SmileyFace"] = "SmileyFaceLocomotion",
            ["Hunter_Ripper"] = "RipperLocomotion", ["Hunter_HellEmber"] = "HellEmberLocomotion",
            ["Survivor_MindsEye"] = "MindsEyeLocomotion", // 도둑은 생존자 기본(대기 Idle (1)) — Ninja Idle은 사용자 요청으로 되돌림
        };

        [MenuItem("Tools/FE/캐릭터 목록 다시 만들기")]
        public static void Build()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(AssetPath);
            if (roster == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
                if (!AssetDatabase.IsValidFolder("Assets/UI/Resources")) AssetDatabase.CreateFolder("Assets/UI", "Resources");
                roster = ScriptableObject.CreateInstance<CharacterRoster>();
                AssetDatabase.CreateAsset(roster, AssetPath);
            }

            var survivorGame = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{MotionsFolder}/CharacterLocomotion.controller");
            var survivorSelect = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{MotionsFolder}/SurvivorSelect.controller");
            var hunterSelect = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{MotionsFolder}/HunterSelect.controller");

            var log = new List<string>();
            roster.entries.Clear();
            foreach (var role in new[] { CharacterRole.Survivor, CharacterRole.Hunter })
            {
                string prefix = role == CharacterRole.Hunter ? "Hunter_" : "Survivor_";
                foreach (var dir in AssetDatabase.GetSubFolders(ModelsFolder))
                {
                    string id = System.IO.Path.GetFileName(dir);
                    if (!id.StartsWith(prefix)) continue;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{id}.fbx");
                    if (model == null) continue;

                    float hips = Measure(model, out _);
                    // 모델 Transform은 모두 위치 0, 회전 0, 크기 4로 맞춘다(사용자 결정, 2026-11-12).
                    float scale = ModelScale;

                    string controllerName = Controllers.TryGetValue(id, out var n) ? n
                        : role == CharacterRole.Survivor ? null : "HunterLocomotion";
                    var game = controllerName == null ? survivorGame
                        : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{MotionsFolder}/{controllerName}.overrideController");

                    roster.entries.Add(new CharacterRoster.Entry
                    {
                        id = id,
                        displayName = Names.TryGetValue(id, out var dn) ? dn : id,
                        role = role,
                        model = model,
                        scale = Mathf.Round(scale * 100f) / 100f,
                        gameController = game,
                        selectController = role == CharacterRole.Hunter ? hunterSelect : survivorSelect,
                        skill = LoadOrCreateSkill(id),
                    });
                    log.Add($"{id} x{scale:0.00}{(hips > 0f ? "" : "(Generic)")}");
                }
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FE] 캐릭터 목록 {roster.entries.Count}명: {string.Join(", ", log)}");
        }

        private static Project1.Systems.SkillDefinition LoadOrCreateSkill(string id)
        {
            if (!Skills.TryGetValue(id, out var make)) return null;
            var proto = make();
            string path = $"{SkillsFolder}/{proto.id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Project1.Systems.SkillDefinition>(path);
            if (existing != null) { Object.DestroyImmediate(proto); return existing; }
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(SkillsFolder)) AssetDatabase.CreateFolder("Assets/Data", "Skills");
            AssetDatabase.CreateAsset(proto, path);
            return proto;
        }

        /// <summary>기본 자세의 골반 높이(Humanoid가 아니면 0)와 전체 높이를 잰다(크기 1 기준).</summary>
        private static float Measure(GameObject prefab, out float boundsHeight)
        {
            boundsHeight = 0f;
            if (prefab == null) return 0f;
            var go = (GameObject)Object.Instantiate(prefab, new Vector3(0f, -2000f, 0f), Quaternion.identity);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    Bounds b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    boundsHeight = b.size.y;
                }
                var anim = go.GetComponent<Animator>();
                var hips = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
                return hips != null ? hips.position.y - go.transform.position.y : 0f;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

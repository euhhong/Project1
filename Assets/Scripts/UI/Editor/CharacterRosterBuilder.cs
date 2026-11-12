using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project1.UI.EditorTools
{
    /// <summary>
    /// `Assets/Models/IdentityV/`의 Survivor_*·Hunter_* 폴더를 읽어 캐릭터 선택 목록(CharacterRoster)을 만든다.
    /// 메뉴: Tools > FE > 캐릭터 목록 다시 만들기. 결과: Assets/UI/Resources/CharacterRoster.asset
    /// 크기: 생존자는 지금 의사(크기 4)와 골반 높이가 같게, 감시자는 그 2.3/1.7배가 되게 맞춘다(GameDesign 키 비율).
    /// </summary>
    public static class CharacterRosterBuilder
    {
        private const string ModelsFolder = "Assets/Models/IdentityV";
        private const string MotionsFolder = ModelsFolder + "/_Motions";
        private const string AssetPath = "Assets/UI/Resources/CharacterRoster.asset";
        private const string ReferenceId = "Survivor_Doctor";
        private const float ReferenceScale = 4f;
        private const float HunterHeightRatio = 2.3f / 1.7f;

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
            ["Hunter_Luchino"] = "LuchinoLocomotion", ["Hunter_Geisha"] = "GeishaLocomotion", ["Hunter_Hastur"] = "HasturLocomotion",
            ["Survivor_MindsEye"] = "MindsEyeLocomotion",
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

            float refHips = Measure(AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{ReferenceId}/{ReferenceId}.fbx"), out float refBounds);
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

                    float hips = Measure(model, out float bounds);
                    float ratio = role == CharacterRole.Hunter ? HunterHeightRatio : 1f;
                    // Humanoid는 골반 높이로, 아니면(하스터) 전체 높이로 맞춘다.
                    float scale = hips > 0f && refHips > 0f
                        ? ReferenceScale * refHips * ratio / hips
                        : ReferenceScale * refBounds * ratio / Mathf.Max(bounds, 0.01f);

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
                    });
                    log.Add($"{id} x{scale:0.00}{(hips > 0f ? "" : "(Generic)")}");
                }
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FE] 캐릭터 목록 {roster.entries.Count}명: {string.Join(", ", log)}");
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

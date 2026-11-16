using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 맵 블록아웃(MapBlockout) 겉모습을 VARCO 3D로 만든 텍스처·모델로 바꾼다. 메뉴: Tools/BG/맵 꾸미기 (VARCO).
    /// 충돌(BoxCollider)은 블록아웃 그대로 두고, 사물은 블록 렌더러만 끄고 같은 자리·크기에 모델을 얹는다.
    /// 벽·바닥·지붕은 월드 좌표 텍스처(TriplanarLit) 머티리얼로 바꾼다. 정원 나무·바위·산울타리는 VARCO_Garden에 따로 세운다.
    /// 블록아웃을 다시 만들면(MapBlockoutBuilder.Build) 끝에서 자동으로 다시 적용된다. 여러 번 실행해도 같은 결과.
    /// 모델: Assets/Environment/Models/VARCO/{Key}/{Key}.fbx — 없는 모델은 블록 그대로 둔다.
    /// </summary>
    public static partial class MapDresser
    {
        private const string RootName = "MapBlockout";
        private const string GardenName = "VARCO_Garden";
        private const string ModelPrefix = "VARCO_";
        private const string ModelFolder = "Assets/Environment/Models/VARCO";
        private const string TexFolder = "Assets/Environment/Textures/VARCO";
        private const string MatFolder = "Assets/Environment/Materials/VARCO";
        private const string ShaderName = "Project1/Environment/TriplanarLit";

        // 모델 앞(얼굴)이 모델 +Z를 본다고 보고, 자리의 앞(+Z)에 맞추기 위한 추가 회전(도). 모델을 보고 맞춘 값.
        private static readonly Dictionary<string, float> YawFix = new()
        {
            { "Chair", 180f }, // 의자 그룹 +Z = 등받이 쪽 → 모델 앞(+Z)을 반대로
            { "Bench", 90f },  // 긴 의자: 블록은 z로 길다, 모델은 x로 길다
            { "Locker", -90f }, { "LockerBroken", -90f }, { "LockerOpen", -90f }, { "LockerDented", -90f },
        };

        private static Material wallMat, woodMat, stoneFloorMat, roofMat, accentMat;
        private static readonly Dictionary<string, GameObject> modelCache = new();
        private static int placed, missing;

        [MenuItem("Tools/BG/맵 꾸미기 (VARCO)")]
        public static void Menu()
        {
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (root == null) { Debug.LogWarning("MapDresser: 씬에 MapBlockout이 없습니다. 먼저 Tools/BG/맵 블록아웃 다시 만들기."); return; }
            Debug.Log(Apply(root.transform));
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        /// <summary>블록아웃 root를 꾸민다. 결과 한 줄을 돌려준다.</summary>
        public static string Apply(Transform root)
        {
            modelCache.Clear();
            placed = missing = 0;
            LoadMaterials();

            // 지난번에 얹은 모델·정원 지우기
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t != root && t.name.StartsWith(ModelPrefix)).ToArray())
                if (t != null) Object.DestroyImmediate(t.gameObject);

            int retextured = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.enabled = true;
                var m = PickMaterial(r);
                if (m != null && r.sharedMaterial != m) { r.sharedMaterial = m; retextured++; }
            }

            DressProps(root);
            BuildGarden(root);
            BuildExtras(root); // 예배당 장식·야외 나무·풀·납작 바위·판자 구역·연습용 우물(MapDresser.Extras)
            return $"MapDresser: 텍스처 {retextured}개 블록, 모델 {placed}개 배치" + (missing > 0 ? $", 모델 파일 없음 {missing}건(블록 그대로)" : "");
        }

        // ───────────────────────────── 텍스처 ─────────────────────────────

        /// <summary>머티리얼(텍스처·노멀·세기)만 다시 맞춘다 — 씬은 건드리지 않는다.</summary>
        public static void RefreshMaterials() => LoadMaterials();

        private static void LoadMaterials()
        {
            // 돌벽 = 튀어나온 거친 돌 블록: 높이 맵(T_StoneWall_Height — 색 텍스처에서 줄눈 직선만 찾아 깊게 파고 블록 모서리를 둥글게, 표면 잔결)
            //   → 노멀(세기 2.2) + 잔결(0.7) + 줄눈 어둡게(0.9) + 시차(0.045) + 매끈함 거의 0(번들거림 없음)
            wallMat = GetMat("M_Env_StoneWall", "T_StoneWall", 2.6f, 0.02f, "T_StoneWall_Height.png", 2.2f, 0.9f, 0.7f, "T_StoneWall_Parallax.png", 0.045f);
            woodMat = GetMat("M_Env_WoodFloor", "T_WoodFloor", 3f, 0.25f);
            stoneFloorMat = GetMat("M_Env_StoneFloor", "T_StoneFloor", 4f, 0.1f);
            roofMat = GetMat("M_Env_RoofSlate", "T_RoofSlate", 3f, 0.15f);
            accentMat = GetMat("M_Env_Accent", "T_Accent", 2f, 0.12f);
        }

        private static Material GetMat(string name, string tex, float meters, float smooth, string normalFile = null, float bump = 1f, float cavity = 0f, float grit = 0f, string heightFile = null, float parallax = 0f)
        {
            Texture2D height = null;
            if (heightFile != null)
            {
                string hp = $"{TexFolder}/{heightFile}";
                if (AssetImporter.GetAtPath(hp) is TextureImporter hi && (hi.sRGBTexture || hi.maxTextureSize != 1024))
                {
                    hi.sRGBTexture = false; // 높이 값 그대로
                    hi.maxTextureSize = 1024;
                    hi.SaveAndReimport();
                }
                height = AssetDatabase.LoadAssetAtPath<Texture2D>(hp);
            }
            Texture2D normal = null;
            if (normalFile != null)
            {
                string np = $"{TexFolder}/{normalFile}";
                if (AssetImporter.GetAtPath(np) is TextureImporter ti &&
                    (ti.textureType != TextureImporterType.NormalMap || !ti.convertToNormalmap || !Mathf.Approximately(ti.heightmapScale, 0.2f)))
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.convertToNormalmap = true;       // 회색 높이 맵 → 노멀 맵
                    ti.heightmapScale = 0.2f;
                    ti.normalmapFilter = TextureImporterNormalFilter.Sobel;
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                }
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(np);
            }
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Environment/Materials", "VARCO");
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ShaderName);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{tex}.jpg"));
            m.SetFloat("_Tiling", meters);
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", bump);
            m.SetFloat("_CavityStrength", cavity);
            m.SetFloat("_DetailBump", grit);
            m.SetTexture("_HeightMap", height);
            m.SetFloat("_Parallax", height != null ? parallax : 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        private static Material PickMaterial(MeshRenderer r)
        {
            string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
            string path = PathOf(r.transform);
            bool outdoor = path.Contains("/Grounds/") || path.Contains("Balcony_Front") || path.Contains("Terrace_Rear")
                || path.Contains("/Connectors/") || path.Contains("Roof_Terrace") || path.Contains("Rubble");
            bool roof = r.transform.GetComponentsInParent<Transform>(true).Any(t =>
                (t.name.StartsWith("Roof") && !t.name.StartsWith("Roof_Terrace")) || t.name.Contains("Fleche") || t.name.Contains("Spire") || t.name == "Cornice");

            if (mat is "M_Blockout_Wall" or "M_Env_StoneWall") return wallMat;
            if (mat is "M_Blockout_Floor" or "M_Env_WoodFloor" or "M_Env_StoneFloor") return outdoor ? stoneFloorMat : woodMat;
            if (mat is "M_Blockout_Accent" or "M_Env_Accent" or "M_Env_RoofSlate") return roof ? roofMat : accentMat;
            return null; // 블록아웃 머티리얼이 아닌 것은 그대로
        }

        private static string PathOf(Transform t)
        {
            var s = new System.Text.StringBuilder();
            for (; t != null; t = t.parent) s.Insert(0, "/" + t.name);
            return s.Append('/').ToString();
        }

        // ───────────────────────────── 사물 ─────────────────────────────

        private static void DressProps(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true).ToArray();
            bool IsBox(Transform t) => t.GetComponent<MeshRenderer>() != null;

            foreach (var t in all)
            {
                string n = t.name;
                if (IsBox(t))
                {
                    if (DressExtraBox(t)) continue; // 예배당 긴 의자·제단, 계단 단, 기둥(MapDresser.Extras)
                    if (n.StartsWith("Statue_")) FitBox(t, "Statue", uniform: true);
                    else if (n.StartsWith("Tomb_")) FitBox(t, "Tomb");
                    else if (n.StartsWith("Locker_") && !n.Contains("Bench")) FitBox(t, LockerVariant(n)); // 사물함 모양을 섞음(그대로·문 뜯김·문 열림·찌그러짐)
                    else if (n is "Locker1F_Bench" or "Locker_Bench") FitBox(t, "Bench");
                    else if (n.StartsWith("Panel_")) FitBox(t, "Screen");
                    else if (n.StartsWith("Chapel_Candle_")) FitBox(t, "Candle");
                    else if (n is "LowTerrace_Crate" or "Storage_A" or "Storage_A_Top" or "Storage_B") FitBox(t, "Crate");
                    else if (t.parent != null && t.parent.name.StartsWith("Crates")) FitBox(t, "Crate");
                }
                else
                {
                    if (DressExtraGroup(t)) continue; // 사다리·무너진 잔해 더미(MapDresser.Extras)
                    if (n == "Fountain") FitGroup(t, "Fountain", t.Cast<Transform>());
                    else if (n == "Gazebo") FitGroup(t, "Gazebo", t.Cast<Transform>());
                    else if (n == "Table" && t.parent != null && t.parent.name == "Lobby") FitGroup(t, "Table", t.Cast<Transform>());
                    else if (t.parent != null && t.parent.name == "Chairs") FitGroup(t, "Chair", t.Cast<Transform>());
                    else if (n == "Chandelier") FitGroup(t, "Chandelier", t.Cast<Transform>().Where(c => c.name != "Chain"), uniformByWidth: true);
                    else if (n == "Perimeter") DressFence(t);
                }
            }
        }

        /// <summary>상자 하나 자리에 모델을 얹고 상자 렌더러를 끈다(충돌은 남김).</summary>
        private static void FitBox(Transform box, string key, bool uniform = false)
        {
            var frameRot = box.localRotation;
            var size = box.localScale;
            var bottom = box.localPosition - frameRot * Vector3.up * (size.y / 2f);
            if (Place(key, box.parent, frameRot, bottom, size, uniform, false, $"{ModelPrefix}{key}_{box.name}") != null)
                Hide(box);
        }

        /// <summary>그룹 안 상자들을 감싸는 자리(그룹 로컬)에 모델 하나를 얹고 그 상자들을 끈다.</summary>
        private static void FitGroup(Transform group, string key, IEnumerable<Transform> parts, bool uniformByWidth = false)
        {
            var boxes = parts.SelectMany(p => p.GetComponentsInChildren<MeshRenderer>(true)).Select(r => r.transform).Where(b => !b.name.StartsWith(ModelPrefix)).ToList();
            if (boxes.Count == 0) return;
            var b = LocalBounds(group, boxes);
            var bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            if (Place(key, group, Quaternion.identity, bottom, b.size, false, uniformByWidth, $"{ModelPrefix}{key}") != null)
                foreach (var x in boxes) Hide(x);
        }

        /// <summary>둘레 담장: 벽 상자마다 6m 안팎 칸으로 나눠 철책 모델을 늘어놓고, 담장·기둥 블록은 끈다.</summary>
        private static void DressFence(Transform perimeter)
        {
            if (LoadModel("Fence") == null) { missing++; return; }
            var boxes = perimeter.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToList();
            foreach (var box in boxes)
            {
                if (box.name.StartsWith("Post_")) { Hide(box); continue; }
                var s = box.localScale;
                bool alongZ = s.z > s.x;
                float len = alongZ ? s.z : s.x;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 6f));
                float seg = len / n;
                var rot = box.localRotation * (alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
                var along = rot * Vector3.right;
                var baseBottom = box.localPosition - box.localRotation * Vector3.up * (s.y / 2f);
                for (int i = 0; i < n; i++)
                {
                    var bottom = baseBottom + along * (-len / 2f + seg * (i + 0.5f));
                    Place("Fence", box.parent, rot, bottom, new Vector3(seg, s.y + 0.7f, 0.7f), false, false, $"{ModelPrefix}Fence_{box.name}_{i}");
                }
                Hide(box);
            }
        }

        private static void Hide(Transform box)
        {
            var r = box.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }

        /// <summary>parent 로컬에서 bottom(바닥 가운데)·size 상자에 맞게 모델을 놓는다. uniform = 높이로 같은 비율, uniformByWidth = 가로로 같은 비율.</summary>
        private static GameObject Place(string key, Transform parent, Quaternion frameRot, Vector3 bottom, Vector3 size, bool uniform, bool uniformByWidth, string name)
        {
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return null; }
            var go = Instantiate(prefab, parent, name);

            float yaw = YawFix.TryGetValue(key, out var y) ? y : 0f;
            var yawRot = Quaternion.Euler(0f, yaw, 0f);
            var mb = ModelBounds(go);
            bool swap = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, -90f)) < 1f;
            // 자리(frame) 축 기준 모델 크기
            var m = new Vector3(swap ? mb.size.z : mb.size.x, mb.size.y, swap ? mb.size.x : mb.size.z);
            m = Vector3.Max(m, Vector3.one * 0.001f);
            Vector3 frameScale;
            if (uniform) frameScale = Vector3.one * (size.y / m.y);
            else if (uniformByWidth) frameScale = Vector3.one * ((size.x / m.x + size.z / m.z) / 2f);
            else frameScale = new Vector3(size.x / m.x, size.y / m.y, size.z / m.z);
            var modelScale = swap ? new Vector3(frameScale.z, frameScale.y, frameScale.x) : frameScale;

            var rot = frameRot * yawRot;
            go.transform.localRotation = rot;
            go.transform.localScale = modelScale;
            var pivot = new Vector3(mb.center.x, mb.min.y, mb.center.z);
            go.transform.localPosition = bottom - rot * Vector3.Scale(modelScale, pivot);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            placed++;
            return go;
        }

        /// <summary>빈 그릇(이름 name) 아래에 모델을 넣는다 — FBX 루트의 축 변환 회전(Z-up → Y-up)을 그대로 둔다.</summary>
        private static GameObject Instantiate(GameObject prefab, Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
            inst.transform.localPosition = Vector3.zero;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject LoadModel(string key)
        {
            if (modelCache.TryGetValue(key, out var g)) return g;
            g = AssetDatabase.LoadAssetAtPath<GameObject>(key.Contains('/') ? $"{ModelFolder}/{key}.fbx" : $"{ModelFolder}/{key}/{key}.fbx"); // "폴더/이름" = 한 폴더에 여러 모델(풀)
            modelCache[key] = g;
            return g;
        }

        /// <summary>모델 루트 로컬 좌표의 메시 경계(루트 스케일·회전 1일 때).</summary>
        private static Bounds ModelBounds(GameObject go)
        {
            var root = go.transform;
            var saveR = root.localRotation; var saveS = root.localScale; var saveP = root.localPosition;
            root.localRotation = Quaternion.identity; root.localScale = Vector3.one; root.localPosition = Vector3.zero;
            var w2l = root.worldToLocalMatrix;
            bool any = false; var b = new Bounds();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var l2w = w2l * mf.transform.localToWorldMatrix;
                foreach (var c in Corners(mb))
                {
                    var p = l2w.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            root.localRotation = saveR; root.localScale = saveS; root.localPosition = saveP;
            return b;
        }

        private static Bounds LocalBounds(Transform frame, List<Transform> boxes)
        {
            var w2l = frame.worldToLocalMatrix;
            bool any = false; var b = new Bounds();
            foreach (var box in boxes)
            {
                var l2w = w2l * box.localToWorldMatrix;
                foreach (var c in Corners(new Bounds(Vector3.zero, Vector3.one)))
                {
                    var p = l2w.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        // ───────────────────────────── 정원 ─────────────────────────────
        // 건물·통로를 막지 않는 담장 안쪽 띠(±45.5)와 뒤뜰에만 둔다. 충돌: 나무 = 줄기 캡슐, 바위·산울타리 = 상자.

        private static void BuildGarden(Transform root)
        {
            var g = new GameObject(GardenName).transform;
            g.SetParent(root, false);
            GameObjectUtility.SetStaticEditorFlags(g.gameObject, StaticEditorFlags.BatchingStatic);

            var cypress = new[]
            {
                new Vector2(-34f, 45.5f), new Vector2(-18f, 45.5f), new Vector2(-6f, 45.5f), new Vector2(6f, 45.5f), new Vector2(18f, 45.5f),
                new Vector2(-38f, -45.5f), new Vector2(-20f, -45.5f), new Vector2(20f, -45.5f), new Vector2(38f, -45.5f),
                new Vector2(-45.5f, -36f), new Vector2(-45.5f, -24f), new Vector2(-45.5f, 30f),
                new Vector2(45.5f, -36f), new Vector2(45.5f, -24f), new Vector2(45.5f, 30f),
            };
            int i = 0;
            foreach (var p in cypress) Tree(g, "Cypress", p, 8f + (i % 3) * 0.6f, (i++ * 47f) % 360f, 0.5f);
            i = 0;
            foreach (var p in new[] { new Vector2(-20f, -40f), new Vector2(20f, -40f), new Vector2(-38f, 45f), new Vector2(44f, 12f) })
                Tree(g, "DeadTree", p, 6.5f + (i % 2) * 0.8f, (i++ * 73f) % 360f, 0.45f);
            i = 0;
            foreach (var p in new[] { new Vector2(-28f, -44.5f), new Vector2(26f, -45f), new Vector2(44.5f, -18f), new Vector2(-45f, -16f), new Vector2(-24f, 44.5f) })
                Rock(g, p, 1.2f + (i % 3) * 0.35f, (i++ * 61f) % 360f);
            foreach (var (p, yaw) in new[] { (new Vector2(-7f, 34f), 0f), (new Vector2(7f, 34f), 0f), (new Vector2(-13f, -45.2f), 0f), (new Vector2(13f, -45.2f), 0f) })
                Hedge(g, p, yaw);
        }

        private static void Tree(Transform g, string key, Vector2 at, float height, float yaw, float trunkR)
        {
            var go = PlaceFree(g, key, at, yaw, h => height / h);
            if (go == null) return;
            var col = go.AddComponent<CapsuleCollider>();
            var s = go.transform.lossyScale;
            col.radius = trunkR / Mathf.Max(s.x, 0.001f);
            col.height = height / Mathf.Max(s.y, 0.001f);
            col.center = new Vector3(0f, col.height / 2f, 0f) + ModelPivotOffset(go);
        }

        private static void Rock(Transform g, Vector2 at, float height, float yaw)
        {
            var go = PlaceFree(g, "Rock", at, yaw, h => height / h);
            if (go != null) AddBoundsBox(go);
        }

        private static void Hedge(Transform g, Vector2 at, float yaw)
        {
            var go = PlaceFree(g, "Hedge", at, yaw, h => 1.2f / h);
            if (go != null) AddBoundsBox(go);
        }

        /// <summary>정원 모델: 바닥(y 0) 위 at에, 높이 비율 scaleFromHeight로 같은 비율 크기.</summary>
        private static GameObject PlaceFree(Transform g, string key, Vector2 at, float yaw, System.Func<float, float> scaleFromHeight)
        {
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return null; }
            var go = Instantiate(prefab, g, $"{ModelPrefix}{key}_{at.x:0}_{at.y:0}");
            var mb = ModelBounds(go);
            float s = scaleFromHeight(Mathf.Max(mb.size.y, 0.001f));
            go.transform.localScale = Vector3.one * s;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var pivot = new Vector3(mb.center.x, mb.min.y, mb.center.z) * s;
            go.transform.localPosition = new Vector3(at.x, 0f, at.y) - go.transform.localRotation * pivot;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            placed++;
            return go;
        }

        private static Vector3 ModelPivotOffset(GameObject go)
        {
            var mb = ModelBounds(go);
            return new Vector3(mb.center.x, mb.min.y, mb.center.z);
        }

        private static void AddBoundsBox(GameObject go)
        {
            var mb = ModelBounds(go);
            var col = go.AddComponent<BoxCollider>();
            col.center = mb.center;
            col.size = mb.size;
        }
    }
}

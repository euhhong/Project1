using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// MapDresser 추가분(BG): VARCO 3D로 새로 만든 소품으로 블록 그대로이던 것을 바꾸고, 장식·자연물·판자 구역을 더 놓는다.
    /// - 바꿈(블록 렌더러만 끄고 충돌은 그대로): 예배당 긴 의자(Pew_ + PewBack_ 한 쌍) → Pew, 제단(Chapel_Altar) → Altar,
    ///   계단 단(Step_, 계단 그룹 아래) → Step, 기둥(Columns 그룹·Pillar_·Porch_) → Column.
    /// - 예배당(3층): 맨 앞 벽에 큰 십자가(Cross), 오른쪽 앞 강단(Pulpit), 제단 양옆 촛대(Candelabra), 옆·뒤 벽에 액자(Frame — 벽을 찾아 붙임).
    /// - 야외: 크기가 다른 나무(TreeLarge·TreeSmall), 낮고 넓적한 바위(FlatRock), 흙바닥에 풀(Grass, 충돌 없음).
    ///   놓기 전에 자리가 비었는지(맵 블록과 겹치지 않는지)·지붕 아래가 아닌지 보고 막히면 건너뛴다.
    /// - 판자 구역(야외 동쪽 빈터): 무너진 벽(RuinWall) – 판자 – 무너진 벽 두 줄 + 나무통(Barrel). 판자는 PalletPlacer가 놓는다.
    /// - 처음 가운데 있던 연습용 벽(씬 루트 Wall)을 없애고 우물(Well, 씬 루트 PracticeWell)로 바꾼다 — 훈련 상대는 우물 옆에 선다.
    /// 모델: Assets/Environment/Models/VARCO/{Key}/{Key}.fbx(Tools/Blender/prep_env_prop.py로 정리). 없는 모델은 건너뛴다.
    /// </summary>
    public static partial class MapDresser
    {
        public const string PracticeWellName = "PracticeWell";
        private const string ExtrasName = "VARCO_Extras";

        // 판자 구역: 무너진 벽 자리(가운데 x, z, 길이 x방향, 높이) — PalletPlacer의 판자 두 개가 벽 사이 틈(2m)에 선다.
        public static readonly Vector3 PalletZoneCenter = new(32f, 0f, -23.5f);
        private static readonly (float x, float z, float len)[] ZoneWalls =
        {
            (29.5f, -21.0f, 4.0f), (35.25f, -21.0f, 3.5f),   // 틈 x 31.5 ~ 33.5
            (29.0f, -26.0f, 3.0f), (34.5f, -26.0f, 4.0f),    // 틈 x 30.5 ~ 32.5
            // 야외 구석 판자 구역 4곳(가운데 x, z) — 두 줄(z −2.5 / +2.5), 틈 x −1~+1 / −2~0(엇갈림). 판자는 PalletPlacer
            (13.25f, 29.5f, 3.5f), (18.5f, 29.5f, 3.0f), (12.75f, 34.5f, 2.5f), (18.0f, 34.5f, 4.0f),          // 북동 (16, 32)
            (29.25f, -44.5f, 3.5f), (34.5f, -44.5f, 3.0f), (28.75f, -39.5f, 2.5f), (34.0f, -39.5f, 4.0f),      // 남동 담장 구석 (32, −42)
            (-14.75f, 37.5f, 3.5f), (-9.5f, 37.5f, 3.0f), (-15.25f, 42.5f, 2.5f), (-10.0f, 42.5f, 4.0f),       // 북서 (−12, 40)
            (-34.75f, -6.5f, 3.5f), (-29.5f, -6.5f, 3.0f), (-35.25f, -1.5f, 2.5f), (-30.0f, -1.5f, 4.0f),      // 서쪽 가장자리 (−32, −4)
        };
        private const float ZoneWallHeight = 2.0f, ZoneWallThick = 0.7f;

        // ───────────────────────────── 블록 바꾸기 ─────────────────────────────

        /// <summary>새 소품으로 바꿀 블록이면 바꾸고 true.</summary>
        private static bool DressExtraBox(Transform t)
        {
            string n = t.name;
            if (IsRailBox(t)) { FitRailing(t); return true; } // 계단 둘레·2층 회랑·발코니·테라스 난간(허리 높이 얇은 벽) → 철창 난간
            if (n.StartsWith("PewBack_")) return true; // Pew_ 쪽에서 함께 처리
            if (n.StartsWith("Pew_")) { FitPew(t); return true; }
            if (n == "Chapel_Altar") { FitBox(t, "Altar"); return true; }
            if (n.StartsWith("Step_") && UnderStairs(t)) { FitStepCap(t); return true; }
            if (t.parent != null && t.parent.name == "Columns") { FitBox(t, "Column"); return true; }
            if (n.StartsWith("Pillar_") || n.StartsWith("Porch_")) { FitBox(t, "Column"); return true; }
            // 맨 큐브로 남아 있던 가구·구조물 → VARCO 모델(충돌 블록은 그대로)
            if (n.StartsWith("Bunk")) { FitLong(t, "Bunk"); return true; }
            if (n.StartsWith("ReadingDesk")) { FitLong(t, "ReadingDesk"); return true; }
            if (n.StartsWith("Workbench")) { FitLong(t, "Workbench"); return true; }
            if (n.StartsWith("Kitchen_Island")) { FitLong(t, "KitchenIsland"); return true; }
            if (n.StartsWith("Shelf_")) { FitLong(t, "Bookshelf"); return true; }
            if (n == "Bed_Head") return true; // Bed 쪽에서 함께 처리
            if (n == "Bed") { FitPair(t, "Bed_Head", "Bed"); return true; }
            if (n.StartsWith("Plinth_")) { FitBox(t, "Plinth"); return true; }
            if (n == "Chapel_AltarStep") { FitLong(t, "Dais"); return true; }
            if (IsLowWall(t)) { FitSegments(t, "LowStoneWall", 2.6f, 0f); return true; }
            if (n == "Arch_Pier_N") return true; // Arch_Pier_S 쪽에서 함께 처리
            if (n == "Arch_Pier_S") { FitBridgeArch(t); return true; }
            if (n.StartsWith("Hall3_")) { FitFacing(t); return true; } // 3F 홀 가구(이름에 모델·앞 방향)
            return false;
        }


        // 키가 큰 쪽(높이)으로만 맞추는 작은 소품 — 가로·세로로 늘이면 찌그러진다
        private static readonly HashSet<string> UniformHallProps = new() { "Harp", "Globe", "BustPedestal", "ArmorStand", "Easel", "PaintingStack" };

        /// <summary>
        /// 이름 Hall3_{모델}_{번호}_{앞 방향 N/E/S/W} 상자: 모델 앞(+Z)을 그 방향(N = +Z, E = +X …)으로 돌려 상자에 맞춘다.
        /// 큰 가구는 상자 크기대로, 작은 소품(UniformHallProps)은 높이 비율로.
        /// </summary>
        private static void FitFacing(Transform box)
        {
            var parts = box.name.Split('_');
            if (parts.Length < 4) { FitBox(box, parts.Length > 1 ? parts[1] : box.name); return; }
            string key = parts[1];
            float yaw = parts[^1] switch { "E" => 90f, "S" => 180f, "W" => -90f, _ => 0f };
            var s = box.localScale;
            bool turn = Mathf.Abs(Mathf.Abs(yaw) - 90f) < 1f;
            var frameRot = box.localRotation * Quaternion.Euler(0f, yaw, 0f);
            var size = turn ? new Vector3(s.z, s.y, s.x) : s;
            var bottom = box.localPosition - box.localRotation * Vector3.up * (s.y / 2f);
            if (Place(key, box.parent, frameRot, bottom, size, UniformHallProps.Contains(key), false, $"{ModelPrefix}{key}_{box.name}") != null)
                Hide(box);
        }
        /// <summary>그룹(빈 부모)째 바꿀 것: 사다리(손잡이+발판) → Ladder, 무너진 잔해 더미 → RubblePile.</summary>
        private static bool DressExtraGroup(Transform t)
        {
            if (t.name.StartsWith("Ladder_")) { FitGroup(t, "Ladder", t.Cast<Transform>()); return true; }
            if (t.name == "Collapse_Rubble") { FitGroup(t, "RubblePile", t.Cast<Transform>()); return true; }
            return false;
        }

        /// <summary>낮은 돌담: 묘지 담(Wall_N/E/S/W 아래)·3층 낮은 벽(LowWall_)·테라스 낮은 벽 — 높이 0.8~1.5m, 두께 0.3~0.6m.</summary>
        private static bool IsLowWall(Transform t)
        {
            var s = t.localScale;
            float thin = Mathf.Min(s.x, s.z);
            if (s.y < 0.8f || s.y > 1.5f || thin < 0.3f || thin > 0.6f) return false;
            string p = t.parent != null ? t.parent.name : "";
            return t.name.StartsWith("LowWall") || t.name.StartsWith("Terrace_LowWall") || p.StartsWith("LowWall") || ((p is "Wall_N" or "Wall_E" or "Wall_S" or "Wall_W") && t.parent.parent != null && t.parent.parent.name == "Graveyard");
        }

        /// <summary>
        /// 연결동 아래 돌 아치: 남·북 교각(Arch_Pier_S/N)과 그 사이(2층 바닥 아래까지)를 감싸는 자리에 BridgeArch 모델 하나 —
        /// 아치 폭(모델의 긴 쪽)이 남북(건물 사이)으로, 트인 아치 구멍으로 동서로 지나다닌다. 교각 블록은 끈다(충돌은 그대로).
        /// </summary>
        private static void FitBridgeArch(Transform south)
        {
            var north = south.parent != null ? south.parent.Find("Arch_Pier_N") : null;
            var prefab = LoadModel("BridgeArch");
            if (prefab == null || north == null) { missing++; return; }
            var b = LocalBounds(south.parent, new List<Transform> { south, north });
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            bool modelLongZ = mr != null && mr.bounds.size.z > mr.bounds.size.x;
            bool spanZ = b.size.z >= b.size.x; // 아치가 걸치는 방향(건물 사이)
            var rot = modelLongZ != spanZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            var size = modelLongZ != spanZ ? new Vector3(b.size.z, b.size.y, b.size.x) : b.size;
            var bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            if (Place("BridgeArch", south.parent, rot, bottom, size, false, false, $"{ModelPrefix}BridgeArch_{south.parent.name}") != null)
            { Hide(south); Hide(north); }
        }

        /// <summary>두 상자(몸 + 머리판 등)를 감싸는 자리에 모델 하나. 머리판이 +Z 쪽이면 모델 앞(+Z)을 반대로.</summary>
        private static void FitPair(Transform body, string partnerName, string key)
        {
            var partner = body.parent != null ? body.parent.Find(partnerName) : null;
            var boxes = new List<Transform> { body };
            if (partner != null) boxes.Add(partner);
            var b = LocalBounds(body.parent, boxes);
            var bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            bool headPlusZ = partner == null || partner.localPosition.z >= body.localPosition.z;
            if (Place(key, body.parent, Quaternion.Euler(0f, headPlusZ ? 180f : 0f, 0f), bottom, b.size, false, false, $"{ModelPrefix}{key}_{body.name}") != null)
                foreach (var x in boxes) Hide(x);
        }

        /// <summary>긴 상자를 seg m 안팎 칸으로 나눠 모델을 늘어놓고 상자는 끈다(양 끝 ext m씩 더 — 이음매 없게).</summary>
        private static void FitSegments(Transform box, string key, float segLen, float ext)
        {
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return; }
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            bool modelLongZ = mr != null && mr.bounds.size.z > mr.bounds.size.x;
            var s = box.localScale;
            bool boxLongZ = s.z > s.x;
            float len = (boxLongZ ? s.z : s.x) + ext * 2f, thick = boxLongZ ? s.x : s.z;
            int count = Mathf.Max(1, Mathf.RoundToInt(len / segLen));
            float seg = len / count;
            var along = box.localRotation * (boxLongZ ? Vector3.forward : Vector3.right);
            var frameRot = box.localRotation * (modelLongZ != boxLongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            var size = modelLongZ ? new Vector3(thick, s.y, seg + 0.03f) : new Vector3(seg + 0.03f, s.y, thick);
            var baseBottom = box.localPosition - box.localRotation * Vector3.up * (s.y / 2f);
            for (int i = 0; i < count; i++)
                Place(key, box.parent, frameRot, baseBottom + along * (-len / 2f + seg * (i + 0.5f)), size, false, false, $"{ModelPrefix}{key}_{box.name}_{i}");
            Hide(box);
        }

        /// <summary>사물함 모양을 섞는다: 그대로 55%, 문이 반쯤 뜯긴 것 15%, 문이 열린 것 15%, 찌그러진 것 15%(이름으로 정해 늘 같음).</summary>
        public static string LockerVariant(string name)
        {
            float h = Hash(name.Length * 3.1f, name.GetHashCode() % 997);
            string k = h < 0.55f ? "Locker" : h < 0.7f ? "LockerBroken" : h < 0.85f ? "LockerOpen" : "LockerDented";
            return LoadModel(k) != null ? k : "Locker";
        }

        /// <summary>난간 상자: 허리 높이(0.8~1.2m), 얇음(0.25m 이하), 길이 0.5m 이상. 사다리 손잡이(높고 가늚)는 뺀다.</summary>
        private static bool IsRailBox(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.StartsWith("Ladder")) return false;
            var s = t.localScale;
            float thin = Mathf.Min(s.x, s.z), len = Mathf.Max(s.x, s.z);
            return s.y >= 0.8f && s.y <= 1.2f && thin <= 0.25f && len >= 0.5f;
        }

        /// <summary>난간 상자를 2.2m 안팎 칸으로 나눠 철창 난간(Railing) 모델을 늘어놓고 상자는 끈다(충돌은 그대로).</summary>
        private static void FitRailing(Transform box)
        {
            var prefab = LoadModel("Railing");
            if (prefab == null) { missing++; return; }
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            bool modelLongZ = mr != null && mr.bounds.size.z > mr.bounds.size.x;
            var s = box.localScale;
            bool boxLongZ = s.z > s.x;
            float len = (boxLongZ ? s.z : s.x) + 0.6f; // 양 끝을 0.3m씩 더 늘여 기둥·벽 속으로 파고들게(난간과 기둥 사이 틈 없음)
            int count = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
            float seg = len / count;
            var along = box.localRotation * (boxLongZ ? Vector3.forward : Vector3.right);
            var frameRot = box.localRotation * (modelLongZ != boxLongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            var size = modelLongZ ? new Vector3(0.22f, s.y, seg + 0.02f) : new Vector3(seg + 0.02f, s.y, 0.22f);
            var baseBottom = box.localPosition - box.localRotation * Vector3.up * (s.y / 2f);
            for (int i = 0; i < count; i++)
                Place("Railing", box.parent, frameRot, baseBottom + along * (-len / 2f + seg * (i + 0.5f)), size, false, false, $"{ModelPrefix}Railing_{box.name}_{i}");
            Hide(box);
        }

        private static bool UnderStairs(Transform t)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == "Stairs" || p.name.StartsWith("Stair_")) return true;
            return false;
        }

        /// <summary>긴 의자: 앉는 판(Pew_*)과 등받이(PewBack_*)를 감싸는 자리에 Pew 하나. 등받이가 +Z 쪽 → 모델 앞(+Z)을 반대로.</summary>
        private static void FitPew(Transform seat)
        {
            var back = seat.parent != null ? seat.parent.Find("PewBack_" + seat.name.Substring("Pew_".Length)) : null;
            var boxes = new List<Transform> { seat };
            if (back != null) boxes.Add(back);
            var b = LocalBounds(seat.parent, boxes);
            var bottom = new Vector3(b.center.x, b.min.y, b.center.z);
            bool backAtPlusZ = back == null || back.localPosition.z >= seat.localPosition.z;
            var rot = Quaternion.Euler(0f, backAtPlusZ ? 180f : 0f, 0f);
            if (Place("Pew", seat.parent, rot, bottom, b.size, false, false, $"{ModelPrefix}Pew_{seat.name}") != null)
                foreach (var x in boxes) Hide(x);
        }

        /// <summary>
        /// 계단 단: 블록(바닥부터 디딤면까지 이어진 상자)은 그대로 두어 옆에서 보면 한 덩어리로 이어지게 하고,
        /// 맨 위에만 얇은 돌 디딤판(Step 모델, 두께 capHeight)을 얹는다 — 모델을 상자 높이만큼 늘이면 모서리가 둥글어 단마다 틈이 보인다.
        /// 디딤판은 앞뒤로 조금 길게(겹쳐서 단 사이 이음매가 안 보이게).
        /// </summary>
        private static void FitStepCap(Transform box)
        {
            const float capHeight = 0.18f, overlap = 0.06f;
            var s = box.localScale;
            var prefab = LoadModel("Step");
            if (prefab == null) { missing++; return; }
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            bool modelLongZ = mr != null && mr.bounds.size.z > mr.bounds.size.x;
            bool boxLongZ = s.z > s.x;
            bool turn = modelLongZ != boxLongZ;
            var frameRot = box.localRotation * (turn ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            var top = box.localPosition + box.localRotation * Vector3.up * (s.y / 2f);
            var bottom = top - box.localRotation * Vector3.up * (capHeight - 0.02f); // 디딤판 윗면이 블록 윗면보다 2cm 위
            var size = turn ? new Vector3(s.z + overlap, capHeight, s.x + overlap) : new Vector3(s.x + overlap, capHeight, s.z + overlap);
            Place("Step", box.parent, frameRot, bottom, size, false, false, $"{ModelPrefix}Step_{box.name}"); // 블록은 끄지 않는다(옆면 = 이어진 블록)
        }

        /// <summary>긴 상자(계단 단 등): 모델의 긴 쪽을 상자의 긴 쪽에 맞춘다(늘어나 찌그러지지 않게).</summary>
        private static void FitLong(Transform box, string key)
        {
            var s = box.localScale;
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return; }
            var mr = prefab.GetComponentInChildren<MeshRenderer>();
            bool modelLongZ = mr != null && mr.bounds.size.z > mr.bounds.size.x; // 모델 경계(프리팹, 회전 포함)
            bool boxLongZ = s.z > s.x;
            bool turn = modelLongZ != boxLongZ;
            var frameRot = box.localRotation * (turn ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity);
            var size = turn ? new Vector3(s.z, s.y, s.x) : s;
            var bottom = box.localPosition - box.localRotation * Vector3.up * (s.y / 2f);
            if (Place(key, box.parent, frameRot, bottom, size, false, false, $"{ModelPrefix}{key}_{box.name}") != null)
                Hide(box);
        }

        // ───────────────────────────── 더 놓기 ─────────────────────────────

        private static void BuildExtras(Transform root)
        {
            var g = new GameObject(ExtrasName).transform;
            g.SetParent(root, false);
            Physics.SyncTransforms();
            DressWindows(root);                   // 네모난 창문 구멍마다 고딕 돌 창틀(MapDresserWindows)
            BuildChapel(g);
            BuildNature(g);
            BuildPalletZone(g);
            BuildMirrors(g);                      // 맵 네 구석 순간이동 전신거울(MapDresserMirrors)
            BuildCenter(root.gameObject.scene);   // 가운데: 긴 벽 – 판자 – 긴 벽, 우물은 다른 빈터로(MapDresserScatter)
            Physics.SyncTransforms();
            BuildScatter(g);                      // 술래잡기용 가구·벽 앞 소품 흩뿌리기(MapDresserScatter)
        }

        private static void BuildChapel(Transform g)
        {
            const float floor = 12.15f;
            // 예배실 = 본채 로컬 x 0..12(넓힘), 월드 x -22..-10 — 가운데 x -16
            // 맨 앞 벽(z -19.8) 가운데 큰 십자가 — 제단 위
            WallItem(g, "Cross", new Vector3(-16f, floor + 3.6f, -17.5f), Vector3.back, 2.4f, 0.04f);
            // 강단: 왼쪽(서) 앞, 신자석 쪽을 보며 살짝 가운데로(동쪽 앞은 복도 문)
            PlaceAt(g, "Pulpit", new Vector3(-20.6f, floor, -16.4f), 20f, 1.35f, collider: true);
            // 제단 단 양옆 큰 촛대
            PlaceAt(g, "Candelabra", new Vector3(-20.3f, floor, -18.7f), 0f, 1.9f, collider: true);
            PlaceAt(g, "Candelabra", new Vector3(-11.7f, floor, -18.7f), 0f, 1.9f, collider: true);
            // 액자: 서쪽 벽 네 점, 뒷벽 세 점, 동쪽 벽 문 사이 두 점(벽을 찾아 붙인다)
            foreach (float z in new[] { -14.4f, -10.8f, -7.2f, -4.2f })
                WallItem(g, "Frame", new Vector3(-20.5f, floor + 2.3f, z), Vector3.left, 1.1f, 0.03f);
            foreach (float x in new[] { -16.5f, -13.5f, -11f })
                WallItem(g, "Frame", new Vector3(x, floor + 2.4f, -3.5f), Vector3.forward, 1.2f, 0.03f);
            foreach (float z in new[] { -13.5f, -8.5f })
                WallItem(g, "Frame", new Vector3(-11f, floor + 2.3f, z), Vector3.right, 1.1f, 0.03f);
        }

        private static void BuildNature(Transform g)
        {
            // 나무(높이 m, 줄기 반지름 m)
            var large = new (float x, float z, float h)[] { (-34f, -23.5f, 9.5f), (-19f, 38f, 8.5f), (36.5f, -37f, 10f), (-41.5f, 3.5f, 7.5f), (40f, 2f, 8f) };
            var small = new (float x, float z, float h)[] { (-8.5f, 18.5f, 5f), (8f, 18.5f, 4.5f), (18.5f, 36.5f, 5.5f), (2f, 30.5f, 4f), (-37f, -37f, 6f), (32f, -4f, 5f), (-26f, 33f, 4.5f) };
            int i = 0;
            foreach (var t in large) OutdoorTree(g, "TreeLarge", t.x, t.z, t.h, 0.55f, (i++ * 67f) % 360f);
            foreach (var t in small) OutdoorTree(g, "TreeSmall", t.x, t.z, t.h, 0.18f, (i++ * 67f) % 360f);
            // 낮고 넓적한 바위(폭 m, 높이 m)
            var rocks = new (float x, float z, float w, float h)[]
            {
                (-34f, -8.5f, 2.6f, 0.5f), (-37f, -30f, 3.0f, 0.45f), (37f, -30f, 2.4f, 0.55f), (-26f, 26f, 2.2f, 0.4f),
                (24f, 30f, 2.8f, 0.5f), (-40f, 28f, 2.4f, 0.45f), (41f, 8f, 2.6f, 0.5f), (0f, 39f, 3.2f, 0.5f), (-14f, 40f, 2.2f, 0.4f),
            };
            i = 0;
            foreach (var r in rocks) OutdoorFlat(g, "FlatRock", r.x, r.z, r.w, r.h, (i++ * 53f) % 360f);
            // 풀: 흙바닥(Environment)에만, 3.5m 격자에 흩뿌림(충돌 없음)
            int grass = 0;
            for (float x = -46f; x <= 46f; x += 2.6f)
                for (float z = -46f; z <= 46f; z += 2.6f)
                {
                    float h1 = Hash(x, z), h2 = Hash(z * 1.7f, x * 0.3f);
                    if (h1 > 0.55f) continue;
                    var p = new Vector3(x + (h2 - 0.5f) * 2.4f, 0f, z + (Hash(x + 3f, z) - 0.5f) * 2.4f);
                    if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.transform.root.name != "Environment" || hit.normal.y < 0.85f) continue; // 흙바닥만
                    if (Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore)) continue; // 지붕 아래 아님
                    if (Blocked(hit.point, new Vector3(0.6f, 0.5f, 0.6f), 0f)) continue;
                    // 풀 모델 여러 개(Grass/Grass_01~NN: 흙 조각 + 실오라기 같은 풀 여러 조합) 중 하나, 크기 0.8~1.25배
                    string variant = $"Grass/Grass_{1 + Mathf.FloorToInt(Hash(z * 0.7f, x * 1.3f) * GrassVariants):00}";
                    var go = PlaceScaled(g, variant, hit.point, h2 * 360f, 0.8f + h1 * 0.45f);
                    if (go != null) grass++;
                    if (grass >= 420) break;
                }
        }

        /// <summary>무너진 벽 – 판자 – 무너진 벽 두 줄 + 나무통(장식). 판자는 PalletPlacer.</summary>
        private static void BuildPalletZone(Transform g)
        {
            var zone = new GameObject($"{ModelPrefix}PalletZone").transform;
            zone.SetParent(g, false);
            foreach (var w in ZoneWalls)
            {
                var p = Ground(new Vector3(w.x, 0f, w.z));
                var go = Place("RuinWall", zone, Quaternion.identity, zone.InverseTransformPoint(p), new Vector3(w.len, ZoneWallHeight, ZoneWallThick), false, false, $"{ModelPrefix}RuinWall_{w.x:0.#}_{w.z:0.#}");
                if (go == null) continue;
                var col = go.AddComponent<BoxCollider>(); // 막는 판정 = 벽 자리 상자 그대로(모델 모양과 상관없이 고르게)
                var s = go.transform.lossyScale;
                var local = go.transform.InverseTransformPoint(p + Vector3.up * (ZoneWallHeight / 2f));
                col.center = local;
                col.size = new Vector3(w.len / Mathf.Max(Mathf.Abs(s.x), 1e-3f), ZoneWallHeight / Mathf.Max(Mathf.Abs(s.y), 1e-3f), ZoneWallThick / Mathf.Max(Mathf.Abs(s.z), 1e-3f));
            }
            foreach (var b in new[] { new Vector3(26.9f, 0f, -21.3f), new Vector3(27.0f, 0f, -20.5f), new Vector3(37.6f, 0f, -26.2f) })
                PlaceAt(zone, "Barrel", Ground(b), Hash(b.x, b.z) * 360f, 1.05f, collider: true);
        }

        /// <summary>처음 가운데 있던 연습용 벽(씬 루트 Wall)을 없애고 같은 자리에 우물(PracticeWell)을 세운다.</summary>
        private static void ReplacePracticeWall(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots.Where(r => r.name == "Wall" || r.name == PracticeWellName).ToArray()) Object.DestroyImmediate(r);
            var prefab = LoadModel("Well");
            if (prefab == null) { missing++; return; }
            var root = new GameObject(PracticeWellName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Ground(new Vector3(0f, 0f, 12f));
            var go = PlaceAt(root.transform, "Well", root.transform.position, 0f, 2.6f, collider: true);
            if (go == null) Object.DestroyImmediate(root);
        }

        // ───────────────────────────── 도우미 ─────────────────────────────

        private const int GrassVariants = 16;

        /// <summary>world 자리(바닥)에 모델을 원래 크기 × scale로 놓는다(충돌 없음) — 풀처럼 모델 크기가 곧 실제 크기인 것.</summary>
        private static GameObject PlaceScaled(Transform parent, string key, Vector3 world, float yaw, float scale)
        {
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return null; }
            var go = Instantiate(prefab, parent, $"{ModelPrefix}{key.Replace('/', '_')}_{world.x:0.#}_{world.z:0.#}");
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localPosition = parent.InverseTransformPoint(world);
            placed++;
            return go;
        }

        /// <summary>world 자리(바닥)에 모델을 높이 height로 같은 비율로 세운다(parent 아래). collider = 모델 경계 상자.</summary>
        private static GameObject PlaceAt(Transform parent, string key, Vector3 world, float yaw, float height, bool collider)
        {
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return null; }
            var go = Instantiate(prefab, parent, $"{ModelPrefix}{key}_{world.x:0.#}_{world.z:0.#}");
            var mb = ModelBounds(go);
            float s = height / Mathf.Max(mb.size.y, 0.001f);
            go.transform.localScale = Vector3.one * s;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var pivot = new Vector3(mb.center.x, mb.min.y, mb.center.z) * s;
            go.transform.localPosition = parent.InverseTransformPoint(world) - go.transform.localRotation * pivot;
            if (collider) AddBoundsBox(go);
            placed++;
            return go;
        }

        /// <summary>나무: 빈 자리(줄기 둘레)·지붕 아래가 아니면 세우고 줄기 캡슐 충돌.</summary>
        private static void OutdoorTree(Transform g, string key, float x, float z, float height, float trunkR, float yaw)
        {
            var p = Ground(new Vector3(x, 0f, z));
            if (Blocked(p, new Vector3(trunkR * 4f, 2f, trunkR * 4f), 0f) || Physics.Raycast(p + Vector3.up * 0.5f, Vector3.up, 30f, ~0, QueryTriggerInteraction.Ignore)) return;
            var go = PlaceAt(g, key, p, yaw, height, collider: false);
            if (go == null) return;
            var col = go.AddComponent<CapsuleCollider>();
            var s = go.transform.lossyScale;
            col.radius = trunkR / Mathf.Max(s.x, 0.001f);
            col.height = height * 0.6f / Mathf.Max(s.y, 0.001f);
            col.center = go.transform.InverseTransformPoint(p + Vector3.up * height * 0.3f);
        }

        /// <summary>낮고 넓적한 바위: 폭 w·높이 h로 늘려 놓는다(빈 자리만), 경계 상자 충돌.</summary>
        private static void OutdoorFlat(Transform g, string key, float x, float z, float w, float h, float yaw)
        {
            var p = Ground(new Vector3(x, 0f, z));
            if (Blocked(p, new Vector3(w, 0.8f, w), yaw)) return;
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return; }
            var go = Instantiate(prefab, g, $"{ModelPrefix}{key}_{x:0}_{z:0}");
            var mb = ModelBounds(go);
            var sc = new Vector3(w / Mathf.Max(mb.size.x, 1e-3f), h / Mathf.Max(mb.size.y, 1e-3f), w / Mathf.Max(mb.size.z, 1e-3f));
            go.transform.localScale = sc;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localPosition = g.InverseTransformPoint(p) - go.transform.localRotation * Vector3.Scale(sc, new Vector3(mb.center.x, mb.min.y, mb.center.z));
            AddBoundsBox(go);
            placed++;
        }

        /// <summary>벽에 거는 것(십자가·액자): from에서 dir 쪽으로 벽을 찾아 벽면에 붙이고 앞(+Z)이 방 안을 보게.</summary>
        private static void WallItem(Transform g, string key, Vector3 from, Vector3 dir, float height, float gap)
        {
            if (!Physics.Raycast(from, dir, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return;
            var prefab = LoadModel(key);
            if (prefab == null) { missing++; return; }
            var go = Instantiate(prefab, g, $"{ModelPrefix}{key}_{hit.point.x:0.#}_{hit.point.z:0.#}");
            var mb = ModelBounds(go);
            float s = height / Mathf.Max(mb.size.y, 0.001f);
            var face = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized; // 방 안 쪽
            go.transform.localScale = Vector3.one * s;
            go.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
            // 가운데를 맞힌 자리에, 뒷면(모델 -Z 끝)이 벽에 닿게
            var centerLocal = new Vector3(mb.center.x, mb.center.y, mb.min.z) * s;
            go.transform.position = hit.point + face * gap - go.transform.rotation * centerLocal;
            placed++;
        }

        /// <summary>p(바닥)를 가운데로 한 상자(크기 size, 바닥에서 위로)가 맵 블록·소품과 겹치는지(바닥 지형은 뺌).</summary>
        private static bool Blocked(Vector3 p, Vector3 size, float yaw)
        {
            var c = p + Vector3.up * (size.y / 2f + 0.15f);
            foreach (var col in Physics.OverlapBox(c, size / 2f, Quaternion.Euler(0f, yaw, 0f), ~0, QueryTriggerInteraction.Ignore))
            {
                string r = col.transform.root.name;
                if (r == "Environment") continue;
                if (r == "MapBlockout" || r == "Pallets" || r == PracticeWellName) return true;
            }
            return false;
        }

        private static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return new Vector3(p.x, 0f, p.z);
        }

        private static float Hash(float x, float z)
        {
            float h = Mathf.Sin(x * 12.9898f + z * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}

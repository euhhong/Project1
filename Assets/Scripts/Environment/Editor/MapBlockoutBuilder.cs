using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 술래잡기(감시자 1 : 생존자) 맵 회색 블록아웃: 고딕 저택 3동. 메뉴: Tools/BG/맵 블록아웃 다시 만들기.
    /// 씬의 "MapBlockout" 하나를 지우고 다시 세운다(다른 씬 오브젝트는 건드리지 않음). 맵을 바꾸려면 이 파일 숫자를 고치고 다시 실행한다.
    ///
    /// 설계 원칙: 방 - 복도 - 방. 방은 복도(와 바깥)로만 문이 나고 방끼리 바로 잇지 않는다. 목적 없는 빈 공간은 두지 않는다.
    ///   복도는 고리로 이어져(로비 둘레·건물 사이 연결동·바깥 띠) 추격이 끊기지 않는다.
    /// 크기 기준: 캐릭터 키 약 2.1m(감시자 ~2.7m), 점프 1.2m, stepOffset 0.3. 층고 6m(천장 5.7m), 복도 폭 3m 이상,
    ///   문 2.4×4.2m, 창 1.6m·턱 1.0m·위 4.6m(넘음), 계단 폭 3~3.5m·단 0.27m·디딤 0.4m(경사 Collider), 난간·낮은 벽 1m.
    ///   뛰어 올라서기 0.74~2.3m(상자 1.2, 식탁 1.05), 매달려 오르기 2.3~3.7m(낮은 테라스 2.4).
    ///
    /// 배치(월드, x=동, z=북): 본채(3층, 44×30, x -22..22, z -32..-2) + 서관(3층, 18×24, x -36..-18, z 6..30)
    ///   + 동관(2층 + 옥상 테라스, 18×22, x 18..36, z 6..28)이 안뜰(x -18..18, 시작 지점)을 U자로 감싼다. 연결동 2개(폭 3m).
    ///   본채: 1F 로비(식탁·의자·병풍·샹들리에, 기둥 열 너머 둘레 복도), 부엌, 락커룸, 계단실 2. 2F 로비 둘레 회랑, 침실, 락커룸.
    ///         3F 가운데 우물(로비 일부만 보임) 둘레 큰 홀(L벽·낮은 벽·병풍 엄폐), 예배실, 창고. 정문 발코니, 뒤 테라스(바깥 계단 2).
    ///   서관: 3층 높이 서재(2F·3F 벽 따라 회랑), 계단실(열람 책상). 안뜰 쪽 복도.
    ///   동관: 1F 락커룸·작업실, 2F 숙소·작업실, 옥상 테라스(본채 3F와 연결동 옥상 길로 이어짐). 안뜰 쪽 복도.
    ///   루키노 도약: 안뜰·옥상 테라스·연결동 옥상 길·서재·로비(우물 아래 제외 천장 12m)·정원은 뜀, 복도·방은 못 뜀.
    /// </summary>
    public static class MapBlockoutBuilder
    {
        private const string RootName = "MapBlockout";
        private const string MatFolder = "Assets/Environment/Materials/Blockout";

        // 높이
        private const float G = 0.15f;
        private const float Story = 6f;
        private const float SlabT = 0.3f;
        private const float F2 = G + Story;        // 6.15
        private const float F3 = F2 + Story;       // 12.15
        private const float RoofTop = F3 + Story;  // 18.15
        private const float InH = Story - SlabT;

        // 벽·개구부
        private const float WallT = 0.4f;
        private const float DoorW = 2.4f, DoorH = 4.2f;
        private const float WinW = 1.6f, WinSill = 1.0f, WinTop = 4.6f;
        private const float RailH = 1f, RailT = 0.15f;
        private const float LowTerraceH = 2.4f;

        // 계단
        private const int StairSteps = 22;
        private const float StairRun = 0.4f;
        private const float StairLen = (StairSteps - 1) * StairRun; // 8.4

        // 본채
        private const float MainW = 44f, MainD = 30f;
        private static readonly Vector3 MainOrigin = new(-22f, 0f, -32f);
        private static readonly Rect2 Atrium = new(13f, 4f, 31f, 24f);   // 로비(2F까지 트임). 서·동 복도는 x 9..13, 31..35(폭 4m)
        private static readonly Rect2 MainWell = new(17f, 9f, 27f, 19f); // 3F 바닥의 트인 곳
        private const float ChandelierY = 9.8f;
        private static readonly Rect2 MainCollapse = new(33.5f, 26f, 38f, 29.6f); // 북 복도 동쪽: 2F·3F 바닥·지붕까지 무너져 하늘로 트임(루키노 도약 자리)

        private static Material floorMat, wallMat, accentMat;

        private struct Opening
        {
            public float from, to, bottom, top;
            public Opening(float from, float to, float bottom, float top) { this.from = from; this.to = to; this.bottom = bottom; this.top = top; }
        }

        private struct Rect2
        {
            public float x0, z0, x1, z1;
            public Rect2(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
            public bool Contains(float x, float z) => x > x0 && x < x1 && z > z0 && z < z1;
        }

        private static Opening Door(float c) => new(c - DoorW / 2f, c + DoorW / 2f, 0f, DoorH);
        private static Opening MainDoor(float c) => new(c - 2.5f, c + 2.5f, 0f, 5f);
        private static Opening Win(float c) => new(c - WinW / 2f, c + WinW / 2f, WinSill, WinTop);
        private static Opening BigWin(float c) => new(c - 2f, c + 2f, 1.5f, 5.2f);
        private static Opening Gap(float from, float to) => new(from, to, 0f, 999f);
        private static Opening Notch(float c, float w, float sill) => new(c - w / 2f, c + w / 2f, sill, 999f);

        [MenuItem("Tools/BG/맵 블록아웃 다시 만들기")]
        public static void BuildMenu()
        {
            if (Build(out string msg)) Debug.Log(msg); else Debug.LogWarning(msg);
        }

        /// <summary>블록아웃을 다시 만든다. 씬 저장은 하지 않는다(더티 표시만).</summary>
        public static bool Build(out string message)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "MapBlockout: 플레이 모드에서는 만들지 않습니다. 플레이를 멈추고 다시 실행하세요.";
                return false;
            }

            floorMat = GetMat("M_Blockout_Floor", new Color(0.36f, 0.36f, 0.37f), 0.15f);
            wallMat = GetMat("M_Blockout_Wall", new Color(0.66f, 0.66f, 0.67f), 0.1f);
            accentMat = GetMat("M_Blockout_Accent", new Color(0.5f, 0.5f, 0.52f), 0.2f);

            var scene = SceneManager.GetActiveScene();
            foreach (var old in scene.GetRootGameObjects().Where(g => g.name == RootName).ToArray())
                Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build MapBlockout");

            BuildMain(Group(root, "Main", MainOrigin));
            BuildSideHall(Group(root, "Hall_West", new Vector3(-36f, 0f, 6f)), east: false, floors: 3, d: 24f);
            BuildSideHall(Group(root, "Hall_East", new Vector3(18f, 0f, 6f)), east: true, floors: 2, d: 22f);
            var connectors = Group(root, "Connectors", Vector3.zero);
            BuildConnector(connectors, "Connector_W", -22f, -18f);
            BuildConnector(connectors, "Connector_E", 18f, 22f);
            BuildGrounds(Group(root, "Grounds", Vector3.zero));
            BuildPerimeter(Group(root, "Perimeter", Vector3.zero));

            int count = root.GetComponentsInChildren<Renderer>().Length;
            EditorSceneManager.MarkSceneDirty(scene);
            message = $"MapBlockout: 블록 {count}개 생성";
            return true;
        }

        // ───────────────────────────── 본채(3층) ─────────────────────────────
        // 로컬 x 0..44(동), z 0..30(북 = 정면). 띠: 서쪽 방 x 0..9 | 서 복도 9..12 | 로비 12..32 | 동 복도 32..35 | 동쪽 방 35..44.
        //   남 복도 z 0..4(뒤 테라스 문), 북 복도 z 24..30(정문·연결동 문). 서·동 방: 계단실 z 0..12, 방 z 12..24(복도 쪽 문 2개 + 바깥 문/창).
        private static void BuildMain(Transform m)
        {
            const float W = MainW, D = MainD;
            float s0 = 1.5f, s1 = 1.5f + StairLen; // 계단실 계단 z 1.5..9.9
            var hW1 = new Rect2(0.2f, s0, 3.7f, s1); var hW2 = new Rect2(3.7f, s0, 7.2f, s1);
            var hE1 = new Rect2(W - 3.7f, s0, W - 0.2f, s1); var hE2 = new Rect2(W - 7.2f, s0, W - 3.7f, s1);

            var floors = Group(m, "Floors", Vector3.zero);
            Slab(floors, "1F_Floor", new Rect2(-0.1f, -0.1f, W + 0.1f, D + 0.1f), G, 0.35f, floorMat);
            Slab(floors, "2F_Floor", new Rect2(-0.1f, -0.1f, W + 0.1f, D + 0.1f), F2, SlabT, floorMat, Atrium, hW1, hE1, MainCollapse);
            Slab(floors, "3F_Floor", new Rect2(-0.1f, -0.1f, W + 0.1f, D + 0.1f), F3, SlabT, floorMat, MainWell, hW2, hE2, MainCollapse);
            Slab(floors, "Roof", new Rect2(-0.25f, -0.25f, W + 0.25f, D + 0.25f), RoofTop, SlabT, accentMat, MainCollapse);

            // 층마다 바깥벽(층마다 문·창이 다름) + 같은 안벽(복도 벽, 방 칸막이 — 방은 복도로만 문)
            var outerN = new[]
            {
                new[] { Door(2.5f), Win(8f), Win(14f), MainDoor(22f), Win(30f), Win(36f), Door(41.5f) },
                new[] { Door(2.5f), Win(8f), Win(14f), Door(22f), Win(30f), Win(36f), Door(41.5f) },
                new[] { Door(2.5f), Win(8f), Win(14f), BigWin(22f), Win(30f), Win(36f), Door(41.5f) },
            };
            var outerS = new[]
            {
                new[] { Door(16f), Win(22f), Door(28f) },
                new[] { Win(4.5f), Door(16f), Win(22f), Door(28f), Win(39.5f) },
                new[] { Win(4.5f), Win(16f), Win(22f), Win(28f), Win(39.5f) },
            };
            var outerSide = new[]
            {
                new[] { Win(6f), Door(18f), Win(27f) },
                new[] { Win(6f), Win(18f), Win(27f) },
                new[] { Win(6f), Win(18f), Win(27f) },
            };
            float[] fys = { G, F2, F3 };
            for (int f = 0; f < 3; f++)
            {
                float fy = fys[f], hOut = f == 0 ? Story : InH;
                var w = Group(m, $"{f + 1}F_Walls", Vector3.zero);
                WallX(w, "Outer_N", -0.2f, W + 0.2f, D, fy, hOut, WallT, wallMat, outerN[f]);
                WallX(w, "Outer_S", -0.2f, W + 0.2f, 0f, fy, hOut, WallT, wallMat, outerS[f]);
                WallZ(w, "Outer_W", 0f, D, 0f, fy, hOut, WallT, wallMat, outerSide[f]);
                WallZ(w, "Outer_E", 0f, D, W, fy, hOut, WallT, wallMat, outerSide[f]);
                // 3F 서쪽은 예배실이 북쪽 벽까지(9×18m) — 복도 쪽 문 하나 더
                if (f == 2) WallZ(w, "Corr_W", 0.2f, D - 0.2f, 9f, fy, InH, WallT, wallMat, Door(6f), Door(15f), Door(21f), Door(27f));
                else WallZ(w, "Corr_W", 0.2f, 24f, 9f, fy, InH, WallT, wallMat, Door(6f), Door(15f), Door(21f));
                WallZ(w, "Corr_E", 0.2f, 24f, W - 9f, fy, InH, WallT, wallMat, Door(6f), Door(15f), Door(21f));
                WallX(w, "W_Split", 0.2f, 9f, 12f, fy, InH, WallT, wallMat);
                if (f != 2) WallX(w, "W_North", 0.2f, 9f, 24f, fy, InH, WallT, wallMat);
                WallX(w, "E_Split", W - 9f, W - 0.2f, 12f, fy, InH, WallT, wallMat);
                WallX(w, "E_North", W - 9f, W - 0.2f, 24f, fy, InH, WallT, wallMat);
            }

            // 계단실(서·동): 1F→2F, 2F→3F 나란히. 1층 계단 꼭대기 뒤 구석은 막음.
            var st = Group(m, "Stairs", Vector3.zero);
            Stair(st, "W_1F", new Vector3(1.95f, G, s0), Vector3.forward, 3.5f, Story);
            Stair(st, "W_2F", new Vector3(5.45f, F2, s1), Vector3.back, 3.5f, Story);
            Stair(st, "E_1F", new Vector3(W - 1.95f, G, s0), Vector3.forward, 3.5f, Story);
            Stair(st, "E_2F", new Vector3(W - 5.45f, F2, s1), Vector3.back, 3.5f, Story);
            Stair(st, "Grand_W", new Vector3(15.5f, G, 4f + StairLen), Vector3.back, 3f, Story); // 로비 → 2F 남 회랑
            Stair(st, "Grand_E", new Vector3(28.5f, G, 4f + StairLen), Vector3.back, 3f, Story);
            BoxMM(st, "W_Fill", new Vector3(0.2f, G, s1), new Vector3(3.7f, F2 - SlabT, 11.8f), wallMat);
            BoxMM(st, "E_Fill", new Vector3(W - 3.7f, G, s1), new Vector3(W - 0.2f, F2 - SlabT, 11.8f), wallMat);
            WallX(st, "W_2F_Rail", 0.2f, 3.7f, s0, F2, RailH, RailT, accentMat);
            WallX(st, "E_2F_Rail", W - 3.7f, W - 0.2f, s0, F2, RailH, RailT, accentMat);
            foreach (var (x0, x1, tag) in new[] { (3.7f, 7.2f, "W"), (W - 7.2f, W - 3.7f, "E") })
            {
                WallZ(st, $"{tag}_3F_Rail_A", s0, s1, x0, F3, RailH, RailT, accentMat);
                WallZ(st, $"{tag}_3F_Rail_B", s0, s1, x1, F3, RailH, RailT, accentMat);
                WallX(st, $"{tag}_3F_Rail_N", x0, x1, s1, F3, RailH, RailT, accentMat);
            }

            // 로비 기둥 열(지붕까지 — 2F 회랑 가장자리, 3F 홀 안 기둥)
            var cols = Group(m, "Columns", Vector3.zero);
            foreach (float z in new[] { 4f, 9f, 14f, 19f, 24f })
            {
                Pillar(cols, $"W_{z:0}", Atrium.x0, z, 1f, G, RoofTop - SlabT);
                Pillar(cols, $"E_{z:0}", Atrium.x1, z, 1f, G, RoofTop - SlabT);
            }
            foreach (float x in new[] { 18f, 22f, 26f })
            {
                Pillar(cols, $"S_{x:0}", x, 4f, 1f, G, RoofTop - SlabT);
                Pillar(cols, $"N_{x:0}", x, 24f, 1f, G, RoofTop - SlabT);
            }

            BuildLobby(Group(m, "Lobby", Vector3.zero));

            // 2F 회랑 난간(로비 내려다봄, 틈 = 뛰어내림)
            var r2 = Group(m, "2F_Gallery", Vector3.zero);
            WallX(r2, "S", Atrium.x0, Atrium.x1, Atrium.z0, F2, RailH, RailT, accentMat, Gap(14f, 17f), Gap(21f, 23f), Gap(27f, 30f));
            WallX(r2, "N", Atrium.x0, Atrium.x1, Atrium.z1, F2, RailH, RailT, accentMat, Gap(21f, 23f));
            WallZ(r2, "W", Atrium.z0, Atrium.z1, Atrium.x0, F2, RailH, RailT, accentMat, Gap(13f, 15f));
            WallZ(r2, "E", Atrium.z0, Atrium.z1, Atrium.x1, F2, RailH, RailT, accentMat, Gap(13f, 15f));

            // 3F 큰 홀: 우물 난간 + 네 모서리 L벽(2.8m) + 서·동 낮은 벽(1m) + 남·북 병풍
            var h3 = Group(m, "3F_Hall", Vector3.zero);
            var wl = MainWell;
            WallX(h3, "Well_S", wl.x0, wl.x1, wl.z0, F3, RailH, RailT, accentMat);
            WallX(h3, "Well_N", wl.x0, wl.x1, wl.z1, F3, RailH, RailT, accentMat);
            WallZ(h3, "Well_W", wl.z0, wl.z1, wl.x0, F3, RailH, RailT, accentMat, Gap(13f, 15f));
            WallZ(h3, "Well_E", wl.z0, wl.z1, wl.x1, F3, RailH, RailT, accentMat, Gap(13f, 15f));
            foreach (var (cx, cz, sx, sz) in new[] { (14f, 6.5f, 1f, 1f), (30f, 6.5f, -1f, 1f), (14f, 21.5f, 1f, -1f), (30f, 21.5f, -1f, -1f) })
            {
                string tag = $"{(sz > 0 ? "S" : "N")}{(sx > 0 ? "W" : "E")}";
                WallX(h3, $"LWall_{tag}_A", Mathf.Min(cx, cx + sx * 3f), Mathf.Max(cx, cx + sx * 3f), cz, F3, 2.8f, 0.4f, wallMat);
                WallZ(h3, $"LWall_{tag}_B", Mathf.Min(cz, cz + sz * 2.5f), Mathf.Max(cz, cz + sz * 2.5f), cx, F3, 2.8f, 0.4f, wallMat);
            }
            WallZ(h3, "LowWall_W", 11.5f, 16.5f, 15f, F3, 1f, 0.4f, accentMat);
            WallZ(h3, "LowWall_E", 11.5f, 16.5f, 29f, F3, 1f, 0.4f, accentMat);
            FoldingScreen(h3, "Screen_S", new Vector3(22f - 6 * ScreenStep / 2f, F3, 6.8f), true, 6);
            FoldingScreen(h3, "Screen_N", new Vector3(22f - 6 * ScreenStep / 2f, F3, 21.2f), true, 6);

            BuildMainRooms(Group(m, "Rooms", Vector3.zero));

            // 정문 위 발코니 + 현관 기둥
            var bf = Group(m, "Balcony_Front", Vector3.zero);
            Slab(bf, "Floor", new Rect2(18f, D + 0.25f, 26f, D + 3f), F2, SlabT, floorMat);
            WallX(bf, "Rail_N", 18f, 26f, D + 3f - RailT / 2f, F2, RailH, RailT, accentMat, Gap(21f, 23f));
            WallZ(bf, "Rail_W", D + 0.25f, D + 3f, 18f + RailT / 2f, F2, RailH, RailT, accentMat);
            WallZ(bf, "Rail_E", D + 0.25f, D + 3f, 26f - RailT / 2f, F2, RailH, RailT, accentMat);
            Pillar(bf, "Porch_W", 18.4f, D + 2.6f, 0.7f, -0.3f, F2 - SlabT);
            Pillar(bf, "Porch_E", 25.6f, D + 2.6f, 0.7f, -0.3f, F2 - SlabT);

            // 뒤 테라스(2F, 남 회랑 문) + 양 끝 바깥 계단
            var rt = Group(m, "Terrace_Rear", Vector3.zero);
            Slab(rt, "Floor", new Rect2(9f, -4f, 35f, -0.25f), F2, SlabT, floorMat);
            WallX(rt, "Rail_S", 9f, 35f, -4f + RailT / 2f, F2, RailH, RailT, accentMat, Gap(14f, 16f), Gap(28f, 30f));
            WallZ(rt, "Rail_W", -4f, -3.4f, 9f + RailT / 2f, F2, RailH, RailT, accentMat);
            WallZ(rt, "Rail_E", -4f, -3.4f, 35f - RailT / 2f, F2, RailH, RailT, accentMat);
            foreach (float x in new[] { 9.5f, 22f, 34.5f })
                Pillar(rt, $"Pillar_{x:0.#}", x, -3.6f, 0.6f, -0.3f, F2 - SlabT);
            Stair(rt, "Stair_W", new Vector3(9f - StairLen, 0f, -2f), Vector3.right, 2.6f, F2);
            Stair(rt, "Stair_E", new Vector3(35f + StairLen, 0f, -2f), Vector3.left, 2.6f, F2);

            // 고딕: 박공지붕·첨탑, 뒤쪽 모서리 탑, 정면·뒷면 버팀벽
            var g = Group(m, "Gothic", Vector3.zero);
            GableRoof(g, "Roof", -0.25f, W + 0.25f, -0.25f, D + 0.25f, RoofTop, 10f, gapSide: 1, gapZ0: MainCollapse.z0 - 0.5f, gapZ1: D + 0.25f);
            Rubble(g, "Collapse_Rubble", MainCollapse, G);
            Tower(g, "Tower_SW", new Vector3(-3.2f, 0f, -3.2f)); // 뒤 바깥 계단 아래쪽을 막지 않게 바깥으로
            Tower(g, "Tower_SE", new Vector3(W + 3.2f, 0f, -3.2f));
            foreach (float x in new[] { 11f, 17.5f, 26.5f, 33f })
                BoxMM(g, $"Butt_N_{x:0.#}", new Vector3(x - 0.4f, -0.3f, D + 0.2f), new Vector3(x + 0.4f, RoofTop + 1f, D + 1.4f), wallMat);
            foreach (float x in new[] { 11f, 33f })
                BoxMM(g, $"Butt_S_{x:0.#}", new Vector3(x - 0.4f, -0.3f, -1.4f), new Vector3(x + 0.4f, RoofTop + 1f, -0.2f), wallMat);
        }

        /// <summary>본채 방(서 x 0..9, 동 x 35..44, z 12..24): 1F 부엌·락커룸, 2F 침실·락커룸, 3F 예배실·창고.</summary>
        private static void BuildMainRooms(Transform r)
        {
            float E(float x) => MainW - x; // 동쪽 방은 서쪽과 좌우 대칭 자리

            // 1F 서: 부엌(조리대 섬 1m — 넘음, 식료품 선반)
            BoxMM(r, "Kitchen_Island_A", new Vector3(2f, G, 14.2f), new Vector3(6f, G + 1f, 16f), accentMat);
            BoxMM(r, "Kitchen_Island_B", new Vector3(2f, G, 19.4f), new Vector3(6f, G + 1f, 21.2f), accentMat);
            BoxMM(r, "Kitchen_Pantry", new Vector3(0.2f, G, 22.4f), new Vector3(1.4f, G + 2.4f, 23.8f), wallMat);
            // 1F 동: 락커룸(2.2m 사물함 두 줄 + 긴 의자)
            Lockers(r, "Locker1F_A", new Vector3(E(5.5f), G, 13.4f), 10);
            Lockers(r, "Locker1F_B", new Vector3(E(3f), G, 13.4f), 10);
            BoxMM(r, "Locker1F_Bench", new Vector3(E(4.6f), G, 14f), new Vector3(E(3.9f), G + 0.45f, 22f), accentMat);

            // 2F 서: 침실(침대·옷장) / 동: 락커룸(사물함 한 줄 + 샤워 칸막이)
            BoxMM(r, "Bed", new Vector3(0.4f, F2, 15f), new Vector3(2.6f, F2 + 0.7f, 17.2f), accentMat);
            BoxMM(r, "Bed_Head", new Vector3(0.4f, F2, 14.8f), new Vector3(2.6f, F2 + 1.6f, 15f), accentMat);
            BoxMM(r, "Wardrobe", new Vector3(0.2f, F2, 21f), new Vector3(1.2f, F2 + 2.4f, 23.6f), wallMat);
            Lockers(r, "Locker2F", new Vector3(E(4.5f), F2, 13.4f), 10);
            foreach (float z in new[] { 14f, 17f, 20f })
                BoxMM(r, $"Shower_{z:0}", new Vector3(E(1.8f), F2, z), new Vector3(E(0.2f), F2 + 2.2f, z + 0.1f), wallMat);

            // 3F 서: 예배실(x 0..9, z 12..30 — 9×18m). 남쪽 제단 단 0.8m + 제단 + 촛대 기둥 2, 장의자 6줄(제단을 봄, 가운데 통로),
            //   북쪽 끝은 비워 둠(연결동 옥상 길 문). 복도 쪽 문 4개(z 6은 계단실, 15·21·27).  / 동: 창고(상자 더미)
            BoxMM(r, "Chapel_AltarStep", new Vector3(1.5f, F3 - 0.05f, 12.2f), new Vector3(7.5f, F3 + 0.8f, 14.6f), accentMat);
            BoxMM(r, "Chapel_Altar", new Vector3(3.8f, F3 + 0.8f, 12.8f), new Vector3(5.2f, F3 + 2f, 13.6f), wallMat);
            Pillar(r, "Chapel_Candle_W", 2.2f, 13.4f, 0.4f, F3 + 0.8f, F3 + 2.6f);
            Pillar(r, "Chapel_Candle_E", 6.8f, 13.4f, 0.4f, F3 + 0.8f, F3 + 2.6f);
            for (int i = 0; i < 6; i++)
            {
                float z = 16.4f + i * 1.8f;
                foreach (var (a, b) in new[] { (0.8f, 3.8f), (5f, 8f) })
                {
                    BoxMM(r, $"Pew_{i}_{a:0.#}", new Vector3(a, F3 - 0.05f, z), new Vector3(b, F3 + 0.5f, z + 0.6f), accentMat);
                    BoxMM(r, $"PewBack_{i}_{a:0.#}", new Vector3(a, F3 + 0.5f, z + 0.6f), new Vector3(b, F3 + 1.2f, z + 0.75f), accentMat);
                }
            }
            BoxMM(r, "Storage_A", new Vector3(E(7.5f), F3, 13.5f), new Vector3(E(5.5f), F3 + 1.2f, 15.5f), accentMat);
            BoxMM(r, "Storage_A_Top", new Vector3(E(7.2f), F3 + 1.2f, 13.8f), new Vector3(E(6f), F3 + 2.4f, 15f), accentMat);
            BoxMM(r, "Storage_B", new Vector3(E(3.5f), F3, 18.5f), new Vector3(E(1.5f), F3 + 1.2f, 21.5f), accentMat);
            BoxMM(r, "Storage_Rack", new Vector3(E(8.6f), F3, 22.4f), new Vector3(E(4f), F3 + 2.4f, 23.6f), wallMat);
        }

        /// <summary>로비: 긴 식탁 + 등받이 높은 의자, 식탁 둘레 병풍 4개(모서리 틈), 샹들리에(2F·3F 사이, 우물로 사슬 — 충돌 없음).</summary>
        private static void BuildLobby(Transform l)
        {
            const float tx0 = 17f, tx1 = 27f, tz0 = 12.7f, tz1 = 15.3f;
            var table = Group(l, "Table", Vector3.zero);
            BoxMM(table, "Top", new Vector3(tx0, G + 0.85f, tz0), new Vector3(tx1, G + 1.05f, tz1), accentMat);
            foreach (float x in new[] { tx0 + 0.3f, 22f, tx1 - 0.3f })
                foreach (float z in new[] { tz0 + 0.3f, tz1 - 0.3f })
                    Box(table, $"Leg_{x:0}_{z:0}", new Vector3(x, G + 0.425f, z), new Vector3(0.3f, 0.85f, 0.3f), accentMat);

            var chairs = Group(l, "Chairs", Vector3.zero);
            for (int i = 0; i < 7; i++)
            {
                float x = 18f + i * 1.5f;
                Chair(chairs, $"S_{i}", new Vector3(x, G, tz0 - 0.65f), Vector3.back);
                Chair(chairs, $"N_{i}", new Vector3(x, G, tz1 + 0.65f), Vector3.forward);
            }
            Chair(chairs, "End_W", new Vector3(tx0 - 0.65f, G, (tz0 + tz1) / 2f), Vector3.left);
            Chair(chairs, "End_E", new Vector3(tx1 + 0.65f, G, (tz0 + tz1) / 2f), Vector3.right);

            var screens = Group(l, "FoldingScreens", Vector3.zero);
            FoldingScreen(screens, "S", new Vector3(22f - 6 * ScreenStep / 2f, G, 10.2f), true, 6);
            FoldingScreen(screens, "N", new Vector3(22f - 6 * ScreenStep / 2f, G, 18.8f), true, 6);
            FoldingScreen(screens, "W", new Vector3(15.4f, G, 13.4f), false, 4);
            FoldingScreen(screens, "E", new Vector3(28.6f, G, 13.4f), false, 4);

            var ch = Group(l, "Chandelier", new Vector3(22f, ChandelierY, 14f));
            const float r = 3.5f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var p = new Vector3(r * Mathf.Cos(a * Mathf.Deg2Rad), 0f, r * Mathf.Sin(a * Mathf.Deg2Rad));
                BoxR(ch, $"Ring_{k}", p, new Vector3(2.9f, 0.3f, 0.3f), Quaternion.Euler(0f, -(90f + a), 0f), accentMat, false);
                BoxR(ch, $"Candle_{k}", p + Vector3.up * 0.45f, new Vector3(0.15f, 0.6f, 0.15f), Quaternion.identity, wallMat, false);
            }
            BoxR(ch, "Hub", Vector3.zero, new Vector3(1f, 1.2f, 1f), Quaternion.identity, accentMat, false);
            BoxR(ch, "Chain", new Vector3(0f, (RoofTop - SlabT - ChandelierY) / 2f, 0f), new Vector3(0.12f, RoofTop - SlabT - ChandelierY, 0.12f), Quaternion.identity, accentMat, false);
        }

        // ───────────────────────────── 서관·동관 ─────────────────────────────
        // 로컬(서관 기준): x 0..18(동 = 안뜰 쪽), z 0..d(북). 동관은 x를 뒤집어 안뜰 쪽 복도가 서쪽에 온다.
        //   안뜰 쪽 복도 x 15..18(바깥 문·연결동 문), 방은 x 0..15: R1 z 0..9, R2(계단실) z 9..d. 방은 복도로만 문(+ 바깥 창).
        //   서관(3층): R1 = 1층부터 3층 천장까지 트인 서재(2F·3F 벽 따라 회랑), R2 = 계단실 + 열람 책상.
        //   동관(2층): R1 = 1F 락커룸 / 2F 숙소, R2 = 계단실 + 작업대, 옥상(3F 높이) = 테라스(계단으로 오름, 연결동 옥상 길).
        private static void BuildSideHall(Transform h, bool east, int floors, float d)
        {
            const float w = 18f;
            float X(float x) => east ? w - x : x;
            Opening MX(Opening o) => east ? new Opening(w - o.to, w - o.from, o.bottom, o.top) : o;
            Rect2 R(float x0, float z0, float x1, float z1) => new(Mathf.Min(X(x0), X(x1)), z0, Mathf.Max(X(x0), X(x1)), z1);
            void HX(Transform p, string n, float xa, float xb, float z, float y0, float hh, float t, Material mat, params Opening[] ops)
                => WallX(p, n, Mathf.Min(X(xa), X(xb)), Mathf.Max(X(xa), X(xb)), z, y0, hh, t, mat, ops.Select(MX).ToArray());
            void HZ(Transform p, string n, float za, float zb, float x, float y0, float hh, float t, Material mat, params Opening[] ops)
                => WallZ(p, n, za, zb, X(x), y0, hh, t, mat, ops);
            void HB(Transform p, string n, Vector3 a, Vector3 b, Material mat)
                => BoxMM(p, n, new Vector3(Mathf.Min(X(a.x), X(b.x)), a.y, a.z), new Vector3(Mathf.Max(X(a.x), X(b.x)), b.y, b.z), mat);

            bool tall = floors == 3;
            float s0 = 10.5f, s1 = s0 + StairLen; // 18.9
            var hole1 = R(0.2f, s0, 3.7f, s1);
            var hole2 = R(3.7f, s0, 7.2f, s1);
            var lib = R(3f, 0.2f, 11f, 6.5f);
            var libCollapse = R(4f, 1f, 8.5f, 5.5f);      // 서재 지붕이 무너져 하늘이 보임(서관)
            var dormCollapse = R(10f, 3f, 13.3f, 8f);     // 락커룸·숙소 위가 옥상까지 무너짐(동관)
            const float topY = F3;                 // 2F→ 계단이 끝나는 높이(서관 3F / 동관 옥상 테라스)

            var fl = Group(h, "Floors", Vector3.zero);
            var outer = R(-0.1f, -0.1f, w + 0.1f, d + 0.1f);
            Slab(fl, "1F_Floor", outer, G, 0.35f, floorMat);
            if (tall)
            {
                Slab(fl, "2F_Floor", outer, F2, SlabT, floorMat, hole1, lib);
                Slab(fl, "3F_Floor", outer, F3, SlabT, floorMat, hole2, lib);
                Slab(fl, "Roof", R(-0.25f, -0.25f, w + 0.25f, d + 0.25f), RoofTop, SlabT, accentMat, libCollapse);
            }
            else
            {
                Slab(fl, "2F_Floor", outer, F2, SlabT, floorMat, hole1, dormCollapse);
                Slab(fl, "Roof_Terrace", R(-0.25f, -0.25f, w + 0.25f, d + 0.25f), F3, SlabT, floorMat, hole2, dormCollapse);
                Rubble(fl, "Collapse_Rubble", dormCollapse, G);
            }

            for (int f = 0; f < floors; f++)
            {
                float fy = f == 0 ? G : f == 1 ? F2 : F3, hOut = f == 0 ? Story : InH;
                var wg = Group(h, $"{f + 1}F_Walls", Vector3.zero);
                bool ground = f == 0;
                HX(wg, "Outer_S", -0.2f, w + 0.2f, 0f, fy, hOut, WallT, wallMat, Win(5f), Win(11f), Door(16.5f));
                HX(wg, "Outer_N", -0.2f, w + 0.2f, d, fy, hOut, WallT, wallMat, Win(5f), Win(11f), ground ? Door(16.5f) : Win(16.5f));
                HZ(wg, "Outer_Out", 0f, d, 0f, fy, hOut, WallT, wallMat, ground ? new[] { Win(4.5f), Win(d - 2f) } : new[] { Win(4.5f), Win(15f), Win(d - 2f) }); // 1층 z 10..20은 낮은 테라스
                HZ(wg, "Outer_Court", 0f, d, w, fy, hOut, WallT, wallMat,
                    ground ? new[] { Win(3f), Door(8f), Win(13f), Door(d - 5f) } : new[] { Win(4f), Win(10f), Win(16f), Win(d - 3f) });
                HZ(wg, "Corridor", 0.2f, d - 0.2f, 14f, fy, InH, WallT, wallMat, Door(4.5f), Door(13f), Door(d - 3.5f)); // 복도 x 14..18(폭 4m)
                HX(wg, "Split", 0.2f, 14f, 9f, fy, InH, WallT, wallMat);
            }

            var st = Group(h, "Stairs", Vector3.zero);
            Stair(st, "1F_2F", new Vector3(X(1.95f), G, s0), Vector3.forward, 3.5f, Story);
            Stair(st, "2F_Up", new Vector3(X(5.45f), F2, s1), Vector3.back, 3.5f, Story);
            HB(st, "Fill", new Vector3(0.2f, G, s1), new Vector3(3.7f, F2 - SlabT, d - 0.2f), wallMat);
            HX(st, "2F_Rail", 0.2f, 3.7f, s0, F2, RailH, RailT, accentMat);
            HZ(st, "Top_Rail_A", s0, s1, 3.7f, topY, RailH, RailT, accentMat);
            HZ(st, "Top_Rail_B", s0, s1, 7.2f, topY, RailH, RailT, accentMat);
            HX(st, "Top_Rail_N", 3.7f, 7.2f, s1, topY, RailH, RailT, accentMat);

            var fu = Group(h, "Furniture", Vector3.zero);
            if (tall)
            {
                // 서재: 2.8m 책장 줄, 2F·3F 회랑 난간(트인 곳 가장자리, 틈 = 뛰어내림)
                foreach (float x in new[] { 4.5f, 7f, 9.5f })
                    HZ(fu, $"Shelf_{x:0.#}", 1.2f, 6f, x, G, 2.8f, 0.6f, accentMat);
                foreach (var fy in new[] { F2, F3 })
                {
                    HZ(fu, $"LibRail_W_{fy:0}", 0.2f, 6.5f, 3f, fy, RailH, RailT, accentMat, Gap(2.5f, 4f));
                    HZ(fu, $"LibRail_E_{fy:0}", 0.2f, 6.5f, 11f, fy, RailH, RailT, accentMat);
                    HX(fu, $"LibRail_N_{fy:0}", 3f, 11f, 6.5f, fy, RailH, RailT, accentMat, Gap(6f, 8f));
                }
                foreach (var fy in new[] { G, F2, F3 })
                    HB(fu, $"ReadingDesk_{fy:0}", new Vector3(8.5f, fy, 13f), new Vector3(12.5f, fy + 0.9f, 15f), accentMat);
            }
            else
            {
                Lockers(fu, "Lockers_A", new Vector3(X(5f), G, 1f), 7);
                Lockers(fu, "Lockers_B", new Vector3(X(9.5f), G, 1f), 7);
                BoxMM(fu, "Locker_Bench", new Vector3(Mathf.Min(X(7f), X(7.6f)), G, 1.5f), new Vector3(Mathf.Max(X(7f), X(7.6f)), G + 0.45f, 7.5f), accentMat);
                for (int i = 0; i < 3; i++) // 2F 숙소: 침대 3개
                    HB(fu, $"Bunk_{i}", new Vector3(1f + i * 4.5f, F2, 0.6f), new Vector3(3.2f + i * 4.5f, F2 + 1.4f, 2.6f), accentMat);
                foreach (var fy in new[] { G, F2 })
                    HB(fu, $"Workbench_{fy:0}", new Vector3(8.5f, fy, 13f), new Vector3(13f, fy + 1f, 14.4f), accentMat);
                // 옥상 테라스: 둘레 난간(남쪽 안뜰 쪽 x 15..18 = 연결동 옥상 길), 굴뚝 2, 낮은 벽
                HX(fu, "Terrace_Rail_S", -0.25f, w + 0.25f, -0.25f + RailT / 2f, F3, RailH, RailT, accentMat, Gap(14f, 18.25f));
                HX(fu, "Terrace_Rail_N", -0.25f, w + 0.25f, d + 0.25f - RailT / 2f, F3, RailH, RailT, accentMat);
                HZ(fu, "Terrace_Rail_Out", -0.25f, d + 0.25f, -0.25f + RailT / 2f, F3, RailH, RailT, accentMat, Gap(6f, 8f), Gap(LadderZ - 0.7f, LadderZ + 0.7f));
                HZ(fu, "Terrace_Rail_Court", -0.25f, d + 0.25f, w + 0.25f - RailT / 2f, F3, RailH, RailT, accentMat);
                HB(fu, "Chimney_A", new Vector3(1f, F3, 3f), new Vector3(3f, F3 + 3.5f, 5f), wallMat);
                HB(fu, "Chimney_B", new Vector3(10f, F3, d - 4f), new Vector3(12f, F3 + 3.5f, d - 2f), wallMat);
                HX(fu, "Terrace_LowWall", 9f, 14f, 8f, F3, 1f, 0.4f, accentMat);
            }

            // 바깥쪽 낮은 테라스(2.4m 매달려 오르기, 북쪽 절반 난간) + 상자
            HB(fu, "LowTerrace", new Vector3(-4.8f, -0.3f, 10f), new Vector3(-0.2f, LowTerraceH, 20f), accentMat);
            HZ(fu, "LowTerrace_Rail", 15f, 20f, -4.8f + RailT / 2f, LowTerraceH, RailH, RailT, accentMat);
            HB(fu, "LowTerrace_Crate", new Vector3(-3.6f, -0.3f, 8.8f), new Vector3(-2.4f, 1.2f, 10f), accentMat);
            // 동관: 낮은 테라스 → 옥상 테라스 사다리(바깥벽, 2F 창 사이). 옥상 난간은 사다리 위만 트였다.
            if (!tall) Ladder(fu, "Ladder_Roof", new Vector3(X(-WallT / 2f), LowTerraceH, LadderZ), east ? Vector3.right : Vector3.left, F3 - LowTerraceH);

            var g = Group(h, "Gothic", Vector3.zero);
            if (tall) GableRoof(g, "Roof", -0.25f, w + 0.25f, -0.25f, d + 0.25f, RoofTop, 8f, gapSide: east ? 1 : -1, gapZ0: -0.5f, gapZ1: 6.5f);
            Tower(g, "Tower", new Vector3(X(-2.2f), 0f, d + 2.2f));
        }

        // ───────────────────────────── 연결동 ─────────────────────────────
        // 월드 좌표, 본채 북쪽 벽(z -2) ↔ 별관 남쪽 벽(z 6), 폭 3m(별관 안뜰 쪽 복도와 같은 x). 1F = 기둥 사이로 지나다님, 2F = 막힌 복도, 옥상(3F 높이) = 난간 길.
        private static void BuildConnector(Transform p, string name, float x0, float x1)
        {
            const float z0 = -2f, z1 = 6f;
            var c = Group(p, name, Vector3.zero);
            foreach (float x in new[] { x0 + 0.3f, x1 - 0.3f })
                foreach (float z in new[] { z0 + 2f, z1 - 2f })
                    Pillar(c, $"Pillar_{x:0.#}_{z:0.#}", x, z, 0.5f, -0.3f, F2 - SlabT);
            Slab(c, "2F_Floor", new Rect2(x0, z0 + 0.15f, x1, z1 - 0.15f), F2, SlabT, floorMat);
            Slab(c, "Walkway", new Rect2(x0, z0 + 0.25f, x1, z1 - 0.25f), F3, SlabT, floorMat);
            WallZ(c, "2F_Wall_W", z0 + 0.2f, z1 - 0.2f, x0 + 0.15f, F2, InH, 0.3f, wallMat, Win(z0 + 2.5f), Win(z1 - 2.5f));
            WallZ(c, "2F_Wall_E", z0 + 0.2f, z1 - 0.2f, x1 - 0.15f, F2, InH, 0.3f, wallMat, Win(z0 + 2.5f), Win(z1 - 2.5f));
            WallZ(c, "Rail_W", z0 + 0.25f, z1 - 0.25f, x0 + RailT / 2f, F3, RailH, RailT, accentMat);
            WallZ(c, "Rail_E", z0 + 0.25f, z1 - 0.25f, x1 - RailT / 2f, F3, RailH, RailT, accentMat);
        }

        // ───────────────────────────── 정원 ─────────────────────────────
        // 월드 좌표. 안뜰(x -18..18, z -2..30 — 시작 지점 x -5..15·z 10..20은 비움) + 건물 둘레 좁은 띠.
        private static void BuildGrounds(Transform g)
        {
            const float B = -0.3f;

            var court = Group(g, "Courtyard", Vector3.zero);
            Fountain(court, "Fountain", new Vector3(0f, 0f, 23f), 4f);
            foreach (var p in new[] { new Vector2(-12f, 26f), new Vector2(12f, 26f), new Vector2(-12f, 10f) })
            {
                Box(court, $"Plinth_{p.x:0}_{p.y:0}", new Vector3(p.x, 0.25f, p.y), new Vector3(2f, 1.1f, 2f), accentMat);
                Pillar(court, $"Statue_{p.x:0}_{p.y:0}", p.x, p.y, 1f, 0.8f, 4.3f);
            }
            WallX(court, "LowWall_W", -14f, -8f, 4f, B, 1f - B, 0.4f, accentMat);
            WallX(court, "LowWall_E", 8f, 14f, 4f, B, 1f - B, 0.4f, accentMat);

            var north = Group(g, "North", Vector3.zero);
            Gazebo(north, "Gazebo", new Vector3(0f, 0f, 39f), 6f);
            Pillar(north, "Statue_W", -12f, 40f, 1f, B, 4f);
            Pillar(north, "Statue_E", 12f, 40f, 1f, B, 4f);
            Shack(north, "Shed", new Vector3(-46f, 0f, 37f), 6f, 5f,
                s: new[] { Win(3f) }, n: new[] { Win(3f) }, w: new[] { Door(2.5f) }, e: new[] { Door(2.5f) });
            Crates(north, "Crates_NW", new Vector2(-26f, 40f));
            var yard = Group(north, "Graveyard", Vector3.zero);
            WallX(yard, "Wall_S", 22f, 40f, 34f, B, 1f - B, 0.4f, accentMat, Gap(29.5f, 32f));
            WallX(yard, "Wall_N", 22f, 40f, 45f, B, 1f - B, 0.4f, accentMat, Gap(29.5f, 32f));
            WallZ(yard, "Wall_W", 34f, 45f, 22f, B, 1f - B, 0.4f, accentMat, Gap(38f, 40.5f));
            WallZ(yard, "Wall_E", 34f, 45f, 40f, B, 1f - B, 0.4f, accentMat, Gap(38f, 40.5f));
            Shack(yard, "Mausoleum", new Vector3(28.5f, 0f, 37f), 5f, 5.5f,
                s: new[] { Door(2.5f) }, n: new[] { Door(2.5f) }, w: new[] { Win(2.75f) }, e: new[] { Win(2.75f) });
            foreach (float x in new[] { 24f, 26.5f, 36f, 38.5f })
                foreach (float z in new[] { 36.5f, 39.5f, 42.5f })
                    Box(yard, $"Tomb_{x:0.#}_{z:0.#}", new Vector3(x, 0.5f, z), new Vector3(0.9f, 1.3f, 0.3f), accentMat);

            var sides = Group(g, "Sides", Vector3.zero);
            WallZ(sides, "WindowWall_W", 8f, 24f, -44f, B, 3.3f, 0.5f, wallMat, Notch(16f, 2f, WinSill - B));
            WallZ(sides, "WindowWall_E", 8f, 24f, 44f, B, 3.3f, 0.5f, wallMat, Notch(16f, 2f, WinSill - B));
            WallX(sides, "WindowWall_SW", -44f, -32f, -16f, B, 3.3f, 0.5f, wallMat, Notch(-38f, 2f, WinSill - B));
            WallX(sides, "WindowWall_SE", 32f, 44f, -16f, B, 3.3f, 0.5f, wallMat, Notch(38f, 2f, WinSill - B));
            foreach (var p in new[] { new Vector2(-38f, -30f), new Vector2(38f, -30f), new Vector2(-40f, -2f), new Vector2(40f, -2f) })
                Pillar(sides, $"Pillar_{p.x:0}_{p.y:0}", p.x, p.y, 1.2f, B, 3.5f);
            Crates(sides, "Crates_E", new Vector2(40f, -8f));
            Crates(sides, "Crates_W", new Vector2(-41f, -8f));

            var rear = Group(g, "Rear", Vector3.zero);
            WallX(rear, "LWall_W_A", -10f, -4f, -42f, B, 3.3f, 0.5f, wallMat);
            WallZ(rear, "LWall_W_B", -45f, -42f, -4f, B, 3.3f, 0.5f, wallMat);
            WallX(rear, "LWall_E_A", 4f, 10f, -42f, B, 3.3f, 0.5f, wallMat);
            WallZ(rear, "LWall_E_B", -45f, -42f, 4f, B, 3.3f, 0.5f, wallMat);
            Pillar(rear, "Pillar_C", 0f, -44f, 1.2f, B, 3.5f);
            Crates(rear, "Crates_W", new Vector2(-32f, -42f));
            Crates(rear, "Crates_E", new Vector2(30f, -42f));
        }

        // 둘레 철책 x·z ±48, 모서리 6m 깎음, 기둥 6m마다.
        private static void BuildPerimeter(Transform p)
        {
            const float x0 = -48f, x1 = 48f, z0 = -48f, z1 = 48f, c = 6f, h = 3.5f, t = 0.3f, b = -0.3f;
            WallX(p, "S", x0 + c, x1 - c, z0, b, h, t, wallMat);
            WallX(p, "N", x0 + c, x1 - c, z1, b, h, t, wallMat);
            WallZ(p, "W", z0 + c, z1 - c, x0, b, h, t, wallMat);
            WallZ(p, "E", z0 + c, z1 - c, x1, b, h, t, wallMat);
            Diagonal(p, "SW", new Vector2(x0, z0 + c), new Vector2(x0 + c, z0), b, h, t);
            Diagonal(p, "SE", new Vector2(x1 - c, z0), new Vector2(x1, z0 + c), b, h, t);
            Diagonal(p, "NW", new Vector2(x0, z1 - c), new Vector2(x0 + c, z1), b, h, t);
            Diagonal(p, "NE", new Vector2(x1 - c, z1), new Vector2(x1, z1 - c), b, h, t);
            for (float a = x0 + c; a <= x1 - c + 0.01f; a += 6f)
            {
                Pillar(p, $"Post_S_{a:0}", a, z0, 0.7f, b, h + 0.7f);
                Pillar(p, $"Post_N_{a:0}", a, z1, 0.7f, b, h + 0.7f);
                Pillar(p, $"Post_W_{a:0}", x0, a, 0.7f, b, h + 0.7f);
                Pillar(p, $"Post_E_{a:0}", x1, a, 0.7f, b, h + 0.7f);
            }
        }

        /// <summary>
        /// 무너진 곳 아래 잔해: 낮은 더미(0.8m) → 가운데 더미(1.6m) → 꼭대기 조각(2.4m) 계단식(뛰어 올라섬), 기울어 떨어진 바닥 판 2장.
        /// r = 무너진 구멍 자리(그 아래 바닥 floorY에 쌓는다).
        /// </summary>
        private static void Rubble(Transform p, string name, Rect2 r, float floorY)
        {
            var g = Group(p, name, Vector3.zero);
            float cx = (r.x0 + r.x1) / 2f, cz = (r.z0 + r.z1) / 2f, sx = r.x1 - r.x0, sz = r.z1 - r.z0;
            Box(g, "Pile_Low", new Vector3(cx, floorY + 0.4f, cz), new Vector3(sx * 0.8f, 0.8f, sz * 0.75f), floorMat, 8f);
            Box(g, "Pile_Mid", new Vector3(cx + sx * 0.08f, floorY + 1.2f, cz - sz * 0.05f), new Vector3(sx * 0.45f, 0.8f, sz * 0.4f), accentMat, -14f);
            Box(g, "Pile_Top", new Vector3(cx + sx * 0.14f, floorY + 2f, cz), new Vector3(sx * 0.22f, 0.8f, sz * 0.22f), floorMat, 25f);
            BoxR(g, "Slab_A", new Vector3(r.x0 + sx * 0.2f, floorY + 1.1f, cz + sz * 0.15f), new Vector3(sx * 0.35f, 0.3f, sz * 0.6f), Quaternion.Euler(0f, 12f, 38f), floorMat, true);
            BoxR(g, "Slab_B", new Vector3(r.x1 - sx * 0.15f, floorY + 0.9f, r.z0 + sz * 0.2f), new Vector3(sx * 0.4f, 0.3f, sz * 0.3f), Quaternion.Euler(-30f, -20f, 0f), floorMat, true);
        }

        /// <summary>사물함 한 줄(+z 방향): 0.9m 폭·0.6m 깊이·2.2m 높이 칸 count개. start = 첫 칸 남쪽 끝 가운데(바닥).</summary>
        private static void Lockers(Transform p, string name, Vector3 start, int count)
        {
            var g = Group(p, name, start);
            for (int i = 0; i < count; i++)
                Box(g, $"Locker_{i}", new Vector3(0f, 1.1f, i * 0.92f + 0.45f), new Vector3(0.6f, 2.2f, 0.9f), accentMat);
        }

        /// <summary>
        /// 가파른 박공지붕(용마루 남북 방향) + 계단식 박공벽 + 첨탑.
        /// gapSide(-1 = 서쪽 경사면, +1 = 동쪽, 0 = 없음)의 z gapZ0..gapZ1 구간은 무너져 비어 있다(그 아래 무너진 곳이 하늘로 트임).
        /// </summary>
        private static void GableRoof(Transform g, string name, float x0, float x1, float z0, float z1, float baseY, float ridge,
            int gapSide = 0, float gapZ0 = 0f, float gapZ1 = 0f)
        {
            float half = (x1 - x0) / 2f, mid = (x0 + x1) / 2f, zc = (z0 + z1) / 2f;
            float angle = Mathf.Atan2(ridge, half) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(half * half + ridge * ridge);
            foreach (int side in new[] { -1, 1 })
            {
                float cx = side < 0 ? x0 + half / 2f : x1 - half / 2f;
                var rot = Quaternion.Euler(0f, 0f, side < 0 ? angle : -angle);
                var spans = gapSide == side
                    ? new[] { (z0 - 0.25f, gapZ0), (gapZ1, z1 + 0.25f) }
                    : new[] { (z0 - 0.25f, z1 + 0.25f) };
                int k = 0;
                foreach (var (a, b) in spans)
                {
                    if (b - a < 0.05f) continue;
                    BoxR(g, $"{name}_{(side < 0 ? "W" : "E")}{(k++ > 0 ? "_" + k : "")}", new Vector3(cx, baseY + ridge / 2f, (a + b) / 2f), new Vector3(len, 0.4f, b - a), rot, accentMat, true);
                }
            }
            const int steps = 4;
            for (int i = 0; i < steps; i++)
            {
                float wdt = (x1 - x0) * (1f - (float)i / steps) - 1f;
                float sh = ridge / steps;
                foreach (float z in new[] { z1 - 0.25f, z0 + 0.25f })
                    Box(g, $"{name}_Gable_{(z > zc ? "N" : "S")}_{i}", new Vector3(mid, baseY + i * sh + sh / 2f, z), new Vector3(wdt, sh, WallT), wallMat);
            }
            Box(g, $"{name}_Fleche", new Vector3(mid, baseY + ridge + 3f, zc), new Vector3(1f, 6f, 1f), accentMat);
            Box(g, $"{name}_FlecheTip", new Vector3(mid, baseY + ridge + 7.5f, zc), new Vector3(0.3f, 3f, 0.3f), accentMat);
        }

        /// <summary>속이 찬 돌탑 + 단계로 좁아지는 뾰족 지붕. at = 탑 가운데(바닥).</summary>
        private static void Tower(Transform g, string name, Vector3 at)
        {
            var t = Group(g, name, at);
            Box(t, "Base", new Vector3(0f, (-0.3f + 22f) / 2f, 0f), new Vector3(6f, 22.3f, 6f), wallMat);
            Box(t, "Cornice", new Vector3(0f, 22.3f, 0f), new Vector3(6.6f, 0.6f, 6.6f), accentMat);
            Box(t, "Spire_1", new Vector3(0f, 24f, 0f), new Vector3(4.4f, 3f, 4.4f), accentMat);
            Box(t, "Spire_2", new Vector3(0f, 27f, 0f), new Vector3(2.8f, 3f, 2.8f), accentMat);
            Box(t, "Spire_3", new Vector3(0f, 30.5f, 0f), new Vector3(1.2f, 4f, 1.2f), accentMat);
            Box(t, "Spire_Tip", new Vector3(0f, 34f, 0f), new Vector3(0.4f, 3f, 0.4f), accentMat);
        }

        // ───────────────────────────── 가구·장식 ─────────────────────────────

        private const float ScreenPanelW = 1.2f, ScreenAngle = 25f;
        private static float ScreenStep => ScreenPanelW * Mathf.Cos(ScreenAngle * Mathf.Deg2Rad);

        /// <summary>접이식 병풍: 판 panels개를 ±ScreenAngle로 번갈아 꺾어 이음. start는 첫 판 끝(바닥), alongX면 +x로, 아니면 +z로 늘어선다.</summary>
        private static void FoldingScreen(Transform p, string name, Vector3 start, bool alongX, int panels)
        {
            const float h = 4f, t = 0.08f;
            var g = Group(p, name, start);
            if (!alongX) g.localRotation = Quaternion.Euler(0f, -90f, 0f); // 로컬 +x → 월드 +z
            for (int i = 0; i < panels; i++)
            {
                float yaw = i % 2 == 0 ? ScreenAngle : -ScreenAngle;
                Box(g, $"Panel_{i}", new Vector3((i + 0.5f) * ScreenStep, h / 2f, 0f), new Vector3(ScreenPanelW, h, t), accentMat, yaw);
            }
        }

        /// <summary>등받이 높은 의자. pos = 앉는 자리 가운데(바닥), back = 등받이가 있는 쪽.</summary>
        private static void Chair(Transform p, string name, Vector3 pos, Vector3 back)
        {
            var g = Group(p, name, pos);
            g.localRotation = Quaternion.LookRotation(back, Vector3.up);
            Box(g, "Seat", new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.5f, 0.55f), accentMat);
            Box(g, "Back", new Vector3(0f, 1.25f, 0.24f), new Vector3(0.55f, 1.5f, 0.08f), accentMat);
        }

        /// <summary>팔각 분수: 턱 0.8m(뛰어 올라섬) 테두리 + 가운데 기둥.</summary>
        private static void Fountain(Transform p, string name, Vector3 center, float r)
        {
            var g = Group(p, name, center);
            float seg = 2f * r * Mathf.Tan(22.5f * Mathf.Deg2Rad) + 0.5f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var pos = new Vector3(r * Mathf.Cos(a * Mathf.Deg2Rad), 0.25f, r * Mathf.Sin(a * Mathf.Deg2Rad));
                BoxR(g, $"Rim_{k}", pos, new Vector3(seg, 1.1f, 0.5f), Quaternion.Euler(0f, -(90f + a), 0f), accentMat, true);
            }
            Box(g, "Basin", new Vector3(0f, 0f, 0f), new Vector3(r * 1.6f, 0.4f, r * 1.6f), floorMat);
            Box(g, "Column", new Vector3(0f, 1.6f, 0f), new Vector3(1.4f, 3.2f, 1.4f), wallMat);
            Box(g, "Top", new Vector3(0f, 3.5f, 0f), new Vector3(2.4f, 0.6f, 2.4f), accentMat);
        }

        /// <summary>정자: 기둥 4개 + 지붕(높이 4.3m, 그 아래는 도약 못 함).</summary>
        private static void Gazebo(Transform p, string name, Vector3 center, float size)
        {
            var g = Group(p, name, center);
            float h = size / 2f - 0.3f;
            foreach (var c in new[] { new Vector2(-h, -h), new Vector2(-h, h), new Vector2(h, -h), new Vector2(h, h) })
                Pillar(g, $"Post_{c.x:0}_{c.y:0}", c.x, c.y, 0.5f, -0.3f, 4f);
            Slab(g, "Roof", new Rect2(-size / 2f, -size / 2f, size / 2f, size / 2f), 4.3f, 0.3f, accentMat);
            Box(g, "RoofCap", new Vector3(0f, 5f, 0f), new Vector3(size * 0.6f, 1.2f, size * 0.6f), accentMat);
        }

        // ───────────────────────────── 조각 ─────────────────────────────

        /// <summary>1층 창고: 바닥·벽(높이 h)·지붕. 각 벽 개구부는 그 벽의 시작점(서·남쪽 모서리)에서 잰 거리.</summary>
        private static void Shack(Transform parent, string name, Vector3 corner, float sx, float sz,
            Opening[] s, Opening[] n, Opening[] w, Opening[] e, float h = 4.4f)
        {
            const float t = 0.35f;
            var g = Group(parent, name, corner);
            Slab(g, "Floor", new Rect2(0f, 0f, sx, sz), G, 0.35f, floorMat);
            Slab(g, "Roof", new Rect2(-0.5f, -0.5f, sx + 0.5f, sz + 0.5f), G + h + 0.25f, 0.25f, accentMat);
            WallX(g, "S", -t / 2f, sx + t / 2f, 0f, G, h, t, wallMat, s);
            WallX(g, "N", -t / 2f, sx + t / 2f, sz, G, h, t, wallMat, n);
            WallZ(g, "W", 0f, sz, 0f, G, h, t, wallMat, w);
            WallZ(g, "E", 0f, sz, sx, G, h, t, wallMat, e);
        }

        private static void Crates(Transform parent, string name, Vector2 at)
        {
            const float s = 1.2f;
            var g = Group(parent, name, new Vector3(at.x, 0f, at.y));
            Box(g, "C0", new Vector3(0f, s / 2f - 0.05f, 0f), Vector3.one * s, accentMat);
            Box(g, "C1", new Vector3(s, s / 2f - 0.05f, 0f), Vector3.one * s, accentMat);
            Box(g, "C2", new Vector3(0f, s / 2f - 0.05f, s), Vector3.one * s, accentMat);
            Box(g, "C3_Top", new Vector3(0f, s * 1.5f - 0.05f, 0f), Vector3.one * s, accentMat);
        }

        private static void Pillar(Transform parent, string name, float x, float z, float size, float y0, float y1)
        {
            Box(parent, name, new Vector3(x, (y0 + y1) / 2f, z), new Vector3(size, y1 - y0, size), accentMat);
        }

        private static void Diagonal(Transform parent, string name, Vector2 a, Vector2 b, float y0, float h, float t)
        {
            Vector2 d = b - a;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            var mid = (a + b) / 2f;
            Box(parent, name, new Vector3(mid.x, y0 + h / 2f, mid.y), new Vector3(t, h, d.magnitude + t), wallMat, yaw);
        }

        /// <summary>
        /// 계단: start = 첫 단 앞 모서리의 폭 가운데(바닥 높이). dir 쪽으로 올라가 rise만큼 높아진다.
        /// 단(StairSteps-1개)은 바닥부터 꽉 찬 상자이고, 맨 위 단은 위층 바닥이 대신한다(위층 바닥 구멍 길이 = (StairSteps-1)·StairRun).
        /// 단 모서리를 잇는 보이지 않는 경사 Collider를 얹어 CharacterController가 덜컹이지 않게 오르내린다.
        /// </summary>
        private static void Stair(Transform parent, string name, Vector3 start, Vector3 dir, float width, float rise)
        {
            var g = Group(parent, name, start);
            g.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            g.gameObject.AddComponent<Project1.Core.NoLedgeGrab>(); // 계단은 점프로 붙잡는 턱이 아니다(이름·구조가 바뀌어도)
            float stepRise = rise / StairSteps;
            for (int i = 1; i < StairSteps; i++)
                BoxMM(g, $"Step_{i:00}", new Vector3(-width / 2f, -0.3f, (i - 1) * StairRun), new Vector3(width / 2f, i * stepRise, i * StairRun), accentMat);

            // 경사면: (z = -run, y = 0) → (z = (n-1)·run, y = rise). 모든 단 앞 모서리를 지난다.
            float horiz = StairSteps * StairRun;
            float angle = Mathf.Atan2(rise, horiz);
            float length = Mathf.Sqrt(horiz * horiz + rise * rise);
            const float thick = 0.1f, lift = 0.02f;
            var normal = new Vector3(0f, Mathf.Cos(angle), -Mathf.Sin(angle));
            var mid = new Vector3(0f, rise / 2f, ((StairSteps - 1) * StairRun - StairRun) / 2f);
            var ramp = new GameObject("Ramp_Collider");
            ramp.transform.SetParent(g, false);
            ramp.transform.localPosition = mid + normal * (lift - thick / 2f);
            ramp.transform.localRotation = Quaternion.Euler(-angle * Mathf.Rad2Deg, 0f, 0f);
            ramp.AddComponent<BoxCollider>().size = new Vector3(width, thick, length);
            GameObjectUtility.SetStaticEditorFlags(ramp, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>x 방향 벽(z 위치 고정). 개구부의 from/to는 x 값, bottom/top은 y0 기준 높이.</summary>
        private const float LadderZ = 12f; // 별관 사다리 자리(별관 z — 바깥벽 2F 창 z 4.5·15 사이)

        /// <summary>
        /// 사다리: bottom = 아래 끝 가운데(벽 면, 발 딛는 바닥 높이), outward = 벽 바깥쪽. 기둥 2개 + 0.3m 간격 발판(보이기만 — 충돌 없음),
        /// 타기는 Ladder 표시로 CharacterTraversal이 한다. 기둥은 위 끝(위층 바닥)보다 1m 더 올라가 손잡이가 된다.
        /// </summary>
        private static void Ladder(Transform p, string name, Vector3 bottom, Vector3 outward, float height)
        {
            const float width = 0.8f, depth = 0.15f, rail = 0.07f, rungStep = 0.3f;
            var g = Group(p, name, bottom);
            g.localRotation = Quaternion.LookRotation(outward, Vector3.up);
            g.gameObject.AddComponent<Project1.Core.Ladder>().Set(height, width, depth);
            g.gameObject.AddComponent<Project1.Core.NoLedgeGrab>();
            var r = Quaternion.identity;
            foreach (float s in new[] { -1f, 1f })
                BoxR(g, s < 0 ? "Rail_L" : "Rail_R", new Vector3(s * width / 2f, (height + 1f) / 2f, depth), new Vector3(rail, height + 1f, rail), r, accentMat, false);
            int i = 0;
            for (float y = rungStep; y < height + 0.9f; y += rungStep, i++)
                BoxR(g, $"Rung_{i:00}", new Vector3(0f, y, depth), new Vector3(width, 0.04f, 0.05f), r, accentMat, false);
        }

        private static void WallX(Transform p, string name, float xa, float xb, float z, float y0, float h, float t, Material m, params Opening[] ops)
            => Wall(p, name, true, xa, xb, z, y0, h, t, m, ops);

        /// <summary>z 방향 벽(x 위치 고정). 개구부의 from/to는 z 값.</summary>
        private static void WallZ(Transform p, string name, float za, float zb, float x, float y0, float h, float t, Material m, params Opening[] ops)
            => Wall(p, name, false, za, zb, x, y0, h, t, m, ops);

        private static void Wall(Transform p, string name, bool alongX, float a0, float a1, float across, float y0, float h, float t, Material m, Opening[] ops)
        {
            var g = Group(p, name, Vector3.zero);
            int k = 0;
            void Piece(float s, float e, float yb, float yt)
            {
                if (e - s < 0.01f || yt - yb < 0.01f) return;
                var min = alongX ? new Vector3(s, y0 + yb, across - t / 2f) : new Vector3(across - t / 2f, y0 + yb, s);
                var max = alongX ? new Vector3(e, y0 + yt, across + t / 2f) : new Vector3(across + t / 2f, y0 + yt, e);
                BoxMM(g, $"{name}_{k++}", min, max, m);
            }

            float cursor = a0;
            foreach (var o in (ops ?? System.Array.Empty<Opening>()).OrderBy(q => q.from))
            {
                Piece(cursor, o.from, 0f, h);
                Piece(o.from, o.to, 0f, Mathf.Min(o.bottom, h));
                Piece(o.from, o.to, Mathf.Min(o.top, h), h);
                cursor = o.to;
            }
            Piece(cursor, a1, 0f, h);
        }

        /// <summary>윗면 높이 yTop인 판. 구멍을 뺀 나머지를 직사각형 몇 개로 나눠 만든다.</summary>
        private static void Slab(Transform p, string name, Rect2 outer, float yTop, float thick, Material m, params Rect2[] holes)
        {
            var g = Group(p, name, Vector3.zero);
            var xs = new SortedSet<float> { outer.x0, outer.x1 };
            var zs = new SortedSet<float> { outer.z0, outer.z1 };
            foreach (var h in holes)
            {
                xs.Add(Mathf.Clamp(h.x0, outer.x0, outer.x1)); xs.Add(Mathf.Clamp(h.x1, outer.x0, outer.x1));
                zs.Add(Mathf.Clamp(h.z0, outer.z0, outer.z1)); zs.Add(Mathf.Clamp(h.z1, outer.z0, outer.z1));
            }
            var xa = xs.ToArray();
            var za = zs.ToArray();
            int k = 0;
            void Emit((float x0, float x1) s, float z0, float z1)
                => BoxMM(g, $"{name}_{k++}", new Vector3(s.x0, yTop - thick, z0), new Vector3(s.x1, yTop, z1), m);

            var open = new Dictionary<(float, float), float>();
            for (int j = 0; j < za.Length - 1; j++)
            {
                float cz = (za[j] + za[j + 1]) / 2f;
                var strips = new List<(float, float)>();
                float? runStart = null;
                for (int i = 0; i < xa.Length - 1; i++)
                {
                    float cx = (xa[i] + xa[i + 1]) / 2f;
                    bool filled = !holes.Any(h => h.Contains(cx, cz));
                    if (filled && runStart == null) runStart = xa[i];
                    if (!filled && runStart != null) { strips.Add((runStart.Value, xa[i])); runStart = null; }
                }
                if (runStart != null) strips.Add((runStart.Value, xa[^1]));

                foreach (var key in open.Keys.ToList())
                    if (!strips.Contains(key)) { Emit(key, open[key], za[j]); open.Remove(key); }
                foreach (var s in strips)
                    if (!open.ContainsKey(s)) open[s] = za[j];
            }
            foreach (var kv in open) Emit(kv.Key, kv.Value, za[^1]);
        }

        private static void BoxMM(Transform p, string name, Vector3 min, Vector3 max, Material m)
            => Box(p, name, (min + max) / 2f, max - min, m);

        private static GameObject Box(Transform p, string name, Vector3 localCenter, Vector3 size, Material m, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(p, false);
            go.transform.localPosition = localCenter;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>아무 방향으로 돌린 상자. collider = false면 보이기만 한다(샹들리에 등).</summary>
        private static GameObject BoxR(Transform p, string name, Vector3 localCenter, Vector3 size, Quaternion rotation, Material m, bool collider)
        {
            var go = Box(p, name, localCenter, size, m);
            go.transform.localRotation = rotation;
            if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            return go;
        }

        private static Transform Group(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            return t;
        }

        private static Material GetMat(string name, Color color, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
                AssetDatabase.CreateFolder("Assets/Environment/Materials", "Blockout");
            string path = $"{MatFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m); // 내 머티리얼만 저장(SaveAssets는 다른 세션의 작업 중 에셋까지 저장함)
            return m;
        }
    }
}

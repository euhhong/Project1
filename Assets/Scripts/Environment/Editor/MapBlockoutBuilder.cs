using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 술래잡기(감시자 1 : 생존자) 맵 회색 블록아웃: 고딕 저택과 정원. 메뉴: Tools/BG/맵 블록아웃 다시 만들기.
    /// 씬의 "MapBlockout" 하나를 지우고 다시 세운다(다른 씬 오브젝트는 건드리지 않음). 맵을 바꾸려면 이 파일 숫자를 고치고 다시 실행한다.
    ///
    /// 크기 기준: 캐릭터 키 약 2.1m(감시자 ~2.7m), 점프 1.2m, stepOffset 0.3, slopeLimit 45°.
    ///   층고 6m(천장 5.7m), 문 2.4×4.2m, 정문 5×5m, 고딕 창 1.6m 폭·턱 1.0m·위 4.6m(점프로 넘음), 아치 3.2×5m,
    ///   계단 폭 3.3~3.5m·단 0.27m·디딤 0.4m(위에 보이지 않는 경사 Collider), 난간·낮은 벽 1m, 산울타리·큰 벽 2.8m 이상.
    ///   뛰어 올라서기 턱 0.74~2.3m(상자 1.2, 분수 턱 0.8, 식탁 1.05), 매달려 오르기 턱 2.3~3.7m(낮은 테라스 2.4).
    /// 루키노 도약(머리 위 6m 천장 검사): 로비(천장 18m)·날개 옥상 테라스·발코니·정원은 뜀, 2F·3F 회랑·방·창고는 못 뜀.
    ///
    /// 배치(월드 좌표, x=동, z=북). 저택 60×36m: x -30..30, z -36..0, 정면(정문)은 북쪽(시작 지점 쪽).
    ///   가운데 본채(x -16..16): 3층. 로비 24×22m(x -12..12, z -28..-6)가 지붕까지 트여 2F·3F 회랑 난간에서 1F 로비가 내려다보인다.
    ///     로비: 긴 식탁(14×3m)과 등받이 높은 의자 20개, 식탁 둘레 높은 병풍 4개(모서리 틈으로 도는 고리), 기둥 열, 샹들리에(충돌 없음).
    ///     대계단 2개(로비 → 2F 남쪽 회랑). 2F 남쪽 문 → 뒤 테라스(바깥 계단 2개로 뒤뜰). 2F 북쪽 문 → 정문 위 발코니.
    ///   서·동 날개(x -30..-16, 16..30): 2층, 방 3개씩(문 2개 이상). 날개 계단(1F→2F→옥상)이 나란히 있고 옥상은 하늘 열린 테라스(3F 높이),
    ///     본채 3F 회랑과 문으로 이어진다. 날개 바깥벽에 2.4m 낮은 테라스(매달려 오르기·상자).
    ///   모서리 탑 4개(뾰족 지붕), 본채 박공지붕·첨탑, 버팀벽(정면·뒷면).
    ///   정원(울타리 x·z ±49, 모서리 깎음): 앞뜰 분수·석상·산울타리 L자 2개·정자, 서쪽 창고·창문 벽, 동쪽 묘지(낮은 담·묘비·납골당), 뒤뜰 L벽.
    /// </summary>
    public static class MapBlockoutBuilder
    {
        private const string RootName = "MapBlockout";
        private const string MatFolder = "Assets/Environment/Materials/Blockout";

        // 높이
        private const float G = 0.15f;       // 1F 바닥 윗면(지형 ≈ 0)
        private const float Story = 6f;      // 층고
        private const float SlabT = 0.3f;    // 바닥·지붕 두께
        private const float F2 = G + Story;  // 2F 바닥 윗면 6.15
        private const float F3 = F2 + Story; // 3F 바닥 윗면 12.15 (= 날개 옥상 테라스)
        private const float RoofTop = F3 + Story; // 본채 지붕 18.15
        private const float InH = Story - SlabT;  // 위층 판 밑면까지 오는 벽 높이

        // 벽·개구부
        private const float WallT = 0.4f;
        private const float DoorW = 2.4f, DoorH = 4.2f;
        private const float WinW = 1.6f, WinSill = 1.0f, WinTop = 4.6f;
        private const float ArchW = 3.2f, ArchH = 5f;
        private const float RailH = 1f, RailT = 0.15f;
        private const float LowTerraceH = 2.4f; // 낮은 테라스: 땅에서 매달려 올라가는 턱

        // 계단
        private const int StairSteps = 22;
        private const float StairRun = 0.4f;
        private const float StairLen = (StairSteps - 1) * StairRun; // 위층 바닥 구멍 길이 8.4

        private static readonly Vector3 MansionOrigin = new(-30f, 0f, -36f);

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
        private static Opening Arch(float c) => new(c - ArchW / 2f, c + ArchW / 2f, 0f, ArchH);
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

            BuildMansion(Group(root, "Mansion", MansionOrigin));
            BuildGrounds(Group(root, "Grounds", Vector3.zero));
            BuildPerimeter(Group(root, "Perimeter", Vector3.zero));

            int count = root.GetComponentsInChildren<Renderer>().Length;
            EditorSceneManager.MarkSceneDirty(scene);
            message = $"MapBlockout: 블록 {count}개 생성";
            return true;
        }

        // ───────────────────────────── 저택 ─────────────────────────────
        // 로컬 좌표: x 0..60(동), z 0..36(북, 정면). 서쪽 날개 x 0..14, 본채 x 14..46, 동쪽 날개 x 46..60. 로비(트인 곳) x 18..42, z 8..30.
        private static void BuildMansion(Transform m)
        {
            var outer = new Rect2(-0.1f, -0.1f, 60.1f, 36.1f);
            var atrium = new Rect2(18f, 8f, 42f, 30f);
            float sz0 = 13f, sz1 = 13f + StairLen; // 날개 계단 z 범위 13..21.4
            var holeW1 = new Rect2(0.2f, sz0, 3.7f, sz1);   // 서쪽 1F→2F
            var holeW2 = new Rect2(3.7f, sz0, 7.2f, sz1);   // 서쪽 2F→옥상
            var holeE1 = new Rect2(56.3f, sz0, 59.8f, sz1); // 동쪽 1F→2F
            var holeE2 = new Rect2(52.8f, sz0, 56.3f, sz1); // 동쪽 2F→옥상

            // 바닥·지붕
            var floors = Group(m, "Floors", Vector3.zero);
            Slab(floors, "1F_Floor", outer, G, 0.35f, floorMat);
            Slab(floors, "2F_Floor", outer, F2, SlabT, floorMat, atrium, holeW1, holeE1);
            // 3F 판: 본채는 3층 바닥, 날개는 옥상 테라스. 벽 겉면보다 5cm 밖으로(깜빡임 방지)
            Slab(floors, "3F_Floor_Terraces", new Rect2(-0.25f, -0.25f, 60.25f, 36.25f), F3, SlabT, floorMat, atrium, holeW2, holeE2);
            Slab(floors, "Roof", new Rect2(13.75f, -0.25f, 46.25f, 36.25f), RoofTop, SlabT, accentMat);

            // ── 1F
            var w1 = Group(m, "1F_Walls", Vector3.zero);
            WallX(w1, "Outer_N", -0.2f, 60.2f, 36f, G, Story, WallT, wallMat,
                Win(4f), Win(10f), Win(17f), Win(22f), MainDoor(30f), Win(38f), Win(43f), Win(50f), Win(56f));
            WallX(w1, "Outer_S", -0.2f, 60.2f, 0f, G, Story, WallT, wallMat,
                Door(3f), Win(17f), Door(22f), Win(26f), Win(34f), Door(38f), Win(43f), Door(57f));
            WallZ(w1, "Outer_W", 0f, 36f, 0f, G, Story, WallT, wallMat, Win(4f), Door(8.5f), Win(28f), Door(31.5f));
            WallZ(w1, "Outer_E", 0f, 36f, 60f, G, Story, WallT, wallMat, Win(4f), Door(8.5f), Win(28f), Door(31.5f));
            WallZ(w1, "Div_W", 0f, 36f, 14f, G, InH, WallT, wallMat, Door(5f), Arch(18f), Door(31f));
            WallZ(w1, "Div_E", 0f, 36f, 46f, G, InH, WallT, wallMat, Door(5f), Arch(18f), Door(31f));
            WallX(w1, "WingW_S", 0.2f, 14f, 10f, G, InH, WallT, wallMat, Door(10f));
            WallX(w1, "WingW_N", 0.2f, 14f, 26f, G, InH, WallT, wallMat, Door(10f));
            WallX(w1, "WingE_S", 46f, 59.8f, 10f, G, InH, WallT, wallMat, Door(50f));
            WallX(w1, "WingE_N", 46f, 59.8f, 26f, G, InH, WallT, wallMat, Door(50f));
            // 날개 계단 꼭대기 뒤 빈 구석을 막는다(막다른 곳 방지)
            BoxMM(w1, "StairW_Fill", new Vector3(0.2f, G, sz1), new Vector3(3.7f, F2 - SlabT, 25.8f), wallMat);
            BoxMM(w1, "StairE_Fill", new Vector3(56.3f, G, sz1), new Vector3(59.8f, F2 - SlabT, 25.8f), wallMat);

            // 로비 기둥 열(지붕까지, 2F·3F 회랑 가장자리에도 선다)
            var cols = Group(m, "Lobby_Columns", Vector3.zero);
            foreach (float z in new[] { 8f, 12.4f, 16.8f, 21.2f, 25.6f, 30f })
            {
                Pillar(cols, $"Col_W_{z:0.#}", 18f, z, 1f, G, RoofTop - SlabT);
                Pillar(cols, $"Col_E_{z:0.#}", 42f, z, 1f, G, RoofTop - SlabT);
            }
            foreach (float x in new[] { 22.8f, 27.6f, 32.4f, 37.2f })
            {
                Pillar(cols, $"Col_S_{x:0.#}", x, 8f, 1f, G, RoofTop - SlabT);
                Pillar(cols, $"Col_N_{x:0.#}", x, 30f, 1f, G, RoofTop - SlabT);
            }

            // 계단: 로비 대계단 2개(→2F 남쪽 회랑), 날개 계단(1F→2F, 2F→옥상 테라스, 나란히)
            var stairs = Group(m, "Stairs", Vector3.zero);
            Stair(stairs, "Grand_W", new Vector3(20.55f, G, 8f + StairLen), Vector3.back, 3.3f, Story);
            Stair(stairs, "Grand_E", new Vector3(39.45f, G, 8f + StairLen), Vector3.back, 3.3f, Story);
            Stair(stairs, "WingW_1F", new Vector3(1.95f, G, sz0), Vector3.forward, 3.5f, Story);
            Stair(stairs, "WingW_2F", new Vector3(5.45f, F2, sz1), Vector3.back, 3.5f, Story);
            Stair(stairs, "WingE_1F", new Vector3(58.05f, G, sz0), Vector3.forward, 3.5f, Story);
            Stair(stairs, "WingE_2F", new Vector3(54.55f, F2, sz1), Vector3.back, 3.5f, Story);

            BuildLobby(Group(m, "Lobby", Vector3.zero));

            // ── 2F
            var w2 = Group(m, "2F_Walls", Vector3.zero);
            WallX(w2, "Outer_N", -0.2f, 60.2f, 36f, F2, InH, WallT, wallMat,
                Win(4f), Win(10f), Win(17f), Win(22f), Win(26f), Door(30f), Win(34f), Win(38f), Win(43f), Win(50f), Win(56f));
            WallX(w2, "Outer_S", -0.2f, 60.2f, 0f, F2, InH, WallT, wallMat,
                Win(4f), Win(10f), Door(17f), Win(22f), Win(27f), Door(30f), Win(33f), Win(38f), Door(43f), Win(50f), Win(56f));
            WallZ(w2, "Outer_W", 0f, 36f, 0f, F2, InH, WallT, wallMat, Win(4f), Win(8.5f), Win(28f), Win(32f));
            WallZ(w2, "Outer_E", 0f, 36f, 60f, F2, InH, WallT, wallMat, Win(4f), Win(8.5f), Win(28f), Win(32f));
            WallZ(w2, "Div_W", 0f, 36f, 14f, F2, InH, WallT, wallMat, Door(5f), Door(18f), Door(31f));
            WallZ(w2, "Div_E", 0f, 36f, 46f, F2, InH, WallT, wallMat, Door(5f), Door(18f), Door(31f));
            WallX(w2, "WingW_S", 0.2f, 14f, 10f, F2, InH, WallT, wallMat, Door(10f));
            WallX(w2, "WingW_N", 0.2f, 14f, 26f, F2, InH, WallT, wallMat, Door(10f));
            WallX(w2, "WingE_S", 46f, 59.8f, 10f, F2, InH, WallT, wallMat, Door(50f));
            WallX(w2, "WingE_N", 46f, 59.8f, 26f, F2, InH, WallT, wallMat, Door(50f));

            var r2 = Group(m, "2F_Railings", Vector3.zero);
            WallX(r2, "Atrium_S", 18f, 42f, 8f, F2, RailH, RailT, accentMat, Gap(18.9f, 22.2f), Gap(29f, 31f), Gap(37.8f, 41.1f));
            WallX(r2, "Atrium_N", 18f, 42f, 30f, F2, RailH, RailT, accentMat, Gap(28.5f, 31.5f));
            WallZ(r2, "Atrium_W", 8f, 30f, 18f, F2, RailH, RailT, accentMat, Gap(18f, 20f));
            WallZ(r2, "Atrium_E", 8f, 30f, 42f, F2, RailH, RailT, accentMat, Gap(18f, 20f));
            WallX(r2, "StairW_S", holeW1.x0, holeW1.x1, sz0, F2, RailH, RailT, accentMat);
            WallX(r2, "StairE_S", holeE1.x0, holeE1.x1, sz0, F2, RailH, RailT, accentMat);

            // 정문 위 발코니(뛰어내림) + 기둥 2개(현관)
            var bf = Group(m, "Balcony_Front", Vector3.zero);
            Slab(bf, "Floor", new Rect2(24f, 36.25f, 36f, 39.5f), F2, SlabT, floorMat);
            WallX(bf, "Rail_N", 24f, 36f, 39.5f - RailT / 2f, F2, RailH, RailT, accentMat, Gap(29f, 31f));
            WallZ(bf, "Rail_W", 36.25f, 39.5f, 24f + RailT / 2f, F2, RailH, RailT, accentMat);
            WallZ(bf, "Rail_E", 36.25f, 39.5f, 36f - RailT / 2f, F2, RailH, RailT, accentMat);
            Pillar(bf, "Porch_W", 24.4f, 39.1f, 0.7f, -0.3f, F2 - SlabT);
            Pillar(bf, "Porch_E", 35.6f, 39.1f, 0.7f, -0.3f, F2 - SlabT);

            // 뒤 테라스(2F 높이, 깊이 5m) + 양 끝 바깥 계단 → 뒤뜰, 아래 기둥
            var rt = Group(m, "Terrace_Rear", Vector3.zero);
            Slab(rt, "Floor", new Rect2(14f, -5f, 46f, -0.25f), F2, SlabT, floorMat);
            WallX(rt, "Rail_S", 14f, 46f, -5f + RailT / 2f, F2, RailH, RailT, accentMat, Gap(22f, 24f), Gap(36f, 38f));
            WallZ(rt, "Rail_W", -5f, -3.6f, 14f + RailT / 2f, F2, RailH, RailT, accentMat);
            WallZ(rt, "Rail_E", -5f, -3.6f, 46f - RailT / 2f, F2, RailH, RailT, accentMat);
            foreach (float x in new[] { 14.5f, 20f, 30f, 40f, 45.5f })
                Pillar(rt, $"Pillar_{x:0.#}", x, -4.6f, 0.6f, -0.3f, F2 - SlabT);
            Stair(rt, "Stair_W", new Vector3(14f - StairLen, 0f, -2.2f), Vector3.right, 2.8f, F2);
            Stair(rt, "Stair_E", new Vector3(46f + StairLen, 0f, -2.2f), Vector3.left, 2.8f, F2);

            // ── 3F (본채만) + 날개 옥상 테라스
            var w3 = Group(m, "3F_Walls", Vector3.zero);
            WallX(w3, "Outer_N", 13.8f, 46.2f, 36f, F3, InH, WallT, wallMat,
                Win(17f), Win(22f), Win(26f), BigWin(30f), Win(34f), Win(38f), Win(43f));
            WallX(w3, "Outer_S", 13.8f, 46.2f, 0f, F3, InH, WallT, wallMat,
                Win(17f), Win(22f), Win(27f), Win(30f), Win(33f), Win(38f), Win(43f));
            WallZ(w3, "Outer_W", -0.2f, 36.2f, 14f, F3, InH, WallT, wallMat, Door(5f), Win(11f), Door(18f), Win(25f), Door(31f));
            WallZ(w3, "Outer_E", -0.2f, 36.2f, 46f, F3, InH, WallT, wallMat, Door(5f), Win(11f), Door(18f), Win(25f), Door(31f));

            var r3 = Group(m, "3F_Railings", Vector3.zero);
            WallX(r3, "Atrium_S", 18f, 42f, 8f, F3, RailH, RailT, accentMat, Gap(29f, 31f));
            WallX(r3, "Atrium_N", 18f, 42f, 30f, F3, RailH, RailT, accentMat, Gap(28.5f, 31.5f));
            WallZ(r3, "Atrium_W", 8f, 30f, 18f, F3, RailH, RailT, accentMat);
            WallZ(r3, "Atrium_E", 8f, 30f, 42f, F3, RailH, RailT, accentMat);

            WingTerrace(Group(m, "Terrace_WingW", Vector3.zero), 0f, 14f, holeW2, true);
            WingTerrace(Group(m, "Terrace_WingE", Vector3.zero), 46f, 60f, holeE2, false);

            // 날개 바깥벽 낮은 테라스(2.4m): 땅에서 매달려 오르기 / 상자(1.2) 두 번 올라서기. 가장자리 북쪽 절반 난간.
            var lt = Group(m, "LowTerraces", Vector3.zero);
            BoxMM(lt, "W_Block", new Vector3(-5f, -0.3f, 10f), new Vector3(-0.2f, LowTerraceH, 26f), accentMat);
            WallZ(lt, "W_Rail", 18f, 26f, -5f + RailT / 2f, LowTerraceH, RailH, RailT, accentMat);
            BoxMM(lt, "W_Crate", new Vector3(-3.8f, -0.3f, 8.8f), new Vector3(-2.6f, 1.2f, 10f), accentMat);
            BoxMM(lt, "E_Block", new Vector3(60.2f, -0.3f, 10f), new Vector3(65f, LowTerraceH, 26f), accentMat);
            WallZ(lt, "E_Rail", 18f, 26f, 65f - RailT / 2f, LowTerraceH, RailH, RailT, accentMat);
            BoxMM(lt, "E_Crate", new Vector3(62.6f, -0.3f, 8.8f), new Vector3(63.8f, 1.2f, 10f), accentMat);

            BuildGothicExterior(Group(m, "Gothic", Vector3.zero));
        }

        /// <summary>날개 옥상 테라스: 바깥 난간, 계단 구멍 난간(남쪽 끝이 출구), 굴뚝·낮은 벽.</summary>
        private static void WingTerrace(Transform t, float x0, float x1, Rect2 hole, bool west)
        {
            float outerX = west ? x0 - 0.25f + RailT / 2f : x1 + 0.25f - RailT / 2f;
            WallX(t, "Rail_S", x0 - 0.25f, x1 + 0.25f, -0.25f + RailT / 2f, F3, RailH, RailT, accentMat);
            WallX(t, "Rail_N", x0 - 0.25f, x1 + 0.25f, 36.25f - RailT / 2f, F3, RailH, RailT, accentMat);
            WallZ(t, "Rail_Out", -0.25f, 36.25f, outerX, F3, RailH, RailT, accentMat);
            WallZ(t, "Hole_W", hole.z0, hole.z1, hole.x0, F3, RailH, RailT, accentMat);
            WallZ(t, "Hole_E", hole.z0, hole.z1, hole.x1, F3, RailH, RailT, accentMat);
            WallX(t, "Hole_N", hole.x0, hole.x1, hole.z1, F3, RailH, RailT, accentMat);
            float cx = west ? 10f : 50f;
            Box(t, "Chimney_S", new Vector3(cx, F3 + 1.75f, 6f), new Vector3(2f, 3.5f, 2f), wallMat);
            Box(t, "Chimney_N", new Vector3(cx, F3 + 1.75f, 30f), new Vector3(2f, 3.5f, 2f), wallMat);
            WallX(t, "LowWall", cx - 2f, cx + 2f, 18f + (west ? 4f : -4f), F3, 1f, 0.4f, accentMat);
        }

        /// <summary>로비: 긴 식탁, 등받이 높은 의자, 높은 병풍 4개(식탁 둘레, 모서리 틈), 샹들리에(충돌 없음).</summary>
        private static void BuildLobby(Transform l)
        {
            const float tx0 = 23f, tx1 = 37f, tz0 = 20f, tz1 = 23f;
            var table = Group(l, "Table", Vector3.zero);
            BoxMM(table, "Top", new Vector3(tx0, G + 0.85f, tz0), new Vector3(tx1, G + 1.05f, tz1), accentMat);
            foreach (float x in new[] { tx0 + 0.3f, 30f, tx1 - 0.3f })
                foreach (float z in new[] { tz0 + 0.3f, tz1 - 0.3f })
                    Box(table, $"Leg_{x:0}_{z:0}", new Vector3(x, G + 0.425f, z), new Vector3(0.3f, 0.85f, 0.3f), accentMat);

            var chairs = Group(l, "Chairs", Vector3.zero);
            for (int i = 0; i < 9; i++)
            {
                float x = 24f + i * 1.5f;
                Chair(chairs, $"S_{i}", new Vector3(x, G, tz0 - 0.65f), Vector3.back);
                Chair(chairs, $"N_{i}", new Vector3(x, G, tz1 + 0.65f), Vector3.forward);
            }
            Chair(chairs, "End_W", new Vector3(tx0 - 0.65f, G, (tz0 + tz1) / 2f), Vector3.left);
            Chair(chairs, "End_E", new Vector3(tx1 + 0.65f, G, (tz0 + tz1) / 2f), Vector3.right);

            // 병풍: 4m 높이 접이식 판(지그재그). 식탁 둘레를 두르되 네 모서리는 틈.
            var screens = Group(l, "FoldingScreens", Vector3.zero);
            float span = 8 * ScreenStep;
            FoldingScreen(screens, "S", new Vector3(30f - span / 2f, G, 16.6f), true, 8);
            FoldingScreen(screens, "N", new Vector3(30f - span / 2f, G, 26.6f), true, 8);
            float spanZ = 6 * ScreenStep;
            FoldingScreen(screens, "W", new Vector3(20.6f, G, 21.5f - spanZ / 2f), false, 6);
            FoldingScreen(screens, "E", new Vector3(39.4f, G, 21.5f - spanZ / 2f), false, 6);

            // 샹들리에(보이기만, 충돌 없음): 고리 + 사슬
            var ch = Group(l, "Chandelier", new Vector3(30f, 13f, 21.5f));
            const float r = 3.5f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var p = new Vector3(r * Mathf.Cos(a * Mathf.Deg2Rad), 0f, r * Mathf.Sin(a * Mathf.Deg2Rad));
                BoxR(ch, $"Ring_{k}", p, new Vector3(2.9f, 0.3f, 0.3f), Quaternion.Euler(0f, -(90f + a), 0f), accentMat, false);
                BoxR(ch, $"Candle_{k}", p + Vector3.up * 0.45f, new Vector3(0.15f, 0.6f, 0.15f), Quaternion.identity, wallMat, false);
            }
            BoxR(ch, "Hub", Vector3.zero, new Vector3(1f, 1.2f, 1f), Quaternion.identity, accentMat, false);
            BoxR(ch, "Chain", new Vector3(0f, (RoofTop - SlabT - 13f) / 2f, 0f), new Vector3(0.12f, RoofTop - SlabT - 13f, 0.12f), Quaternion.identity, accentMat, false);
        }

        /// <summary>정면·뒷면 버팀벽, 모서리 탑 4개(뾰족 지붕), 본채 박공지붕·박공벽·첨탑.</summary>
        private static void BuildGothicExterior(Transform g)
        {
            // 버팀벽(창·문 사이)
            foreach (float x in new[] { 19.5f, 23.5f, 36.5f, 40.5f })
                Buttress(g, $"Butt_N_{x:0.#}", x, 36.2f, 1.2f, RoofTop + 1f);
            foreach (float x in new[] { 19.5f, 24.5f, 35.5f, 40.5f })
                Buttress(g, $"Butt_S_{x:0.#}", x, -0.2f, -1.2f, RoofTop + 1f);
            foreach (float x in new[] { 7f, 53f })
                Buttress(g, $"Butt_WingN_{x:0}", x, 36.2f, 1.2f, F3 + 1.2f);

            // 모서리 탑(속이 찬 돌탑 + 단계로 좁아지는 뾰족 지붕)
            foreach (var c in new[] { new Vector2(-2.2f, -2.2f), new Vector2(-2.2f, 38.2f), new Vector2(62.2f, -2.2f), new Vector2(62.2f, 38.2f) })
            {
                var t = Group(g, $"Tower_{c.x:0}_{c.y:0}", new Vector3(c.x, 0f, c.y));
                Box(t, "Base", new Vector3(0f, (-0.3f + 22f) / 2f, 0f), new Vector3(6f, 22.3f, 6f), wallMat);
                Box(t, "Cornice", new Vector3(0f, 22.3f, 0f), new Vector3(6.6f, 0.6f, 6.6f), accentMat);
                Box(t, "Spire_1", new Vector3(0f, 24f, 0f), new Vector3(4.4f, 3f, 4.4f), accentMat);
                Box(t, "Spire_2", new Vector3(0f, 27f, 0f), new Vector3(2.8f, 3f, 2.8f), accentMat);
                Box(t, "Spire_3", new Vector3(0f, 30.5f, 0f), new Vector3(1.2f, 4f, 1.2f), accentMat);
                Box(t, "Spire_Tip", new Vector3(0f, 34f, 0f), new Vector3(0.4f, 3f, 0.4f), accentMat);
            }

            // 본채 박공지붕(가파른 두 경사면, 용마루는 남북 방향) + 계단식 박공벽 + 첨탑
            const float x0 = 13.75f, x1 = 46.25f, ridge = 12f;
            float half = (x1 - x0) / 2f, mid = (x0 + x1) / 2f;
            float angle = Mathf.Atan2(ridge, half) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(half * half + ridge * ridge);
            BoxR(g, "Roof_W", new Vector3(x0 + half / 2f, RoofTop + ridge / 2f, 18f), new Vector3(len, 0.4f, 37f), Quaternion.Euler(0f, 0f, angle), accentMat, true);
            BoxR(g, "Roof_E", new Vector3(x1 - half / 2f, RoofTop + ridge / 2f, 18f), new Vector3(len, 0.4f, 37f), Quaternion.Euler(0f, 0f, -angle), accentMat, true);
            for (int i = 0; i < 4; i++)
            {
                float w = (x1 - x0) * (1f - i / 4f) - 1f;
                foreach (float z in new[] { 36f, 0f })
                    Box(g, $"Gable_{(z > 0 ? "N" : "S")}_{i}", new Vector3(mid, RoofTop + i * 3f + 1.5f, z), new Vector3(w, 3f, WallT), wallMat);
            }
            Box(g, "Fleche", new Vector3(mid, RoofTop + ridge + 4f, 18f), new Vector3(1.4f, 8f, 1.4f), accentMat);
            Box(g, "Fleche_Tip", new Vector3(mid, RoofTop + ridge + 10f, 18f), new Vector3(0.4f, 4f, 0.4f), accentMat);
        }

        private static void Buttress(Transform p, string name, float x, float wallFace, float depth, float top)
        {
            float z0 = Mathf.Min(wallFace, wallFace + depth), z1 = Mathf.Max(wallFace, wallFace + depth);
            BoxMM(p, name, new Vector3(x - 0.4f, -0.3f, z0), new Vector3(x + 0.4f, top, z1), wallMat);
        }

        // ───────────────────────────── 정원 ─────────────────────────────
        // 월드 좌표. 시작 지점 근처(x -5..15, z 10..20: 플레이어·허수아비·연습 벽)는 비워 둔다.
        private static void BuildGrounds(Transform g)
        {
            const float B = -0.3f;

            // 앞뜰: 분수, 석상, 산울타리 L자, 정자, 상자
            var front = Group(g, "Front", Vector3.zero);
            Fountain(front, "Fountain", new Vector3(0f, 0f, 32f), 5f);
            foreach (var p in new[] { new Vector2(-8f, 42f), new Vector2(8f, 42f), new Vector2(-20f, 30f), new Vector2(20f, 30f) })
            {
                Box(front, $"Plinth_{p.x:0}_{p.y:0}", new Vector3(p.x, 0.25f, p.y), new Vector3(2f, 1.1f, 2f), accentMat);
                Pillar(front, $"Statue_{p.x:0}_{p.y:0}", p.x, p.y, 1f, 0.8f, 4.3f);
            }
            WallX(front, "Hedge_W_A", -30f, -20f, 8f, B, 2.8f - B, 1f, wallMat);
            WallZ(front, "Hedge_W_B", 8f, 16f, -30f, B, 2.8f - B, 1f, wallMat);
            WallX(front, "Hedge_E_A", 20f, 30f, 8f, B, 2.8f - B, 1f, wallMat);
            WallZ(front, "Hedge_E_B", 8f, 16f, 30f, B, 2.8f - B, 1f, wallMat);
            Gazebo(front, "Gazebo", new Vector3(-38f, 0f, 32f), 7f);
            Crates(front, "Crates_NE", new Vector2(38f, 36f));

            // 서쪽 정원: 창고, 창문 벽 2개, 낮은 벽, 기둥
            var west = Group(g, "West", Vector3.zero);
            Shack(west, "Shed", new Vector3(-48f, 0f, 8f), 7f, 6f,
                s: new[] { Win(3.5f) }, n: new[] { Win(3.5f) }, w: new[] { Door(3f) }, e: new[] { Door(3f) });
            WallX(west, "WindowWall_N", -48f, -38f, -6f, B, 3.3f, 0.5f, wallMat, Notch(-43f, 2f, WinSill - B));
            WallZ(west, "WindowWall_S", -32f, -16f, -44f, B, 3.3f, 0.5f, wallMat, Notch(-24f, 2f, WinSill - B));
            WallX(west, "LowWall", -42f, -36f, -40f, B, 1f - B, 0.4f, accentMat);
            Pillar(west, "Pillar_A", -40f, -30f, 1.2f, B, 3.5f);
            Pillar(west, "Pillar_B", -46f, -36f, 1.2f, B, 3.5f);

            // 동쪽 묘지: 낮은 담(동쪽은 울타리), 묘비, 납골당
            var yard = Group(g, "Graveyard", Vector3.zero);
            WallX(yard, "Wall_S", 38f, 49f, -32f, B, 1f - B, 0.4f, accentMat, Gap(41.5f, 44f));
            WallX(yard, "Wall_N", 38f, 49f, -2f, B, 1f - B, 0.4f, accentMat, Gap(41.5f, 44f));
            WallZ(yard, "Wall_W", -32f, -2f, 38f, B, 1f - B, 0.4f, accentMat, Gap(-25f, -22.5f), Gap(-11f, -8.5f));
            Shack(yard, "Mausoleum", new Vector3(40.5f, 0f, -20f), 6f, 7f,
                s: new[] { Door(3f) }, n: new[] { Door(3f) }, w: new[] { Win(3.5f) }, e: new[] { Win(3.5f) });
            foreach (float x in new[] { 40.5f, 43.5f, 46.5f })
                for (float z = -29f; z <= -4f; z += 3f)
                {
                    if (z > -21.5f && z < -12f) continue; // 납골당 자리
                    Box(yard, $"Tomb_{x:0}_{-z:0}", new Vector3(x, 0.5f, z), new Vector3(0.9f, 1.3f, 0.3f), accentMat);
                }

            // 뒤뜰: L벽 2개, 기둥, 상자
            var rear = Group(g, "Rear", Vector3.zero);
            WallX(rear, "LWall_W_A", -12f, -4f, -45f, B, 3.3f, 0.5f, wallMat);
            WallZ(rear, "LWall_W_B", -48f, -45f, -4f, B, 3.3f, 0.5f, wallMat);
            WallX(rear, "LWall_E_A", 4f, 12f, -45f, B, 3.3f, 0.5f, wallMat);
            WallZ(rear, "LWall_E_B", -48f, -45f, 4f, B, 3.3f, 0.5f, wallMat);
            Pillar(rear, "Pillar_C", 0f, -46f, 1.2f, B, 3.5f);
            Crates(rear, "Crates_W", new Vector2(-26f, -46f));
            Crates(rear, "Crates_E", new Vector2(24f, -46f));
        }

        // 둘레 철책 x·z ±49(지형 둔덕 안쪽), 모서리 7m 깎음, 기둥 6m마다.
        private static void BuildPerimeter(Transform p)
        {
            const float x0 = -49f, x1 = 49f, z0 = -49f, z1 = 49f, c = 7f, h = 3.5f, t = 0.3f, b = -0.3f;
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

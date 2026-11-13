using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 술래잡기(감시자 1 : 생존자) 맵 회색 블록아웃을 씬에 만든다. 메뉴: Tools/BG/맵 블록아웃 다시 만들기.
    /// 씬의 "MapBlockout" 하나를 지우고 다시 세운다(다른 씬 오브젝트는 건드리지 않음). 맵을 바꾸려면 이 파일 숫자를 고치고 다시 실행한다.
    ///
    /// 크기 기준: 캐릭터 키 약 2.1m(감시자 ~2.7m), 반지름 0.4~0.5, 점프 1.2m, stepOffset 0.3, slopeLimit 45°.
    ///   층고 5.5m(천장 높이 5.2m, 3인칭 카메라가 실내에서 답답하지 않게), 문 2.4×3.8m, 창문 1.8m 폭·턱 1.0m·위 4.0m(점프로 넘음, 틈 높이 3.0m), 아치 3.0×4.4m,
    ///   통로 2.5m 이상, 계단 폭 2.6~2.8m·단 0.28m·디딤 0.4m(약 35°, 위에 보이지 않는 경사 Collider로 미끄럽게),
    ///   난간·낮은 벽 1.0m(점프로 넘음), 큰 벽 3m 이상(못 넘음, 시야 가림).
    /// 루키노 도약(머리 위 6m 천장 검사): 실내 1F·2F·창고는 못 뜀, 안뜰·발코니·야외 마당은 뜀.
    ///
    /// 배치(월드 좌표, x=동, z=북). 본관 28×19m, 남서 모서리 (-19, -21).
    ///   1F: 남쪽 홀(정문·동문) / 서쪽 방(계단 A) / 북쪽 복도(뒷문·서문, 계단 B 입구) / 동쪽 방, 가운데 8×8 안뜰(하늘 열림).
    ///       안뜰 남·북·서는 아치, 동은 창문. 방마다 문 2개 이상이라 안뜰을 도는 고리가 실내에 생긴다.
    ///   2F: 1F와 같은 방 배치, 안뜰 둘레 난간(남·북 난간 틈으로 안뜰에 뛰어내림), 남쪽 발코니(외부 계단 C로 마당과 연결,
    ///       난간 틈 2곳에서 남쪽 마당으로 뛰어내림, 아래는 기둥 회랑), 북동 발코니(북쪽 마당으로 뛰어내림), 바깥 창문에서도 뛰어내림.
    ///   마당: 둘레 울타리(모서리 깎아 막다른 곳 없앰) 안에 창고 2채(문 2개 + 창문), T벽·L벽, 창문 벽, 낮은 벽, 기둥, 상자.
    /// </summary>
    public static class MapBlockoutBuilder
    {
        private const string RootName = "MapBlockout";
        private const string MatFolder = "Assets/Environment/Materials/Blockout";

        // 높이
        private const float G = 0.15f;      // 1F 바닥 윗면(지형 ≈ 0)
        private const float Story = 5.5f;   // 층고(바닥 윗면 → 다음 바닥 윗면). 1F 천장 높이 5.2m
        private const float SlabT = 0.3f;    // 바닥·지붕 두께
        private const float F2 = G + Story; // 2F 바닥 윗면
        private const float RoofTop = F2 + Story;

        // 벽·개구부
        private const float WallT = 0.4f;
        private const float DoorW = 2.4f, DoorH = 3.8f;
        private const float WinW = 1.8f, WinSill = 1.0f, WinTop = 4.0f;
        private const float ArchW = 3f, ArchH = 4.4f;
        private const float RailH = 1f, RailT = 0.15f;
        private const float LowTerraceH = 2.4f; // 낮은 테라스·부두 높이: 땅에서 매달려 올라가는 턱(점프 꼭대기 발 기준 1.2m 위)

        // 계단
        private const int StairSteps = 20;
        private const float StairRun = 0.4f;

        private static readonly Vector3 BuildingOrigin = new(-19f, 0f, -21f);
        private static readonly Vector3 AnnexOrigin = new(14f, 0f, -8f);

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
        private static Opening Win(float c) => new(c - WinW / 2f, c + WinW / 2f, WinSill, WinTop);
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

            BuildMainHouse(Group(root, "MainHouse", BuildingOrigin));
            BuildAnnex(Group(root, "Annex", AnnexOrigin));
            BuildYard(Group(root, "Yard", Vector3.zero));
            BuildPerimeter(Group(root, "Perimeter", Vector3.zero));

            int count = root.GetComponentsInChildren<Renderer>().Length;
            EditorSceneManager.MarkSceneDirty(scene);
            message = $"MapBlockout: 블록 {count}개 생성";
            return true;
        }

        // ───────────────────────────── 본관(2층) ─────────────────────────────
        // 로컬 좌표: x 0..28(동), z 0..19(북). 안뜰 x 10..18, z 6..14.
        private static void BuildMainHouse(Transform house)
        {
            // 판 가장자리는 바깥벽 두께 안에 숨긴다(벽 겉면과 같은 면이면 깜빡임).
            var outer = new Rect2(-0.1f, -0.1f, 28.1f, 19.1f);
            const float InH = Story - SlabT;    // 칸막이 벽: 위층 판 밑면까지(판 윗면과 겹쳐 깜빡이지 않게)
            const float CourtH = Story - 0.02f; // 안뜰 벽: 판 윗면보다 살짝 낮게
            var court = new Rect2(10f, 6f, 18f, 14f);
            var holeA = new Rect2(0.2f, 3f, 3f, 3f + (StairSteps - 1) * StairRun);         // 계단 A: 서쪽 방, 북쪽으로 올라감
            var holeB = new Rect2(25f, 16.2f - (StairSteps - 1) * StairRun, 27.8f, 16.2f);  // 계단 B: 동쪽, 남쪽으로 올라감

            // 바닥·지붕
            var floors = Group(house, "Floors", Vector3.zero);
            Slab(floors, "1F_Floor", outer, G, 0.35f, floorMat);
            Slab(floors, "2F_Floor", outer, F2, SlabT, floorMat, court, holeA, holeB);
            Slab(floors, "Roof", outer, RoofTop, SlabT, accentMat, court);
            // 지붕 난간(겉모습)
            var roof = Group(house, "RoofParapet", Vector3.zero);
            WallX(roof, "Parapet_S", -0.2f, 28.2f, 0f, RoofTop, 0.8f, WallT, accentMat);
            WallX(roof, "Parapet_N", -0.2f, 28.2f, 19f, RoofTop, 0.8f, WallT, accentMat);
            WallZ(roof, "Parapet_W", 0f, 19f, 0f, RoofTop, 0.8f, WallT, accentMat);
            WallZ(roof, "Parapet_E", 0f, 19f, 28f, RoofTop, 0.8f, WallT, accentMat);

            // 1F
            var w1 = Group(house, "1F_Walls", Vector3.zero);
            WallX(w1, "Outer_S", -0.2f, 28.2f, 0f, G, Story, WallT, wallMat, Win(9.9f), Door(14f), Win(19.9f), Win(24.4f));
            WallX(w1, "Outer_N", -0.2f, 28.2f, 19f, G, Story, WallT, wallMat, Win(5f), Door(14f), Win(22.9f));
            WallZ(w1, "Outer_W", 0f, 19f, 0f, G, Story, WallT, wallMat, Win(1.9f), Door(16.5f));
            WallZ(w1, "Outer_E", 0f, 19f, 28f, G, Story, WallT, wallMat, Door(4.4f), Win(7.4f));
            WallX(w1, "Court_S", 9.8f, 18.2f, 6f, G, CourtH, WallT, wallMat, Arch(14f));
            WallX(w1, "Court_N", 9.8f, 18.2f, 14f, G, CourtH, WallT, wallMat, Arch(14f));
            WallZ(w1, "Court_W", 6f, 14f, 10f, G, CourtH, WallT, wallMat, Arch(10f));
            WallZ(w1, "Court_E", 6f, 14f, 18f, G, CourtH, WallT, wallMat, Win(10f));
            WallX(w1, "Part_W_S", 3f, 10f, 6f, G, InH, WallT, wallMat, Door(6.5f));
            WallX(w1, "Part_W_N", 0.2f, 10f, 14f, G, InH, WallT, wallMat, Door(6.5f));
            WallX(w1, "Part_E_S", 18f, 27.8f, 6f, G, InH, WallT, wallMat, Door(21.5f));
            WallX(w1, "Part_E_N", 18f, 25f, 14f, G, InH, WallT, wallMat, Door(21.5f));
            // 계단 옆 막이 벽: 계단 중간에서 옆방으로 떨어져 들어가는 샛길을 막는다.
            WallZ(w1, "StairA_Side", 6f, 14f, 3f + WallT / 2f, G, InH, WallT, wallMat);
            WallZ(w1, "StairB_Side", holeB.z0, 14f, 25f - WallT / 2f, G, InH, WallT, wallMat);

            // 계단
            var stairs = Group(house, "Stairs", Vector3.zero);
            Stair(stairs, "Stair_A", new Vector3(1.6f, G, holeA.z0), Vector3.forward, 2.8f, Story);
            Stair(stairs, "Stair_B", new Vector3(26.4f, G, holeB.z1), Vector3.back, 2.8f, Story);

            // 안뜰 가운데 낮은 화단(점프로 올라섬)
            Box(house, "Court_Planter", new Vector3(14f, G + 0.5f, 10f), new Vector3(2.4f, 1f, 2.4f), accentMat);

            // 2F
            var w2 = Group(house, "2F_Walls", Vector3.zero);
            WallX(w2, "Outer_S", -0.2f, 28.2f, 0f, F2, Story, WallT, wallMat, Win(3.9f), Door(11.2f), Win(15.9f), Door(20.2f), Win(25f));
            WallX(w2, "Outer_N", -0.2f, 28.2f, 19f, F2, Story, WallT, wallMat, Win(5.9f), Win(14f), Door(23.2f));
            WallZ(w2, "Outer_W", 0f, 19f, 0f, F2, Story, WallT, wallMat, Win(12f), Win(16f));
            WallZ(w2, "Outer_E", 0f, 19f, 28f, F2, Story, WallT, wallMat, Win(4.9f), Door(17.4f)); // 17.4 = 별관 다리 문
            WallX(w2, "Part_W_S", 3f, 10f, 6f, F2, InH, WallT, wallMat, Door(6.5f));
            WallX(w2, "Part_W_N", 0.2f, 10f, 14f, F2, InH, WallT, wallMat, Door(6.5f));
            WallX(w2, "Part_E_S", 18f, 27.8f, 6f, F2, InH, WallT, wallMat, Door(22f));
            WallX(w2, "Part_E_N", 18f, 25f, 14f, F2, InH, WallT, wallMat, Door(21.5f));

            var r2 = Group(house, "2F_Railings", Vector3.zero);
            WallX(r2, "Court_S", 10f, 18f, 6f, F2, RailH, RailT, accentMat, Gap(13f, 15f));
            WallX(r2, "Court_N", 10f, 18f, 14f, F2, RailH, RailT, accentMat, Gap(13f, 15f));
            WallZ(r2, "Court_W", 6f, 14f, 10f, F2, RailH, RailT, accentMat);
            WallZ(r2, "Court_E", 6f, 14f, 18f, F2, RailH, RailT, accentMat);
            WallZ(r2, "StairA_E", holeA.z0, holeA.z1, holeA.x1, F2, RailH, RailT, accentMat);
            WallX(r2, "StairA_S", holeA.x0, holeA.x1, holeA.z0, F2, RailH, RailT, accentMat);
            WallZ(r2, "StairB_W", holeB.z0, holeB.z1, holeB.x0, F2, RailH, RailT, accentMat);
            WallX(r2, "StairB_N", holeB.x0, holeB.x1, holeB.z1, F2, RailH, RailT, accentMat);

            // 남쪽 발코니 + 외부 계단 C + 아래 기둥 회랑
            var bs = Group(house, "Balcony_S", Vector3.zero);
            Slab(bs, "Floor", new Rect2(8f, -4.5f, 26f, -0.2f), F2, SlabT, floorMat); // 깊이 4.5m 테라스
            WallX(bs, "Rail_S", 8f, 26f, -4.425f, F2, RailH, RailT, accentMat, Gap(12f, 14f), Gap(21f, 23f));
            WallZ(bs, "Rail_E", -4.5f, -0.2f, 25.925f, F2, RailH, RailT, accentMat);
            foreach (float x in new[] { 8.4f, 11f, 17f, 22f, 25.6f })
                Pillar(bs, $"Pillar_{x:0.#}", x, -4.1f, 0.6f, -0.3f, F2 - SlabT);
            // 지형(≈0)에서 2F 바닥까지. 맨 윗단이 발코니 서쪽 끝(x 8)에 닿는다.
            Stair(bs, "Stair_C_Outside", new Vector3(8f - (StairSteps - 1) * StairRun, 0f, -1.5f), Vector3.right, 2.6f, F2);

            // 북동 발코니(뛰어내리는 자리)
            var bn = Group(house, "Balcony_NE", Vector3.zero);
            Slab(bn, "Floor", new Rect2(18f, 19.2f, 28.2f, 21.5f), F2, SlabT, floorMat);
            WallX(bn, "Rail_N", 18f, 28.2f, 21.425f, F2, RailH, RailT, accentMat, Gap(20.5f, 22.5f));
            WallZ(bn, "Rail_W", 19.2f, 21.5f, 18.075f, F2, RailH, RailT, accentMat);
            WallZ(bn, "Rail_E", 19.2f, 21.5f, 28.125f, F2, RailH, RailT, accentMat);
            Pillar(bn, "Pillar_W", 18.4f, 21.1f, 0.5f, -0.3f, F2 - SlabT);
            Pillar(bn, "Pillar_E", 27.8f, 21.1f, 0.5f, -0.3f, F2 - SlabT);

            // 서쪽 낮은 테라스(2.4m): 땅에서 매달려 올라가거나, 남쪽 상자(1.2m)를 밟고 두 번 뛰어 올라섬.
            // 서쪽 가장자리 북쪽 절반은 난간(매달렸다 난간 넘어 올라감), 남쪽 절반은 맨 턱.
            var tw = Group(house, "Terrace_W", Vector3.zero);
            BoxMM(tw, "Block", new Vector3(-5f, -0.3f, 5f), new Vector3(-0.2f, LowTerraceH, 12f), accentMat);
            WallZ(tw, "Rail_W", 8.5f, 12f, -4.925f, LowTerraceH, RailH, RailT, accentMat);
            BoxMM(tw, "StepCrate", new Vector3(-3.8f, -0.3f, 3.8f), new Vector3(-2.6f, 1.2f, 5f), accentMat);
        }

        // ───────────────────────────── 별관(1층 + 옥상 테라스) ─────────────────────────────
        // 로컬 좌표: x 0..16(동), z 0..14(북). 월드 x 14..30, z -8..6. 옥상(= 본관 2F 높이)이 하늘 열린 넓은 테라스.
        // 옥상 오르는 길: 본관 2F 동쪽 문 → 다리 / 북쪽 바깥 계단 D / 동쪽 부두(2.4m)에서 매달려 올라감(난간 있는 곳·없는 곳).
        // 옥상 가운데 작은 정자(지붕 3.4m: 매달려 올라섬), 큰 벽·낮은 벽. 옥상 가장자리 난간 틈으로 뛰어내림.
        private static void BuildAnnex(Transform annex)
        {
            const float sx = 16f, sz = 14f;
            const float InH = Story - SlabT;

            // 1F 바닥·옥상(옥상 판은 벽 겉면보다 5cm 밖으로: 같은 면 깜빡임 방지)
            Slab(annex, "1F_Floor", new Rect2(-0.1f, -0.1f, sx + 0.1f, sz + 0.1f), G, 0.35f, floorMat);
            Slab(annex, "Roof", new Rect2(-0.25f, -0.25f, sx + 0.25f, sz + 0.25f), F2, SlabT, floorMat);

            // 1F 벽(위는 옥상 판 밑면까지)
            var w = Group(annex, "1F_Walls", Vector3.zero);
            WallX(w, "Outer_S", -0.2f, sx + 0.2f, 0f, G, InH, WallT, wallMat, Win(3f), Door(8f), Win(13f));
            WallX(w, "Outer_N", -0.2f, sx + 0.2f, sz, G, InH, WallT, wallMat, Door(13.5f));
            WallZ(w, "Outer_W", 0f, sz, 0f, G, InH, WallT, wallMat, Door(3f), Win(10f));
            WallZ(w, "Outer_E", 0f, sz, sx, G, InH, WallT, wallMat, Door(1.6f), Win(12.5f));
            // 가운데 막이 벽(창문) + 기둥 2개: 실내에서 도는 고리
            WallX(w, "Inner", 4f, 12f, 7f, G, InH, WallT, wallMat, Win(8f));
            Pillar(w, "Pillar_W", 4f, 3.5f, 0.8f, G, F2 - SlabT);
            Pillar(w, "Pillar_E", 12f, 3.5f, 0.8f, G, F2 - SlabT);

            // 옥상 난간(틈 = 뛰어내리는 자리, 다리·계단 자리는 열림, 동쪽 z 7..11은 맨 턱 = 부두에서 매달려 올라옴)
            var r = Group(annex, "Roof_Railings", Vector3.zero);
            const float e = 0.25f - RailT / 2f;
            WallX(r, "S", -0.25f, sx + 0.25f, -e, F2, RailH, RailT, accentMat, Gap(3f, 5f), Gap(11f, 13f));
            WallX(r, "N", -0.25f, sx + 0.25f, sz + e, F2, RailH, RailT, accentMat, Gap(8.6f, 11.2f));
            WallZ(r, "W", -0.25f, sz + 0.25f, -e, F2, RailH, RailT, accentMat, Gap(2.9f, 5.9f), Gap(10f, 12f));
            WallZ(r, "E", -0.25f, sz + 0.25f, sx + e, F2, RailH, RailT, accentMat, Gap(7f, 11f));

            // 옥상 위 엄폐물
            var top = Group(annex, "Roof_Cover", Vector3.zero);
            WallZ(top, "TallWall", 1f, 5.5f, 5f, F2, 2.6f, 0.4f, wallMat);
            WallX(top, "LowWall", 2f, 6f, 11.5f, F2, 1f, 0.4f, accentMat);
            var kioskDoor = new Opening(0.3f, 2.7f, 0f, 2.8f);
            Shack(top, "Kiosk", new Vector3(9f, F2, 7f), 4f, 3f,
                s: new[] { Win(2f) }, n: new[] { Win(2f) }, w: new[] { kioskDoor }, e: new[] { kioskDoor }, h: 3f);

            // 다리: 본관 2F 동쪽 문(월드 x 9, z -3.6) → 옥상 서쪽(로컬 z 2.9..5.9). 남쪽 난간 가운데 틈.
            var br = Group(annex, "Bridge", Vector3.zero);
            Slab(br, "Floor", new Rect2(-4.8f, 2.9f, -0.25f, 5.9f), F2, SlabT, floorMat);
            WallX(br, "Rail_N", -4.8f, -0.25f, 5.9f - RailT / 2f, F2, RailH, RailT, accentMat);
            WallX(br, "Rail_S", -4.8f, -0.25f, 2.9f + RailT / 2f, F2, RailH, RailT, accentMat, Gap(-3.3f, -1.8f));

            // 북쪽 바깥 계단 D(서→동으로 올라감) + 꼭대기 층계참(옥상 북쪽 난간 틈으로 이어짐)
            var sd = Group(annex, "Stair_D", Vector3.zero);
            float topX = 1f + (StairSteps - 1) * StairRun;
            Stair(sd, "Steps", new Vector3(1f, 0f, 15.5f), Vector3.right, 2.5f, F2);
            Slab(sd, "Landing", new Rect2(topX, sz + 0.25f, 11.2f, 16.75f), F2, SlabT, floorMat);
            WallX(sd, "Landing_Rail_N", topX, 11.2f, 16.75f - RailT / 2f, F2, RailH, RailT, accentMat);
            WallZ(sd, "Landing_Rail_E", sz + 0.25f, 16.75f, 11.2f - RailT / 2f, F2, RailH, RailT, accentMat);
            Pillar(sd, "Landing_Pillar", 10.9f, 16.45f, 0.5f, -0.3f, F2 - SlabT);

            // 동쪽 부두(2.4m): 땅에서 매달려 올라감, 북쪽 상자(1.2m)로 뛰어 올라섬. 부두 위에서 옥상(3.25m 위)으로 매달려 올라감.
            var dock = Group(annex, "Dock_E", Vector3.zero);
            BoxMM(dock, "Block", new Vector3(sx + 0.2f, -0.3f, 3f), new Vector3(sx + 3f, LowTerraceH, 11f), accentMat);
            BoxMM(dock, "StepCrate", new Vector3(sx + 1.4f, -0.3f, 11f), new Vector3(sx + 2.6f, 1.2f, 12.2f), accentMat);
        }

        // ───────────────────────────── 야외 마당 ─────────────────────────────
        // 월드 좌표. 시작 지점 근처(x -5..15, z 10..20: 플레이어·허수아비·연습 벽)는 비워 둔다.
        private static void BuildYard(Transform yard)
        {
            const float B = -0.3f; // 지형 속으로 조금 묻는다

            // 남서 창고: 동·서 문, 남·북 창문
            Shack(yard, "Shed_SW", new Vector3(-30f, 0f, -39f), 8f, 6f,
                s: new[] { Win(4f) }, n: new[] { Win(4f) }, w: new[] { Door(3f) }, e: new[] { Door(3f) });
            // 동쪽 창고: 남·북 문, 동·서 창문
            Shack(yard, "Shed_E", new Vector3(21f, 0f, -30f), 6f, 7f,
                s: new[] { Door(3f) }, n: new[] { Door(3f) }, w: new[] { Win(3.5f) }, e: new[] { Win(3.5f) });

            var cover = Group(yard, "Cover", Vector3.zero);
            // 남쪽 T벽(큰 벽)
            WallX(cover, "TWall_Bar", -2f, 6f, -36f, B, 3.3f, 0.5f, wallMat);
            WallZ(cover, "TWall_Stem", -36f, -29f, 2f, B, 3.3f, 0.5f, wallMat);
            // 남동 L벽
            WallX(cover, "LWall_SE_A", 26f, 32.25f, -42f, B, 3.3f, 0.5f, wallMat);
            WallZ(cover, "LWall_SE_B", -42f, -36f, 32f, B, 3.3f, 0.5f, wallMat);
            // 서쪽 L벽
            WallX(cover, "LWall_W_A", -35.25f, -28f, -17f, B, 3.3f, 0.5f, wallMat);
            WallZ(cover, "LWall_W_B", -17f, -10f, -35f, B, 3.3f, 0.5f, wallMat);
            // 서쪽 창문 벽(창문과 같은 턱 1.0m 틈을 점프로 넘음)
            WallZ(cover, "WindowWall_W", -4f, 6f, -30f, B, 3.3f, 0.5f, wallMat, Notch(1f, 2f, WinSill - B));
            // 북서 긴 벽(가운데 창문 틈)
            WallX(cover, "LongWall_NW", -30f, -18f, 12f, B, 3.3f, 0.5f, wallMat, Notch(-24f, 2f, WinSill - B));
            // 낮은 벽(1m, 점프로 넘음)
            WallX(cover, "LowWall_SE_1", 13f, 19f, -33f, B, 1f - B, 0.4f, accentMat);
            WallX(cover, "LowWall_SE_2", 13f, 19f, -40f, B, 1f - B, 0.4f, accentMat);
            WallX(cover, "LowWall_NE", 18f, 24f, 14f, B, 1f - B, 0.4f, accentMat);
            WallZ(cover, "LowWall_E", -20f, -14f, 33f, B, 1f - B, 0.4f, accentMat);
            // 기둥
            foreach (var p in new[] { new Vector2(-12f, -40f), new Vector2(-8f, -40f), new Vector2(-10f, -36f),
                                      new Vector2(20f, 22f), new Vector2(28f, 22f), new Vector2(24f, 26f) })
                Pillar(cover, $"Pillar_{p.x:0}_{p.y:0}", p.x, p.y, 1.2f, B, 3.5f);
            // 상자(1.2m, 점프로 올라섬)
            Crates(cover, "Crates_W", new Vector2(-36f, 14f));
            Crates(cover, "Crates_E", new Vector2(16f, -26f));
            Crates(cover, "Crates_N", new Vector2(-8f, 24f));
        }

        // 둘레 울타리 x -42..36, z -46..30. 모서리는 6m씩 비스듬히 깎는다.
        private static void BuildPerimeter(Transform p)
        {
            const float x0 = -42f, x1 = 36f, z0 = -46f, z1 = 30f, c = 6f, h = 4.3f, t = 0.6f, b = -0.3f;
            WallX(p, "S", x0 + c, x1 - c, z0, b, h, t, wallMat);
            WallX(p, "N", x0 + c, x1 - c, z1, b, h, t, wallMat);
            WallZ(p, "W", z0 + c, z1 - c, x0, b, h, t, wallMat);
            WallZ(p, "E", z0 + c, z1 - c, x1, b, h, t, wallMat);
            Diagonal(p, "SW", new Vector2(x0, z0 + c), new Vector2(x0 + c, z0), b, h, t);
            Diagonal(p, "SE", new Vector2(x1 - c, z0), new Vector2(x1, z0 + c), b, h, t);
            Diagonal(p, "NW", new Vector2(x0, z1 - c), new Vector2(x0 + c, z1), b, h, t);
            Diagonal(p, "NE", new Vector2(x1 - c, z1), new Vector2(x1, z1 - c), b, h, t);
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

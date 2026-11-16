using System.Collections.Generic;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 화면 정보 칸에 쓰는 조작 설명(아주 짧게). 게임 코드(스킬·CharacterLoadout)의 실제 동작을 줄여 쓴 것 — 동작이 바뀌면 여기도 고친다.
    /// 감시자: 좌클릭 평타 / 좌클릭 차징 / E 스킬. 생존자: 좌클릭 평타 / 좌클릭 차징 = 패링 / E 스킬 / 스페이스바 판자 내리기.
    /// </summary>
    public static class CharacterControlsText
    {
        public readonly struct Controls
        {
            public readonly string Charge, Skill;
            /// <summary>마우스를 올리면 뜨는 자세한 설명(없으면 null) — 차징(감시자) / E 스킬 / 평타(기본 설명 대신).</summary>
            public readonly string ChargeTip, SkillTip, AttackTip;
            public Controls(string charge, string skill, string chargeTip = null, string skillTip = null, string attackTip = null)
            { Charge = charge; Skill = skill; ChargeTip = chargeTip; SkillTip = skillTip; AttackTip = attackTip; }
        }

        public const string HunterChargeDefault = "모아서 강하게 치기";
        public const string SurvivorCharge = "패링";
        public const string ParryTooltip = "누르고 있는 동안 정면에서 오는 팔을 사용한 공격을 막는다.\n막으면 상대를 0.5초 스턴한다.\n떼면 돌려차기.";
        public const string PalletDrop = "판자 내리기";
        public const string PalletDropDesc = "판자 옆에서 · 깔린 감시자는 경직";
        // 평타 설명(마우스를 올리면). 생존자는 주먹, 감시자는 무기. 맹인은 지팡이.
        public const string SurvivorAttackTooltip = "주먹으로 공격한다.\n달리며 점프하면 발차기.";
        public const string HunterAttackTooltip = "무기로 공격한다.\n달리며 점프하면 내려치기.";
        public const string MindsEyeAttackTooltip = "지팡이로 공격해 밀어낸다.\n달리며 점프하면 발차기.";

        public static string AttackTooltip(CharacterRoster.Entry entry)
        {
            if (entry != null && ById.TryGetValue(entry.id, out var c) && c.AttackTip != null) return c.AttackTip;
            return entry != null && entry.id == "Survivor_MindsEye" ? MindsEyeAttackTooltip
                 : entry != null && entry.role == CharacterRole.Hunter ? HunterAttackTooltip : SurvivorAttackTooltip;
        }

        private static readonly Dictionary<string, Controls> ById = new()
        {
            // 감시자
            ["Hunter_Antonio"] = new("가리킨 땅에 음표 놓기", "바로 앞에 음표 · 두 음표가 전류로 이어지며 공격",
                chargeTip: "좌클릭을 누른 채 놓을 자리를 고르고 떼면 음표를 놓는다.\n벽 너머 안 보이는 곳·너무 높은 곳은 안 된다.\n누른 채 E = 취소.",
                skillTip: "E = 바로 앞에 음표를 놓는다.\n첫 음표를 놓고 5초 안에 둘째를 놓아야 한 쌍이 된다.\n두 음표가 전류선으로 이어지는 순간 선에 닿아 있던 상대가 맞는다.\n이어진 뒤 7초가 지나야 새 음표를 놓을 수 있다."),
            ["Hunter_Gamekeeper"] = new("1초 모아 내려치기 · 모으는 동안 점점 빨라짐", "그랩 · 맞힌 상대를 끌어옴",
                chargeTip: "좌클릭을 꾹 누르면 1초 모은 뒤 저절로 내려친다.\n모으는 동안 이동이 점점 빨라진다(최대 2배).\n빗맞히면 0.6초 동안 제자리.",
                skillTip: "E를 누른 채 조준하며 걷고, 떼면 그랩을 던진다(최대 12m).\n처음 닿은 상대를 물고 앞까지 끌어온다(끌려오는 동안 기절).\n상대보다 벽에 먼저 닿으면 내가 벽 쪽으로 끌려간다.\n공중에서는 못 쓴다."),
            ["Hunter_Geisha"] = new("가리킨 땅에 나비 놓기", "나비에게 날아가기",
                chargeTip: "좌클릭을 누른 채 놓을 자리를 고르고 떼면 나비를 놓는다(최대 100m, 땅 위만).\n누른 채 E = 취소.",
                skillTip: "겨누고 있는 나비 자리로 곧게 날아간다(위층·아래층도).\n날아가는 동안은 공격에 맞지 않는다(맹인의 밀기만 받음).\n장애물에 부딪히면 그 자리에서 멈춰 떨어진다."),
            ["Hunter_Hastur"] = new("가리킨 땅에 촉수 세우기", "꾹: 촉수 고르기 → 떼면 내려찍기",
                chargeTip: "좌클릭을 누른 채 자리를 고르고 떼면 그 땅에서 촉수가 솟는다.\n촉수 수에는 한도가 있어 넘으면 가장 오래된 것이 들어가고, 시간이 지나도 들어간다.\n누른 채 E = 취소.",
                skillTip: "E를 꾹 누르면 바라보는 쪽 촉수를 고르고, 떼면 그 촉수가 내려찍는다.\n가장 가까운 상대 쪽으로 부채꼴로 내리친다 — 밑동에 바짝 붙은 곳은 안전."),
            ["Hunter_HellEmber"] = new("인형 겨눠 던지기", "짧게: 인형과 자리 바꾸기 · 꾹: 분신 부르기",
                chargeTip: "좌클릭을 누른 채 마우스로 떨어질 자리를 고르고 떼면 인형을 포물선으로 던진다.\n우클릭을 누른 채 마우스 = 높이 조절(위층·지붕에도 올라감).\n인형은 하나 — 평타로 맞혀 회수해야 다시 던진다.",
                skillTip: "E 짧게 = 던진 인형과 자리를 바꾼다.\nE 꾹(0.4초) = 분신을 부른다. 분신은 가까운 상대를 끝까지 쫓아가 공격하고, 공격에 맞지 않는다.\n다시 부르면 지난 분신은 사라진다."),
            ["Hunter_Luchino"] = new(HunterChargeDefault, "높이 뛰어올라 좌클릭으로 내려찍기",
                skillTip: "앞으로 크게 뛰어오른다.\n공중에서 좌클릭 = 착지 지점 둘레를 내려찍는다(맞으면 경직).\n공중에서 E = 공격 없이 바로 떨어진다.\n떠 있는 동안 앞(W)을 누르면 손이 닿는 턱에 매달린다. 높이 떨어지는 중에도 쓸 수 있다."),
            ["Hunter_Ripper"] = new("모아서 강하게 치기 · 안개 2칸이면 안개 칼날", "안개 1칸으로 칼날 날리기",
                attackTip: "무기(손톱)로 공격한다. 달리며 점프하면 내려치기.\n안개가 7초마다 1칸씩 모인다(최대 2칸).\n2칸이 다 찼을 때 공격하면 넓은 손톱 다섯 개 모양 안개 칼날이 날아간다.",
                skillTip: "안개 1칸을 써서 약한 칼날(손톱 세 개)을 날린다.\n안개가 한 칸도 없으면 못 쓴다.\n칼날이 지나간 자리에 7초 동안 안개가 깔리고, 그 위에서는 빨라진다."),
            ["Hunter_SmileyFace"] = new(HunterChargeDefault, "꾹: 로켓 들고 걷기 → 떼면 돌진",
                skillTip: "상대를 정면으로 보고 있으면 시작할 수 없다.\nE를 꾹 누르는 동안 로켓을 들고 걷는다(좌클릭 = 취소).\n떼면 돌진 — 캐릭터에 닿으면 달린 거리만큼 상대 기절, 벽에 닿으면 그냥 멈춘다.\n돌진 중 E = 멈춤."),
            // 생존자(차징은 모두 패링)
            ["Survivor_Doctor"] = new(SurvivorCharge, "1.5초 제자리 · 체력 40% 회복",
                skillTip: "1.5초 동안 제자리에서 주사를 놓으면 최대 체력의 40%를 회복한다.\n도중에 끊기면 회복되지 않는다. 체력이 가득 차 있으면 못 쓴다."),
            ["Survivor_Forward"] = new(SurvivorCharge, "부딪힌 상대를 밀어내고, 벽으로 밀어붙이면 기절",
                skillTip: "바라보는 방향으로 돌진한다(점프·공격 불가, E를 다시 누르면 멈춤).\n부딪힌 상대를 밀어낸다 — 오래 달릴수록 멀리(짧게 약 1m ~ 길게 16m).\n밀려난 상대가 벽에 닿으면(벽에서 0.6m 안) 기절 — 오래 달릴수록 길게(0.7초 ~ 5.5초).\n부딪히는 순간 A/D를 누르고 있으면 그쪽으로 15° 비스듬히 민다.\n내가 벽에 부딪히면 내가 기절한다(0.25초 ~ 1.8초)."),
            ["Survivor_Mercenary"] = new(SurvivorCharge, "켜고 끄기 · 벽에 닿으면 벽 차고 도약",
                skillTip: "E로 켜고 E로 끈다(켜 둔 채 걷기·달리기·공격은 평소대로).\n벽에 옆으로 닿으면 도약 준비 → 다른 방향을 누르면 그쪽으로 초고속 도약.\n0.5초 안에 입력이 없으면 바라보는 쪽으로 저절로 도약한다.\n도약 중 처음 누른 방향키 쪽으로 휜다. 한 번 도약하면 5초 재사용 대기."),
            ["Survivor_Coordinator"] = new(SurvivorCharge, "가까운 상대를 쫓는 신호탄 · 맞으면 기절",
                skillTip: "잠깐 겨눈 뒤 신호탄을 쏜다(쏘는 동안 제자리).\n신호탄은 가장 가까운 상대를 따라가며 휘지만 길을 찾지는 않아 벽·바닥에 닿으면 사라진다.\n맞은 상대는 기절한다."),
            ["Survivor_Cowboy"] = new(SurvivorCharge, "올가미 · 잡은 상대를 뛰어넘기",
                skillTip: "E를 누른 채 조준하며 걷고, 떼면 올가미를 던진다.\n상대를 잡으면 그 상대를 뛰어넘어 반대편에 내려선다.\n벽에 닿으면 끌려가지 않고 그냥 돌아온다."),
            ["Survivor_MindsEye"] = new(SurvivorCharge, "앞으로 상대를 밀어내 벽에 맞추면 각종 피해를 입힘"),
            ["Survivor_Thief"] = new(SurvivorCharge, "손전등 · 비추면 E 봉인, 정면이면 기절",
                skillTip: "손전등을 꺼내 앞을 비춘다(비추는 동안 조금 느려짐, E를 다시 누르면 넣음).\n빛에 든 상대는 게이지가 차서, 일정 이상이면 그 상대의 E가 잠긴다.\n얼굴(정면)로 받으면 정면 게이지도 차서 다 차면 기절 + 피해.\n벽에 가리면 빛이 닿지 않는다."),
        };

        public static Controls For(CharacterRoster.Entry entry)
        {
            if (entry != null && ById.TryGetValue(entry.id, out var c)) return c;
            bool hunter = entry != null && entry.role == CharacterRole.Hunter;
            return new Controls(hunter ? HunterChargeDefault : SurvivorCharge, "");
        }
    }
}

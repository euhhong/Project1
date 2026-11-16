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
            public Controls(string charge, string skill) { Charge = charge; Skill = skill; }
        }

        public const string HunterChargeDefault = "모아서 강하게 치기";
        public const string SurvivorCharge = "패링";
        public const string ParryTooltip = "누르고 있는 동안 정면에서 오는 주먹을 막는다.\n막으면 상대를 0.5초 스턴한다.\n떼면 돌려차기.";
        public const string PalletDrop = "판자 내리기";
        public const string PalletDropDesc = "판자 옆에서 · 깔린 감시자는 경직";
        // 평타 설명(마우스를 올리면). 생존자는 주먹, 감시자는 무기. 맹인은 지팡이.
        public const string SurvivorAttackTooltip = "주먹으로 공격한다.\n달리며 점프하면 발차기.";
        public const string HunterAttackTooltip = "무기로 공격한다.\n달리며 점프하면 내려치기.";
        public const string MindsEyeAttackTooltip = "지팡이로 공격해 밀어낸다.\n달리며 점프하면 발차기.";

        public static string AttackTooltip(CharacterRoster.Entry entry) =>
            entry.id == "Survivor_MindsEye" ? MindsEyeAttackTooltip : entry.role == CharacterRole.Hunter ? HunterAttackTooltip : SurvivorAttackTooltip;

        private static readonly Dictionary<string, Controls> ById = new()
        {
            // 감시자
            ["Hunter_Antonio"] = new("가리킨 땅에 음표 놓기", "바로 앞에 음표 · 두 음표가 전류로 이어지며 공격"),
            ["Hunter_Gamekeeper"] = new("1초 모아 내려치기 · 모으는 동안 점점 빨라짐", "그랩 · 맞힌 상대를 끌어옴"),
            ["Hunter_Geisha"] = new("가리킨 땅에 나비 놓기", "나비에게 날아가기"),
            ["Hunter_Hastur"] = new("가리킨 땅에 촉수 세우기", "꾹: 촉수 고르기 → 떼면 내려찍기"),
            ["Hunter_HellEmber"] = new("인형 겨눠 던지기", "짧게: 인형과 자리 바꾸기 · 꾹: 분신 부르기"),
            ["Hunter_Luchino"] = new(HunterChargeDefault, "높이 뛰어올라 좌클릭으로 내려찍기"),
            ["Hunter_Ripper"] = new("모아서 강하게 치기 · 안개 2칸이면 안개 칼날", "안개 1칸으로 칼날 날리기"),
            ["Hunter_SmileyFace"] = new(HunterChargeDefault, "꾹: 로켓 들고 걷기 → 떼면 돌진"),
            // 생존자(차징은 모두 패링)
            ["Survivor_Doctor"] = new(SurvivorCharge, "1.5초 제자리 · 체력 40% 회복"),
            ["Survivor_Forward"] = new(SurvivorCharge, "앞으로 돌진 · 부딪힌 상대 기절"),
            ["Survivor_Mercenary"] = new(SurvivorCharge, "켜고 끄기 · 벽에 닿으면 벽 차고 도약"),
            ["Survivor_Coordinator"] = new(SurvivorCharge, "가까운 상대를 쫓는 신호탄 · 맞으면 기절"),
            ["Survivor_Cowboy"] = new(SurvivorCharge, "올가미 · 잡은 상대를 뛰어넘기"),
            ["Survivor_MindsEye"] = new(SurvivorCharge, "앞으로 상대를 밀어내 벽에 맞추면 각종 피해를 입힘"),
            ["Survivor_Thief"] = new(SurvivorCharge, "손전등 · 비추면 E 봉인, 정면이면 기절"),
        };

        public static Controls For(CharacterRoster.Entry entry)
        {
            if (entry != null && ById.TryGetValue(entry.id, out var c)) return c;
            bool hunter = entry != null && entry.role == CharacterRole.Hunter;
            return new Controls(hunter ? HunterChargeDefault : SurvivorCharge, "");
        }
    }
}

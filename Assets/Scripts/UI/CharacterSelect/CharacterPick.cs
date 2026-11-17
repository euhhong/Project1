using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 캐릭터 선택 씬(CharacterSelect)에서 고른 캐릭터와 모드를 게임 씬(SampleScene)으로 넘긴다(씬이 바뀌어도 남는 static).
    /// 선택 화면이 골라 두고(Set) 게임 씬이 시작할 때 꺼내 쓴다(CharacterSelectBootstrap). 선택 씬으로 돌아가면 지운다(Clear) — 다음에 처음 고른 상태로 쓰도록 Last는 남긴다.
    /// </summary>
    public static class CharacterPick
    {
        public static bool Has { get; private set; }
        public static string EntryId { get; private set; }
        /// <summary>true = 게임 시작(AI 봇과 전투), false = 연습 모드.</summary>
        public static bool Battle { get; private set; }
        /// <summary>
        /// 마지막으로 고른 캐릭터 ID(연습·대전에서 선택 화면으로 돌아오면 그 캐릭터부터 보여 준다). 그 실행 동안만 기억한다 —
        /// PlayerPrefs에도 적어 두어 실행 중 코드가 다시 불려 와도(static이 비워짐) 남지만, 게임을 새로 켜면(ResetOnPlay) 지워 처음(공군)부터 열린다.
        /// </summary>
        public static string LastId
        {
            get => !string.IsNullOrEmpty(lastId) ? lastId : PlayerPrefs.GetString(LastIdPref, null);
            private set { lastId = value; if (!string.IsNullOrEmpty(value)) { PlayerPrefs.SetString(LastIdPref, value); PlayerPrefs.Save(); } }
        }
        private static string lastId;
        private const string LastIdPref = "Project1.LastCharacter";

        public static void Set(CharacterRoster.Entry entry, bool battle)
        {
            Has = entry != null;
            EntryId = entry != null ? entry.id : null;
            LastId = EntryId ?? LastId;
            Battle = battle;
        }

        /// <summary>선택 화면에서 ESC로 타이틀로 돌아갈 때 true. 미리 불러 둔 게임 씬이 먼저 열리므로(막을 수 없음) 게임 씬이 이 값을 보고 곧장 타이틀로 넘긴다.</summary>
        public static bool BackToTitle { get; set; }

        /// <summary>마지막 캐릭터 기억을 지운다(타이틀로 돌아갈 때 — 타이틀에서 다시 들어오면 처음(공군)부터).</summary>
        public static void ForgetLast()
        {
            lastId = null;
            PlayerPrefs.DeleteKey(LastIdPref);
        }

        public static void Clear()
        {
            Has = false;
            EntryId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Has = false; EntryId = null; lastId = null; Battle = false; BackToTitle = false;
            PlayerPrefs.DeleteKey(LastIdPref); // 게임을 새로 켜면 처음(공군)부터 — 마지막 캐릭터는 그 실행 동안만 기억
        }
    }
}

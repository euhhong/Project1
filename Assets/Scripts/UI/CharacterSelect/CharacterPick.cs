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
        /// <summary>마지막으로 고른 캐릭터 ID(선택 화면을 다시 열 때 그 캐릭터부터 보여 준다).</summary>
        public static string LastId { get; private set; }

        public static void Set(CharacterRoster.Entry entry, bool battle)
        {
            Has = entry != null;
            EntryId = entry != null ? entry.id : null;
            LastId = EntryId ?? LastId;
            Battle = battle;
        }

        public static void Clear()
        {
            Has = false;
            EntryId = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Has = false; EntryId = null; LastId = null; Battle = false;
        }
    }
}

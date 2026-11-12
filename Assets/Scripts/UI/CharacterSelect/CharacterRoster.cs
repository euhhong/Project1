using System.Collections.Generic;
using UnityEngine;

namespace Project1.UI
{
    public enum CharacterRole { Survivor, Hunter }

    /// <summary>
    /// 고를 수 있는 캐릭터 목록. 에디터 메뉴 Tools > FE > 캐릭터 목록 다시 만들기가 모델 폴더를 읽어 채운다.
    /// `Assets/UI/Resources/CharacterRoster.asset`에 두고 실행 중에 Resources.Load로 읽는다(인스펙터 연결 없음).
    /// </summary>
    [CreateAssetMenu(menuName = "Project1/Character Roster")]
    public class CharacterRoster : ScriptableObject
    {
        public const string ResourceName = "CharacterRoster";

        [System.Serializable]
        public class Entry
        {
            [Tooltip("모델 폴더 이름(예: Survivor_Doctor).")]
            public string id;
            public string displayName;
            public CharacterRole role;
            [Tooltip("캐릭터 모델(FBX). 캐릭터 루트의 Model 자식으로 들어간다.")]
            public GameObject model;
            [Tooltip("모델 크기. 생존자는 의사 기준, 감시자는 키 비율 2.3/1.7로 맞춘 값.")]
            public float scale = 4f;
            [Tooltip("게임 중 Animator Controller.")]
            public RuntimeAnimatorController gameController;
            [Tooltip("선택 화면 Animator Controller(대기 모션).")]
            public RuntimeAnimatorController selectController;
        }

        [Tooltip("Play를 누르면 캐릭터 선택 화면부터 띄운다.")]
        public bool showOnStart = true;

        public List<Entry> entries = new();

        public static CharacterRoster Load() => Resources.Load<CharacterRoster>(ResourceName);

        public List<Entry> Of(CharacterRole role) => entries.FindAll(e => e.role == role && e.model != null);
    }
}

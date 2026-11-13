using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 캐릭터 루트에 붙어 "이 캐릭터가 누구이고 어느 편인지"를 알려 준다.
    /// ID는 실행 중에 자동으로 매긴다. 멀티에서는 AssignId로 서버가 정한 값을 넣는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterIdentity : MonoBehaviour
    {
        public const int NoTeam = -1;

        [Tooltip("같은 팀끼리는 맞지 않는다. -1이면 팀 없음(자기 자신만 제외).")]
        [SerializeField] private int teamId = NoTeam;

        private static int nextId = 1;

        public int Id { get; private set; }
        public int TeamId => teamId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCounter()
        {
            nextId = 1;
        }

        private void Awake()
        {
            if (Id == 0)
            {
                Id = nextId++;
            }
        }

        /// <summary>네트워크 등 외부에서 ID를 정할 때 쓴다.</summary>
        public void AssignId(int id)
        {
            Id = id;
        }

        /// <summary>이 캐릭터를 불러낸 캐릭터(예: 공장장 분신 → 공장장). 없으면 null. 불러낸 쪽과 불려 나온 쪽은 서로 맞지 않는다.</summary>
        public CharacterIdentity Summoner { get; private set; }

        public void SetSummoner(CharacterIdentity summoner)
        {
            Summoner = summoner;
            if (summoner != null) teamId = summoner.teamId;
        }

        /// <summary>other를 공격할 수 있는지 (자기 자신·같은 팀·불러낸 쪽/불려 나온 쪽 제외).</summary>
        public bool CanHit(CharacterIdentity other)
        {
            if (other == null || other == this)
            {
                return false;
            }
            CharacterIdentity mine = Summoner != null ? Summoner : this, theirs = other.Summoner != null ? other.Summoner : other;
            if (mine == theirs) return false; // 나와 내 분신, 같은 주인의 분신끼리
            return teamId == NoTeam || teamId != other.teamId;
        }
    }
}

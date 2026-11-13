namespace Project1.Core
{
    /// <summary>캐릭터가 물건(PushableObject)을 미는 상태(CharacterPush, BE). FE가 미는 모션(Push Start)으로 보여 준다.</summary>
    public interface ICharacterPushState
    {
        bool IsPushing { get; }

        /// <summary>밀기 시작한 뒤 지난 시간(초). 처음 pushLeanTime 동안은 몸을 기울여 붙기만 한다.</summary>
        float PushTime { get; }

        /// <summary>밀고 있지만 물건이 막혀 움직이지 않는지(벽·다른 물건·턱 끝).</summary>
        bool PushBlocked { get; }
    }
}

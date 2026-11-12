namespace Project1.Core
{
    /// <summary>
    /// 캐릭터에게 입력을 공급하는 쪽(로컬 키보드·마우스, 네트워크, AI).
    /// 캐릭터는 매 틱 이것만 읽고, 키보드나 카메라를 직접 읽지 않는다.
    /// </summary>
    public interface ICharacterInputSource
    {
        /// <summary>
        /// 이번 틱의 입력을 꺼낸다. 같은 틱에 여러 컴포넌트(이동, 공격)가 불러도 같은 값을 돌려주고,
        /// Jump·LightAttack·HeavyRelease 같은 일회성 값은 다음 틱에 비워진다.
        /// </summary>
        CharacterInputFrame ReadInput();
    }
}

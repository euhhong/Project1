namespace Project1.Core
{
    /// <summary>
    /// 캐릭터에게 입력을 공급하는 쪽(로컬 키보드·마우스, 네트워크, AI).
    /// 캐릭터는 매 틱 이것만 읽고, 키보드나 카메라를 직접 읽지 않는다.
    /// </summary>
    public interface ICharacterInputSource
    {
        /// <summary>이번 틱의 입력을 꺼낸다. 한 번 꺼낸 Jump 같은 일회성 값은 비워진다.</summary>
        CharacterInputFrame ReadInput();
    }
}

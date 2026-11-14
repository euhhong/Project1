namespace Project1.Core
{
    /// <summary>
    /// 지금 공중에 있으면 "그냥 놓듯이 툭" 떨어지는지 알려 준다(캐릭터 루트의 컴포넌트가 구현 — 예: 공장장 DollThrow, 인형과 자리를 바꿔 공중에 나타난 뒤 다음 착지까지).
    /// 참이면 CharacterTraversal이 높은 낙하 자세(Fall) 대신 서 있는 자세로 떨어뜨리고(TraversalAction.DollDrop), 착지할 때 착지 동작·경직을 걸지 않는다.
    /// </summary>
    public interface IGentleDropSource
    {
        bool GentleDrop { get; }
    }
}

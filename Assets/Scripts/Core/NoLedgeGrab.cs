using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 표시만 하는 컴포넌트: 이것이 붙은 오브젝트와 그 자식의 Collider는 점프해서 매달리기·뛰어 올라서기·창틀 넘기의 턱이 되지 않는다(CharacterTraversal).
    /// 예: 계단 그룹 루트. 맵 생성기가 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoLedgeGrab : MonoBehaviour { }
}

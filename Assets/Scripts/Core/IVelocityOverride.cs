using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 이동 컴포넌트(CharacterMotor)에게 이번 틱 속도를 통째로 정해 준다(예: 턱 오르기·매달리기). 입력·중력·점프는 쓰지 않는다.
    /// 캐릭터 루트에 붙인다.
    /// </summary>
    public interface IVelocityOverride
    {
        /// <param name="worldVelocity">월드 속도(m/s).</param>
        /// <param name="passThrough">true면 충돌 없이 그대로 옮긴다(턱·난간을 넘어갈 때 — 갈 자리가 비었는지는 부르는 쪽이 미리 확인한다).</param>
        bool TryGetVelocity(out Vector3 worldVelocity, out bool passThrough);
    }
}

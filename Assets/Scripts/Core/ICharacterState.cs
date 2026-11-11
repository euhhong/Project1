using System;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 캐릭터의 읽기 전용 상태. 카메라·모션(FE)은 이것만 읽는다.
    /// 캐릭터 루트 오브젝트에서 GetComponent&lt;ICharacterState&gt;()로 얻는다.
    /// </summary>
    public interface ICharacterState
    {
        CharacterMoveState MoveState { get; }

        bool IsGrounded { get; }

        /// <summary>수직 속도(m/s). + = 위로.</summary>
        float VerticalSpeed { get; }

        /// <summary>캐릭터 로컬 기준 수평 속도(m/s). x = 우(+), y = 앞(+).</summary>
        Vector2 LocalPlanarVelocity { get; }

        /// <summary>시야 좌우 각도(도). 캐릭터 몸도 이 방향을 본다.</summary>
        float LookYaw { get; }

        /// <summary>시야 상하 각도(도). + = 아래를 봄. Quaternion.Euler(LookPitch, LookYaw, 0)으로 바로 쓸 수 있다.</summary>
        float LookPitch { get; }

        /// <summary>걷기 속도(m/s). 모션용 속도 정규화 기준.</summary>
        float WalkSpeed { get; }

        /// <summary>달리기 속도(m/s).</summary>
        float RunSpeed { get; }

        event Action Jumped;

        event Action Landed;
    }
}

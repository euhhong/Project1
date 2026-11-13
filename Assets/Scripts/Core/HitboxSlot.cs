using System;

namespace Project1.Core
{
    /// <summary>
    /// 때리는 판정(Hitbox)이 붙는 자리. 공격 하나가 여러 자리를 동시에 켤 수 있게 Flags로 둔다.
    /// 예: 양손 공격 = LeftHand | RightHand.
    /// </summary>
    [Flags]
    public enum HitboxSlot : byte
    {
        None = 0,
        LeftHand = 1 << 0,
        RightHand = 1 << 1,
        LeftFoot = 1 << 2,
        RightFoot = 1 << 3,
        Weapon = 1 << 4, // 손에 든 소품(주사기 등)
        RunJumpReach = 1 << 5, // 달리기 점프 공격에만 켜는 몸 앞 긴 판정 상자(감시자 RunJumpReach). All에는 넣지 않는다
        All = LeftHand | RightHand | LeftFoot | RightFoot | Weapon,
    }
}

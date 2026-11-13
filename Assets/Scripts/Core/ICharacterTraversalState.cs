namespace Project1.Core
{
    /// <summary>오르기·매달리기·낙하 동작(CharacterTraversal, BE). FE(CharacterTraversalAnimator)가 모션으로 보여 준다.</summary>
    public enum TraversalAction
    {
        None,
        /// <summary>중간 높이 바닥으로 뛰어 올라서기(Running Jump).</summary>
        ClimbUp,
        /// <summary>창틀(낮고 얇은 턱) 넘기 — 반대편 바닥이 같은 높이(생존자: 점프 넘기와 같은 Jumping (1)을 느리게, 감시자: Jump).</summary>
        WindowVault,
        /// <summary>창틀 위로 올라섰다가 반대편 낮은 바닥으로 뛰어내림(Jumping Down (2)). 떨어지는 동안·착지는 낙하 동작이 이어 받는다.</summary>
        WindowDrop,
        /// <summary>점프 중 창틀을 넘어가려 하면 반드시 창틀을 한 손으로 짚고 넘는다(Jumping (1)).</summary>
        WindowJump,
        /// <summary>점프 중 손이 닿은 턱을 잡음(잡는 동작).</summary>
        HangGrab,
        /// <summary>턱에 매달려 있음.</summary>
        Hang,
        /// <summary>매달려 있다가 턱 위로 올라감(난간 없음, Freehang Climb).</summary>
        HangClimb,
        /// <summary>매달려 있다가 난간을 넘어 그 뒤 바닥으로 올라감(Climbing).</summary>
        HangClimbRail,
        /// <summary>매달려 있다가 벽 한 장(얇은 꼭대기)을 넘어가 바로 반대편으로 떨어짐(Climbing 앞부분).</summary>
        HangClimbOver,
        /// <summary>점프하지 않고 걸어서 높은 곳에서 떨어지는 중(Jumping).</summary>
        Fall,
        /// <summary>낮은 곳 착지(Jumping Down).</summary>
        Land,
        /// <summary>걸어서 높은 곳에서 떨어진 착지(Jumping — 깊이 웅크림).</summary>
        LandHigh,
        /// <summary>점프해서 높은 곳에서 떨어진 착지 — 앞구르기(Falling To Roll).</summary>
        LandRoll,
        /// <summary>고공 낙하 착지(Hard Landing).</summary>
        LandHard,
    }

    /// <summary>캐릭터의 오르기·매달리기·낙하 상태. 캐릭터 루트에서 GetComponent로 얻는다.</summary>
    public interface ICharacterTraversalState
    {
        TraversalAction Action { get; }

        /// <summary>시간이 정해진 동작의 진행도(0~1). 매달림·낙하처럼 끝이 정해지지 않은 동작은 0.</summary>
        float ActionProgress { get; }

        /// <summary>매달린 턱 아래가 벽이라 발로 벽을 짚고 매달렸는지(Braced Hang). 아니면 몸이 늘어진 매달림(Freehang).</summary>
        bool BracedHang { get; }

        /// <summary>낙하(Fall)가 넉백·뒤로 걷다가 떨어진 것인지(하늘을 보며 등으로 떨어지는 모션 — Falling을 뒤집음).</summary>
        bool FallBackward { get; }

        /// <summary>감시자인지(창틀을 감시자 모션 Jump로 뛰어넘는다).</summary>
        bool HunterStyle { get; }

        /// <summary>공격·스킬을 막는지(오르는 중·매달림·크게 착지하는 중).</summary>
        bool LocksActions { get; }
    }
}

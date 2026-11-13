using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 날아가는 투사체(읽기 전용, 예: 공군 신호탄). FE가 머리·연기 궤적·맞은 자리 효과를 그린다.
    /// BE(Systems)의 ProjectileEvents.Launched / Ended로 생기고 끝나는 것을 알린다.
    /// </summary>
    public interface IProjectile
    {
        /// <summary>종류(예: Flare). FE가 어떤 모양으로 그릴지 고른다.</summary>
        string Kind { get; }

        /// <summary>쏜 캐릭터 ID(CharacterIdentity.Id).</summary>
        int OwnerId { get; }

        /// <summary>지금 자리(월드). FixedUpdate마다 바뀐다.</summary>
        Vector3 Position { get; }

        /// <summary>날아가는 방향(월드, 크기 1).</summary>
        Vector3 Direction { get; }

        /// <summary>속도(m/s).</summary>
        float Speed { get; }

        /// <summary>아직 날아가는 중인지. 끝나면 false.</summary>
        bool Alive { get; }

        /// <summary>끝난 자리(Alive가 false가 된 뒤).</summary>
        Vector3 EndPoint { get; }

        /// <summary>캐릭터를 맞혀서 끝났는지(false = 벽·바닥·천장에 닿았거나 시간이 다 됨).</summary>
        bool EndedOnCharacter { get; }

        /// <summary>공격 세기 배율(HitResult.Power와 같음, 0 = 보통). FE가 크기·진하기를 고를 수 있다.</summary>
        float Power { get; }
    }
}

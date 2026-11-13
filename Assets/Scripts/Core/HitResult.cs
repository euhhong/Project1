using System;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 히트 판정 결과. "누가, 누구의, 어느 부위를, 언제, 어디서" 때렸는지만 담는다.
    /// 데미지·체력 계산은 이 데이터를 받는 쪽이 한다. 네트워크로 보낼 수 있게 단순한 값만 쓴다.
    /// </summary>
    [Serializable]
    public struct HitResult
    {
        /// <summary>때린 캐릭터 ID (CharacterIdentity.Id).</summary>
        public int AttackerId;

        /// <summary>맞은 캐릭터 ID.</summary>
        public int VictimId;

        /// <summary>맞은 부위.</summary>
        public BodyPart Part;

        /// <summary>맞힌 Hitbox 자리.</summary>
        public HitboxSlot Slot;

        /// <summary>공격 번호. 때린 캐릭터 안에서 공격을 시작할 때마다 1씩 늘어난다.</summary>
        public int AttackId;

        /// <summary>판정이 난 고정 틱 (SimulationTick.Current).</summary>
        public int Tick;

        /// <summary>맞은 위치(월드).</summary>
        public Vector3 Point;

        /// <summary>
        /// 공격 세기 배율(데미지를 정할 때 곱한다). 0 = 보통(1배)으로 본다 — 따로 정하지 않은 공격은 모두 보통.
        /// 예: 리퍼 안개 칼날 = 2칸 공격 1, E(1칸) 0.5.
        /// </summary>
        public float Power;

        /// <summary>세기 배율(0이면 1).</summary>
        public readonly float PowerScale => Power > 0f ? Power : 1f;

        public override string ToString()
        {
            return $"Hit attacker={AttackerId} victim={VictimId} part={Part} slot={Slot} attack={AttackId} tick={Tick} point={Point} power={PowerScale}";
        }
    }
}

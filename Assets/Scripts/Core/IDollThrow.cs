using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 공장장 인형 던지기(읽기 전용 — BE DollThrow). FE가 포물선 보조선·떨어질 자리 원·날아가는 인형(작은 검은 구체)을 그린다.
    /// 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface IDollThrow
    {
        /// <summary>좌클릭을 꾹 눌러 던질 곳을 겨누는 중.</summary>
        bool Aiming { get; }
        /// <summary>겨누는 동안: 던지면 인형이 날아갈 길(손 → 멈추는 곳, 월드 점들).</summary>
        IReadOnlyList<Vector3> AimArc { get; }
        /// <summary>겨누는 동안: 인형이 멈출(떨어질) 자리.</summary>
        Vector3 AimLanding { get; }
        /// <summary>겨누는 곳에 던질 수 있는지(못 던지거나 몸 근처 = 떼면 취소).</summary>
        bool AimValid { get; }

        /// <summary>인형이 있는지(날아가는 중이거나 떨어져 있음).</summary>
        bool DollActive { get; }
        /// <summary>인형 자리(구체 가운데, 월드).</summary>
        Vector3 DollPosition { get; }
        /// <summary>인형이 날아가는 중인지(아니면 멈춰 있음).</summary>
        bool DollFlying { get; }
        /// <summary>던진 횟수 / 자리를 바꾼 횟수(바뀔 때마다 1씩 — FE가 던지는 순간·바꾸는 순간을 알아챈다).</summary>
        int ThrowCount { get; }
        int SwapCount { get; }
        /// <summary>E를 눌러 자리를 바꾸려고 기다리는 중(잠깐 뒤 누른 순간의 인형 자리로 옮긴다 — 그동안 몸을 떤다) / 그 진행도(0~1).</summary>
        bool SwapPending { get; }
        /// <summary>공장장이 자기 평타로 인형을 맞혀 회수한 횟수(늘어날 때 FE가 집어 드는 모션을 낸다).</summary>
        int RetrieveCount { get; }
        float SwapProgress { get; }
    }

    /// <summary>이번 낙하는 다치지 않는 낙하인지(예: 공장장 인형과 자리 바꿈 — 다음 착지까지). FallDamage가 착지할 때 캐릭터에 붙은 것을 모두 본다.</summary>
    public interface ISafeFallSource
    {
        bool SafeFall { get; }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>하스터 촉수 하나의 단계.</summary>
    public enum TentacleState { Rising, Idle, SlamWindup, SlamActive, SlamRecovery, Sinking }

    /// <summary>하스터 촉수 하나(읽기 전용 값).</summary>
    public struct TentacleInfo
    {
        /// <summary>촉수 번호(생길 때마다 1씩 늘어난다). FE가 모델을 짝지을 때 쓴다.</summary>
        public int Id;
        /// <summary>밑동 자리(땅 위 점, 월드).</summary>
        public Vector3 Position;
        public TentacleState State;
        /// <summary>지금 단계에 들어온 뒤 지난 시간(초).</summary>
        public float StateTime;
        /// <summary>지금 단계의 길이(초). Idle은 남은 수명.</summary>
        public float StateDuration;
        /// <summary>내려찍는 방향(수평, 월드). 내려찍기 전에는 하스터 쪽을 등진 방향.</summary>
        public Vector3 Direction;
    }

    /// <summary>
    /// 하스터 촉수(읽기 전용). FE가 놓을 자리 원·촉수 모델·고른 촉수 표시를 그린다. 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface ITentacleField
    {
        /// <summary>좌클릭을 꾹 눌러 촉수를 세울 자리를 고르는 중.</summary>
        bool Placing { get; }
        Vector3 PreviewPoint { get; }
        bool PreviewValid { get; }
        bool PreviewCancels { get; }

        /// <summary>E를 꾹 눌러 내려찍을 촉수를 고르는 중.</summary>
        bool Selecting { get; }

        /// <summary>고른(겨눈) 촉수 번호(Tentacles 안의 순서). 없으면 -1.</summary>
        int TargetIndex { get; }

        IReadOnlyList<TentacleInfo> Tentacles { get; }

        /// <summary>촉수 키(m) = 내려찍으면 닿는 거리.</summary>
        float TentacleHeight { get; }

        /// <summary>(예전 일자 판정 굵기 — 내려찍는 방향을 고를 때 여유로만 쓴다.)</summary>
        float SlamRadius { get; }

        /// <summary>내려찍기 판정 부채꼴의 반각(도) — 내려찍는 방향에서 양옆으로.</summary>
        float SlamHalfAngle { get; }

        /// <summary>촉수 밑동에서 이 거리(m) 안은 맞지 않는다(바짝 붙어 돌면 피함).</summary>
        float SlamInnerRadius { get; }

        /// <summary>부채꼴이 닿는 거리(m, 밑동에서 수평).</summary>
        float SlamReach { get; }
    }
}

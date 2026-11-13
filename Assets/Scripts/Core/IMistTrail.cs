using System.Collections.Generic;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>안개 칼날이 지나간 자리에 남긴 안개 한 덩이(BE MistTrailField). FE가 손톱 모양 안개로 그린다.</summary>
    public struct MistTrailPoint
    {
        /// <summary>자리(월드, 칼날이 날아간 높이).</summary>
        public Vector3 Position;
        /// <summary>칼날이 날아가던 방향(수평, 크기 1).</summary>
        public Vector3 Direction;
        /// <summary>칼날을 휘두른 방향(손톱 모양을 세우는 방향).</summary>
        public Vector3 SwingAxis;
        /// <summary>생긴 시각(Time.time).</summary>
        public float Born;
        /// <summary>칼날 세기(약한 E 칼날 = 0.5).</summary>
        public float Power;
    }

    /// <summary>리퍼 안개 칼날이 남긴 안개 구역(읽기 전용). 캐릭터 루트에서 GetComponent로 얻는다.</summary>
    public interface IMistTrail
    {
        /// <summary>지금 남아 있는 안개(오래된 것부터).</summary>
        IReadOnlyList<MistTrailPoint> Points { get; }

        /// <summary>안개 한 덩이가 남아 있는 시간(초).</summary>
        float Lifetime { get; }

        /// <summary>안개 한 덩이의 반지름(m, 수평). 이 안에 있으면 안개 위에 있는 것.</summary>
        float Radius { get; }

        /// <summary>지금까지 놓은 덩이 수(줄지 않음). 새로 생긴 덩이를 알아볼 때 쓴다 — 새 덩이는 Points의 맨 뒤에 붙는다.</summary>
        int TotalAdded { get; }
    }
}

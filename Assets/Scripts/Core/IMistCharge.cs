using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 리퍼 손톱에 감기는 안개(읽기 전용). FE가 손톱에 감긴 안개 양과 날아가는 안개 칼날 모양을 그린다.
    /// 캐릭터 루트에서 GetComponent로 얻는다.
    /// </summary>
    public interface IMistCharge
    {
        /// <summary>모인 안개 칸 수(0 ~ MaxCharges).</summary>
        int Charges { get; }

        int MaxCharges { get; }

        /// <summary>다음 칸이 차기까지 진행도 0~1(다 찼으면 0).</summary>
        float NextProgress { get; }

        /// <summary>한 칸이 차는 시간(초).</summary>
        float ChargeInterval { get; }

        /// <summary>마지막으로 날린 안개 칼날의 휘두른 방향(월드, 크기 1 — 날아가는 방향에 수직, 손톱이 지나간 방향). 칼날 모양을 이 방향으로 세운다.</summary>
        Vector3 LastSwingAxis { get; }
    }
}

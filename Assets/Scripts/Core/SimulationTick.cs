using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 고정 업데이트(FixedUpdate) 기준 틱 번호. 판정 시점을 프레임과 무관하게 기록하는 데 쓴다.
    /// 멀티로 가면 서버 틱으로 바꿔 끼운다.
    /// </summary>
    public static class SimulationTick
    {
        public static int Current => Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
    }
}

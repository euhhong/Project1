using System;
using UnityEngine;

namespace Project1.Core
{
    /// <summary>
    /// 데미지 없는 충격파가 퍼졌음을 알린다(예: 사냥터지기 그랩이 벽에 도착). 연출(고리)이 듣는다.
    /// 맞은 캐릭터 쪽 반응(감속·모션)은 그 캐릭터의 ShockSlow가 알린다.
    /// </summary>
    public static class ShockwaveEvents
    {
        /// <summary>source = 충격파를 낸 캐릭터, center = 가운데(월드, 발 높이), radius = 반지름(m).</summary>
        public static event Action<GameObject, Vector3, float> Emitted;

        public static void Raise(GameObject source, Vector3 center, float radius) => Emitted?.Invoke(source, center, radius);
    }
}

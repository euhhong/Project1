using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 같은 뼈를 여러 연출 스크립트가 덧씌울 때(노란옷의 왕 걷기 팔·인사·나가떨어짐) 쓰는 공용 바탕 자세(연출 전용).
    /// - 프레임마다 그 뼈를 처음 만지는 스크립트만 바탕을 정한다: 지난 프레임 마지막에 쓴 그대로면 모션이 안 쓰는 뼈 → 바탕으로 되돌림, 바뀌었으면 모션이 새로 쓴 자세가 바탕.
    /// - 같은 프레임의 다음 스크립트는 앞 스크립트가 더한 자세 위에 그대로 더한다.
    /// - 마지막으로 쓴 값은 누가 썼든 End가 적는다 — 스크립트마다 따로 기억하면 남이 더한 것을 모션으로 착각해 자세가 쌓이고 굳는다.
    /// </summary>
    public static class SharedBonePose
    {
        private class Entry { public Quaternion Base, Written; public bool Has; public int Frame = -1; }
        private static readonly Dictionary<Transform, Entry> entries = new();

        private static Entry Get(Transform t)
        {
            if (!entries.TryGetValue(t, out var e)) { e = new Entry(); entries[t] = e; }
            return e;
        }

        /// <summary>이번 프레임 이 뼈를 만지기 전에 부른다(그 프레임 처음 부를 때만 바탕을 정한다).</summary>
        public static void Begin(Transform t)
        {
            if (t == null) return;
            var e = Get(t);
            if (e.Frame == Time.frameCount) return;
            e.Frame = Time.frameCount;
            if (e.Has && Quaternion.Dot(t.localRotation, e.Written) > 0.999999f) t.localRotation = e.Base;
            else e.Base = t.localRotation;
        }

        /// <summary>이 뼈를 다 만진 뒤 부른다(지금 값을 마지막으로 쓴 값으로 적는다).</summary>
        public static void End(Transform t)
        {
            if (t == null) return;
            var e = Get(t);
            e.Written = t.localRotation; e.Has = true;
        }

        /// <summary>바탕 자세로 되돌린다(연출을 끌 때).</summary>
        public static void Restore(Transform t)
        {
            if (t == null || !entries.TryGetValue(t, out var e) || !e.Has) return;
            t.localRotation = e.Base; e.Has = false; e.Frame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear() => entries.Clear();
    }
}

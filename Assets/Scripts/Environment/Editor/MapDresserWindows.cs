using UnityEngine;

namespace Project1.Map.EditorTools
{
    /// <summary>
    /// 창문 장식(BG): 벽의 네모난 창문 구멍(MapBlockoutBuilder가 남긴 WinOpening_ 자리)마다 고딕 돌 창틀 모델을 벽 양쪽 면에 붙인다.
    /// 창틀은 바깥이 네모난 돌판에 뾰족한 아치 구멍이 뚫린 모양 — 아치 구멍이 창문 구멍과 맞도록 키우므로, 구멍 위 네모 모서리는 돌판이 가려
    /// 창문이 아치 모양으로 보인다. 좁고 긴 창 = WindowArch, 넓은 창(폭 ÷ 높이 0.8 넘음) = WindowArchWide. 충돌 없음(창문 넘기 그대로).
    /// </summary>
    public static partial class MapDresser
    {
        // 모델 안의 아치 구멍 자리(모델 크기 대비 비율: 폭, 높이, 구멍 아래 높이) — 모델을 보고 맞춘 값
        private static readonly System.Collections.Generic.Dictionary<string, (float w, float h, float bottom)> WindowInner = new()
        {
            ["WindowArch"] = (0.5f, 0.72f, 0.12f),
            ["WindowArchWide"] = (0.567f, 0.766f, 0.117f), // 구멍: 폭 56.7%, 아래 11.7% ~ 아치 꼭대기 88.3%(모델을 앞에서 광선으로 재 봄)
        };
        private const float WindowFrameDepth = 0.12f; // 벽 면에서 튀어나오는 두께(m)

        private static void DressWindows(Transform root)
        {
            int n = 0;
            foreach (var mk in root.GetComponentsInChildren<Transform>(true))
            {
                if (!mk.name.StartsWith("WinOpening_")) continue;
                Vector3 s = mk.localScale; // 구멍 폭, 높이, 벽 두께
                string key = s.x / Mathf.Max(0.01f, s.y) > 0.8f ? "WindowArchWide" : "WindowArch";
                if (LoadModel(key) == null) key = "WindowArchWide"; // 좁은 창 모델이 없으면 넓은 창틀을 늘려 씀
                if (LoadModel(key) == null) { missing++; continue; }
                var inner = WindowInner[key];
                var size = new Vector3(s.x / inner.w, s.y / inner.h, WindowFrameDepth);
                for (int side = 0; side < 2; side++)
                {
                    // 앞(+Z)이 벽 바깥을 보게 — 반대쪽 면은 180° 돌림
                    var rot = mk.localRotation * Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
                    Vector3 normal = rot * Vector3.forward;
                    Vector3 bottom = mk.localPosition + normal * (s.z / 2f) - Vector3.up * (inner.bottom * size.y); // 구멍 아래 = 모델 구멍 아래
                    if (Place(key, mk.parent, rot, bottom, size, false, false, $"{ModelPrefix}{key}_{mk.name}_{side}") != null) n++;
                }
            }
            Debug.Log($"[BG] 창문 고딕 창틀 {n}개");
        }
    }
}

using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 붉은나비 나비 연출(IButterflyField, BE를 읽어 그린다).
    /// - 놓은 나비: 땅 위에 떠서 파닥거린다(ButterflyModel).
    /// - 좌클릭을 꾹 누르는 동안: 마우스가 가리키는 자리에 반투명 미리보기 나비(놓을 수 없는 자리면 붉게) + 바닥 고리.
    /// - 지금 겨누는 나비: 테두리가 밝게 빛나고 바닥에 붉은 표적 고리가 돈다(E를 누르면 그 나비로 간다).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 붉은나비만).
    /// </summary>
    [DisallowMultipleComponent]
    public class ButterflyVisual : MonoBehaviour
    {
        [Tooltip("나비 크기(날개 끝~끝, m).")]
        [SerializeField, Min(0.05f)] private float size = 0.6f;
        [Tooltip("땅 위로 떠 있는 높이(m) / 오르내림(m).")]
        [SerializeField] private float hoverHeight = 0.55f, bob = 0.06f;
        [Tooltip("표적 고리 반지름(m) / 색.")]
        [SerializeField, Min(0.05f)] private float markerRadius = 0.6f;
        [SerializeField] private Color markerColor = new(1f, 0.2f, 0.15f, 0.9f);
        [SerializeField] private Color previewRingColor = new(1f, 1f, 1f, 0.6f);

        private IButterflyField field;
        private readonly List<ButterflyModel> placed = new();
        private ButterflyModel preview;
        private LineRenderer marker, previewRing;
        private Transform root;

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        private void LateUpdate()
        {
            if (field == null) field = GetComponent<IButterflyField>();
            if (field == null) return;
            if (root == null) root = new GameObject("Butterflies").transform; // 캐릭터를 따라 움직이지 않게 따로 둔다

            // 놓은 나비
            var list = field.Butterflies;
            while (placed.Count < list.Count) placed.Add(Make($"Butterfly_{placed.Count}", false));
            while (placed.Count > list.Count) { Destroy(placed[^1].gameObject); placed.RemoveAt(placed.Count - 1); }
            float t = Time.time;
            for (int i = 0; i < placed.Count; i++)
            {
                var b = placed[i];
                b.transform.position = list[i] + Vector3.up * (hoverHeight + Mathf.Sin(t * 2.2f + i * 1.7f) * bob);
                b.transform.rotation = Quaternion.Euler(-12f, i * 77f + Mathf.Sin(t * 0.7f + i) * 20f, 0f);
                b.Highlight = i == field.TargetIndex;
            }

            // 미리보기
            if (preview == null) { preview = Make("Butterfly_Preview", true); previewRing = Ring("PreviewRing", previewRingColor); }
            bool showPreview = field.Placing;
            preview.gameObject.SetActive(showPreview);
            previewRing.gameObject.SetActive(showPreview);
            if (showPreview)
            {
                preview.GhostValid = field.PreviewValid;
                preview.transform.position = field.PreviewPoint + Vector3.up * hoverHeight;
                preview.transform.rotation = Quaternion.Euler(-12f, transform.eulerAngles.y, 0f);
                previewRing.startColor = previewRing.endColor = field.PreviewValid ? previewRingColor : new Color(1f, 0.25f, 0.2f, 0.6f);
                SetRing(previewRing, field.PreviewPoint, markerRadius * 0.7f, 0f);
            }

            // 겨누는 나비 표적
            if (marker == null) marker = Ring("TargetMarker", markerColor);
            int ti = field.TargetIndex;
            bool showMarker = ti >= 0 && ti < list.Count && !field.Placing;
            marker.gameObject.SetActive(showMarker);
            if (showMarker)
            {
                float pulse = 1f + Mathf.Sin(t * 6f) * 0.08f;
                SetRing(marker, list[ti], markerRadius * pulse, t * 90f);
            }
        }

        private ButterflyModel Make(string name, bool ghost)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * size;
            var m = go.AddComponent<ButterflyModel>();
            m.Ghost = ghost;
            return m;
        }

        private const int RingSegments = 40;

        private LineRenderer Ring(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = RingSegments;
            lr.widthMultiplier = 0.06f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        // 바닥 고리(원), spin(도)만큼 돌린다.
        private static void SetRing(LineRenderer lr, Vector3 center, float radius, float spin)
        {
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments * 360f + spin) * Mathf.Deg2Rad;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.04f);
            }
        }
    }
}

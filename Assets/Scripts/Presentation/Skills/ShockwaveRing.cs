using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 이 캐릭터가 낸 데미지 없는 충격파(ShockwaveEvents — 예: 사냥터지기 그랩이 벽에 도착)를 발밑에서 빠르게 퍼지며 옅어지는 고리로 보여 준다.
    /// 연출 전용. GrabShotVisual이 붙을 때 같이 붙는다(RequireComponent).
    /// </summary>
    [DisallowMultipleComponent]
    public class ShockwaveRing : MonoBehaviour
    {
        [Tooltip("고리가 반지름까지 퍼지는 시간(초).")]
        [SerializeField, Min(0.05f)] private float duration = 0.35f;
        [SerializeField, Min(0.005f)] private float width = 0.12f;
        [SerializeField] private Color color = new(1f, 1f, 1f, 0.85f);
        private const int Segments = 40;

        private LineRenderer ring;
        private Vector3 center;
        private float radius, time = -1f;

        private void OnEnable() => ShockwaveEvents.Emitted += OnEmitted;

        private void OnDisable()
        {
            ShockwaveEvents.Emitted -= OnEmitted;
            if (ring != null) ring.enabled = false;
            time = -1f;
        }

        private void OnDestroy()
        {
            if (ring == null) return;
            Destroy(ring.material);
            Destroy(ring.gameObject);
        }

        private void OnEmitted(GameObject source, Vector3 at, float r)
        {
            if (source != gameObject) return;
            center = at + Vector3.up * 0.05f;
            radius = r;
            time = 0f;
            if (ring == null)
            {
                var go = new GameObject("ShockwaveRing");
                ring = go.AddComponent<LineRenderer>();
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = Segments;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.receiveShadows = false;
                ring.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private void LateUpdate()
        {
            if (ring == null || time < 0f) return;
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            if (t >= 1f) { ring.enabled = false; time = -1f; return; }
            float r = radius * (1f - (1f - t) * (1f - t)); // 빠르게 퍼졌다 느려짐
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            Color c = color; c.a *= 1f - t;
            ring.startColor = ring.endColor = c;
            ring.widthMultiplier = width * (1f - 0.5f * t);
            ring.enabled = true;
        }
    }
}

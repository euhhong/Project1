using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 벽 치기 이펙트(FE): 평타가 벽을 정면으로 쳐서 튕겨 나오면(BE WallBounceEvents) 벽에 닿은 자리에서 파란 리본 몇 가닥이 튀어 나간다.
    /// 리본은 벽 바깥쪽으로 퍼지며 휘어 떨어지고, 가늘어지며 사라진다. 플레이를 시작하면 저절로 켜진다(붙일 것 없음).
    /// </summary>
    public class WallBounceSpark : MonoBehaviour
    {
        public static int Count = 7;
        public static float Life = 0.4f;
        public static float Speed = 4.5f;
        public static float Width = 0.07f;
        public static Color Color = new(0.3f, 0.65f, 1f, 1f);

        private const int Points = 8;
        private static Material material;

        private LineRenderer line;
        private Vector3 pos, vel;
        private readonly Vector3[] trail = new Vector3[Points];
        private float age, life;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => WallBounceEvents.Struck -= OnStruck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Subscribe()
        {
            WallBounceEvents.Struck -= OnStruck;
            WallBounceEvents.Struck += OnStruck;
        }

        private static void OnStruck(GameObject attacker, Vector3 point, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up;
            Vector3 side = Vector3.Cross(n, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(side, n);
            for (int i = 0; i < Count; i++)
            {
                float a = (i / (float)Count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Vector3 dir = (n * 1.1f + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * Random.Range(0.6f, 1f)).normalized;
                var go = new GameObject("WallBounceRibbon");
                var s = go.AddComponent<WallBounceSpark>();
                s.Init(point + n * 0.03f, dir * Speed * Random.Range(0.7f, 1.2f), Life * Random.Range(0.8f, 1.15f));
            }
        }

        private void Init(Vector3 start, Vector3 velocity, float lifeTime)
        {
            pos = start; vel = velocity; life = lifeTime;
            for (int i = 0; i < Points; i++) trail[i] = start;
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = Points;
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GetMaterial();
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)); // 앞(머리)이 굵고 꼬리는 가늘게
            line.SetPositions(trail);
            Apply(0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life) { Destroy(gameObject); return; }
            vel += Vector3.down * 9f * dt;   // 휘어 떨어진다
            vel *= 1f - 3f * dt;              // 공기 저항
            pos += vel * dt;
            for (int i = Points - 1; i > 0; i--) trail[i] = Vector3.Lerp(trail[i], trail[i - 1], 0.55f);
            trail[0] = pos;
            line.SetPositions(trail);
            Apply(age / life);
        }

        private void Apply(float t)
        {
            float k = 1f - t;
            line.widthMultiplier = Width * (0.4f + 0.6f * k);
            Color head = Color; head.a = k;
            Color tail = Color.Lerp(Color, Color.white, 0.4f); tail.a = 0f;
            line.startColor = head; line.endColor = tail;
        }

        private static Material GetMaterial()
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader) { name = "WallBounceSpark", renderQueue = 3100 };
            return material;
        }
    }
}

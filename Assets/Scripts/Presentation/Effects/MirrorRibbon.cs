using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>거울 순간이동 리본 한 가닥(FE, MirrorTravelEffect가 만든다): 짧게 날아가며 가늘어지고 사라진다.</summary>
    public class MirrorRibbon : MonoBehaviour
    {
        public static int Count = 12;
        public static float Life = 0.55f;
        public static float Width = 0.05f;
        public static Color Color = new(0.78f, 0.7f, 1f, 1f);

        private const int Points = 7;
        private static Material material;
        private LineRenderer line;
        private Vector3 pos, vel;
        private readonly Vector3[] trail = new Vector3[Points];
        private float age, life;

        public static void Spawn(Vector3 start, Vector3 velocity)
        {
            var go = new GameObject("MirrorRibbon");
            go.AddComponent<MirrorRibbon>().Init(start, velocity, Life * Random.Range(0.8f, 1.2f));
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
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader) { name = "MirrorRibbon", renderQueue = 3100 };
            }
            line.sharedMaterial = material;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            line.SetPositions(trail);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            vel *= 1f - 2.5f * Time.deltaTime;
            pos += vel * Time.deltaTime;
            for (int i = Points - 1; i > 0; i--) trail[i] = Vector3.Lerp(trail[i], trail[i - 1], 0.5f);
            trail[0] = pos;
            line.SetPositions(trail);
            float k = 1f - age / life;
            line.widthMultiplier = Width * (0.4f + 0.6f * k);
            Color head = Color; head.a = k;
            Color tail = Color.white; tail.a = 0f;
            line.startColor = head; line.endColor = tail;
        }
    }
}

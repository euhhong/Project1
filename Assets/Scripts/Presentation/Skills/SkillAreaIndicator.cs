using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 스킬 공격 범위를 땅에 빨간 원으로 보여 준다(ICharacterSkillState.ShowArea / AreaCenter / AreaRadius).
    /// 예: 루키노 도약 중 공격 키 → 착지 지점에 원이 뜨고, 내려찍어 착지하면 잠깐 밝게 남았다가 사라진다.
    /// 캐릭터 루트에 붙인다. 원은 캐릭터를 따라 움직이지 않는다(월드 위치).
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillAreaIndicator : MonoBehaviour
    {
        private const int Segments = 64;

        [SerializeField] private Color color = new(1f, 0.2f, 0.15f, 0.9f);
        [SerializeField, Min(0.01f)] private float lineWidth = 0.08f;
        [Tooltip("착지 뒤 원이 남아 있는 시간(초).")]
        [SerializeField, Min(0f)] private float linger = 0.35f;

        private ICharacterSkillState skill;
        private LineRenderer ring;
        private float hideAt;
        private Vector3 center;
        private float radius;

        private void Awake() => skill = GetComponent<ICharacterSkillState>();

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<ICharacterSkillState>();
            if (skill == null) return;

            if (skill.ShowArea)
            {
                center = skill.AreaCenter;
                radius = skill.AreaRadius;
                hideAt = Time.time + linger;
            }
            bool visible = Time.time < hideAt && radius > 0f;
            if (!visible) { if (ring != null) ring.enabled = false; return; }

            EnsureRing();
            ring.enabled = true;
            // 범위가 뜬 동안은 살짝 맥박치고, 착지 뒤에는 점점 옅어진다.
            float alpha = skill.ShowArea ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 10f)) : Mathf.Clamp01((hideAt - Time.time) / Mathf.Max(linger, 0.01f));
            var c = color; c.a *= alpha;
            ring.startColor = ring.endColor = c;
            ring.widthMultiplier = lineWidth;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius));
            }
        }

        private void EnsureRing()
        {
            if (ring != null) return;
            var go = new GameObject("SkillAreaRing");
            go.transform.SetParent(null, false); // 월드에 둔다(캐릭터 회전을 따라가지 않게)
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = Segments;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            ring.sharedMaterial = new Material(shader) { name = "SkillAreaRing" };
        }

        private void OnDestroy()
        {
            if (ring != null) Destroy(ring.gameObject);
        }
    }
}

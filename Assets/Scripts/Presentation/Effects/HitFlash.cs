using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 피격 시 몸이 빨갛게 달궈지는 연출: 이 캐릭터가 맞은 순간(HitEvents) 모델 전체(몸·손에 든 소품)의 색이 붉게 달아올랐다가
    /// fadeTime초 동안 원래 색으로 식는다. 머티리얼은 건드리지 않고 MaterialPropertyBlock으로 기본 색(_BaseColor/_Color)만 바꾼다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 모두 / 훈련 상대). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color hotColor = new(1f, 0.16f, 0.08f, 1f);
        [Tooltip("맞은 순간 붉은색 쪽으로 섞는 정도(0~1).")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.8f;
        [Tooltip("원래 색으로 식는 시간(초).")]
        [SerializeField, Min(0.02f)] private float fadeTime = 0.35f;

        private struct Slot { public Renderer r; public int index; public int prop; public Color baseColor; }

        private CharacterIdentity identity;
        private readonly List<Slot> slots = new();
        private Animator boundTo;
        private MaterialPropertyBlock block;
        private float hitTime = -10f;
        private bool applied;

        private void Awake() => identity = GetComponent<CharacterIdentity>();
        private void OnEnable() => HitEvents.HitOccurred += OnHit;
        private void OnDisable() { HitEvents.HitOccurred -= OnHit; Restore(); }

        private void OnHit(HitResult hit)
        {
            if (identity == null) identity = GetComponent<CharacterIdentity>();
            if (identity != null && hit.VictimId == identity.Id) hitTime = Time.time;
        }

        private void LateUpdate()
        {
            float k = 1f - Mathf.Clamp01((Time.time - hitTime) / fadeTime);
            if (k <= 0f) { if (applied) Restore(); return; }
            Collect();
            block ??= new MaterialPropertyBlock();
            float w = strength * k * k * (3f - 2f * k); // 처음 확 달아올랐다 부드럽게 식음
            foreach (var s in slots)
            {
                if (s.r == null) continue;
                s.r.GetPropertyBlock(block, s.index);
                block.SetColor(s.prop, Color.Lerp(s.baseColor, new Color(hotColor.r, hotColor.g, hotColor.b, s.baseColor.a), w));
                s.r.SetPropertyBlock(block, s.index);
            }
            applied = true;
        }

        /// <summary>모델의 렌더러·머티리얼 칸마다 원래 기본 색을 모은다(모델이 바뀌면 다시).</summary>
        private void Collect()
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim == boundTo && slots.Count > 0) return;
            Restore();
            slots.Clear();
            boundTo = anim;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    int prop = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
                    if (prop < 0) continue;
                    slots.Add(new Slot { r = r, index = i, prop = prop, baseColor = m.GetColor(prop) });
                }
            }
        }

        private void Restore()
        {
            if (!applied) return;
            foreach (var s in slots)
            {
                if (s.r == null) continue;
                s.r.SetPropertyBlock(null, s.index);
            }
            applied = false;
        }
    }
}

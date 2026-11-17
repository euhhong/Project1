using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공장장 인형 차징 중 상어 망치(모델의 HellEmber_Weapon — 오른손 아래 무기 메시)를 반투명하게(연출 전용).
    /// - 좌클릭 차징(IDollThrow.Aiming) 동안 + 던진 뒤 팔을 휘두르는 동안(afterThrow초)은 alpha까지 흐리게, 그 뒤 부드럽게 원래대로.
    /// - URP Lit 원본 머티리얼(에셋)은 건드리지 않는다: 흐려지는 동안만 투명 모드로 바꾼 사본을 끼우고, 다 돌아오면 원본으로 되돌린다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 공장장만). 분신(갈고리 낫)은 해당 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public class HellEmberWeaponFade : MonoBehaviour
    {
        private const string WeaponName = "HellEmber_Weapon";

        [Tooltip("흐려졌을 때 불투명도(0 = 안 보임).")]
        [SerializeField, Range(0f, 1f)] private float alpha = 0.25f;
        [Tooltip("던진 뒤에도 흐리게 두는 시간(초) — 팔을 휘두르는 동안.")]
        [SerializeField, Min(0f)] private float afterThrow = 0.6f;
        [Tooltip("흐려지고 돌아오는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;

        private IDollThrow doll;
        private Renderer weapon;
        private Material[] originals, faded;
        private int seenThrows = -1;
        private float throwHold, amount; // amount: 흐린 정도(0 = 원래, 1 = alpha)
        private bool usingFaded;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void OnDisable() => Restore();

        private void OnDestroy()
        {
            Restore();
            if (faded != null) foreach (var m in faded) if (m != null) Destroy(m);
        }

        private void LateUpdate()
        {
            doll ??= GetComponent<IDollThrow>();
            if (doll == null) return;
            if (weapon == null) { Find(); if (weapon == null) return; }

            if (seenThrows < 0) seenThrows = doll.ThrowCount;
            if (doll.ThrowCount != seenThrows) { seenThrows = doll.ThrowCount; throwHold = afterThrow; }
            if (throwHold > 0f) throwHold -= Time.deltaTime;
            bool on = doll.Aiming || throwHold > 0f;
            amount = Mathf.MoveTowards(amount, on ? 1f : 0f, Time.deltaTime / fadeTime);

            if (amount <= 0f) { Restore(); return; }
            if (faded == null) BuildFaded();
            if (!usingFaded) { weapon.sharedMaterials = faded; usingFaded = true; }
            float a = Mathf.Lerp(1f, alpha, amount);
            for (int i = 0; i < faded.Length; i++)
            {
                var m = faded[i];
                if (m == null) continue;
                int id = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
                if (id < 0) continue;
                Color c = originals[i] != null && originals[i].HasProperty(id) ? originals[i].GetColor(id) : m.GetColor(id);
                c.a *= a;
                m.SetColor(id, c);
            }
        }

        /// <summary>모델(손 아래)에서 상어 무기 메시를 찾는다. 모델이 바뀌면(캐릭터 교체) 다시 찾는다.</summary>
        private void Find()
        {
            Restore();
            faded = null;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.name == WeaponName) { weapon = r; break; }
            if (weapon != null) originals = weapon.sharedMaterials;
        }

        /// <summary>원본 머티리얼의 사본을 투명(알파 섞기) 모드로 만든다 — URP Lit 기준.</summary>
        private void BuildFaded()
        {
            faded = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null) continue;
                var m = new Material(originals[i]) { name = originals[i].name + OutlineMeshCopy.FadeSuffix };
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);     // Alpha
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                faded[i] = m;
            }
        }

        private void Restore()
        {
            amount = 0f;
            if (usingFaded && weapon != null && originals != null) weapon.sharedMaterials = originals;
            usingFaded = false;
        }
    }
}

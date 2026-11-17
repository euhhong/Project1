using System.Collections.Generic;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 등 뒤가 벽이라 카메라가 몸 쪽으로 당겨지면(ThirdPersonCamera.Blocked01) 몸 전체를 반투명하게(연출 전용, 공장장 인형 차징 때 망치 흐려지기와 같은 방식).
    /// 앞으로 걸어 나가 카메라가 다시 물러나면 부드럽게 원래대로. 카메라가 따라가는 캐릭터(내 캐릭터)일 때만, 내 화면에서만(상대에게는 그대로 보임) — 모든 캐릭터에 붙인다(CharacterLoadout).
    /// URP Lit 원본 머티리얼(에셋)은 건드리지 않는다: 흐려지는 동안만 투명 모드 사본(이름 끝 OutlineMeshCopy.FadeSuffix)을 끼우고, 다 돌아오면 원본으로. 흐린 동안 흰 윤곽선 등 복사본("~")은 숨긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraBlockedBodyFade : MonoBehaviour
    {
        [Tooltip("흐려졌을 때 불투명도(0 = 안 보임).")]
        [SerializeField, Range(0f, 1f)] private float alpha = 0.25f;
        [Tooltip("카메라가 이만큼(0~1) 당겨지면 흐려지기 시작.")]
        [SerializeField, Range(0f, 1f)] private float startBlocked = 0.3f;
        [Tooltip("카메라가 이만큼(0~1) 당겨지면 다 흐려짐.")]
        [SerializeField, Range(0f, 1f)] private float fullBlocked = 0.5f;
        [Tooltip("흐려지고 돌아오는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private class Part { public Renderer r; public Material[] originals, faded; }
        private readonly List<Part> parts = new();
        private readonly List<Renderer> copies = new();
        // 반투명 사본 → 원본 머티리얼(다른 연출이 흐린 동안 머티리얼을 복사해 가도 원본을 찾을 수 있게 — Unfaded)
        private static readonly Dictionary<Material, Material> FadedToOriginal = new();

        /// <summary>m이 몸 반투명 사본이면 그 원본, 아니면 m 그대로. 모델 머티리얼을 복사해 새 렌더러를 만드는 연출(GrabShotVisual)이 쓴다.</summary>
        public static Material Unfaded(Material m) => m != null && FadedToOriginal.TryGetValue(m, out var o) && o != null ? o : m;

        /// <summary>머티리얼 배열을 Unfaded로 바꾼 새 배열.</summary>
        public static Material[] Unfaded(Material[] mats)
        {
            var r = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++) r[i] = Unfaded(mats[i]);
            return r;
        }
        private ThirdPersonCamera cam;
        private int builtFor;
        private float rebuildAt, amount;
        private bool usingFaded;

        private void OnDisable() => Restore();

        private void OnDestroy()
        {
            Restore();
            foreach (var p in parts) if (p.faded != null) foreach (var m in p.faded) if (m != null) { FadedToOriginal.Remove(m); Destroy(m); }
            parts.Clear();
        }

        private void LateUpdate()
        {
            if (cam == null) cam = FindAnyObjectByType<ThirdPersonCamera>();
            float goal = 0f;
            if (cam != null && cam.Target == transform)
                goal = Mathf.InverseLerp(startBlocked, Mathf.Max(fullBlocked, startBlocked + 0.01f), cam.Blocked01);
            amount = Mathf.MoveTowards(amount, goal, Time.deltaTime / fadeTime);

            if (amount <= 0f) { if (usingFaded) Restore(); return; }
            if (Time.unscaledTime >= rebuildAt || parts.Count == 0)
            {
                rebuildAt = Time.unscaledTime + 0.5f;
                int sig = Signature();
                if (sig != builtFor) Rebuild(sig);
            }
            if (!usingFaded)
            {
                foreach (var p in parts) if (p.r != null) p.r.sharedMaterials = Faded(p);
                copies.Clear();
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name.StartsWith("~") && !r.forceRenderingOff) { r.forceRenderingOff = true; copies.Add(r); } // 흰 윤곽선 등도 같이 숨김
                usingFaded = true;
            }
            float a = Mathf.Lerp(1f, alpha, amount);
            foreach (var p in parts)
            {
                if (p.faded == null) continue;
                for (int i = 0; i < p.faded.Length; i++)
                {
                    var m = p.faded[i];
                    if (m == null) continue;
                    int id = m.HasProperty(BaseColorId) ? BaseColorId : m.HasProperty(ColorId) ? ColorId : -1;
                    if (id < 0) continue;
                    Color c = p.originals[i] != null && p.originals[i].HasProperty(id) ? p.originals[i].GetColor(id) : m.GetColor(id);
                    c.a *= a;
                    m.SetColor(id, c);
                }
            }
        }

        private int Signature()
        {
            int h = 29;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (OutlineMeshCopy.IsModelRenderer(r)) h = h * 31 + r.GetEntityId().GetHashCode();
            return h;
        }

        /// <summary>모델(캐릭터 교체 시 새 모델)의 렌더러와 원본 머티리얼을 다시 모은다.</summary>
        private void Rebuild(int sig)
        {
            Restore();
            // 흐린 동안 다른 연출이 반투명 사본을 복사해 간 렌더러(예: 사냥터지기 그랩을 쪼갠 물고기 조각)도 원본으로 돌려놓고 나서 지운다 — 지운 머티리얼을 끼고 있으면 분홍색이 된다
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && FadedToOriginal.TryGetValue(mats[i], out var orig)) { mats[i] = orig; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
            foreach (var p in parts) if (p.faded != null) foreach (var m in p.faded) if (m != null) { FadedToOriginal.Remove(m); Destroy(m); }
            parts.Clear();
            builtFor = sig;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (OutlineMeshCopy.IsModelRenderer(r) && r.name != HellEmberWeaponFade.WeaponName) // 공장장 망치는 HellEmberWeaponFade가 따로 흐리게 한다
                    parts.Add(new Part { r = r, originals = r.sharedMaterials });
        }

        /// <summary>원본 머티리얼의 사본을 투명(알파 섞기) 모드로 — URP Lit 기준(HellEmberWeaponFade와 같음).</summary>
        private static Material[] Faded(Part p)
        {
            if (p.faded != null) return p.faded;
            p.faded = new Material[p.originals.Length];
            for (int i = 0; i < p.originals.Length; i++)
            {
                var src = p.originals[i];
                if (src == null) continue;
                var m = new Material(src) { name = src.name + OutlineMeshCopy.FadeSuffix };
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                p.faded[i] = m;
                FadedToOriginal[m] = src;
            }
            return p.faded;
        }

        private void Restore()
        {
            amount = 0f;
            if (usingFaded) foreach (var p in parts) if (p.r != null && p.originals != null) p.r.sharedMaterials = p.originals;
            foreach (var r in copies) if (r != null) r.forceRenderingOff = false;
            copies.Clear();
            usingFaded = false;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project1.Presentation
{
    /// <summary>
    /// 캐릭터 윤곽선(FE, 게임 중에만 — CharacterLoadout이 붙인다): 모델의 렌더러마다 같은 메시·뼈대를 쓰는 윤곽선 렌더러(셰이더 Project1/Characters/Outline —
    /// 화면에서 얇은 흰 선)를 옆에 하나씩 붙인다. 모델이 바뀌면(캐릭터 교체) 다시 만든다.
    /// 원래 렌더러가 꺼지면(숨김·투명해지는 무기 등) 윤곽선도 끄고, 표정(블렌드셰이프)도 따라간다. 알파로 오린 부분(머리카락 등)은 같이 오린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterOutline : MonoBehaviour
    {
        public static Color Color = Color.white;
        public static float WidthPixels = 1.1f;
        private const string OutlineName = "~Outline";

        private static Shader shader;
        private static readonly Dictionary<Material, Material> MatCache = new();

        private readonly List<(Renderer src, Renderer outline)> pairs = new();
        private int builtFor;
        private float checkAt;

        private void LateUpdate()
        {
            if (Time.unscaledTime >= checkAt)
            {
                checkAt = Time.unscaledTime + 0.5f;
                int sig = Signature();
                if (sig != builtFor) Rebuild(sig);
            }
            for (int i = pairs.Count - 1; i >= 0; i--)
            {
                var (src, line) = pairs[i];
                if (src == null || line == null) { if (line != null) Destroy(line.gameObject); pairs.RemoveAt(i); continue; }
                bool on = src.enabled && src.gameObject.activeInHierarchy && !src.forceRenderingOff;
                if (line.enabled != on) line.enabled = on;
                if (on && src is SkinnedMeshRenderer s && line is SkinnedMeshRenderer l && s.sharedMesh != null)
                    for (int b = 0; b < s.sharedMesh.blendShapeCount; b++) l.SetBlendShapeWeight(b, s.GetBlendShapeWeight(b));
            }
        }

        /// <summary>윤곽선을 지금 다시 만든다(모델을 바꾼 직후 바로 보이게 하거나 편집 화면에서 확인할 때).</summary>
        public void Refresh() => Rebuild(Signature());

        private void OnDestroy()
        {
            foreach (var (_, line) in pairs) if (line != null) Destroy(line.gameObject);
            pairs.Clear();
        }

        /// <summary>지금 모델 렌더러들을 나타내는 값(바뀌면 다시 만든다).</summary>
        private int Signature()
        {
            int h = 17;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (Wanted(r)) h = h * 31 + r.GetEntityId().GetHashCode();
            return h;
        }

        /// <summary>모델(Animator 아래)의 메시 렌더러만 — 윤곽선 자신, 다 투명한 것(손전등 빛·연기 같은 연출)은 뺀다.</summary>
        private static bool Wanted(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || r.gameObject.name == OutlineName || r.GetComponentInParent<Animator>() == null) return false;
            if (r is MeshRenderer && r.GetComponent<MeshFilter>() == null) return false;
            foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue < 3000) return true;
            return false;
        }

        private void Rebuild(int sig)
        {
            foreach (var (_, line) in pairs) if (line != null) Destroy(line.gameObject);
            pairs.Clear();
            builtFor = sig;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t != null && t.name == OutlineName) Destroy(t.gameObject); // 복제된 캐릭터(봇)가 원본에서 물려받은 윤곽선은 지운다
            if (shader == null) shader = Resources.Load<Shader>("CharacterOutline");
            if (shader == null) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!Wanted(r)) continue;
                var mats = r.sharedMaterials;
                var outlineMats = new Material[mats.Length];
                for (int m = 0; m < mats.Length; m++) outlineMats[m] = OutlineFor(mats[m]);
                Renderer line;
                if (r is SkinnedMeshRenderer smr)
                {
                    if (smr.sharedMesh == null) continue;
                    var go = new GameObject(OutlineName);
                    go.transform.SetParent(smr.transform.parent, false);
                    go.transform.SetLocalPositionAndRotation(smr.transform.localPosition, smr.transform.localRotation);
                    go.transform.localScale = smr.transform.localScale;
                    go.layer = smr.gameObject.layer;
                    var l = go.AddComponent<SkinnedMeshRenderer>();
                    l.sharedMesh = smr.sharedMesh;
                    l.bones = smr.bones;
                    l.rootBone = smr.rootBone;
                    l.updateWhenOffscreen = smr.updateWhenOffscreen;
                    l.localBounds = smr.localBounds;
                    line = l;
                }
                else
                {
                    var go = new GameObject(OutlineName);
                    go.transform.SetParent(r.transform, false);
                    go.layer = r.gameObject.layer;
                    go.AddComponent<MeshFilter>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    line = go.AddComponent<MeshRenderer>();
                }
                line.sharedMaterials = outlineMats;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                pairs.Add((r, line));
            }
        }

        /// <summary>원래 머티리얼마다 윤곽선 머티리얼 하나(같은 텍스처 알파로 오림 — 알파로 오리거나 투명한 머티리얼만).</summary>
        private static Material OutlineFor(Material src)
        {
            if (src != null && MatCache.TryGetValue(src, out var m) && m != null) return m;
            m = new Material(shader) { name = $"Outline_{(src != null ? src.name : "none")}", enableInstancing = true };
            m.SetColor("_OutlineColor", Color);
            m.SetFloat("_OutlineWidth", WidthPixels);
            if (src != null)
            {
                Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
                bool cut = src.IsKeywordEnabled("_ALPHATEST_ON") || (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) || src.renderQueue >= 2450;
                if (tex != null && cut)
                {
                    m.SetTexture("_BaseMap", tex);
                    if (src.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                    float c = src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f;
                    m.SetFloat("_Cutoff", Mathf.Clamp(c, 0.1f, 0.9f));
                }
                MatCache[src] = m;
            }
            return m;
        }
    }
}

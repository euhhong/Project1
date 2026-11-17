using UnityEngine;
using UnityEngine.Rendering;

namespace Project1.Presentation
{
    /// <summary>
    /// 윤곽선용 렌더러 복사(FE): 모델 렌더러와 같은 메시·뼈대를 쓰는 렌더러를 옆에 하나 만든다(그림자·빛 탐침 없음).
    /// CharacterOutline(흰 윤곽선)과 HiddenOpponentOutline(가려진 상대 빨간 윤곽선)이 같이 쓴다. 이름이 "~"로 시작하는 렌더러는 이런 복사본이다.
    /// </summary>
    public static class OutlineMeshCopy
    {
        /// <summary>윤곽선을 붙일 모델 렌더러인지: 모델(Animator 아래)의 메시 렌더러, 복사본("~")과 다 투명한 것(손전등 빛·연기 같은 연출)은 뺀다.</summary>
        public static bool IsModelRenderer(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || r.gameObject.name.StartsWith("~") || r.GetComponentInParent<Animator>() == null) return false;
            if (r is MeshRenderer && r.GetComponent<MeshFilter>() == null) return false;
            foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue < 3000) return true;
            return false;
        }

        /// <summary>src와 같은 메시(스킨이면 같은 뼈대)로 그리는 렌더러를 name 이름으로 만든다. 메시가 없으면 null.</summary>
        public static Renderer Create(Renderer src, string name, Material[] materials)
        {
            Renderer line;
            if (src is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return null;
                var go = new GameObject(name);
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
                var mf = src.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return null;
                var go = new GameObject(name);
                go.transform.SetParent(src.transform, false);
                go.layer = src.gameObject.layer;
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                line = go.AddComponent<MeshRenderer>();
            }
            line.sharedMaterials = materials;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        /// <summary>복사본이 원래 렌더러를 따라가게: 켜짐/꺼짐(extraOn과 함께)과 표정(블렌드셰이프).</summary>
        public static void Follow(Renderer src, Renderer copy, bool extraOn = true)
        {
            bool on = extraOn && src.enabled && src.gameObject.activeInHierarchy && !src.forceRenderingOff;
            if (copy.enabled != on) copy.enabled = on;
            if (on && src is SkinnedMeshRenderer s && copy is SkinnedMeshRenderer l && s.sharedMesh != null)
                for (int b = 0; b < s.sharedMesh.blendShapeCount; b++) l.SetBlendShapeWeight(b, s.GetBlendShapeWeight(b));
        }

        /// <summary>알파로 오리는 머티리얼이면 그 텍스처·기준값을 윤곽선 머티리얼에 옮긴다(머리카락 등 같이 오림).</summary>
        public static void CopyCutout(Material src, Material dst)
        {
            if (src == null) return;
            Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            bool cut = src.IsKeywordEnabled("_ALPHATEST_ON") || (src.HasProperty("_AlphaClip") && src.GetFloat("_AlphaClip") > 0.5f) || src.renderQueue >= 2450;
            if (tex == null || !cut) return;
            dst.SetTexture("_BaseMap", tex);
            if (src.HasProperty("_BaseMap")) dst.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
            float c = src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.5f;
            dst.SetFloat("_Cutoff", Mathf.Clamp(c, 0.1f, 0.9f));
        }
    }
}

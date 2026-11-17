using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 평타 봉인 표시(FE): 평타가 봉인된(AttackSeal — 맹인에게 밀려 벽에 박힘) 캐릭터 머리 위에 기절 게이지(StunDizzyIndicator)와 같은 모양의 막대.
    /// 왼쪽에 작은 빨간 금지 표시(⊘), 오른쪽 막대 = 남은 봉인 시간(봉인이 걸리면 가득, 시간이 지나며 오른쪽부터 줄어 0이면 풀림). 카메라를 본다.
    /// 기절 중이면 기절 게이지 바로 위에 놓는다. 플레이를 시작하면 저절로 생긴다(붙일 것 없음).
    /// </summary>
    public class AttackSealMarker : MonoBehaviour
    {
        private static readonly Vector2 BarSize = new(0.42f, 0.055f);   // 기절 게이지와 같은 크기(m)
        private const float IconSize = 0.12f, IconGap = 0.03f;
        private static readonly Color BarBack = new(0.08f, 0.08f, 0.1f, 1f);
        private static readonly Color BarFill = new(0.86f, 0.24f, 0.55f, 1f); // 자홍 — 기절(노랑)과 구분
        private static readonly Color IconColor = new(0.95f, 0.18f, 0.16f, 1f);

        private static AttackSealMarker instance;
        private static Texture2D signTexture;
        private static Material unlitBack, unlitFill, iconMat;

        private class Mark { public AttackSeal seal; public GameObject root; public Transform fill; }
        private readonly List<Mark> marks = new();
        private float scanAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("AttackSealMarker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AttackSealMarker>();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + 0.2f;
                foreach (var s in FindObjectsByType<AttackSeal>())
                    if (s != null && s.Sealed && marks.Find(m => m.seal == s) == null) marks.Add(Build(s));
            }
            var cam = Camera.main;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var m = marks[i];
                if (m.seal == null || !m.seal.isActiveAndEnabled || !m.seal.Sealed)
                {
                    if (m.root != null) Destroy(m.root);
                    marks.RemoveAt(i);
                    continue;
                }
                Transform t = m.seal.transform;
                float height = t.TryGetComponent(out CharacterController cc) ? cc.height * t.lossyScale.y : 1.8f;
                float s = Mathf.Clamp(height / 1.8f, 0.6f, 2f);
                bool stunned = t.TryGetComponent(out ICharacterStun stun) && stun.IsStunned;
                float above = stunned ? 0.36f : 0.24f; // 기절 게이지(0.24) 위
                m.root.transform.position = t.position + Vector3.up * (height + above * s);
                if (cam != null) m.root.transform.rotation = cam.transform.rotation;
                m.root.transform.localScale = Vector3.one * s;
                m.seal.TryGetCooldown(out _, out float remaining, out _); // 남은 봉인 비율(1 → 0)
                float w = BarSize.x * Mathf.Clamp01(remaining);
                float left = -BarSize.x * 0.5f + (IconSize + IconGap) * 0.5f;
                m.fill.localScale = new Vector3(Mathf.Max(w, 0.0001f), BarSize.y, 1f);
                m.fill.localPosition = new Vector3(left + w * 0.5f, 0f, -0.002f);
            }
        }

        private Mark Build(AttackSeal seal)
        {
            var root = new GameObject("AttackSealBar");
            DontDestroyOnLoad(root);
            float barCenter = (IconSize + IconGap) * 0.5f; // 아이콘 자리만큼 오른쪽으로
            Quad(root.transform, new Vector3(barCenter, 0f, 0f), new Vector3(BarSize.x + 0.02f, BarSize.y + 0.02f, 1f), Back());
            var fill = Quad(root.transform, new Vector3(barCenter, 0f, -0.002f), new Vector3(BarSize.x, BarSize.y, 1f), Fill());
            float iconX = barCenter - BarSize.x * 0.5f - IconGap - IconSize * 0.5f;
            Quad(root.transform, new Vector3(iconX, 0f, -0.002f), new Vector3(IconSize, IconSize, 1f), Icon());
            return new Mark { seal = seal, root = root, fill = fill };
        }

        private static Transform Quad(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            return go.transform;
        }

        private static Material Back() => unlitBack != null ? unlitBack : unlitBack = Unlit(BarBack);
        private static Material Fill() => unlitFill != null ? unlitFill : unlitFill = Unlit(BarFill);

        /// <summary>그림자 없는 단색 머티리얼(URP Unlit, 기절 게이지와 같음).</summary>
        private static Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "AttackSealBar", hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        private static Material Icon()
        {
            if (iconMat != null) return iconMat;
            iconMat = new Material(Shader.Find("Sprites/Default")) { name = "AttackSealIcon", mainTexture = SignTexture(), color = IconColor, hideFlags = HideFlags.DontSave };
            return iconMat;
        }

        /// <summary>금지 표시(⊘): 굵은 원 테두리 + 왼위→오아래 사선, 가장자리 부드럽게. 바탕은 검게 채워 막대 바탕과 어울리게(한 번만 만든다).</summary>
        private static Texture2D SignTexture()
        {
            if (signTexture != null) return signTexture;
            const int n = 64;
            signTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "AttackSealSign", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[n * n];
            const float outer = 0.98f, ringOuter = 0.9f, ringInner = 0.66f, slashHalf = 0.13f;
            float aa = 2.5f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float disc = Mathf.Clamp01((outer - r) / aa);                                         // 전체 원(바탕)
                    float ring = Mathf.Clamp01((ringOuter - r) / aa) * Mathf.Clamp01((r - ringInner) / aa);
                    float slash = Mathf.Clamp01((slashHalf - Mathf.Abs(u + v) / 1.41421356f) / aa) * Mathf.Clamp01((ringInner + 0.03f - r) / aa);
                    float red = Mathf.Max(ring, slash);
                    // 빨간 표시(흰색 × 머티리얼 빨강) 위, 나머지 원 안은 짙은 바탕
                    Color c = Color.Lerp(new Color(0.09f, 0.09f, 0.11f, 1f) * new Color(1f / IconColor.r, 1f / IconColor.g, 1f / IconColor.b, 1f), Color.white, red);
                    c.a = disc;
                    px[y * n + x] = c;
                }
            signTexture.SetPixels(px);
            signTexture.Apply();
            return signTexture;
        }

        private void OnDestroy()
        {
            foreach (var m in marks) if (m.root != null) Destroy(m.root);
            marks.Clear();
            if (instance == this) instance = null;
        }
    }
}

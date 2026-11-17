using System.Collections.Generic;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 가려진 상대 빨간 윤곽선(FE, 상대 캐릭터에만 — CharacterLoadout(봇)·TrainingDummySpawner가 붙인다):
    /// 카메라에서 상대 몸(발·가슴·머리)이 모두 벽·바닥에 가려 안 보이면, 맵을 뚫고 보이는 빨간 윤곽선을 켠다(셰이더 Project1/Characters/HiddenOpponentOutline).
    /// 다시 보이면 끈다(부드럽게 나타나고 사라짐). 캐릭터 몸(자기·플레이어)은 가리는 것으로 치지 않는다. 모델이 바뀌면 다시 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HiddenOpponentOutline : MonoBehaviour
    {
        public static Color Color = new(1f, 0.12f, 0.12f, 1f);
        public static float WidthPixels = 2.6f;
        private const string MaskName = "~HiddenMask", LineName = "~HiddenLine";
        private const float CheckEvery = 0.12f;
        private const float ShowAfter = 0.2f;   // 이만큼 계속 가려져 있어야 켠다(잠깐 기둥 뒤로 지나갈 때 깜빡이지 않게)
        private const float FadeSpeed = 6f;
        private static readonly float[] Heights = { 0.3f, 1.0f, 1.7f };

        private static Shader shader;
        private readonly List<(Renderer src, Renderer mask, Renderer line)> sets = new();
        private readonly List<Material> lineMats = new();
        private readonly List<Material> ownMats = new();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private int builtFor;
        private float rebuildAt, checkAt, hiddenSince = -1f, alpha = -1f;
        private bool hidden;

        private void LateUpdate()
        {
            if (Time.unscaledTime >= rebuildAt)
            {
                rebuildAt = Time.unscaledTime + 0.5f;
                int sig = Signature();
                if (sig != builtFor) Rebuild(sig);
            }
            if (Time.time >= checkAt) { checkAt = Time.time + CheckEvery; hidden = IsHidden(); }
            if (!hidden) hiddenSince = -1f; else if (hiddenSince < 0f) hiddenSince = Time.time;
            bool show = hidden && Time.time - hiddenSince >= ShowAfter;
            float a = Mathf.MoveTowards(Mathf.Max(alpha, 0f), show ? 1f : 0f, FadeSpeed * Time.deltaTime);
            if (!Mathf.Approximately(a, alpha))
            {
                alpha = a;
                var c = Color; c.a *= a;
                foreach (var m in lineMats) if (m != null) m.SetColor("_OutlineColor", c);
            }
            bool on = alpha > 0.001f;
            for (int i = sets.Count - 1; i >= 0; i--)
            {
                var (src, mask, line) = sets[i];
                if (src == null || mask == null || line == null) { Kill(mask); Kill(line); sets.RemoveAt(i); continue; }
                OutlineMeshCopy.Follow(src, mask, on);
                OutlineMeshCopy.Follow(src, line, on);
            }
        }

        /// <summary>카메라에서 발·가슴·머리로 선을 그어 셋 다 벽·바닥·물건에 막히면 가려진 것(캐릭터 몸·트리거는 무시).</summary>
        private bool IsHidden()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 eye = cam.transform.position;
            foreach (float h in Heights)
            {
                Vector3 to = transform.position + Vector3.up * h;
                Vector3 d = to - eye;
                float dist = d.magnitude;
                if (dist < 0.01f) return false;
                int n = Physics.RaycastNonAlloc(eye, d / dist, hits, dist - 0.2f, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < n && !blocked; i++)
                    if (hits[i].collider.GetComponentInParent<CharacterIdentity>() == null) blocked = true;
                if (!blocked) return false;
            }
            return true;
        }

        private int Signature()
        {
            int h = 23;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (OutlineMeshCopy.IsModelRenderer(r)) h = h * 31 + r.GetEntityId().GetHashCode();
            return h;
        }

        private void Rebuild(int sig)
        {
            Clear();
            builtFor = sig;
            alpha = -1f; // 새 머티리얼에 색을 다시 넣게
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != null && (t.name == MaskName || t.name == LineName)) Destroy(t.gameObject); // 복제본이 물려받은 것
            if (shader == null) shader = Resources.Load<Shader>("HiddenOpponentOutline");
            if (shader == null) return;
            var cache = new Dictionary<Material, (Material mask, Material line)>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!OutlineMeshCopy.IsModelRenderer(r)) continue;
                var mats = r.sharedMaterials;
                var maskMats = new Material[mats.Length];
                var lineMatsFor = new Material[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                {
                    var key = mats[m];
                    if (key == null || !cache.TryGetValue(key, out var pair)) { pair = (Make(key, true), Make(key, false)); if (key != null) cache[key] = pair; }
                    maskMats[m] = pair.mask; lineMatsFor[m] = pair.line;
                }
                var mask = OutlineMeshCopy.Create(r, MaskName, maskMats);
                var line = OutlineMeshCopy.Create(r, LineName, lineMatsFor);
                if (mask == null || line == null) { Kill(mask); Kill(line); continue; }
                mask.enabled = line.enabled = false;
                sets.Add((r, mask, line));
            }
        }

        /// <summary>마스크(몸 모양을 스텐실에, 색 없음, 큐 4000) 또는 선(부풀린 몸에서 마스크 밖만 빨갛게, 큐 4001).</summary>
        private Material Make(Material src, bool mask)
        {
            var m = new Material(shader) { name = $"{(mask ? "HiddenMask" : "HiddenLine")}_{(src != null ? src.name : "none")}", enableInstancing = true };
            m.SetFloat("_OutlineWidth", mask ? 0f : WidthPixels);
            m.SetFloat("_StencilComp", mask ? 8f : 6f); // Always / NotEqual
            m.SetFloat("_StencilOp", mask ? 2f : 0f);   // Replace / Keep
            m.SetFloat("_ColorMask", mask ? 0f : 15f);
            m.SetColor("_OutlineColor", Color);
            m.renderQueue = mask ? 4000 : 4001;
            OutlineMeshCopy.CopyCutout(src, m);
            ownMats.Add(m);
            if (!mask) lineMats.Add(m);
            return m;
        }

        private void Clear()
        {
            foreach (var (_, mask, line) in sets) { Kill(mask); Kill(line); }
            sets.Clear();
            foreach (var m in ownMats) if (m != null) Destroy(m);
            ownMats.Clear(); lineMats.Clear();
        }

        private static void Kill(Renderer r) { if (r != null) Destroy(r.gameObject); }

        private void OnDisable() { foreach (var (_, mask, line) in sets) { if (mask != null) mask.enabled = false; if (line != null) line.enabled = false; } }
        private void OnDestroy() => Clear();
    }
}

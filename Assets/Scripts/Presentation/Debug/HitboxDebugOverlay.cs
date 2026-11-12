using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Project1.Presentation
{
    /// <summary>
    /// 판정 디버그 표시. 에디터 Play와 개발 빌드에서 Hurtbox_*(맞는 판정)·Hitbox_*(때리는 판정) BoxCollider를 선으로 그린다.
    /// 씬에 붙일 필요 없이 자동으로 생긴다. 기본은 숨김이고, 조정할 때 F3으로 켜고 끈다. 판정(Collider)은 표시와 상관없이 항상 동작한다.
    /// 색: 머리 빨강, 몸통 노랑, 팔 하늘, 다리 초록 / 공격 중 Hitbox 자홍, 평소 Hitbox 회색.
    /// </summary>
    public class HitboxDebugOverlay : MonoBehaviour
    {
        private const string HurtPrefix = "Hurtbox_";
        private const string HitPrefix = "Hitbox_";
        private const float RescanInterval = 1f;

        private static bool visible = false; // 게임 중에는 숨김. F3으로 켠다.
        private readonly List<BoxCollider> boxes = new();
        private float nextScan;
        private Material lineMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (!Debug.isDebugBuild) return; // 에디터 Play와 개발 빌드에서만
            var go = new GameObject(nameof(HitboxDebugOverlay));
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<HitboxDebugOverlay>();
        }

        private void OnEnable() => RenderPipelineManager.endCameraRendering += Draw;
        private void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) visible = !visible;
            if (Time.unscaledTime >= nextScan) { Rescan(); nextScan = Time.unscaledTime + RescanInterval; }
        }

        private void Rescan()
        {
            boxes.Clear();
            foreach (var b in FindObjectsByType<BoxCollider>(FindObjectsInactive.Include))
                if (b.name.StartsWith(HurtPrefix) || b.name.StartsWith(HitPrefix)) boxes.Add(b);
        }

        private void Draw(ScriptableRenderContext context, Camera cam)
        {
            if (!visible || boxes.Count == 0 || cam.cameraType == CameraType.Preview) return;
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always); // 몸에 가려도 보이게
                lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            }
            lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            foreach (var b in boxes)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                GL.Color(ColorOf(b));
                DrawBox(b.transform.localToWorldMatrix, b.center, b.size);
            }
            GL.End();
            GL.PopMatrix();
        }

        /// <summary>부위·종류별 표시 색.</summary>
        public static Color ColorOf(BoxCollider b)
        {
            if (b.name.StartsWith(HitPrefix))
            {
                var detector = b.GetComponentInParent<Project1.Systems.AttackHitDetector>();
                bool attacking = detector != null && detector.IsAttacking;
                return attacking ? new Color(1f, 0.2f, 0.9f) : new Color(0.55f, 0.55f, 0.55f, 0.5f);
            }
            // Hurtbox_<부위>_<뼈>
            if (b.name.StartsWith(HurtPrefix + "Head")) return new Color(1f, 0.25f, 0.25f);
            if (b.name.StartsWith(HurtPrefix + "Body")) return new Color(1f, 0.85f, 0.2f);
            if (b.name.StartsWith(HurtPrefix + "Arms") || b.name.StartsWith(HurtPrefix + "Hands")) return new Color(0.3f, 0.75f, 1f);
            if (b.name.StartsWith(HurtPrefix + "Legs") || b.name.StartsWith(HurtPrefix + "Feet")) return new Color(0.3f, 1f, 0.4f);
            return Color.white;
        }

        private static void DrawBox(Matrix4x4 m, Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
                p[i] = m.MultiplyPoint3x4(c + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
            int[] e = { 0,1, 2,3, 4,5, 6,7, 0,2, 1,3, 4,6, 5,7, 0,4, 1,5, 2,6, 3,7 };
            for (int i = 0; i < e.Length; i += 2) { GL.Vertex(p[e[i]]); GL.Vertex(p[e[i + 1]]); }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}

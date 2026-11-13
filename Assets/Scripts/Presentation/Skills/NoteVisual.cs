using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 안토니오 음표 연출(INoteField, BE를 읽어 그린다).
    /// - 놓은 음표: 떠서 천천히 오르내린다(NoteModel). 첫 음표 아래에는 둘째를 놓을 남은 시간만큼 줄어드는 붉은 고리.
    /// - 좌클릭을 꾹 누르는 동안: 마우스가 가리키는 자리에 반투명 미리보기 음표(놓을 수 없으면 흐리게) + 바닥 고리.
    /// - 두 음표가 이어지면: 둘 사이에 지지직거리는 붉은 전류선. 맞으면 맞은 자리에 붉은 불꽃이 잠깐 터진다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 안토니오만).
    /// </summary>
    [DisallowMultipleComponent]
    public class NoteVisual : MonoBehaviour
    {
        [Tooltip("음표 크기 배율.")]
        [SerializeField, Min(0.1f)] private float size = 1f;
        [SerializeField] private float bob = 0.06f;
        [SerializeField] private Color wireColor = new(1f, 0.12f, 0.08f, 1f);
        [Tooltip("전류선이 지지직거리는 폭(m) / 모양이 바뀌는 간격(초).")]
        [SerializeField, Min(0f)] private float jitter = 0.12f;
        [SerializeField, Min(0.01f)] private float jitterInterval = 0.05f;

        private const int WirePoints = 22, RingPoints = 40;
        private INoteField field;
        private Transform root;
        private readonly List<NoteModel> notes = new();
        private NoteModel preview;
        private LineRenderer wire, wireGlow, timerRing, previewRing;
        private Transform hitFlash;
        private Material flashMat;
        private float nextJitter;
        private readonly Vector3[] wireOffsets = new Vector3[WirePoints];

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            if (flashMat != null) Destroy(flashMat);
        }

        private void LateUpdate()
        {
            if (field == null) field = GetComponent<INoteField>();
            if (field == null) return;
            if (root == null) Build();
            float t = Time.time, h = field.NoteHeight;

            // 놓은 음표
            var list = field.Notes;
            while (notes.Count < list.Count) notes.Add(MakeNote($"Note_{notes.Count}", false));
            while (notes.Count > list.Count) { Destroy(notes[^1].gameObject); notes.RemoveAt(notes.Count - 1); }
            for (int i = 0; i < notes.Count; i++)
                notes[i].transform.position = list[i] + Vector3.up * (h + Mathf.Sin(t * 2f + i * 1.3f) * bob);

            // 첫 음표 아래 남은 시간 고리(줄어듦)
            bool waiting = list.Count == 1 && !field.Linked && field.PendingDuration > 0f;
            timerRing.gameObject.SetActive(waiting);
            if (waiting) SetArc(timerRing, list[0], 0.6f, Mathf.Clamp01(field.PendingTimeLeft / field.PendingDuration));

            // 미리보기
            bool showPreview = field.Placing;
            preview.gameObject.SetActive(showPreview);
            previewRing.gameObject.SetActive(showPreview);
            if (showPreview)
            {
                preview.GhostValid = field.PreviewValid;
                preview.transform.position = field.PreviewPoint + Vector3.up * h;
                previewRing.startColor = previewRing.endColor = field.PreviewValid ? new Color(1f, 0.4f, 0.4f, 0.6f) : new Color(0.6f, 0.6f, 0.6f, 0.4f);
                SetArc(previewRing, field.PreviewPoint, 0.45f, 1f);
            }

            // 전류선
            bool linked = field.Linked && list.Count == 2;
            wire.gameObject.SetActive(linked);
            wireGlow.gameObject.SetActive(linked);
            if (linked)
            {
                Vector3 a = notes[0].transform.position, b = notes[1].transform.position;
                if (t >= nextJitter)
                {
                    nextJitter = t + jitterInterval;
                    Vector3 dir = (b - a).normalized;
                    Vector3 side = Vector3.Cross(dir, Vector3.up); if (side.sqrMagnitude < 1e-4f) side = Vector3.right; side.Normalize();
                    Vector3 up = Vector3.Cross(side, dir).normalized;
                    for (int i = 0; i < WirePoints; i++)
                    {
                        float k = i / (float)(WirePoints - 1);
                        float amp = jitter * Mathf.Sin(k * Mathf.PI); // 양 끝(음표)에서는 붙고 가운데가 많이 튄다
                        wireOffsets[i] = (side * Random.Range(-1f, 1f) + up * Random.Range(-1f, 1f)) * amp;
                    }
                }
                for (int i = 0; i < WirePoints; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, i / (float)(WirePoints - 1)) + wireOffsets[i];
                    wire.SetPosition(i, p);
                    wireGlow.SetPosition(i, p);
                }
            }

            // 맞은 자리 불꽃
            float since = t - field.LastHitTime;
            bool flash = field.LastHitTime >= 0f && since < 0.25f;
            hitFlash.gameObject.SetActive(flash);
            if (flash)
            {
                hitFlash.position = field.LastHitPoint;
                hitFlash.localScale = Vector3.one * Mathf.Lerp(0.25f, 0.9f, since / 0.25f);
                flashMat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.12f, 1f - since / 0.25f));
            }
        }

        private void Build()
        {
            root = new GameObject("Notes").transform; // 캐릭터를 따라 움직이지 않게 따로 둔다
            preview = MakeNote("Note_Preview", true);
            wire = Line("ElectricWire", wireColor, 0.06f, WirePoints, false);
            wireGlow = Line("ElectricWireGlow", new Color(wireColor.r, wireColor.g, wireColor.b, 0.25f), 0.2f, WirePoints, false);
            timerRing = Line("NoteTimer", new Color(1f, 0.2f, 0.15f, 0.8f), 0.05f, RingPoints, false);
            previewRing = Line("NotePreviewRing", Color.white, 0.05f, RingPoints, true);
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            f.name = "WireHitFlash";
            Destroy(f.GetComponent<Collider>());
            f.transform.SetParent(root, false);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            flashMat = new Material(shader) { name = "WireHitFlash" };
            flashMat.SetFloat("_Surface", 1f);
            flashMat.SetOverrideTag("RenderType", "Transparent");
            flashMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            flashMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            flashMat.SetInt("_ZWrite", 0);
            flashMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            flashMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            f.GetComponent<Renderer>().sharedMaterial = flashMat;
            hitFlash = f.transform;
            hitFlash.gameObject.SetActive(false);
        }

        private NoteModel MakeNote(string name, bool ghost)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * size;
            var m = go.AddComponent<NoteModel>();
            m.Ghost = ghost;
            return m;
        }

        private LineRenderer Line(string name, Color color, float width, int points, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.loop = loop; lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return lr;
        }

        // 바닥 고리(fill = 0~1 만큼의 호)
        private static void SetArc(LineRenderer lr, Vector3 center, float radius, float fill)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)(n - 1)) * fill * Mathf.PI * 2f + Mathf.PI * 0.5f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.04f);
            }
        }
    }
}

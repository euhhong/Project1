using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 판자 모양(FE): 판자(Pallet)에 나무 판자 모델(Resources WoodPlanks/WoodPlanks_Pallet — 사용자 모델 Bundled Wooden Planks 1번을
    /// Tools/Blender/orient_upright_prop.py로 세운 것: 원점 = 바닥 가운데, 위 = 길이, 앞 = 두께)을 붙여 판자 크기(높이·폭·두께)에 맞춘다.
    /// - 게임 중: 판자 몸(Board — 넘어갈 때 도는 자식)에 붙인다.
    /// - 편집 화면: 판자 루트에 미리보기로 붙인다(씬에 저장되지 않음) — 맵에서 판자 자리를 눈으로 보고 옮길 수 있게.
    /// 모델 축이 어긋나 들어와도 가장 긴 쪽을 위, 가장 얇은 쪽을 앞으로 돌려 맞춘다. 판자 루트에 붙인다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PalletVisual : MonoBehaviour
    {
        private const string ModelResource = "WoodPlanks/WoodPlanks_Pallet", MaterialResource = "WoodPlanks/WoodPlanks";

        private GameObject preview;

        private void OnEnable()
        {
            if (!Application.isPlaying) BuildPreview();
        }

        private void OnDisable()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (!TryGetComponent(out Pallet pallet)) return;
            Build(pallet.Board, pallet.Width, pallet.Height, pallet.Thickness);
        }

        private void BuildPreview()
        {
            if (preview != null || !TryGetComponent(out Pallet pallet)) return;
            preview = Build(transform, pallet.Width, pallet.Height, pallet.Thickness);
            if (preview == null) return;
            foreach (var t in preview.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave; // 씬에 저장하지 않는 미리보기
        }

        /// <summary>parent 아래에 판자 크기에 맞춘 모델을 만든다(가장 바깥 오브젝트를 돌려준다).</summary>
        private static GameObject Build(Transform parent, float width, float height, float thickness)
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null) { Debug.LogWarning("[FE] 판자 모델을 찾지 못했습니다: Resources/" + ModelResource); return null; }
            var holder = new GameObject("PalletModel").transform;
            holder.SetParent(parent, false);
            var model = Instantiate(prefab, holder, false);
            var mat = Resources.Load<Material>(MaterialResource);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>()) DestroyImmediate(c); // 막는 판정은 Pallet이 따로 가진다

            // holder 기준 모델 크기 → 가장 긴 축 = 위, 가장 얇은 축 = 앞으로 돌리고 크기를 맞춘다
            Bounds b = LocalBounds(holder, model);
            Vector3 s = b.size;
            int up = s.y >= s.x && s.y >= s.z ? 1 : s.x >= s.z ? 0 : 2;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            if (thin == up) thin = up == 2 ? 0 : 2;
            static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            model.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(Axis(thin), Axis(up))) * model.transform.localRotation;
            b = LocalBounds(holder, model);
            holder.localScale = new Vector3(width / Mathf.Max(b.size.x, 1e-4f), height / Mathf.Max(b.size.y, 1e-4f), thickness / Mathf.Max(b.size.z, 1e-4f));
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z); // 바닥 가운데를 원점에
            return holder.gameObject;
        }

        /// <summary>holder 좌표계에서 model 렌더러들의 경계(메시 꼭짓점 기준).</summary>
        private static Bounds LocalBounds(Transform holder, GameObject model)
        {
            bool any = false; Bounds b = default;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = holder.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            return b;
        }
    }
}

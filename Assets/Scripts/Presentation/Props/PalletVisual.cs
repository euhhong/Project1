using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 판자 모양(FE): 판자(Pallet)의 판자 몸(Board)에 나무 판자 모델(Resources WoodPlanks/WoodPlanks_Pallet — 사용자 모델 Bundled Wooden Planks 1번을
    /// Tools/Blender/orient_upright_prop.py로 세운 것: 원점 = 바닥 가운데, 위 = 길이, 앞 = 두께)을 붙여 판자 크기(높이·폭·두께)에 맞춘다.
    /// 모델 축이 어긋나 들어와도 가장 긴 쪽을 위, 가장 얇은 쪽을 앞으로 돌려 맞춘다. 판자 루트에 붙인다(PalletSpawner).
    /// </summary>
    [DisallowMultipleComponent]
    public class PalletVisual : MonoBehaviour
    {
        private const string ModelResource = "WoodPlanks/WoodPlanks_Pallet", MaterialResource = "WoodPlanks/WoodPlanks";

        private void Start()
        {
            if (!TryGetComponent(out Pallet pallet)) return;
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null) { Debug.LogWarning("[FE] 판자 모델을 찾지 못했습니다: Resources/" + ModelResource); return; }
            var holder = new GameObject("PalletModel").transform;
            holder.SetParent(pallet.Board, false);
            var model = Instantiate(prefab, holder, false);
            var mat = Resources.Load<Material>(MaterialResource);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c); // 막는 판정은 Pallet이 따로 가진다

            // 판자 몸 기준 모델 크기 → 가장 긴 축 = 위, 가장 얇은 축 = 앞으로 돌리고 크기를 맞춘다
            Bounds b = LocalBounds(holder, model);
            Vector3 s = b.size;
            int up = s.y >= s.x && s.y >= s.z ? 1 : s.x >= s.z ? 0 : 2;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            if (thin == up) thin = up == 2 ? 0 : 2;
            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            model.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(Axis(thin), Axis(up))) * model.transform.localRotation;
            b = LocalBounds(holder, model);
            holder.localScale = new Vector3(pallet.Width / Mathf.Max(b.size.x, 1e-4f), pallet.Height / Mathf.Max(b.size.y, 1e-4f), pallet.Thickness / Mathf.Max(b.size.z, 1e-4f));
            // 바닥 가운데를 판자 몸 원점에
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
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

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 캐릭터 모델(Assets/Models/IdentityV, Humanoid)을 임포트할 때 부위 뼈에 판정용 Collider를 자동으로 붙인다.
    /// 모델 파일에 들어가므로 모델을 바꾸거나 다시 임포트해도 유지된다. 씬에 손으로 붙이지 않는다.
    ///
    /// 만드는 자식 오브젝트(모두 BoxCollider, isTrigger):
    ///   Hurtbox_Head(머리 뼈), Hurtbox_Body(가슴/척추 뼈), Hurtbox_LeftHand·RightHand, Hurtbox_LeftFoot·RightFoot — 맞는 판정, 켜짐
    ///   Hitbox_LeftHand·RightHand, Hitbox_LeftFoot·RightFoot, Hitbox_Prop(손에 든 소품) — 때리는 판정, 기본 꺼짐
    /// 크기는 부위 메시(<캐릭터>_Head/_Body/_Hands/_Feet)의 정점을 그 뼈 기준으로 감싸서 정한다.
    /// </summary>
    public class BodyPartColliderPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Models/IdentityV/";
        public const string HurtPrefix = "Hurtbox_";
        public const string HitPrefix = "Hitbox_";
        private const float Padding = 1.08f;      // 부위 메시보다 살짝 크게
        private const float HitboxScale = 1.15f;  // 때리는 판정은 조금 더 넉넉하게

        // 임포트 설정이 바뀌면 다시 만들도록 버전을 둔다.
        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root) || assetPath.Contains("/_Motions/")) return;
            var importer = assetImporter as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human) return;

            // Humanoid 이름 → 실제 뼈 Transform
            var all = root.GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>();
            foreach (var t in all) byName[t.name] = t;
            Transform Bone(string human)
            {
                var hb = importer.humanDescription.human.FirstOrDefault(h => h.humanName == human);
                return !string.IsNullOrEmpty(hb.boneName) && byName.TryGetValue(hb.boneName, out var t) ? t : null;
            }

            var smrs = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer Part(string suffix) => smrs.FirstOrDefault(s => s.name.EndsWith("_" + suffix));

            var head = Bone("Head");
            var torso = Bone("Chest") ?? Bone("Spine");
            var hips = Bone("Hips");
            var lHand = Bone("LeftHand"); var rHand = Bone("RightHand");
            var lFoot = Bone("LeftFoot"); var rFoot = Bone("RightFoot");

            // 머리: 머리 메시 전체
            Fit(Part("Head"), head, null, HurtPrefix + "Head", true, 1f);
            // 몸통: 몸통 메시 중 엉덩이~가슴 뼈에 묶인 정점만 (팔·다리 제외)
            var torsoBones = new HashSet<Transform>(new[] { hips, Bone("Spine"), Bone("Chest"), Bone("UpperChest") }.Where(b => b != null));
            Fit(Part("Body"), torso, torsoBones, HurtPrefix + "Body", true, 1f);
            // 손·발: 좌우를 뼈 계층으로 나눈다
            foreach (var (bone, side, suffix) in new[] { (lHand, "Left", "Hands"), (rHand, "Right", "Hands"), (lFoot, "Left", "Feet"), (rFoot, "Right", "Feet") })
            {
                if (bone == null) continue;
                var chain = new HashSet<Transform>(bone.GetComponentsInChildren<Transform>(true)) { bone };
                string part = suffix == "Hands" ? "Hand" : "Foot";
                Fit(Part(suffix), bone, chain, HurtPrefix + side + part, true, 1f);
                Fit(Part(suffix), bone, chain, HitPrefix + side + part, false, HitboxScale);
            }
            // 손에 든 소품: 메시 자체 크기로
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!(mr.name.EndsWith("_Prop") || mr.name.EndsWith("_Syringe"))) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                var go = MakeChild(mr.transform, HitPrefix + "Prop");
                var box = go.AddComponent<BoxCollider>();
                box.center = b.center; box.size = b.size * Padding; box.isTrigger = true; box.enabled = false;
            }
        }

        /// <summary>부위 메시의 정점(bones 필터 적용)을 target 뼈 기준으로 감싸는 BoxCollider를 만든다.</summary>
        private static void Fit(SkinnedMeshRenderer smr, Transform target, HashSet<Transform> onlyBones, string name, bool enabled, float scale)
        {
            if (smr == null || target == null || smr.sharedMesh == null) return;
            var mesh = smr.sharedMesh;
            var verts = mesh.vertices;
            var weights = mesh.boneWeights;
            var bones = smr.bones;
            var bind = mesh.bindposes;
            int ti = System.Array.IndexOf(bones, target);
            // 바인드 시점의 '메시 → target 뼈' 변환. target이 이 메시의 뼈 목록에 없으면 현재 계층으로 계산한다.
            Matrix4x4 toBone = ti >= 0 ? bind[ti] : target.worldToLocalMatrix * smr.transform.localToWorldMatrix;

            bool any = false; Vector3 mn = Vector3.zero, mx = Vector3.zero;
            for (int i = 0; i < verts.Length; i++)
            {
                if (onlyBones != null && weights.Length == verts.Length)
                {
                    int bi = weights[i].boneIndex0;
                    if (bi < 0 || bi >= bones.Length || !onlyBones.Contains(bones[bi])) continue;
                }
                Vector3 p = toBone.MultiplyPoint3x4(verts[i]);
                if (!any) { mn = mx = p; any = true; } else { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            }
            if (!any) return;
            var go = MakeChild(target, name);
            var box = go.AddComponent<BoxCollider>();
            box.center = (mn + mx) * 0.5f;
            box.size = (mx - mn) * Padding * scale;
            box.isTrigger = true;
            box.enabled = enabled;
        }

        private static GameObject MakeChild(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}

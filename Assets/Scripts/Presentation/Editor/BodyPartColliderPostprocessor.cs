using System.Collections.Generic;
using System.Linq;
using Project1.Core;
using Project1.Systems;
using UnityEditor;
using UnityEngine;
using BodyPart = Project1.Core.BodyPart;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 캐릭터 모델(Assets/Models/IdentityV, Humanoid)을 임포트할 때 판정 Collider를 자동으로 붙인다.
    /// 모델 파일에 들어가므로 모델을 바꾸거나 다시 임포트해도 유지된다. 씬에 손으로 붙이지 않는다.
    ///
    /// 맞는 판정(Hurtbox): Humanoid 뼈마다 상자 하나. 메시의 모든 정점이 어느 상자 안에 들어가고, 관절은 겹쳐서 빈틈이 없다.
    ///   부위는 4개 — Head(머리), Body(골반·척추·가슴·목·어깨), Arms(위팔·아래팔·손·손가락), Legs(허벅지·종아리·발·발가락).
    ///   이름: Hurtbox_<부위>_<뼈>  예) Hurtbox_Arms_LeftLowerArm
    /// 때리는 판정(Hitbox): Hitbox_LeftHand·RightHand·LeftFoot·RightFoot(손·발 상자보다 조금 크게), Hitbox_Weapon(손에 든 소품).
    /// </summary>
    public class BodyPartColliderPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/Models/IdentityV/";
        public const string HurtPrefix = "Hurtbox_";
        public const string HitPrefix = "Hitbox_";
        private const float Padding = 1.06f;        // 상자를 정점보다 조금 크게
        private const float JointMargin = 0.025f;   // 키 대비 여유(관절 겹침용)
        private const float HitboxScale = 1.2f;

        // BodyPart 값(이름이 Hands/Feet → Arms/Legs로 바뀌어도 값은 같다)
        private const BodyPart PartHead = (BodyPart)0, PartBody = (BodyPart)1, PartArms = (BodyPart)2, PartLegs = (BodyPart)3;

        public override uint GetVersion() => 2;

        // 상자를 갖는 Humanoid 뼈와 부위. 손가락·눈·턱·발가락은 각각 손·머리·발 상자에 합친다.
        private static readonly (string human, BodyPart part)[] BoxBones =
        {
            ("Head", PartHead), ("Neck", PartBody), ("Hips", PartBody), ("Spine", PartBody), ("Chest", PartBody), ("UpperChest", PartBody),
            ("LeftShoulder", PartBody), ("RightShoulder", PartBody),
            ("LeftUpperArm", PartArms), ("LeftLowerArm", PartArms), ("LeftHand", PartArms),
            ("RightUpperArm", PartArms), ("RightLowerArm", PartArms), ("RightHand", PartArms),
            ("LeftUpperLeg", PartLegs), ("LeftLowerLeg", PartLegs), ("LeftFoot", PartLegs),
            ("RightUpperLeg", PartLegs), ("RightLowerLeg", PartLegs), ("RightFoot", PartLegs),
        };

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Root) || assetPath.Contains("/_Motions/")) return;
            var importer = assetImporter as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human) return;

            var byName = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            var human = new Dictionary<string, Transform>();
            foreach (var h in importer.humanDescription.human)
                if (!string.IsNullOrEmpty(h.boneName) && byName.TryGetValue(h.boneName, out var t)) human[h.humanName] = t;

            // 상자 주인 뼈 → 부위
            var owners = new Dictionary<Transform, (string human, BodyPart part)>();
            foreach (var (hn, part) in BoxBones)
                if (human.TryGetValue(hn, out var t)) owners[t] = (hn, part);
            if (owners.Count == 0) return;
            var hips = human.TryGetValue("Hips", out var hp) ? hp : owners.Keys.First();

            // 정점마다 가장 크게 묶인 뼈 → 위로 올라가며 처음 만나는 상자 주인 뼈에 넣는다.
            var points = owners.Keys.ToDictionary(k => k, _ => new List<Vector3>());
            Bounds all = default; bool anyAll = false;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh; if (mesh == null) continue;
                var verts = mesh.vertices; var weights = mesh.boneWeights; var bones = smr.bones;
                var ownerOfBone = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) ownerOfBone[i] = FindOwner(bones[i], owners) ?? hips;
                var meshToWorld = smr.transform.localToWorldMatrix;
                for (int i = 0; i < verts.Length; i++)
                {
                    int bi = weights.Length == verts.Length ? weights[i].boneIndex0 : -1;
                    var owner = bi >= 0 && bi < bones.Length ? ownerOfBone[bi] : hips;
                    Vector3 w = meshToWorld.MultiplyPoint3x4(verts[i]);
                    points[owner].Add(w);
                    if (!anyAll) { all = new Bounds(w, Vector3.zero); anyAll = true; } else all.Encapsulate(w);
                }
            }
            float margin = JointMargin * (anyAll ? all.size.y : 1f);

            var boxOf = new Dictionary<string, (Transform bone, Vector3 c, Vector3 s)>();
            foreach (var kv in points)
            {
                var bone = kv.Key; var (hn, part) = owners[bone];
                // 상자가 뼈에서 다음 뼈까지는 반드시 덮도록 뼈 자신과 자식 상자 뼈 위치도 포함한다.
                var pts = new List<Vector3>(kv.Value) { bone.position };
                foreach (var child in owners.Keys) if (child.parent == bone || (child.parent != null && child.parent.parent == bone && !owners.ContainsKey(child.parent))) pts.Add(child.position);
                if (pts.Count < 2) continue;
                Vector3 mn = bone.InverseTransformPoint(pts[0]), mx = mn;
                foreach (var w in pts) { var p = bone.InverseTransformPoint(w); mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                float localMargin = margin / Mathf.Max(bone.lossyScale.x, 1e-6f);
                Vector3 size = (mx - mn) * Padding + Vector3.one * localMargin * 2f;
                Vector3 center = (mn + mx) * 0.5f;
                var go = MakeChild(bone, $"{HurtPrefix}{part}_{hn}");
                var box = go.AddComponent<BoxCollider>(); box.center = center; box.size = size; box.isTrigger = true;
                SetLayer(go, CombatLayers.HurtboxName);
                go.AddComponent<Hurtbox>().SetPart(part);
                boxOf[hn] = (bone, center, size);
            }

            // 때리는 판정: 손·발
            foreach (var (hn, slot) in new[] { ("LeftHand", HitboxSlot.LeftHand), ("RightHand", HitboxSlot.RightHand), ("LeftFoot", HitboxSlot.LeftFoot), ("RightFoot", HitboxSlot.RightFoot) })
            {
                if (!boxOf.TryGetValue(hn, out var b)) continue;
                var go = MakeChild(b.bone, HitPrefix + hn);
                var box = go.AddComponent<BoxCollider>(); box.center = b.c; box.size = b.s * HitboxScale; box.isTrigger = true;
                SetLayer(go, CombatLayers.HitboxName);
                go.AddComponent<Hitbox>().SetSlot(slot);
            }
            // 때리는 판정: 손에 든 소품
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!(mr.name.EndsWith("_Prop") || mr.name.EndsWith("_Syringe"))) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var bnd = mf.sharedMesh.bounds;
                var go = MakeChild(mr.transform, HitPrefix + "Weapon");
                var box = go.AddComponent<BoxCollider>(); box.center = bnd.center; box.size = bnd.size * 1.1f; box.isTrigger = true;
                SetLayer(go, CombatLayers.HitboxName);
                go.AddComponent<Hitbox>().SetSlot(HitboxSlot.Weapon);
            }
        }

        private static Transform FindOwner(Transform t, Dictionary<Transform, (string, BodyPart)> owners)
        {
            for (var x = t; x != null; x = x.parent) if (owners.ContainsKey(x)) return x;
            return null;
        }

        private static void SetLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
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

using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Systems
{
    /// <summary>
    /// 맞는 판정(Hurtbox)이 하나도 없는 모델(Generic 리그 — 하스터 등)에 실행 중에 판정 상자를 만든다.
    /// Humanoid 모델은 임포트할 때 BodyPartColliderPostprocessor가 뼈마다 상자를 넣지만 Generic은 빠져서 그랩·평타 등 어떤 공격에도 맞지 않았다.
    /// - 메시 정점을 가장 크게 묶인 뼈별로 모아, 정점이 많은 뼈(전체의 minShare 이상)마다 그 뼈 아래 상자 하나(바인드 자세 기준, 뼈를 따라 움직임).
    ///   정점이 적은 뼈는 위로 올라가 처음 만나는 상자 뼈에 합친다(촉수 끝 마디 등).
    /// - 부위: 뼈 이름에 head → 머리, arm·hand → 팔, leg·foot·thigh·calf → 다리, 그 밖 → 몸. 머리 이름 뼈가 없으면 가장 높은 상자를 머리로.
    /// 캐릭터를 고를 때 모델을 바꾼 뒤 CharacterLoadout이 부른다. 이미 Hurtbox가 있으면 아무것도 하지 않는다.
    /// </summary>
    public static class GenericHurtboxes
    {
        private const float MinShare = 0.03f;   // 상자를 갖는 뼈: 정점이 전체의 이 비율 이상
        private const float Padding = 1.06f;
        private const float MarginOfHeight = 0.02f;

        public static void EnsureFor(GameObject model)
        {
            if (model == null || model.GetComponentInChildren<Hurtbox>(true) != null) return;

            // 뼈별 정점(뼈 로컬, 바인드 자세)
            var pointsByBone = new Dictionary<Transform, List<Vector3>>();
            int total = 0;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || IsWeapon(smr.name)) continue;
                var bones = smr.bones; var binds = mesh.bindposes;
                var verts = mesh.vertices; var weights = mesh.boneWeights;
                if (bones.Length == 0 || binds.Length != bones.Length || weights.Length != verts.Length) continue;
                var used = new bool[verts.Length];
                foreach (int vi in mesh.triangles) used[vi] = true;
                for (int i = 0; i < verts.Length; i++)
                {
                    if (!used[i]) continue;
                    int b = weights[i].boneIndex0;
                    if (b < 0 || b >= bones.Length || bones[b] == null) continue;
                    if (!pointsByBone.TryGetValue(bones[b], out var list)) pointsByBone[bones[b]] = list = new List<Vector3>();
                    list.Add(binds[b].MultiplyPoint3x4(verts[i]));
                    Vector3 w = smr.transform.TransformPoint(verts[i]);
                    minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y);
                    total++;
                }
            }
            if (total == 0) return;

            // 상자 뼈 고르기 + 적은 뼈는 위 상자 뼈에 합치기
            var owners = new HashSet<Transform>();
            Transform biggest = null; int biggestCount = 0;
            foreach (var kv in pointsByBone)
            {
                if (kv.Value.Count >= total * MinShare) owners.Add(kv.Key);
                if (kv.Value.Count > biggestCount) { biggest = kv.Key; biggestCount = kv.Value.Count; }
            }
            if (owners.Count == 0) owners.Add(biggest);
            var boxPoints = new Dictionary<Transform, List<Vector3>>();
            foreach (var o in owners) boxPoints[o] = new List<Vector3>();
            foreach (var kv in pointsByBone)
            {
                Transform owner = null;
                for (var t = kv.Key; t != null && t != model.transform.parent; t = t.parent) if (owners.Contains(t)) { owner = t; break; }
                if (owner == null) owner = biggest;
                if (owner == kv.Key) boxPoints[owner].AddRange(kv.Value);
                else foreach (var p in kv.Value) boxPoints[owner].Add(owner.InverseTransformPoint(kv.Key.TransformPoint(p))); // 지금 자세 기준으로 옮김(합친 끝 마디는 짧아 차이가 작다)
            }

            float height = Mathf.Max(maxY - minY, 0.5f);
            bool anyHead = false;
            Transform highest = null; float highestY = float.MinValue;
            var made = new List<(Transform bone, Hurtbox hurt)>();
            foreach (var kv in boxPoints)
            {
                var bone = kv.Key; var pts = kv.Value;
                if (pts.Count < 2) continue;
                Vector3 mn = pts[0], mx = pts[0];
                foreach (var p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                float localMargin = height * MarginOfHeight / Mathf.Max(bone.lossyScale.x, 1e-6f);
                var go = new GameObject("Hurtbox_" + bone.name);
                go.transform.SetParent(bone, false);
                var box = go.AddComponent<BoxCollider>();
                box.center = (mn + mx) * 0.5f;
                box.size = (mx - mn) * Padding + Vector3.one * localMargin * 2f;
                box.isTrigger = true;
                var hurt = go.AddComponent<Hurtbox>();
                BodyPart part = PartOf(bone.name);
                if (part == BodyPart.Head) anyHead = true;
                hurt.SetPart(part);
                made.Add((bone, hurt));
                float top = bone.TransformPoint(box.center).y;
                if (top > highestY) { highestY = top; highest = bone; }
            }
            if (!anyHead && highest != null)
                foreach (var (bone, hurt) in made) if (bone == highest) hurt.SetPart(BodyPart.Head);
        }

        private static BodyPart PartOf(string boneName)
        {
            string n = boneName.ToLowerInvariant();
            if (n.Contains("head")) return BodyPart.Head;
            if (n.Contains("arm") || n.Contains("hand")) return BodyPart.Arms;
            if (n.Contains("leg") || n.Contains("foot") || n.Contains("thigh") || n.Contains("calf")) return BodyPart.Legs;
            return BodyPart.Body;
        }

        private static bool IsWeapon(string name) => name.EndsWith("_Weapon") || name.EndsWith("_Prop") || name.EndsWith("_Syringe");
    }
}

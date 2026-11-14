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
    /// - 손 Hitbox도 없으면 좌우 팔 끝 상자를 조금 키워 손 판정으로 만든다(AddHandHitboxes).
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
            // 팔 뼈 아래에 달린 상자(하스터 아래팔 끝 촉수 팔 등)도 팔.
            foreach (var (bone, hurt) in made)
                if (hurt.Part == BodyPart.Body && UnderArm(bone, made, model.transform)) hurt.SetPart(BodyPart.Arms);

            AddHandHitboxes(model, made);
        }

        /// <summary>
        /// 때리는 판정(손)이 없으면: 왼쪽·오른쪽 팔 상자(뼈 이름의 L/R, 가장 끝 마디 — 손 마디가 합쳐진 아래팔 등)를 HitboxScale배 크게 해서 손 Hitbox로.
        /// 공격이 손 자리(RightHand·LeftHand)로 맞히는 캐릭터(하스터 두 손 내려찍기)가 실제로 맞힐 수 있게. AttackHitDetector가 다시 모으게 한다.
        /// </summary>
        private static void AddHandHitboxes(GameObject model, List<(Transform bone, Hurtbox hurt)> made)
        {
            bool hasLeft = false, hasRight = false;
            foreach (var h in model.GetComponentsInChildren<Hitbox>(true))
            {
                hasLeft |= (h.Slot & HitboxSlot.LeftHand) != 0;
                hasRight |= (h.Slot & HitboxSlot.RightHand) != 0;
            }
            Transform left = null, right = null; int leftDepth = -1, rightDepth = -1;
            foreach (var (bone, hurt) in made)
            {
                if (hurt.Part != BodyPart.Arms) continue;
                int side = SideUp(bone, model.transform), depth = Depth(bone, model.transform);
                if (side < 0 && depth > leftDepth) { left = bone; leftDepth = depth; }
                if (side > 0 && depth > rightDepth) { right = bone; rightDepth = depth; }
            }
            if (!hasLeft && left != null) MakeHandHitbox(left, HitboxSlot.LeftHand);
            if (!hasRight && right != null) MakeHandHitbox(right, HitboxSlot.RightHand);
            var detector = model.GetComponentInParent<AttackHitDetector>();
            if (detector != null) detector.RefreshHitboxes();
        }

        private static void MakeHandHitbox(Transform bone, HitboxSlot slot)
        {
            var hurtBox = bone.Find("Hurtbox_" + bone.name)?.GetComponent<BoxCollider>();
            if (hurtBox == null) return;
            var go = new GameObject("Hitbox_" + slot);
            go.transform.SetParent(bone, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = hurtBox.center;
            box.size = hurtBox.size * HitboxScale;
            box.isTrigger = true;
            go.AddComponent<Hitbox>().SetSlot(slot);
        }

        private const float HitboxScale = 1.2f;

        /// <summary>뼈 이름의 좌우: -1 = 왼쪽, +1 = 오른쪽, 0 = 모름. "biped L Forearm", "LeftHand", "hand_l", "Hand.R" 등.</summary>
        private static int SideOf(string boneName)
        {
            string n = " " + boneName.ToLowerInvariant().Replace('_', ' ').Replace('.', ' ') + " ";
            if (n.Contains("left") || n.Contains(" l ")) return -1;
            if (n.Contains("right") || n.Contains(" r ")) return 1;
            return 0;
        }

        /// <summary>뼈 자신 또는 위 뼈 이름에서 처음 찾은 좌우(끝 마디 이름에 L/R이 없는 촉수 등).</summary>
        private static int SideUp(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root; x = x.parent) { int s = SideOf(x.name); if (s != 0) return s; }
            return 0;
        }

        /// <summary>bone 위로 팔 상자 뼈가 있는지.</summary>
        private static bool UnderArm(Transform bone, List<(Transform bone, Hurtbox hurt)> made, Transform root)
        {
            for (var x = bone.parent; x != null && x != root; x = x.parent)
                foreach (var (b, h) in made) if (b == x && PartOf(b.name) == BodyPart.Arms) return true;
            return false;
        }

        private static int Depth(Transform t, Transform root)
        {
            int d = 0;
            for (var x = t; x != null && x != root; x = x.parent) d++;
            return d;
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

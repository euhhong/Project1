using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 캐릭터 모델을 임포트할 때 손에 든 무기의 위치를 바로잡는다. 원본 FBX는 그대로 두고 임포트 결과만 고친다.
    /// - 사냥터지기: 무기 메시(Gamekeeper_Weapon)에 그랩(보라색 집게 + 손잡이)과 왼 손목 사슬 팔찌가 한 메시로 붙어
    ///   왼손 뼈 아래에 있어서, 오른손 쪽에 있어야 할 그랩이 왼손을 따라 엉뚱한 곳에 떠 있었다.
    ///   → 그랩만 떼어 오른손 뼈에 붙이고, 손잡이 끝(가는 쪽)이 손바닥 가운데에 오게 옮긴다. 사슬 팔찌는 왼 손목에 그대로 둔다.
    ///   그랩 이름은 Gamekeeper_Grab_Weapon(무기 판정 Hitbox_Weapon이 그랩에 붙는다), 사슬은 Gamekeeper_Chain.
    /// - 공장장: 상어 모양 무기(HellEmber_Weapon)가 손에서 조금 떨어져 있어 손바닥 쪽으로 붙인다(FixHellEmber).
    /// - 공군: 신호총(Coordinator_Prop)이 앞뒤가 바뀌어 총구가 뒤(얼굴 쪽)를 보고 있어, 손잡이 축을 중심으로 180도 돌린다(FixCoordinator).
    /// 판정 상자(BodyPartColliderPostprocessor)보다 먼저 돈다.
    /// </summary>
    public class ModelPropFixer : AssetPostprocessor
    {
        public override uint GetVersion() => 8; // 2: 그랩을 몸 앞쪽으로 돌림, 3: 공장장 상어 무기를 손바닥에 붙임, 4: 공군 신호총 앞뒤를 돌림
        public override int GetPostprocessOrder() => -5;

        private void OnPostprocessModel(GameObject root)
        {
            string path = assetPath.Replace('\\', '/');
            var importer = assetImporter as ModelImporter;
            if (importer == null) return;
            if (path.Contains("/Hunter_HellEmber/")) { FixHellEmber(root, importer); return; }
            if (path.Contains("/Survivor_Coordinator/")) { FixCoordinator(root, importer); return; }
            if (!path.Contains("/Hunter_Gamekeeper/")) return;
            var weapon = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Gamekeeper_Weapon");
            if (weapon == null) return;
            var rightHand = HumanBone(root, importer, "RightHand");
            var rightMiddle = HumanBone(root, importer, "RightMiddleProximal");
            var rightLower = HumanBone(root, importer, "RightLowerArm");
            if (rightHand == null) return;

            var src = weapon.GetComponent<MeshFilter>().sharedMesh;
            var v = src.vertices;
            // 두 조각은 메시 로컬 x로 갈린다(가운데가 비어 있다): x < 0 = 그랩, x >= 0 = 사슬 팔찌.
            bool IsGrab(int i) => v[i].x < 0f;
            var grabMesh = Extract(src, IsGrab, "Gamekeeper_Grab");
            var chainMesh = Extract(src, i => !IsGrab(i), "Gamekeeper_Chain");
            context.AddObjectToAsset("Gamekeeper_Grab", grabMesh);
            context.AddObjectToAsset("Gamekeeper_Chain", chainMesh);

            // 사슬: 원래 자리(왼손 뼈 아래) 그대로, 메시만 사슬 부분으로.
            weapon.GetComponent<MeshFilter>().sharedMesh = chainMesh;
            weapon.name = "Gamekeeper_Chain";

            // 그랩: 같은 월드 자리에서 시작해 오른손 뼈로 옮긴다.
            var grab = new GameObject("Gamekeeper_Grab_Weapon");
            grab.transform.SetPositionAndRotation(weapon.transform.position, weapon.transform.rotation);
            grab.transform.localScale = weapon.transform.lossyScale;
            grab.AddComponent<MeshFilter>().sharedMesh = grabMesh;
            grab.AddComponent<MeshRenderer>().sharedMaterials = weapon.sharedMaterials;
            grab.transform.SetParent(rightHand, true);

            // 손잡이 끝 = 그랩의 긴 축 양 끝 중 단면이 가는 쪽. 그 점을 손바닥 가운데로 옮기고,
            // 손잡이 → 집게 머리 방향이 몸 앞쪽(+Z)을 보게 돌린다(주먹에서 앞으로 뻗어 쥔 모양, 스킬 때 그대로 앞으로 뻗어 나간다).
            var world = grabMesh.vertices.Select(p => grab.transform.TransformPoint(p)).ToArray();
            Vector3 handleEnd = HandleEnd(world, out Vector3 headEnd);
            Vector3 fingers = rightMiddle != null ? rightMiddle.position - rightHand.position
                : rightLower != null ? (rightHand.position - rightLower.position) * 0.3f : Vector3.zero;
            Vector3 palm = rightHand.position + fingers * 0.5f;
            var turn = Quaternion.FromToRotation(headEnd - handleEnd, root.transform.forward);
            grab.transform.rotation = turn * grab.transform.rotation;
            handleEnd = turn * (handleEnd - grab.transform.position) + grab.transform.position;
            grab.transform.position += palm - handleEnd;
            Debug.Log($"[FE] 사냥터지기 그랩을 오른손으로 옮겼습니다(손잡이 끝 → 손바닥, {(palm - handleEnd).magnitude:0.000} 이동).");
        }

        /// <summary>
        /// 공장장: 상어 모양 무기(HellEmber_Weapon, 오른손 뼈 아래)가 손에서 조금 떨어져 있었다.
        /// → 무기의 긴 축이 손바닥 가운데를 지나도록 축에 수직인 방향으로만 옮긴다(쥔 자리·방향은 그대로, 주먹이 몸통을 감싼다).
        /// </summary>
        private static void FixHellEmber(GameObject root, ModelImporter importer)
        {
            var weapon = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "HellEmber_Weapon");
            var rightHand = HumanBone(root, importer, "RightHand");
            if (weapon == null || rightHand == null) return;
            var rightMiddle = HumanBone(root, importer, "RightMiddleProximal");
            var rightLower = HumanBone(root, importer, "RightLowerArm");
            Vector3 fingers = rightMiddle != null ? rightMiddle.position - rightHand.position
                : rightLower != null ? (rightHand.position - rightLower.position) * 0.3f : Vector3.zero;
            Vector3 palm = rightHand.position + fingers * 0.5f;

            var world = weapon.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => weapon.transform.TransformPoint(p)).ToArray();
            Vector3 handleEnd = HandleEnd(world, out Vector3 headEnd);
            Vector3 axis = (headEnd - handleEnd).normalized;
            Vector3 onAxis = handleEnd + axis * Vector3.Dot(palm - handleEnd, axis);
            Vector3 move = palm - onAxis;
            weapon.transform.position += move;
            Debug.Log($"[FE] 공장장 상어 무기를 손바닥 쪽으로 {move.magnitude:0.000} 옮겼습니다(긴 축이 손바닥 가운데를 지나게).");
        }

        /// <summary>
        /// 공군: 신호총(Coordinator_Prop, 오른손 뼈 아래)의 총구가 뒤를 보고 있었다(쏘면 노리쇠 쪽에서 나가는 것처럼 보임).
        /// → 손바닥을 지나는 세로축(쏘는 자세에서의 위쪽)으로 180도 돌려 총열을 앞으로, 이어 위에서 봐도 곧게(총열 = 몸 앞쪽) 손잡이가 똑바로 아래를 보게 세운다.
        /// </summary>
        private const float BarrelPitchUp = 25f; // 공군 신호총 총열을 수평으로 들어 올리는 각도(도)

        private static void FixCoordinator(GameObject root, ModelImporter importer)
        {
            var gun = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Coordinator_Prop");
            var rightHand = HumanBone(root, importer, "RightHand");
            if (gun == null || rightHand == null) return;
            var rightMiddle = HumanBone(root, importer, "RightMiddleProximal");
            var rightLower = HumanBone(root, importer, "RightLowerArm");
            Vector3 fingers = rightMiddle != null ? rightMiddle.position - rightHand.position
                : rightLower != null ? (rightHand.position - rightLower.position) * 0.3f : Vector3.zero;
            Vector3 palm = rightHand.position + fingers * 0.5f;

            // 쏘는 자세(Skill_Coordinator_Flare, 쏘는 순간)에서 잰 "월드 위쪽"·"몸 앞쪽"을 오른손 뼈 기준으로 나타낸 방향.
            Vector3 up = rightHand.TransformDirection(new Vector3(-0.629f, 0.552f, 0.548f)).normalized;
            Vector3 fwd = rightHand.TransformDirection(new Vector3(-0.265f, 0.510f, -0.818f)).normalized;
            // 1) 위쪽 축으로 180도: 총열이 뒤 → 앞(손잡이는 아래 그대로).
            gun.transform.RotateAround(palm, up, 180f);
            // 2) 똑바로 세우기: 총열(긴 축, 총구 쪽)이 몸 앞쪽과 나란하게(위에서 봐도 곧게), 손잡이(총열 → 손바닥)가 똑바로 아래를 보게. 손바닥을 중심으로 돌린다.
            var world = gun.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => gun.transform.TransformPoint(p)).ToArray();
            Vector3 a = HandleEnd(world, out Vector3 b);
            Vector3 barrel = (b - a).normalized;
            if (Vector3.Dot(barrel, fwd) < 0f) barrel = -barrel;
            Vector3 mean = Vector3.zero; foreach (var p in world) mean += p; mean /= world.Length;
            Vector3 down = Vector3.ProjectOnPlane(palm - mean, barrel);
            down = down.sqrMagnitude > 1e-10f ? down.normalized : -up;
            Quaternion align = Quaternion.LookRotation(Vector3.ProjectOnPlane(fwd, up).normalized, up) * Quaternion.Inverse(Quaternion.LookRotation(barrel, -down));
            gun.transform.SetPositionAndRotation(palm + align * (gun.transform.position - palm), align * gun.transform.rotation);
            // 3) 긴 축에 손잡이가 섞여 총열이 앞으로 25도쯤 숙여 있었다 → 몸 오른쪽 축으로 들어 올려 총열을 수평으로(옆에서 봐도 곧게).
            gun.transform.RotateAround(palm, Vector3.Cross(up, fwd).normalized, -BarrelPitchUp);

            // 총구 자리: 몸 앞쪽으로 가장 멀리 나온 정점들(총열 끝, 나팔처럼 벌어진 입구)의 가운데.
            // 빈 자식 Coordinator_Muzzle로 둔다 — FE(FlareShotVisual)가 신호탄을 여기서 내보낸다.
            world = gun.GetComponent<MeshFilter>().sharedMesh.vertices.Select(p => gun.transform.TransformPoint(p)).ToArray();
            float far = world.Max(p => Vector3.Dot(p, fwd)), near = world.Min(p => Vector3.Dot(p, fwd));
            var tip = world.Where(p => Vector3.Dot(p, fwd) >= far - (far - near) * 0.04f).ToArray();
            Vector3 muzzle = Vector3.zero; foreach (var p in tip) muzzle += p; muzzle /= tip.Length;
            var mz = new GameObject("Coordinator_Muzzle").transform;
            mz.SetParent(gun.transform, false);
            mz.position = muzzle;
            mz.rotation = Quaternion.LookRotation(fwd, up);
            Debug.Log($"[FE] 공군 신호총을 손잡이 축으로 180도 돌렸습니다(총구가 앞을 보게).");
        }

        private static Transform HumanBone(GameObject root, ModelImporter importer, string humanName)
        {
            var map = importer.humanDescription.human.FirstOrDefault(h => h.humanName == humanName);
            if (string.IsNullOrEmpty(map.boneName)) return null;
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == map.boneName);
        }

        /// <summary>정점 집합의 긴 축(주성분) 양 끝 8% 중 단면이 더 가는 쪽의 끝점(손잡이). 반대쪽 끝(머리)도 돌려준다.</summary>
        private static Vector3 HandleEnd(Vector3[] pts, out Vector3 other)
        {
            Vector3 mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= pts.Length;
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p0 in pts) { var p = p0 - mean; xx += p.x * p.x; xy += p.x * p.y; xz += p.x * p.z; yy += p.y * p.y; yz += p.y * p.z; zz += p.z * p.z; }
            Vector3 axis = new Vector3(1f, 0.7f, 0.3f).normalized;
            for (int i = 0; i < 60; i++)
                axis = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z, xz * axis.x + yz * axis.y + zz * axis.z).normalized;
            float min = pts.Min(p => Vector3.Dot(p - mean, axis)), max = pts.Max(p => Vector3.Dot(p - mean, axis));
            float len = max - min;
            (float spread, Vector3 tip) End(bool low)
            {
                var sel = pts.Where(p => { float t = (Vector3.Dot(p - mean, axis) - min) / len; return low ? t <= 0.08f : t >= 0.92f; }).ToArray();
                Vector3 c = Vector3.zero; foreach (var p in sel) c += p; c /= sel.Length;
                float spread = sel.Max(p => Vector3.ProjectOnPlane(p - c, axis).magnitude);
                Vector3 tip = mean + axis * (low ? min : max);
                return (spread, Vector3.ProjectOnPlane(c - mean, axis) + tip); // 축 위의 끝점을 단면 가운데로 옮김
            }
            var a = End(true); var b = End(false);
            other = a.spread < b.spread ? b.tip : a.tip;
            return a.spread < b.spread ? a.tip : b.tip;
        }

        /// <summary>조건에 맞는 정점이 쓰는 삼각형만 남긴 새 메시(정점 번호를 다시 매긴다).</summary>
        private static Mesh Extract(Mesh src, System.Func<int, bool> keep, string name)
        {
            var map = new Dictionary<int, int>();
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tangents = new List<Vector4>();
            var sv = src.vertices; var sn = src.normals; var su = src.uv; var st = src.tangents;
            var mesh = new Mesh { name = name, subMeshCount = src.subMeshCount };
            var subs = new List<int>[src.subMeshCount];
            for (int s = 0; s < src.subMeshCount; s++)
            {
                subs[s] = new List<int>();
                var tris = src.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (!keep(tris[i])) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int old = tris[i + k];
                        if (!map.TryGetValue(old, out int idx))
                        {
                            idx = verts.Count; map[old] = idx;
                            verts.Add(sv[old]);
                            if (sn.Length == sv.Length) normals.Add(sn[old]);
                            if (su.Length == sv.Length) uvs.Add(su[old]);
                            if (st.Length == sv.Length) tangents.Add(st[old]);
                        }
                        subs[s].Add(idx);
                    }
                }
            }
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            if (uvs.Count == verts.Count) mesh.SetUVs(0, uvs);
            if (tangents.Count == verts.Count) mesh.SetTangents(tangents);
            for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

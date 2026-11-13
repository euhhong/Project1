using System.Collections.Generic;
using System.Linq;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩 쏘기 연출. 그랩은 물고기 모양: 꼬리(손잡이)는 손에 남고 물고기 몸통·머리가 사슬에 이어져 곧게 날아가 문다.
    /// - 그랩 메시를 실행 중에 나눈다: 꼬리(긴 축 0~40%) / 몸통(40~62%) / 위·아래 턱(62~100%, 좌우로 갈라진 두 쪽).
    /// - 스킬이 그랩을 뻗으면(ICharacterSkillState.Reach &gt; 0) 몸통·턱을 꼬리 끝에서 Direction 쪽으로 Reach만큼 옮기고(머리가 앞을 보게),
    ///   꼬리 끝과 몸통 사이를 사슬 고리로 잇는다. 날아가는 동안 입을 크게 벌리고, 다 뻗으면(되감기 시작) 콱 문다.
    /// - 다 되감기면 원래 자리로 돌려놓는다. 연출 전용(판정은 BE GrabShotSkill).
    /// 사슬 고리는 지금 손목 사슬 모델(Gamekeeper_Chain)을 쓴다(VARCO 3D 사슬 모델로 바꿀 예정).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 사냥터지기만).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabShotVisual : MonoBehaviour
    {
        private const float TailEnd = 0.40f, JawStart = 0.62f;

        [Tooltip("사슬 고리 간격(m).")]
        [SerializeField, Min(0.05f)] private float linkSpacing = 0.28f;
        [Tooltip("줄기 사슬 크기(손목 팔찌 대비).")]
        [SerializeField, Min(0.05f)] private float linkScale = 0.55f;
        [Tooltip("날아갈 때 입을 더 벌리는 각도 / 물 때 닫는 각도(도).")]
        [SerializeField] private float jawOpenAngle = 18f, jawBiteAngle = 22f;
        [Tooltip("입을 여닫는 속도(도/초).")]
        [SerializeField, Min(1f)] private float jawSpeed = 600f;

        private ICharacterSkillState skill;
        private Animator animator;
        private Transform grab, hand;
        private MeshFilter chainSource;
        private Transform fish, jawA, jawB;           // 날아가는 몸통 기준점(자르는 곳), 두 턱의 경첩
        private Vector3 fishLocalPos; private Quaternion fishLocalRot;
        private Vector3 axisLocal, hingeAxisLocal;    // 꼬리 → 머리 방향, 턱 경첩 축(그랩 로컬)
        private float jaw;                            // 지금 턱 각도(+ = 벌림, - = 닫힘)
        private float lastReach;
        private bool shooting;
        private readonly List<Transform> links = new();
        private Transform linkRoot;
        private Vector3 linkCenterLocal; private Vector3 linkAxisLocal = Vector3.forward;

        private void Awake() => skill = GetComponent<ICharacterSkillState>();

        private void OnDestroy()
        {
            Restore();
            if (linkRoot != null) Destroy(linkRoot.gameObject);
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<ICharacterSkillState>();
            if (skill == null || !Resolve()) return;
            float reach = skill.Reach;
            if (reach <= 0.01f)
            {
                if (shooting) Restore();
                lastReach = 0f;
                return;
            }

            shooting = true;
            Vector3 dir = skill.Direction.sqrMagnitude > 0.01f ? skill.Direction.normalized : transform.forward;
            // 몸통: 자르는 곳이 (꼬리 끝 + dir * reach)에, 머리가 dir을 보게.
            Vector3 tailEnd = grab.TransformPoint(fishLocalPos);
            Quaternion inHand = grab.rotation * fishLocalRot;
            Vector3 headNow = grab.TransformDirection(axisLocal);
            fish.rotation = Quaternion.FromToRotation(headNow, dir) * inHand;
            fish.position = tailEnd + dir * reach;

            // 턱: 뻗는 중엔 크게 벌리고, 되감기 시작(길이가 줄기 시작)하면 문다.
            float target = reach >= lastReach - 0.0001f ? jawOpenAngle : -jawBiteAngle;
            jaw = Mathf.MoveTowards(jaw, target, jawSpeed * Time.deltaTime);
            SetJaw(jaw);
            lastReach = reach;

            // 사슬 줄기: 꼬리 끝 → 몸통 사이.
            EnsureLinks(Mathf.CeilToInt(reach / linkSpacing));
            for (int i = 0; i < links.Count; i++)
            {
                float d = (i + 0.5f) * linkSpacing;
                bool on = d < reach;
                links[i].gameObject.SetActive(on);
                if (!on) continue;
                Quaternion r = Quaternion.AngleAxis(i * 90f, dir) * Quaternion.FromToRotation(linkAxisLocal, dir);
                links[i].rotation = r;
                links[i].position = tailEnd + dir * d - r * (linkCenterLocal * links[i].localScale.x);
            }
        }

        private void SetJaw(float angle)
        {
            Vector3 hinge = hingeAxisLocal;
            jawA.localRotation = Quaternion.AngleAxis(angle, hinge);   // 턱 A(+옆): + 각도 = 벌어짐
            jawB.localRotation = Quaternion.AngleAxis(-angle, hinge);  // 턱 B(-옆): 반대로
        }

        private void Restore()
        {
            if (fish != null) { fish.localPosition = fishLocalPos; fish.localRotation = fishLocalRot; }
            if (jawA != null) { jaw = 0f; SetJaw(0f); }
            foreach (var l in links) if (l != null) l.gameObject.SetActive(false);
            shooting = false;
        }

        // ---------- 준비: 그랩 메시를 꼬리 / 몸통 / 두 턱으로 나눈다 ----------

        private bool Resolve()
        {
            if (animator != null && animator.isActiveAndEnabled && grab != null && fish != null) return true;
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var renderers = animator.GetComponentsInChildren<MeshRenderer>(true);
            var g = renderers.FirstOrDefault(r => r.name == "Gamekeeper_Grab_Weapon");
            var c = renderers.FirstOrDefault(r => r.name == "Gamekeeper_Chain");
            if (g == null || hand == null) return false;
            grab = g.transform;
            chainSource = c != null ? c.GetComponent<MeshFilter>() : null;
            Split(g);
            shooting = false; jaw = 0f;
            return true;
        }

        private void Split(MeshRenderer g)
        {
            var mesh = g.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices;
            Vector3 mean = Mean(v);
            Vector3 axis = Principal(v, mean);
            float min = v.Min(p => Vector3.Dot(p - mean, axis)), max = v.Max(p => Vector3.Dot(p - mean, axis));
            // 손잡이(꼬리)는 가는 쪽. 꼬리 → 머리를 +로 맞춘다.
            if (EndSpread(v, mean, axis, min, max, true) > EndSpread(v, mean, axis, min, max, false)) { axis = -axis; (min, max) = (-max, -min); }
            float len = max - min;
            float T(Vector3 p) => (Vector3.Dot(p - mean, axis) - min) / len;
            // 턱이 갈라지는 방향: 턱 부분 정점의 축에 수직인 성분 중 가장 넓게 퍼진 방향.
            var jawPts = v.Where(p => T(p) >= JawStart).Select(p => Vector3.ProjectOnPlane(p - mean, axis)).ToArray();
            Vector3 side = Principal(jawPts, Vector3.zero);
            Vector3 jawMid = Mean(v.Where(p => T(p) >= JawStart).ToArray());
            float S(Vector3 p) => Vector3.Dot(Vector3.ProjectOnPlane(p - jawMid, axis), side);

            axisLocal = axis;
            hingeAxisLocal = Vector3.Cross(axis, side).normalized;
            Vector3 cut = mean + axis * (min + TailEnd * len);       // 꼬리/몸통 자르는 곳
            Vector3 hingePt = mean + axis * (min + JawStart * len);  // 턱 경첩

            int Part(int a, int b, int cc)
            {
                Vector3 m = (v[a] + v[b] + v[cc]) / 3f;
                float t = T(m);
                if (t < TailEnd) return 0;
                if (t < JawStart) return 1;
                return S(m) >= 0f ? 2 : 3;
            }
            var meshes = new Mesh[4];
            for (int k = 0; k < 4; k++) meshes[k] = Extract(mesh, Part, k);

            // 꼬리는 그랩 자리에 그대로, 몸통 기준점(fish)은 자르는 곳. 턱 두 쪽은 경첩 기준.
            var mats = g.sharedMaterials;
            g.enabled = false;
            var tail = Piece("Grab_Tail", grab, meshes[0], mats, Vector3.zero);
            fish = new GameObject("Grab_Fish").transform;
            fish.SetParent(grab, false); fish.localPosition = cut;
            fishLocalPos = cut; fishLocalRot = Quaternion.identity;
            Piece("Grab_Body", fish, meshes[1], mats, -cut);
            jawA = new GameObject("Grab_JawA").transform; jawA.SetParent(fish, false); jawA.localPosition = hingePt - cut;
            jawB = new GameObject("Grab_JawB").transform; jawB.SetParent(fish, false); jawB.localPosition = hingePt - cut;
            Piece("Mesh", jawA, meshes[2], mats, -hingePt);
            Piece("Mesh", jawB, meshes[3], mats, -hingePt);
        }

        private static Transform Piece(string name, Transform parent, Mesh mesh, Material[] mats, Vector3 meshOffset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = meshOffset;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            return go.transform;
        }

        private static Mesh Extract(Mesh src, System.Func<int, int, int, int> part, int which)
        {
            var map = new Dictionary<int, int>();
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>();
            var sv = src.vertices; var sn = src.normals; var su = src.uv;
            var mesh = new Mesh { name = "Grab_" + which, subMeshCount = src.subMeshCount };
            var subs = new List<int>[src.subMeshCount];
            for (int s = 0; s < src.subMeshCount; s++)
            {
                subs[s] = new List<int>();
                var tris = src.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (part(tris[i], tris[i + 1], tris[i + 2]) != which) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int old = tris[i + k];
                        if (!map.TryGetValue(old, out int idx))
                        {
                            idx = verts.Count; map[old] = idx; verts.Add(sv[old]);
                            if (sn.Length == sv.Length) normals.Add(sn[old]);
                            if (su.Length == sv.Length) uvs.Add(su[old]);
                        }
                        subs[s].Add(idx);
                    }
                }
            }
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            if (uvs.Count == verts.Count) mesh.SetUVs(0, uvs);
            for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Mean(Vector3[] pts) { Vector3 m = Vector3.zero; foreach (var p in pts) m += p; return pts.Length > 0 ? m / pts.Length : m; }

        private static Vector3 Principal(Vector3[] pts, Vector3 mean)
        {
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p0 in pts) { var p = p0 - mean; xx += p.x * p.x; xy += p.x * p.y; xz += p.x * p.z; yy += p.y * p.y; yz += p.y * p.z; zz += p.z * p.z; }
            Vector3 a = new Vector3(1f, 0.7f, 0.3f).normalized;
            for (int i = 0; i < 60; i++) a = new Vector3(xx * a.x + xy * a.y + xz * a.z, xy * a.x + yy * a.y + yz * a.z, xz * a.x + yz * a.y + zz * a.z).normalized;
            return a;
        }

        private static float EndSpread(Vector3[] v, Vector3 mean, Vector3 axis, float min, float max, bool low)
        {
            float len = max - min;
            var sel = v.Where(p => { float t = (Vector3.Dot(p - mean, axis) - min) / len; return low ? t <= 0.08f : t >= 0.92f; }).ToArray();
            Vector3 c = Mean(sel);
            return sel.Length == 0 ? 0f : sel.Max(p => Vector3.ProjectOnPlane(p - c, axis).magnitude);
        }

        private void EnsureLinks(int count)
        {
            if (chainSource == null) return;
            if (linkRoot == null) linkRoot = new GameObject("GrabChain").transform;
            var mr = chainSource.GetComponent<MeshRenderer>();
            if (links.Count == 0)
            {
                var b = chainSource.sharedMesh.bounds;
                linkCenterLocal = b.center;
                var s = b.size;
                linkAxisLocal = s.x <= s.y && s.x <= s.z ? Vector3.right : s.y <= s.z ? Vector3.up : Vector3.forward;
            }
            float worldScale = chainSource.transform.lossyScale.x * linkScale;
            while (links.Count < count)
            {
                var go = new GameObject("Link");
                go.transform.SetParent(linkRoot, false);
                go.transform.localScale = Vector3.one * worldScale;
                go.AddComponent<MeshFilter>().sharedMesh = chainSource.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mr.sharedMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                links.Add(go.transform);
            }
        }
    }
}

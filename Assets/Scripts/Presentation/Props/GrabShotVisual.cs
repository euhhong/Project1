using System.Collections.Generic;
using System.Linq;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사냥터지기 그랩 쏘기 연출. 스킬이 그랩을 뻗으면(ICharacterSkillState.Reach &gt; 0)
    /// - 오른손에 쥔 그랩 모델(Gamekeeper_Grab_Weapon)을 손에서 Direction 쪽으로 Reach만큼 떨어진 곳으로 옮기고(집게 머리가 앞을 보게),
    /// - 손과 그랩 사이를 왼 손목 사슬 모델(Gamekeeper_Chain)을 여러 개 이어 붙여 줄기로 그린다.
    /// 다 되감기면 그랩을 손에 원래대로 돌려놓고 사슬 줄기를 숨긴다. 연출 전용(판정은 BE GrabShotSkill).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 사냥터지기만).
    /// </summary>
    [DisallowMultipleComponent]
    public class GrabShotVisual : MonoBehaviour
    {
        [Tooltip("사슬 고리 간격(m).")]
        [SerializeField, Min(0.05f)] private float linkSpacing = 0.28f;
        [Tooltip("줄기 사슬 크기(손목 팔찌 대비).")]
        [SerializeField, Min(0.05f)] private float linkScale = 0.55f;

        private ICharacterSkillState skill;
        private Animator animator;
        private Transform grab, hand;
        private MeshFilter chainSource;
        private Vector3 grabLocalPos; private Quaternion grabLocalRot;
        private Vector3 headAxisLocal = Vector3.forward, handleLocal;
        private readonly List<Transform> links = new();
        private Transform linkRoot;
        private bool out_;
        private Vector3 linkCenterLocal; // 사슬 메시 가운데(메시 기준점에서 떨어져 있다)
        private Vector3 linkAxisLocal = Vector3.forward; // 고리의 축(가장 얇은 방향)

        private void Awake() => skill = GetComponent<ICharacterSkillState>();

        private void OnDestroy()
        {
            Restore();
            if (linkRoot != null) Destroy(linkRoot.gameObject);
        }

        private bool Resolve()
        {
            if (animator != null && animator.isActiveAndEnabled && grab != null) return true;
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var renderers = animator.GetComponentsInChildren<MeshRenderer>(true);
            var g = renderers.FirstOrDefault(r => r.name == "Gamekeeper_Grab_Weapon");
            var c = renderers.FirstOrDefault(r => r.name == "Gamekeeper_Chain");
            if (g == null || hand == null) return false;
            grab = g.transform; chainSource = c != null ? c.GetComponent<MeshFilter>() : null;
            grabLocalPos = grab.localPosition; grabLocalRot = grab.localRotation;
            FindAxis(g.GetComponent<MeshFilter>().sharedMesh);
            out_ = false;
            return true;
        }

        private void LateUpdate()
        {
            if (skill == null) skill = GetComponent<ICharacterSkillState>();
            if (skill == null || !Resolve()) return;
            float reach = skill.Reach;
            if (reach <= 0.01f) { if (out_) Restore(); return; }

            out_ = true;
            Vector3 dir = skill.Direction.sqrMagnitude > 0.01f ? skill.Direction.normalized : transform.forward;
            // 그랩: 손잡이 끝이 (손 + dir * reach)에 오고 집게 머리가 dir을 보게.
            Quaternion rot = Quaternion.FromToRotation(grab.parent.rotation * grabLocalRot * headAxisLocal, dir) * (grab.parent.rotation * grabLocalRot);
            grab.rotation = rot;
            Vector3 handleWorldOffset = grab.TransformVector(handleLocal);
            Vector3 start = hand.position;
            grab.position = start + dir * reach - handleWorldOffset;

            // 사슬 줄기: 손 → 그랩 손잡이 사이에 고리를 일정 간격으로.
            EnsureLinks(Mathf.CeilToInt(reach / linkSpacing));
            for (int i = 0; i < links.Count; i++)
            {
                float d = (i + 0.5f) * linkSpacing;
                bool on = d < reach;
                links[i].gameObject.SetActive(on);
                if (!on) continue;
                // 고리 축을 뻗는 방향에 맞추고 고리마다 조금씩 돌린다. 메시 가운데가 줄 위에 오게 놓는다.
                Quaternion r = Quaternion.AngleAxis(i * 37f, dir) * Quaternion.FromToRotation(linkAxisLocal, dir);
                links[i].rotation = r;
                links[i].position = start + dir * d - r * (linkCenterLocal * links[i].localScale.x);
            }
        }

        private void Restore()
        {
            if (grab != null) { grab.localPosition = grabLocalPos; grab.localRotation = grabLocalRot; }
            foreach (var l in links) if (l != null) l.gameObject.SetActive(false);
            out_ = false;
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
            // 팔찌 메시의 크기(월드)를 기준으로 고리 크기를 맞춘다.
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

        /// <summary>그랩 메시의 긴 축에서 손잡이 끝(가는 쪽)과 머리 방향을 찾는다(메시 로컬).</summary>
        private void FindAxis(Mesh mesh)
        {
            var pts = mesh.vertices;
            if (pts.Length == 0) return;
            Vector3 mean = Vector3.zero; foreach (var p in pts) mean += p; mean /= pts.Length;
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (var p0 in pts) { var p = p0 - mean; xx += p.x * p.x; xy += p.x * p.y; xz += p.x * p.z; yy += p.y * p.y; yz += p.y * p.z; zz += p.z * p.z; }
            Vector3 axis = new Vector3(1f, 0.7f, 0.3f).normalized;
            for (int i = 0; i < 60; i++)
                axis = new Vector3(xx * axis.x + xy * axis.y + xz * axis.z, xy * axis.x + yy * axis.y + yz * axis.z, xz * axis.x + yz * axis.y + zz * axis.z).normalized;
            float min = pts.Min(p => Vector3.Dot(p - mean, axis)), max = pts.Max(p => Vector3.Dot(p - mean, axis));
            float len = max - min;
            float Spread(bool low)
            {
                var sel = pts.Where(p => { float t = (Vector3.Dot(p - mean, axis) - min) / len; return low ? t <= 0.08f : t >= 0.92f; }).ToArray();
                Vector3 c = Vector3.zero; foreach (var p in sel) c += p; c /= sel.Length;
                return sel.Max(p => Vector3.ProjectOnPlane(p - c, axis).magnitude);
            }
            bool handleLow = Spread(true) < Spread(false);
            headAxisLocal = handleLow ? axis : -axis;
            handleLocal = mean + axis * (handleLow ? min : max);
        }
    }
}

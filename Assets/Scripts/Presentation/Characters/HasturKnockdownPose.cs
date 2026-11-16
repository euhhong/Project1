using System.Collections.Generic;
using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 노란옷의 왕이 헤드샷 날아 차기에 맞아 나가떨어질 때(HitResult.Knockdown) 연출(전용 — Generic 리그라 사람 모션 대신 런타임으로 뼈를 돌린다).
    /// - 날아감: 공중으로 떠올라(floatHeight) 맞은 쪽으로 몸이 젖혀지고(flightTilt), 촉수 다리들은 한데 모여 꽈배기처럼 꼬이며(braidTwist),
    ///   두 팔은 앞(때린 쪽)으로 뻗는다. 기절하는 동안 공중에 뜬 채 살짝 흔들린다.
    /// - 일어남(기절이 getUpTime초 남았을 때부터): 팔을 뒤로 뻗어 밀고(pushTime 비율), 용수철 튀기듯 바로 선다 — 넘어서 앞으로 조금 쏠렸다가 되돌아오고(감쇠 진동), 높이도 한 번 튀었다가 내려앉으며 꼬인 다리가 풀린다.
    /// 시간은 BE 기절(CharacterStun) 남은 시간을 따른다(FollowUpKickStun: 밀려남 0.5초 + 기절, 합 1.5초).
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 노란옷의 왕만).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)] // 다리 흔들림(HasturLegWriggle — 매 프레임 쉬는 자세로 되돌림)·걷기 팔 뒤에: 꽈배기·팔 자세가 그 위에 덮인다
    public class HasturKnockdownPose : MonoBehaviour
    {
        [Header("날아감·떠 있음")]
        [SerializeField, Min(0f)] private float floatHeight = 0.9f;
        [SerializeField, Min(0.01f)] private float riseTime = 0.3f;
        [Tooltip("맞은 쪽으로 몸이 젖혀지는 각도(도).")]
        [SerializeField, Range(0f, 80f)] private float flightTilt = 40f;
        [Tooltip("떠 있는 동안 위아래 흔들림(m) / 빠르기(Hz).")]
        [SerializeField, Min(0f)] private float hoverBob = 0.06f, hoverHz = 1.6f;
        [Header("꽈배기 다리")]
        [Tooltip("촉수 다리 마디마다 더 꼬는 각도(도) / 다리끼리 모이는 정도(작을수록 한데 붙음).")]
        [SerializeField] private float braidTwist = 100f;
        [SerializeField, Range(0f, 1f)] private float braidSpread = 0.35f;
        [Header("일어남")]
        [SerializeField, Min(0.05f)] private float getUpTime = 0.45f;
        [Tooltip("일어남 중 팔을 뒤로 뻗어 미는 앞부분 비율(0~1). 나머지는 용수철처럼 튀어 선다.")]
        [SerializeField, Range(0.05f, 0.9f)] private float pushTime = 0.35f;
        [Tooltip("용수철: 감쇠(클수록 빨리 멈춤) / 진동 빠르기(rad/s) / 다 선 뒤 더 흔들리는 시간(초).")]
        [SerializeField, Min(0f)] private float springDamping = 7f, springFrequency = 18f;
        [SerializeField, Min(0f)] private float settleTime = 0.35f;

        private static readonly string[] ArmNames = { "biped R UpperArm", "biped R Forearm", "biped R Hand", "biped L UpperArm", "biped L Forearm", "biped L Hand" };

        private class Bone { public Transform T; }
        private class Chain { public List<Bone> Bones = new(); public float Angle; } // Angle: 몸 둘레 어느 쪽 다리인지(위에서 본 각도)

        private CharacterIdentity identity;
        private CharacterStun stun;
        private Animator animator;
        private Transform skeletonRoot; private Vector3 rootBasePos, rootWrittenPos; private bool rootHas;
        private Bone pelvis;
        private Bone[] arms;
        private readonly List<Chain> legs = new();

        private bool active;
        private float time, total, getUpStart = -1f, springStart = -1f;
        private Vector3 knockDir = Vector3.back;

        private void OnEnable() => HitEvents.HitOccurred += OnHit;
        private void OnDisable() { HitEvents.HitOccurred -= OnHit; Restore(); active = false; }

        private void OnHit(HitResult hit)
        {
            if (!hit.Knockdown) return;
            if (identity == null) TryGetComponent(out identity);
            if (identity == null || hit.VictimId != identity.Id) return;
            if (stun == null) TryGetComponent(out stun);
            Vector3 k = Vector3.ProjectOnPlane(hit.Knockback, Vector3.up);
            Begin(k.sqrMagnitude > 1e-4f ? k.normalized : -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized,
                  stun != null && stun.IsStunned ? stun.StunRemaining : 1.5f);
        }

        private void Begin(Vector3 dir, float duration)
        {
            knockDir = dir; total = Mathf.Max(duration, getUpTime + 0.1f);
            time = 0f; getUpStart = -1f; springStart = -1f; active = true;
        }

        private void LateUpdate()
        {
            if (!active) return;
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); Resolve(); }
            if (pelvis == null) return;
            time += Time.deltaTime;
            // 일어남 시작: BE 기절이 getUpTime초 남았을 때(기절이 없으면 정한 총 시간 기준)
            float remaining = stun != null && stun.IsStunned ? stun.StunRemaining : Mathf.Max(0f, total - time);
            if (getUpStart < 0f && remaining <= getUpTime) getUpStart = time;
            Pose(time, getUpStart);
            if (springStart >= 0f && time - springStart > (1f - pushTime) * getUpTime + settleTime) { Restore(); active = false; }
        }

        /// <summary>에디터 미리보기용: 나가떨어진 뒤 t초 자세(일어남은 getUpAt초부터, 음수면 아직 떠 있음).</summary>
        public void PreviewPose(float t, float getUpAt, Vector3 dir)
        {
            Resolve();
            if (pelvis == null) return;
            knockDir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            springStart = -1f;
            Pose(t, getUpAt);
        }

        private void Pose(float t, float getUpAt)
        {
            Rebase();
            // 1) 세기: 날아감(떠오름) → 일어남(팔로 밀기 → 용수철)
            float rise = Mathf.SmoothStep(0f, 1f, t / riseTime);
            float knocked = rise;        // 젖힘·꽈배기·팔 앞으로(1 = 다 됨)
            float height = floatHeight * rise + hoverBob * Mathf.Sin(t * hoverHz * Mathf.PI * 2f) * rise;
            float push = 0f;             // 팔을 뒤로 뻗어 밀기(1 = 다 뻗음)
            if (getUpAt >= 0f && t >= getUpAt)
            {
                float g = (t - getUpAt) / getUpTime;
                if (g < pushTime)
                {
                    float p = Mathf.SmoothStep(0f, 1f, g / pushTime);
                    push = p;
                    height -= 0.15f * p; // 밀기 전 살짝 웅크림
                }
                else
                {
                    if (springStart < 0f) springStart = t;
                    float s = t - springStart;
                    float spring = Mathf.Exp(-springDamping * s) * Mathf.Cos(springFrequency * s); // 1 → 0, 넘쳐서 -로 갔다가 되돌아옴
                    knocked = spring;                                   // 젖힘은 넘어서 앞으로 조금 쏠렸다가 섬
                    push = Mathf.Max(0f, spring);
                    height = (floatHeight - 0.15f) * Mathf.Max(0f, spring) + 0.12f * Mathf.Abs(Mathf.Min(0f, spring)) * 3f; // 내려앉으며 한 번 튐
                }
            }

            // 2) 떠오름 + 젖힘(골반 기준) — 맞은 쪽(knockDir)으로 머리가 넘어간다
            if (skeletonRoot != null) skeletonRoot.position += Vector3.up * height;
            Vector3 up = Vector3.up;
            Quaternion tilt = Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.FromToRotation(up, Vector3.Slerp(up, knockDir, flightTilt / 90f)), knocked);
            pelvis.T.rotation = tilt * pelvis.T.rotation;
            Vector3 bodyUp = tilt * up, bodyFwd = tilt * -knockDir, bodyRight = Vector3.Cross(bodyUp, bodyFwd);

            // 3) 꽈배기 다리: 마디마다 조금씩 더 돌아가며 몸 아래 한가운데로 모인다
            float braid = Mathf.Clamp01(Mathf.Max(0f, knocked));
            if (braid > 0f)
            {
                foreach (var c in legs)
                    for (int j = 0; j < c.Bones.Count - 1; j++)
                    {
                        float a = (c.Angle + braidTwist * (j + 1)) * Mathf.Deg2Rad;
                        Vector3 radial = bodyRight * Mathf.Sin(a) + bodyFwd * Mathf.Cos(a);
                        Aim(c.Bones[j], c.Bones[j + 1], (-bodyUp + radial * braidSpread).normalized, braid);
                    }
            }

            // 4) 팔: 날아가는 동안 앞(때린 쪽)으로 뻗음 → 일어날 때 뒤로 뻗어 밈
            Vector3 forwardReach = (-knockDir + up * 0.15f).normalized;
            Vector3 backPush = (knockDir * 0.7f - up).normalized;
            float reach = Mathf.Clamp01(Mathf.Max(0f, knocked)) * (1f - push);
            for (int side = 0; side < 2; side++)
            {
                var upper = arms[side * 3]; var fore = arms[side * 3 + 1]; var hand = arms[side * 3 + 2];
                if (reach > 0f) { Aim(upper, fore, forwardReach, reach); Aim(fore, hand, forwardReach, reach); }
                if (push > 0f) { Aim(upper, fore, backPush, push); Aim(fore, hand, backPush, push); } // 미는 팔이 앞으로 뻗은 팔보다 우선
            }
            Mark();
        }

        private static void Aim(Bone bone, Bone child, Vector3 worldDir, float w)
        {
            if (bone == null || bone.T == null || child == null || child.T == null || w <= 0f) return;
            Vector3 cur = child.T.position - bone.T.position;
            if (cur.sqrMagnitude < 1e-8f) return;
            bone.T.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur.normalized, worldDir), w) * bone.T.rotation;
        }

        private IEnumerable<Bone> AllBones()
        {
            if (pelvis != null) yield return pelvis;
            if (arms != null) foreach (var b in arms) if (b != null) yield return b;
            foreach (var c in legs) foreach (var b in c.Bones) yield return b;
        }

        // 지난 프레임에 우리가 쓴 그대로면 모션이 안 쓰는 뼈 → 바탕 자세로 되돌린 뒤 더한다(매 프레임 쌓이지 않음)
        // 뼈 회전 바탕은 걷기 팔·다리 흔들림과 같은 뼈를 쓰므로 공용(SharedBonePose).
        private void Rebase()
        {
            foreach (var b in AllBones()) SharedBonePose.Begin(b.T);
            if (skeletonRoot != null)
            {
                if (rootHas && (skeletonRoot.localPosition - rootWrittenPos).sqrMagnitude < 1e-10f) skeletonRoot.localPosition = rootBasePos;
                else rootBasePos = skeletonRoot.localPosition;
            }
        }

        private void Mark()
        {
            foreach (var b in AllBones()) SharedBonePose.End(b.T);
            if (skeletonRoot != null) { rootWrittenPos = skeletonRoot.localPosition; rootHas = true; }
        }

        private void Restore()
        {
            foreach (var b in AllBones()) SharedBonePose.Restore(b.T);
            if (skeletonRoot != null && rootHas) { skeletonRoot.localPosition = rootBasePos; rootHas = false; }
        }

        private void Resolve()
        {
            pelvis = null; arms = null; legs.Clear(); skeletonRoot = null; rootHas = false;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            var all = animator.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
            skeletonRoot = Find("biped");
            var p = Find("biped Pelvis");
            if (p == null) return;
            pelvis = new Bone { T = p };
            arms = new Bone[ArmNames.Length];
            for (int i = 0; i < ArmNames.Length; i++) { var t = Find(ArmNames[i]); arms[i] = t != null ? new Bone { T = t } : null; }
            // 촉수 다리: 골반에 달린 촉수 사슬(등뼈 제외) + 다리(허벅지 → 종아리 → 발). 각 사슬은 첫 자식을 따라간다.
            var roots = new List<Transform>();
            foreach (Transform c in p) if (!c.name.Contains("Spine")) roots.Add(c);
            foreach (var n in new[] { "biped L Thigh", "biped R Thigh" }) { var t = Find(n); if (t != null) roots.Add(t); }
            Vector3 center = p.position;
            foreach (var r in roots)
            {
                var chain = new Chain();
                for (var t = r; t != null; t = t.childCount > 0 ? t.GetChild(0) : null) chain.Bones.Add(new Bone { T = t });
                if (chain.Bones.Count < 2) continue;
                Vector3 off = transform.InverseTransformDirection(chain.Bones[^1].T.position - center); // 위에서 본 다리 끝 방향(캐릭터 기준)
                chain.Angle = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
                legs.Add(chain);
            }
        }
    }
}

using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 감시자가 손에 든 무기(공장장 상어, 루키노 마체테, 사냥터지기 그랩 등)가 공격으로 휘두를 때 자기 몸을 뚫고 지나가 보이지 않게 한다(연출 전용, 공격 중에만 — 끝나면 부드럽게 원래대로).
    /// - 몸 = 캐릭터 루트의 CharacterController 캡슐(조금 여유 margin). 무기 = 쥔 곳(손)에서 양 끝까지의 긴 축.
    /// - 무기 위 점들 중 몸 속으로 가장 깊이 들어간 곳을 찾아, 쥔 곳을 중심으로 몸 밖으로 나오는 만큼만 무기를 돌린다(몇 번 되풀이).
    ///   매 프레임 원래 자리(손 아래)에서 다시 계산하고, 바뀌는 각도는 부드럽게 따라가 튀지 않게 한다.
    /// - 무기 = 손 뼈 아래 켜진 MeshRenderer(손목 사슬 _Chain, 로켓(ToyRocket, RocketWeapon이 따로 맞춤) 제외).
    ///   고유 스킬 중(예: 그랩이 날아감)에는 손대지 않는다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 감시자).
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponBodyClearance : MonoBehaviour
    {
        [Tooltip("몸 캡슐 바깥으로 더 띄울 여유(m).")]
        [SerializeField, Min(0f)] private float margin = 0.08f;
        [Tooltip("몸 반지름 배율(캡슐 반지름 대비, 몸통이 캡슐보다 굵거나 가늘 때).")]
        [SerializeField, Range(0.3f, 2f)] private float radiusScale = 1f;
        [Tooltip("돌린 각도를 따라가는 빠르기(클수록 바로). 0이면 바로.")]
        [SerializeField, Min(0f)] private float smoothing = 30f;
        [Tooltip("한 번에 돌릴 수 있는 최대 각도(도).")]
        [SerializeField, Range(5f, 120f)] private float maxAngle = 75f;

        private const int Samples = 8, Iterations = 3;

        private class Weapon
        {
            public Transform t;
            public Vector3 restPos; public Quaternion restRot;
            public Vector3 endA, endB;      // 무기 로컬: 긴 축 양 끝
            public Quaternion correction = Quaternion.identity; // 지금 적용 중인 보정(월드, 쥔 곳 중심)
        }

        private readonly List<Weapon> weapons = new();
        private Animator animator;
        private CharacterController body;
        private ICharacterSkillState skill;
        private ICharacterAttackState attack;

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled) { if (!Collect()) return; }
            if (skill == null) TryGetComponent(out skill);
            if (attack == null) TryGetComponent(out attack);
            bool swinging = attack != null && (attack.AttackState == CharacterAttackState.Light || attack.AttackState == CharacterAttackState.Heavy); // 휘두를 때만(대기·걷기 자세는 그대로)
            if (body == null) TryGetComponent(out body);
            bool skillActive = skill != null && skill.State == SkillState.Casting;
            float k = smoothing > 0f ? 1f - Mathf.Exp(-smoothing * Time.deltaTime) : 1f;

            for (int i = weapons.Count - 1; i >= 0; i--)
            {
                var w = weapons[i];
                if (w.t == null) { weapons.RemoveAt(i); continue; }
                w.t.SetLocalPositionAndRotation(w.restPos, w.restRot); // 원래 자리에서 다시 계산
                Quaternion target = skillActive || !swinging || body == null ? Quaternion.identity : Clearance(w);
                w.correction = skillActive ? Quaternion.identity : Quaternion.Slerp(w.correction, target, k); // 스킬 중(그랩이 날아감 등)에는 바로 원래대로
                if (Quaternion.Angle(w.correction, Quaternion.identity) < 0.05f) continue;
                Vector3 pivot = Pivot(w);
                w.t.SetPositionAndRotation(pivot + w.correction * (w.t.position - pivot), w.correction * w.t.rotation);
            }
        }

        /// <summary>쥔 곳(손 뼈를 무기 긴 축에 내린 점).</summary>
        private static Vector3 Pivot(Weapon w)
        {
            Vector3 a = w.t.TransformPoint(w.endA), b = w.t.TransformPoint(w.endB);
            Vector3 hand = w.t.parent.position, ab = b - a;
            float s = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(hand - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * s;
        }

        /// <summary>몸 밖으로 나오게 할 회전(월드, 쥔 곳 중심). 뚫지 않으면 identity.</summary>
        private Quaternion Clearance(Weapon w)
        {
            GetCapsule(out Vector3 c0, out Vector3 c1, out float radius);
            Vector3 pivot = Pivot(w);
            Vector3 a = w.t.TransformPoint(w.endA) - pivot, b = w.t.TransformPoint(w.endB) - pivot; // 쥔 곳 기준
            Quaternion total = Quaternion.identity;
            for (int it = 0; it < Iterations; it++)
            {
                float worst = 0f; Vector3 worstV = Vector3.zero, push = Vector3.zero;
                for (int s = 0; s <= Samples; s++)
                {
                    Vector3 v = Vector3.Lerp(a, b, s / (float)Samples);
                    Vector3 p = pivot + total * v;
                    Vector3 axisPoint = ClosestOnSegment(c0, c1, p);
                    Vector3 off = p - axisPoint;
                    float pen = radius - off.magnitude;
                    if (pen <= worst || v.sqrMagnitude < 0.0025f) continue; // 쥔 곳 바로 근처는 돌려도 거의 안 움직이므로 보지 않는다
                    worst = pen; worstV = total * v;
                    Vector3 flat = Vector3.ProjectOnPlane(off, c1 - c0);
                    push = flat.sqrMagnitude > 1e-6f ? flat.normalized : transform.right;
                }
                if (worst <= 0f) break;
                // worstV를 push 쪽으로 pen만큼 옮기는 회전(쥔 곳 중심).
                float len = worstV.magnitude;
                Vector3 axis = Vector3.Cross(worstV, push);
                if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(worstV, transform.up);
                if (axis.sqrMagnitude < 1e-8f) break;
                float angle = Mathf.Min(Mathf.Rad2Deg * Mathf.Asin(Mathf.Clamp01((worst + 0.01f) / Mathf.Max(len, 1e-3f))) * 1.15f, maxAngle);
                total = Quaternion.AngleAxis(angle, axis.normalized) * total;
                if (Quaternion.Angle(total, Quaternion.identity) >= maxAngle) break;
            }
            return total;
        }

        private void GetCapsule(out Vector3 c0, out Vector3 c1, out float radius)
        {
            Vector3 s = body.transform.lossyScale;
            float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            radius = body.radius * scale * radiusScale + margin;
            float half = Mathf.Max(body.height * Mathf.Abs(s.y) * 0.5f - body.radius * scale, 0f);
            Vector3 center = body.transform.TransformPoint(body.center);
            Vector3 up = body.transform.up;
            c0 = center - up * half; c1 = center + up * half;
        }

        private static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        /// <summary>손 뼈 아래의 무기들을 모은다(모델이 바뀌면 다시).</summary>
        private bool Collect()
        {
            foreach (var w in weapons) if (w.t != null) w.t.SetLocalPositionAndRotation(w.restPos, w.restRot);
            weapons.Clear();
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return false;
            foreach (var bone in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
            {
                var hand = animator.GetBoneTransform(bone);
                if (hand == null) continue;
                foreach (var r in hand.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.enabled || r.name.EndsWith("_Chain") || r.GetComponentInParent<ToyRocket>() != null) continue; // 감춘 원래 무기(루키노 → 마체테로 바꿈)·손목 사슬·로켓 제외
                    if (weapons.Exists(x => r.transform.IsChildOf(x.t))) continue; // 이미 모은 무기의 조각
                    Bounds b = r.localBounds;
                    Vector3 e = b.extents;
                    Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                    float half = Vector3.Dot(e, axis);
                    // 무기 루트는 손 바로 아래 자식(조각이 나뉘어도 같이 돈다)
                    Transform root = r.transform;
                    while (root.parent != null && root.parent != hand) root = root.parent;
                    if (weapons.Exists(x => x.t == root)) continue;
                    var w = new Weapon
                    {
                        t = root, restPos = root.localPosition, restRot = root.localRotation,
                        endA = root.InverseTransformPoint(r.transform.TransformPoint(b.center + axis * half)),
                        endB = root.InverseTransformPoint(r.transform.TransformPoint(b.center - axis * half)),
                    };
                    weapons.Add(w);
                }
            }
            return true;
        }

        private void OnDisable()
        {
            foreach (var w in weapons) if (w.t != null) w.t.SetLocalPositionAndRotation(w.restPos, w.restRot);
            weapons.Clear();
            animator = null;
        }
    }
}

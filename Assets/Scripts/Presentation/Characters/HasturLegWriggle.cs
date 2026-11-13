using System.Collections.Generic;
using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터 다리(치마 밑 촉수 다섯 가닥) 꿈틀거림. 모델(Hunter_Hastur)의 다리는 사람 다리가 아니라 골반 아래 촉수 뼈 사슬 다섯 개
    /// (앞 왼쪽 biped_bone_09~13, 앞 오른쪽 14~18, 뒤 19~21, 뒤 왼쪽 44·39·38·25, 뒤 오른쪽 37·36·35·29 — Blender로 확인, 가닥마다 따로 무게가 잡혀 있다)이고,
    /// 사람 모양(Humanoid) 이동 모션은 이 뼈들을 움직이지 않는다. 그래서 매 프레임 모션이 끝난 뒤 각 가닥을 밑동에서 끝으로 흐르는 물결로 굽힌다:
    /// 위아래(땅을 짚었다 듦) + 좌우. 끝으로 갈수록 크게, 가닥마다 박자를 어긋나게(지렁이처럼 차례로).
    /// 움직이는 빠르기에 따라 커지고 빨라진다(가만히 있을 때도 아주 조금). 캐릭터 루트에 붙인다(CharacterLoadout, 하스터만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturLegWriggle : MonoBehaviour
    {
        private static readonly string[] ChainRoots = { "biped_bone_09", "biped_bone_14", "biped_bone_19", "biped_bone_44", "biped_bone_37" };

        [Tooltip("움직일 때 끝 마디가 굽는 최대 각도(도) — 위아래 / 좌우.")]
        [SerializeField, Range(0f, 60f)] private float liftAngle = 16f, swayAngle = 10f;
        [Tooltip("가만히 있을 때 각도 비율.")]
        [SerializeField, Range(0f, 1f)] private float idleRatio = 0.2f;
        [Tooltip("물결 빠르기(초당 번) — 가만히 / 달릴 때.")]
        [SerializeField, Min(0f)] private float idleFrequency = 0.5f, moveFrequency = 2.2f;
        [Tooltip("마디마다 물결이 늦어지는 정도(라디안).")]
        [SerializeField, Range(0f, 3f)] private float phasePerBone = 0.9f;
        [Tooltip("이 빠르기(m/s)에서 가장 크게 꿈틀거린다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;

        private class Chain { public Transform[] Bones; public Quaternion[] Rest; public float Offset; }

        private Animator animator;
        private readonly List<Chain> chains = new();
        private ICharacterState state;
        private float phase, amount;

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            if (!TryGetComponent(out HasturArmWalk _)) gameObject.AddComponent<HasturArmWalk>(); // 걸을 때 두 촉수 팔·어깨도 움직인다
            if (!TryGetComponent(out HasturBodyBob _)) gameObject.AddComponent<HasturBodyBob>(); // 걸을 때 몸 전체가 위아래로 들썩인다
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); if (!Resolve()) return; }
            state ??= GetComponent<ICharacterState>();
            float speed = state != null ? state.LocalPlanarVelocity.magnitude : 0f;
            float move = Mathf.Clamp01(speed / fullSpeed);
            amount = Mathf.MoveTowards(amount, Mathf.Lerp(idleRatio, 1f, move), Time.deltaTime * 3f);
            phase += Time.deltaTime * Mathf.Lerp(idleFrequency, moveFrequency, move) * Mathf.PI * 2f;

            Vector3 up = transform.up;
            foreach (var c in chains)
            {
                int n = c.Bones.Length;
                for (int i = 0; i < n; i++)
                {
                    var b = c.Bones[i];
                    b.localRotation = c.Rest[i]; // 쉬는 자세에서 시작(모션이 이 뼈를 쓰지 않으므로 매번 되돌린다)
                    float weight = (i + 1f) / n;  // 끝으로 갈수록 크게
                    float w = phase - i * phasePerBone + c.Offset;
                    // 가닥 방향(이 뼈 → 다음 뼈)에 수직인 수평 축으로 들썩, 위 축으로 좌우.
                    Vector3 along = i + 1 < n ? c.Bones[i + 1].position - b.position : b.position - c.Bones[i - 1].position;
                    Vector3 side = Vector3.Cross(up, along);
                    if (side.sqrMagnitude < 1e-8f) continue;
                    side.Normalize();
                    Quaternion q = Quaternion.AngleAxis(liftAngle * amount * weight * Mathf.Sin(w), side)
                                 * Quaternion.AngleAxis(swayAngle * amount * weight * Mathf.Sin(w * 0.7f + 1.3f), up);
                    b.rotation = q * b.rotation;
                }
            }
        }

        private bool Resolve()
        {
            chains.Clear();
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return false;
            var all = animator.GetComponentsInChildren<Transform>(true);
            int k = 0;
            foreach (var rootName in ChainRoots)
            {
                Transform r = null;
                foreach (var t in all) if (t.name == rootName) { r = t; break; }
                if (r == null) continue;
                var bones = new List<Transform>();
                for (var b = r; b != null && !b.name.StartsWith("nub"); b = b.childCount > 0 ? b.GetChild(0) : null) bones.Add(b);
                if (bones.Count < 2) continue;
                var rest = new Quaternion[bones.Count];
                for (int i = 0; i < bones.Count; i++) rest[i] = bones[i].localRotation;
                chains.Add(new Chain { Bones = bones.ToArray(), Rest = rest, Offset = k++ * 2.1f });
            }
            return chains.Count > 0;
        }

        private void Restore()
        {
            foreach (var c in chains)
                for (int i = 0; i < c.Bones.Length; i++) if (c.Bones[i] != null) c.Bones[i].localRotation = c.Rest[i];
        }
    }
}

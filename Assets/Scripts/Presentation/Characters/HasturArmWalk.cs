using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터가 걸을 때 두 촉수 팔을 움직이고 어깨를 들썩인다(연출 전용).
    /// 모델(Hunter_Hastur, Generic 리그 — Blender로 확인): 팔 = 소매(biped L/R UpperArm·Forearm) 아래로 땅까지 닿는 검은 촉수 팔
    /// (왼쪽 biped_bone_00→01→02→03, 오른쪽 biped_bone_27→26→24→23, 아래팔에 붙음, 마디마다 무게가 따로 잡혀 있다). 어깨 = biped L/R Clavicle.
    /// 매 프레임 모션이 끝난 뒤:
    /// - 대시하듯 두 팔을 함께 뒤로 젖힌다(빠를수록 많이, 멈추면 부드럽게 내려옴). 걸음마다 두 팔이 같이 살짝 들썩인다.
    /// - 촉수 팔은 뒤로 끌리며 들리고(끝으로 갈수록 크게), 밑동에서 끝으로 흐르는 물결로 굽는다(끝으로 갈수록 크게, 늦게 따라옴 + 살짝 좌우).
    /// - 어깨는 걸음마다 두 어깨가 같이 들썩이고(조금 번갈아), 공격(두 손 모아 머리 위 → 내려치기)을 시작하면 이 자세는 빠르게 꺼진다.
    /// 움직이는 빠르기에 따라 커지고 빨라진다(가만히 있을 때는 아주 조금 숨 쉬듯). 공격 중에는 공격 모션을 그대로 둔다(부드럽게 꺼짐).
    /// 모션이 쓰는 뼈는 모션 자세 위에 더하고, 쓰지 않는 뼈(촉수 마디)는 쉬는 자세에서 더한다(매 프레임 쌓이지 않음).
    /// HasturLegWriggle이 붙인다(하스터만) — 하스터가 아니게 되면(HasturLegWriggle이 없어지면) 스스로 원래대로 되돌리고 없어진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturArmWalk : MonoBehaviour
    {
        private static readonly string[] LeftChain = { "biped_bone_00", "biped_bone_01", "biped_bone_02", "biped_bone_03" };
        private static readonly string[] RightChain = { "biped_bone_27", "biped_bone_26", "biped_bone_24", "biped_bone_23" };

        [Tooltip("움직일 때 두 팔을 함께 뒤로 젖히는 각도(도, 가장 빠를 때) — 위팔 / 아래팔 / 촉수 팔 끝 마디(뒤로 끌리며 들림). 대시하듯 달리는 자세.")]
        [SerializeField, Range(0f, 90f)] private float dashBack = 55f, dashForearm = 18f, dashTrail = 22f;
        [Tooltip("뒤로 젖힌 두 팔이 몸 쪽으로 붙는 각도(도) — 벌어지지 않게.")]
        [SerializeField, Range(0f, 30f)] private float dashTuck = 6f;
        [Tooltip("뒤로 젖힌 두 팔이 걸음마다 같이 들썩이는 각도(도) — 위팔 / 아래팔.")]
        [SerializeField, Range(0f, 30f)] private float upperArmSwing = 5f, forearmSwing = 4f;
        [Tooltip("뒤로 젖히는 자세가 켜지고 꺼지는 빠르기(초당 비율).")]
        [SerializeField, Min(0.1f)] private float dashBlend = 4f;
        [Tooltip("촉수 팔 끝 마디가 앞뒤로 굽는 각도(도) / 좌우로 흔들리는 각도(도).")]
        [SerializeField, Range(0f, 60f)] private float tentacleCurl = 15f, tentacleSway = 7f;
        [Tooltip("촉수 마디마다 물결이 늦어지는 정도(라디안).")]
        [SerializeField, Range(0f, 3f)] private float phasePerBone = 0.7f;
        [Tooltip("어깨 들썩임(도) — 팔을 뻗는 쪽이 번갈아 들림 / 걸음마다 두 어깨가 같이.")]
        [SerializeField, Range(0f, 40f)] private float shoulderLift = 5f, shoulderHeave = 8f;
        [Tooltip("걸음 빠르기(초당 한 쌍의 걸음) — 가만히 / 가장 빠를 때.")]
        [SerializeField, Min(0f)] private float idleFrequency = 0.35f, moveFrequency = 1.5f;
        [Tooltip("가만히 있을 때 크기 비율.")]
        [SerializeField, Range(0f, 1f)] private float idleRatio = 0.12f;
        [Tooltip("이 빠르기(m/s)에서 가장 크게 움직인다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;

        // 한 뼈: 모션이 이번 프레임에 쓴 자세(없으면 쉬는 자세)를 바탕으로 더한다.
        private class Bone
        {
            public Transform T; public Quaternion Base, Written; public bool Has;
            public void Begin()
            {
                // 지난 프레임에 우리가 쓴 그대로면 모션이 안 쓰는 뼈 → 바탕 자세로 되돌림. 바뀌었으면 모션이 새로 쓴 자세가 바탕.
                if (Has && Quaternion.Dot(T.localRotation, Written) > 0.999999f) T.localRotation = Base;
                else Base = T.localRotation;
            }
            public void End() { Written = T.localRotation; Has = true; }
            public void Restore() { if (T != null && Has) T.localRotation = Base; Has = false; }
        }

        private class Arm { public Bone Clavicle, UpperArm, Forearm; public Transform Hand; public Bone[] Chain; public float Offset, Side; } // Side: 왼쪽 -1 / 오른쪽 +1
        private HasturHangPose hang;
        private Transform[] chainTs;

        private Animator animator;
        private Arm left, right;
        private ICharacterState state;
        private ICharacterAttackState attack;
        private float phase, amount, dash; // dash: 뒤로 젖히는 자세 크기(멈추면 0, 가장 빠를 때 1)

        /// <summary>걸음 위상(라디안). 한 바퀴(2π) = 걸음 한 쌍. HasturBodyBob이 몸 들썩임 박자를 맞춘다.</summary>
        public float StepPhase => phase;

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            if (!TryGetComponent(out HasturLegWriggle _)) { Restore(); Destroy(this); return; } // 하스터가 아니게 됨
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); if (!Resolve()) return; }
            state ??= GetComponent<ICharacterState>();
            attack ??= GetComponent<ICharacterAttackState>();
            float speed = state != null ? state.LocalPlanarVelocity.magnitude : 0f;
            float move = Mathf.Clamp01(speed / fullSpeed);
            if (hang == null) TryGetComponent(out hang);
            // 공격 중·턱에 매달린 동안은 걷기 팔 움직임을 끈다(공격 모션 / 매달리기 자세가 대신).
            bool attacking = (attack != null && attack.AttackState != CharacterAttackState.None) || (hang != null && hang.Weight > 0f);
            float target = attacking ? 0f : Mathf.Lerp(idleRatio, 1f, move);
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * (attacking ? 6f : 3f));
            dash = Mathf.MoveTowards(dash, attacking ? 0f : Mathf.SmoothStep(0f, 1f, move), Time.deltaTime * (attacking ? 6f : dashBlend));
            phase += Time.deltaTime * Mathf.Lerp(idleFrequency, moveFrequency, move) * Mathf.PI * 2f;

            Pose(left);
            Pose(right);
        }

        /// <summary>미리보기·확인용(에디터): 걸음 위상 cyclePhase(라디안)·크기 strength(0~1)로 한 번 자세를 잡는다. RestorePose로 되돌린다.</summary>
        public bool PoseAt(float cyclePhase, float strength)
        {
            if (animator == null && !Resolve()) return false;
            phase = cyclePhase; amount = strength; dash = strength;
            Pose(left); Pose(right);
            return true;
        }

        /// <summary>PoseAt으로 바꾼 뼈를 되돌린다.</summary>
        public void RestorePose() => Restore();

        private void Pose(Arm a)
        {
            if (a == null) return;
            Vector3 up = transform.up, fwd = transform.forward;
            float p = phase + a.Offset;
            float s = Mathf.Sin(p);

            // 어깨: 팔을 앞으로 뻗는 쪽이 들리고(번갈아), 걸음마다 두 어깨가 같이 한 번 더 들썩.
            if (a.Clavicle != null && a.UpperArm != null)
            {
                a.Clavicle.Begin();
                Vector3 along = a.UpperArm.T.position - a.Clavicle.T.position;
                float lift = (shoulderLift * s + shoulderHeave * Mathf.Abs(Mathf.Sin(phase))) * amount;
                Rotate(a.Clavicle.T, Raise(along, up), lift);
            }
            // 위팔 → 아래팔: 두 팔을 함께 뒤로 젖히고(대시 자세), 걸음마다 같이 살짝 들썩(두 팔 같은 박자, 걸음 한 쌍에 두 번).
            float flap = Mathf.Sin(phase * 2f);
            if (a.UpperArm != null && a.Forearm != null)
            {
                a.UpperArm.Begin();
                Vector3 upperAlong = a.Forearm.T.position - a.UpperArm.T.position;
                Rotate(a.UpperArm.T, Raise(upperAlong, -fwd), dashBack * dash + upperArmSwing * flap * amount);
                Rotate(a.UpperArm.T, Raise(a.Forearm.T.position - a.UpperArm.T.position, transform.right * -a.Side), dashTuck * dash); // 몸 쪽으로
            }
            if (a.Forearm != null && a.Chain.Length > 0)
            {
                a.Forearm.Begin();
                Vector3 along = a.Chain[^1].T.position - a.Forearm.T.position;
                Rotate(a.Forearm.T, Raise(along, -fwd), dashForearm * dash + forearmSwing * Mathf.Sin(phase * 2f - 0.6f) * amount);
            }
            // 촉수 팔: 뒤로 끌리며 들리고(끝으로 갈수록 크게), 밑동에서 끝으로 흐르는 물결(끝으로 갈수록 크게, 늦게) + 살짝 좌우.
            int n = a.Chain.Length;
            for (int i = 0; i < n; i++)
            {
                var b = a.Chain[i];
                b.Begin();
                Vector3 along = i + 1 < n ? a.Chain[i + 1].T.position - b.T.position : b.T.position - a.Chain[i - 1].T.position;
                float weight = (i + 1f) / n;
                float w = p - 0.9f - i * phasePerBone;
                Rotate(b.T, Raise(along, fwd), tentacleCurl * weight * Mathf.Sin(w) * amount - dashTrail * weight * dash);
                b.T.rotation = Quaternion.AngleAxis(tentacleSway * weight * Mathf.Sin(w * 0.8f + 1.1f) * amount, up) * b.T.rotation;
            }

            // 턱에 매달림: 소매를 턱 쪽으로 뻗고 촉수 팔로 턱을 넘어 감아 쥔다(HasturHangPose). 이 뼈들은 여기서만 쓴다.
            if (hang != null && hang.Weight > 0f)
            {
                if (chainTs == null || chainTs.Length != n) chainTs = new Transform[n];
                for (int i = 0; i < n; i++) chainTs[i] = a.Chain[i].T;
                hang.ApplyArm(a.Clavicle?.T, a.UpperArm?.T, a.Forearm?.T, a.Hand, chainTs, a.Side);
            }
            a.Clavicle?.End(); a.UpperArm?.End(); a.Forearm?.End();
            for (int i = 0; i < n; i++) a.Chain[i].End();
        }

        /// <summary>뼈 방향 along의 끝이 toward 쪽으로 가는 회전축(이 축으로 + 각도만큼 돌리면 끝이 toward로 움직인다).</summary>
        private static Vector3 Raise(Vector3 along, Vector3 toward)
        {
            Vector3 axis = Vector3.Cross(along, toward);
            return axis.sqrMagnitude > 1e-10f ? axis.normalized : Vector3.zero;
        }

        private static void Rotate(Transform t, Vector3 axis, float angle)
        {
            if (axis == Vector3.zero || Mathf.Abs(angle) < 1e-4f) return;
            t.rotation = Quaternion.AngleAxis(angle, axis) * t.rotation;
        }

        private bool Resolve()
        {
            left = right = null;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return false;
            var all = animator.GetComponentsInChildren<Transform>(true);
            left = MakeArm(all, "L", LeftChain, 0f);
            right = MakeArm(all, "R", RightChain, Mathf.PI); // 번갈아
            return left != null || right != null;
        }

        private static Arm MakeArm(Transform[] all, string side, string[] chain, float offset)
        {
            var bones = new Bone[chain.Length];
            for (int i = 0; i < chain.Length; i++)
            {
                var t = Find(all, chain[i]);
                if (t == null) return null; // 하스터 모델이 아님
                bones[i] = new Bone { T = t };
            }
            return new Arm
            {
                Clavicle = Wrap(Find(all, $"biped {side} Clavicle")),
                UpperArm = Wrap(Find(all, $"biped {side} UpperArm")),
                Forearm = Wrap(Find(all, $"biped {side} Forearm")),
                Hand = Find(all, $"biped {side} Hand"),
                Chain = bones,
                Offset = offset,
                Side = side == "L" ? -1f : 1f,
            };
        }

        private static Bone Wrap(Transform t) => t != null ? new Bone { T = t } : null;

        private static Transform Find(Transform[] all, string name)
        {
            foreach (var t in all) if (t.name == name) return t;
            return null;
        }

        private void Restore()
        {
            foreach (var a in new[] { left, right })
            {
                if (a == null) continue;
                // 자식부터 되돌린다.
                for (int i = a.Chain.Length - 1; i >= 0; i--) a.Chain[i].Restore();
                a.Forearm?.Restore(); a.UpperArm?.Restore(); a.Clavicle?.Restore();
            }
        }
    }
}

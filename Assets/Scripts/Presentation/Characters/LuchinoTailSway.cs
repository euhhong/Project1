using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 루키노 꼬리 움직임. 모델(Hunter_Luchino)의 꼬리는 골반 아래 뼈 사슬 6마디(Bone004 → 005 → 006 → 007 → 060 → 080, 끝 Bonenub019)에
    /// 무게가 고르게 잡혀 있지만(Blender로 확인), 사람 모양(Humanoid) 모션은 이 뼈들을 움직이지 않아 꼬리가 뻣뻣하게 굳어 있다.
    /// 그래서 매 프레임 모션이 끝난 뒤 꼬리를 쉬는 자세로 되돌리고 아래를 더해 굽힌다:
    /// - 물결: 밑동에서 끝으로 흐르는 좌우 흔들기(도마뱀 꼬리처럼) + 살짝 위아래. 움직이면 커지고 빨라진다.
    /// - 관성: 몸이 돌면 꼬리가 반대쪽으로 처졌다 따라오고, 옆으로 가면 반대쪽으로 끌린다. 뛰어오르면 처지고, 떨어지면 들리며,
    ///   착지하면 아래로 탁 쳤다 튀어 오른다. 마디마다 따로 된 스프링이라 끝 마디일수록 늦게·크게 따라온다(채찍처럼).
    /// - 달리면 꼬리를 조금 들어 몸 균형을 잡는다. 기절하면 작게, 쓰러지면(체력 0) 흔들지 않고 축 늘어진다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 루키노만). 연출 전용.
    /// </summary>
    [DisallowMultipleComponent]
    public class LuchinoTailSway : MonoBehaviour
    {
        private const string ChainRoot = "Bone004";

        [Header("물결")]
        [Tooltip("끝 마디의 좌우 흔들기 최대 각도(도) — 가만히 / 달릴 때.")]
        [SerializeField, Range(0f, 45f)] private float idleSway = 9f, moveSway = 16f;
        [Tooltip("위아래 들썩 각도(도, 좌우 흔들기 대비 비율).")]
        [SerializeField, Range(0f, 1f)] private float bobRatio = 0.35f;
        [Tooltip("물결 빠르기(초당 번) — 가만히 / 달릴 때.")]
        [SerializeField, Min(0f)] private float idleFrequency = 0.45f, moveFrequency = 1.6f;
        [Tooltip("마디마다 물결이 늦어지는 정도(라디안).")]
        [SerializeField, Range(0f, 2f)] private float phasePerBone = 0.55f;
        [Tooltip("이 빠르기(m/s)에서 가장 크게 흔든다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 6f;
        [Tooltip("달릴 때 꼬리를 드는 각도(도, 꼬리 전체).")]
        [SerializeField, Range(0f, 40f)] private float runLift = 12f;

        [Header("관성")]
        [Tooltip("몸이 돈 각도 중 꼬리가 제자리에 남는 비율(꼬리 전체). 남은 만큼 스프링이 다시 끌고 온다.")]
        [SerializeField, Range(0f, 1f)] private float turnInertia = 0.6f;
        [Tooltip("옆으로 1m/s 갈 때 꼬리가 반대로 끌리는 각도(도, 꼬리 전체).")]
        [SerializeField, Min(0f)] private float strafeLag = 3f;
        [Tooltip("위로 1m/s 오를 때 꼬리가 처지는 각도(도, 꼬리 전체). 떨어질 때는 반대로 들린다.")]
        [SerializeField, Min(0f)] private float verticalLag = 1.6f;
        [Tooltip("관성으로 굽는 최대 각도(도, 꼬리 전체) — 좌우 / 위아래.")]
        [SerializeField, Min(0f)] private float maxYawLag = 40f, maxPitchLag = 35f;
        [Tooltip("착지할 때 꼬리가 아래로 치는 세기(도/초, 꼬리 전체).")]
        [SerializeField, Min(0f)] private float landKick = 260f;
        [Tooltip("스프링 단단함(밑동). 끝 마디로 갈수록 tipStiffness 비율까지 무르다.")]
        [SerializeField, Min(1f)] private float stiffness = 55f;
        [SerializeField, Range(0.1f, 1f)] private float tipStiffness = 0.4f;
        [Tooltip("스프링 감쇠(1 = 넘치지 않음, 작을수록 출렁임).")]
        [SerializeField, Range(0.1f, 1.5f)] private float dampingRatio = 0.55f;

        [Header("기절·쓰러짐")]
        [SerializeField, Range(0f, 1f)] private float stunnedRatio = 0.3f;
        [Tooltip("쓰러졌을 때 꼬리가 처지는 각도(도, 꼬리 전체).")]
        [SerializeField, Range(0f, 60f)] private float deadDroop = 20f;

        private Animator animator;
        private Transform[] bones;
        private Quaternion[] rest;
        private Vector2[] lag, lagVelocity; // 마디마다 x = 좌우(+ = 왼쪽으로), y = 위아래(+ = 처짐), 도
        private ICharacterState state;
        private ICharacterHealth health;
        private ICharacterStun stun;
        private float phase, amount, lastYaw;
        private bool hasYaw;

        private void OnEnable()
        {
            state ??= GetComponent<ICharacterState>();
            if (state != null) state.Landed += OnLanded;
        }

        private void OnDisable()
        {
            if (state != null) state.Landed -= OnLanded;
            Restore();
        }

        private void OnLanded()
        {
            if (lagVelocity == null) return;
            float fall = state != null ? Mathf.Clamp01(-state.VerticalSpeed / 15f) : 0.5f;
            float kick = landKick * Mathf.Lerp(0.4f, 1f, fall) / lagVelocity.Length;
            for (int i = 0; i < lagVelocity.Length; i++) lagVelocity[i].y += kick;
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled || bones == null) { Restore(); if (!Resolve()) return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f); // 끊긴 프레임에 스프링이 튀지 않게
            if (dt <= 0f) return;

            health ??= GetComponent<ICharacterHealth>();
            stun ??= GetComponent<ICharacterStun>();
            bool dead = health != null && health.IsDead;
            bool stunned = stun != null && stun.IsStunned;

            // 움직임 크기·박자
            Vector2 planar = state != null ? state.LocalPlanarVelocity : Vector2.zero;
            float move = Mathf.Clamp01(planar.magnitude / fullSpeed);
            float targetAmount = dead ? 0f : stunned ? stunnedRatio : 1f;
            amount = Mathf.MoveTowards(amount, targetAmount, dt * 2.5f);
            phase += dt * Mathf.Lerp(idleFrequency, moveFrequency, move) * Mathf.PI * 2f;

            // 관성으로 굽는 목표(꼬리 전체 각도)
            float yaw = animator.transform.eulerAngles.y; // 모델이 보는 방향(루트 + 모델 자식 회전)
            float turned = hasYaw ? Mathf.DeltaAngle(lastYaw, yaw) : 0f; // 이번 프레임에 몸이 돈 각도(+ = 오른쪽)
            lastYaw = yaw; hasYaw = true;
            float vertical = state != null && !state.IsGrounded ? state.VerticalSpeed : 0f;
            float yawTarget = Mathf.Clamp(planar.x * strafeLag, -maxYawLag, maxYawLag);
            float pitchTarget = Mathf.Clamp(vertical * verticalLag - runLift * Mathf.Clamp01(planar.y / fullSpeed), -maxPitchLag, maxPitchLag);
            if (dead) { yawTarget = 0f; pitchTarget = deadDroop; }

            int n = bones.Length;
            Vector3 up = transform.up;
            float sway = Mathf.Lerp(idleSway, moveSway, move) * amount;
            for (int i = 0; i < n; i++)
            {
                // 마디마다 스프링: 끝으로 갈수록 무르게 → 늦게 따라와 채찍처럼 휜다.
                float t = n > 1 ? i / (n - 1f) : 0f;
                float k = stiffness * Mathf.Lerp(1f, tipStiffness, t);
                float c = 2f * dampingRatio * Mathf.Sqrt(k);
                Vector2 target = new(yawTarget / n, pitchTarget / n);
                // 몸이 돌면 꼬리는 제자리에 남으려 한다 → 오른쪽으로 돌면 엉덩이는 왼쪽으로 가고 꼬리는 몸 기준 오른쪽(-)으로 남는다. 끝 마디일수록 많이.
                float share = 2f * (i + 1f) / (n * (n + 1f)); // 끝으로 갈수록 큰 몫, 다 더하면 1
                float limit = maxYawLag * share * 1.5f;
                lag[i].x = Mathf.Clamp(lag[i].x - turned * turnInertia * share, -limit, limit);
                lagVelocity[i] += (k * (target - lag[i]) - c * lagVelocity[i]) * dt;
                lag[i] += lagVelocity[i] * dt;

                // 물결: 끝으로 갈수록 크게, 마디마다 박자가 늦게.
                float weight = (i + 1f) / n;
                float w = phase - i * phasePerBone;
                float waveYaw = sway * weight * Mathf.Sin(w) / n * 2f;
                float wavePitch = sway * bobRatio * weight * Mathf.Sin(w * 2f + 0.8f) / n * 2f;

                var b = bones[i];
                b.localRotation = rest[i]; // 쉬는 자세에서 시작(모션이 이 뼈를 쓰지 않으므로 매번 되돌린다)
                Vector3 along = i + 1 < n ? bones[i + 1].position - b.position : b.position - bones[i - 1].position;
                Vector3 side = Vector3.Cross(up, along); // 이 축으로 + 돌리면 꼬리가 아래로 처진다
                if (side.sqrMagnitude < 1e-8f) continue;
                side.Normalize();
                Quaternion q = Quaternion.AngleAxis(lag[i].y + wavePitch, side) * Quaternion.AngleAxis(lag[i].x + waveYaw, up);
                b.rotation = q * b.rotation;
            }
        }

        private bool Resolve()
        {
            bones = null;
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return false;
            Transform root = null;
            foreach (var tr in animator.GetComponentsInChildren<Transform>(true)) if (tr.name == ChainRoot) { root = tr; break; }
            if (root == null) return false;
            var list = new System.Collections.Generic.List<Transform>();
            for (var b = root; b != null && !b.name.StartsWith("Bonenub"); b = b.childCount > 0 ? b.GetChild(0) : null) list.Add(b);
            if (list.Count < 2) return false;
            bones = list.ToArray();
            rest = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rest[i] = bones[i].localRotation;
            lag = new Vector2[bones.Length];
            lagVelocity = new Vector2[bones.Length];
            hasYaw = false;
            return true;
        }

        private void Restore()
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = rest[i];
        }
    }
}

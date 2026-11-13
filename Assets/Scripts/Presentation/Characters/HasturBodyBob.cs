using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 하스터가 움직일 때 몸 전체(보이는 모델)를 위아래로 들썩인다(연출 전용).
    /// 캐릭터 루트 아래 Model(Animator가 있는 자식)의 높이만 바꾼다 — 루트·CharacterController·이동 판정은 그대로.
    /// 걸음 박자는 HasturArmWalk의 걸음 위상을 따른다(걸음 한 쌍에 두 번 들썩 = 두 어깨가 같이 들썩이는 순간에 몸이 들림).
    /// 빠를수록 크고 빠르게(빠르기는 HasturArmWalk가 속도에 맞춰 정함), 멈추거나 공중에 뜨면 부드럽게 가라앉는다.
    /// 다른 코드가 Model 위치를 바꾸면 그 위치를 바탕으로 더한다(매 프레임 쌓이지 않음).
    /// HasturLegWriggle이 붙인다(하스터만) — 하스터가 아니게 되면 스스로 원래대로 되돌리고 없어진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HasturBodyBob : MonoBehaviour
    {
        [Tooltip("가장 빠를 때 들썩이는 높이(m, 가장 낮은 곳 → 가장 높은 곳).")]
        [SerializeField, Range(0f, 0.3f)] private float height = 0.07f;
        [Tooltip("이 빠르기(m/s)에서 가장 크게 들썩인다.")]
        [SerializeField, Min(0.1f)] private float fullSpeed = 5f;
        [Tooltip("느릴 때 높이가 덜 줄게 하는 곡선(1 = 속도에 비례, 작을수록 걸을 때도 잘 보임).")]
        [SerializeField, Range(0.2f, 1f)] private float speedCurve = 0.6f;
        [Tooltip("들썩임이 커지고 가라앉는 빠르기(초당 비율).")]
        [SerializeField, Min(0.1f)] private float blendSpeed = 3f;
        [Tooltip("HasturArmWalk가 없을 때 쓰는 걸음 빠르기(초당 한 쌍) — 가만히 / 가장 빠를 때.")]
        [SerializeField, Min(0f)] private float idleFrequency = 0.35f, moveFrequency = 1.5f;

        private Animator animator;
        private Transform model;
        private ICharacterState state;
        private HasturArmWalk armWalk;
        private Vector3 basePos, written;
        private bool has;
        private float phase, amount;

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            if (!TryGetComponent(out HasturLegWriggle _)) { Restore(); Destroy(this); return; } // 하스터가 아니게 됨
            if (animator == null || !animator.isActiveAndEnabled) { Restore(); if (!Resolve()) return; }
            state ??= GetComponent<ICharacterState>();
            if (armWalk == null) TryGetComponent(out armWalk);

            float speed = state != null ? state.LocalPlanarVelocity.magnitude : 0f;
            float move = Mathf.Clamp01(speed / fullSpeed);
            bool grounded = state == null || state.IsGrounded;
            float target = grounded && speed > 0.05f ? Mathf.Pow(move, speedCurve) : 0f;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime * blendSpeed);

            if (armWalk != null) phase = armWalk.StepPhase;
            else phase += Time.deltaTime * Mathf.Lerp(idleFrequency, moveFrequency, move) * Mathf.PI * 2f;

            // 바탕 위치: 지난 프레임에 우리가 쓴 그대로면 그대로, 누가 바꿨으면 바뀐 위치가 새 바탕.
            if (!has || (model.localPosition - written).sqrMagnitude > 1e-10f) basePos = model.localPosition;

            // sin² = 걸음 한 쌍에 두 번, 부드럽게 오르내림. 가운데(0)를 기준으로 위아래 반씩 → 평균 높이는 그대로.
            float s = Mathf.Sin(phase);
            float offset = height * amount * (s * s - 0.5f);
            float rootScale = Mathf.Max(transform.lossyScale.y, 1e-4f);
            model.localPosition = basePos + Vector3.up * (offset / rootScale);
            written = model.localPosition;
            has = true;
        }

        private bool Resolve()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.transform == transform) { animator = null; model = null; return false; }
            model = animator.transform;
            has = false;
            return true;
        }

        private void Restore()
        {
            if (model != null && has) model.localPosition = basePos;
            has = false;
        }
    }
}

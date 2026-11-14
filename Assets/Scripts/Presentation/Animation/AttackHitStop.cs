using Project1.Core;
using Project1.Systems;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 공격이 맞으면(HitEvents — 이 캐릭터가 때린 히트) 모델 Animator를 잠깐(stopTime초) 멈췄다 다시 돌린다 — 맞힌 순간 짧게 멈칫(히트스톱, 연출 전용).
    /// 이동 정지는 BE(분신은 CloneBrain.hitStop)가 맡는다. 공장장 분신에 붙인다(HellEmberCloneVisual).
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackHitStop : MonoBehaviour
    {
        [Tooltip("맞힌 순간 모션을 멈추는 시간(초).")]
        [SerializeField, Min(0f)] private float stopTime = 0.15f;

        private CharacterIdentity self;
        private Animator animator;
        private float resumeAt = -1f, savedSpeed = 1f;

        private void OnEnable() => HitEvents.HitOccurred += OnHit;

        private void OnDisable()
        {
            HitEvents.HitOccurred -= OnHit;
            Resume();
        }

        private void OnHit(HitResult hit)
        {
            if (self == null) TryGetComponent(out self);
            if (self == null || hit.AttackerId != self.Id || hit.VictimId == self.Id || stopTime <= 0f) return;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            if (resumeAt < 0f) savedSpeed = animator.speed; // 이미 멈춰 있으면 원래 빠르기를 덮어쓰지 않는다
            animator.speed = 0f;
            resumeAt = Time.time + stopTime;
        }

        private void Update()
        {
            if (resumeAt >= 0f && Time.time >= resumeAt) Resume();
        }

        private void Resume()
        {
            if (resumeAt < 0f) return;
            resumeAt = -1f;
            if (animator != null) animator.speed = savedSpeed;
        }
    }
}

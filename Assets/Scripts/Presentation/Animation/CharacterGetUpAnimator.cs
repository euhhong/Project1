using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 누워 있다가 일어나는 모션을 하나로 고정한다: Animator "GetUp" 레이어(전신, 맨 위)의 GetUp(Kip Up).
    /// - 넉다운(헤드샷 킥에 나가떨어짐)·뒤에서 맞아 쓰러짐: CharacterHitReactionAnimator가 Begin(남은 기절 시간)을 부른다.
    /// - 스킬이 막혀 넘어짐(전위 돌진·광대 로켓 돌진이 벽에 부딪힘 — Skill 레이어 "_End" 상태): 넘어진 시간이 getUpTime만큼 남으면 스스로 시작한다.
    /// 재생 위치는 행동 불가 시간이 끝나는 순간 다 일어서게 맞춘다(짧으면 빨리). 체력 0 쓰러짐(CharacterDeathAnimator)은 일어나지 않으므로 쓰지 않는다.
    /// 연출 전용이고 게임 상태는 바꾸지 않는다. 캐릭터 루트에 붙인다(CharacterHitReactionAnimator가 함께 붙인다).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterGetUpAnimator : MonoBehaviour
    {
        private const string LayerName = "GetUp";
        private static readonly int GetUpId = Animator.StringToHash("GetUp");

        [Tooltip("일어나는 데 쓰는 시간(초). 행동 불가 시간이 이만큼 남으면 일어나기 시작한다(모션 원래 길이 약 1.47초 → 빠르게).")]
        [SerializeField, Min(0.2f)] private float getUpTime = 0.95f;
        [Tooltip("누운 자세에서 일어나기 모션으로 섞어 들어가는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeIn = 0.1f;
        [Tooltip("다 일어선 뒤 원래 모션으로 돌아오는 시간(초).")]
        [SerializeField, Min(0.01f)] private float fadeOut = 0.15f;

        private Animator animator;
        private int layer = -1;
        private float weight, time, duration;
        private bool playing;
        private ICharacterSkillState skill;
        private bool skillFallen, skillGotUp;
        private float skillFallTime;

        /// <summary>일어나기 모션이 재생 중인지(섞여 나가는 동안 포함).</summary>
        public bool Playing => playing;

        /// <summary>일어나기 시작까지 남겨 둘 시간(초): 행동 불가 시간이 이만큼 남으면 Begin을 부른다.</summary>
        public float GetUpTime => getUpTime;

        /// <summary>모션 컨트롤러에 일어나기 레이어(GetUp)가 있는지.</summary>
        public bool Available => Resolve();

        private bool Resolve()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                layer = -1; weight = 0f; playing = false;
            }
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (layer < 0) layer = animator.GetLayerIndex(LayerName);
            return layer >= 0 && animator.HasState(layer, GetUpId);
        }

        /// <summary>지금부터 seconds초 동안 누운 자세에서 일어난다(이미 일어나는 중이면 무시). 쓸 수 없으면 false.</summary>
        public bool Begin(float seconds)
        {
            if (playing || !Resolve()) return false;
            duration = Mathf.Max(0.2f, seconds);
            time = 0f;
            weight = 0f;
            playing = true;
            animator.Play(GetUpId, layer, 0f);
            return true;
        }

        private void LateUpdate()
        {
            WatchSkillFall();
            if (!playing || !Resolve()) return;
            time += Time.deltaTime;
            float t = time / duration;
            animator.Play(GetUpId, layer, Mathf.Min(t, 0.999f)); // 행동 불가 시간이 끝나는 순간 다 일어선다
            weight = t < 1f ? Mathf.MoveTowards(weight, 1f, Time.deltaTime / fadeIn) : Mathf.MoveTowards(weight, 0f, Time.deltaTime / fadeOut);
            if (t >= 1f && weight <= 0f) playing = false;
            animator.SetLayerWeight(layer, weight);
        }

        // 스킬이 막혀 넘어짐(Recovering + "_End" 상태): 넘어진 전체 시간은 진행도로 알아내고, getUpTime이 남으면 일어난다.
        private void WatchSkillFall()
        {
            if (skill == null && !TryGetComponent(out skill)) return;
            bool fallen = skill.State == SkillState.Recovering && skill.AnimationState.EndsWith("_End");
            if (!fallen) { skillFallen = false; skillGotUp = false; return; }
            if (!skillFallen) { skillFallen = true; skillGotUp = false; skillFallTime = 0f; }
            skillFallTime += Time.deltaTime;
            float p = skill.RecoveryProgress;
            if (skillGotUp || p <= 0.05f) return;
            float remaining = skillFallTime / p * (1f - p);
            if (remaining <= getUpTime) skillGotUp = Begin(remaining);
        }
    }
}

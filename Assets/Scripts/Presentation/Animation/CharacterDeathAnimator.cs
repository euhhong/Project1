using Project1.Core;
using UnityEngine;

namespace Project1.Presentation
{
    /// <summary>
    /// 사망 모션: 체력이 0이 되면(ICharacterHealth.IsDead) Animator의 IsDown을 켜 쓰러지는 모션(Down = Mixamo Zombie Dying)을 재생하고
    /// 마지막 누운 자세로 멈춘다. 쓰러져 있는 동안은 위에 얹히는 레이어(공격·피격·스킬·들고 다니기)를 꺼서 누운 자세가 그대로 보이게 한다.
    /// 체력이 다시 차면(다시 하기 등) 일어나 원래대로. 다른 레이어 무게를 덮어쓰도록 가장 늦게 돈다.
    /// 캐릭터 루트에 붙인다(CharacterLoadout, 모두).
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public class CharacterDeathAnimator : MonoBehaviour
    {
        private static readonly int IsDownId = Animator.StringToHash("IsDown");
        private ICharacterHealth health;
        private Animator animator;
        private bool hasParam;

        private void LateUpdate()
        {
            if (health == null) health = GetComponent<ICharacterHealth>();
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                hasParam = false;
                if (animator != null && animator.runtimeAnimatorController != null)
                    foreach (var p in animator.parameters) if (p.nameHash == IsDownId) hasParam = true;
            }
            if (animator == null || !hasParam || health == null) return;
            bool dead = health.IsDead;
            animator.SetBool(IsDownId, dead);
            if (!dead) return;
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f); // 바닥 레이어(쓰러짐)만
        }
    }
}

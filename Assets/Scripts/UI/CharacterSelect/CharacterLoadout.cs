using Project1.Presentation;
using Project1.Systems;
using UnityEngine;

namespace Project1.UI
{
    /// <summary>
    /// 고른 캐릭터를 플레이어 캐릭터 루트에 입힌다: 모델 교체, 체력 초기화, 고유 스킬, HUD.
    /// 체력·스킬 컴포넌트가 루트에 없으면 붙인다(씬을 고치지 않아도 되게).
    /// </summary>
    public static class CharacterLoadout
    {
        public static void Apply(GameObject player, CharacterRoster.Entry entry)
        {
            CharacterModelSwapper.Swap(player, entry.model, entry.scale, entry.gameController);

            var health = Ensure<CharacterHealth>(player);
            health.ResetToFull();

            Ensure<CharacterStun>(player);
            var skill = Ensure<CharacterSkill>(player);
            skill.SetSkill(entry.skill);
            // 스킬·기절이 이동을 제한하거나 대신 정하므로, 이동 컴포넌트가 새 컴포넌트를 알게 한다.
            var motor = player.GetComponent<CharacterMotor>();
            if (motor != null) motor.RefreshSpeedLimits();

            Ensure<CharacterSkillAnimator>(player);
            Ensure<CharacterHitReactionAnimator>(player);

            var hud = Object.FindAnyObjectByType<PlayerHud>();
            if (hud == null) hud = new GameObject("PlayerHud").AddComponent<PlayerHud>();
            hud.SetTarget(player, entry.displayName);

            if (Debug.isDebugBuild) Ensure<HealthDebugKeys>(player);
        }

        private static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}

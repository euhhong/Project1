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
            Ensure<SkillAreaIndicator>(player);
            Ensure<AfterimageTrail>(player);

            // 캐릭터별 소품(무기). 다른 캐릭터로 바꾸면 지운다.
            SetProp<FanWeapon>(player, entry.id == "Hunter_Geisha"); // 붉은나비: 접는 부채
            SetProp<GrabShotVisual>(player, entry.id == "Hunter_Gamekeeper"); // 사냥터지기: 그랩 쏘기 연출
            SetProp<CharacterCarryPose>(player, entry.id == "Hunter_SmileyFace"); // 광대: 로켓 폭죽을 두 손으로 받쳐 든 자세
            SetProp<RocketWeapon>(player, entry.id == "Hunter_SmileyFace");       // 광대: 로켓 폭죽 무기
            // 만화 타격 효과: 생존자 모두(플라잉킥). 생존자 기본 공격(Mutant Punch)을 쓰면 펀치 효과 + 늘어난 리치도.
            SetProp<CartoonPunch>(player, entry.role == CharacterRole.Survivor);
            if (player.TryGetComponent(out CartoonPunch punch))
                punch.SetPunchReach(entry.gameController != null && entry.gameController.name == "CharacterLocomotion");

            var hud = Object.FindAnyObjectByType<PlayerHud>();
            if (hud == null) hud = new GameObject("PlayerHud").AddComponent<PlayerHud>();
            hud.SetTarget(player, entry.displayName);

            if (Debug.isDebugBuild) Ensure<HealthDebugKeys>(player);
        }

        private static void SetProp<T>(GameObject go, bool use) where T : Component
        {
            if (use) Ensure<T>(go);
            else if (go.TryGetComponent(out T c)) Object.Destroy(c);
        }

        private static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
    }
}

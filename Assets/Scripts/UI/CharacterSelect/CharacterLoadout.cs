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
        /// <param name="isPlayer">false = AI 봇 등(HUD·디버그 키를 붙이지 않는다).</param>
        public static void Apply(GameObject player, CharacterRoster.Entry entry, bool isPlayer = true)
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
            Ensure<CharacterDeathAnimator>(player); // 체력 0 = 쓰러지는 모션(Zombie Dying)
            Ensure<SkillAreaIndicator>(player);
            Ensure<AfterimageTrail>(player);

            // 캐릭터별 소품(무기). 다른 캐릭터로 바꾸면 지운다.
            SetProp<FanWeapon>(player, entry.id == "Hunter_Geisha"); // 붉은나비: 접는 부채
            SetProp<ButterflyField>(player, entry.id == "Hunter_Geisha");  // 붉은나비: 좌클릭 꾹 = 나비 놓기, E = 겨눈 나비에게(BE)
            SetProp<ButterflyVisual>(player, entry.id == "Hunter_Geisha"); // 붉은나비: 나비·미리보기·표적 표시(FE)
            SetProp<GrabShotVisual>(player, entry.id == "Hunter_Gamekeeper"); // 사냥터지기: 그랩 쏘기 연출
            SetProp<FlareShotVisual>(player, entry.id == "Survivor_Coordinator"); // 공군: 신호탄 붉은 연기 궤적
            SetProp<NoteField>(player, entry.id == "Hunter_Antonio");   // 안토니오: 좌클릭 꾹 / E = 붉은 음표, 두 개를 전류선으로 이음(BE)
            SetProp<NoteVisual>(player, entry.id == "Hunter_Antonio");  // 안토니오: 음표·전류선 연출(FE)
            SetProp<AntonioViolin>(player, entry.id == "Hunter_Antonio"); // 안토니오: 바이올린·활(VARCO) — 대기 땐 켜는 자세, 움직일 땐 들고 다님(FE)
            SetProp<FlashlightVisual>(player, entry.id == "Survivor_Thief"); // 도둑: 손전등·빛·지져짐 게이지(FE)
            SetProp<HellEmberCloneVisual>(player, entry.id == "Hunter_HellEmber"); // 공장장: 분신 모델·용암 겉모습·갈고리 낫(FE)
            if (player.TryGetComponent(out HellEmberCloneVisual cloneVisual)) cloneVisual.SetModel(entry.model, entry.scale, entry.gameController);
            SetProp<MistCharge>(player, entry.id == "Hunter_Ripper");     // 리퍼: 공격하지 않으면 7초마다 안개 1칸(최대 2칸), 2칸에서 공격 = 안개 칼날, E = 1칸으로 약한 칼날(BE)
            SetProp<MistClawVisual>(player, entry.id == "Hunter_Ripper"); // 리퍼: 손톱에 감기는 안개·날아가는 손톱 모양 안개 칼날(FE)
            SetProp<CharacterCarryPose>(player, entry.id == "Hunter_SmileyFace"); // 광대: 로켓 폭죽을 두 손으로 받쳐 든 자세
            SetProp<RocketWeapon>(player, entry.id == "Hunter_SmileyFace");       // 광대: 로켓 폭죽 무기
            SetProp<WeaponBodyClearance>(player, entry.role == CharacterRole.Hunter); // 감시자: 휘두르는 무기가 자기 몸을 뚫어 보이지 않게(FE)
            SetProp<CaneKnockback>(player, entry.id == "Survivor_MindsEye"); // 맹인: 지팡이에 맞으면 데미지 대신 넉백(BE)
            SetProp<CaneSweepReach>(player, entry.id == "Survivor_MindsEye"); // 맹인: 지팡이 휘두르기 판정을 옆으로 아주 넓게(BE)
            SetProp<CaneThrustPose>(player, entry.id == "Survivor_MindsEye"); // 맹인: E 찌르기 동안 지팡이 끝이 정면을 보게 길게 뻗음(FE)
            // 리퍼: 무기가 없고 왼손(손톱 칼날) 자체가 무기 → 약·강공격 판정을 왼손 + 무기로. 다른 캐릭터는 처음 값.
            if (player.TryGetComponent(out CharacterAttack attack))
            {
                var slots = entry.id == "Hunter_Ripper" ? Project1.Core.HitboxSlot.LeftHand | Project1.Core.HitboxSlot.Weapon
                          : entry.id == "Survivor_MindsEye" ? Project1.Core.HitboxSlot.RightHand | Project1.Core.HitboxSlot.Weapon // 맹인: 지팡이(무기)로 맞힌다
                          : entry.id == "Hunter_Hastur" ? Project1.Core.HitboxSlot.RightHand | Project1.Core.HitboxSlot.LeftHand  // 하스터: 두 손으로 내려찍는다
                          : Project1.Core.HitboxSlot.None;
                attack.SetAttackSlots(slots, slots);
                attack.SetSwipeAttacks(entry.id == "Survivor_MindsEye"); // 맹인: 좌클릭을 누른 채 마우스를 좌우로 그으면 그쪽으로 지팡이 후리기
            }
            // 만화 타격 효과: 생존자 모두(플라잉킥). 주먹 기본 공격(Mutant Punch·훅 펀치)을 쓰면 펀치 효과 + 늘어난 리치도.
            SetProp<CartoonPunch>(player, entry.role == CharacterRole.Survivor);
            if (player.TryGetComponent(out CartoonPunch punch))
                punch.SetPunchReach(UsesDefaultPunch(entry.gameController));

            if (!isPlayer) return;
            var hud = Object.FindAnyObjectByType<PlayerHud>();
            if (hud == null) hud = new GameObject("PlayerHud").AddComponent<PlayerHud>();
            hud.SetTarget(player, entry.displayName);

            if (Debug.isDebugBuild) Ensure<HealthDebugKeys>(player);
        }

        /// <summary>주먹 기본 공격을 쓰는지: 생존자 기본(Mutant Punch, 클립 Attack_Light) 또는 빈손 생존자 훅 펀치(Attack_Light_HookPunch, 용병·도둑·카우보이). 무기 공격으로 바꾼 캐릭터는 아님.</summary>
        private static bool UsesDefaultPunch(RuntimeAnimatorController controller)
        {
            if (controller == null) return false;
            foreach (var clip in controller.animationClips) if (clip != null && (clip.name == "Attack_Light" || clip.name == "Attack_Light_HookPunch")) return true;
            return false;
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

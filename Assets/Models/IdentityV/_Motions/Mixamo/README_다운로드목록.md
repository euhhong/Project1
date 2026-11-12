# Mixamo 모션 목록 (FE)

**2026-11-12부터 모션을 코드로 만들지 않는다(사용자 결정).** 모든 모션은 이 폴더의 모션 파일(FBX)을 쓴다.
아직 파일이 없는 자리만 예전 임시 모션(`_Motions/Procedural/*.anim`)을 그대로 쓴다. 임시 모션은 더 만들거나 고치지 않는다.

- 받은 파일을 **이 폴더**에 아래 "저장 이름"으로 넣으면 끝이다. 임포트 설정(Humanoid, Loop, 제자리, 자르기)과
  Animator Controller 재구성은 자동으로 된다(`Presentation/Editor/MixamoMotionPostprocessor.cs`,
  `CharacterMotionControllerBuilder.cs`, 메뉴 Tools > FE > 모션 컨트롤러 다시 만들기).
- 생존자는 `_Motions/CharacterLocomotion.controller`를 쓴다. 감시자는 Override Controller를 쓴다:
  공장장·사냥터지기·안토니오·리퍼·광대 = `HunterLocomotion`, 루키노 = `LuchinoLocomotion`,
  붉은나비(게이샤) = `GeishaLocomotion`, 하스터 = `HasturLocomotion`. 감시자 공통 모션(`_Hunter`) 위에 캐릭터 모션(`_<이름>`)을 덮는다.
- 공격 모션의 판정 순간(손·발이 맞는 프레임)은 `Presentation/Animation/CharacterAttackAnimator.cs`의 `Timing` 표에 적는다.

## 다운로드 설정 (모든 모션 공통)
- **Format:** FBX for Unity (.fbx) / **Skin:** Without Skin / **Frames per Second:** 30
- 이동 모션은 **In Place**를 켠다(이동은 게임 코드가 한다). 꺼져 있어도 수평 이동은 임포트할 때 버린다.

## 자리별 현황

| 자리(저장 이름) | 용도 | 상태 | 받은 원본 |
|---|---|---|---|
| Walk_F / Walk_B / Walk_L / Walk_R | 걷기 4방향 | **받음** | Standing Walk Forward / Back / Left / Right |
| Run_F | 앞 달리기(생존자·감시자 공통) | **받음** | Standard Run |
| Run_B / Run_L / Run_R | 뒤·옆 달리기 | 대기 — 지금은 같은 방향 걷기를 1.7배속으로 빌려 씀 | |
| Attack_Light | 기본 공격(생존자, 맹인 제외) | **받음** — 오른손 훅 2~30프레임(판정 6~11). 만화 "펑" 효과 + 리치 1.8m(CartoonPunch) | Mutant Punch (전: Boxing) |
| Attack_Light_MindsEye | 맹인 기본 공격 → `MindsEyeLocomotion` | **받음** — 10~48프레임, 첫 타 가로 베기 24~31 후 한 바퀴 돌아 앞을 봄(두 번째 타는 뺌) | Standing Melee Combo Attack Ver. 3 |
| Attack_Light_Hunter | 기본 공격(감시자: 공장장·사냥터지기·안토니오·리퍼·광대) → `HunterLocomotion` | **받음** — 6~50프레임, 오른손 내려치기 20~28 | Standing Melee Attack Downward |
| Attack_Light_Luchino | 루키노 기본 공격 → `LuchinoLocomotion` | **받음** — 4~60프레임, 오른손 가로 베기 22~29 | Standing Melee Attack Horizontal |
| Attack_Light_Geisha | 붉은나비 기본 공격 → `GeishaLocomotion` | **받음** — 6~60프레임, 오른손 찌르기 14~25(왼손은 횃불 자세). 붉은나비는 유령처럼 이동(걷기·달리기도 대기 자세로 미끄러짐), 다리 Hurtbox 없음 | Standing Torch Melee Attack Stab |
| Attack_Light_Hastur | 하스터 기본 공격 → `HasturLocomotion` | **받음** — 8~44프레임, 두 손 내뻗기 28~36(뒤의 긴 유지 자세는 뺌) | Standing 2H Magic Attack 03 |
| (스킬) 사냥터지기 그랩 던지기 | 사냥터지기(노루) 스킬 | **받음, 연결 대기** — `Attack_Light_Hunter.fbx`를 같이 쓴다. 스킬 시스템(BE)이 생기면 Animator에 스킬 상태를 붙인다 | Standing Melee Attack Downward |
| Attack_RunJump_Survivor | 앞으로 달리다 점프 중 좌클릭(생존자) | **받음** — 0~30프레임, 왼발 차기 | Flying Kick |
| Attack_RunJump_Hunter | 앞으로 달리다 점프 중 좌클릭(감시자) | **받음** — 36~70프레임, 내려치기 | Standing Melee Run Jump Attack |
| Down | 맞아서 쓰러짐 | **받음** — Animator `IsDown`(bool)을 켜면 재생, 누운 자세로 멈춤 | Standing Death Left 01 |
| Hit_Head | 머리 피격(공통) | **받음** — HitReact 레이어(상체)에서 한 번 재생 | Head Hit |
| Hit_Body_Side / Hit_Body_Back | 몸통 피격(공통) — 맞은 위치가 앞·옆이면 Side, 뒤면 Back | **받음** | Big Side Hit / Big Kidney Hit |
| Hit_Limb | 팔·다리 피격(공통) | **받음** | Side Hit |
| Fall_1 | 넘어지기 1 — 전위 돌진이 벽·장애물에 부딪힘(`Forward_Charge_End`) | **받음** — 1.8초 동안 넘어져 못 움직임 | Rifle Hit To Back |
| Skill_Forward_Charge | 전위 럭비공 돌진(반복) | **받음** — 돌진 16m/s에 맞춰 2배속 재생 | Running |
| Skill_Luchino_Leap | 루키노 도약(E) → 공중에서 공격 키 = 내려찍기 | **받음** — 한 파일을 둘로 나눔: Skill_Luchino_Leap(16~30, 도약~꼭대기) + Skill_Luchino_Leap_Slam(39~87, 내리치기~착지~일어서기, 발 기준 높이) | Jump Attack |
| Skill_Doctor_SelfHeal | 의사 자가치료(주사기 꽂기) | **대기** — 파일을 넣으면 Skill 레이어에 자동으로 들어간다 | |
| Stun | 기절(반복, 고개 숙임) — 전위 돌진에 맞음 | **대기** — 없으면 머리 피격(Head Hit)으로 고개를 숙이고 기절 동안 유지 | |
| Block | 막기(공통, 반복) | **받음, 연결 대기** — 막기 기능이 아직 없어 Animator에는 넣지 않았다 | Standing Block Idle |
| Vault_Survivor | 창틀·판자 넘기(생존자) | **받음, 연결 대기** — 넘기 기능이 아직 없어 Animator에는 넣지 않았다 | Jumping Down |
| SelectIdle_Hunter | 캐릭터 선택 화면 감시자 대기(반복) | **받음** — `_Motions/HunterSelect.controller` | Idle |
| SelectIdle_Survivor | 캐릭터 선택 화면 생존자 대기(반복) | **받음** — `_Motions/SurvivorSelect.controller` | Standing Idle |
| Idle | 대기 | 대기(임시 모션) | |
| Air | 제자리 점프·떨어짐(공통) | **받음** — 0~22프레임(도약~착지), 게임 공중 시간 0.69초에 맞춰 재생 | Jump (1) (전: Standing Jump) |
| Air_F | 걸어서 점프(공통) — 점프할 때 MoveZ > 0.5 | **받음** — 25~44프레임, 0.69초에 맞춰 재생 | Jumping |
| Air_Run | 뛰면서 점프(공통) — 점프할 때 MoveZ > 1.75 | **받음** — 0~20프레임, 0.69초에 맞춰 재생 | Running Jump |
| Attack_HeavyCharge / Attack_Heavy | 강공격 모으기 / 내려치기 | 대기(임시 모션) | |
| Attack_AirStomp / Attack_AirCharge | 제자리 점프 공격 / 공중 모으기 | 없음 — 파일이 없으면 공중에서도 땅 공격 모션을 쓴다(공중 모으기는 없음) | |

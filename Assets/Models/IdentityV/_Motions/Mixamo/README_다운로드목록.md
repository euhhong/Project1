# Mixamo 모션 목록 (FE)

**2026-11-12부터 모션을 코드로 만들지 않는다(사용자 결정).** 모든 모션은 이 폴더의 모션 파일(FBX)을 쓴다.
아직 파일이 없는 자리만 예전 임시 모션(`_Motions/Procedural/*.anim`)을 그대로 쓴다. 임시 모션은 더 만들거나 고치지 않는다.

- 받은 파일을 **이 폴더**에 아래 "저장 이름"으로 넣으면 끝이다. 임포트 설정(Humanoid, Loop, 제자리, 자르기)과
  Animator Controller 재구성은 자동으로 된다(`Presentation/Editor/MixamoMotionPostprocessor.cs`,
  `CharacterMotionControllerBuilder.cs`, 메뉴 Tools > FE > 모션 컨트롤러 다시 만들기).
- 생존자는 `_Motions/CharacterLocomotion.controller`, 감시자는 `_Motions/HunterLocomotion.overrideController`를 쓴다.
  감시자 Controller는 생존자 Controller에서 `_Hunter` 모션만 바꾼다.
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
| Attack_Light | 기본 공격(생존자) | **받음** — 왼손 잽을 좌우 반전해 오른손(BE 판정 손)으로 씀, 5~30프레임 | Boxing |
| Attack_Light_Hunter | 기본 공격(감시자: 공장장·사냥터지기·안토니오·리퍼·광대) | **받음** — 6~50프레임, 오른손 내려치기 20~28. 감시자 Override 전체에 들어가므로 게이샤·하스터·루키노도 지금은 이 모션 | Standing Melee Attack Downward |
| (스킬) 사냥터지기 그랩 던지기 | 사냥터지기(노루) 스킬 | **받음, 연결 대기** — `Attack_Light_Hunter.fbx`를 같이 쓴다. 스킬 시스템(BE)이 생기면 Animator에 스킬 상태를 붙인다 | Standing Melee Attack Downward |
| Attack_RunJump_Survivor | 앞으로 달리다 점프 중 좌클릭(생존자) | **받음** — 0~30프레임, 왼발 차기 | Flying Kick |
| Attack_RunJump_Hunter | 앞으로 달리다 점프 중 좌클릭(감시자) | **받음** — 36~70프레임, 내려치기 | Standing Melee Run Jump Attack |
| Down | 맞아서 쓰러짐 | **받음** — Animator `IsDown`(bool)을 켜면 재생, 누운 자세로 멈춤 | Standing Death Left 01 |
| Idle | 대기 | 대기(임시 모션) | |
| Air | 제자리 점프·떨어짐(공통) | **받음** — 14~34프레임(도약~착지), 게임 공중 시간 0.69초에 맞춰 재생 | Standing Jump |
| Air_F | 앞점프(공통) — 점프할 때 MoveZ > 0.5 | **받음** — 0~16프레임, 0.69초에 맞춰 재생 | Jump |
| Attack_HeavyCharge / Attack_Heavy | 강공격 모으기 / 내려치기 | 대기(임시 모션) | |
| Attack_AirStomp / Attack_AirCharge | 제자리 점프 공격 / 공중 모으기 | 대기(임시 모션) | |

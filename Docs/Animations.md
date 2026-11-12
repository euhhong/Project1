# 모션 목록 (Animations)

> 작성: PM 세션 / 최종 수정: 2026-11-11
> 모션 출처: **VARCO 3D** (플로우 `Identity5Fight`, Rig 노드 → Animate 노드). GameDesign Q9 확정.
> 키 이름은 VARCO 3D 애니메이션 카탈로그(전체 864개)에서 확인했다. `*` 표시는 PLUS 등급 모션이다. 계정 등급은 CREATOR이고, Rig·Animate는 0포인트다. PLUS 모션을 쓸 수 있는지는 첫 실행 때 확인한다. 쓸 수 없으면 "대체" 열의 무료 모션으로 바꾼다.

> **현재 상태 (2026-11-11):** VARCO 모션이 나오기 전까지 **임시 모션**이 연결되어 있다. FE가 Humanoid 근육 커브로 만들었다.
> - 임시 모션: 대기, 걷기 4방향, 달리기 4방향, 공중 (`Assets/Models/IdentityV/_Motions/Procedural/`)
> - Controller: `_Motions/CharacterLocomotion.controller`
> - VARCO 모션이 나오면 같은 Controller의 블렌드 트리 클립만 아래 1장의 키로 바꾼다.

## 1. M1 필수 모션

조작 규칙(GameDesign 2.1): 캐릭터는 항상 시야 방향을 바라본다. 그래서 A/D는 옆걸음, S는 뒷걸음이다.

| 상태 | 입력 | VARCO 키 (1순위) | 대체 (무료) | 루프 | 비고 |
|---|---|---|---|---|---|
| 대기 | 입력 없음 | `standing_idle_1_looping` | `standing_idle_2_looping` | O | |
| 걷기 앞 | W | `walk_looping` | `walk` | O | |
| 걷기 뒤 | S | `backward_walk` | — | O | |
| 걷기 왼쪽 | A | `left_sidestep`* | `walk_looping` 재사용 | O | 대체 시 몸은 앞을 보고 옆으로 미끄러지듯 보인다 |
| 걷기 오른쪽 | D | `right_sidestep`* | `walk_looping` 재사용 | O | 〃 |
| 달리기 앞 | Shift + W | `run` | `jog_looping` | O | 너무 빠르면 `jog_looping` |
| 달리기 뒤 | Shift + S | `backward_run` | — | O | |
| 달리기 왼쪽 | Shift + A | `left_side_run_1_looping`* | `run` 재사용 | O | |
| 달리기 오른쪽 | Shift + D | `right_side_run_1_looping`* | `run` 재사용 | O | |
| 점프 | Space | `jump_in_place` | `stand_jump_idle` | X | 제자리·걷기 중 점프에 사용 |

- 대각선(W+A 등)은 Blend Tree가 앞·옆 모션을 섞어서 처리한다. 대각선 전용 모션(`left_front_walk`* 등)은 M1에 넣지 않는다.
- 점프는 한 덩어리 클립이다. 체공 시간이 클립 길이와 맞지 않으면 FE가 재생 속도로 맞춘다. 도약·체공·착지로 나누는 작업은 M1 범위 밖이다.

## 2. 있으면 좋은 모션 (M1 이후 선택)

| 용도 | VARCO 키 |
|---|---|
| 달리면서 점프 | `run_jump`* |
| 착지 | `light_landing_1`* |
| 달리다 멈춤 | `run_stop_idle` |
| 전력 질주 (나중에 쓸 수도 있음) | `sprint`, `forward_sprint`* |

공격·피격 같은 전투 모션은 M3에서 공격 키와 액션 종류(GameDesign Q1·Q2)가 정해지면 고른다.

## 3. 적용 대상 캐릭터

**캐릭터 15명 전원이 Unity Humanoid다** (2026-11-11, FE).
- 9명(Gamekeeper, Hastur, HellEmber, Luchino, Ripper, Coordinator, Doctor, Forward, MindsEye): 원래 있던 3ds Max Biped 뼈대를 Humanoid로 전환했다(A포즈).
- 6명(Antonio, Geisha, SmileyFace, Cowboy, Mercenary, Thief): 사용자 지시로 Blender에서 Humanoid 뼈대와 가중치를 넣어 FBX를 교체했다.
- **변경 (2026-11-12, FE 커밋 e24b777):** Hastur는 촉수형이라 원본 뼈대를 쓰는 **Generic**으로 바꿨다. 사용자가 새로 만들 예정이다. 그래서 현재 Humanoid는 **14명**이고, Hastur에는 Humanoid 모션을 쓸 수 없다.
- 생존자 소품 4종(신호탄 총, 막대, 럭비공, 지팡이)은 오른손 뼈에 붙였다(커밋 da8afc2).

### 히트 판정용 부위 분리 (2026-11-12, FE 커밋 75d3aa3)
- 사람형 14명은 모델 안 메시가 부위별로 나뉘어 있다. 이름 규칙: `<캐릭터>_Head`, `_Body`, `_Hands`, `_Feet` (예: `Doctor_Head`).
- 소품은 `<캐릭터>_Prop`이고, 의사만 `Doctor_Syringe`다. 둘 다 오른손 뼈 아래에 있다.
- 실제 판정은 렌더 메시가 아니라 Collider로 한다(Tasks.md M3 참고).
- **판정 부위는 4개다 (2026-11-12 사용자 요청): Head / Body / Arms / Legs.** 위의 메시 이름(`_Hands`, `_Feet`)은 렌더용 분리 이름이라 판정 부위 이름과 다르다.
  - Humanoid 뼈마다 상자(BoxCollider)를 붙여 전신을 빈틈없이 덮는다(FE 커밋 024c99c). 의사 기준 Hurtbox 19개, Hitbox 5개다.

정리:
- 게임에 쓰는 모델은 **VARCO 리깅이 필요 없다.**
- VARCO 모션은 Humanoid 클립이라 Unity 리타겟으로 15명 모두에 쓸 수 있다. 모션은 한 번만 만든다.
- VARCO Animate는 VARCO Rig 노드의 결과만 입력으로 받는다. 그래서 **모션을 뽑기 위한 기준 모델 1명**은 VARCO에 올려 Rig해야 한다. 이 리깅된 모델은 모션 추출에만 쓰고 게임에는 쓰지 않는다.
- 기준 모델은 키와 체형이 메인 캐릭터와 비슷한 생존자를 추천한다(예: Thief). GLB는 `C:\Users\mbc\Desktop\VarcoUpload\`에 있다.

**메인 캐릭터: 생존자 의사(`Survivor_Doctor`)** — 2026-11-11 사용자 확정. 키 1.70m(Model 자식 스케일 3.865).

## 4. 작업 흐름

0. **사용자 (막힘 해결용)** — VARCO MCP에는 3D 파일 업로드 기능이 없다. 사용자가 VARCO 웹 에디터의 `Identity5Fight` 플로우에서 3DInput 노드에 GLB 1개를 직접 올린다. `C:\Users\mbc\Desktop\VarcoUpload\`에 있는 GLB를 쓴다.
1. **메인 세션** — VARCO 3D: 기준 모델 Rig(humanoid) → Animate 노드로 1장의 10개 모션 생성 → 다운로드 → Unity 임포트(`Assets/Models/IdentityV/_Motions/` 제안).
2. **FE** — Unity:
   - 임포트한 모션 클립의 Rig를 **Humanoid**로 설정하고 Avatar를 확인한다.
   - 루프 모션에 Loop Time을 켠다.
   - Animator Controller를 만든다. 구성: 대기 / 이동 Blend Tree(걷기·달리기 각 4방향) / 점프.
   - BE의 캐릭터 상태(BE-5)를 Animator 파라미터로 넘긴다.
3. VARCO 기준 모델(리깅된 GLB/FBX)은 모션 추출용이다. 게임 모델(`Assets/Models/IdentityV/`)을 대체하지 않는다. 씬 배치를 바꾸기 전에는 사용자에게 확인한다.

## 5. Animator 파라미터 (BE·FE 합의용 초안)

| 파라미터 | 타입 | 출처 (`ICharacterState`, BE-5) | 의미 |
|---|---|---|---|
| `MoveX` | float | `LocalPlanarVelocity` 방향 × 구간별 크기 (아래 참고) | 좌우 이동량. 걷기 1, 달리기 2.5 |
| `MoveZ` | float | 〃 | 앞뒤 이동량. `LocalPlanarVelocity`는 x가 오른쪽, y가 앞쪽 |
| `Speed` | float | `LocalPlanarVelocity.magnitude` | 수평 속력 (m/s) |
| `IsGrounded` | bool | `IsGrounded` | 땅에 닿았는지 |
| `Jump` | trigger | `Jumped` 이벤트 | 점프 시작 |

- **확정** (2026-11-11, BE·FE 합의). `WalkSpeed`와 `RunSpeed` 속성은 BE가 추가했다.
- **MoveX/MoveZ 공식 변경** (2026-11-12, FE 커밋 4b1929b). 방향은 `LocalPlanarVelocity`의 정규화 방향을 그대로 쓰고, 크기만 속력 구간별로 정한다.
  - 속력 0 ~ `WalkSpeed` → 0 ~ 1
  - 속력 `WalkSpeed` ~ `RunSpeed` → 1 ~ 2.5 (`CharacterAnimatorDriver.RunBlendValue` = 2.5)
  - 이유: 걷기·달리기 속도 비율이 바뀌어도(예: 2.5 → 1.8) 블렌드 트리의 걷기(±1)·달리기(±2.5) 지점에 모션이 정확히 맞게 하려는 것이다. 이전 공식(`LocalPlanarVelocity / WalkSpeed`)은 비율이 2.5일 때만 맞았다.
  - 좌우 이동 몸 틀기(`CharacterBodyTurn`)가 켜져 있으면, 몸을 튼 각도만큼 속도 방향을 돌려서 넘긴다. 몸을 틀 때 고개는 시선 쪽으로 90% 보정한다.
- 모션 느낌 (2026-11-12 사용자 요청): 걷기는 조깅 수준, 달리기는 전력질주. 걷기 속도 상향(2.6 → 3.6 m/s 제안)은 BE에 요청되어 있다.
- 그 밖에 쓸 수 있는 상태: `MoveState`(Idle/Walk/Run), `VerticalSpeed`(위가 +), `Landed` 이벤트, `LookYaw`, `LookPitch`(아래가 +, -30~60). 카메라 회전은 `Quaternion.Euler(LookPitch, LookYaw, 0)`이다.

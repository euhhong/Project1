# 작업 목록 (Tasks)

> 작성: PM 세션 / 최종 수정: 2026-11-11 (M1 범위 확장: 걷기·달리기·점프·시점 회전·3인칭 카메라)
> 설계 기준은 [GameDesign.md](GameDesign.md)를 따른다. 특히 2장 "확정된 내용"과 3장 "멀티 확장을 위한 구조 원칙"을 참고한다.

## 담당 범위

| 역할 | 폴더 | 내용 |
|---|---|---|
| BE | `Assets/Scripts/Core/`, `Assets/Scripts/Systems/` | 입력 데이터, 캐릭터 이동·전투 로직, 게임 상태 |
| FE | `Assets/Scripts/UI/`, `Assets/Scripts/Presentation/`, `Assets/UI/`, `Assets/Models/IdentityV/` | 카메라, UI, 애니메이션·연출, 캐릭터 모델 에셋 관리 |
| BG | `Assets/Environment/`, `Assets/Scripts/Environment/` | 맵·지형·배경 소품·조명·하늘 (2026-11-11 FE에서 분리, 커밋 접두어 `[BG]`) |
| 메인 | `Tools/`, `CLAUDE.md` | 공용 Unity 도구, 공통 규칙, VARCO 3D 파이프라인 |

- 공통 규칙은 `CLAUDE.md`를 따른다. Unity 6 API, 스크립트 하나에 역할 하나, 인스펙터 연결 최소화.
- 씬(`SampleScene.unity`)의 `IdentityV_Characters` 배치는 바꾸기 전에 사용자에게 확인한다.
- 다른 역할의 폴더를 고쳐야 하면 먼저 그 세션에 알린다.
- **Unity 확인과 커밋은 각 담당 세션이 직접 한다** (2026-11-11 사용자 지시).
  - 컴파일, 콘솔, Avatar, 씬 캡처는 메인 세션에 부탁하지 않는다. 각자 `Tools\Unity\unity.ps1`로 확인한다.
  - 작업이 끝날 때마다 자기 담당 파일만 `git add <경로>` → `git commit -m "[역할] ..."` → `git push`.
  - 자세한 규칙은 `CLAUDE.md`의 "Unity 에디터 확인", "Git" 섹션.
- **모델 방향 규칙** (2026-11-11 사용자 요청): 캐릭터 모델은 얼굴이 캐릭터 루트의 **앞(+Z)**을 보게 넣는다. 루트의 +Z는 이동·시야 방향이다. 담당은 FE(FE-7).
  - 새 모델은 리깅한 뒤 Unity에 넣기 전에 한 번 실행한다: `blender -b --python Tools/Blender/fix_model_facing.py -- <FBX 또는 폴더>`
  - 발끝 뼈 방향으로 앞뒤를 자동 판단해서 뒤집힌 모델만 돌린다. 여러 번 돌려도 안전하다. 발 뼈가 없는 모델은 "판단 불가"로 건너뛰니 직접 확인한다.
  - 씬 회전값으로 맞추지 않는다. 모델(FBX) 자체를 맞춘다.
- 이 원칙에 따라 Unity 확인이 필요한 작업은 **그 영역의 담당**에게 배정한다. 예: Avatar·모델은 FE, 이동 코드는 BE.

---

## M1. 메인 캐릭터 조작과 3인칭 카메라

**목표:** Play를 누르면 메인 캐릭터를 WASD로 걷고, Shift로 달리고, Space로 점프한다. 카메라는 캐릭터 등 뒤에 있고, 마우스 우클릭 드래그로 시점을 돌리면 캐릭터도 같이 돈다. 상태에 맞는 모션(대기·걷기·달리기·점프)이 나온다.

**완료 기준:**
1. W/A/S/D가 **카메라 기준** 앞·좌·뒤·우로 움직인다. 대각선 이동이 직선보다 빠르지 않다.
2. 이동 중 Shift를 누르고 있으면 달리기 속도로 바뀌고, 떼면 걷기로 돌아온다.
3. Space를 누르면 땅에 있을 때만 점프하고, 중력으로 내려와 착지한다.
4. 우클릭 드래그로 시점이 좌우·상하로 돌고, 캐릭터는 시야의 좌우 방향을 바라본다. 우클릭을 떼면 시점이 멈춘다.
5. 카메라는 회전해도 항상 캐릭터의 등 뒤에서 캐릭터를 비춘다.
6. 모션: 정지 → 대기, 이동 → 걷기, 달리기 → 달리기, 공중 → 점프. 모션 소스가 아직 없으면 5번까지만 완료로 보고 6번은 M1b로 넘긴다.
7. 키 입력, 캐릭터 이동, 카메라, 모션 코드가 각각 다른 스크립트에 있다. 콘솔 에러가 없다.

**설계 결정 (BE·FE 공통):**
- **시야 방향(lookYaw, lookPitch)은 입력 데이터에 포함한다.** 마우스 우클릭 드래그는 입력 소스가 읽어서 시야 각도로 쌓는다. 캐릭터(BE)는 이 값으로 몸을 돌리고 이동 방향을 계산한다. 카메라(FE)는 캐릭터의 시야 값을 **읽기만** 해서 등 뒤에 자리 잡는다.
  - 이유: 나중에 멀티가 되면 원격 플레이어의 시야 방향도 네트워크 입력으로 받아야 한다. 캐릭터가 로컬 카메라를 직접 읽으면 멀티에서 깨진다(GameDesign 3장 원칙 1·2).
- 기본값(조정 가능): 걷기 2 m/s, 달리기 5 m/s, 점프 높이 1.2 m, 마우스 감도는 설정값, Pitch 제한 -30°~60°.
- **메인 캐릭터: 생존자 의사(`Survivor_Doctor`)** — 사용자 확정. Biped 뼈대가 있어 Humanoid로 바로 전환할 수 있다. 씬 구조는 BE의 캐릭터 구조 규칙(빈 루트 + 모델 자식)을 따른다.

### BE 작업

> 메인 세션이 BE에 "WASD 이동 구현 착수"를 이미 전달했다. BE-1~3은 그 작업의 범위이고, 이번 확장으로 **달리기·점프·시야 방향**이 추가되었다.

| # | 작업 | 상태 | 비고 |
|---|---|---|---|
| BE-1 | **입력 데이터 구조** — `move`(Vector2), `run`(bool, 누르고 있는 중), `jump`(bool, 이번 틱에 눌림), `lookYaw`·`lookPitch`(float, 시야 각도). | 완료 (컴파일 OK) | `Core/`. 네트워크로 보낼 수 있게 단순한 값만 담는다. |
| BE-2 | **키보드·마우스 입력 소스** — 입력 인터페이스(예: `ICharacterInputSource`)와 New Input System 구현. WASD, Shift, Space, 우클릭을 누른 동안의 마우스 델타 → 시야 각도 누적(Pitch 제한 포함), 우클릭 중 커서 잠금. | 완료 (컴파일 OK) | 마우스 감도는 인스펙터 노출. 커서 잠금도 입력 소스가 맡는다. |
| BE-3 | **캐릭터 이동 로직** — 입력 데이터만 받아서 처리. 시야 Yaw 기준 이동 방향 계산, 걷기/달리기 속도, 대각선 정규화, 몸을 시야 Yaw로 회전. | 완료 (컴파일 OK) | 회전 기준이 "이동 방향"에서 "**시야 방향**"으로 바뀌었다. 키보드·카메라를 직접 읽지 않는다. |
| BE-4 | **점프와 중력** — 땅 체크, 땅에 있을 때만 점프, 중력 적용, 착지 처리. | 완료 (컴파일 OK) | `FixedUpdate` 기준. |
| BE-5 | **캐릭터 상태 노출** — 읽기 전용으로 공개: 이동 상태(Idle/Walk/Run), 땅에 닿았는지, 수직 속도, 현재 수평 속도(로컬 기준 x/z), 시야 Yaw/Pitch. 상태 변화 이벤트(점프 시작, 착지)도 둔다. | 완료 (컴파일 OK) | FE가 카메라(FE-1)와 모션(FE-3)에 쓴다. **FE와 이름을 먼저 맞춘다.** |
| BE-6 | **에디터 연결 안내** — 메인 캐릭터에 붙일 컴포넌트, Input Actions 설정(Move/Run/Jump/Look/LookHold), 물리 컴포넌트 설정을 단계별로 정리해 전달한다. | 완료 — 안내 대신 BE가 직접 씬 구성 | SampleScene: `Survivor_Doctor` 루트(CharacterMotor, LocalPlayerInput, CharacterController, CharacterAnimatorDriver) + `Model` 자식(스케일 3.865), `Ground`, Main Camera(ThirdPersonCamera). |

### FE 작업

| # | 작업 | 상태 | 비고 |
|---|---|---|---|
| FE-1 | **3인칭 뒤쪽 카메라** — 캐릭터의 시야 Yaw/Pitch(BE-5)를 읽어 등 뒤 일정 거리·높이에 자리 잡고 캐릭터를 바라본다. 부드러운 추적(약간의 감쇠). | 완료 (컴파일 OK) | `Presentation/Camera/ThirdPersonCamera.cs`. **Cinemachine 미사용** — 필요한 기능이 "시야 값 읽어 등 뒤 배치 + 감쇠"뿐이라 스크립트로 충분하고 패키지 의존을 늘리지 않는다. 벽 충돌이 필요해지는 M2 이후 재검토. |
| FE-2 | **메인 캐릭터 모델 준비 (Doctor)** — `Survivor_Doctor`를 Humanoid로 전환하고 Avatar 뼈 매핑을 확인한다(A포즈). 스케일, 피벗(발이 바닥에 닿는지), 머티리얼도 확인한다. | 완료 (Avatar 확인은 FE-6) | 15명 전원 Humanoid로 전환. Doctor: Biped 뼈대 완비, 발 피벗 z≈0, Model 자식 스케일 3.865 → 키 1.70m, 텍스처 3장 연결. |
| FE-3 | **모션 연결** — Animator Controller(대기 / 이동 Blend Tree 걷기·달리기 4방향 / 점프), BE-5 상태를 Animator 파라미터로 넘기는 스크립트. | 스크립트 완료 (컴파일 OK), **Controller 에셋은 클립 대기** | `Presentation/Animation/CharacterAnimatorDriver.cs`. Controller는 MAIN-1 → FE-4 이후에 만든다. 모션 목록과 파라미터는 [Animations.md](Animations.md). Apply Root Motion 끄기: **완료** — `CharacterAnimatorDriver`가 Awake에서 끈다(씬 수정 없음, 모델 교체에도 적용, 커밋 9bc0a07). 이동은 BE `CharacterMotor`가 코드로 한다. |
| FE-4 | **모션 임포트 설정** — VARCO 모션 클립의 Rig를 Humanoid로 설정하고 Avatar를 확인한다. 루프 모션은 Loop Time을 켠다. | 대기 | MAIN-1 이후. 임포트 위치 제안: `Assets/Models/IdentityV/_Motions/` (메인 세션과 협의 중). |
| FE-5 | **뼈 없던 6명 리깅 (Blender)** — Antonio, Geisha, SmileyFace, Cowboy, Mercenary, Thief에 Humanoid 뼈대와 가중치를 넣어 FBX 교체. | 완료 (사용자 지시) | 이로써 15명 전원이 Humanoid 리타겟 대상이다. |
| FE-6 | **Avatar 유효성 확인** — `unity.ps1`로 확인한다. `Survivor_Doctor` 우선, 가능하면 15명 전부. 확인 항목: Avatar isValid / isHuman, 필수 뼈 매핑, 씬 `Model`의 Animator에 Avatar가 들어가 있는지. | **완료** (2026-11-11, 메인 세션이 확인) | 15명 전원 Human, isValid·isHuman = True, 필수 뼈 15개 누락 없음(매핑 19~43개). 씬 `Survivor_Doctor/Model`의 Animator에 `Survivor_DoctorAvatar`가 들어가 있다. 재확인: `unity.ps1 run Library\ClaudeUnity\check_avatars.cs`. FE도 확인했다. 빠진 선택 뼈(Biped 9명 Neck, Blender 6명 Chest, SmileyFace RightToes)는 모션 재생에 영향이 없다. |
| FE-7 | **모델 앞뒤 방향 바로잡기** — 지금 의사 모델이 이동할 때 원하는 방향의 **정반대**를 보고 있다(사용자 보고). 모델이 캐릭터 루트의 앞(+Z, 시야 방향)을 보도록 180° 뒤집는다. **앞으로 넣는 모든 캐릭터 모델도 같은 방향으로 들어가게** 한다. | **완료** (커밋 c8e0dbd) | 15명 모두 Blender에서 뼈대·메시 최상위 노드를 Z축 180° 회전해 다시 내보냈다. 씬과 BE 코드는 수정하지 않았다. 재임포트 후 15명 Avatar valid·human, 발끝 +Z, 루트 앞에서 의사 얼굴 정면 확인, 콘솔 에러 0. 새 모델용 스크립트 `Tools/Blender/fix_model_facing.py` 추가(위 "모델 방향 규칙"). 주의: 씬에 옛 배치 모델이 남아 있다면 그쪽도 함께 돌아 보인다. |

### 메인 세션 작업

| # | 작업 | 상태 | 비고 |
|---|---|---|---|
| MAIN-1 | **VARCO 3D 모션 생성** — 기준 모델 1명 Rig(humanoid) → Animate로 [Animations.md](Animations.md) 1장의 10개 모션 생성 → 다운로드 → Unity로 임포트(`Assets/Models/IdentityV/_Motions/` 제안, FE와 협의). | **막힘** — 사용자 GLB 업로드 대기 | 게임에 쓰는 모델은 Blender 리깅(FE-5)으로 끝났다. 그래서 VARCO Rig는 **모션을 뽑기 위한 기준 모델용**으로만 필요하다(VARCO Animate는 VARCO Rig 결과만 받음). MCP로는 3D 파일을 올릴 수 없어 사용자가 VARCO 웹 에디터에서 GLB 1개를 올려야 한다. PLUS 모션이 막히면 대체 키를 쓴다. |

### 순서와 의존 관계

```
BE-1 → BE-2 → BE-3 → BE-4 → BE-5 ─┬→ FE-1 (최종 연결)
                                  └→ FE-3 (모션 연결) ← FE-4 (임포트 설정) ← MAIN-1 (VARCO 모션 생성)
                    BE-6 (씬 구성)은 BE-5 이후 — 완료
FE-6 (Avatar 확인) — 완료
MAIN-1 은 사용자의 VARCO GLB 업로드 후 시작
```

- **M1a (조작·카메라):** 완료 기준 1~5, 7. 모션 없이 끝낼 수 있다. → **구현·씬 구성 완료, 사용자 Play 확인 대기** (2026-11-11)
- **M1b (모션):** 완료 기준 6. VARCO 3D 모션(MAIN-1) → 임포트(FE-4) → 연결(FE-3).

---

## 다음 마일스톤 (미정 — GameDesign 4장 질문에 답이 오면 구체화)

- **BG-1. 아레나 맵 (M2와 같이 진행):** 지금의 임시 `Ground`를 대전 맵으로 교체한다. 담당 BG. 형태·크기·분위기는 GameDesign Q5·Q12에 따라 정한다. 답이 오기 전에는 BG가 배경 폴더·조명·하늘의 기본 틀만 잡아 둘 수 있다.
- **M2. 상대 등장:** 두 번째 캐릭터(AI 또는 허수아비) 배치. Q3에 따라 결정. 상대를 향한 록온 시점이 필요한지도 이때 정한다.
- **M3. 공격과 피격:** 공격 입력, 히트 판정, 체력. Q1·Q2에 따라 결정.
- **M4. 라운드와 승패:** 게임 상태 매니저, 승패 처리, HUD(체력바, 타이머). Q6에 따라 결정.
- **M5. 화면 흐름:** 타이틀, 캐릭터 선택, 결과 화면. Q7·Q10에 따라 결정.

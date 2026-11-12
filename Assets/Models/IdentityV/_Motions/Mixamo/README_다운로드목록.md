# Mixamo 모션 다운로드 목록 (FE 요청)

Mixamo(https://www.mixamo.com)는 Adobe 로그인이 필요해서 다운로드는 사용자가 직접 해 주세요.
받은 파일은 **이 폴더**(`Assets/Models/IdentityV/_Motions/Mixamo/`)에 그대로 넣으면 됩니다. 이름은 아래 "저장 이름"으로 바꿔 주세요.
FE가 Humanoid로 임포트해서 지금 쓰는 임시 모션(Procedural)과 교체합니다. 의사를 포함한 사람형 14명 모두에 리타겟됩니다.

## 다운로드 설정 (모든 모션 공통)
- 캐릭터: 기본(Y Bot)으로 둡니다. 캐릭터를 올릴 필요는 없습니다.
- **Format:** FBX for Unity (.fbx)
- **Skin:** Without Skin
- **Frames per Second:** 30
- **Keyframe Reduction:** none
- **In Place:** 아래 표에 "켬"이라고 적힌 모션은 반드시 체크합니다. 이동은 게임 코드가 하므로 모션이 앞으로 나가면 안 됩니다.

## 목록 (우선순위 순)

| # | 용도 | Mixamo 검색어 | 고를 모션(이름) | In Place | 저장 이름 |
|---|---|---|---|---|---|
| 1 | 대기 | idle | Breathing Idle | - | Idle.fbx |
| 2 | 걷기(앞) | walking | Walking (또는 Standard Walk) | 켬 | Walk_F.fbx |
| 3 | 뒤로 걷기 | walking backwards | Walking Backwards | 켬 | Walk_B.fbx |
| 4 | 옆걸음 왼쪽 | left strafe walking | Left Strafe Walking | 켬 | Walk_L.fbx |
| 5 | 옆걸음 오른쪽 | right strafe walking | Right Strafe Walking | 켬 | Walk_R.fbx |
| 6 | 달리기(앞, 전력질주) | sprint | Fast Run (또는 Sprint) | 켬 | Run_F.fbx |
| 7 | 뒤로 달리기 | running backward | Running Backward | 켬 | Run_B.fbx |
| 8 | 옆으로 달리기 왼쪽 | left strafe | Left Strafe (달리기 속도) | 켬 | Run_L.fbx |
| 9 | 옆으로 달리기 오른쪽 | right strafe | Right Strafe (달리기 속도) | 켬 | Run_R.fbx |
| 10 | 점프 중(공중) | falling idle | Falling Idle | - | Air.fbx |
| 11 | 약공격(오른손 찌르기) | stabbing | Stabbing (또는 Punching) | 켬 | Attack_Light.fbx |
| 12 | 강공격 모으기(반복) | standing idle 2h / taunt | Standing Melee Attack Downward의 시작 자세가 있으면 그것, 없으면 "Power Up" | 켬 | Attack_HeavyCharge.fbx |
| 13 | 강공격 내려치기 | melee downward | Standing Melee Attack Downward | 켬 | Attack_Heavy.fbx |
| 14 | 공중 발 내려찍기 | jump attack / stomp | Jump Attack (또는 Stomping) | 켬 | Attack_AirStomp.fbx |

- 1~10은 이동 품질을 크게 올려 주니 먼저 받아 주세요. 11~14는 공격 모션입니다.
- 여러 후보가 보이면 저장 이름의 용도에 가장 가까운 것을 고르면 됩니다. FE가 판정 구간(손·발이 맞는 순간)에 맞게 시간을 맞춥니다.
- 다 받은 뒤 FE 세션에 "받았어"라고만 알려 주세요.

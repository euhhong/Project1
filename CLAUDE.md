# Project1

## 환경
- Unity 6.4 (6000.4.0f1), URP
- 입력 시스템: New Input System
- 에디터: Visual Studio 2022

## 코드 규칙
- C# 스크립트는 `Assets/Scripts` 폴더에 만든다.
- Unity 6 기준 최신 API를 쓴다. (예: `GetInstanceID` 대신 `GetEntityId`, `FindObjectOfType` 대신 `FindFirstObjectByType`)
- 한 스크립트는 한 가지 역할만 맡게 나눈다.
- 인스펙터 연결은 최소화하고, 필요하면 사용자가 해야 할 에디터 작업을 단계별로 알려 준다.
- 코드를 수정한 뒤에는 무엇을 바꿨는지 짧게 알려 준다.

## 프로젝트 메모
- 메인 씬: `Assets/Scenes/SampleScene.unity`
- 캐릭터 모델: `Assets/Models/IdentityV/` (제5인격 공식 PMX → Blender로 FBX 변환, URP Lit 머티리얼)
  - 씬의 `IdentityV_Characters` 아래 `Hunters`(키 2.3m), `Survivors`(키 1.7m) 두 줄로 배치되어 있다.
  - 모델은 Generic 리그, 애니메이션 없음.

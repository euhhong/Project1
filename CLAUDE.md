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

## Unity 에디터 확인 (모든 세션이 직접 한다)
메인 세션에 부탁하지 말고 각 세션이 바로 실행한다. 연결은 모든 세션이 하나를 공유한다.
```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 console    # 콘솔 에러/경고
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 compile    # 다시 컴파일 + 결과 + 콘솔
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 run <file.cs>   # 에디터 C# 명령 실행 (internal class CommandScript : IRunCommand)
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 capture    # 씬 4방향 캡처 PNG (경로 출력, Read로 확인)
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\Unity\unity.ps1 tool <Unity_도구이름> '<json>'
```
- 코드를 고친 뒤에는 `compile`로 에러 0을 확인하고 나서 끝났다고 보고한다.
- 임시 `.cs` 명령 파일은 `Assets` 밖(예: `Library/ClaudeUnity/`)에 둔다. `Assets`에 두면 게임 코드로 컴파일된다.
- `relay_win.exe` 프로세스를 이름으로 종료하지 않는다. Unity 자체 relay까지 죽어 콘솔 에러가 난다.

## Git (작업 끝날 때마다 바로 커밋·푸시)
- 브랜치: `feat/identityv-models` 하나를 모든 세션이 같은 폴더에서 같이 쓴다. 브랜치를 바꾸지 않는다.
- 자기 담당 파일만 경로를 지정해서 올린다: `git add <내 파일/폴더>` (`git add -A`, `git add .` 금지 — 다른 세션의 작업 중인 파일이 섞인다). Unity `.meta` 파일도 같이 올린다.
- `git commit -m "[BE|FE|PM|MAIN] 무엇을 했는지"` → `git push`.
- push가 거절되면 `git pull --rebase --autostash` 후 다시 push. `index.lock` 에러는 다른 세션이 커밋 중인 것이니 몇 초 뒤 다시 시도.
- 담당 폴더: BE `Assets/Scripts/Core`, `Assets/Scripts/Systems` / FE `Assets/Scripts/UI`, `Assets/Scripts/Presentation`, `Assets/UI`, `Assets/Models`(캐릭터), 카메라 / BG(배경) `Assets/Environment`, `Assets/Scripts/Environment`, 맵·조명·하늘 / PM `Docs` / MAIN `Tools`, `CLAUDE.md`. 씬 파일(`Assets/Scenes`)은 수정한 세션이 커밋한다.

## 프로젝트 메모
- 메인 씬: `Assets/Scenes/SampleScene.unity`
- 캐릭터 모델: `Assets/Models/IdentityV/` (제5인격 공식 PMX → Blender로 FBX 변환, URP Lit 머티리얼). 씬에는 늘어놓지 않는다.
  - 모델 방향: 얼굴이 캐릭터 루트의 앞(+Z)을 보게 넣는다. 새 모델은 리깅 후 `blender -b --python Tools/Blender/fix_model_facing.py -- <FBX 또는 폴더>`로 맞춘다. 씬 회전값으로 맞추지 않는다.
- 메인 캐릭터: 생존자 의사(`Survivor_Doctor`) — 빈 루트 + Model 자식 구조.

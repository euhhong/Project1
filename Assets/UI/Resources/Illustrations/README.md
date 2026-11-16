# 캐릭터 일러스트 (캐릭터 선택 창 UI)

캐릭터 선택 창에서 쓰는 캐릭터별 일러스트. 파일 이름 = 캐릭터 ID(`CharacterRoster`의 `id`), PNG(배경 투명 또는 흰색).
게임에서는 `Resources.Load<Texture2D>("Illustrations/<ID>")`로 읽는다.
흰 배경 PNG를 넣으면 Unity가 자동으로 누끼를 따서 PNG 파일을 바꿔 저장한다(`Assets/Scripts/UI/Editor/IllustrationImporter.cs`, 메뉴 Tools/FE/일러스트 흰 배경 빼기).

| 캐릭터 | 파일 이름 | 받음 |
|---|---|---|
| 붉은나비(미치코) | Hunter_Geisha.png | ✅ (1402×1122, 흰 배경) |
| 안토니오 | Hunter_Antonio.png | |
| 사냥터지기(노루) | Hunter_Gamekeeper.png | ✅ (1462×1076, 흰 배경) |
| 노란옷의 왕 | Hunter_Hastur.png | ✅ (1402×1122, 흰 배경) |
| 공장장 | Hunter_HellEmber.png | ✅ (1402×1122, 원본 투명 배경 그대로) |
| 재앙의 도마뱀 | Hunter_Luchino.png | ✅ (1402×1122, 흰 배경) |
| 리퍼 | Hunter_Ripper.png | |
| 광대 | Hunter_SmileyFace.png | ✅ (1402×1122, 흰 배경) |
| 공군 | Survivor_Coordinator.png | ✅ (1402×1122, 흰 배경) |
| 카우보이 | Survivor_Cowboy.png | |
| 의사 | Survivor_Doctor.png | ✅ (1402×1122, 흰 배경) |
| 포워드 | Survivor_Forward.png | ✅ (1402×1122, 원본 투명 배경 그대로) |
| 용병 | Survivor_Mercenary.png | |
| 맹인 | Survivor_MindsEye.png | |
| 도둑 | Survivor_Thief.png | |

원본은 채팅 첨부(압축 WebP)가 아니라 원본 PNG 파일로 받는다 — 채팅 첨부는 화질이 떨어진다.

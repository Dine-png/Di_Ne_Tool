# 말풍선 튜토리얼

각 도구의 언어 선택 아래에서 **말풍선 튜토리얼 시작**을 누릅니다.
기능 탭이 있는 도구에서는 먼저 사용할 탭을 선택합니다. Lighting Designer는 튜토리얼 목록에서 안내할 기능을 선택합니다.

- 강조된 필드와 버튼을 따라 조작합니다.
- 이미 완료된 필수 항목과 선택 항목은 말풍선을 눌러 넘깁니다.
- 아직 준비되지 않은 필수 항목은 조작을 완료하면 넘어갑니다.
- **처음부터**로 다시 시작하고 **튜토리얼 종료**로 안내를 끝냅니다.
- 진행 상태는 현재 Unity 세션에 보관합니다. 다른 기능의 안내와는 별도로 보관합니다.

저장, 삭제, 가져오기, 내보내기, 변환은 선택 단계입니다. 안내만으로 에셋을 생성하거나 설정을 적용하지 않습니다.

## 적용 범위

Multi Dresser의 옷장·독립 토글, Avi Editor, Animation Tool, Material Tool, Screen Saver,
Lighting Designer와 프리셋 인스펙터, Smart Toggle, Opticore, Remove Mesh,
Shape Key Freezer, Toggle Animator, Asset Cleaner, Package Patcher와 압축 패키지 선택,
Extra Modifier, In-Game Checker, 텍스처 미리보기에 안내를 제공합니다.

설치본에 없는 기능은 추가하지 않고, 그 설치본에서 사용할 수 있는 기능을 안내합니다.

## 개발 확인

- 공통 진행 기능: `Editor/Core/DiNeGuidedTutorial.cs`
- 말풍선과 강조 영역: `Editor/Core/DiNeTutorialBubble.cs`
- 각 도구의 단계와 앵커: 해당 도구의 `*.Tutorial.cs`
- 회귀 검사: `Tests/MultiDresserPreview/Run-MultiDresserPreviewRegression.ps1 -TutorialOnly`

UI 표준 체크리스트: 기존 헤더와 브랜드 에셋 유지, 기존 카드·민트 강조 사용,
공유 `DiNeLang`과 3개 언어 지원, 짧은 조작 안내, GUI 상태 복원,
조건부 UI의 안내 유지, 스크롤 강조 영역과 입력 보존을 확인합니다.

# Di Ne Expression Editor

Di Ne Expression Editor는 VRChat의 `VRCExpressionsMenu`와 `VRCExpressionParameters` 에셋을 편집하는 Unity Inspector입니다. Di Ne Tool을 설치한 뒤 Project 창에서 해당 에셋을 선택하면 기본적으로 Di Ne Inspector를 사용합니다.

Di Ne의 공통 헤더, 민트 색상과 `DiNeLang` 설정을 사용하며 English / 한국어 / 日本語를 지원합니다. 업로드 창이나 Avatar Descriptor Quick Setup은 이 기능의 범위에 포함하지 않습니다.

## 시작하기

1. VCC에서 사용할 프로젝트에 Di Ne Tool을 설치하고 Unity를 엽니다.
2. VRCSDK+가 함께 설치되어 있다면 VCC의 **Manage Project**에서 제거합니다. 두 도구가 같은 Inspector를 교체하므로 동시 설치 상태는 지원하지 않습니다.
3. Project 창에서 기존 Expression Menu 또는 Expression Parameters 에셋을 선택합니다.
4. Inspector에서 내용을 편집합니다. 에셋 선택만으로 Avatar Descriptor나 Animator Controller의 참조를 바꾸지 않습니다.

씬 아바타의 메뉴나 파라미터를 편집하려면 Avatar Descriptor에 연결된 해당 에셋을 Project 창에서 선택하세요. 이 Inspector는 선택한 에셋을 직접 편집합니다. 원본과 별도로 작업하려면 먼저 에셋을 복제한 뒤 복제본을 선택하세요.

## Expression Menu

- 항목을 펼쳐 이름, 아이콘, 종류, 값과 파라미터를 편집합니다. Puppet 항목의 하위 파라미터와 레이블·스타일 등 SDK 필드도 편집할 수 있습니다.
- 항목을 추가·삭제하거나 위아래로 이동합니다.
- 항목을 복사·붙여넣기·복제하고, 선택한 다른 메뉴로 이동합니다.
- Sub Menu 에셋을 만들거나 연결된 메뉴로 이동하고, 뒤로 이동해 이전 메뉴를 다시 확인합니다.
- 파라미터를 찾아 선택하거나 Animator의 파라미터를 제안받습니다. 누락된 Expression Parameter는 사용자가 추가 작업을 실행할 때만 추가합니다.
- 메뉴의 8개 항목 제한, 항목의 파라미터 누락, Puppet 축의 파라미터 종류 불일치와 Sub Menu 연결 문제를 진단합니다.

메뉴 이동이나 파라미터 추가처럼 다른 에셋에 영향을 주는 작업은 대상 에셋을 확인한 뒤 실행하세요. 진단과 제안만으로 에셋을 자동 수정하지 않습니다.

## Expression Parameters

- 이름을 검색하고 항목을 추가·삭제하거나 위아래로 이동합니다.
- 종류, 기본값, Saved와 Synced 설정을 편집합니다.
- Animator의 파라미터를 제안받아 명시적으로 추가합니다.
- 다른 Expression Parameters 에셋에서 누락된 항목만 병합합니다. 같은 이름의 항목이 이미 있으면 기존 설정을 유지합니다.
- 빈 이름과 설정까지 동일한 중복 항목을 사용자가 정리 작업을 실행할 때 제거합니다. 설정이 다른 같은 이름의 항목은 임의로 합치지 않습니다.
- 동기화 파라미터 메모리 사용량과 SDK 제한을 확인합니다.
- 빈 이름·중복 이름, 기본값 범위와 선택한 아바타의 Animator 파라미터 불일치를 진단합니다.

표시되는 진단은 편집을 돕는 검사입니다. 최종 빌드에 NDMF나 Modular Avatar 등이 추가하는 파라미터는 SDK 빌드 결과에서도 확인해야 합니다.

## 기본 SDK Inspector로 돌아가기

상단 메뉴의 **DiNe → Expression Editor → Di Ne Inspector** 체크를 해제하면 공식 VRChat SDK Inspector를 사용합니다. 다시 체크하면 Di Ne Inspector를 사용합니다. 에셋을 선택하지 않아도 이 메뉴를 사용할 수 있습니다.

설정은 `EditorPrefs`의 `DiNeExpressionInspectorEnabled`에 저장하며 기본값은 켜짐입니다. 따라서 이 선택은 Unity 프로젝트의 에셋 파일을 수정하지 않고, 같은 컴퓨터의 다른 Unity 프로젝트에서도 공유될 수 있습니다.

VRCSDK+가 설치된 상태에서는 원본 도구가 Inspector를 강제로 교체할 수 있습니다. 이 경우 Di Ne 선택이나 기본 SDK 선택이 의도대로 적용된다고 보장하지 않습니다. VCC에서 VRCSDK+를 제거한 뒤 사용하세요. 감지되는 패키지 이름은 `dev.vrlabs.vrcsdkplus`, `com.dreadscripts.vrcsdkplus`, `com.awavr.vrcsdkplus`입니다. Console 충돌 경고는 현재 `DiNeLang` 언어로 Unity Editor 세션마다 한 번 기록하며, Di Ne Inspector의 충돌 안내는 설치된 동안 계속 표시합니다.

## 에셋 변경과 원본 참조

사용자가 실행한 에셋 편집은 Unity Undo를 지원하며, 변경된 에셋의 dirty 상태를 기록합니다. Inspector를 열거나 다시 그리는 동작, 파라미터 제안과 진단은 에셋을 변경하지 않습니다.

Avatar Descriptor의 FX Controller, Expressions Menu, Expression Parameters와 Multi Dresser의 Controller/Menu 참조를 자동 배정하거나 임시 생성물로 바꾸지 않습니다. Inspector를 통해 편집한 에셋을 아바타에 연결하는 작업은 별도로 사용자가 수행합니다. 기존 참조가 가리키는 에셋을 직접 편집하면 그 에셋을 공유하는 아바타에도 변경이 반영됩니다.

## 구현 출처와 호환성

이 기능은 공식 VRChat SDK 에셋 형식과 Unity Editor를 사용하는 Di Ne의 독립 구현입니다. VRCSDK+의 GPL 소스 코드, 아이콘, 스타일 또는 기타 에셋을 포함하거나 복사하지 않습니다. 원본 VRCSDK+를 포팅하거나 재배포하는 작업과 구분합니다.

Inspector는 `[CustomEditor]`로 등록합니다. Unity 2022.3에서 공식 SDK Inspector가 먼저 선택되는 경우를 처리하기 위해 Unity의 내부 Inspector 등록 캐시에 접근하여 **이미 등록된 Di Ne 자신의 레코드만 목록 맨 앞으로 이동**합니다. 공식 SDK나 다른 도구의 등록 레코드, Inspector 타입 필드와 캐시 초기화 플래그는 덮어쓰지 않습니다. 캐시가 아직 만들어지지 않았다면 메모리상의 임시 에셋으로 공개 `Editor.CreateEditor`를 호출하여 Unity가 초기화하도록 하고 임시 오브젝트를 즉시 제거합니다. 사용자 에셋 파일이나 씬 참조는 이 등록 과정에서 변경하지 않습니다.

이 우선순위 조정은 [Unity 2022.3의 Inspector 등록 구조](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/CustomEditorAttributes.cs)에 의존합니다. 등록 구조가 다른 Unity 버전에서는 자동 선택을 보장하지 않으며, 지원하지 않는 구조가 감지되면 현재 언어로 경고하고 기존 등록을 유지합니다. Unity 6 등 새로운 버전에서 캐시 구조가 바뀌면 대응 업데이트가 필요합니다. 열린 기본 Inspector는 공개 Editor Tracker API로 갱신하며, 잠긴 Inspector에 이전 화면이 남으면 에셋을 다시 선택하거나 해당 Inspector 창을 다시 열어주세요.

Di Ne Inspector를 꺼도 Di Ne의 래퍼 등록을 유지하여 알려진 공식 SDK Editor 타입으로 명시적으로 위임합니다. 플레이 모드나 읽기 전용 에셋에서는 공식 SDK Editor의 초기화가 에셋을 수정하지 않도록 편집이 잠긴 직렬화 필드를 표시합니다.

향후 VRChat SDK가 에셋 필드, 공식 Editor 타입 또는 Inspector UI 구조를 변경하면 대응 업데이트가 필요할 수 있습니다. 다른 패키지의 동일 대상 Inspector와의 우선순위도 보장하지 않습니다. SDK 기본 Inspector로 전환했을 때도 문제가 계속되면 설치한 SDK 버전과 해당 에셋을 확인하세요.

## UI 기준 확인

Inspector UI에 [Di Ne UI Standard](DI_NE_UI_STANDARD.md)의 완료 체크리스트를 적용했습니다.

- [x] 표준 Di Ne 아이콘, DungGeunMo 제목 폰트·크기와 설명 배치
- [x] English / 한국어 / 日本語 및 공통 `DiNeLang` 설정
- [x] 선택된 언어와 주요 작업의 표준 민트 색상
- [x] 카드 안의 설정 배치, 바로 보이는 주요 설정, 중복 브랜드 설정 없음
- [x] 안내·툴팁·경고·빈 상태·작업 결과의 세 언어 지원
- [x] SerializedProperty, Undo와 dirty 상태 및 리페인트 시 에셋 비변경
- [x] 기존 Di Ne Inspector의 헤더·언어 선택·카드와 실제 화면 비교

이 기능은 Editor 전용 ScriptableObject Inspector이므로 런타임 컴포넌트 아이콘과 컴포넌트 Prefab override는 적용 대상이 아닙니다.

## 검증 기록

Unity 2022.3.22f1의 격리 프로젝트에서 설치된 공식 SDK의 실제 DLL로 **23개 회귀 테스트**를 통과했습니다. 자동 Inspector 선택, 공식 SDK Inspector로 전환, 등록 우선순위의 반복 적용과 SDK 등록 보존, 복사·이동·병합·한도·Undo를 확인했습니다.

세 언어와 320 / 480 / 700 px 폭의 **45개 실제 Inspector 레이아웃·리페인트 검사** 및 **6개 실제 화면 캡처**를 확인했습니다. 언어·Inspector 설정은 테스트 전 값으로 복원했고, 사용자 아바타 프로젝트나 씬은 열거나 수정하지 않았습니다.

테스트 범위와 재실행 방법은 [테스트 README](../Tests/ExpressionEditor/README.md)에 기록했습니다. 이 검증은 아바타 업로드·Play Mode·VCC 배포 설치 검증을 포함하지 않습니다.

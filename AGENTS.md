# Di Ne Tool repository instructions

## Mandatory UI standard

Before creating or changing any Unity Editor UI, read and follow [Docs/DI_NE_UI_STANDARD.md](Docs/DI_NE_UI_STANDARD.md).

This requirement applies to both of the following:

- creating a new tool, `EditorWindow`, custom inspector, popup, or component;
- adding a feature, setting, panel, tab, button, status display, or localization string to an existing tool.

New functionality must be integrated into the existing tool's Di Ne visual language. Do not introduce a locally invented header, font, color palette, toolbar, language selector, card style, or component icon when the repository standard already defines one.

An Editor UI task is not complete until the UI-standard checklist in the linked document has been reviewed. Functional implementation alone is not sufficient.

## Unity 테스트 후 원본 참조 복원 (필수)

- 테스트 전에 아바타의 FX Controller, Expressions Menu, Expression Parameters와 Multi Dresser의 Controller/Menu 원본 참조를 기록한다. 경로·GUID와 `null`, FX 기본 레이어 및 커스텀 설정 여부까지 보존한다.
- 플레이 모드·업로드용 생성 데이터는 임시 복사본에만 만든다. 사용자가 원본 교체를 명시적으로 요청하지 않았다면 원본 에셋을 변경하거나 임시 생성물을 원본으로 배정하지 않는다.
- 임시 참조를 배정한 테스트는 성공·실패·취소·도메인 리로드·Unity 재시작 후에도 원본으로 되돌려야 한다. Avatar Descriptor뿐 아니라 Multi Dresser의 참조도 함께 복원한다.
- 작업 종료 전에 **실제 열린 씬과 저장된 씬 모두** FX·메뉴·파라미터 및 Multi Dresser 참조가 기록한 원본과 일치하는지 확인한다. `__Temp` 참조나 Missing 상태가 남아 있으면 완료로 보고하지 않는다.
- 디스크 복원 매니페스트가 있으면 해당 기록을 사용한다. 원본이 확인되지 않으면 임의의 에셋이나 이름 추측으로 덮어쓰지 않고 복원 기록과 임시 에셋을 보존한다.
- 원본 참조 복원을 검증한 뒤에만 임시 세션 에셋을 정리한다. 실제 Editor 확인이 불가능하면 그 한계와 남은 복원 상태를 명시한다.

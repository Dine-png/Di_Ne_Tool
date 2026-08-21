# Di Ne Tool repository instructions

## Mandatory UI standard

Before creating or changing any Unity Editor UI, read and follow [Docs/DI_NE_UI_STANDARD.md](Docs/DI_NE_UI_STANDARD.md).

This requirement applies to both of the following:

- creating a new tool, `EditorWindow`, custom inspector, popup, or component;
- adding a feature, setting, panel, tab, button, status display, or localization string to an existing tool.

New functionality must be integrated into the existing tool's Di Ne visual language. Do not introduce a locally invented header, font, color palette, toolbar, language selector, card style, or component icon when the repository standard already defines one.

An Editor UI task is not complete until the UI-standard checklist in the linked document has been reviewed. Functional implementation alone is not sufficient.

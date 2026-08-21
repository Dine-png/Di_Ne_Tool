# Di Ne Tool UI Design Standard

This document is the mandatory visual and interaction standard for Di Ne Tool's Unity Editor UI. The current reference implementations are:

- `Packages/com.dinetool.avatar-tools/Editor/MultiDresser/UI/DiNeMultiSupporter.cs`
- `Packages/com.dinetool.avatar-tools/Editor/LightingDesigner/UI/DiNeLightingDesignerEditor.cs`

When the two references differ, prefer the Lighting Designer implementation for newly written inspectors and the Multi Dresser implementation for large editor windows.

## Scope

Apply this standard whenever:

- a new Di Ne tool or component is created;
- an existing tool receives a new feature or setting;
- a new custom inspector, `EditorWindow`, popup, tab, panel, or status section is added;
- an icon or any user-facing text is added or replaced.

Adding a feature to an existing tool does not permit a separate visual style. The new UI must look like it was part of that tool from the beginning.

## Brand assets and icons

- Load package assets through `DiNePackageAssets`; do not depend on an absolute project path.
- Use `DungGeunMo.ttf` for the large tool title.
- Use `Assets/DiNe.png` as the standard header brand icon.
- Runtime components must use `Assets/DiNe_Icon.png` as their MonoImporter/component icon unless a reviewed tool-specific component icon exists.
- Tool-specific VRChat menu icons belong under `Assets/<ToolName>/` and should receive stable `.meta` files and GUIDs.
- Supply sensible icons automatically. Do not add redundant global **menu name** or **menu icon** fields merely for branding. Expose them only when changing them is an actual user workflow requirement.
- Functional rows may use small icons when they improve recognition, but icon size, alignment, and visual weight must remain consistent across sibling rows.

Component icon example:

```yaml
MonoImporter:
  icon: {fileID: 2800000, guid: f3a9c2b1d4e5678901234567890abcde, type: 3}
```

The GUID above belongs to the package's `Assets/DiNe_Icon.png`. Preserve the real asset GUID instead of generating an unrelated duplicate.

## Standard header

Every main tool window and major custom inspector uses the same identity block:

1. A boxed header at the top.
2. The 72 × 72 Di Ne icon on the left of the title.
3. A centered title using `DungGeunMo.ttf`, bold, 36 px.
4. A short localized description centered below the title, 12 px, word-wrapped.
5. The language selector immediately below the header.

Reference values:

| Token | Value |
|---|---:|
| Header GUI background | `(0.90, 0.90, 0.90, 1.00)` |
| Header icon | `72 × 72` |
| Icon/title gap | `6 px` |
| Title size | `36 px`, bold |
| Description size | `12 px` |
| Description text | `(0.80, 0.80, 0.80, 1.00)` |
| Header-to-language spacing | `5 px` before, `15 px` after |

Use the feature name as the title. Do not replace the title with an editable menu-name field or display a second redundant name/icon configuration block below the header.

## Language support

- All new user-facing UI must support `English`, `한국어`, and `日本語` in the same change.
- Use the shared `EditorPrefs` key `DiNeLang` so switching language in one Di Ne tool is reflected in the others.
- The button order is always `English`, `한국어`, `日本語`.
- Place the three-button language toolbar directly below the header; standard height is `35 px`.
- Localize titles, labels, buttons, tooltips, descriptions, help boxes, warnings, empty states, confirmations, and status text.
- Shader property names, parameter identifiers, filenames, and other technical identifiers may remain untranslated when translation would make them inaccurate.
- Do not mix hard-coded Korean-only text into an otherwise localized panel.

## Colors and controls

Use the standard segmented toolbar and action hierarchy:

| Role | Value |
|---|---:|
| Selected tab / primary mint | `(0.30, 0.82, 0.76, 1.00)` |
| Unselected tab | `(0.50, 0.50, 0.50, 1.00)` |
| Selected text | white, bold |
| Unselected text | `(0.80, 0.80, 0.80, 1.00)` |

- Use mint for the selected tab or the principal action in a section.
- Use normal Unity buttons for secondary actions.
- Use red/destructive emphasis only for genuinely destructive actions.
- Restore `GUI.backgroundColor`, `GUI.color`, `GUI.enabled`, indentation, and label width immediately after a custom scope.
- Prefer `EditorStyles` and existing helper methods over one-off `GUIStyle` definitions.
- Recommended heights are `24 px` for compact actions, `30 px` for normal actions, and `35 px` for main segmented tabs.

## Layout and information architecture

- Group related settings in `GroupBox` or `EditorStyles.helpBox` cards.
- Give every card a bold section title. Add a short word-wrapped explanation when the effect is not self-evident.
- Use approximately `8 px` vertical spacing between major cards and `3–5 px` within a closely related control group.
- Align sibling fields and buttons. Avoid arbitrary widths unless they prevent unstable layout.
- Keep essential settings visible. Do not hide routinely used item settings behind a separate **Settings** button.
- Use Simple/Advanced tabs only when the amount or risk of configuration justifies them. Simple contains the safest, most common workflow; Advanced contains optional detail, not duplicated controls.
- Put diagnostics and current status at the bottom of the workflow, close to the action they validate.
- Remove implementation-oriented clutter such as supported-shader and raw-parameter columns from the normal user flow. Report compatibility through concise diagnostics or tooltips instead.
- Avoid repeating the tool name, menu name, or icon in multiple cards.

## Existing-tool feature additions

When extending an existing tool:

1. Identify the existing header, localization helper, color constants, toolbar helper, card style, and status area before writing UI.
2. Place the feature in the existing workflow and appropriate Simple/Advanced section.
3. Reuse the tool's existing helpers and spacing. If the helper is missing, follow this document rather than creating a new visual language.
4. Add all three translations and tooltips together with the feature.
5. Reuse the component and menu icon rules. Do not introduce a second branding block.
6. Extend diagnostics/status output when the feature has prerequisites or can fail silently.
7. Preserve serialized-property, Undo, Prefab override, and dirty-state behavior.

## Unity editor behavior

- Custom inspectors must use `serializedObject.Update()` and `ApplyModifiedProperties()` correctly.
- User-triggered object or asset changes must support Undo where Unity permits it.
- Mark changed objects/assets dirty and save assets only when the operation requires it.
- Preserve Prefab override behavior by using `SerializedProperty` for editable component fields.
- Do not make visual refresh code mutate user data every repaint.
- Keep Play Mode and upload/build behavior visible in the status text when a component is applied non-destructively through NDMF or Modular Avatar.

## Definition of done

Before completing any new-tool or UI-feature task, verify all applicable items:

- [ ] Standard Di Ne icon, title font, title scale, and description layout are used.
- [ ] The component script has the correct Di Ne component icon.
- [ ] English, Korean, and Japanese are complete and use `DiNeLang`.
- [ ] Selected tabs and primary actions use the standard mint color.
- [ ] New controls are placed in the correct existing card or mode.
- [ ] No redundant menu-name/icon block or unrelated style was introduced.
- [ ] Common settings are visible without an unnecessary extra settings button.
- [ ] Tooltips, warnings, empty states, and status messages match the same language and style.
- [ ] Serialized properties, Undo, Prefab overrides, and dirty state remain correct.
- [ ] Existing nearby UI was compared visually and structurally before handoff.

If any checked item is intentionally not applicable, document the reason in the implementation or handoff instead of silently diverging.

# Avi Editor regressions

Run from the repository with Unity 2022.3.22f1 installed:

```powershell
./Tests/AviEditor/Run-AviEditorRegression.ps1 -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe'
```

The runner copies the actual Avi Editor scripts and shared helpers into the ignored
`.codex_tmp/AviEditorRegression` project. It imports no assets into an avatar project.
It copies the editor's `System.Collections.Immutable.dll` for the existing armature
mapper. `SdkTypeStubs.cs` supplies only the Modular Avatar scale component's type and
`Scale` property. The suite verifies the tool's capture/apply and Undo behavior, not
the real Modular Avatar build pipeline or runtime behavior. No VRChat descriptor or
Multi Dresser component is created, and no actual avatar scene/reference is opened.
The runner restores the preset picker preference keys after the suite finishes.
Both the MA adapter and preview lifecycle probe are compiled outside the isolated
project's `Editor` directory so Unity can attach them to fixture objects.

The suite checks:

- One armature preset captures live direct scale/rotation/position values alongside
  MA scale and the child-position option, survives asset reimport, and drops obsolete
  captured entries when overwritten. Stale editor caches cannot replace live values.
- Full combined apply and repeated apply restore mapped absolute child positions
  without double MA adjustment, while unmapped children retain requested adjustment.
- Partial direct scale/rotation/position and MA loads preserve unrequested values.
- One Undo/Redo operation restores direct transforms, newly added MA components and
  the stored child-position option together; invalid/null/unknown/missing parts skip.
- Original direct-only dictionaries and schema-1 MA presets remain selectable and
  load through the unified picker without changing the other kind of scale data.
- Persisted scale deformation via `SkinnedMeshRenderer.BakeMesh` at weights 0/50/100,
  with an unrelated active shape retained and the original mesh unchanged.
- A generated 737-key mesh with `笑い` at index 688: 70% preview, persisted core Apply,
  window cleanup and asset reload, with other active keys preserved. This exercises
  the core and post-Apply methods; it does not simulate the IMGUI Apply button click.
- Multiple shape frames, delta normals/tangents, all eight UV channels and bounds.
- Consecutive saved edits, Undo/Redo, and isolation between equal mesh names.
- Prefab mesh override persistence after saving and reopening a scene.
- Real camera pixels from the isolated preview, excluding unrelated scene geometry;
  edited deformation, direct renderer targets, unchanged source weights and properties,
  unchanged scene roots/dirty flag, no behavior cloning and complete disposal.
- During `Camera.onPreCull`, the tool camera has a parent in its private preview scene
  and a render-texture target. These properties exclude it from cached proxy replacement
  under the source-reviewed NDMF `ProxyManager.ShouldHookCamera` predicate installed
  during this investigation. This is a camera contract test, not full NDMF integration.
- The actual editor window's shared row-selection handler: selecting B removes A's
  temporary override, and clicking selected B again toggles preview off. Both zero
  and nonzero source weights remain intact; switching/toggling disposes scaled meshes.
  Explicit mix entries still accumulate, and preview never alters source weights.
- Replacement preview matches the persisted replacement at weights 0/50/100 while
  other source shape keys remain active.
- Expression and FX clip preview coverage has moved to `Tests/AnimationTool`.
- Head framing using posed mesh geometry instead of enlarged culling bounds, named
  eye-bone focus, source weight preservation and scaled world-space bounds.

Graphics remain enabled to exercise `Camera.Render` and render-texture readback.
Results are written to `.codex_tmp/AviEditorRegression/AviEditorRegression-results.txt`;
the full compilation/editor log is beside it. `-PrepareOnly` prepares without launching
Unity (also pass the editor path to copy the Immutable assembly). `-PackageSource`
and `-ProjectName` can select a different source snapshot and isolated project.

## UI standard review

Reviewed against `Docs/DI_NE_UI_STANDARD.md` and the existing Avi Editor sections:

- The unified armature preset controls share the existing preset card, picker helper,
  localization helper and mint primary action in both edit modes. All added labels,
  tooltips, legacy-data explanation and load status have English/Korean/Japanese
  translations. The existing header, `DiNeLang` toolbar and component icons are
  unchanged; this feature adds no runtime component. Structural review and actual
  armature EditorWindow pixel inspection are complete.
- The unified preset regressions and all existing cases passed together in Unity
  2022.3.22f1: 16 tests, zero failures. Undo and partial-apply behavior are verified
  on synthetic objects; actual Modular Avatar build integration is outside the suite.
- The existing Di Ne header assets/font, shared `DiNeLang` selector, mint actions and
  existing cards/modes are retained; no new branding or component UI was introduced.
- The new preview explanation has English, Korean and Japanese translations, uses
  the existing localization helper and word-wrapped label style, and stays beside
  the existing controls. Component icon rules are not applicable to this editor-only
  preview helper.
- Both existing face previews reuse the same toolbar and now remain above their
  control scroll areas. Their size responds to window dimensions, up to 360 pixels.
- Undo, prefab overrides and source dirty-state behavior are exercised above.
- The camera output is rendered and checked on the GPU. A separate isolated UI probe
  used Unity 2022.3's internal `GUIView.GrabPixels` render-texture capture in hidden
  batch mode to inspect eight actual armature windows: both edit modes in English,
  Korean and Japanese at 420 × 850, plus both Korean modes at 300 × 850. The unified
  preset card fits and its descriptions wrap correctly. The existing narrow MA
  child-position toggle can truncate its label outside the new preset card.
  Screenshots and capture diagnostics are retained in
  `.codex_tmp/AviEditorRegression/AviEditorUiCapture/`. The probe restored and verified
  `DiNeLang` and both picker preference roots, and removed its synthetic avatar and
  preset. The separate face-preview layout checks described above remain structural.

## Arkveld framing verification

The open Unity 2022.3.22f1 `D:/Arkveld` scene was inspected using a temporary,
read-only editor probe (removed after verification). Its humanoid Avatar does not
map either eye, although `Eye_L` / `Eye_R` transforms exist. The prior mapping guard
skipped these eyes when head and arms were mapped. Body culling bounds also extended
to about 1.99 m, yielding a 0.409 m head size and focus at 1.23 m.

With eye fallback and posed-vertex bounds, focus is at the eyes (about 1.05 m), with
head size 0.148 m. The actual Avi Editor expression preview was visually checked
before and after in the open project: the face now fills and centers in the preview.
The framing fix was applied to both the repository and the project's older package
without replacing its other features. The existing Di Ne header, language selector,
colors, controls and layout remain unchanged; all UI-standard checklist items were
reviewed, with new controls/translations/component icons inapplicable to this
calculation-only change. No scene, source transform, mesh, or expression values were
edited by the diagnostic probe.

## Animation Tool split

Animation and expression authoring now live in `DiNe/Animation Tool`. Avi Editor
retains armature, mesh shape-key and PhysBone editing. Preview rendering and head
framing are shared under `Editor/Core`; the runner copies these helpers and the
existing guided tutorial implementation. Serialized editor mode IDs and preset
asset types remain stable. New Animation Tool coverage lives in `Tests/AnimationTool`.

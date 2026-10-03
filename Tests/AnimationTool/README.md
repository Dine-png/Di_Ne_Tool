# Animation Tool regressions

```powershell
./Tests/AnimationTool/Run-AnimationToolRegression.ps1
./Tests/AnimationTool/Run-AnimationToolRegression.ps1 -RealSdkProjectPath 'D:/Tool_Test' -ProjectName AnimationToolRealSdkRegression
```

The runner uses Unity 2022.3.22f1 at `E:/Unity/2022.3.22f1/Editor/Unity.exe`
by default. `-PrepareOnly` copies the source without launching an editor.
`-PackageSource`, `-UnityEditorPath` and `-ProjectName` select an alternate source
snapshot/editor/isolated project. Each run acquires a project-specific lock and
checks that no Unity process is already using that isolated project.

The production Animation Tool and retained Avi Editor scripts, shared
preview/framing/tutorial helpers and humanoid mapper are compiled. Source copies and optional
real SDK assemblies have SHA256 manifests and are checked again after execution.
Graphics remain enabled to exercise the private preview camera and IMGUI.

The default run uses small VRChat schema adapters. The optional real SDK run
reuses `Tests/ExpressionEditor/Prepare-RealSdkAssemblies.ps1` to read installed
VRChat DLLs and copy their dependency closure into the isolated project. It does
not open, import, save or enter Play Mode in the SDK project. The Multi Dresser
fixture is always a reference-only adapter; menu generation and build integration
are outside this suite.

The real-DLL entry point runs the 18 functional cases and actual OnGUI
Layout/Repaint checks synchronously, then exits. Its DLL-only environment lacks
the full SDK package metadata, VPM and XR loader setup: SDK initializers and save
hooks still log `VRCPackageSettings`/`VRCConstraintManager` errors. These logs are
retained, and a passing assertion report does not mean a clean SDK startup or a
full SDK GUI integration pass. An attempted asynchronous SDK UI capture failed
on those initializer errors. The 22 final GPU UI captures use the schema-adapter
environment; asynchronous capture is deliberately skipped in the real-DLL run.

All assets and synthetic saved scenes live under
`.codex_tmp/<ProjectName>/Assets/AnimationToolCase_*`. Reports, logs and source/DLL
hash manifests remain under the same isolated project. The runner restores
`DiNeLang` and both Avi preset-picker preference roots after the suite. It never opens a user avatar scene.

The suite checks:

- Renamed object paths, blendshape names and material object-reference curves;
  complete weighted curve keys, wrap modes, animation events, clip settings and
  frame rate survive. Originals remain unchanged in memory and on disk.
- Simultaneous cross mappings for both float and object curves, untouched
  destination overwrites, many-to-one collisions and conflicting source rows.
- Float/PPtr destination-kind mismatches are rejected in both manually entered
  and JSON-imported mappings.
- Empty root paths, same leaf names in different branches, ambiguous identical
  sibling paths, missing components/shapes/material slots and unverified fields.
- Nested controller state machines/blend trees and effective override clips;
  JSON mapping profiles, Unicode/empty paths and disabled rows.
- Independent repaired assets, later modifications and asset reimport.
- Detached animation and expression previews: source transforms, shape weights,
  materials, mesh data and clips remain unchanged. SDK/dresser/custom behaviours
  are excluded. Source lifecycle/event callbacks remain unchanged.
- Source and reopened synthetic-scene FX controller, Expressions Menu, Expression
  Parameters and Multi Dresser Controller/Menu references: path, GUID, null state,
  FX default flags and customizeAnimationLayers are preserved.
- Camera GPU pixels, private scene/parent/target contract, source scene dirty
  state, normal scene count, active render target and preview disposal.
- The actual Animation Tool OnGUI receives English/Korean/Japanese Layout and
  Repaint events for Preview, Expression and Repair tabs. Expression loading
  preserves unmentioned values; explicit Apply/Restore restores source pose.
- Explicit Apply/Restore on a synthetic prefab instance records transform and
  shape overrides, supports Undo/Redo, marks the scene dirty, retains original
  prefab asset bytes and preserves avatar/dresser references after scene reload.
- Expression overwrites retain unrelated mesh/transform/material curves and clip
  metadata. Nested FX BlendTree leaf replacement changes only its selected slot
  and supports Undo/Redo. Repaired save workflows write only changed clips,
  mapping tables and reports; transient repair previews have an owned lifetime.
  Expression/FX save does not persist unrelated dirty assets. Session/reload and
  play-mode cleanup callbacks are invoked directly; the suite never enters Play Mode.
- The retained Avi Editor Armature/ShapeKey/Extra tabs receive all three languages'
  Layout/Repaint events. Twenty-two actual Unity GUIView PNG captures (three tabs ×
  three languages × two tools, plus 420px English/Japanese and Korean lower-control cases) are saved under `AnimationToolUiCapture` for visual
  review. The suite checks nonblank pixels and source-reference preservation;
  final layout approval requires inspecting those PNGs.

Animation Tool captures use its actual native window. Avi Editor captures render
its actual `OnGUI` inside the same test host used for the event checks: the direct
native Avi GUIView returned a uniform buffer in this batch-mode environment.
The test host delegates the unchanged production controls and captures their GPU
pixels; it does not recreate the layout or draw replacement controls.

UI capture writes a disk preference-restoration manifest before changing shared
language/preset preferences. Cleanup restores and verifies them even after a
capture failure; a later run recovers a manifest left by an interrupted editor.

## UI standard review

Reviewed against `Docs/DI_NE_UI_STANDARD.md`, the retained Avi Editor and the
existing Di Ne window helpers. The source and actual rendered controls were
checked together:

- [x] Existing Di Ne icon assets and DungGeunMo title font; 72px icon, 36px bold
  title, 12px description and standard header spacing.
- [x] Shared `DiNeLang`, English/Korean/Japanese selector, 35px segmented tabs,
  mint selection and primary actions. FX field labels are localized.
- [x] Existing helpBox cards, bold section titles, visible common controls,
  localized tooltips/empty states/warnings, and diagnostics beside the relevant
  bottom actions. No extra branding or separate settings flow was introduced.
- [x] Actual EN/KO/JP Layout/Repaint checks for all three tabs of both windows;
  visual comparison with retained Avi controls, including lower Repair/Expression
  workflows and 420px minimum-width Animation Tool views.
- [x] Undo/Redo, prefab overrides and scene/asset dirty behavior exercised on
  synthetic objects. Passive rendering preserves scene dirtiness and refs;
  explicit Apply marks the scene dirty; expression/FX saves leave unrelated
  dirty assets unsaved.
- [x] Component script icon: not applicable. This change adds an EditorWindow
  and editor-only helpers, with no new user-facing runtime component.

The Avi expression cases moved to this suite with the Animation Tool split;
`Tests/AviEditor` continues to cover the retained armature/shape-key/PhysBone tool.
The Animation Tool GUIView is captured directly. Avi pixel review uses the
unchanged actual OnGUI through the host fallback described above.

## Recorded verification

Unity 2022.3.22f1 final-source runs under `.codex_tmp` completed with:

- `AnimationToolRegression`: 18 functional/event cases plus the 22-capture case,
  zero assertion failures; 22 production/branding files verified by SHA256.
- `AnimationToolRealSdkRegression`: 18 functional/event cases, zero assertion
  failures; 22 production/branding files and 31 installed SDK/dependency DLLs
  verified by SHA256. Descriptor, menu and parameters resolve to `VRCSDK3A`.
  SDK initializer errors remain in its log as explained above.
- Each tool processed 54 Layout and 27 Repaint events across three tabs and
  three languages. Source synthetic refs matched live and reloaded scenes.
- Language and preset preferences were restored and verified to each final
  run's entry snapshot. This does not reconstruct an unknown preference from
  an earlier interrupted harness run; any separately chosen language setting
  is a preference change, not original-state restoration.

Reports are `AnimationToolRegression-results.txt`; source and optional DLL
identities/hashes are in `AnimationToolRegression-source-hashes.json` and
`AnimationToolRegression-sdk-hashes.json`. All final captures are in the
schema-adapter project's `AnimationToolUiCapture` directory.

`AnimationToolUiGeometry-results.txt` records a targeted four-capture follow-up.
The requested position is applied again after `ShowUtility`, since the first
native utility-window show can restore its previous 440px geometry. The final
EN/JP minimum-width views have EditorWindow, GUIView and root layout widths of
420px; the Korean lower-control views are 440px. All four captures passed and
preferences were restored. The final EN 420px avatar refresh/card edges fit.

This is an isolated regression suite. Its results do not establish the state of
any currently open or saved user scene, nor full VRChat/NDMF/Modular Avatar build
integration. Actual user scenes and their references are not touched.

# Expression Editor regression

These tests compile the production Expression Editor scripts in an isolated
Unity 2022.3.22f1 project at `.codex_tmp/ExpressionEditorRegression`. They never
open an avatar project, edit its assets, assign generated data to an avatar, or
stop another project's Unity process. `D:/Tool_Test` is only a read source for
installed official SDK assemblies and editor resources.

Run from the repository root in PowerShell:

```powershell
& 'Tests/ExpressionEditor/Run-ExpressionEditorRegression.ps1'
& 'Tests/ExpressionEditor/Run-ExpressionEditorRegression.ps1' -CaptureUiOnly
```

Use `-PrepareOnly` to inspect the project without launching Unity and
`-DescribeOnly` to emit the real SDK type/field schema. `-UnityEditorPath`,
`-SdkProjectPath`, `-PackageSource`, and `-TimeoutSeconds` can override the defaults.
The runner uses a project lock, starts its own Editor hidden in batch mode, polls
in five-second intervals, and terminates only that process if its bound expires.
Graphics remain enabled for actual Inspector rendering and render-texture capture.

## Official SDK inputs

The seeds are the installed assembly identities `VRCSDK3A`, `VRC.SDK3A`, and
`VRC.SDK3A.Editor`; `VRC.SDK3.Avatars` is a namespace. The menu, parameter, and
AvatarDescriptor types are confirmed at runtime to originate from `VRCSDK3A`,
instead of test stubs. Only their managed dependency closure is copied. The
official SDK editor's Resources are copied so its native Inspector can be tested.
The copies live in a test-only embedded package so the SDK's package settings can
resolve their assembly. Nothing in this test package is part of Di Ne's release.

VRCSDK+ source, assets, and assemblies are not inspected, copied, or referenced.
Assembly candidates whose name contains `vrlabs` or `vrcsdkplus` are excluded
before metadata loading, and the dependency traversal rejects those identities.

SHA-256 manifests record every production source/brand asset, SDK assembly, and
SDK resource copy. The runner checks the original and the compiled copy again
after the run, and fails if production inputs changed during verification.

## Coverage

The 23 regression cases cover:

- Real SDK provenance and absence of VRCSDK+.
- Control deep copying, independent parameter/label arrays, null entries, and
  retained Unity asset references.
- Eight-control menu limits, invalid input, ordering, add/remove Undo/Redo, atomic
  cross-menu moves, and one transaction restoring both assets.
- Submenu traversal through cyclic graphs without mutation.
- Parameter copying, first-source duplicate preference, preservation of existing
  conflicts, source non-mutation, and independent copied data.
- Atomic 256-bit budget rejection, exact-limit success, unsynced parameters,
  serialized empty-slot cost, and the SDK's total parameter-count limit.
- Cleanup that removes exact duplicates/empty entries while retaining conflicting
  same-name definitions; add/merge/cleanup Undo/Redo and intentional `isEmpty` state.
- Actual FX/root/override controller parameter lookup, deduplication, sorted
  results, excluded Trigger/type conflicts, and unchanged avatar references.
- Refusal to mutate read-only files.
- Natural startup selection of both Di Ne inspectors through
  `Editor.CreateEditor(asset)` without an explicit test registration call.
- Actual native SDK fallback creation for both asset types; registry idempotence,
  preservation of all record fields and identities, and no multi-editor changes.
- The actual production IMGUI across 320/480/700-pixel widths, all three languages,
  and empty menu, Puppet menu, empty parameters, populated parameters, and a
  damaged null parameter array: 45 layout/repaint cases with no asset mutation.

The damaged-data observation records that the SDK can throw for a null parameter
array. Unity serialization can normalize a null entry to an empty slot, which the
installed SDK charges eight bits. Budget tests preserve that cost instead of
silently discarding the slot.

## Visual capture and retained evidence

`-CaptureUiOnly` hosts the production `CreateInspectorGUI()` root in an isolated
test window and binds it to the actual Inspector's serialized object. After ten
Editor update frames, `GUIView.GrabPixels` renders that window into a render
texture. It never reads desktop pixels or captures another Unity project.

The six 480 × 850 PNGs show menu and parameter inspectors in English, Korean, and
Japanese. The capture requires varied pixel data and verifies that rendering did
not mutate either synthetic asset. Capture preferences are saved to a disk
manifest, restored and checked at completion, and recovered on a subsequent
capture run if a previous process was interrupted. Both the synthetic assets and
window are destroyed at completion.

Evidence remains under `.codex_tmp/ExpressionEditorRegression`:

- `ExpressionEditorRegression-results.txt`: the 23-case result and registry metadata.
- `ExpressionEditorRegression-ui-layout.txt`: 45 measured layout rows.
- `ExpressionEditorRegression-ui-capture-results.txt`: capture and preference restoration.
- `ExpressionEditorUiCapture/*.png`: actual offscreen Inspector pixels.
- `ExpressionEditorRegression-*-hashes.json`: exact input manifests.
- `ExpressionEditorRegression.log`: the most recent Unity run.

The minimal SDK DLL test environment can emit SDK Dynamics scheduler initialization
exceptions (`VRCConstraintManager.ScheduleExecutionJobs`), because it does not
import the complete avatar SDK project setup. Those are distinct from the tested
Inspector drawing paths. This harness verifies editor asset operations and UI; it
does not exercise avatar Play Mode, upload, build processing, or a VCC release
installation. No user avatar/controller/menu/parameter reference was modified,
so no user scene restoration was required.

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

VRCSDK+ source, assets, and assemblies are not included in this regression project
or production package. The installed SDK+ 1.6.2 assembly is used separately as a
visual reference in `.codex_tmp/VRCSDKPlusUiReference`; that isolated GPL reference
project never enters the release or this runner.
Assembly candidates whose name contains `vrlabs` or `vrcsdkplus` are excluded
before metadata loading, and the dependency traversal rejects those identities.

SHA-256 manifests record every production source/brand asset, SDK assembly, and
SDK resource copy. The runner checks the original and the compiled copy again
after the run, and fails if production inputs changed during verification.

## Coverage

The suite contains 27 regression cases covering:

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
- Refusal to mutate read-only files, including inspector initialization in both
  modes without creating the SDK editor.
- Natural startup selection of both Di Ne inspectors through
  `Editor.CreateEditor(asset)` without an explicit test registration call.
- Enabled SDK+ style IMGUI roots and disabled actual native SDK roots. Native
  `ControlsListView` / `ControlOptionsContainer` and `ParametersListView` /
  `MemoryBar` remain present for valid assets only in SDK-only mode.
- Empty parameter initialization with both values of `isEmpty`, in both modes,
  without SDK default population or a changed empty flag.
- Registry idempotence, preservation of all record fields and identities, and no
  multi-editor changes.
- Connected production actions: pending serialized name edits before smart menu
  duplication, Copy/Paste as new, atomic cross-menu Move/Place, parameter
  duplication/footer Add, name cleanup, append merge preserving duplicate names,
  explicit Controller Add, and menu inline Add beyond the network memory limit
  with Saved/Synced defaults. Each mutation supports Undo, preserves source
  copies, and leaves avatar controller/menu/parameter references unchanged.
- Descriptor Quick Setup on a synthetic nonhumanoid avatar with named eye
  transforms and a Blink mesh: eye/eyelid binding, view placement, descriptor
  Undo, and unchanged FX flags/controller/menu/parameter references.
- SDK+ advisory controller lookup retains Trigger names and conflicting types
  through override chains. Name cleanup keeps those names while deleting
  unknown/blank names, with Undo and unchanged source references.
- Actual `CreateInspectorGUI()` roots across 320/480/700-pixel widths, all three
  languages, and both modes: empty menu, valid Puppet menu, intentional empty
  parameters, pristine empty parameters, populated parameters, a damaged null
  parameter array, and malformed Puppet fields. This produces 126 layout/repaint
  cases. Each case waits for Editor updates and checks content height and asset
  equality after initialization, binding and repaint. Enabled roots use one
  IMGUI container with compact rows and right-side delete buttons; native lists
  and memory bars remain single instances in SDK-only mode. Invalid SDK data
  uses serialized fallback in that mode. Actual wide English mouse events click
  menu/parameter row delete and parameter footer plus buttons, then Undo restores
  the assets. Old extension foldouts must be absent in both modes.

The damaged-data observation records that the SDK can throw for a null parameter
array. Unity serialization can normalize a null entry to an empty slot, which the
installed SDK charges eight bits. Budget tests preserve that cost instead of
silently discarding the slot.

## Visual capture and retained evidence

`-CaptureUiOnly` hosts the production `CreateInspectorGUI()` root in an isolated
test window and binds it to the actual Inspector's serialized object. After at
least 24 Editor update frames and one second, `GUIView.GrabPixels` renders that
window into a render texture. Blank initial frames are retried within a
120-second bound. It never reads desktop pixels or captures another Unity project.

The capture writes fifteen 700 × 900 PNGs: menu and parameter inspectors in
English, Korean, and Japanese, with six SDK-only references, six enabled SDK+
style full inspectors, and three compact menu inspectors. `*-sdk-*.png` files
show the official SDK; `menu-*.png`, `parameters-*.png` and
`menu-compact-*.png` show the enabled UI. The first Puppet row is selected
explicitly. Varied pixel data, mode-specific inspector shape, compact row-side
button geometry and unchanged synthetic asset data are required.
The menu fixture has a separate transient parameter lookup with matching Bool
and Float definitions. A synthetic Reference Avatar and FX controller provide
detected types and the matching five-row parameter fixture includes explicit
missing-controller and blank-name examples, as in the actual SDK+ reference.
The lookup, FX default flag, controller data and menu/parameter references are
verified unchanged during rendering and restored before fixture cleanup.

Both the layout run and capture save global language, inspector and compact preferences
to separate disk manifests, restore and verify them at completion, and recover
their manifest on a subsequent run if the previous process was interrupted.
Synthetic assets and windows are destroyed at completion.

Evidence remains under `.codex_tmp/ExpressionEditorRegression`:

- `ExpressionEditorRegression-results.txt`: suite results and registry metadata.
- `ExpressionEditorRegression-ui-layout.txt`: completed layout case measurements.
- `ExpressionEditorRegression-ui-capture-results.txt`: capture and preference restoration.
- `ExpressionEditorUiCapture/*.png`: actual SDK-only and SDK+ style offscreen
  Inspector pixels.
- `ExpressionEditorRegression-*-hashes.json`: exact input manifests.
- `ExpressionEditorRegression.log`: the most recent Unity run.

Counts above describe current test coverage; a completed run and its result files
confirm whether those checks passed for the compiled production source.

The minimal SDK DLL test environment can emit SDK Dynamics scheduler initialization
exceptions (`VRCConstraintManager.ScheduleExecutionJobs`), because it does not
import the complete avatar SDK project setup. Those are distinct from the tested
Inspector drawing paths. This harness verifies editor asset operations and UI; it
does not exercise avatar Play Mode, upload, build processing, or a VCC release
installation. No user avatar/controller/menu/parameter reference was modified,
so no user scene restoration was required.

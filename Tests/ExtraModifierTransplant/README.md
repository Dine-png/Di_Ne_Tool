# Extra Modifier prefab transplant regressions

Run the actual transplant utility in an isolated Unity 2022.3.22f1 editor:

```powershell
./Tests/ExtraModifierTransplant/Run-ExtraModifierTransplantRegression.ps1 -CompileWindow
```

The default editor is `E:/Unity/2022.3.22f1/Editor/Unity.exe`; override it with
`-UnityEditorPath`. `-PrepareOnly` prepares without starting Unity. `-PackageSource`
and `-ProjectName` select another package snapshot and isolated project folder.
The runner holds an exclusive project lock and rejects an editor already using
that folder, so concurrent runs cannot share a Unity project.

The ignored `.codex_tmp/ExtraModifierTransplantRegression` project compiles only
the production `DiNePrefabTransplantUtility.cs`, the test harness, and small real
MonoBehaviour probe types. It creates all fixtures through Unity APIs; no avatar
project, original prefab, FBX, or scene is modified.

The utility suite contains **28 tests** covering the direct-target contract:

- Read-only analysis and input snapshots, including the scene dirty flag and
  analyze/execute totals.
- Target static/skinned mesh assets, materials, renderer settings and bone
  references, plus components on a matched mesh host and target-only references.
- Independent collision-only meshes, retargeted rendered collision geometry and
  preservation/clearing when the old rendered mesh has no revised counterpart;
  target Animator Avatar retention and missing-Avatar warnings.
- Multiple components of one type, typed references, sibling cycles, nested
  lists/arrays, external scene references and actual scene save/reload.
  UnityEvent persistent targets/arguments are also remapped, and OnValidate never
  observes transient references into the original hierarchy.
- Real native colliders and ParentConstraint sources, weights, axes and offsets.
- The exact second scene object receives the copied components. Every original
  target root, bone, renderer host and target-only node retains its local/world
  transforms, name, parent, sibling, scene, active state, tag, layer and static
  flags. Serialized Transform data is compared except the allowed new-child list;
  existing RectTransform layout fields remain exact. No new outfit clone is
  created and there is no transform-copy option. Only newly created helpers
  inherit original prefab local TRS and object settings under their mapped target
  parent, including RectTransform helpers with different parent layout dimensions.
- Excluded missing mesh hosts, dangling references and their hidden descendant
  chains; exact duplicate sibling paths, count ambiguity and unique bone fallback
  under translated, rotated and nonuniformly scaled target parent wrappers.
  Existing target placement remains exact across different scene parents and
  moved bones. New helper subtrees retain original local TRS under the revised
  hierarchy. A uniquely named moved renderer host receives nonmesh components
  while keeping its revised geometry and placement.
- Real synchronous OnEnable/OnValidate callbacks deliberately alter original
  target roots, matched bones, target-only nodes and UI layouts; final state is
  repaired while component configuration remains enabled. An active-only linked
  prefab callback records its own overrides; target state survives Undo/Redo and
  scene save/reload. New helper Reset/OnValidate/OnEnable callbacks move their own
  local TRS, and final placement is restored to the original prefab values.
- The reported Option_C_R raw quaternion is stored through SerializedObject to
  reproduce Unity Transform setter normalization: raw
  `(0.3933636, 0.2968083, 0.429588854, -0.756718934)` becomes
  `(0.393363535, 0.296808273, 0.4295888, -0.7567188)`, so raw Quaternion.Equals is
  false. Full transfer preserves orientation and copied configuration; a real
  0.01-degree changed rotation is rejected. An actual helper callback changes q
  to -q and remains valid. Only new-helper rotation comparison accepts this tiny
  normalized orientation difference; helper position/scale and all preexisting
  target snapshots remain exact. A preexisting raw imported quaternion survives
  a real mutation callback byte-for-byte.
- Automatic collider dependencies and recursive mesh dependencies declared with
  RequireComponent, plus cyclic SerializeReference graphs and aliases across
  top-level fields, embedded Object references, private serialized values, Bounds,
  AnimationCurve and Gradient data, with input and OnValidate preservation.
- Source prefab assets stay byte-identical. Persistent prefab/model asset targets
  are rejected with TargetAssetInput; linked scene prefab/model instances are
  modified directly and retain links. Nested prefab links, recorded overrides,
  scene save/reload and one-step Undo/Redo are checked through Unity APIs.
- Undo/Redo restores the same target object and valid references. A genuine
  DisallowMultipleComponent subclass conflict rolls back the destination without
  destroying it. Its saved-clean scene becomes dirty after rollback; the harness
  records this conservative policy rather than clearing the dirty flag.
- Rejected invalid inputs leaving the scene untouched.

`-CompileWindow` also compiles the actual main window, transplant panel, existing
VRM helper implementations and shared asset loader, and adds **2 tests** for
complete English/Korean/Japanese text, matching format placeholders, the shared
`DiNeLang` preference, every literal diagnostic code emitted by the utility,
absence of the obsolete transform option/selection controls, and the real
window's deferred action. The actual window queues exactly one EditorApplication
delayCall without changing objects/report/selection immediately, captures its
assigned inputs, and cancels on OnDisable or Undo. The harness invokes only that
window's queued delegate outside OnGUI, leaving unrelated editor callbacks alone.
In the SDK-free mode, `WindowCompilationAdapters.cs` supplies unrelated
Focus-panel compile names for VRCAvatarDescriptor, IEditorOnly and FindSettings.
The real SDK mode below supplies the actual VRChat types and only adapts the
unrelated Focus processor lookup. The transplant core and panel always compile
from their actual production implementations.

## Real installed SDK regressions

```powershell
./Tests/ExtraModifierTransplant/Run-ExtraModifierTransplantRegression.ps1 -RealSdkProjectPath 'D:/Mew Project' -CompileWindow
```

This adds **13 tests**, for **43 total** with the window checks. The runner copies
the existing project's compiled/runtime assemblies and their managed dependencies
into the separate `.codex_tmp/ExtraModifierTransplantSdkRegression` project. It
does not edit that avatar project or define substitute SDK components. The tested
installation is VRChat SDK **3.10.5** and Modular Avatar **1.18.3**.

- Actual VRCPhysBone and VRCPhysBoneCollider values, curve/string fields, two
  remapped internal colliders, an external collider, a cleared removed-mesh
  collider, ignoreTransforms and rootTransform reach the exact second target.
  Existing target components are reused, Undo/Redo restores the destination,
  and a real PhysBone prefab source stays byte-identical. A created real
  VRCPhysBoneCollider host also uses the reported raw Option_C_R quaternion.
- All six installed VRC constraint kinds (Parent, Position, Rotation, Scale,
  Aim and LookAt) copy nondefault native fields and **18 sources** each, including
  inline slots and overflow entries. Internal transforms point to the second
  target, external transforms stay external, and the actual Sources Count/indexer
  agrees with the copied serialized data.
- Actual MA MergeArmature and ScaleAdjuster retain their configuration, including
  BidirectionalExact armature-lock mode and the private scale field. MA
  AvatarObjectReference cases cover a path-only old-outfit reference, an empty
  disabled path, a stale direct object outside the avatar, an external avatar
  reference, and a destination without an avatar ancestor. The actual MA Get
  method verifies destination resolution. Source snapshots stay unchanged during
  each operation; the real source SetLockMode is invoked before the snapshot.

Every SDK Execute also checks the same exact original-target transform/object
snapshots. Nondefault target root and bone transforms are used for PhysBone,
VRC constraint and MA cases so accidentally restoring original prefab placement
would fail the test while copied functional settings remain enabled.

The isolated runtime-only SDK fixture registers the actual VRCAvatarDescriptor
type in NDMF RuntimeUtil.AllRootTypes, reproducing installed NDMF PlatformRegistry
root registration without loading its editor/build framework. Burst compilation
is disabled with its own `--burst-disable-compilation` option because copied
managed assemblies do not include the package's native JIT installation. Unity
logs an ignored OnParticleUpdateJobScheduled signature message in this setup.
These fixture differences do not replace SDK components, SerializedObject,
constraint source APIs or MA reference resolution.

Results are written to
`.codex_tmp/ExtraModifierTransplantRegression/ExtraModifierTransplantRegression-results.txt`;
the full compilation/editor log is beside the report. SDK mode writes the same
report name under `.codex_tmp/ExtraModifierTransplantSdkRegression`.
The runner records SHA-256 hashes of every copied production source in
`ExtraModifierTransplantRegression-source-hashes.json`, checks the copied files,
and rejects a passing run if the production source changes during verification.
Real SDK mode also records and rechecks every copied SDK DLL against its source
in `ExtraModifierTransplantSdkRegression-assembly-hashes.json`.

Verified on 2026-09-30 using Unity **2022.3.22f1** with the combined real SDK and
`-CompileWindow` command: **43/43 tests passed**, process exit code 0, all
**9 production source copies** and **16 real SDK DLL copies** matched their source
SHA-256 hashes before and after the run. The rollback
case intentionally logs Unity's rejected incompatible-component error and the
utility's caught exception before confirming complete rollback.

Limits: the SDK mode verifies actual SDK serialization, references and direct
target transactions. It does not run VRChat PhysBone/constraint simulation, MA
editor lock updates across frames, NDMF builds, avatar upload or Blender imports.
Immediate serialized source preservation and PhysBone prefab bytes are verified;
arbitrary future SDK callbacks are outside this evidence. The model fixture is
a generated OBJ imported as Unity's real Model prefab; it does not test FBX
exporter versions or a user's clothing asset. The hidden batch editor does not
click the Extra Modifier IMGUI button or screenshot its layout; the real queued
execution is tested programmatically outside a GUI frame. Test-only
probes do not introduce production component icons.

## UI standard review

The integrated panel was reviewed against every applicable item of
`Docs/DI_NE_UI_STANDARD.md` and structurally compared with the Multi Dresser and
Lighting Designer references. It uses the existing DiNePackageAssets loader,
72 px brand icon, 36 px title font, 12 px description, and shared DiNeLang toolbar
with English/Korean/Japanese in that order, 35 px height and 15 px spacing. The
existing box/section helpers and 30 px mint primary action are reused; essential
fields remain visible, with no redundant branding block. Tooltips, status and
diagnostics have all three translations. The 400 px minimum window width and
existing nearby layout were checked structurally.

The two input ObjectFields use the full available row width. The obsolete
selection shortcut buttons and transform-copy toggle were removed; the localized
rule text explains that existing target transforms stay intact and only new
helpers inherit original local TRS. Transfer and subsequent selection updates run
after the drawing event through EditorApplication.delayCall; inputs are disabled
while that action is pending. This keeps component callbacks and rollback outside
the active IMGUI layout.

The actual window/panel compile and localization coverage are verified by
`-CompileWindow`. Undo, dirty state, prefab handling and input preservation are
covered by real Unity tests. A rendered EditorWindow screenshot remains
unverified in this hidden batch harness. Production component icons and custom
inspector serialized-property editing are inapplicable to the editor-only
transplant utility/window addition.

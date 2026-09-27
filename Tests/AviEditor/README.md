# Avi Editor regressions

Run from the repository with Unity 2022.3.22f1 installed:

```powershell
./Tests/AviEditor/Run-AviEditorRegression.ps1 -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe'
```

The runner copies the actual Avi Editor scripts and shared helpers into the ignored
`.codex_tmp/AviEditorRegression` project. It imports no assets into an avatar project.
It copies the editor's `System.Collections.Immutable.dll` for the existing armature
mapper. `SdkTypeStubs.cs` supplies only the unrelated Modular Avatar scale component's
type and `Scale` property; these tests do not verify Modular Avatar behavior.

The suite checks:

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
- Expression and FX clip previews retaining unmentioned keys and source weights.
- Head framing using posed mesh geometry instead of enlarged culling bounds, named
  eye-bone focus, source weight preservation and scaled world-space bounds.

Graphics remain enabled to exercise `Camera.Render` and render-texture readback.
Results are written to `.codex_tmp/AviEditorRegression/AviEditorRegression-results.txt`;
the full compilation/editor log is beside it. `-PrepareOnly` prepares without launching
Unity (also pass the editor path to copy the Immutable assembly). `-PackageSource`
and `-ProjectName` can select a different source snapshot and isolated project.

## UI standard review

Reviewed against `Docs/DI_NE_UI_STANDARD.md` and the existing Avi Editor sections:

- The existing Di Ne header assets/font, shared `DiNeLang` selector, mint actions and
  existing cards/modes are retained; no new branding or component UI was introduced.
- The new preview explanation has English, Korean and Japanese translations, uses
  the existing localization helper and word-wrapped label style, and stays beside
  the existing controls. Component icon rules are not applicable to this editor-only
  preview helper.
- Both existing face previews reuse the same toolbar and now remain above their
  control scroll areas. Their size responds to window dimensions, up to 360 pixels.
- Undo, prefab overrides and source dirty-state behavior are exercised above.
- The camera output is rendered and checked on the GPU. The surrounding IMGUI window
  layout was compared structurally; an actual EditorWindow screenshot remains
  unverified in this hidden batch-mode harness. Unity 2022's GUIView exposes RenderDoc
  capture methods but no direct render-to-texture window API used by this harness.

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

# Multi Dresser preview regression

This isolated Unity 2022.3 project compiles the production dresser and custom
inspector. It sends real IMGUI Layout/Repaint events to an editor window hosting
that inspector, and counts the NDMF invalidation calls through test doubles.
It does not load or modify an avatar in the user's open project.

```powershell
./Tests/MultiDresserPreview/Run-MultiDresserPreviewRegression.ps1 `
  -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe'
```

Use `-InspectorSource <path>` and its matching `DiNeMultiSupporter.Tutorial.cs`
to compare an alternative inspector against the same tests. `-TutorialSource
<path>` overrides the companion partial source. `-ProjectName <name>` chooses
an isolated project under `.codex_tmp`.
`-TutorialOnly` runs the eight tutorial cases without the other preview/toggle
regressions or their newer inspector dependencies. Optional phases are read from
the production tutorial enum, so an older inspector without Independent Toggles
receives the complete tutorial coverage appropriate to its own workflow.
The runner writes `MultiDresserPreviewRegression-results.txt` and the Unity log
inside that project. The benchmark reports timing without a
machine-dependent pass/fail threshold.

The Bool-toggle MMD tests use a real AnimatorControllerPlayable with 0, 1, 2,
3 and 5 original FX layers. They switch grouped and standalone targets before,
during and after disabling zero-based FX layers 1 and 2, and check repeated
generation and original layer order. For the optional MA build-intent check,
pass `-MmdLayerControlSource <path-to-MA/Runtime/ModularAvatarMMDLayerControl.cs>`.
This compiles MA's actual behaviour source and verifies the opt-out flag after
removing original layers; it does not execute the full MA/NDMF build pipeline
or reproduce a VRChat world. Without that input, the behaviour check is not
applicable and is reported in the Unity log.

The MMD fix shares the wardrobe protection with independent and Smart Toggle
layers and reserves missing FX slots for avatars with only Bool toggles. It
changes generated animation data only; the UI-standard checklist adds no
applicable visual changes, settings or localization strings.

## Regression

The affected inspector ended the per-item `BeginChangeCheck` twice. Unity's
`EndChangeCheck` reports a change when its stack is empty, so an idle active
preview called `RefreshPreview` on each repaint. That invalidated the NDMF cache
and requested another repaint, sustaining the cycle.

On Unity 2022.3.22f1, the original inspector invalidated and flushed caches 36
times during 24 Layout / 12 Repaint events without input. Both the updated
repository inspector and the minimal installed-package hotfix made zero idle
invalidation calls and passed all five tests. Shape synchronization with 20
outfits and 320 blendshapes averaged 21.384 ms per call before and 0.935 ms after
over 120 iterations; this measures that method, not avatar frame rate.

Keep one change-check pair around the complete item. Cache invalidation remains
necessary when preview state actually changes: the drawer edits objects without
Undo, and Modular Avatar can otherwise display stale preview geometry.

The tests cover idle drawing, preview application and restoration of object
activation, blendshape weights and materials, clearing an absent preview, and
shape-name reconciliation after mesh/target changes. NDMF, VRChat and unrelated
package services are test doubles; this is not an end-to-end Modular Avatar
rendering or avatar frame-rate benchmark.

The tutorial regressions check that required stages already valid on entry can
advance by clicking their bubbles, while incomplete stages require the actual
action. Scene bindings, category names, non-default avatar descendants and a real
preview snapshot must stay valid until the queued transition commits. Preview
restoration requires the original scene state and cleared restoration resources;
changing only preview indices cannot finish the stage. Optional steps advance on
bubble activation, including real mouse events. Starting preserves existing
preview state; stopping or disabling the inspector restores activation, blendshape
weights and materials. Reopening resumes an interrupted preview instruction, and
stopping clears the tutorial session.
The empty-category all-OFF action is clicked through real mouse events, creates
only an empty default slot, keeps the outfit stage required, and supports Undo/Redo.

The production inspector receives real Layout/Repaint events at every tutorial
stage in English, Korean and Japanese, at 360, 540 and 900 pixel widths. The checks
assert that idle tutorial drawing does not advance stages, mark avatar settings
dirty, mutate serialized configuration or preview states, or invalidate NDMF
caches. Empty material setup is checked at both Renderer and slot instructions;
the Add action remains highlighted, with no row or material created by drawing.
A test-only scroll host mirrors the Inspector's scrolling behavior.
The spotlight checks ensure dim rectangles remain inside the inspector, exclude
the active control and bubble, and do not overlap. A real click on a dimmed sibling
control still works. Drawing preserves GUI colors, enabled/changed state, matrix,
indentation and label width; inactive and cleared frames retain no stale overlay.
These tests do not read desktop pixels or generate screenshots.

## UI-standard review

The earlier preview fix changes preview work and shape-list synchronization only. Existing
header, icons, fonts, colors, layout, controls, translations and language
preferences are retained; the corresponding visual checklist items require no
new UI. Shape values and recording flags remain intact when names survive a
mesh change. Existing serialized-property, Undo and prefab edit paths are
retained. The test window hosts the existing inspector and is not shipped as
product UI.

The tutorial addition uses the existing Di Ne header, package icon, DungGeunMo
title font and title scale. It introduces no component, so the existing runtime
component icon remains applicable. All tutorial text, tooltips, required/optional
status and controls use English, Korean and Japanese through `DiNeLang`. Bubbles
use standard mint borders and `EditorStyles` help-box/label styles, next to their
existing workflow controls. No branding block, menu setting, font selector or
independent settings panel is added. Session-only progress leaves serialized
avatar/build settings intact; the explicit empty default-slot action uses Undo,
dirty marking and prefab modification recording. The runner includes the limited
package brand/font assets for comparison with nearby inspector UI. Structural
review supplements all-language Layout/Repaint and spotlight geometry checks;
automated geometry checks do not claim a rendered-pixel visual review.

# Multi Dresser preview regression

This isolated Unity 2022.3 project compiles the production dresser and custom
inspector. It sends real IMGUI Layout/Repaint events to an editor window hosting
that inspector, and counts the NDMF invalidation calls through test doubles.
It does not load or modify an avatar in the user's open project.

```powershell
./Tests/MultiDresserPreview/Run-MultiDresserPreviewRegression.ps1 `
  -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe'
```

Use `-InspectorSource <path>` to compare an older inspector against the same
tests. `-ProjectName <name>` chooses an isolated project under `.codex_tmp`.
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

## UI-standard review

This fix changes preview work and shape-list synchronization only. Existing
header, icons, fonts, colors, layout, controls, translations and language
preferences are retained; the corresponding visual checklist items require no
new UI. Shape values and recording flags remain intact when names survive a
mesh change. Existing serialized-property, Undo and prefab edit paths are
retained. The test window hosts the existing inspector and is not shipped as
product UI.

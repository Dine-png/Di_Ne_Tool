# Multi Dresser MMD regression

The runner copies the exact production `DiNeMultiDresser.cs` into an isolated
Unity 2022.3.22f1 project under `.codex_tmp`; it does not edit an avatar project.
It uses the real Unity Animator/PlayableGraph, animation assets, renderer,
blendshape, and material APIs. SDK types and the menu generator are stand-ins
used only to compile this focused harness, not part of the distributed package.

```powershell
./Tests/MultiDresser/Run-MultiDresserRegression.ps1 `
  -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe'
```

Use `-PrepareOnly` to inspect the test project, or `-PackageSource` and a different
`-ProjectName` to compare a previous version. Failures return a nonzero exit code.
The report is `MultiDresserRegression-results.txt` inside the test project.

Coverage: FX controllers starting with 0, 1, 2, 3, or 5 layers; original layer
order and settings; repeated generation, incremental updates and multiple
dressers; migration of an old generated layer during normal regeneration;
cleanup and empty configurations. An actual Unity animation test disables FX
layer indices 1 and 2, checks that the selected clothing/body blendshape/material
stay correct, then changes outfits while those layers are disabled and after
they are restored.

The Schoolbag cases use independent clothing and accessory layers in both
orders, an empty default (Off) accessory slot, and a linked strap object.
They exercise all clothing/accessory combinations before and during the
disabling of FX layers 1 and 2, then check switching after the layers recover.
The bag starts enabled in the scene.

This models the [documented MMD behavior](https://modular-avatar.nadena.dev/docs/general-behavior/mmd).
It does not reproduce a particular avatar/world, run the MA/NDMF build pipeline,
or verify a VRChat upload. Other causes of clothing deformation remain possible.

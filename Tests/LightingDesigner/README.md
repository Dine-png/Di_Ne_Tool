# Lighting Designer GPU regression tests

The PowerShell runner copies the exact production profile, sink, bake-session, and
control definitions into an isolated Unity 2022.3.22f1 project under `.codex_tmp`.
It copies an installed lilToon package and uses its unmodified native baker as the
pixel reference. It does not modify either avatar project or require NUnit.

```powershell
./Tests/LightingDesigner/Run-LightingDesignerRegression.ps1 `
  -UnityEditorPath 'E:/Unity/2022.3.22f1/Editor/Unity.exe' `
  -LilToonPackagePath 'D:/Comm_YakGwa/Packages/jp.lilxyzw.liltoon'
```

Use `-PrepareOnly` to inspect the project before launching it. Use `-PackageSource`
to run the same tests against a previous package, with a different `-ProjectName`.
Keep graphics enabled; `-nographics` cannot exercise the GPU baking path.

The tests cover RGB/alpha preservation, the order of tone correction and tint,
masked gradation, secondary layers, temperature animation with neutral alpha, sampling
settings, alpha-mask modes, null textures, and RenderTexture non-mutation. They
allow small DXT quantization error and record each result in the isolated
project's `LightingDesignerRegression-results.txt`; failures exit Unity nonzero.

The sampled-animation test uses two material slots with different alpha values:
the tint alpha is baked into each texture and the animation writes an explicit
alpha multiplier of one. Omitting alpha curves is insufficient because Unity
can initialize the animated color property block with zero alpha.

The RenderTexture test verifies non-mutation only. This harness does not prove
compatibility with animated texture/material swaps, every lilToon variant, or a
VRChat avatar upload. The native baker's `_ALPHAMASK` special packing mode is not
used: mask property preservation and the runtime alpha operations are checked
separately.

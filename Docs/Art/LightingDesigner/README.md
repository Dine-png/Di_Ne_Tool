# Lighting Designer menu icons

17 transparent white pictograms for the generated VRChat submenu. They follow
the existing DNLighting/DNHair/DNDresser silhouette language: solid left half,
outlined right half, rounded edges, and no DN initials. Supporting symbols use
the same white stroke weight. The top-level DNLighting icon remains unchanged.

The final assets are drawn from SVG geometry, rather than the ImageGen trial,
whose transparent boundaries were too noisy at menu scale.

## Files and regeneration

Editable SVGs and their generator live here, outside Unity's imported package.
The 256×256 RGBA PNGs are in
`Packages/com.dinetool.avatar-tools/Assets/LightingDesigner/Menu*.png`.
Their committed `.meta` files keep their GUIDs, disable mipmaps, use clamp/bilinear
sampling, and avoid compression artifacts on thin white edges.

With Python and CairoSVG installed, run:

```powershell
python Docs/Art/LightingDesigner/generate_icons.py
```

The generator updates SVGs and PNGs without modifying `.meta` files.

| Menu function | Icon suffix |
|---|---|
| Lighting brightness | Light |
| Saturation | Saturation |
| Hue | Hue |
| Texture brightness | Brightness |
| Gamma | Gamma |
| Color temperature | ColorTemp |
| Monochrome | Monochrome |
| Emission | Emission |
| Shadow strength | ShadowStrength |
| Outline color | OutlineTint |
| Outline width | OutlineWidth |
| Reflectance | Reflectance |
| Fixed light direction | LightDir |
| Enable toggle | Power |
| Preset submenu and preset buttons | Presets |
| Renderer group submenu | Group |
| Next page | Next |

`DiNeLightingGenerator` resolves defaults through `DiNePackageAssets` when
generating menus, including group controls and overflow pages. An explicitly
assigned control/group/preset icon takes priority. Existing serialized component
and preset data is not rewritten.

## UI standard checklist review

- Brand/icon rules: package-relative loading, tool asset location, stable GUIDs,
  sibling visual weight, and reference artwork comparison verified.
- No additional settings, branding blocks, panels, or controls introduced.
- Header, font, component icon, language toolbar, mint actions, cards, tooltips,
  status text, and Simple/Advanced layout: unchanged; no new editor UI or strings.
- Serialized properties, Undo, Prefab overrides, and dirty-state behavior:
  unchanged; icon defaults are resolved only while generating temporary menus.

Validation: visual contact sheet reviewed; all 17 textures checked for size,
RGBA transparency and monochrome color; current Lighting Designer editor sources
compiled with the Unity 2022.3.22f1 compiler and installed SDK references.
In-client VRChat rendering was not exercised.

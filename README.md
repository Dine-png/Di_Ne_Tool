# 🎭 Di Ne Tool - Avatar Editor Suite

Professional-grade **Unity Editor tools** for VRChat avatar customization and optimization. Built for creators who demand efficiency and precision.

![Unity 2022.3](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity&style=flat)
![License](https://img.shields.io/badge/License-MIT-green?style=flat)
![Languages](https://img.shields.io/badge/Languages-EN%20%2F%20KO%20%2F%20JP-blue?style=flat)

---

## 📥 Installation

### Add to VPM (Recommended)
[![Add to VCC](https://img.shields.io/badge/Add_to_VCC-005AF0?style=for-the-badge&logo=vrchat&logoColor=white)](https://dine-png.github.io/Di-Ne-Tool-Page/)

Alternatively, download the `.unitypackage` and import directly into your project.

---

## 🛠️ Tools Overview

### 🎪 Avi Editor
**All-in-one avatar editing suite with Armature, ShapeKey, and Expression tabs**

**Location:** `DiNe → Avi Editor`

#### 🦴 Armature Tab
- Scale, rotate, and position individual bones with per-axis precision
- Supports all standard humanoid bones including shoulders, neck, breast, and butt physics bones
- **Avatar Info Panel** — displays real-time stats when an avatar is assigned:
  - Mesh memory, vertex/triangle count, blend shape count, bone count
  - Material and texture count with accurate VRAM usage
- Persistent avatar assignment across tool switches
- Refresh button (↺) to re-scan the avatar at any time

#### ❄️ ShapeKey Tab
- Animate and preview blend shapes using an animation clip timeline
- Slider bar stays in sync even after Ctrl+Z (Undo)
- Face preview camera with automatic near-clip tuning to isolate the face mesh

#### 🎭 Expression Tab
- Create and edit expression animation clips directly in the tool
- Load existing clips to tweak values with sliders
- **Gesture ShapeKey Inclusion** — optional feature that automatically adds all shapekeys used in other gesture animations at value `0`, preventing expression blending artifacts
- VRChat FX Controller integration — replace gesture slots directly from within the tool

---

### 🎨 Material Tool
**Comprehensive shader preset management and VRAM optimization suite**

**Location:** `DiNe → Material Tool`

#### 🏷️ Preset Apply
Apply LilToon shader presets to selected materials
- Drag-and-drop preset assignment
- Batch processing for multiple materials at once
- Instant material preview

#### ✂️ Diet
Detect and remove unused textures from LilToon materials
- Sections are individually toggleable: **Shadow / AO**, **Shadow Mask**, **Outline**, **Normal Map**, **MatCap**, **Rim Light**, **Emission**, **Glitter**, **Backlight**, **Parallax**, **Dissolve**
- Two removal modes:
  - **Texture Only** — removes assigned textures while leaving feature toggles intact
  - **Texture + Disable Feature** — removes textures AND turns off the corresponding feature toggle (only available when the feature is actually enabled)
- Material cards show badge indicators (texture count / feature ON / Clean)

#### ⚙️ VRAM Optimize
**Accurate texture VRAM analysis and optimization engine**

- **Precise VRAM Calculation** — uses BPP × pixel count × mipmap chain formula, matching in-game values
- **Platform Override Aware** — reads Windows Standalone platform override resolution instead of default texture size
- **Format Compression** — suggests optimal format per texture (BC7 for alpha, DXT1 for opaque, etc.)
- **Resolution Scaling** — downscale high-res textures with live savings preview
- **Batch Optimize** — apply all suggestions in one click
- **Texture Preview Window** — double-click any thumbnail to open a full preview with zoom, 1:1, and fit controls

**Memory Calculation Method:**
```
VRAM = Σ (mip_width × mip_height × BPP / 8)  for each mipmap level
```
Supports 60+ texture formats with accurate BPP data based on GPU memory layout.

---

### 📦 Package Patcher
**Batch import and organize Unity packages with intelligent folder management**

**Location:** `DiNe → EX → Package Patcher`

**Features:**
- 📂 Drag-and-drop `.unitypackage` files or entire folders
- 🗜️ **ZIP support** — automatically detects and extracts `.unitypackage` files nested inside `.zip` archives
- 🔤 **Unicode / special character filename handling** — safely copies files to an ASCII temp path before importing, preventing import failures caused by Japanese, Korean, or special characters (e.g. `♡`) in filenames
- 🗂️ Auto-organizes all imported folders into a single designated target folder
- ✅ Select / deselect individual packages before importing
- 🌍 Full 3-language localization

**Workflow:**
1. Set target folder name (default: `_1_Patch`)
2. Drag packages or ZIP files into the list
3. Select the packages to import
4. Click "Start Import" — all imports are processed sequentially and organized automatically

---

### 📸 Screen Saver
**Unity Editor screen saver and icon generation utility**

**Location:** `DiNe → Screen Saver`

**Features:**
- 🖼️ Unity Editor screen saver to protect your display during idle time
- 🎨 Avatar icon generation directly from the editor
- 💾 Auto-save with configurable output path
- 🌍 Localized interface

---

### 👗 Multi Dresser
**FX toggle generator for avatar clothing and accessories**

**Location:** Add `DiNeMultiSupporter` component to your avatar

**Features:**
- Generate VRChat FX layer toggles for multiple outfits and accessories
- Enable/disable individual items with per-item control
- Batch generation with automatic animator layer setup
- 🌍 Full 3-language localization

---

### ❄️ ShapeKey Freezer
**Freeze blend shape values from an animation clip into the mesh**

**Location:** `DiNe → EX → ShapeKey Freezer`

**Features:**
- Select an animation clip and scrub to the desired frame
- Bake the blend shape weights at that frame permanently into the mesh
- Non-destructive workflow — original mesh is preserved until confirmed
- 🌍 Localized interface

---

## 🎯 Key Features

✨ **Multi-Language Support**
- English, 한국어 (Korean), 日本語 (Japanese)
- Manual language selection in every tool

⚡ **Accurate VRAM Calculation**
- BPP × pixel × mipmap formula — matches in-game VRChat memory usage
- Platform override resolution support (Windows Standalone)
- Shared calculation core across Avi Editor and Material Tool

🎨 **Professional UI**
- Unified title and layout style across all tools
- Color-coded VRAM indicators (green → yellow → red)
- Persistent settings survive domain reloads and tool switches

🔧 **VRChat Integration**
- Works seamlessly with VRChat SDK3
- FX Controller clip replacement from within the Expression tab
- Compatible with LilToon, Poiyomi, and standard Unity materials

🔁 **Undo / Redo Support**
- Full undo history for armature edits, shapekey changes, and material modifications
- Slider UI stays in sync after Ctrl+Z

---

## 📊 VRAM Optimization Guide

### Example: Optimize a 4K Avatar

| Texture | Original | Format | Resolution | Optimized | Savings |
|---------|----------|--------|------------|-----------|---------|
| Skin Diffuse | 128 MB | DXT1 | 2048×2048 | 32 MB | 96 MB |
| Normal Map | 128 MB | BC5 | 2048×2048 | 32 MB | 96 MB |
| Detail AO | 128 MB | DXT1 | 1024×1024 | 8 MB | 120 MB |
| **Total** | **384 MB** | — | — | **72 MB** | **312 MB** |

**81% VRAM reduction** while maintaining visual quality!

---

## 🚀 Getting Started

### Requirements
- **Unity 2022.3 LTS or later**
- **VRChat SDK3** (recommended, not required for core tools)
- **LilToon shader** (optional, for Material Tool presets and Diet mode)

### Quick Setup
1. Add to VCC using the button above, OR
2. Import the `.unitypackage` into your project
3. Open any tool from the `DiNe` menu

### First Steps
- **Start with:** `Material Tool → VRAM Optimize` to analyze your avatar's texture memory
- **Clean up:** `Material Tool → Diet` to remove unused textures from LilToon materials
- **Fine-tune:** `Avi Editor → Armature` to adjust bone proportions
- **Create expressions:** `Avi Editor → Expression` to build and assign face animations

---

## 🔧 Troubleshooting

**Package won't import (filename contains Japanese or special characters)?**
- Enable the **"임포트 창 강제 표시"** option in Package Patcher as a fallback
- The tool automatically copies files to a safe temp path — check the Console for path error details

**VRAM numbers differ from other tools?**
- Di Ne Tool uses a BPP × mipmap formula that matches in-game GPU memory
- Make sure your Windows Standalone platform override is set correctly in Texture Import Settings

**Slider doesn't move after Ctrl+Z in ShapeKey tab?**
- This is fixed in the current version via `Undo.undoRedoPerformed` sync
- If it persists, re-assign the avatar to refresh the SMR reference

**Expression blending causes distorted faces?**
- Enable **"재스쳐 클립의 쉐이프키 0값 포함"** in the Expression tab before saving
- This writes all gesture-used shapekeys at `0` in the new clip, preventing bleed-through

**Window won't open?**
- Check the `DiNe` menu appears in the top menu bar
- Restart Unity if menu items are not visible

---

## 📝 Version History

**v1.1**
- Avi Editor: Avatar Info Panel with real-time mesh/VRAM stats
- Avi Editor: Expression tab with FX Controller integration and gesture shapekey inclusion
- Avi Editor: Undo/Redo slider sync in ShapeKey tab
- Avi Editor: Persistent avatar assignment across tool switches
- Material Tool: Diet mode split into Shadow/AO and Shadow Mask as separate sections
- Material Tool: Right button (Disable Feature) now inactive when feature is already off
- Material Tool: VRAM calculation unified with accurate BPP formula, platform override resolution support
- Package Patcher: ZIP file support and Unicode filename handling
- UI: Title bar style unified across all tools

**v1.0** - Initial release
- Avi Editor with Armature & Shape Key tabs
- Material Tool with Preset Apply, Diet, VRAM Optimize
- Package Patcher with folder management
- Screen Saver utility
- Full 3-language localization

---

## 💡 Tips & Tricks

💭 **Avatar Optimization Priority:**
1. Remove unused textures (Material Tool → Diet)
2. Reduce high-resolution textures (VRAM Optimize → Resolution)
3. Switch to compressed formats (VRAM Optimize → Format)
4. Check the Avatar Info Panel in Avi Editor for a quick overall summary

💭 **Expression Workflow:**
- Use the **Gesture ShapeKey Inclusion** option whenever your avatar has multiple expression animations
- Always save with "Save as New" first, then overwrite once confirmed

💭 **Package Patcher:**
- Drop an entire folder to batch-detect all `.unitypackage` and `.zip` files at once
- Use the select/deselect buttons to filter what gets imported

---

## 🤝 Contributing & Feedback

Found a bug? Have a feature request?

- 📧 Report issues on GitHub
- 💬 Suggest improvements
- 🌟 Share your optimized avatars!

---

## 📄 License

MIT License - Free for personal and commercial use

---

## 🎮 Compatible With

- ✅ VRChat Avatar 3.0
- ✅ LilToon Shader
- ✅ Poiyomi Shader
- ✅ Standard Unity Materials
- ✅ Custom Shaders

---

**Made with ❤️ for avatar creators**

*Last Updated: 2026*

# 🎭 Di Ne Tool - Avatar Editor Suite

Professional-grade **Unity Editor tools** for VRChat avatar customization and optimization. Built for creators who demand efficiency and precision.

![Unity 2019+](https://img.shields.io/badge/Unity-2019%2B-black?logo=unity&style=flat)
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
**Combined armature scaling and blend shape freezing in one tabbed interface**

**Location:** `DiNe → Avi Editor`

**Features:**
- 📏 **Armature Scaler** - Scale character bones with uniform or per-axis precision
- ❄️ **Shape Key Freezer** - Freeze blend shapes (shape keys) at specific keyframes from animation clips
- 🎯 Simultaneous preview and modification
- ⚡ Undo/Redo support for all changes
- 🌍 Full localization (English, 한국어, 日本語)

**Usage:**
1. Select your avatar in the scene
2. Open `DiNe → Avi Editor` from the menu
3. Choose **Armature** tab to scale bones, or **Shape Key** tab to freeze blend shapes
4. For Shape Key: Select animation clip → Choose frame → Click "Freeze"

---

### 🎨 Material Tool
**Comprehensive shader preset management and VRAM optimization suite**

**Location:** `DiNe → Material Tool`

**Tabs:**

#### 🏷️ Preset Apply
Apply LilToon shader presets to selected materials
- Drag-and-drop preset assignment
- Batch processing multiple materials
- Instant material updates

#### ✂️ AO Remove
Remove Ambient Occlusion texture from materials
- Detects AO texture usage
- Cleans unused texture references
- Optimizes material performance

#### ⚙️ VRAM Optimize
**Intelligent texture optimization engine**

- **Automatic VRAM Calculation** - Scans all textures in scene renderers
- **Format Compression** - Smart format selection:
  - Textures with alpha channel → **BC7** (better quality)
  - Opaque textures → **DXT1** (smaller size)
- **Resolution Scaling** - Downscale high-resolution textures
- **Real-time Estimates** - Preview VRAM savings before optimization
- **Batch Processing** - Optimize all textures with one click

**Memory Calculation Method:**
```
VRAM = (width × height × BPP / 8) × mipmap_factor
```
- Each mipmap level: 1/4 memory of previous level
- Supports 60+ texture formats with accurate BPP data

**Double-click any texture thumbnail** to launch the **Texture Preview Window** with:
- Scrollable view with checkerboard alpha visualization
- Zoom controls (0.1x - 4.0x)
- 1:1 and Fit buttons for quick navigation
- Mouse wheel zoom support

---

### 📦 Package Patcher
**Batch import and organize Unity packages with intelligent folder management**

**Location:** `DiNe → Package Patcher`

**Features:**
- 📂 Drag-and-drop .unitypackage files
- 🔄 Auto-organize imports into designated folder
- ✅ Folder existence detection with warnings
- 📋 Import history tracking
- 🌍 Localized UI

**Workflow:**
1. Select target folder for imports
2. Drag packages into the list
3. Click "Import All" to batch process
4. All packages organized in chosen folder automatically

---

### 📸 Screen Saver
**High-quality screenshot capture directly from Unity Editor**

**Location:** `DiNe → Screen Saver`

**Features:**
- 🖼️ Capture Editor viewport as PNG
- 💾 Save to designated folder with auto-naming
- 🎬 Batch screenshot support
- 🎯 Scene-aware capture
- 🌍 Localized interface

---

### 👗 Multi Dresser (In Development)
**Advanced avatar multi-material and outfit switching system**

**Features:**
- Multiple outfit presets
- Quick-switch between configurations
- Material variation support

---

## 🎯 Key Features

✨ **Multi-Language Support**
- English, 한국어 (Korean), 日本語 (Japanese)
- Auto-detects system language
- Manual override in settings

⚡ **Performance First**
- Cached texture scanning for large scenes
- Real-time VRAM calculation
- Optimized for avatars with 100+ textures

🎨 **Professional UI**
- Clean, organized editor windows
- Color-coded indicators
- Responsive toolbar controls
- Persistent settings via EditorPrefs

🔧 **Integration**
- Works seamlessly with VRChat SDK
- Compatible with LilToon shaders
- Supports standard Unity materials
- Undo/Redo system integration

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
- **Unity 2019.4 LTS or later**
- **VRChat SDK3** (recommended, not required for core tools)
- **LilToon shader** (optional, for Material Tool presets)

### Quick Setup
1. Add to VCC using the button above, OR
2. Import the `.unitypackage` into your project
3. Open any tool from `DiNe` menu
4. UI automatically localizes to your system language

### First Steps
- **Start with:** Material Tool → VRAM Optimize to analyze your avatar
- **Then use:** Avi Editor to fine-tune armature if needed
- **Optimize:** Review VRAM card, adjust format/resolution, hit "Optimize All"

---

## 🔧 Troubleshooting

**Textures show "?" in preview?**
- Some format types don't generate thumbnails in Editor
- Use the double-click texture preview window instead

**VRAM numbers seem wrong?**
- Editor view differs from runtime memory
- Check in-game performance with VRChat's built-in tools
- Mipmap chain is automatically included in calculations

**Window won't open?**
- Check `Window → DiNe` menu appears
- Restart Unity if menu items not visible
- Verify correct project is open

---

## 📝 Version History

**v1.0** - Initial release
- Avi Editor with Armature & Shape Key tabs
- Material Tool with Preset Apply, AO Remove, VRAM Optimize
- Package Patcher with folder management
- Screen Saver utility
- Full 3-language localization

---

## 💡 Tips & Tricks

💭 **Avatar Optimization Priority:**
1. Remove unused textures and materials (AO Remove)
2. Reduce high-resolution textures (VRAM Optimize → Size)
3. Switch to compressed formats (VRAM Optimize → Format)
4. Test in VRChat world performance

💭 **VRAM Optimize Best Practices:**
- Always preview savings before batch processing
- Test in your target world (performance varies by location)
- Keep originals backed up before format changes
- Consider texture quality vs. file size trade-off

💭 **Shape Key Freezing:**
- Works best with single-keyframe animations
- Remember to save your scene before freezing
- Frozen values become part of the mesh permanently

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

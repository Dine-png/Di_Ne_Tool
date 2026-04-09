using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class DiNeMaterialTool : EditorWindow
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Enums
    // ══════════════════════════════════════════════════════════════════════════
    private enum ToolMode { PresetApply, Diet, VRAMOptimize }
    private enum Lang { English, Korean, Japanese }

    private ToolMode _mode = ToolMode.PresetApply;
    private Lang _lang = Lang.Korean;
    private int L => (int)_lang;

    // ══════════════════════════════════════════════════════════════════════════
    //  UI Text (English / Korean / Japanese)
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly string[][] UI =
    {
        /* 00 */ new[] { "Preset Apply",        "프리셋 적용",         "プリセット適用"           },
        /* 01 */ new[] { "Diet",                "다이어트",           "ダイエット"              },
        // ── Common ──
        /* 02 */ new[] { "Target Settings",     "대상 설정",           "対象設定"                },
        /* 03 */ new[] { "Target Object",       "대상 오브젝트",       "対象オブジェクト"         },
        /* 04 */ new[] { "Include Children",    "자식 오브젝트 포함",   "子オブジェクトを含む"     },
        /* 05 */ new[] { "Include Inactive",    "비활성 오브젝트 포함", "非アクティブを含む"       },
        /* 06 */ new[] { "[ Preview Mode ]  No changes will be saved",      "[ 미리보기 모드 ]  실제로 적용되지 않습니다",      "[ プレビューモード ]  変更されません"     },
        /* 07 */ new[] { "[ Apply Mode ]  Changes will be written to disk", "[ 적용 모드 ]  변경사항이 실제로 저장됩니다",       "[ 適用モード ]  適用されます"             },
        // ── Preset ──
        /* 08 */ new[] { "Preset Library",              "프리셋 라이브러리",               "プリセットライブラリ"             },
        /* 09 */ new[] { "Scan Project",                "프로젝트 스캔",                   "プロジェクトスキャン"             },
        /* 10 */ new[] { "All",                         "전체",                           "すべて"                         },
        /* 11 */ new[] { "No presets found in project.", "프로젝트에서 프리셋을 찾을 수 없습니다.", "プリセット内にプリセットがありません。" },
        /* 12 */ new[] { "presets",                     "개",                             "個"                             },
        /* 13 */ new[] { "Scan Materials",              "마테리얼 스캔",                   "マテリアルスキャン"               },
        /* 14 */ new[] { "Apply Preset",                "프리셋 적용",                     "プリセット適用"                   },
        /* 15 */ new[] { "LilToon materials: {0} / Selected: {1}", "LilToon 마테리얼: {0}개 / 선택: {1}개", "LilToonマテリアル: {0}個 / 選択: {1}個" },
        /* 16 */ new[] { "Select All",                  "전체 선택",                       "全て選択"                        },
        /* 17 */ new[] { "Deselect All",                "전체 해제",                       "全て解除"                        },
        /* 18 */ new[] { "Ping",                        "Ping",                           "Ping"                           },
        /* 19 */ new[] { "Please assign a Target Object first.", "대상 오브젝트를 먼저 지정해주세요.", "対象オブジェクトを先に指定してください。" },
        /* 20 */ new[] { "Scan complete — {0} material(s) found", "스캔 완료 — 마테리얼 {0}개 발견", "スキャン完了 — マテリアル {0}個を発見" },
        /* 21 */ new[] { "No materials selected.",       "선택된 마테리얼이 없습니다.",       "選択されたマテリアルがありません。" },
        /* 22 */ new[] { "[Preview] {0} material(s) queued", "[미리보기] {0}개 적용 예정",    "[プレビュー] {0}個適用予定"        },
        /* 23 */ new[] { "Apply preset [{1}] to {0} material(s).\nSupports Undo.", "{0}개 마테리얼에 [{1}]을(를) 적용합니다.\nUndo로 되돌릴 수 있습니다.", "{0}個のマテリアルに [{1}] を適用します。\nUndoで元に戻せます。" },
        /* 24 */ new[] { "Apply",   "적용",   "適用"     },
        /* 25 */ new[] { "Cancel",  "취소",   "キャンセル" },
        /* 26 */ new[] { "Done — Applied to {0} material(s)", "완료 — {0}개 마테리얼에 적용됨", "完了 — {0}個に適用しました" },
        /* 27 */ new[] { "Select a preset from the library above.", "위 라이브러리에서 프리셋을 선택하세요.", "上のライブラリからプリセットを選択してください。" },
        /* 28 */ new[] { "Preview Check", "미리보기 확인", "プレビュー確認" },
        /* 29 */ new[] { "Colors",   "컬러",   "カラー"     },
        /* 30 */ new[] { "Floats",   "플로트", "フロート"   },
        /* 31 */ new[] { "Vectors",  "벡터",   "ベクター"   },
        /* 32 */ new[] { "Textures", "텍스쳐", "テクスチャー" },
        // ── Diet ──
        /* 33 */ new[] { "Select Sections",          "섹션 선택",                  "セクション選択"                    },
        /* 34 */ new[] { "Apply Diet",               "다이어트 적용",               "ダイエット適用"                    },
        /* 35 */ new[] { "LilToon: {0} found / Affected: {1}", "LilToon 마테리얼: {0}개 / 대상: {1}개", "LilToon: {0}個 / 対象: {1}個" },
        /* 36 */ new[] { "No LilToon materials found.", "LilToon 마테리얼을 찾을 수 없습니다.", "LilToonマテリアルが見つかりません。" },
        /* 37 */ new[] { "Textures to remove:",      "제거할 텍스쳐:",              "削除するテクスチャ:"               },
        /* 38 */ new[] { "Scan complete — {0} LilToon material(s), {1} affected", "스캔 완료 — LilToon {0}개, 대상 {1}개", "スキャン完了 — LilToon {0}個, 対象 {1}個" },
        /* 39 */ new[] { "No textures to remove.", "제거할 텍스쳐가 없습니다.", "削除するテクスチャがありません。" },
        /* 40 */ new[] { "[Preview] {1} texture(s) in {0} material(s) would be removed.", "[미리보기] {0}개 마테리얼에서 총 {1}개 텍스쳐가 제거될 예정입니다.", "[プレビュー] {0}個のマテリアルから計 {1}個のテクスチャが削除される予定です。" },
        /* 41 */ new[] { "Apply Diet",               "다이어트 적용",               "ダイエット適用"                    },
        /* 42 */ new[] { "Remove textures from {0} material(s).\nSupports Undo.", "{0}개 마테리얼에서 텍스쳐를 제거합니다.\nUndo로 되돌릴 수 있습니다.", "{0}個のマテリアルからテクスチャを削除します。\nUndoで元に戻せます。" },
        /* 43 */ new[] { "Continue?",  "계속하시겠습니까?", "続けますか？"  },
        /* 44 */ new[] { "Apply",      "적용",              "適用"           },
        /* 45 */ new[] { "Done — Removed {1} texture(s) from {0} material(s).", "완료 — {0}개 마테리얼에서 {1}개 텍스쳐를 제거했습니다.", "完了 — {0}個のマテリアルから {1}個のテクスチャを削除しました。" },
        /* 46 */ new[] { "Preview Result",           "미리보기 결과 확인",          "プレビュー結果を確認"              },
        // ── VRAM ──
        /* 47 */ new[] { "VRAM Optimize",      "VRAM 최적화",        "VRAM最適化"               },
        /* 48 */ new[] { "Total VRAM",         "총 VRAM 사용량",      "VRAM合計"                 },
        /* 49 */ new[] { "Textures: {0}  |  Total: {1}", "텍스처: {0}개  |  합계: {1}", "テクスチャ: {0}個  |  合計: {1}" },
        /* 50 */ new[] { "Optimize All",       "전체 최적화",         "全て最適化"                },
        /* 51 */ new[] { "This will change compression and/or max size for {0} texture(s).\nThis action is NOT undo-able.\n\nContinue?",
                         "{0}개 텍스처의 압축 포맷 및/또는 최대 해상도를 변경합니다.\n이 작업은 실행취소(Undo)가 불가능합니다.\n\n계속하시겠습니까?",
                         "{0}個のテクスチャの圧縮フォーマットや最大解像度を変更します。\nこの操作は元に戻せません。\n\n続けますか？" },
        /* 52 */ new[] { "Done — Optimized {0} texture(s), saved {1}", "완료 — {0}개 텍스처 최적화, {1} 절약", "完了 — {0}個のテクスチャを最適化、{1}削減" },
        /* 53 */ new[] { "No textures found.",  "텍스처를 찾을 수 없습니다.",  "テクスチャが見つかりません。" },
        /* 54 */ new[] { "Format",             "포맷",               "フォーマット"              },
        // ── Diet extra (58+) ──
        /* 55 */ new[] { "_unused_55_",        "_unused_55_",        "_unused_55_"               },
        /* 56 */ new[] { "_unused_56_",        "_unused_56_",        "_unused_56_"               },
        /* 57 */ new[] { "_unused_57_",        "_unused_57_",        "_unused_57_"               },
        /* 58 */ new[] { "Disable Feature",    "기능 비활성화",       "機能を無効化"              },
        /* 59 */ new[] { "Sections",           "섹션",               "セクション"                },
        /* 60 */ new[] { "Select All",         "전체 선택",           "全て選択"                  },
        /* 61 */ new[] { "Deselect All",       "전체 해제",           "全て解除"                  },
        /* 62 */ new[] { "Remove Textures Only", "텍스쳐만 제거",     "テクスチャのみ削除"         },
        /* 63 */ new[] { "Remove + Disable",   "제거 + 기능 끄기",    "削除＋機能を無効化"         },
    };
    private string T(int i) => UI[i][L];
    private string Tf(int i, params object[] a) => string.Format(UI[i][L], a);

    // ══════════════════════════════════════════════════════════════════════════
    //  Categories (Preset)
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly string[] CategoryNames =
        { "Skin", "Hair", "Cloth", "Nature", "Inorganic", "Effect", "Other" };
    private static readonly Color[] CategoryColors =
    {
        new Color(0.95f, 0.70f, 0.60f),
        new Color(0.60f, 0.50f, 0.85f),
        new Color(0.40f, 0.75f, 0.90f),
        new Color(0.50f, 0.85f, 0.50f),
        new Color(0.70f, 0.70f, 0.70f),
        new Color(0.90f, 0.60f, 0.90f),
        new Color(0.80f, 0.80f, 0.50f),
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  Colors
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly Color ColCard    = new Color(0.21f, 0.21f, 0.24f);
    private static readonly Color ColAccent  = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColAction  = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColApply   = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColSelect  = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColDanger  = new Color(0.60f, 0.25f, 0.25f);
    private static readonly Color ColWarn    = new Color(0.72f, 0.55f, 0.18f);
    private static readonly Color ColText    = new Color(0.88f, 0.88f, 0.92f);
    private static readonly Color ColSubText = new Color(0.58f, 0.58f, 0.63f);
    private static readonly Color ColLine    = new Color(0.30f, 0.30f, 0.35f, 0.8f);

    // ══════════════════════════════════════════════════════════════════════════
    //  Diet — Section Definitions
    // ══════════════════════════════════════════════════════════════════════════
    private struct SectionDef
    {
        public string[] Names;    // [EN, KO, JP]
        public string[] TexProps; // texture property names
        public string   Toggle;   // float toggle prop to zero when DisableFeature=true (null = none)
        public SectionDef(string[] n, string[] t, string tog = null) { Names = n; TexProps = t; Toggle = tog; }
    }

    private static readonly SectionDef[] SECTIONS = new[]
    {
        new SectionDef(new[]{"Shadow / AO",  "쉐도우 / AO",  "シャドウ / AO"},
            new[]{"_ShadowStrengthMask","_ShadowBorderMask","_ShadowBlurMask",
                  "_Shadow2ndColorTex","_Shadow3rdColorTex"}),
        new SectionDef(new[]{"Outline",      "아웃라인",      "アウトライン"},
            new[]{"_OutlineTex","_OutlineWidthMask","_OutlineVectorTex"},
            "_UseOutline"),   // lilToon unified: _UseOutline=0 / Outline variant: _OutlineWidth=0
        new SectionDef(new[]{"Normal Map",   "노멀 맵",       "ノーマルマップ"},
            new[]{"_BumpMap","_Bump2ndMap","_DetailNormalMap"}),
        new SectionDef(new[]{"MatCap",       "맷캡",          "マットキャップ"},
            new[]{"_MatCapTex","_MatCapBlendMask","_MatCap2ndTex","_MatCap2ndBlendMask"}, "_UseMatCap"),
        new SectionDef(new[]{"Rim Light",    "림라이트",      "リムライト"},
            new[]{"_RimColorTex"}, "_UseRim"),
        new SectionDef(new[]{"Emission",     "에미션",        "エミッション"},
            new[]{"_EmissionMap","_Emission2ndMap"}, "_UseEmission"),
        new SectionDef(new[]{"Glitter",      "글리터",        "グリッター"},
            new[]{"_GlitterColorTex"}, "_UseGlitter"),
        new SectionDef(new[]{"Backlight",    "백라이트",      "バックライト"},
            new[]{"_BacklightColorTex"}, "_UseBacklight"),
        new SectionDef(new[]{"Parallax",     "시차",          "パララックス"},
            new[]{"_ParallaxMap"}, "_UseParallax"),
        new SectionDef(new[]{"Dissolve",     "디졸브",        "ディゾルブ"},
            new[]{"_DissolveMask","_DissolveNoiseMask"}, "_UseDissolve"),
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  State — Common
    // ══════════════════════════════════════════════════════════════════════════
    private Texture2D _windowIcon;
    private Texture2D _tabIcon;
    private Font      _titleFont;
    private GameObject _targetObject;
    private bool _includeChildren = true;
    private bool _includeInactive = true;
    private bool _previewOnly     = false;
    private string _status = "";
    private bool   _statusWarn;
    private GameObject _prevTargetObject;

    // ══════════════════════════════════════════════════════════════════════════
    //  State — Preset Mode
    // ══════════════════════════════════════════════════════════════════════════
    private class PresetEntry
    {
        public ScriptableObject Asset;
        public string Name;
        public int    CategoryIdx;
        public string Path;
        public int    ColorCount, FloatCount, VectorCount, TextureCount;
    }

    private List<PresetEntry> _library     = new List<PresetEntry>();
    private bool              _libReady    = false;
    private int               _catFilter   = -1;
    private string            _search      = "";
    private Vector2           _libScroll;
    private PresetEntry       _selPreset;

    private List<MaterialInfo> _presetMats    = new List<MaterialInfo>();
    private bool               _presetScanned = false;
    private Vector2            _presetMatScroll;

    // ══════════════════════════════════════════════════════════════════════════
    //  State — Diet Mode
    // ══════════════════════════════════════════════════════════════════════════
    private List<MaterialInfo> _dietMats    = new List<MaterialInfo>();
    private bool               _dietScanned = false;
    private Vector2            _dietScroll;
    private Vector2            _dietMatScroll;
    private bool[]             _dietEnabled;   // section checkbox (enabled = should clean)

    // ══════════════════════════════════════════════════════════════════════════
    //  State — VRAM Mode
    // ══════════════════════════════════════════════════════════════════════════
    private class TextureVRAMInfo
    {
        public Texture   Texture;
        public string    AssetPath;
        public long      VRAMBytes;
        public float     BPP;
        public string    FormatString;
        public TextureFormat Format;
        public bool      HasAlpha;
        public float     MinBPP;
        public int       MaxDimension;
        public bool      CanOptimizeFormat;
        public bool      CanOptimizeSize;
        public long      FormatSavings;
        public long      SizeSavings;
        public TextureImporterFormat SuggestedFormat;
        
        // 사용 중인 마테리얼과 오브젝트 리스트
        public List<Material> UsedByMaterials = new List<Material>();
        public List<GameObject> UsedByObjects = new List<GameObject>(); // 추가됨!
        
        // 개별 드롭다운 상태
        public bool      MaterialDropdown;
        public bool      ObjectDropdown; // 추가됨!
    }

    private List<TextureVRAMInfo> _vramTextures = new List<TextureVRAMInfo>();
    private bool _vramScanned = false;
    private Vector2 _vramScroll;
    private long _vramTotal;

    private static readonly Dictionary<TextureFormat, float> TEX_BPP = new Dictionary<TextureFormat, float>()
    {
        { TextureFormat.Alpha8, 8 }, { TextureFormat.ARGB4444, 16 }, { TextureFormat.RGB24, 24 },
        { TextureFormat.RGBA32, 32 }, { TextureFormat.ARGB32, 32 }, { TextureFormat.RGB565, 16 },
        { TextureFormat.R16, 16 }, { TextureFormat.DXT1, 4 }, { TextureFormat.DXT5, 8 },
        { TextureFormat.RGBA4444, 16 }, { TextureFormat.BGRA32, 32 },
        { TextureFormat.RHalf, 16 }, { TextureFormat.RGHalf, 32 }, { TextureFormat.RGBAHalf, 64 },
        { TextureFormat.RFloat, 32 }, { TextureFormat.RGFloat, 64 }, { TextureFormat.RGBAFloat, 128 },
        { TextureFormat.YUY2, 16 }, { TextureFormat.RGB9e5Float, 32 },
        { TextureFormat.BC6H, 8 }, { TextureFormat.BC7, 8 }, { TextureFormat.BC4, 4 }, { TextureFormat.BC5, 8 },
        { TextureFormat.DXT1Crunched, 4 }, { TextureFormat.DXT5Crunched, 8 },
        { TextureFormat.PVRTC_RGB2, 6 }, { TextureFormat.PVRTC_RGBA2, 8 },
        { TextureFormat.PVRTC_RGB4, 12 }, { TextureFormat.PVRTC_RGBA4, 16 },
        { TextureFormat.ETC_RGB4, 4 }, { TextureFormat.EAC_R, 4 }, { TextureFormat.EAC_R_SIGNED, 4 },
        { TextureFormat.EAC_RG, 8 }, { TextureFormat.EAC_RG_SIGNED, 8 },
        { TextureFormat.ETC2_RGB, 4 }, { TextureFormat.ETC2_RGBA1, 4 }, { TextureFormat.ETC2_RGBA8, 8 },
        { TextureFormat.ASTC_4x4, 8 }, { TextureFormat.ASTC_5x5, 5.12f }, { TextureFormat.ASTC_6x6, 3.56f },
        { TextureFormat.ASTC_8x8, 2 }, { TextureFormat.ASTC_10x10, 1.28f }, { TextureFormat.ASTC_12x12, 0.89f },
        { TextureFormat.RG16, 16 }, { TextureFormat.R8, 8 },
        { TextureFormat.ETC_RGB4Crunched, 4 }, { TextureFormat.ETC2_RGBA8Crunched, 8 },
        { TextureFormat.ASTC_HDR_4x4, 8 }, { TextureFormat.ASTC_HDR_5x5, 5.12f },
        { TextureFormat.ASTC_HDR_6x6, 3.56f }, { TextureFormat.ASTC_HDR_8x8, 2 },
        { TextureFormat.ASTC_HDR_10x10, 1.28f }, { TextureFormat.ASTC_HDR_12x12, 0.89f },
        { TextureFormat.RG32, 32 }, { TextureFormat.RGB48, 48 }, { TextureFormat.RGBA64, 64 },
    };

    // ══════════════════════════════════════════════════════════════════════════
    //  Shared Material Info
    // ══════════════════════════════════════════════════════════════════════════
    private class DietSectionResult
    {
        public int           SectionIndex;
        public List<string>  Props    = new List<string>();
        public List<Texture> Textures = new List<Texture>();
    }

    private class MaterialInfo
    {
        public Material      Material;
        public string        Path;
        public string        ShaderName;
        public bool          Selected;
        public bool          Foldout = true;
        // Diet mode
        public List<DietSectionResult> DietResults = new List<DietSectionResult>();
        public int  TotalDietCount => DietResults.Sum(r => r.Props.Count);
        public bool HasDiet        => TotalDietCount > 0;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Menu / Lifecycle
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("DiNe/Material Tool", false, 2)]
    public static void ShowWindow()
    {
        var w = GetWindow<DiNeMaterialTool>();
        w.minSize = new Vector2(340, 500);
        w.position = new Rect(w.position.x, w.position.y, 440, 780);
    }

    void OnEnable()
    {
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        _tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Material", _tabIcon);
        if (_dietEnabled == null || _dietEnabled.Length != SECTIONS.Length)
        {
            _dietEnabled = new bool[SECTIONS.Length];
            // default: Shadow/AO, Outline, Normal, MatCap, Rim, Emission on
            for (int i = 0; i < SECTIONS.Length; i++)
                _dietEnabled[i] = i < 6;
        }
        LoadSettings();
        ScanLibrary();
    }

    void OnDisable() => SaveSettings();

    // ══════════════════════════════════════════════════════════════════════════
    //  OnGUI
    // ══════════════════════════════════════════════════════════════════════════
    void OnGUI()
    {
        DrawHeader();
        DrawLangBar();
        HLine();
        DrawModeSelector();
        HLine();
        DrawTargetSettings();
        HLine();

        if (_mode == ToolMode.PresetApply)
            DrawPresetMode();
        else if (_mode == ToolMode.Diet)
            DrawDietMode();
        else
            DrawVRAMMode();

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, _statusWarn ? MessageType.Warning : MessageType.Info);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Common UI
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawHeader()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        var style = new GUIStyle(EditorStyles.label)
        {
            font = _titleFont, alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold, fontSize = 36,
            normal = { textColor = Color.white }
        };
        float iconSize = 72f;
        GUILayout.Label(_windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Material Tool", style, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (_lang)
        {
            case Lang.Korean: desc = "빠르고 간편하게 마테리얼과 릴툰 프리셋을 일괄 적용/수정하는 도구입니다."; break;
            case Lang.Japanese: desc = "マテリアルとlilToonプリセットを素早く適用・変更するためのツールです。"; break;
            default:      desc = "A tool to quickly apply and modify materials and lilToon presets."; break;
        }
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }

    private void DrawLangBar()
    {
        int idx = L;
        int next = DrawCustomToolbar(idx, new[] { "English", "한국어", "日本語" }, 26);
        if (next != idx) { _lang = (Lang)next; SaveSettings(); }
    }

    private void DrawModeSelector()
    {
        int idx = (int)_mode;
        int next = DrawCustomToolbar(idx, new[] { T(0), T(1), T(47) }, 30);
        if (next != idx)
        {
            _mode = (ToolMode)next;
            _status = "";
            AutoScan();
        }
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button) { 
                fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
            {
                newSelected = i;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }

    private void DrawTargetSettings()
    {
        SectionLabel(T(2));
        GUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        _targetObject = (GameObject)EditorGUILayout.ObjectField(T(3), _targetObject, typeof(GameObject), true);
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColAction;
        if (GUILayout.Button("↺", GUILayout.Width(28), GUILayout.Height(18)))
            AutoScan();
        GUI.backgroundColor = prev;
        EditorGUILayout.EndHorizontal();

        // Auto-scan on target change
        if (_targetObject != _prevTargetObject)
        {
            _prevTargetObject = _targetObject;
            AutoScan();
        }

        GUILayout.Space(4);
        GuiLine(1, 2);
        
        bool prevChildren = _includeChildren;
        bool prevInactive = _includeInactive;
        _includeChildren = EditorGUILayout.Toggle(T(4), _includeChildren);
        _includeInactive = EditorGUILayout.Toggle(T(5), _includeInactive);
        if (_includeChildren != prevChildren || _includeInactive != prevInactive)
            AutoScan();
            
        GUILayout.Space(4);
        
        // 미리보기 토글 UI 복구
        bool prevPreview = _previewOnly;
        _previewOnly = EditorGUILayout.Toggle(T(_previewOnly ? 6 : 7), _previewOnly);
        if (_previewOnly != prevPreview) AutoScan();
        
        GUILayout.Space(4);
    }

    private void AutoScan()
    {
        if (_targetObject == null) { _presetMats.Clear(); _presetScanned = false; _dietMats.Clear(); _dietScanned = false; _vramTextures.Clear(); _vramScanned = false; _status = ""; return; }
        if (_mode == ToolMode.PresetApply) PresetScanMaterials();
        else if (_mode == ToolMode.Diet) DietScanMaterials();
        else VRAMScanTextures();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PRESET MODE
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawPresetMode()
    {
        DrawPresetLibrary();
        HLine();

        if (_selPreset != null)
        {
            DrawSelectedPresetInfo();
            HLine();
            DrawPresetActions();
            HLine();
        }
        else
        {
            GUILayout.Space(8);
            DrawCenteredHint(T(27));
            GUILayout.Space(8);
        }

        if (_presetScanned && _selPreset != null)
            DrawPresetMaterialResults();
    }

    private void DrawPresetLibrary()
    {
        EditorGUILayout.BeginHorizontal();
        SectionLabel(T(8));
        GUILayout.FlexibleSpace();
        if (_libReady)
        {
            GUILayout.Label($"{_library.Count} {T(12)}", new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = ColSubText } });
            GUILayout.Space(6);
        }
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColAction;
        if (GUILayout.Button(T(9), EditorStyles.miniButton, GUILayout.Width(90), GUILayout.Height(20)))
            ScanLibrary();
        GUI.backgroundColor = prev;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        if (!_libReady) { DrawCenteredHint(T(11)); GUILayout.Space(6); return; }

        // Category tabs
        var usedCats = _library.Select(p => p.CategoryIdx).Distinct().OrderBy(x => x).ToList();
        var tabLabels = new List<string> { T(10) };
        var tabValues = new List<int> { -1 };
        foreach (var ci in usedCats)
        {
            tabLabels.Add(ci >= 0 && ci < CategoryNames.Length ? CategoryNames[ci] : "Other");
            tabValues.Add(ci);
        }
        int curTab = tabValues.IndexOf(_catFilter);
        if (curTab < 0) curTab = 0;

        EditorGUILayout.BeginHorizontal();
        for (int ti = 0; ti < tabLabels.Count; ti++)
        {
            bool active = (ti == curTab);
            int catVal = tabValues[ti];
            Color tabCol = active
                ? (catVal >= 0 && catVal < CategoryColors.Length ? CategoryColors[catVal] : ColAccent)
                : new Color(0.22f, 0.22f, 0.25f);
            var btn = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = active ? Color.white : new Color(0.70f, 0.70f, 0.75f) }
            };
            var p = GUI.backgroundColor;
            GUI.backgroundColor = tabCol;
            if (GUILayout.Button(tabLabels[ti], btn, GUILayout.Height(22)))
            { _catFilter = catVal; _libScroll = Vector2.zero; }
            GUI.backgroundColor = p;
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        // Search
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔍", GUILayout.Width(18));
        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
        if (!string.IsNullOrEmpty(_search) && GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            _search = "";
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        // List
        var filtered = _library.Where(p =>
            (_catFilter < 0 || p.CategoryIdx == _catFilter) &&
            (string.IsNullOrEmpty(_search) || p.Name.ToLower().Contains(_search.ToLower()))
        ).ToList();

        float h = Mathf.Clamp(filtered.Count * 36f + 8f, 80f, 145f);
        _libScroll = EditorGUILayout.BeginScrollView(_libScroll, GUILayout.Height(h));
        if (filtered.Count == 0) { GUILayout.Space(20); DrawCenteredHint(T(11)); }
        else foreach (var e in filtered) DrawPresetRow(e);
        EditorGUILayout.EndScrollView();
    }

    private void DrawPresetRow(PresetEntry entry)
    {
        bool sel = (_selPreset == entry);
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = sel ? new Color(0.22f, 0.35f, 0.55f) : ColCard;
        Rect rowRect = EditorGUILayout.BeginHorizontal("box");
        GUI.backgroundColor = prev;

        int ci = entry.CategoryIdx;
        Color cc = ci >= 0 && ci < CategoryColors.Length ? CategoryColors[ci] : new Color(0.7f, 0.7f, 0.7f);
        var bar = EditorGUILayout.GetControlRect(false, GUILayout.Width(4), GUILayout.Height(24));
        EditorGUI.DrawRect(bar, cc);
        GUILayout.Space(6);

        var ns = new GUIStyle(EditorStyles.label)
        {
            fontStyle = sel ? FontStyle.Bold : FontStyle.Normal, fontSize = 12,
            normal = { textColor = sel ? Color.white : new Color(0.85f, 0.85f, 0.88f) }
        };
        GUILayout.Label(entry.Name, ns, GUILayout.ExpandWidth(true), GUILayout.Height(24));

        GUILayout.FlexibleSpace();
        string catName = ci >= 0 && ci < CategoryNames.Length ? CategoryNames[ci] : "?";
        GUILayout.Label(catName, new GUIStyle(EditorStyles.miniLabel)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = cc } },
            GUILayout.Width(58));
        bool pingClicked = GUILayout.Button("⊙", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(22));
        EditorGUILayout.EndHorizontal();

        if (pingClicked)
        {
            EditorGUIUtility.PingObject(entry.Asset);
        }
        else
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
            {
                _selPreset = entry; _status = ""; AutoScan();
                e.Use();
                GUIUtility.keyboardControl = 0;
            }
        }
    }

    private void DrawSelectedPresetInfo()
    {
        if (_selPreset == null) return;
        int ci = _selPreset.CategoryIdx;
        Color cc = ci >= 0 && ci < CategoryColors.Length ? CategoryColors[ci] : Color.white;

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        EditorGUILayout.BeginHorizontal();
        var bar = EditorGUILayout.GetControlRect(false, GUILayout.Width(6), GUILayout.Height(42));
        EditorGUI.DrawRect(bar, cc);
        GUILayout.Space(6);

        EditorGUILayout.BeginVertical();
        GUILayout.Label(_selPreset.Name, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 14, normal = { textColor = Color.white } });
        string catName = ci >= 0 && ci < CategoryNames.Length ? CategoryNames[ci] : "Other";
        GUILayout.Label(catName, new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = cc }, fontStyle = FontStyle.Bold });
        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        DrawBadge(T(29), _selPreset.ColorCount,   new Color(0.9f, 0.55f, 0.55f));
        DrawBadge(T(30), _selPreset.FloatCount,   new Color(0.55f, 0.85f, 0.55f));
        DrawBadge(T(31), _selPreset.VectorCount,  new Color(0.55f, 0.75f, 1.0f));
        if (_selPreset.TextureCount > 0)
            DrawBadge(T(32), _selPreset.TextureCount, new Color(1.0f, 0.85f, 0.45f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawBadge(string label, int count, Color col)
    {
        if (count <= 0) return;
        GUILayout.Label($"{label}: {count}", new GUIStyle(EditorStyles.miniLabel)
            { fontStyle = FontStyle.Bold, normal = { textColor = col } });
        GUILayout.Space(4);
    }

    private void DrawPresetActions()
    {
        var btn = new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
        var prev = GUI.backgroundColor;

        bool canApply = _presetScanned && _selPreset != null && _presetMats.Any(m => m.Selected);
        GUI.enabled = canApply;
        GUI.backgroundColor = !canApply ? new Color(0.35f, 0.35f, 0.38f) : _previewOnly ? ColWarn : ColApply;
        if (GUILayout.Button(_previewOnly ? T(28) : T(14), btn, GUILayout.Height(38)))
            ApplyPreset();
        GUI.backgroundColor = prev;
        GUI.enabled = true;
    }

    private void DrawPresetMaterialResults()
    {
        GUILayout.Space(4); HLine();
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        int total = _presetMats.Count, sel = _presetMats.Count(m => m.Selected);
        EditorGUILayout.LabelField(Tf(15, total, sel), new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);
        DrawSelectButtons(_presetMats);
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);

        _presetMatScroll = EditorGUILayout.BeginScrollView(_presetMatScroll);
        foreach (var info in _presetMats) DrawMaterialCard(info, false);
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DIET MODE — UI
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawDietMode()
    {
        DrawDietSectionPanel();
        HLine();
        DrawDietActionButton();

        if (_dietScanned)
        {
            HLine();
            DrawDietResults();
        }
    }

    private void DrawDietSectionPanel()
    {
        SectionLabel(T(59));
        GUILayout.Space(4);

        // Select All / Deselect All
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(T(60), EditorStyles.miniButton, GUILayout.Width(72)))
            { for (int i = 0; i < _dietEnabled.Length; i++) _dietEnabled[i] = true; DietScanMaterials(); }
        if (GUILayout.Button(T(61), EditorStyles.miniButton, GUILayout.Width(72)))
            { for (int i = 0; i < _dietEnabled.Length; i++) _dietEnabled[i] = false; DietScanMaterials(); }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);

        var labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, fontStyle = FontStyle.Bold };
        var subStyle   = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColSubText } };
        var disStyle   = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.85f, 0.55f, 0.55f) } };

        for (int i = 0; i < SECTIONS.Length; i++)
        {
            var sec = SECTIONS[i];
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = _dietEnabled[i] ? new Color(0.22f, 0.30f, 0.28f) : ColCard;
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = prev;

            // Section enable toggle
            bool newEn = GUILayout.Toggle(_dietEnabled[i], "", GUILayout.Width(18));
            if (newEn != _dietEnabled[i]) { _dietEnabled[i] = newEn; DietScanMaterials(); }

            GUILayout.Space(4);
            // Section name
            GUILayout.Label(sec.Names[L], _dietEnabled[i] ? labelStyle : subStyle, GUILayout.ExpandWidth(true));

            // Prop count hint
            GUILayout.Label($"({sec.TexProps.Length})", subStyle, GUILayout.Width(28));
            // Toggle prop indicator
            if (sec.Toggle != null)
                GUILayout.Label("⚡", new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.85f, 0.55f, 0.55f) } }, GUILayout.Width(18));
            else
                GUILayout.Space(18);

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawDietActionButton()
    {
        // 버튼 1: 지울 텍스쳐가 있는 마테리얼이 선택된 경우
        bool canRemove  = _dietScanned && _dietMats.Any(m => m.Selected && m.HasDiet);
        // 버튼 2: 지울 텍스쳐 OR 끌 수 있는 기능 토글이 있는 마테리얼이 선택된 경우
        bool canDisable = _dietScanned && _dietMats.Any(m => m.Selected && (m.HasDiet || HasEnabledToggles(m)));

        var btn = new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
        var prev = GUI.backgroundColor;

        EditorGUILayout.BeginHorizontal();

        // 버튼 1: 텍스쳐만 제거
        GUI.enabled = canRemove;
        GUI.backgroundColor = _previewOnly ? ColWarn : new Color(0.55f, 0.35f, 0.35f);
        if (GUILayout.Button(_previewOnly ? T(46) : T(62), btn, GUILayout.Height(36)))
            ApplyDiet(disableFeatures: false);

        // 버튼 2: 제거 + 기능 끄기
        GUI.enabled = canDisable;
        GUI.backgroundColor = _previewOnly ? ColWarn : ColDanger;
        if (GUILayout.Button(_previewOnly ? T(46) : T(63), btn, GUILayout.Height(36)))
            ApplyDiet(disableFeatures: true);

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = prev;
        GUI.enabled = true;
    }

    private bool HasEnabledToggles(MaterialInfo info)
    {
        for (int si = 0; si < SECTIONS.Length; si++)
        {
            if (!_dietEnabled[si]) continue;
            if (IsSectionFeatureOn(info.Material, si)) return true;
        }
        return false;
    }

    // 해당 섹션의 기능이 켜져 있는지 판단
    private bool IsSectionFeatureOn(Material mat, int si)
    {
        string tog = SECTIONS[si].Toggle;
        if (tog == null) return false;

        // 일반 float 토글 (_UseOutline, _UseMatCap 등)
        if (mat.HasProperty(tog) && mat.GetFloat(tog) > 0.5f) return true;

        // Outline 전용 셰이더 처리
        // _UseOutline 프로퍼티가 없는 "lilToon Outline" 셰이더 변형은
        // 셰이더 이름에 "Outline"이 있으면 항상 켜진 상태로 간주
        if (tog == "_UseOutline" &&
            mat.shader.name.IndexOf("Outline", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // _OutlineWidth가 거의 0이면 기능 끄기가 적용된 것으로 간주
            if (mat.HasProperty("_OutlineWidth") && mat.GetFloat("_OutlineWidth") <= 0.001f)
                return false;
            return true;
        }

        return false;
    }

    private void DrawDietResults()
    {
        int lilCount = _dietMats.Count;
        int affected = _dietMats.Count(m => m.HasDiet);

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;
        EditorGUILayout.LabelField(Tf(35, lilCount, affected), new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(5);
        DrawSelectButtons(_dietMats, true);
        EditorGUILayout.EndVertical();

        if (lilCount == 0) { EditorGUILayout.HelpBox(T(36), MessageType.Info); return; }

        _dietMatScroll = EditorGUILayout.BeginScrollView(_dietMatScroll);
        foreach (var info in _dietMats) DrawDietCard(info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawDietCard(MaterialInfo info)
    {
        // HasDiet도 없고 활성화된 기능 토글도 없으면 선택 해제
        bool hasAction = info.HasDiet || HasEnabledToggles(info);
        if (!hasAction) info.Selected = false;

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        // ── Header row ──
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = info.HasDiet || HasEnabledToggles(info);
        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(20), GUILayout.Height(20));
        GUI.enabled = true;
        GUILayout.Space(2);

        // Badge
        var badge = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = info.HasDiet ? new Color(1f, 0.55f, 0.2f)
                     : HasEnabledToggles(info) ? new Color(0.85f, 0.50f, 0.50f)
                     : new Color(0.4f, 0.85f, 0.4f) }
        };
        string badgeLabel = info.HasDiet ? $"×{info.TotalDietCount}" : HasEnabledToggles(info) ? "⚡ON" : "Clean";
        GUILayout.Label(badgeLabel, badge, GUILayout.Width(42), GUILayout.Height(20));

        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Material.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(T(18), new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11, fontStyle = FontStyle.Bold }, GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Material);
        EditorGUILayout.EndHorizontal();

        // ── Card content ──
        if (info.Foldout && info.HasDiet)
        {
            GUILayout.Space(3);
            GuiLine(1, 2);
            GUILayout.Space(4);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
            GUILayout.Space(6);

            foreach (var res in info.DietResults)
            {
                var sec = SECTIONS[res.SectionIndex];
                // Section sub-header
                EditorGUILayout.LabelField($"▸  {sec.Names[L]}", new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = new Color(0.95f, 0.75f, 0.35f) } });

                for (int j = 0; j < res.Props.Count; j++)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"  {res.Props[j]}",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.75f, 0.75f, 0.8f) } });

                    if (res.Textures[j] != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        Rect previewRect = GUILayoutUtility.GetRect(56, 56, GUILayout.Width(56), GUILayout.Height(56));
                        Texture2D thumb = AssetPreview.GetAssetPreview(res.Textures[j]);
                        if (thumb != null)
                            GUI.DrawTexture(previewRect, thumb, ScaleMode.ScaleToFit);
                        else if (res.Textures[j] is Texture2D raw)
                            { EditorGUI.DrawPreviewTexture(previewRect, raw); Repaint(); }
                        else
                            { GUI.Box(previewRect, ""); Repaint(); }

                        if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
                        { EditorGUIUtility.PingObject(res.Textures[j]); Event.current.Use(); }

                        GUILayout.Space(6);
                        EditorGUILayout.BeginVertical();
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(res.Textures[j].name,
                            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
                        if (res.Textures[j] is Texture2D t2d)
                            EditorGUILayout.LabelField($"{t2d.width} × {t2d.height}  |  {t2d.format}",
                                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColSubText } });
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(2);
                }

                GUILayout.Space(4);
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(3);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Material Card (Preset mode)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawMaterialCard(MaterialInfo info, bool _ = false)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(20), GUILayout.Height(20));
        GUILayout.Space(2);
        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Material.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(T(18), new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11, fontStyle = FontStyle.Bold }, GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Material);
        EditorGUILayout.EndHorizontal();

        if (info.Foldout)
        {
            GUILayout.Space(3); GuiLine(1, 2); GUILayout.Space(4);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
            if (info.ShaderName != null)
                EditorGUILayout.LabelField(info.ShaderName,
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.45f, 0.72f, 1f) } });
            EditorGUI.indentLevel--;
            GUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(3);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Preset Logic
    // ══════════════════════════════════════════════════════════════════════════
    private void ScanLibrary()
    {
        _library.Clear(); _libReady = false;
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;
            var entry = TryParsePreset(asset, path);
            if (entry != null) _library.Add(entry);
        }
        _library = _library.OrderBy(p => p.CategoryIdx < 0 ? 99 : p.CategoryIdx).ThenBy(p => p.Name).ToList();
        _libReady = true;
        if (_selPreset != null)
        {
            _selPreset = _library.FirstOrDefault(e => e.Asset == _selPreset.Asset);
            if (_selPreset == null) { _presetScanned = false; _presetMats.Clear(); }
        }
        Repaint();
    }

    private PresetEntry TryParsePreset(ScriptableObject asset, string path)
    {
        try
        {
            var so = new SerializedObject(asset);
            var bases = so.FindProperty("bases");
            if (bases == null || !bases.isArray) return null;
            var e = new PresetEntry { Asset = asset, Path = path };
            if (bases.arraySize > 0) e.Name = bases.GetArrayElementAtIndex(0).FindPropertyRelative("name")?.stringValue;
            if (string.IsNullOrEmpty(e.Name)) e.Name = asset.name;
            var catProp = so.FindProperty("category");
            e.CategoryIdx = catProp?.enumValueIndex ?? -1;
            if (e.CategoryIdx >= CategoryNames.Length) e.CategoryIdx = CategoryNames.Length - 1;
            var c = so.FindProperty("colors");   e.ColorCount   = c is { isArray: true } ? c.arraySize : 0;
            var f = so.FindProperty("floats");   e.FloatCount   = f is { isArray: true } ? f.arraySize : 0;
            var v = so.FindProperty("vectors");  e.VectorCount  = v is { isArray: true } ? v.arraySize : 0;
            var t = so.FindProperty("textures"); e.TextureCount = t is { isArray: true } ? t.arraySize : 0;
            return e;
        }
        catch { return null; }
    }

    private void PresetScanMaterials()
    {
        _presetMats.Clear(); _presetScanned = false;
        if (_targetObject == null) { SetStatus(T(19), true); return; }
        Renderer[] renderers = _includeChildren
            ? _targetObject.GetComponentsInChildren<Renderer>(_includeInactive)
            : _targetObject.GetComponents<Renderer>();
        var seen = new HashSet<int>();
        foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || !seen.Add(mat.GetInstanceID()) || !IsLilToon(mat)) continue;
                _presetMats.Add(new MaterialInfo { Material = mat, Path = AssetDatabase.GetAssetPath(mat), ShaderName = mat.shader.name, Selected = true });
            }
        _presetMats = _presetMats.OrderBy(m => m.Material.name).ToList();
        _presetScanned = true;
        SetStatus(Tf(20, _presetMats.Count), _presetMats.Count == 0);
        Repaint();
    }

    private void ApplyPreset()
    {
        var targets = _presetMats.Where(m => m.Selected).ToList();
        if (targets.Count == 0) { SetStatus(T(21), false); return; }
        if (_selPreset == null) return;
        if (_previewOnly) { SetStatus(Tf(22, targets.Count), true); return; }

        bool ok = EditorUtility.DisplayDialog(T(14), Tf(23, targets.Count, _selPreset.Name), T(24), T(25));
        if (!ok) return;

        var so = new SerializedObject(_selPreset.Asset);
        var colors   = so.FindProperty("colors");
        var floats   = so.FindProperty("floats");
        var vectors  = so.FindProperty("vectors");
        var textures = so.FindProperty("textures");

        Undo.RecordObjects(targets.Select(m => (Object)m.Material).ToArray(), "DiNe Material Tool Preset Apply");
        int success = 0;
        foreach (var info in targets)
        {
            var mat = info.Material;
            ApplyProps(colors, (e, p) => { if (mat.HasProperty(p)) mat.SetColor(p, e.FindPropertyRelative("value").colorValue); });
            ApplyProps(floats, (e, p) => { if (mat.HasProperty(p)) mat.SetFloat(p, e.FindPropertyRelative("value").floatValue); });
            ApplyProps(vectors, (e, p) => { if (mat.HasProperty(p)) mat.SetVector(p, e.FindPropertyRelative("value").vector4Value); });
            if (textures != null)
                for (int i = 0; i < textures.arraySize; i++)
                {
                    var e = textures.GetArrayElementAtIndex(i);
                    string p = e.FindPropertyRelative("name").stringValue;
                    if (!mat.HasProperty(p)) continue;
                    mat.SetTexture(p, e.FindPropertyRelative("value").objectReferenceValue as Texture);
                    mat.SetTextureOffset(p, e.FindPropertyRelative("offset").vector2Value);
                    mat.SetTextureScale(p, e.FindPropertyRelative("scale").vector2Value);
                }
            EditorUtility.SetDirty(mat);
            success++;
        }
        AssetDatabase.SaveAssets();
        SetStatus(Tf(26, success), false);
    }

    private void ApplyProps(SerializedProperty arr, System.Action<SerializedProperty, string> apply)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.arraySize; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            apply(e, e.FindPropertyRelative("name").stringValue);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Diet Logic
    // ══════════════════════════════════════════════════════════════════════════
    private void DietScanMaterials()
    {
        // 재스캔 전 선택 상태 저장
        var prevSelected = new HashSet<int>(_dietMats.Where(m => m.Selected).Select(m => m.Material.GetInstanceID()));

        _dietMats.Clear(); _dietScanned = false;
        if (_targetObject == null) { SetStatus(T(19), true); return; }
        Renderer[] renderers = _includeChildren
            ? _targetObject.GetComponentsInChildren<Renderer>(_includeInactive)
            : _targetObject.GetComponents<Renderer>();
        var seen = new HashSet<int>();
        foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || !seen.Add(mat.GetInstanceID())) continue;
                if (!IsLilToon(mat)) continue;
                var info = new MaterialInfo
                {
                    Material   = mat,
                    Path       = AssetDatabase.GetAssetPath(mat),
                    ShaderName = mat.shader.name,
                };
                CollectDietProperties(mat, info);
                bool wasSel = prevSelected.Contains(mat.GetInstanceID());
                // 텍스쳐 제거 대상이거나 기능이 켜져있으면 자동 선택
                // (재스캔 시 이전에 선택 해제한 것은 유지)
                bool hasAction = info.HasDiet || HasEnabledToggles(info);
                bool prevDeselected = prevSelected.Count > 0 && !prevSelected.Contains(mat.GetInstanceID());
                info.Selected = hasAction && !prevDeselected;
                _dietMats.Add(info);
            }
        // 정렬: 액션 있는 것(ON) 최상단 알파벳순, 그 아래 클린 알파벳순
        _dietMats = _dietMats
            .OrderByDescending(m => m.HasDiet || HasEnabledToggles(m))
            .ThenBy(m => m.Material.name)
            .ToList();
        _dietScanned = true;
        int affected = _dietMats.Count(m => m.HasDiet);
        SetStatus(Tf(38, _dietMats.Count, affected), affected > 0);
        Repaint();
    }

    private void CollectDietProperties(Material mat, MaterialInfo info)
    {
        for (int si = 0; si < SECTIONS.Length; si++)
        {
            if (!_dietEnabled[si]) continue;
            var sec = SECTIONS[si];
            var res = new DietSectionResult { SectionIndex = si };
            foreach (string prop in sec.TexProps)
            {
                if (!mat.HasProperty(prop)) continue;
                var tex = mat.GetTexture(prop);
                if (tex == null) continue;
                res.Props.Add(prop);
                res.Textures.Add(tex);
            }
            if (res.Props.Count > 0)
                info.DietResults.Add(res);
        }
    }

    private void ApplyDiet(bool disableFeatures)
    {
        // 텍스쳐 제거 대상: HasDiet인 것
        // 기능 끄기 대상: disableFeatures일 때 HasDiet OR HasEnabledToggles인 것
        var removeTargets  = _dietMats.Where(m => m.Selected && m.HasDiet).ToList();
        var disableTargets = disableFeatures
            ? _dietMats.Where(m => m.Selected && (m.HasDiet || HasEnabledToggles(m))).ToList()
            : new List<MaterialInfo>();

        var allTargets = removeTargets.Union(disableTargets).Distinct().ToList();
        if (allTargets.Count == 0) { SetStatus(T(39), false); return; }

        if (_previewOnly)
        {
            int total = removeTargets.Sum(m => m.TotalDietCount);
            SetStatus(Tf(40, allTargets.Count, total), true);
            return;
        }

        string dialogTitle = disableFeatures ? T(63) : T(62);
        bool ok = EditorUtility.DisplayDialog(dialogTitle, Tf(42, allTargets.Count) + "\n" + T(43), T(44), T(25));
        if (!ok) return;

        Undo.RecordObjects(allTargets.Select(m => (Object)m.Material).ToArray(), "DiNe Diet Tool");
        int removed = 0;

        foreach (var info in allTargets)
        {
            // 텍스쳐 제거
            foreach (var res in info.DietResults)
            {
                var sec = SECTIONS[res.SectionIndex];
                foreach (string prop in res.Props)
                { info.Material.SetTexture(prop, null); removed++; }
            }
            // 기능 끄기
            if (disableFeatures)
            {
                for (int si = 0; si < SECTIONS.Length; si++)
                {
                    if (!_dietEnabled[si]) continue;
                    if (!IsSectionFeatureOn(info.Material, si)) continue;

                    string tog = SECTIONS[si].Toggle;
                    if (tog == "_UseOutline")
                    {
                        // 통합 셰이더: float + 키워드 같이 끄기
                        if (info.Material.HasProperty("_UseOutline"))
                            info.Material.SetFloat("_UseOutline", 0f);
                        // 전용 Outline 셰이더: 키워드 + 폭을 0으로
                        if (info.Material.HasProperty("_OutlineWidth"))
                            info.Material.SetFloat("_OutlineWidth", 0f);
                        info.Material.DisableKeyword("_OUTLINE");
                    }
                    else if (info.Material.HasProperty(tog))
                    {
                        info.Material.SetFloat(tog, 0f);
                    }
                }
            }
            EditorUtility.SetDirty(info.Material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetStatus(Tf(45, allTargets.Count, removed), false);
        DietScanMaterials();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  VRAM MODE
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawVRAMMode()
    {
        if (!_vramScanned) { EditorGUILayout.HelpBox(T(53), MessageType.Info); return; }
        if (_vramTextures.Count == 0) { EditorGUILayout.HelpBox(T(53), MessageType.Info); return; }

        // ── 총합 표시 ──
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        EditorGUILayout.LabelField(Tf(49, _vramTextures.Count, FormatBytes(_vramTotal)),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 });
        EditorGUILayout.EndVertical();

        GUILayout.Space(4);

        // ── 전체 최적화 버튼 ──
        var optimizable = _vramTextures.Where(t => t.CanOptimizeFormat || t.CanOptimizeSize).ToList();
        long totalSavings = optimizable.Sum(t => t.FormatSavings + t.SizeSavings);

        if (optimizable.Count > 0)
        {
            prev = GUI.backgroundColor;
            GUI.backgroundColor = ColApply;
            var btnStyle = new GUIStyle(GUI.skin.button)
                { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
            if (GUILayout.Button($"{T(50)}  ({optimizable.Count})  → -{FormatBytes(totalSavings)}", btnStyle, GUILayout.Height(38)))
                VRAMOptimizeAll(optimizable);
            GUI.backgroundColor = prev;
        }

        HLine();

        // ── 개별 텍스처 리스트 ──
        _vramScroll = EditorGUILayout.BeginScrollView(_vramScroll);
        foreach (var info in _vramTextures)
            DrawVRAMTextureCard(info);
        EditorGUILayout.EndScrollView();
    }

    private static readonly TextureImporterFormat[] _formatOptions =
    {
        TextureImporterFormat.DXT1, TextureImporterFormat.DXT1Crunched,
        TextureImporterFormat.DXT5, TextureImporterFormat.DXT5Crunched,
        TextureImporterFormat.BC7, TextureImporterFormat.BC4, TextureImporterFormat.BC5, TextureImporterFormat.BC6H,
        TextureImporterFormat.RGBA32, TextureImporterFormat.RGB24, TextureImporterFormat.Alpha8,
        TextureImporterFormat.ASTC_4x4, TextureImporterFormat.ASTC_6x6, TextureImporterFormat.ASTC_8x8, TextureImporterFormat.ASTC_12x12,
    };
    private static readonly string[] _formatNames = _formatOptions.Select(f => f.ToString()).ToArray();
    private static readonly int[] _sizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
    private static readonly string[] _sizeNames = _sizeOptions.Select(s => s.ToString()).ToArray();

    private void DrawVRAMTextureCard(TextureVRAMInfo info)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        // ── 썸네일 + 우측 텍스트 영역 ──
        EditorGUILayout.BeginHorizontal();

        // 1. 썸네일 영역
        Rect thumbRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40), GUILayout.Height(40));
        Texture2D thumb = AssetPreview.GetAssetPreview(info.Texture);
        if (thumb != null)
            GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
        else if (info.Texture is Texture2D t2d)
            { EditorGUI.DrawPreviewTexture(thumbRect, t2d); Repaint(); }
        else
            { GUI.Box(thumbRect, ""); Repaint(); }

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && thumbRect.Contains(Event.current.mousePosition))
        {
            if (Event.current.clickCount == 2)
            {
                DiNeTexturePreviewWindow.Open(info.Texture);
            }
            else
            {
                EditorGUIUtility.PingObject(info.Texture);
                Selection.activeObject = info.Texture;
            }
            Event.current.Use();
        }
        EditorGUIUtility.AddCursorRect(thumbRect, MouseCursor.Link);

        GUILayout.Space(6);

        // 2. 우측 텍스트 & 옵션 영역 시작
        EditorGUILayout.BeginVertical();

        // 줄 1: 이름 + VRAM 용량
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(info.Texture.name, new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
        GUILayout.Label(FormatBytes(info.VRAMBytes), new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13, alignment = TextAnchor.MiddleRight, normal = { textColor = GetVRAMColor(info.VRAMBytes) } },
            GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 줄 2: 포맷 & 해상도 드롭다운 + (우측 끝) 오브젝트 및 마테리얼 펼쳐보기 버튼
        var importer = !string.IsNullOrEmpty(info.AssetPath) ? AssetImporter.GetAtPath(info.AssetPath) as TextureImporter : null;
        EditorGUILayout.BeginHorizontal();

        if (importer != null && info.Texture is Texture2D)
        {
            // 포맷 드롭다운
            int curFmtIdx = System.Array.IndexOf(_formatOptions, (TextureImporterFormat)info.Format);
            if (curFmtIdx < 0) curFmtIdx = 0;
            int newFmtIdx = EditorGUILayout.Popup(curFmtIdx, _formatNames, GUILayout.Width(110));
            if (newFmtIdx != curFmtIdx)
            {
                info.SuggestedFormat = _formatOptions[newFmtIdx];
                VRAMChangeCompression(info);
            }

            GUILayout.Space(4);

            // 해상도 드롭다운
            int curSizeIdx = System.Array.IndexOf(_sizeOptions, importer.maxTextureSize);
            if (curSizeIdx < 0) curSizeIdx = _sizeOptions.Length - 1; 
            int newSizeIdx = EditorGUILayout.Popup(curSizeIdx, _sizeNames, GUILayout.Width(70));
            if (newSizeIdx != curSizeIdx)
            {
                VRAMChangeSize(info, _sizeOptions[newSizeIdx]);
            }
        }
        else
        {
            GUILayout.Label(info.FormatString, new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = ColSubText } }, GUILayout.ExpandWidth(false));
        }

        // 빈 공간을 채워 남은 버튼들을 우측으로 밀어냄
        GUILayout.FlexibleSpace(); 

        var foldStyle = new GUIStyle(EditorStyles.foldout) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = ColSubText } };

        // 오브젝트 펼쳐보기 버튼 (새로 추가됨)
        if (info.UsedByObjects.Count > 0)
        {
            string objLabel = L == 1 ? $"오브젝트 ({info.UsedByObjects.Count})" : 
                              L == 2 ? $"オブジェクト ({info.UsedByObjects.Count})" : 
                                       $"Objects ({info.UsedByObjects.Count})";

            info.ObjectDropdown = GUILayout.Toggle(info.ObjectDropdown, objLabel, foldStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(8); // 마테리얼 버튼과의 간격
        }

        // 마테리얼 펼쳐보기 버튼
        if (info.UsedByMaterials.Count > 0)
        {
            string matLabel = L == 1 ? $"마테리얼 ({info.UsedByMaterials.Count})" : 
                              L == 2 ? $"マテリアル ({info.UsedByMaterials.Count})" : 
                                       $"Mats ({info.UsedByMaterials.Count})";

            info.MaterialDropdown = GUILayout.Toggle(info.MaterialDropdown, matLabel, foldStyle, GUILayout.ExpandWidth(false));
        }

        EditorGUILayout.EndHorizontal(); // 줄 2 닫기

        // 줄 3: 리스트 표시 영역 (둘 중 하나라도 열려 있으면 표시)
        if (info.ObjectDropdown || info.MaterialDropdown)
        {
            GUILayout.Space(4);
            
            // 만약 둘 다 열려있다면 좌우로 나란히(Horizontal) 표시해서 뚱뚱해지는 걸 최소화
            EditorGUILayout.BeginHorizontal();
            
            // 오브젝트 할당칸 리스트 표시
            if (info.ObjectDropdown && info.UsedByObjects.Count > 0)
            {
                EditorGUILayout.BeginVertical();
                foreach (var obj in info.UsedByObjects)
                {
                    if (obj == null) continue;
                    // allowSceneObjects 파라미터를 true로 주어 씬의 게임오브젝트를 찾을 수 있게 함
                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true, GUILayout.Height(18));
                }
                EditorGUILayout.EndVertical();
                
                if (info.MaterialDropdown) GUILayout.Space(6); // 둘 다 열렸을 때 사이 여백
            }

            // 마테리얼 할당칸 리스트 표시
            if (info.MaterialDropdown && info.UsedByMaterials.Count > 0)
            {
                EditorGUILayout.BeginVertical();
                foreach (var mat in info.UsedByMaterials)
                {
                    if (mat == null) continue;
                    EditorGUILayout.ObjectField(mat, typeof(Material), false, GUILayout.Height(18));
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical(); // 우측 영역 닫기
        EditorGUILayout.EndHorizontal(); // 썸네일 + 우측 영역 닫기

        EditorGUILayout.EndVertical(); // 카드 전체 박스 닫기
        GUILayout.Space(2);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  VRAM Logic
    // ══════════════════════════════════════════════════════════════════════════
    
    // 현재 빌드 타겟 플랫폼 이름 가져오기
    private string GetActivePlatformName()
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        switch (target)
        {
            case BuildTarget.Android: return "Android";
            case BuildTarget.iOS: return "iPhone";
            case BuildTarget.WebGL: return "WebGL";
            default: return "Standalone"; // PC/Mac/Linux
        }
    }

    private void VRAMScanTextures()
    {
        _vramTextures.Clear(); _vramScanned = false; _vramTotal = 0;
        if (_targetObject == null) { SetStatus(T(19), true); return; }

        Renderer[] renderers = _includeChildren
            ? _targetObject.GetComponentsInChildren<Renderer>(_includeInactive)
            : _targetObject.GetComponents<Renderer>();

        var textureMap = new Dictionary<Texture, TextureVRAMInfo>();
        var allMaterials = new List<Material>();

        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                allMaterials.Add(mat);
                foreach (string propName in mat.GetTexturePropertyNames())
                {
                    Texture tex = mat.GetTexture(propName);
                    if (tex == null) continue;
                    if (!textureMap.ContainsKey(tex))
                    {
                        var info = CalculateVRAMInfo(tex);
                        if (info != null) textureMap[tex] = info;
                    }
                    if (textureMap.ContainsKey(tex))
                    {
                        if (!textureMap[tex].UsedByMaterials.Contains(mat))
                            textureMap[tex].UsedByMaterials.Add(mat);
                        
                        // 오브젝트(GameObject) 정보도 함께 수집
                        if (!textureMap[tex].UsedByObjects.Contains(r.gameObject))
                            textureMap[tex].UsedByObjects.Add(r.gameObject);
                    }
                }
            }
        }

        _vramTextures = textureMap.Values.OrderByDescending(t => t.VRAMBytes).ToList();
        _vramTotal = _vramTextures.Sum(t => t.VRAMBytes);
        _vramScanned = true;
        SetStatus(Tf(49, _vramTextures.Count, FormatBytes(_vramTotal)), false);
        Repaint();
    }

    private TextureVRAMInfo CalculateVRAMInfo(Texture tex)
    {
        var info = new TextureVRAMInfo { Texture = tex, AssetPath = AssetDatabase.GetAssetPath(tex) };
        info.MaxDimension = Mathf.Max(tex.width, tex.height);

        if (tex is Texture2D t2d)
        {
            info.Format = t2d.format;
            info.FormatString = t2d.format.ToString();
            if (!TEX_BPP.TryGetValue(t2d.format, out info.BPP)) info.BPP = 16;
            info.VRAMBytes = CalcVRAMBytes(tex, info.BPP);

            if (!string.IsNullOrEmpty(info.AssetPath))
            {
                var importer = AssetImporter.GetAtPath(info.AssetPath) as TextureImporter;
                if (importer != null)
                {
                    info.HasAlpha = importer.DoesSourceTextureHaveAlpha();
                    info.MinBPP = (info.HasAlpha || importer.textureType == TextureImporterType.NormalMap) ? 8 : 4;

                    // 포맷 최적화 가능 여부
                    if (info.BPP > info.MinBPP)
                    {
                        info.CanOptimizeFormat = true;
                        info.SuggestedFormat = (info.HasAlpha || importer.textureType == TextureImporterType.NormalMap)
                            ? TextureImporterFormat.BC7 : TextureImporterFormat.DXT1;
                        TextureFormat newFmt = info.SuggestedFormat == TextureImporterFormat.BC7 ? TextureFormat.BC7 : TextureFormat.DXT1;
                        float newBpp = TEX_BPP[newFmt];
                        info.FormatSavings = info.VRAMBytes - CalcVRAMBytes(tex, newBpp);
                    }

                    // 해상도 축소 가능 여부
                    if (info.MaxDimension > 2048)
                    {
                        info.CanOptimizeSize = true;
                        float scale = 2048f / info.MaxDimension;
                        info.SizeSavings = info.VRAMBytes - CalcVRAMBytes(tex, info.BPP, scale);
                    }
                }
            }
        }
        else
        {
            info.FormatString = tex.GetType().Name;
            info.BPP = 32;
            info.VRAMBytes = CalcVRAMBytes(tex, info.BPP);
        }

        return info;
    }

    private static long CalcVRAMBytes(Texture tex, float bpp, float resolutionScale = 1f)
    {
        int w = (int)(tex.width * resolutionScale);
        int h = (int)(tex.height * resolutionScale);
        long bytes = 0;
        for (int i = 0; i < tex.mipmapCount; i++)
        {
            int mw = Mathf.Max(1, w >> i);
            int mh = Mathf.Max(1, h >> i);
            bytes += (long)Mathf.RoundToInt(mw * mh * bpp / 8f);
        }
        return bytes;
    }

    private void VRAMChangeCompression(TextureVRAMInfo info)
    {
        var importer = AssetImporter.GetAtPath(info.AssetPath) as TextureImporter;
        if (importer == null) return;
        
        string platform = GetActivePlatformName();
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings()
        {
            name = platform,
            overridden = true,
            format = info.SuggestedFormat,
            maxTextureSize = importer.maxTextureSize,
            compressionQuality = 100
        });
        importer.SaveAndReimport();
        VRAMScanTextures();
    }

    private void VRAMChangeSize(TextureVRAMInfo info, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(info.AssetPath) as TextureImporter;
        if (importer == null) return;
        
        string platform = GetActivePlatformName();
        importer.maxTextureSize = maxSize; 
        
        var settings = importer.GetPlatformTextureSettings(platform); 
        settings.name = platform;
        settings.overridden = true; 
        settings.maxTextureSize = maxSize;
        
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
        VRAMScanTextures();
    }

    private void VRAMOptimizeAll(List<TextureVRAMInfo> targets)
    {
        if (!EditorUtility.DisplayDialog(T(50), Tf(51, targets.Count), T(24), T(25))) return;

        long savedTotal = 0;
        int count = 0;
        string platform = GetActivePlatformName(); 

        for (int i = 0; i < targets.Count; i++)
        {
            var info = targets[i];
            var importer = AssetImporter.GetAtPath(info.AssetPath) as TextureImporter;
            if (importer == null) continue;

            EditorUtility.DisplayProgressBar(T(50), info.Texture.name, (float)i / targets.Count);
            long before = info.VRAMBytes;
            bool changed = false;

            if (info.CanOptimizeFormat)
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings()
                {
                    name = platform, 
                    overridden = true,
                    format = info.SuggestedFormat,
                    maxTextureSize = importer.maxTextureSize,
                    compressionQuality = 100
                });
                changed = true;
            }

            if (info.CanOptimizeSize)
            {
                importer.maxTextureSize = 2048;
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                importer.SetPlatformTextureSettings(settings);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                savedTotal += info.FormatSavings + info.SizeSavings;
                count++;
            }
        }

        EditorUtility.ClearProgressBar();
        SetStatus(Tf(52, count, FormatBytes(savedTotal)), false);
        VRAMScanTextures();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MiB";
    }

    private static Color GetVRAMColor(long bytes)
    {
        float mib = bytes / (1024f * 1024f);
        if (mib < 1f)  return new Color(0.4f, 0.85f, 0.4f);
        if (mib < 5f)  return new Color(0.9f, 0.85f, 0.4f);
        if (mib < 10f) return new Color(0.95f, 0.6f, 0.3f);
        return new Color(0.95f, 0.35f, 0.35f);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Utilities
    // ══════════════════════════════════════════════════════════════════════════
    private static bool IsLilToon(Material mat)
    {
        if (mat == null || mat.shader == null) return false;
        string sn = mat.shader.name.ToLower();
        return sn.Contains("liltoon") || sn.Contains("lil_toon");
    }

    private void SetStatus(string msg, bool warn) { _status = msg; _statusWarn = warn; }

    private void SectionLabel(string text) =>
        GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 11, normal = { textColor = ColAccent } });

    private static void DrawCenteredHint(string text) =>
        GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { fontStyle = FontStyle.Italic, fontSize = 11, wordWrap = true });

    private void DrawSelectButtons(List<MaterialInfo> mats, bool dietMode = false)
    {
        var selBtn = new GUIStyle(GUI.skin.button)
            { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
        EditorGUILayout.BeginHorizontal();
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColSelect;
        if (GUILayout.Button(T(16), selBtn, GUILayout.Height(28)))
            mats.ForEach(m => m.Selected = !dietMode || m.HasDiet || HasEnabledToggles(m));
        GUI.backgroundColor = ColDanger;
        if (GUILayout.Button(T(17), selBtn, GUILayout.Height(28)))
            mats.ForEach(m => m.Selected = false);
        GUI.backgroundColor = prev;
        EditorGUILayout.EndHorizontal();
    }

    private static void HLine()
    {
        GUILayout.Space(4);
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, ColLine);
        GUILayout.Space(4);
    }

    private static void GuiLine(int h, int space)
    {
        GUILayout.Space(space);
        var r = EditorGUILayout.GetControlRect(false, h);
        r.height = h;
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        GUILayout.Space(space);
    }

    private static string TruncatePath(string path, int max)
    {
        if (string.IsNullOrEmpty(path)) return "(Scene Instance)";
        return path.Length <= max ? path : "..." + path.Substring(path.Length - max);
    }

    // ── Settings ──
    private void SaveSettings()
    {
        EditorPrefs.SetInt("DiNeMaterialTool_Lang", L);
        EditorPrefs.SetInt("DiNeMaterialTool_Mode", (int)_mode);
        EditorPrefs.SetBool("DiNeMaterialTool_Children", _includeChildren);
        EditorPrefs.SetBool("DiNeMaterialTool_Inactive", _includeInactive);
        EditorPrefs.SetBool("DiNeMaterialTool_Preview", _previewOnly);
    }

    private void LoadSettings()
    {
        if (EditorPrefs.HasKey("DiNeMaterialTool_Lang"))
            _lang = (Lang)EditorPrefs.GetInt("DiNeMaterialTool_Lang");
        if (EditorPrefs.HasKey("DiNeMaterialTool_Mode"))
            _mode = (ToolMode)EditorPrefs.GetInt("DiNeMaterialTool_Mode");
        if (EditorPrefs.HasKey("DiNeMaterialTool_Children"))
            _includeChildren = EditorPrefs.GetBool("DiNeMaterialTool_Children");
        if (EditorPrefs.HasKey("DiNeMaterialTool_Inactive"))
            _includeInactive = EditorPrefs.GetBool("DiNeMaterialTool_Inactive");
        if (EditorPrefs.HasKey("DiNeMaterialTool_Preview"))
            _previewOnly = EditorPrefs.GetBool("DiNeMaterialTool_Preview");
    }
}
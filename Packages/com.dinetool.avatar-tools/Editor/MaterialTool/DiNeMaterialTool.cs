using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class DiNeMaterialTool : EditorWindow
{
    // ══════════════════════════════════════════════════════════════════════════
    //  Enums
    // ══════════════════════════════════════════════════════════════════════════
    private enum ToolMode { PresetApply, AORemove }
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
        /* 01 */ new[] { "AO Remove",           "AO 제거",            "AO削除"                  },
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
        // ── AO ──
        /* 33 */ new[] { "Scan",                "스캔",                "スキャン"                 },
        /* 34 */ new[] { "Remove AO Textures",  "AO 텍스처 제거",      "AOテクスチャを削除"        },
        /* 35 */ new[] { "LilToon: {0} found / With AO: {1}", "LilToon 마테리얼: {0}개 / AO 존재: {1}개", "LilToon: {0}個 / AO あり: {1}個" },
        /* 36 */ new[] { "No LilToon materials found.", "LilToon 마테리얼을 찾을 수 없습니다.", "LilToonマテリアルが見つかりません。" },
        /* 37 */ new[] { "AO Textures",         "AO 텍스쳐",           "AOテクスチャ"              },
        /* 38 */ new[] { "Scan complete — {0} LilToon material(s), {1} with AO texture(s)", "스캔 완료 — LilToon {0}개, AO 보유 {1}개", "スキャン完了 — LilToon {0}個, AO あり {1}個" },
        /* 39 */ new[] { "No AO textures to remove.", "제거할 AO 텍스처가 없습니다.", "削除するAOテクスチャがありません。" },
        /* 40 */ new[] { "[Preview] {1} AO texture(s) in {0} material(s) would be removed.", "[미리보기] {0}개 마테리얼에서 총 {1}개 AO 텍스처가 제거될 예정입니다.", "[プレビュー] {0}個のマテリアルから計 {1}個のAOテクスチャが削除される予定です。" },
        /* 41 */ new[] { "Remove AO Textures",  "AO 텍스처 제거",      "AOテクスチャの削除"        },
        /* 42 */ new[] { "This will remove AO textures from {0} material(s).\nSupports Undo.", "{0}개 마테리얼에서 AO 텍스처를 제거합니다.\nUndo로 되돌릴 수 있습니다.", "{0}個のマテリアルからAOテクスチャを削除します。\nUndoで元に戻せます。" },
        /* 43 */ new[] { "Continue?", "계속하시겠습니까?", "続けますか？" },
        /* 44 */ new[] { "Remove",   "제거",   "削除"     },
        /* 45 */ new[] { "Done — Removed {1} AO texture(s) from {0} material(s).", "완료 — {0}개 마테리얼에서 {1}개 AO 텍스처를 제거했습니다.", "完了 — {0}個のマテリアルから {1}個のAOテクスチャを削除しました。" },
        /* 46 */ new[] { "Preview Result", "미리보기 결과 확인", "プレビュー結果を確認" },
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
    //  AO Properties
    // ══════════════════════════════════════════════════════════════════════════
    private static readonly string[] LILTOON_AO_PROPS =
        { "_ShadowStrengthMask", "_ShadowBorderMask", "_ShadowBlurMask" };

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
    //  State — AO Mode
    // ══════════════════════════════════════════════════════════════════════════
    private List<MaterialInfo> _aoMats    = new List<MaterialInfo>();
    private bool               _aoScanned = false;
    private Vector2            _aoScroll;
    private Vector2            _aoMatScroll;

    // ══════════════════════════════════════════════════════════════════════════
    //  Shared Material Info
    // ══════════════════════════════════════════════════════════════════════════
    private class MaterialInfo
    {
        public Material      Material;
        public string        Path;
        public string        ShaderName;
        public bool          Selected;
        public bool          Foldout = true;
        public List<string>  AOProperties = new List<string>();
        public List<Texture> AOTextures   = new List<Texture>();
        public bool HasAO => AOProperties.Count > 0;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Menu / Lifecycle
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("DiNe/Material Tool")]
    public static void ShowWindow()
    {
        var w = GetWindow<DiNeMaterialTool>("DiNe Material Tool");
        w.minSize = new Vector2(340, 500);
        w.position = new Rect(w.position.x, w.position.y, 440, 780);
    }

    void OnEnable()
    {
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        _tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Material Tool", _tabIcon);
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
        else
            DrawAOMode();

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
        float iconSize = _windowIcon != null ? _windowIcon.height * 2f / 3f : 48;
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
        int next = DrawCustomToolbar(idx, new[] { T(0), T(1) }, 30);
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
    }

    private void AutoScan()
    {
        if (_targetObject == null) { _presetMats.Clear(); _presetScanned = false; _aoMats.Clear(); _aoScanned = false; _status = ""; return; }
        if (_mode == ToolMode.PresetApply) PresetScanMaterials();
        else AOScanMaterials();
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
    //  AO MODE
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawAOMode()
    {
        DrawAOActions();

        if (_aoScanned)
        {
            HLine();
            DrawAOMaterialResults();
        }
    }

    private void DrawAOActions()
    {
        var btn = new GUIStyle(GUI.skin.button)
            { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
        var prev = GUI.backgroundColor;

        bool canRemove = _aoScanned && _aoMats.Any(m => m.Selected && m.HasAO);
        GUI.enabled = canRemove;
        GUI.backgroundColor = _previewOnly ? ColWarn : ColDanger;
        if (GUILayout.Button(_previewOnly ? T(46) : T(34), btn, GUILayout.Height(38)))
            RemoveAOTextures();
        GUI.backgroundColor = prev;
        GUI.enabled = true;
    }

    private void DrawAOMaterialResults()
    {
        int lilCount = _aoMats.Count(m => m.ShaderName != null);
        int aoCount  = _aoMats.Count(m => m.HasAO);

        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;
        EditorGUILayout.LabelField(Tf(35, lilCount, aoCount), new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(5);
        DrawSelectButtons(_aoMats, true);
        EditorGUILayout.EndVertical();

        if (lilCount == 0) { EditorGUILayout.HelpBox(T(36), MessageType.Info); return; }

        _aoMatScroll = EditorGUILayout.BeginScrollView(_aoMatScroll);
        foreach (var info in _aoMats) DrawMaterialCard(info, true);
        EditorGUILayout.EndScrollView();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Material Card (NoMoreAO style)
    // ══════════════════════════════════════════════════════════════════════════
    private void DrawMaterialCard(MaterialInfo info, bool showAO)
    {
        bool hasAO = info.HasAO;
        if (showAO && !hasAO) info.Selected = false;

        EditorGUILayout.BeginVertical("box");

        // Header row
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !showAO || hasAO;
        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(20), GUILayout.Height(20));
        GUI.enabled = true;
        GUILayout.Space(2);

        if (showAO)
        {
            var badge = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold, fontSize = 10, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = hasAO ? new Color(1f, 0.55f, 0.2f) : new Color(0.4f, 0.85f, 0.4f) }
            };
            GUILayout.Label(hasAO ? $"AO ×{info.AOProperties.Count}" : "Clean", badge, GUILayout.Width(54), GUILayout.Height(20));
        }

        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Material.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(T(18), new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11, fontStyle = FontStyle.Bold }, GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Material);
        EditorGUILayout.EndHorizontal();

        // Card content
        if (info.Foldout)
        {
            GUILayout.Space(3);
            GuiLine(1, 2);
            GUILayout.Space(4);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
            if (!showAO && info.ShaderName != null)
                EditorGUILayout.LabelField(info.ShaderName,
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.45f, 0.72f, 1f) } });

            if (showAO && hasAO)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField(T(37), new GUIStyle(EditorStyles.boldLabel)
                    { fontSize = 11, normal = { textColor = new Color(0.95f, 0.6f, 0.3f) } });
                GUILayout.Space(4);

                for (int i = 0; i < info.AOProperties.Count; i++)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"• {info.AOProperties[i]}",
                        new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
                    GUILayout.Space(3);

                    EditorGUILayout.BeginHorizontal();
                    if (info.AOTextures[i] != null)
                    {
                        Rect previewRect = GUILayoutUtility.GetRect(72, 72, GUILayout.Width(72), GUILayout.Height(72));
                        Texture2D thumb = AssetPreview.GetAssetPreview(info.AOTextures[i]);
                        if (thumb != null)
                            GUI.DrawTexture(previewRect, thumb, ScaleMode.ScaleToFit);
                        else if (info.AOTextures[i] is Texture2D raw)
                        { EditorGUI.DrawPreviewTexture(previewRect, raw); Repaint(); }
                        else
                            GUI.Box(previewRect, "?");

                        GUILayout.Space(8);
                        EditorGUILayout.BeginVertical();
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField(info.AOTextures[i].name,
                            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
                        if (info.AOTextures[i] is Texture2D t2d)
                            EditorGUILayout.LabelField($"{t2d.width} × {t2d.height}  |  {t2d.format}",
                                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } });
                        GUILayout.Space(4);
                        EditorGUILayout.ObjectField(info.AOTextures[i], typeof(Texture), false, GUILayout.Height(18));
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(null)",
                            new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } });
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(3);
                }
            }

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
    //  AO Logic
    // ══════════════════════════════════════════════════════════════════════════
    private void AOScanMaterials()
    {
        _aoMats.Clear(); _aoScanned = false;
        if (_targetObject == null) { SetStatus(T(19), true); return; }
        Renderer[] renderers = _includeChildren
            ? _targetObject.GetComponentsInChildren<Renderer>(_includeInactive)
            : _targetObject.GetComponents<Renderer>();
        var seen = new HashSet<int>();
        foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || !seen.Add(mat.GetInstanceID())) continue;
                bool isLil = IsLilToon(mat);
                if (!isLil) continue;
                var info = new MaterialInfo { Material = mat, Path = AssetDatabase.GetAssetPath(mat), ShaderName = mat.shader.name, Selected = false };
                CollectAOProperties(mat, info);
                info.Selected = info.HasAO;
                _aoMats.Add(info);
            }
        _aoMats = _aoMats.OrderByDescending(m => m.HasAO).ThenByDescending(m => m.AOProperties.Count).ToList();
        _aoScanned = true;
        int lilCount = _aoMats.Count;
        int aoCount = _aoMats.Count(m => m.HasAO);
        SetStatus(Tf(38, lilCount, aoCount), aoCount > 0);
        Repaint();
    }

    private void CollectAOProperties(Material mat, MaterialInfo info)
    {
        foreach (string prop in LILTOON_AO_PROPS)
        {
            if (!mat.HasProperty(prop)) continue;
            var tex = mat.GetTexture(prop);
            if (tex == null) continue;
            info.AOProperties.Add(prop);
            info.AOTextures.Add(tex);
        }
    }

    private void RemoveAOTextures()
    {
        var targets = _aoMats.Where(m => m.Selected && m.HasAO).ToList();
        if (targets.Count == 0) { SetStatus(T(39), false); return; }
        if (_previewOnly)
        {
            int total = targets.Sum(m => m.AOProperties.Count);
            SetStatus(Tf(40, targets.Count, total), true);
            return;
        }
        bool ok = EditorUtility.DisplayDialog(T(41), Tf(42, targets.Count) + "\n" + T(43), T(44), T(25));
        if (!ok) return;

        Undo.RecordObjects(targets.Select(m => (Object)m.Material).ToArray(), "DiNe Material Tool AO Remove");
        int removed = 0;
        foreach (var info in targets)
        {
            foreach (string prop in info.AOProperties)
            { info.Material.SetTexture(prop, null); removed++; }
            EditorUtility.SetDirty(info.Material);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetStatus(Tf(45, targets.Count, removed), false);
        AOScanMaterials();
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

    private void DrawSelectButtons(List<MaterialInfo> mats, bool aoMode = false)
    {
        var selBtn = new GUIStyle(GUI.skin.button)
            { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, hover = { textColor = Color.white } };
        EditorGUILayout.BeginHorizontal();
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColSelect;
        if (GUILayout.Button(T(16), selBtn, GUILayout.Height(28)))
            mats.ForEach(m => m.Selected = !aoMode || m.HasAO);
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

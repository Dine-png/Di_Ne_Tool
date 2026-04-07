using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class DiNeLilToonPreset : EditorWindow
{
    // ──────────────────────────────────────────────────────────────────────────
    //  언어
    // ──────────────────────────────────────────────────────────────────────────
    private enum Lang { English, Korean, Japanese }
    private Lang _lang = Lang.Korean;
    private int  L => (int)_lang;

    private static readonly string[][] UI =
    {
        /* 00 */ new[] { "Preset Library",                          "프리셋 라이브러리",                   "プリセットライブラリ"                },
        /* 01 */ new[] { "Scan Project",                            "프로젝트 스캔",                        "プロジェクトスキャン"                },
        /* 02 */ new[] { "All",                                     "전체",                                "すべて"                             },
        /* 03 */ new[] { "No presets found in project.",            "프로젝트에서 프리셋을 찾을 수 없습니다.", "プロジェクト内にプリセットがありません。"},
        /* 04 */ new[] { "Search",                                  "검색",                                "検索"                               },
        /* 05 */ new[] { "Target Settings",                         "대상 설정",                           "対象設定"                           },
        /* 06 */ new[] { "Target Object",                           "대상 오브젝트",                        "対象オブジェクト"                   },
        /* 07 */ new[] { "Include Children",                        "자식 오브젝트 포함",                   "子オブジェクトを含む"                },
        /* 08 */ new[] { "Include Inactive",                        "비활성 오브젝트 포함",                  "非アクティブを含む"                 },
        /* 09 */ new[] { "[ Preview Mode ]  No changes will be saved", "[ 미리보기 모드 ]  실제로 적용되지 않습니다", "[ プレビューモード ]  変更されません" },
        /* 10 */ new[] { "[ Apply Mode ]  Preset will be applied", "[ 적용 모드 ]  마테리얼에 적용됩니다",  "[ 適用モード ]  適用されます"        },
        /* 11 */ new[] { "Scan Materials",                          "마테리얼 스캔",                        "マテリアルスキャン"                  },
        /* 12 */ new[] { "Apply Preset",                            "프리셋 적용",                          "プリセット適用"                     },
        /* 13 */ new[] { "LilToon materials: {0} / Selected: {1}", "LilToon 마테리얼: {0}개 / 선택: {1}개", "LilToonマテリアル: {0}個 / 選択: {1}個" },
        /* 14 */ new[] { "Select All",                              "전체 선택",                           "全て選択"                           },
        /* 15 */ new[] { "Deselect All",                            "전체 해제",                           "全て解除"                           },
        /* 16 */ new[] { "Ping",                                    "Ping",                                "Ping"                               },
        /* 17 */ new[] { "Please assign a Target Object first.",    "대상 오브젝트를 먼저 지정해주세요.",    "対象オブジェクトを先に指定してください。" },
        /* 18 */ new[] { "Scan complete — {0} material(s) found",  "스캔 완료 — 마테리얼 {0}개 발견",       "スキャン完了 — マテリアル {0}個を発見"  },
        /* 19 */ new[] { "No materials selected.",                  "선택된 마테리얼이 없습니다.",            "選択されたマテリアルがありません。"  },
        /* 20 */ new[] { "[Preview] {0} material(s) queued",       "[미리보기] {0}개 적용 예정",            "[プレビュー] {0}個適用予定"          },
        /* 21 */ new[] { "Apply preset [{1}] to {0} material(s).\nSupports Undo.", "{0}개 마테리얼에 [{1}]을(를) 적용합니다.\nUndo로 되돌릴 수 있습니다.", "{0}個のマテリアルに [{1}] を適用します。\nUndoで元に戻せます。" },
        /* 22 */ new[] { "Apply",                                   "적용",                                "適用"                               },
        /* 23 */ new[] { "Cancel",                                  "취소",                                "キャンセル"                         },
        /* 24 */ new[] { "Done — Applied to {0} material(s)",      "완료 — {0}개 마테리얼에 적용됨",        "完了 — {0}個に適用しました"          },
        /* 25 */ new[] { "Select a preset from the library above.", "위 라이브러리에서 프리셋을 선택하세요.", "上のライブラリからプリセットを選択してください。" },
        /* 26 */ new[] { "presets",                                 "개",                                  "個"                                 },
        /* 27 */ new[] { "Preview Check",                           "미리보기 확인",                        "プレビュー確認"                     },
        /* 28 */ new[] { "Colors",                                  "컬러",                                "カラー"                             },
        /* 29 */ new[] { "Floats",                                  "플로트",                               "フロート"                           },
        /* 30 */ new[] { "Vectors",                                 "벡터",                                "ベクター"                           },
        /* 31 */ new[] { "Textures",                                "텍스쳐",                               "テクスチャー"                       },
    };
    private string T(int i) => UI[i][L];
    private string Tf(int i, params object[] args) => string.Format(UI[i][L], args);

    // ──────────────────────────────────────────────────────────────────────────
    //  카테고리
    // ──────────────────────────────────────────────────────────────────────────
    private static readonly string[] CategoryNames =
        { "Skin", "Hair", "Cloth", "Nature", "Inorganic", "Effect", "Other" };

    private static readonly Color[] CategoryColors =
    {
        new Color(0.95f, 0.70f, 0.60f),   // Skin
        new Color(0.60f, 0.50f, 0.85f),   // Hair
        new Color(0.40f, 0.75f, 0.90f),   // Cloth
        new Color(0.50f, 0.85f, 0.50f),   // Nature
        new Color(0.70f, 0.70f, 0.70f),   // Inorganic
        new Color(0.90f, 0.60f, 0.90f),   // Effect
        new Color(0.80f, 0.80f, 0.50f),   // Other
    };

    // ──────────────────────────────────────────────────────────────────────────
    //  프리셋 라이브러리
    // ──────────────────────────────────────────────────────────────────────────
    private class PresetEntry
    {
        public ScriptableObject Asset;
        public string           Name;
        public int              CategoryIdx;   // 0-6, -1 = unknown
        public string           Path;
        public int              ColorCount;
        public int              FloatCount;
        public int              VectorCount;
        public int              TextureCount;
    }

    private List<PresetEntry> _library       = new List<PresetEntry>();
    private bool              _libraryReady  = false;
    private int               _categoryFilter = -1;   // -1 = All
    private string            _search         = "";
    private Vector2           _libScroll;
    private PresetEntry       _selected;

    // ──────────────────────────────────────────────────────────────────────────
    //  타겟 / 마테리얼
    // ──────────────────────────────────────────────────────────────────────────
    private GameObject _targetObject;
    private bool       _includeChildren = true;
    private bool       _includeInactive = true;
    private bool       _previewOnly     = false;

    private class MatInfo
    {
        public Material Mat;
        public string   Path;
        public string   ShaderName;
        public bool     Selected;
        public bool     Foldout = true;
    }

    private List<MatInfo> _mats    = new List<MatInfo>();
    private bool          _scanned = false;
    private string        _status  = "";
    private bool          _statusWarn;
    private Vector2       _matScroll;

    // ──────────────────────────────────────────────────────────────────────────
    //  UI 에셋
    // ──────────────────────────────────────────────────────────────────────────
    private Texture2D _windowIcon;
    private Font      _titleFont;

    // ──────────────────────────────────────────────────────────────────────────
    //  메뉴
    // ──────────────────────────────────────────────────────────────────────────
    [MenuItem("DiNe/Shading/LilToon Preset")]
    public static void ShowWindow()
    {
        var w = GetWindow<DiNeLilToonPreset>("DiNe LilToon Preset");
        w.minSize  = new Vector2(340, 500);
        w.position = new Rect(w.position.x, w.position.y, 460, 780);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  라이프사이클
    // ──────────────────────────────────────────────────────────────────────────
    void OnEnable()
    {
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("DiNe LilToon Preset", _windowIcon);
        LoadSettings();
        ScanLibrary();
    }

    void OnDisable() => SaveSettings();

    // ──────────────────────────────────────────────────────────────────────────
    //  OnGUI
    // ──────────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        DrawHeader();
        DrawLangBar();
        HLine();

        DrawLibrarySection();
        HLine();

        if (_selected != null)
        {
            DrawSelectedPresetInfo();
            HLine();
            DrawTargetSection();
            HLine();
            DrawActionButtons();
            HLine();
        }
        else
        {
            GUILayout.Space(8);
            DrawCenteredHint(T(25));
            GUILayout.Space(8);
        }

        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, _statusWarn ? MessageType.Warning : MessageType.Info);

        if (_scanned && _selected != null)
            DrawMaterialResults();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  헤더
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawHeader()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        var titleStyle = new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 28,
            normal    = { textColor = Color.white }
        };
        GUILayout.Label(new GUIContent("DiNe LilToon Preset", _windowIcon), titleStyle);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  언어 바
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawLangBar()
    {
        int idx = L;
        int next = GUILayout.Toolbar(idx, new[] { "English", "한국어", "日本語" }, GUILayout.Height(26));
        if (next != idx)
        {
            _lang = (Lang)next;
            SaveSettings();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  프리셋 라이브러리 섹션
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawLibrarySection()
    {
        // 섹션 헤더
        EditorGUILayout.BeginHorizontal();
        SectionLabel(T(0));
        GUILayout.FlexibleSpace();

        // 프리셋 수 뱃지
        if (_libraryReady)
        {
            GUILayout.Label($"{_library.Count} {T(26)}", new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } });
            GUILayout.Space(6);
        }

        // 스캔 버튼
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.28f, 0.42f, 0.62f);
        if (GUILayout.Button(T(1), EditorStyles.miniButton, GUILayout.Width(90), GUILayout.Height(20)))
            ScanLibrary();
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (!_libraryReady)
        {
            DrawCenteredHint(T(3));
            GUILayout.Space(6);
            return;
        }

        // ── 카테고리 탭 ──
        // "All" + 실제로 존재하는 카테고리만 표시
        var usedCats = _library.Select(p => p.CategoryIdx).Distinct().OrderBy(x => x).ToList();
        var tabLabels = new List<string> { T(2) };
        var tabValues = new List<int>   { -1 };
        foreach (var ci in usedCats)
        {
            string name = ci >= 0 && ci < CategoryNames.Length ? CategoryNames[ci] : "Other";
            tabLabels.Add(name);
            tabValues.Add(ci);
        }

        int curTabPos = tabValues.IndexOf(_categoryFilter);
        if (curTabPos < 0) curTabPos = 0;

        // 카테고리 탭 버튼 (색상 강조)
        EditorGUILayout.BeginHorizontal();
        for (int ti = 0; ti < tabLabels.Count; ti++)
        {
            bool isActive = (ti == curTabPos);
            int  catVal   = tabValues[ti];

            Color tabColor = isActive
                ? (catVal >= 0 && catVal < CategoryColors.Length ? CategoryColors[catVal] : new Color(0.40f, 0.60f, 0.90f))
                : new Color(0.22f, 0.22f, 0.25f);

            var btnStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = isActive ? Color.white : new Color(0.70f, 0.70f, 0.75f) }
            };

            prevBg = GUI.backgroundColor;
            GUI.backgroundColor = tabColor;
            if (GUILayout.Button(tabLabels[ti], btnStyle, GUILayout.Height(22)))
            {
                _categoryFilter = catVal;
                _libScroll = Vector2.zero;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── 검색 바 ──
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔍", GUILayout.Width(18));
        _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
        if (!string.IsNullOrEmpty(_search) && GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            _search = "";
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── 프리셋 리스트 ──
        var filtered = _library.Where(p =>
            (_categoryFilter < 0 || p.CategoryIdx == _categoryFilter) &&
            (string.IsNullOrEmpty(_search) || p.Name.ToLower().Contains(_search.ToLower()))
        ).ToList();

        float listHeight = Mathf.Clamp(filtered.Count * 36f + 8f, 80f, 220f);
        _libScroll = EditorGUILayout.BeginScrollView(_libScroll, GUILayout.Height(listHeight));

        if (filtered.Count == 0)
        {
            GUILayout.Space(20);
            DrawCenteredHint(T(3));
        }
        else
        {
            foreach (var entry in filtered)
                DrawPresetRow(entry);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPresetRow(PresetEntry entry)
    {
        bool isSelected = (_selected == entry);

        Color rowBg = isSelected
            ? new Color(0.22f, 0.35f, 0.55f)
            : new Color(0.20f, 0.20f, 0.23f);

        int catIdx = entry.CategoryIdx;
        Color catCol = (catIdx >= 0 && catIdx < CategoryColors.Length)
            ? CategoryColors[catIdx]
            : new Color(0.7f, 0.7f, 0.7f);

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = rowBg;
        EditorGUILayout.BeginHorizontal("box");
        GUI.backgroundColor = prevBg;

        // 카테고리 색 바
        var barRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(4), GUILayout.Height(24));
        EditorGUI.DrawRect(barRect, catCol);

        GUILayout.Space(6);

        // 프리셋 이름
        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
            fontSize  = 12,
            normal    = { textColor = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.88f) }
        };
        if (GUILayout.Button(entry.Name, nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(24)))
        {
            _selected = entry;
            _status   = "";
            _scanned  = false;
            _mats.Clear();
        }

        GUILayout.FlexibleSpace();

        // 카테고리 뱃지
        var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = catCol }
        };
        string catName = catIdx >= 0 && catIdx < CategoryNames.Length ? CategoryNames[catIdx] : "?";
        GUILayout.Label(catName, badgeStyle, GUILayout.Width(58));

        // Ping 버튼
        if (GUILayout.Button("⊙", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(entry.Asset);

        EditorGUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  선택된 프리셋 정보
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawSelectedPresetInfo()
    {
        if (_selected == null) return;

        int catIdx = _selected.CategoryIdx;
        Color catCol = catIdx >= 0 && catIdx < CategoryColors.Length
            ? CategoryColors[catIdx] : Color.white;

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.20f, 0.20f, 0.23f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prevBg;

        EditorGUILayout.BeginHorizontal();

        // 카테고리 사이드 바
        var barRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(6), GUILayout.Height(42));
        EditorGUI.DrawRect(barRect, catCol);
        GUILayout.Space(6);

        EditorGUILayout.BeginVertical();
        GUILayout.Label(_selected.Name, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 14, normal = { textColor = Color.white } });

        string catName = catIdx >= 0 && catIdx < CategoryNames.Length ? CategoryNames[catIdx] : "Other";
        GUILayout.Label(catName, new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = catCol }, fontStyle = FontStyle.Bold });
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        // 프로퍼티 카운트 배지들
        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        DrawBadge(T(28), _selected.ColorCount, new Color(0.9f, 0.55f, 0.55f));
        DrawBadge(T(29), _selected.FloatCount, new Color(0.55f, 0.85f, 0.55f));
        DrawBadge(T(30), _selected.VectorCount, new Color(0.55f, 0.75f, 1.0f));
        if (_selected.TextureCount > 0)
            DrawBadge(T(31), _selected.TextureCount, new Color(1.0f, 0.85f, 0.45f));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawBadge(string label, int count, Color col)
    {
        if (count <= 0) return;
        GUILayout.Label($"{label}: {count}", new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = col }
        });
        GUILayout.Space(4);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  타겟 설정
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawTargetSection()
    {
        SectionLabel(T(5));
        GUILayout.Space(4);

        _targetObject = (GameObject)EditorGUILayout.ObjectField(
            T(6), _targetObject, typeof(GameObject), true);

        GUILayout.Space(4);
        GuiLine(1, 2);

        _includeChildren = EditorGUILayout.Toggle(T(7), _includeChildren);
        _includeInactive = EditorGUILayout.Toggle(T(8), _includeInactive);

        GUILayout.Space(4);
        GuiLine(1, 2);

        EditorGUILayout.BeginHorizontal();
        _previewOnly = EditorGUILayout.Toggle(_previewOnly, GUILayout.Width(14));
        GUILayout.Label(_previewOnly ? T(9) : T(10), new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = _previewOnly ? new Color(1f, 0.75f, 0.2f) : new Color(0.45f, 0.85f, 0.45f) }
        });
        EditorGUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  액션 버튼
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawActionButtons()
    {
        var btn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white },
        };

        var prevBg = GUI.backgroundColor;

        // 스캔
        GUI.enabled = _targetObject != null;
        GUI.backgroundColor = new Color(0.35f, 0.48f, 0.70f);
        if (GUILayout.Button(T(11), btn, GUILayout.Height(34))) ScanMaterials();
        GUI.backgroundColor = prevBg;
        GUI.enabled = true;

        GUILayout.Space(4);

        // 적용
        bool canApply = _scanned && _selected != null && _mats.Any(m => m.Selected);
        GUI.enabled = canApply;
        GUI.backgroundColor = !canApply
            ? new Color(0.35f, 0.35f, 0.38f)
            : _previewOnly
                ? new Color(0.72f, 0.55f, 0.18f)
                : new Color(0.28f, 0.68f, 0.38f);
        if (GUILayout.Button(_previewOnly ? T(27) : T(12), btn, GUILayout.Height(34)))
            ApplyPreset();
        GUI.backgroundColor = prevBg;
        GUI.enabled = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  마테리얼 결과 목록
    // ──────────────────────────────────────────────────────────────────────────
    private void DrawMaterialResults()
    {
        GUILayout.Space(4);
        HLine();

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.20f, 0.20f, 0.23f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prevBg;

        int total    = _mats.Count;
        int selected = _mats.Count(m => m.Selected);
        EditorGUILayout.LabelField(Tf(13, total, selected),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });

        GUILayout.Space(4);

        var selBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white },
        };
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.28f, 0.55f, 0.32f);
        if (GUILayout.Button(T(14), selBtn, GUILayout.Height(24)))
            _mats.ForEach(m => m.Selected = true);
        GUI.backgroundColor = new Color(0.55f, 0.28f, 0.28f);
        if (GUILayout.Button(T(15), selBtn, GUILayout.Height(24)))
            _mats.ForEach(m => m.Selected = false);
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUILayout.Space(4);

        _matScroll = EditorGUILayout.BeginScrollView(_matScroll);
        foreach (var info in _mats)
            DrawMatCard(info);
        EditorGUILayout.EndScrollView();
    }

    private void DrawMatCard(MatInfo info)
    {
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.20f, 0.20f, 0.23f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prevBg;

        EditorGUILayout.BeginHorizontal();
        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(18), GUILayout.Height(18));
        GUILayout.Space(2);
        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Mat.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(T(16), new GUIStyle(EditorStyles.miniButton)
            { fontSize = 11 }, GUILayout.Width(40), GUILayout.Height(20)))
            EditorGUIUtility.PingObject(info.Mat);
        EditorGUILayout.EndHorizontal();

        if (info.Foldout)
        {
            GUILayout.Space(2);
            GuiLine(1, 1);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } });
            EditorGUILayout.LabelField(info.ShaderName,
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.45f, 0.72f, 1f) } });
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  라이브러리 스캔 (프로젝트 전체에서 lilToon 프리셋 탐색)
    // ──────────────────────────────────────────────────────────────────────────
    private void ScanLibrary()
    {
        _library.Clear();
        _libraryReady = false;

        // t:ScriptableObject 으로 찾고 "bases" 배열 존재 여부로 lilToon 프리셋 판별
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
        foreach (var guid in guids)
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            var    asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;

            var entry = TryParsePreset(asset, path);
            if (entry != null) _library.Add(entry);
        }

        // 카테고리 → 이름 순 정렬
        _library = _library
            .OrderBy(p => p.CategoryIdx < 0 ? 99 : p.CategoryIdx)
            .ThenBy(p => p.Name)
            .ToList();

        _libraryReady = true;

        // 기존 선택이 있으면 동기화
        if (_selected != null)
        {
            _selected = _library.FirstOrDefault(e => e.Asset == _selected.Asset) ?? null;
            if (_selected == null) { _scanned = false; _mats.Clear(); }
        }

        Repaint();
    }

    private PresetEntry TryParsePreset(ScriptableObject asset, string path)
    {
        try
        {
            var so    = new SerializedObject(asset);
            var bases = so.FindProperty("bases");
            if (bases == null || !bases.isArray) return null;

            var entry = new PresetEntry { Asset = asset, Path = path };

            // 이름
            if (bases.arraySize > 0)
                entry.Name = bases.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("name")?.stringValue;
            if (string.IsNullOrEmpty(entry.Name))
                entry.Name = asset.name;

            // 카테고리
            var catProp = so.FindProperty("category");
            entry.CategoryIdx = catProp?.enumValueIndex ?? -1;
            if (entry.CategoryIdx >= CategoryNames.Length) entry.CategoryIdx = CategoryNames.Length - 1;

            // 카운트
            var colors   = so.FindProperty("colors");
            var floats   = so.FindProperty("floats");
            var vectors  = so.FindProperty("vectors");
            var textures = so.FindProperty("textures");

            entry.ColorCount   = colors   is { isArray: true }   ? colors.arraySize   : 0;
            entry.FloatCount   = floats   is { isArray: true }   ? floats.arraySize   : 0;
            entry.VectorCount  = vectors  is { isArray: true }   ? vectors.arraySize  : 0;
            entry.TextureCount = textures is { isArray: true }   ? textures.arraySize : 0;

            return entry;
        }
        catch { return null; }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  마테리얼 스캔
    // ──────────────────────────────────────────────────────────────────────────
    private void ScanMaterials()
    {
        _mats.Clear();
        _scanned = false;

        if (_targetObject == null) { SetStatus(T(17), true); return; }

        Renderer[] renderers = _includeChildren
            ? _targetObject.GetComponentsInChildren<Renderer>(_includeInactive)
            : _targetObject.GetComponents<Renderer>();

        var seen = new HashSet<int>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (!seen.Add(mat.GetInstanceID())) continue;
                if (!IsLilToon(mat)) continue;

                _mats.Add(new MatInfo
                {
                    Mat        = mat,
                    Path       = AssetDatabase.GetAssetPath(mat),
                    ShaderName = mat.shader.name,
                    Selected   = true,
                });
            }
        }

        _mats    = _mats.OrderBy(m => m.Mat.name).ToList();
        _scanned = true;
        SetStatus(Tf(18, _mats.Count), _mats.Count == 0);
        Repaint();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  프리셋 적용
    // ──────────────────────────────────────────────────────────────────────────
    private void ApplyPreset()
    {
        var targets = _mats.Where(m => m.Selected).ToList();
        if (targets.Count == 0) { SetStatus(T(19), false); return; }
        if (_selected == null) return;

        if (_previewOnly)
        {
            SetStatus(Tf(20, targets.Count), true);
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            T(12),
            Tf(21, targets.Count, _selected.Name),
            T(22), T(23));
        if (!ok) return;

        var so       = new SerializedObject(_selected.Asset);
        var colors   = so.FindProperty("colors");
        var floats   = so.FindProperty("floats");
        var vectors  = so.FindProperty("vectors");
        var textures = so.FindProperty("textures");

        Undo.RecordObjects(targets.Select(m => (Object)m.Mat).ToArray(), "DiNe LilToon Preset Apply");

        int success = 0;
        foreach (var info in targets)
        {
            var mat = info.Mat;

            if (colors != null)
                for (int i = 0; i < colors.arraySize; i++)
                {
                    var e = colors.GetArrayElementAtIndex(i);
                    string p = e.FindPropertyRelative("name").stringValue;
                    Color  v = e.FindPropertyRelative("value").colorValue;
                    if (mat.HasProperty(p)) mat.SetColor(p, v);
                }

            if (floats != null)
                for (int i = 0; i < floats.arraySize; i++)
                {
                    var e = floats.GetArrayElementAtIndex(i);
                    string p = e.FindPropertyRelative("name").stringValue;
                    float  v = e.FindPropertyRelative("value").floatValue;
                    if (mat.HasProperty(p)) mat.SetFloat(p, v);
                }

            if (vectors != null)
                for (int i = 0; i < vectors.arraySize; i++)
                {
                    var e = vectors.GetArrayElementAtIndex(i);
                    string  p = e.FindPropertyRelative("name").stringValue;
                    Vector4 v = e.FindPropertyRelative("value").vector4Value;
                    if (mat.HasProperty(p)) mat.SetVector(p, v);
                }

            if (textures != null)
                for (int i = 0; i < textures.arraySize; i++)
                {
                    var e = textures.GetArrayElementAtIndex(i);
                    string  p  = e.FindPropertyRelative("name").stringValue;
                    Texture v  = e.FindPropertyRelative("value").objectReferenceValue as Texture;
                    Vector2 ofs = e.FindPropertyRelative("offset").vector2Value;
                    Vector2 sc  = e.FindPropertyRelative("scale").vector2Value;
                    if (mat.HasProperty(p))
                    {
                        mat.SetTexture(p, v);
                        mat.SetTextureOffset(p, ofs);
                        mat.SetTextureScale(p, sc);
                    }
                }

            EditorUtility.SetDirty(mat);
            success++;
        }

        AssetDatabase.SaveAssets();
        SetStatus(Tf(24, success), false);
        Repaint();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  헬퍼
    // ──────────────────────────────────────────────────────────────────────────
    private bool IsLilToon(Material mat)
    {
        if (mat?.shader == null) return false;
        string s = mat.shader.name.ToLower();
        return s.Contains("liltoon") || s.Contains("lil_toon");
    }

    private void SetStatus(string msg, bool warn) { _status = msg; _statusWarn = warn; }

    private string TruncatePath(string path, int max)
    {
        if (string.IsNullOrEmpty(path)) return "(Scene Instance)";
        return path.Length <= max ? path : "..." + path.Substring(path.Length - max);
    }

    private void GuiLine(int h, int space)
    {
        GUILayout.Space(space);
        var r = EditorGUILayout.GetControlRect(false, h);
        r.height = h;
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.40f, 0.6f));
        GUILayout.Space(space);
    }

    private static void HLine()
    {
        GUILayout.Space(4);
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.30f, 0.30f, 0.35f, 0.8f));
        GUILayout.Space(4);
    }

    private static void SectionLabel(string text)
    {
        GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 11, normal = { textColor = new Color(0.35f, 0.65f, 1.00f) } });
    }

    private static void DrawCenteredHint(string text)
    {
        GUILayout.Label(text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            { fontStyle = FontStyle.Italic, wordWrap = true,
              normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } });
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  설정 저장 / 불러오기
    // ──────────────────────────────────────────────────────────────────────────
    private void SaveSettings()
    {
        EditorPrefs.SetInt ("DiNeLilPreset_Lang",     (int)_lang);
        EditorPrefs.SetBool("DiNeLilPreset_Children", _includeChildren);
        EditorPrefs.SetBool("DiNeLilPreset_Inactive", _includeInactive);
        EditorPrefs.SetBool("DiNeLilPreset_Preview",  _previewOnly);
    }

    private void LoadSettings()
    {
        _lang            = (Lang)EditorPrefs.GetInt ("DiNeLilPreset_Lang",     0);
        _includeChildren = EditorPrefs.GetBool("DiNeLilPreset_Children", true);
        _includeInactive = EditorPrefs.GetBool("DiNeLilPreset_Inactive", true);
        _previewOnly     = EditorPrefs.GetBool("DiNeLilPreset_Preview",  false);
    }
}

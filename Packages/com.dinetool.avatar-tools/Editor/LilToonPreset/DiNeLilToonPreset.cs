using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class DiNeLilToonPreset : EditorWindow
{
    // ──────────────────────────────────────────────
    //  언어
    // ──────────────────────────────────────────────
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset _language = LanguagePreset.Korean;
    private string[] UI_TEXT;

    // ──────────────────────────────────────────────
    //  설정
    // ──────────────────────────────────────────────
    private ScriptableObject _preset;
    private GameObject       _targetObject;
    private bool             _includeChildren = true;
    private bool             _includeInactive = true;
    private bool             _previewOnly     = false;

    // ──────────────────────────────────────────────
    //  프리셋 정보 (파싱 결과)
    // ──────────────────────────────────────────────
    private string _presetName;
    private string _presetCategory;
    private int    _presetColorCount;
    private int    _presetFloatCount;
    private int    _presetVectorCount;
    private int    _presetTextureCount;
    private bool   _presetValid;

    // ──────────────────────────────────────────────
    //  스캔 결과
    // ──────────────────────────────────────────────
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
    private Vector2       _scroll;

    // ──────────────────────────────────────────────
    //  아이콘
    // ──────────────────────────────────────────────
    private Texture2D _windowIcon;

    // ──────────────────────────────────────────────
    //  카테고리 이름 매핑
    // ──────────────────────────────────────────────
    private static readonly string[] CategoryNames =
        { "Skin", "Hair", "Cloth", "Nature", "Inorganic", "Effect", "Other" };

    private static readonly Color[] CategoryColors =
    {
        new Color(0.95f, 0.7f, 0.6f),   // Skin
        new Color(0.6f, 0.5f, 0.85f),   // Hair
        new Color(0.4f, 0.75f, 0.9f),   // Cloth
        new Color(0.5f, 0.85f, 0.5f),   // Nature
        new Color(0.7f, 0.7f, 0.7f),    // Inorganic
        new Color(0.9f, 0.6f, 0.9f),    // Effect
        new Color(0.8f, 0.8f, 0.5f),    // Other
    };

    // ──────────────────────────────────────────────
    //  메뉴
    // ──────────────────────────────────────────────
    [MenuItem("DiNe/Shading/LilToon Preset")]
    public static void ShowWindow()
    {
        var w = GetWindow<DiNeLilToonPreset>("LilToon Preset");
        w.minSize  = new Vector2(300, 400);
        w.position = new Rect(w.position.x, w.position.y, 430, 680);
    }

    // ──────────────────────────────────────────────
    //  라이프사이클
    // ──────────────────────────────────────────────
    void OnEnable()
    {
        LoadSettings();
        SetLanguage(_language);
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
    }
    void OnDisable() => SaveSettings();

    // ──────────────────────────────────────────────
    //  OnGUI
    // ──────────────────────────────────────────────
    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ── 타이틀 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 28,
            normal    = new GUIStyleState() { textColor = Color.white }
        };
        GUILayout.Label(new GUIContent("LilToon Preset", _windowIcon), titleStyle);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 언어 탭 ──
        int li = (int)_language;
        int nl = GUILayout.Toolbar(li, new[] { "English", "한국어", "日本語" }, GUILayout.Height(28));
        if (nl != li) { _language = (LanguagePreset)nl; SetLanguage(_language); SaveSettings(); }

        GUILayout.Space(10);

        // ── 프리셋 파일 ──
        EditorGUILayout.BeginVertical("box");
        DrawPresetSection();
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 타겟 설정 ──
        EditorGUILayout.BeginVertical("box");
        DrawTargetSection();
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 액션 버튼 ──
        EditorGUILayout.BeginVertical("box");
        DrawActionButtons();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 상태 메시지 ──
        if (!string.IsNullOrEmpty(_status))
            EditorGUILayout.HelpBox(_status, _statusWarn ? MessageType.Warning : MessageType.Info);

        // ── 결과 목록 ──
        if (_scanned)
        {
            GUILayout.Space(5);
            DrawResults();
        }
    }

    // ──────────────────────────────────────────────
    //  프리셋 파일 섹션
    // ──────────────────────────────────────────────
    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField(UI_TEXT[0], new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        _preset = (ScriptableObject)EditorGUILayout.ObjectField(
            new GUIContent(UI_TEXT[1]), _preset, typeof(ScriptableObject), false);
        if (EditorGUI.EndChangeCheck())
            ParsePresetInfo();

        if (_preset != null && _presetValid)
        {
            GUILayout.Space(4);
            GuiLine(1, 2);

            // 프리셋 정보 표시
            Color catColor = Color.white;
            int catIdx = GetPresetCategoryIndex();
            if (catIdx >= 0 && catIdx < CategoryColors.Length)
                catColor = CategoryColors[catIdx];

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            // 카테고리 배지
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 10,
                alignment = TextAnchor.MiddleCenter,
                normal    = new GUIStyleState() { textColor = catColor }
            };
            GUILayout.Label($"[{_presetCategory}]", badgeStyle, GUILayout.Width(70));

            // 프리셋 이름
            GUIStyle nameStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 12,
                normal    = new GUIStyleState() { textColor = Color.white }
            };
            GUILayout.Label(_presetName, nameStyle);
            EditorGUILayout.EndHorizontal();

            // 프로퍼티 카운트
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = new GUIStyleState() { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            string countText = $"Colors: {_presetColorCount}  |  Floats: {_presetFloatCount}  |  Vectors: {_presetVectorCount}";
            if (_presetTextureCount > 0)
                countText += $"  |  Textures: {_presetTextureCount}";
            GUILayout.Label(countText, countStyle);
            EditorGUILayout.EndHorizontal();
        }
        else if (_preset != null && !_presetValid)
        {
            GUILayout.Space(2);
            EditorGUILayout.HelpBox(UI_TEXT[15], MessageType.Error);
        }
    }

    // ──────────────────────────────────────────────
    //  타겟 설정 섹션
    // ──────────────────────────────────────────────
    private void DrawTargetSection()
    {
        EditorGUILayout.LabelField(UI_TEXT[2], new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);

        _targetObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent(UI_TEXT[3]), _targetObject, typeof(GameObject), true);

        GUILayout.Space(4);
        GuiLine(1, 3);

        _includeChildren = EditorGUILayout.Toggle(UI_TEXT[4], _includeChildren);
        _includeInactive = EditorGUILayout.Toggle(UI_TEXT[5], _includeInactive);

        GUILayout.Space(4);
        GuiLine(1, 3);

        // 미리보기 모드 토글
        EditorGUILayout.BeginHorizontal();
        _previewOnly = EditorGUILayout.Toggle(_previewOnly, GUILayout.Width(14));
        GUILayout.Label(_previewOnly ? UI_TEXT[6] : UI_TEXT[7],
            new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                normal    = new GUIStyleState
                {
                    textColor = _previewOnly ? new Color(1f, 0.75f, 0.2f) : new Color(0.45f, 0.85f, 0.45f)
                }
            });
        EditorGUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────
    //  액션 버튼
    // ──────────────────────────────────────────────
    private void DrawActionButtons()
    {
        GUIStyle btn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14,
            fontStyle = FontStyle.Bold,
            normal    = new GUIStyleState() { textColor = Color.white },
            hover     = new GUIStyleState() { textColor = Color.white },
        };

        // 스캔
        GUI.enabled = _targetObject != null;
        GUI.backgroundColor = new Color(0.45f, 0.55f, 0.75f, 1f);
        if (GUILayout.Button(UI_TEXT[8], btn, GUILayout.Height(36))) ScanMaterials();
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.enabled = true;

        GUILayout.Space(5);

        // 적용
        bool canApply = _scanned && _presetValid && _preset != null
                        && _mats.Any(m => m.Selected);
        GUI.enabled = canApply;
        GUI.backgroundColor = canApply
            ? (_previewOnly ? new Color(0.75f, 0.6f, 0.2f) : new Color(0.35f, 0.75f, 0.45f))
            : new Color(0.4f, 0.4f, 0.4f);
        if (GUILayout.Button(_previewOnly ? UI_TEXT[16] : UI_TEXT[9], btn, GUILayout.Height(36)))
            ApplyPreset();
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.enabled = true;
    }

    // ──────────────────────────────────────────────
    //  결과 목록
    // ──────────────────────────────────────────────
    private void DrawResults()
    {
        EditorGUILayout.BeginVertical("box");

        int total    = _mats.Count;
        int selected = _mats.Count(m => m.Selected);
        EditorGUILayout.LabelField(
            string.Format(UI_TEXT[10], total, selected),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });

        GUILayout.Space(4);

        GUIStyle selBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = new GUIStyleState() { textColor = Color.white },
            hover     = new GUIStyleState() { textColor = Color.white },
        };
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[11], selBtn, GUILayout.Height(26)))
            _mats.ForEach(m => m.Selected = true);
        GUI.backgroundColor = new Color(0.55f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[12], selBtn, GUILayout.Height(26)))
            _mats.ForEach(m => m.Selected = false);
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUILayout.Space(4);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var info in _mats)
            DrawMatCard(info);
        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────
    //  마테리얼 카드
    // ──────────────────────────────────────────────
    private void DrawMatCard(MatInfo info)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(20), GUILayout.Height(20));
        GUILayout.Space(2);

        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Mat.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(UI_TEXT[13],
            new GUIStyle(EditorStyles.miniButton) { fontSize = 11, fontStyle = FontStyle.Bold },
            GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Mat);

        EditorGUILayout.EndHorizontal();

        if (info.Foldout)
        {
            GUILayout.Space(3);
            GuiLine(1, 2);
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = new GUIStyleState() { textColor = new Color(0.6f, 0.6f, 0.6f) } });

            EditorGUILayout.LabelField(info.ShaderName,
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = new GUIStyleState() { textColor = new Color(0.5f, 0.75f, 1f) } });

            EditorGUI.indentLevel--;
            GUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }

    // ──────────────────────────────────────────────
    //  프리셋 파싱
    // ──────────────────────────────────────────────
    private void ParsePresetInfo()
    {
        _presetValid = false;
        _presetName  = "";
        _presetCategory = "";
        _presetColorCount = _presetFloatCount = _presetVectorCount = _presetTextureCount = 0;

        if (_preset == null) return;

        var so = new SerializedObject(_preset);

        // bases 배열에서 이름 가져오기
        var bases = so.FindProperty("bases");
        if (bases == null || !bases.isArray) return; // lilToon preset 이 아닌 경우

        _presetValid = true;

        if (bases.arraySize > 0)
        {
            var firstBase = bases.GetArrayElementAtIndex(0);
            _presetName = firstBase.FindPropertyRelative("name")?.stringValue ?? _preset.name;
        }
        if (string.IsNullOrEmpty(_presetName))
            _presetName = _preset.name;

        // 카테고리
        var categoryProp = so.FindProperty("category");
        if (categoryProp != null)
        {
            int catIdx = categoryProp.enumValueIndex;
            _presetCategory = catIdx >= 0 && catIdx < CategoryNames.Length
                ? CategoryNames[catIdx]
                : "Unknown";
        }

        // 프로퍼티 카운트
        var colors   = so.FindProperty("colors");
        var floats   = so.FindProperty("floats");
        var vectors  = so.FindProperty("vectors");
        var textures = so.FindProperty("textures");

        _presetColorCount   = colors   != null && colors.isArray   ? colors.arraySize   : 0;
        _presetFloatCount   = floats   != null && floats.isArray   ? floats.arraySize   : 0;
        _presetVectorCount  = vectors  != null && vectors.isArray  ? vectors.arraySize  : 0;
        _presetTextureCount = textures != null && textures.isArray ? textures.arraySize : 0;
    }

    private int GetPresetCategoryIndex()
    {
        if (_preset == null) return -1;
        var so = new SerializedObject(_preset);
        var categoryProp = so.FindProperty("category");
        return categoryProp?.enumValueIndex ?? -1;
    }

    // ──────────────────────────────────────────────
    //  스캔
    // ──────────────────────────────────────────────
    private void ScanMaterials()
    {
        _mats.Clear();
        _scanned = false;

        if (_targetObject == null) { SetStatus(UI_TEXT[17], true); return; }

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

        _mats = _mats.OrderBy(m => m.Mat.name).ToList();
        _scanned = true;
        SetStatus(string.Format(UI_TEXT[18], _mats.Count), _mats.Count == 0);
        Repaint();
    }

    // ──────────────────────────────────────────────
    //  적용
    // ──────────────────────────────────────────────
    private void ApplyPreset()
    {
        var targets = _mats.Where(m => m.Selected).ToList();
        if (targets.Count == 0) { SetStatus(UI_TEXT[19], false); return; }
        if (_preset == null || !_presetValid) return;

        if (_previewOnly)
        {
            SetStatus(string.Format(UI_TEXT[20], targets.Count), true);
            return;
        }

        // 확인 다이얼로그
        bool ok = EditorUtility.DisplayDialog(
            UI_TEXT[9],
            string.Format(UI_TEXT[21], targets.Count, _presetName),
            UI_TEXT[22], UI_TEXT[23]);
        if (!ok) return;

        var so = new SerializedObject(_preset);
        var colors   = so.FindProperty("colors");
        var floats   = so.FindProperty("floats");
        var vectors  = so.FindProperty("vectors");
        var textures = so.FindProperty("textures");

        Undo.RecordObjects(targets.Select(m => (Object)m.Mat).ToArray(), "DiNe LilToon Preset Apply");

        int success = 0;
        foreach (var info in targets)
        {
            var mat = info.Mat;

            // Colors 적용
            if (colors != null)
            {
                for (int i = 0; i < colors.arraySize; i++)
                {
                    var elem = colors.GetArrayElementAtIndex(i);
                    string propName = elem.FindPropertyRelative("name").stringValue;
                    Color  value    = elem.FindPropertyRelative("value").colorValue;
                    if (mat.HasProperty(propName))
                        mat.SetColor(propName, value);
                }
            }

            // Floats 적용
            if (floats != null)
            {
                for (int i = 0; i < floats.arraySize; i++)
                {
                    var elem = floats.GetArrayElementAtIndex(i);
                    string propName = elem.FindPropertyRelative("name").stringValue;
                    float  value    = elem.FindPropertyRelative("value").floatValue;
                    if (mat.HasProperty(propName))
                        mat.SetFloat(propName, value);
                }
            }

            // Vectors 적용
            if (vectors != null)
            {
                for (int i = 0; i < vectors.arraySize; i++)
                {
                    var elem = vectors.GetArrayElementAtIndex(i);
                    string propName = elem.FindPropertyRelative("name").stringValue;
                    Vector4 value   = elem.FindPropertyRelative("value").vector4Value;
                    if (mat.HasProperty(propName))
                        mat.SetVector(propName, value);
                }
            }

            // Textures 적용
            if (textures != null)
            {
                for (int i = 0; i < textures.arraySize; i++)
                {
                    var elem = textures.GetArrayElementAtIndex(i);
                    string  propName = elem.FindPropertyRelative("name").stringValue;
                    Texture value    = elem.FindPropertyRelative("value").objectReferenceValue as Texture;
                    Vector2 offset   = elem.FindPropertyRelative("offset").vector2Value;
                    Vector2 scale    = elem.FindPropertyRelative("scale").vector2Value;
                    if (mat.HasProperty(propName))
                    {
                        mat.SetTexture(propName, value);
                        mat.SetTextureOffset(propName, offset);
                        mat.SetTextureScale(propName, scale);
                    }
                }
            }

            EditorUtility.SetDirty(mat);
            success++;
        }

        AssetDatabase.SaveAssets();
        SetStatus(string.Format(UI_TEXT[24], success), false);
        Repaint();
    }

    // ──────────────────────────────────────────────
    //  헬퍼
    // ──────────────────────────────────────────────
    private bool IsLilToon(Material mat)
    {
        if (mat?.shader == null) return false;
        string sn = mat.shader.name.ToLower();
        return sn.Contains("liltoon") || sn.Contains("lil_toon");
    }

    private void SetStatus(string msg, bool warn)
    {
        _status     = msg;
        _statusWarn = warn;
    }

    private string TruncatePath(string path, int max)
    {
        if (string.IsNullOrEmpty(path)) return "(Scene Instance)";
        return path.Length <= max ? path : "..." + path.Substring(path.Length - max);
    }

    private void GuiLine(int h, int space)
    {
        GUILayout.Space(space);
        Rect r = EditorGUILayout.GetControlRect(false, h);
        r.height = h;
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 0.4f));
        GUILayout.Space(space);
    }

    // ──────────────────────────────────────────────
    //  설정 저장 / 불러오기
    // ──────────────────────────────────────────────
    private void SaveSettings()
    {
        EditorPrefs.SetInt("DiNeLilPreset_Language",  (int)_language);
        EditorPrefs.SetBool("DiNeLilPreset_Children", _includeChildren);
        EditorPrefs.SetBool("DiNeLilPreset_Inactive", _includeInactive);
        EditorPrefs.SetBool("DiNeLilPreset_Preview",  _previewOnly);
    }

    private void LoadSettings()
    {
        if (EditorPrefs.HasKey("DiNeLilPreset_Language"))
            _language        = (LanguagePreset)EditorPrefs.GetInt("DiNeLilPreset_Language");
        if (EditorPrefs.HasKey("DiNeLilPreset_Children"))
            _includeChildren = EditorPrefs.GetBool("DiNeLilPreset_Children");
        if (EditorPrefs.HasKey("DiNeLilPreset_Inactive"))
            _includeInactive = EditorPrefs.GetBool("DiNeLilPreset_Inactive");
        if (EditorPrefs.HasKey("DiNeLilPreset_Preview"))
            _previewOnly     = EditorPrefs.GetBool("DiNeLilPreset_Preview");
    }

    // ──────────────────────────────────────────────
    //  다국어
    // ──────────────────────────────────────────────
    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[]
                {
                    /* 00 */ "프리셋 파일",
                    /* 01 */ "릴툰 프리셋 (.asset)",
                    /* 02 */ "대상 설정",
                    /* 03 */ "대상 오브젝트",
                    /* 04 */ "자식 오브젝트 포함",
                    /* 05 */ "비활성 오브젝트 포함",
                    /* 06 */ "[ 미리보기 모드 ]  실제로 적용되지 않습니다",
                    /* 07 */ "[ 적용 모드 ]  마테리얼에 프리셋이 적용됩니다",
                    /* 08 */ "스캔",
                    /* 09 */ "프리셋 적용",
                    /* 10 */ "LilToon 마테리얼: {0}개 / 선택됨: {1}개",
                    /* 11 */ "전체 선택",
                    /* 12 */ "전체 해제",
                    /* 13 */ "선택",
                    /* 14 */ "Ping",
                    /* 15 */ "릴툰 프리셋 파일이 아닙니다.",
                    /* 16 */ "미리보기 확인",
                    /* 17 */ "대상 오브젝트를 먼저 지정해주세요.",
                    /* 18 */ "스캔 완료 — LilToon 마테리얼 {0}개 발견",
                    /* 19 */ "선택된 마테리얼이 없습니다.",
                    /* 20 */ "[미리보기] {0}개 마테리얼에 프리셋 적용 예정",
                    /* 21 */ "{0}개 마테리얼에 프리셋 [{1}]을(를) 적용합니다.\nUndo로 되돌릴 수 있습니다.",
                    /* 22 */ "적용",
                    /* 23 */ "취소",
                    /* 24 */ "완료 — {0}개 마테리얼에 프리셋 적용됨",
                };
                break;

            case LanguagePreset.Japanese:
                UI_TEXT = new string[]
                {
                    /* 00 */ "プリセットファイル",
                    /* 01 */ "lilToonプリセット (.asset)",
                    /* 02 */ "対象設定",
                    /* 03 */ "対象オブジェクト",
                    /* 04 */ "子オブジェクトを含む",
                    /* 05 */ "非アクティブを含む",
                    /* 06 */ "[ プレビューモード ]  実際には変更されません",
                    /* 07 */ "[ 適用モード ]  マテリアルにプリセットが適用されます",
                    /* 08 */ "スキャン",
                    /* 09 */ "プリセット適用",
                    /* 10 */ "LilToonマテリアル: {0}個 / 選択中: {1}個",
                    /* 11 */ "全て選択",
                    /* 12 */ "全て解除",
                    /* 13 */ "選択",
                    /* 14 */ "Ping",
                    /* 15 */ "lilToonプリセットファイルではありません。",
                    /* 16 */ "プレビュー確認",
                    /* 17 */ "対象オブジェクトを先に指定してください。",
                    /* 18 */ "スキャン完了 — LilToonマテリアル {0}個を発見",
                    /* 19 */ "選択されたマテリアルがありません。",
                    /* 20 */ "[プレビュー] {0}個のマテリアルにプリセットを適用予定",
                    /* 21 */ "{0}個のマテリアルにプリセット [{1}] を適用します。\nUndoで元に戻せます。",
                    /* 22 */ "適用",
                    /* 23 */ "キャンセル",
                    /* 24 */ "完了 — {0}個のマテリアルにプリセットを適用しました",
                };
                break;

            default: // English
                UI_TEXT = new string[]
                {
                    /* 00 */ "Preset File",
                    /* 01 */ "lilToon Preset (.asset)",
                    /* 02 */ "Target Settings",
                    /* 03 */ "Target Object",
                    /* 04 */ "Include Children",
                    /* 05 */ "Include Inactive",
                    /* 06 */ "[ Preview Mode ]  No changes will be saved",
                    /* 07 */ "[ Apply Mode ]  Preset will be applied to materials",
                    /* 08 */ "Scan",
                    /* 09 */ "Apply Preset",
                    /* 10 */ "LilToon materials: {0} / Selected: {1}",
                    /* 11 */ "Select All",
                    /* 12 */ "Deselect All",
                    /* 13 */ "Ping",
                    /* 14 */ "Ping",
                    /* 15 */ "This is not a valid lilToon preset file.",
                    /* 16 */ "Preview Check",
                    /* 17 */ "Please assign a Target Object first.",
                    /* 18 */ "Scan complete — {0} LilToon material(s) found",
                    /* 19 */ "No materials selected.",
                    /* 20 */ "[Preview] {0} material(s) will have preset applied",
                    /* 21 */ "Apply preset [{1}] to {0} material(s).\nSupports Undo.",
                    /* 22 */ "Apply",
                    /* 23 */ "Cancel",
                    /* 24 */ "Done — Preset applied to {0} material(s)",
                };
                break;
        }
    }
}

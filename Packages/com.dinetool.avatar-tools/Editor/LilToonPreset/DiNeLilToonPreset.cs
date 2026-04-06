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
    //  렌더링 모드 enum
    // ──────────────────────────────────────────────
    private enum LilRenderingMode
    {
        Opaque             = 0,
        Cutout             = 1,
        Transparent        = 2,
        OnePassTransparent = 3,
        TwoPassTransparent = 4,
    }

    // Outline 처리 옵션
    private enum OutlineMode { KeepAsIs, ForceAdd, ForceRemove }

    // ──────────────────────────────────────────────
    //  설정
    // ──────────────────────────────────────────────
    private GameObject   _targetObject;
    private bool         _includeChildren = true;
    private bool         _includeInactive = true;
    private LilRenderingMode _targetMode  = LilRenderingMode.Opaque;
    private OutlineMode  _outlineMode     = OutlineMode.KeepAsIs;
    private bool         _previewOnly     = false;

    // ──────────────────────────────────────────────
    //  스캔 결과
    // ──────────────────────────────────────────────
    private class MatInfo
    {
        public Material        Mat;
        public string          Path;
        public LilRenderingMode CurrentMode;
        public bool            HasOutline;
        public string          BaseName;   // "lilToon" or "lilToonLite" etc.
        public bool            IsSpecial;  // Fur / Gem / Refraction 등 특수 셰이더
        public bool            Selected;
        public bool            Foldout = true;
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
    //  렌더링 모드별 색상
    // ──────────────────────────────────────────────
    private static readonly Dictionary<LilRenderingMode, Color> ModeColors = new Dictionary<LilRenderingMode, Color>
    {
        { LilRenderingMode.Opaque,             new Color(0.4f, 0.85f, 0.4f)  },
        { LilRenderingMode.Cutout,             new Color(0.9f, 0.7f,  0.2f)  },
        { LilRenderingMode.Transparent,        new Color(0.3f, 0.65f, 1f)    },
        { LilRenderingMode.OnePassTransparent, new Color(0.5f, 0.3f,  1f)    },
        { LilRenderingMode.TwoPassTransparent, new Color(0.8f, 0.35f, 0.8f)  },
    };

    // ──────────────────────────────────────────────
    //  메뉴
    // ──────────────────────────────────────────────
    [MenuItem("DiNe/LilToon Preset")]
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

        // ── 설정 섹션 ──
        EditorGUILayout.BeginVertical("box");
        DrawSettings();
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // ── 프리셋 (적용할 렌더링 모드) ──
        EditorGUILayout.BeginVertical("box");
        DrawPresetSection();
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
    //  설정 섹션
    // ──────────────────────────────────────────────
    private void DrawSettings()
    {
        EditorGUILayout.LabelField(UI_TEXT[0], new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);

        _targetObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent(UI_TEXT[1]), _targetObject, typeof(GameObject), true);

        GUILayout.Space(4);
        GuiLine(1, 3);

        _includeChildren = EditorGUILayout.Toggle(UI_TEXT[2], _includeChildren);
        _includeInactive = EditorGUILayout.Toggle(UI_TEXT[3], _includeInactive);
    }

    // ──────────────────────────────────────────────
    //  프리셋 섹션
    // ──────────────────────────────────────────────
    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField(UI_TEXT[4], new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(6);

        // 렌더링 모드 선택 버튼 (5개)
        string[] modeLabels = { UI_TEXT[10], UI_TEXT[11], UI_TEXT[12], UI_TEXT[13], UI_TEXT[14] };
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < modeLabels.Length; i++)
        {
            var mode = (LilRenderingMode)i;
            bool selected = _targetMode == mode;
            Color modeCol = ModeColors[mode];

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 10,
                normal    = new GUIStyleState() { textColor = selected ? Color.white : new Color(0.7f, 0.7f, 0.7f) },
                hover     = new GUIStyleState() { textColor = Color.white },
            };

            GUI.backgroundColor = selected ? modeCol : new Color(0.25f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button(modeLabels[i], btnStyle, GUILayout.Height(30)))
                _targetMode = mode;
        }
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);
        GuiLine(1, 3);

        // Outline 처리
        EditorGUILayout.LabelField(UI_TEXT[5], new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);

        string[] outlineLabels = { UI_TEXT[15], UI_TEXT[16], UI_TEXT[17] };
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < outlineLabels.Length; i++)
        {
            var opt = (OutlineMode)i;
            bool sel = _outlineMode == opt;
            GUIStyle s = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 10,
                normal    = new GUIStyleState() { textColor = sel ? Color.white : new Color(0.7f, 0.7f, 0.7f) },
            };
            GUI.backgroundColor = sel ? new Color(0.4f, 0.5f, 0.7f) : new Color(0.25f, 0.25f, 0.25f, 1f);
            if (GUILayout.Button(outlineLabels[i], s, GUILayout.Height(26)))
                _outlineMode = opt;
        }
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
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
        bool canApply = _scanned && _mats.Any(m => m.Selected && !m.IsSpecial);
        GUI.enabled = canApply;
        Color applyCol = _previewOnly
            ? new Color(0.75f, 0.6f, 0.2f)
            : ModeColors[_targetMode];
        GUI.backgroundColor = applyCol;
        if (GUILayout.Button(_previewOnly ? UI_TEXT[18] : UI_TEXT[9], btn, GUILayout.Height(36)))
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

        // 헤더
        int total    = _mats.Count;
        int selected = _mats.Count(m => m.Selected);
        EditorGUILayout.LabelField(
            string.Format(UI_TEXT[19], total, selected),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });

        GUILayout.Space(4);

        // 전체 선택 / 해제
        GUIStyle selBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = new GUIStyleState() { textColor = Color.white },
            hover     = new GUIStyleState() { textColor = Color.white },
        };
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[20], selBtn, GUILayout.Height(26)))
            _mats.ForEach(m => m.Selected = !m.IsSpecial);
        GUI.backgroundColor = new Color(0.55f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[21], selBtn, GUILayout.Height(26)))
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

        // 체크박스
        GUI.enabled   = !info.IsSpecial;
        info.Selected = GUILayout.Toggle(info.Selected, "", GUILayout.Width(20), GUILayout.Height(20));
        GUI.enabled   = true;

        GUILayout.Space(2);

        // 현재 렌더링 모드 배지
        Color badgeCol = info.IsSpecial
            ? new Color(0.6f, 0.6f, 0.6f)
            : ModeColors.ContainsKey(info.CurrentMode) ? ModeColors[info.CurrentMode] : Color.gray;

        GUIStyle badge = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 9,
            alignment = TextAnchor.MiddleCenter,
            normal    = new GUIStyleState() { textColor = badgeCol }
        };

        string badgeText = info.IsSpecial ? "Special" : GetModeShortLabel(info.CurrentMode);
        if (info.HasOutline && !info.IsSpecial) badgeText += "+OL";
        GUILayout.Label(badgeText, badge, GUILayout.Width(62), GUILayout.Height(20));

        // 폴드아웃
        info.Foldout = EditorGUILayout.Foldout(info.Foldout, info.Mat.name, true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });

        GUILayout.FlexibleSpace();

        // Ping
        if (GUILayout.Button(UI_TEXT[22],
            new GUIStyle(EditorStyles.miniButton) { fontSize = 11, fontStyle = FontStyle.Bold },
            GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Mat);

        EditorGUILayout.EndHorizontal();

        if (info.Foldout)
        {
            GUILayout.Space(3);
            GuiLine(1, 2);
            EditorGUI.indentLevel++;

            // 경로
            EditorGUILayout.LabelField(TruncatePath(info.Path, 54),
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = new GUIStyleState() { textColor = new Color(0.6f, 0.6f, 0.6f) } });

            // 현재 셰이더명
            EditorGUILayout.LabelField(info.Mat.shader.name,
                new GUIStyle(EditorStyles.miniLabel)
                    { normal = new GUIStyleState() { textColor = new Color(0.5f, 0.75f, 1f) } });

            if (info.IsSpecial)
            {
                EditorGUILayout.LabelField(UI_TEXT[23],
                    new GUIStyle(EditorStyles.miniLabel)
                        { normal = new GUIStyleState() { textColor = new Color(0.9f, 0.5f, 0.2f) } });
            }
            else
            {
                // 적용 후 셰이더 미리보기
                string preview = GetTargetShaderName(info, _targetMode, _outlineMode);
                string previewLabel = preview != null
                    ? $"→  {preview}"
                    : UI_TEXT[24];
                EditorGUILayout.LabelField(previewLabel,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = new GUIStyleState()
                        {
                            textColor = preview != null
                                ? new Color(0.4f, 0.9f, 0.5f)
                                : new Color(0.9f, 0.4f, 0.4f)
                        }
                    });
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }

    // ──────────────────────────────────────────────
    //  스캔
    // ──────────────────────────────────────────────
    private void ScanMaterials()
    {
        _mats.Clear();
        _scanned = false;

        if (_targetObject == null) { SetStatus(UI_TEXT[25], true); return; }

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

                var info = BuildMatInfo(mat);
                _mats.Add(info);
            }
        }

        // 정렬: 현재 모드가 타겟과 다른 것 먼저, Special 마지막
        _mats = _mats
            .OrderBy(m => m.IsSpecial ? 1 : 0)
            .ThenBy(m => m.CurrentMode == _targetMode ? 1 : 0)
            .ToList();

        _scanned = true;
        int special = _mats.Count(m => m.IsSpecial);
        SetStatus(string.Format(UI_TEXT[26], _mats.Count, special), special > 0);
        Repaint();
    }

    private MatInfo BuildMatInfo(Material mat)
    {
        string sn = mat.shader.name;

        var info = new MatInfo
        {
            Mat        = mat,
            Path       = AssetDatabase.GetAssetPath(mat),
            HasOutline = sn.Contains("Outline"),
        };

        // 베이스 이름 판별
        if (sn.StartsWith("lilToonLite"))
            info.BaseName = "lilToonLite";
        else if (sn.StartsWith("lilToon"))
            info.BaseName = "lilToon";
        else
            info.BaseName = sn;

        // 특수 셰이더 (Fur / Gem / Refraction / FakeShadow 등)
        info.IsSpecial = sn.Contains("Fur") || sn.Contains("Gem") ||
                         sn.Contains("Refraction") || sn.Contains("FakeShadow") ||
                         sn.Contains("Tessellation");

        // 현재 렌더링 모드
        info.CurrentMode = DetectMode(sn);

        // 기본 선택: 현재 모드가 타겟과 다를 때만 선택
        info.Selected = !info.IsSpecial && info.CurrentMode != _targetMode;

        return info;
    }

    private LilRenderingMode DetectMode(string shaderName)
    {
        if (shaderName.Contains("Two Pass Transparent")) return LilRenderingMode.TwoPassTransparent;
        if (shaderName.Contains("One Pass Transparent")) return LilRenderingMode.OnePassTransparent;
        if (shaderName.Contains("Cutout"))               return LilRenderingMode.Cutout;
        if (shaderName.Contains("Transparent"))          return LilRenderingMode.Transparent;
        return LilRenderingMode.Opaque;
    }

    // ──────────────────────────────────────────────
    //  타겟 셰이더 이름 계산
    // ──────────────────────────────────────────────
    private string GetTargetShaderName(MatInfo info, LilRenderingMode mode, OutlineMode outlineOpt)
    {
        if (info.IsSpecial) return null;

        bool outline;
        switch (outlineOpt)
        {
            case OutlineMode.ForceAdd:    outline = true;  break;
            case OutlineMode.ForceRemove: outline = false; break;
            default:                     outline = info.HasOutline; break;
        }

        // lilToonLite 는 One Pass / Two Pass 없음
        if (info.BaseName == "lilToonLite" &&
            (mode == LilRenderingMode.OnePassTransparent || mode == LilRenderingMode.TwoPassTransparent))
            return null;

        string outlineSuffix = outline ? " Outline" : "";

        switch (mode)
        {
            case LilRenderingMode.Opaque:
                return info.BaseName + outlineSuffix;
            case LilRenderingMode.Cutout:
                return info.BaseName + " Cutout" + outlineSuffix;
            case LilRenderingMode.Transparent:
                return info.BaseName + " Transparent" + outlineSuffix;
            case LilRenderingMode.OnePassTransparent:
                return info.BaseName + " One Pass Transparent" + outlineSuffix;
            case LilRenderingMode.TwoPassTransparent:
                return info.BaseName + " Two Pass Transparent" + outlineSuffix;
            default:
                return null;
        }
    }

    // ──────────────────────────────────────────────
    //  적용
    // ──────────────────────────────────────────────
    private void ApplyPreset()
    {
        var targets = _mats.Where(m => m.Selected && !m.IsSpecial).ToList();
        if (targets.Count == 0) { SetStatus(UI_TEXT[27], false); return; }

        if (_previewOnly)
        {
            int changeable = targets.Count(m => GetTargetShaderName(m, _targetMode, _outlineMode) != null);
            SetStatus(string.Format(UI_TEXT[28], changeable, targets.Count), true);
            return;
        }

        // 확인 다이얼로그
        bool ok = EditorUtility.DisplayDialog(
            UI_TEXT[9],
            string.Format(UI_TEXT[29], targets.Count, GetModeLabel(_targetMode)),
            UI_TEXT[30], UI_TEXT[31]);
        if (!ok) return;

        Undo.RecordObjects(targets.Select(m => (Object)m.Mat).ToArray(), "DiNe LilToon Preset");

        int success = 0, failed = 0;
        foreach (var info in targets)
        {
            string targetShaderName = GetTargetShaderName(info, _targetMode, _outlineMode);
            if (targetShaderName == null) { failed++; continue; }

            Shader sh = Shader.Find(targetShaderName);
            if (sh == null)
            {
                Debug.LogWarning($"[DiNe LilToon Preset] 셰이더를 찾을 수 없음: {targetShaderName}");
                failed++;
                continue;
            }

            info.Mat.shader = sh;

            // _TransparentMode 프로퍼티 동기화 (있을 경우)
            if (info.Mat.HasProperty("_TransparentMode"))
                info.Mat.SetFloat("_TransparentMode", (float)_targetMode);

            EditorUtility.SetDirty(info.Mat);
            success++;
        }

        AssetDatabase.SaveAssets();

        SetStatus(string.Format(UI_TEXT[32], success, failed), failed > 0);

        // 재스캔
        ScanMaterials();
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

    private string GetModeShortLabel(LilRenderingMode mode)
    {
        switch (mode)
        {
            case LilRenderingMode.Opaque:             return "Opaque";
            case LilRenderingMode.Cutout:             return "Cutout";
            case LilRenderingMode.Transparent:        return "Trans";
            case LilRenderingMode.OnePassTransparent: return "1P Trans";
            case LilRenderingMode.TwoPassTransparent: return "2P Trans";
            default: return "?";
        }
    }

    private string GetModeLabel(LilRenderingMode mode)
    {
        switch (_language)
        {
            case LanguagePreset.Korean:
                switch (mode)
                {
                    case LilRenderingMode.Opaque:             return "불투명 (Opaque)";
                    case LilRenderingMode.Cutout:             return "컷아웃 (Cutout)";
                    case LilRenderingMode.Transparent:        return "반투명 (Transparent)";
                    case LilRenderingMode.OnePassTransparent: return "1패스 반투명";
                    case LilRenderingMode.TwoPassTransparent: return "2패스 반투명";
                }
                break;
            case LanguagePreset.Japanese:
                switch (mode)
                {
                    case LilRenderingMode.Opaque:             return "不透明 (Opaque)";
                    case LilRenderingMode.Cutout:             return "カットアウト (Cutout)";
                    case LilRenderingMode.Transparent:        return "半透明 (Transparent)";
                    case LilRenderingMode.OnePassTransparent: return "1パス半透明";
                    case LilRenderingMode.TwoPassTransparent: return "2パス半透明";
                }
                break;
            default:
                return mode.ToString();
        }
        return mode.ToString();
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
        EditorPrefs.SetInt("DiNeLilPreset_Language",    (int)_language);
        EditorPrefs.SetInt("DiNeLilPreset_TargetMode",  (int)_targetMode);
        EditorPrefs.SetInt("DiNeLilPreset_OutlineMode", (int)_outlineMode);
        EditorPrefs.SetBool("DiNeLilPreset_Children",   _includeChildren);
        EditorPrefs.SetBool("DiNeLilPreset_Inactive",   _includeInactive);
        EditorPrefs.SetBool("DiNeLilPreset_Preview",    _previewOnly);
    }

    private void LoadSettings()
    {
        if (EditorPrefs.HasKey("DiNeLilPreset_Language"))
            _language        = (LanguagePreset)EditorPrefs.GetInt("DiNeLilPreset_Language");
        if (EditorPrefs.HasKey("DiNeLilPreset_TargetMode"))
            _targetMode      = (LilRenderingMode)EditorPrefs.GetInt("DiNeLilPreset_TargetMode");
        if (EditorPrefs.HasKey("DiNeLilPreset_OutlineMode"))
            _outlineMode     = (OutlineMode)EditorPrefs.GetInt("DiNeLilPreset_OutlineMode");
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
                    /* 00 */ "설정",
                    /* 01 */ "대상 오브젝트",
                    /* 02 */ "자식 오브젝트 포함",
                    /* 03 */ "비활성 오브젝트 포함",
                    /* 04 */ "적용할 렌더링 모드",
                    /* 05 */ "아웃라인 처리",
                    /* 06 */ "[ 미리보기 모드 ]  실제로 적용되지 않습니다",
                    /* 07 */ "[ 적용 모드 ]  셰이더가 실제로 교체됩니다",
                    /* 08 */ "스캔",
                    /* 09 */ "프리셋 적용",
                    /* 10 */ "Opaque",
                    /* 11 */ "Cutout",
                    /* 12 */ "Trans",
                    /* 13 */ "1P Trans",
                    /* 14 */ "2P Trans",
                    /* 15 */ "유지",
                    /* 16 */ "아웃라인 추가",
                    /* 17 */ "아웃라인 제거",
                    /* 18 */ "미리보기 확인",
                    /* 19 */ "LilToon 마테리얼: {0}개 / 선택됨: {1}개",
                    /* 20 */ "전체 선택",
                    /* 21 */ "전체 해제",
                    /* 22 */ "선택",
                    /* 23 */ "특수 셰이더 — 자동 변경 불가",
                    /* 24 */ "해당 셰이더 variant 없음",
                    /* 25 */ "대상 오브젝트를 먼저 지정해주세요.",
                    /* 26 */ "스캔 완료 — 총 {0}개 (특수 셰이더 {1}개는 변경 불가)",
                    /* 27 */ "선택된 마테리얼이 없습니다.",
                    /* 28 */ "[미리보기] {0}/{1}개 마테리얼에 프리셋 적용 예정",
                    /* 29 */ "{0}개 마테리얼의 렌더링 모드를 [{1}](으)로 변경합니다.\nUndo로 되돌릴 수 있습니다.",
                    /* 30 */ "적용",
                    /* 31 */ "취소",
                    /* 32 */ "완료 — {0}개 성공 / {1}개 실패",
                };
                break;

            case LanguagePreset.Japanese:
                UI_TEXT = new string[]
                {
                    /* 00 */ "設定",
                    /* 01 */ "対象オブジェクト",
                    /* 02 */ "子オブジェクトを含む",
                    /* 03 */ "非アクティブを含む",
                    /* 04 */ "適用するレンダリングモード",
                    /* 05 */ "アウトライン処理",
                    /* 06 */ "[ プレビューモード ]  実際には変更されません",
                    /* 07 */ "[ 適用モード ]  シェーダーが実際に変更されます",
                    /* 08 */ "スキャン",
                    /* 09 */ "プリセット適用",
                    /* 10 */ "Opaque",
                    /* 11 */ "Cutout",
                    /* 12 */ "Trans",
                    /* 13 */ "1P Trans",
                    /* 14 */ "2P Trans",
                    /* 15 */ "維持",
                    /* 16 */ "アウトライン追加",
                    /* 17 */ "アウトライン削除",
                    /* 18 */ "プレビュー確認",
                    /* 19 */ "LilToonマテリアル: {0}個 / 選択中: {1}個",
                    /* 20 */ "全て選択",
                    /* 21 */ "全て解除",
                    /* 22 */ "選択",
                    /* 23 */ "特殊シェーダー — 自動変更不可",
                    /* 24 */ "対応するシェーダーバリアントなし",
                    /* 25 */ "対象オブジェクトを先に指定してください。",
                    /* 26 */ "スキャン完了 — 計 {0}個 (特殊シェーダー {1}個は変更不可)",
                    /* 27 */ "選択されたマテリアルがありません。",
                    /* 28 */ "[プレビュー] {0}/{1}個のマテリアルにプリセットを適用予定",
                    /* 29 */ "{0}個のマテリアルのレンダリングモードを [{1}] に変更します。\nUndoで元に戻せます。",
                    /* 30 */ "適用",
                    /* 31 */ "キャンセル",
                    /* 32 */ "完了 — {0}個成功 / {1}個失敗",
                };
                break;

            default: // English
                UI_TEXT = new string[]
                {
                    /* 00 */ "Settings",
                    /* 01 */ "Target Object",
                    /* 02 */ "Include Children",
                    /* 03 */ "Include Inactive",
                    /* 04 */ "Target Rendering Mode",
                    /* 05 */ "Outline Handling",
                    /* 06 */ "[ Preview Mode ]  No changes will be saved",
                    /* 07 */ "[ Apply Mode ]  Shaders will actually be swapped",
                    /* 08 */ "Scan",
                    /* 09 */ "Apply Preset",
                    /* 10 */ "Opaque",
                    /* 11 */ "Cutout",
                    /* 12 */ "Trans",
                    /* 13 */ "1P Trans",
                    /* 14 */ "2P Trans",
                    /* 15 */ "Keep",
                    /* 16 */ "Add Outline",
                    /* 17 */ "Remove Outline",
                    /* 18 */ "Preview Check",
                    /* 19 */ "LilToon materials: {0} / Selected: {1}",
                    /* 20 */ "Select All",
                    /* 21 */ "Deselect All",
                    /* 22 */ "Ping",
                    /* 23 */ "Special shader — cannot auto-change",
                    /* 24 */ "No matching shader variant found",
                    /* 25 */ "Please assign a Target Object first.",
                    /* 26 */ "Scan complete — {0} total ({1} special shader(s) skipped)",
                    /* 27 */ "No materials selected.",
                    /* 28 */ "[Preview] {0}/{1} material(s) will have preset applied",
                    /* 29 */ "Change rendering mode of {0} material(s) to [{1}].\nSupports Undo.",
                    /* 30 */ "Apply",
                    /* 31 */ "Cancel",
                    /* 32 */ "Done — {0} succeeded / {1} failed",
                };
                break;
        }
    }
}

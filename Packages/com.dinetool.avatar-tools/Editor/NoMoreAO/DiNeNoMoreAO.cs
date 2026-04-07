using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public class DiNeNoMoreAO : EditorWindow
{
    // ──────────────────────────────────────────────
    //  언어
    // ──────────────────────────────────────────────
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset _language = LanguagePreset.English;
    private string[] UI_TEXT;

    // ──────────────────────────────────────────────
    //  설정
    // ──────────────────────────────────────────────
    private GameObject _targetObject;
    private bool       _includeChildren = true;
    private bool       _includeInactive = true;
    private bool       _previewOnly     = false;

    // ──────────────────────────────────────────────
    //  결과 상태
    // ──────────────────────────────────────────────
    private List<MaterialAOInfo> _foundMaterials = new List<MaterialAOInfo>();
    private bool                 _scanned        = false;
    private string               _statusMessage  = "";
    private bool                 _statusIsWarn   = false;
    private Vector2              _scrollPos;

    // ──────────────────────────────────────────────
    //  아이콘
    // ──────────────────────────────────────────────
    private Texture2D _windowIcon;
    private Font      _titleFont;

    // ──────────────────────────────────────────────
    //  LilToon 실제 AO 텍스처 프로퍼티명
    //  출처: github.com/lilxyzw/lilToon 소스코드
    //  _ShadowStrengthMask : 그림자 강도 마스크 (AO Map 역할)
    //  _ShadowBorderMask   : 그림자 경계 마스크
    //  _ShadowBlurMask     : 그림자 블러 마스크
    // ──────────────────────────────────────────────
    private static readonly string[] LILTOON_AO_PROPS = new string[]
    {
        "_ShadowStrengthMask",
        "_ShadowBorderMask",
        "_ShadowBlurMask",
    };

    // ──────────────────────────────────────────────
    //  내부 데이터
    //  ★ Selected 기본값은 반드시 false
    //    스캔 완료 후 AOProperties.Count > 0 인 경우에만 true 로 설정
    // ──────────────────────────────────────────────
    private class MaterialAOInfo
    {
        public Material      Material;
        public string        MaterialPath;
        public List<string>  AOProperties = new List<string>();
        public List<Texture> AOTextures   = new List<Texture>();
        public bool          IsLilToon    = false;
        public bool          Foldout      = true;
        public bool          Selected     = false;   // ★ false 고정 기본값
    }

    // ──────────────────────────────────────────────
    //  메뉴
    // ──────────────────────────────────────────────
    [MenuItem("DiNe/Shading/No More AO")]
    public static void ShowWindow()
    {
        var window = GetWindow<DiNeNoMoreAO>("DiNe No More AO");
        window.minSize = new Vector2(175, 150);
        window.position = new Rect(window.position.x, window.position.y, 420, 620);
    }

    // ──────────────────────────────────────────────
    //  라이프사이클
    // ──────────────────────────────────────────────
    void OnEnable()
    {
        LoadSettings();
        SetLanguage(_language);
        _windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        _titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
    }

    void OnDisable() => SaveSettings();

    // ──────────────────────────────────────────────
    //  OnGUI
    // ──────────────────────────────────────────────
    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ── 타이틀 박스 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 28,
            normal    = new GUIStyleState() { textColor = Color.white }
        };
        GUILayout.Label(new GUIContent("No More AO", _windowIcon), titleStyle);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 언어 탭 ──
        int langIdx = (int)_language;
        int newLang = GUILayout.Toolbar(langIdx,
            new string[] { "English", "한국어", "日本語" }, GUILayout.Height(28));
        if (newLang != langIdx)
        {
            _language = (LanguagePreset)newLang;
            SetLanguage(_language);
            SaveSettings();
        }

        GUILayout.Space(10);

        // ── 설정 섹션 ──
        EditorGUILayout.BeginVertical("box");
        DrawSettings();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // ── 액션 버튼 섹션 ──
        EditorGUILayout.BeginVertical("box");
        DrawActionButtons();
        GUILayout.Space(3);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 상태 메시지 ──
        if (!string.IsNullOrEmpty(_statusMessage))
            EditorGUILayout.HelpBox(_statusMessage,
                _statusIsWarn ? MessageType.Warning : MessageType.Info);

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
        EditorGUILayout.LabelField(UI_TEXT[0],
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.Space(4);

        _targetObject = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent(UI_TEXT[1], UI_TEXT[10]),
            _targetObject, typeof(GameObject), true);

        GUILayout.Space(4);
        GuiLine(1, 4);

        _includeChildren = EditorGUILayout.Toggle(
            new GUIContent(UI_TEXT[2], UI_TEXT[11]), _includeChildren);
        _includeInactive = EditorGUILayout.Toggle(
            new GUIContent(UI_TEXT[3], UI_TEXT[12]), _includeInactive);

        GUILayout.Space(4);
        GuiLine(1, 4);

        EditorGUILayout.BeginHorizontal();
        _previewOnly = EditorGUILayout.Toggle(_previewOnly, GUILayout.Width(14));
        GUILayout.Label(_previewOnly ? UI_TEXT[4] : UI_TEXT[5],
            new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                normal    = new GUIStyleState
                {
                    textColor = _previewOnly
                        ? new Color(1f, 0.75f, 0.2f)
                        : new Color(0.45f, 0.85f, 0.45f)
                }
            });
        EditorGUILayout.EndHorizontal();
    }

    // ──────────────────────────────────────────────
    //  액션 버튼
    // ──────────────────────────────────────────────
    private void DrawActionButtons()
    {
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    = new GUIStyleState() { textColor = Color.white },
            hover     = new GUIStyleState() { textColor = Color.white },
        };

        GUI.enabled = _targetObject != null;
        GUI.backgroundColor = new Color(0.45f, 0.55f, 0.75f, 1f);
        if (GUILayout.Button(UI_TEXT[6], btnStyle, GUILayout.Height(38)))
            ScanMaterials();
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.enabled = true;

        GUILayout.Space(5);

        bool canRemove = _scanned &&
            _foundMaterials.Any(m => m.Selected && m.AOProperties.Count > 0);
        GUI.enabled = canRemove;
        GUI.backgroundColor = _previewOnly
            ? new Color(0.75f, 0.6f, 0.2f, 1f)
            : new Color(0.65f, 0.25f, 0.25f, 1f);

        if (GUILayout.Button(_previewOnly ? UI_TEXT[7] : UI_TEXT[8],
                btnStyle, GUILayout.Height(38)))
            RemoveAOTextures();

        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        GUI.enabled = true;
    }

    // ──────────────────────────────────────────────
    //  결과 목록 헤더
    // ──────────────────────────────────────────────
    private void DrawResults()
    {
        int lilCount = _foundMaterials.Count(m => m.IsLilToon);
        int aoCount  = _foundMaterials.Count(m => m.IsLilToon && m.AOProperties.Count > 0);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            string.Format(UI_TEXT[9], lilCount, aoCount),
            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });

        GUILayout.Space(5);

        GUIStyle selBtn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            normal    = new GUIStyleState() { textColor = Color.white },
            hover     = new GUIStyleState() { textColor = Color.white },
        };
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.55f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[13], selBtn, GUILayout.Height(28)))
            _foundMaterials.ForEach(m =>
                m.Selected = m.IsLilToon && m.AOProperties.Count > 0);
        GUI.backgroundColor = new Color(0.55f, 0.35f, 0.35f, 1f);
        if (GUILayout.Button(UI_TEXT[14], selBtn, GUILayout.Height(28)))
            _foundMaterials.ForEach(m => m.Selected = false);
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        if (lilCount == 0)
        {
            EditorGUILayout.HelpBox(UI_TEXT[15], MessageType.Info);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        foreach (var info in _foundMaterials)
        {
            if (!info.IsLilToon) continue;
            DrawMaterialCard(info);
        }
        EditorGUILayout.EndScrollView();
    }

    // ──────────────────────────────────────────────
    //  마테리얼 카드
    // ──────────────────────────────────────────────
    private void DrawMaterialCard(MaterialAOInfo info)
    {
        bool hasAO = info.AOProperties.Count > 0;

        // ★ AO 없는 카드는 Selected 를 OnGUI 매 프레임 강제 false
        if (!hasAO) info.Selected = false;

        EditorGUILayout.BeginVertical("box");

        // 헤더 행
        EditorGUILayout.BeginHorizontal();

        // 체크박스 — AO 없으면 비활성
        GUI.enabled   = hasAO;
        info.Selected = GUILayout.Toggle(info.Selected, "",
            GUILayout.Width(20), GUILayout.Height(20));
        GUI.enabled = true;

        GUILayout.Space(2);

        // 상태 배지
        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 10,
            alignment = TextAnchor.MiddleCenter,
            normal    = new GUIStyleState
            {
                textColor = hasAO
                    ? new Color(1f, 0.55f, 0.2f)
                    : new Color(0.4f, 0.85f, 0.4f)
            }
        };
        GUILayout.Label(hasAO ? $"AO ×{info.AOProperties.Count}" : "Clean",
            badgeStyle, GUILayout.Width(54), GUILayout.Height(20));

        // 폴드아웃
        info.Foldout = EditorGUILayout.Foldout(info.Foldout,
            info.Material.name, true,
            new GUIStyle(EditorStyles.foldout)
                { fontStyle = FontStyle.Bold, fontSize = 12 });

        GUILayout.FlexibleSpace();

        // Ping 버튼
        if (GUILayout.Button(UI_TEXT[16],
                new GUIStyle(EditorStyles.miniButton)
                    { fontSize = 11, fontStyle = FontStyle.Bold },
                GUILayout.Width(46), GUILayout.Height(22)))
            EditorGUIUtility.PingObject(info.Material);

        EditorGUILayout.EndHorizontal();

        // 카드 내용
        if (info.Foldout)
        {
            GUILayout.Space(3);
            GuiLine(1, 2);
            GUILayout.Space(4);

            EditorGUI.indentLevel++;

            // 경로
            EditorGUILayout.LabelField(TruncatePath(info.MaterialPath, 54),
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = new GUIStyleState()
                        { textColor = new Color(0.6f, 0.6f, 0.6f) }
                });

            if (hasAO)
            {
                GUILayout.Space(6);
                EditorGUILayout.LabelField(UI_TEXT[17],
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 11,
                        normal   = new GUIStyleState()
                            { textColor = new Color(0.95f, 0.6f, 0.3f) }
                    });
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
                        // ── 텍스처 프리뷰 72×72 ──
                        Rect previewRect = GUILayoutUtility.GetRect(72, 72,
                            GUILayout.Width(72), GUILayout.Height(72));

                        Texture2D thumb = AssetPreview.GetAssetPreview(info.AOTextures[i]);
                        if (thumb != null)
                        {
                            GUI.DrawTexture(previewRect, thumb, ScaleMode.ScaleToFit);
                        }
                        else if (info.AOTextures[i] is Texture2D raw)
                        {
                            EditorGUI.DrawPreviewTexture(previewRect, raw);
                            Repaint();
                        }
                        else
                        {
                            GUI.Box(previewRect, "?");
                        }

                        GUILayout.Space(8);

                        // 텍스처 정보
                        EditorGUILayout.BeginVertical();
                        GUILayout.FlexibleSpace();

                        EditorGUILayout.LabelField(info.AOTextures[i].name,
                            new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });

                        if (info.AOTextures[i] is Texture2D t2d)
                            EditorGUILayout.LabelField(
                                $"{t2d.width} × {t2d.height}  |  {t2d.format}",
                                new GUIStyle(EditorStyles.miniLabel)
                                {
                                    normal = new GUIStyleState()
                                        { textColor = new Color(0.6f, 0.6f, 0.6f) }
                                });

                        GUILayout.Space(4);
                        EditorGUILayout.ObjectField(
                            info.AOTextures[i], typeof(Texture), false,
                            GUILayout.Height(18));

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("(null)",
                            new GUIStyle(EditorStyles.miniLabel)
                            {
                                normal = new GUIStyleState()
                                    { textColor = new Color(0.55f, 0.55f, 0.55f) }
                            });
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

    // ──────────────────────────────────────────────
    //  스캔
    // ──────────────────────────────────────────────
    private void ScanMaterials()
    {
        _foundMaterials.Clear();
        _scanned = false;

        if (_targetObject == null)
        {
            SetStatus(UI_TEXT[18], true);
            return;
        }

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

                var info = new MaterialAOInfo
                {
                    Material     = mat,
                    MaterialPath = AssetDatabase.GetAssetPath(mat),
                    IsLilToon    = IsLilToon(mat),
                    Selected     = false,   // ★ 항상 false 로 시작
                };

                if (info.IsLilToon)
                {
                    CollectAOProperties(mat, info);
                    // ★ AO 텍스처가 하나라도 있을 때만 Selected = true
                    info.Selected = info.AOProperties.Count > 0;
                }

                _foundMaterials.Add(info);
            }
        }

        _foundMaterials = _foundMaterials
            .OrderByDescending(m => m.IsLilToon)
            .ThenByDescending(m => m.AOProperties.Count)
            .ToList();

        _scanned = true;

        int lilCount = _foundMaterials.Count(m => m.IsLilToon);
        int aoCount  = _foundMaterials.Count(m => m.IsLilToon && m.AOProperties.Count > 0);
        SetStatus(string.Format(UI_TEXT[19], lilCount, aoCount), aoCount > 0);
        Repaint();
    }

    private bool IsLilToon(Material mat)
    {
        if (mat == null || mat.shader == null) return false;
        string sn = mat.shader.name.ToLower();
        return sn.Contains("liltoon") || sn.Contains("lil_toon");
    }

    private void CollectAOProperties(Material mat, MaterialAOInfo info)
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

    // ──────────────────────────────────────────────
    //  제거
    // ──────────────────────────────────────────────
    private void RemoveAOTextures()
    {
        var targets = _foundMaterials
            .Where(m => m.IsLilToon && m.Selected && m.AOProperties.Count > 0)
            .ToList();

        if (targets.Count == 0)
        {
            SetStatus(UI_TEXT[20], false);
            return;
        }

        if (_previewOnly)
        {
            int total = targets.Sum(m => m.AOProperties.Count);
            SetStatus(string.Format(UI_TEXT[21], targets.Count, total), true);
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            UI_TEXT[22],
            string.Format(UI_TEXT[23], targets.Count) + "\n" + UI_TEXT[24],
            UI_TEXT[25], UI_TEXT[26]);
        if (!ok) return;

        Undo.RecordObjects(
            targets.Select(m => (Object)m.Material).ToArray(),
            "DiNe No More AO");

        int removed = 0;
        foreach (var info in targets)
        {
            foreach (string prop in info.AOProperties)
            {
                info.Material.SetTexture(prop, null);
                removed++;
            }
            EditorUtility.SetDirty(info.Material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SetStatus(string.Format(UI_TEXT[27], targets.Count, removed), false);
        ScanMaterials();
    }

    // ──────────────────────────────────────────────
    //  유틸
    // ──────────────────────────────────────────────
    private void SetStatus(string msg, bool warn)
    {
        _statusMessage = msg;
        _statusIsWarn  = warn;
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
        EditorPrefs.SetInt ("DiNeNoMoreAO_Language",        (int)_language);
        EditorPrefs.SetBool("DiNeNoMoreAO_IncludeChildren", _includeChildren);
        EditorPrefs.SetBool("DiNeNoMoreAO_IncludeInactive", _includeInactive);
        EditorPrefs.SetBool("DiNeNoMoreAO_PreviewOnly",     _previewOnly);
    }

    private void LoadSettings()
    {
        if (EditorPrefs.HasKey("DiNeNoMoreAO_Language"))
            _language = (LanguagePreset)EditorPrefs.GetInt("DiNeNoMoreAO_Language");
        if (EditorPrefs.HasKey("DiNeNoMoreAO_IncludeChildren"))
            _includeChildren = EditorPrefs.GetBool("DiNeNoMoreAO_IncludeChildren");
        if (EditorPrefs.HasKey("DiNeNoMoreAO_IncludeInactive"))
            _includeInactive = EditorPrefs.GetBool("DiNeNoMoreAO_IncludeInactive");
        if (EditorPrefs.HasKey("DiNeNoMoreAO_PreviewOnly"))
            _previewOnly = EditorPrefs.GetBool("DiNeNoMoreAO_PreviewOnly");
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
                    /* 04 */ "[ 미리보기 모드 ]  실제로 적용되지 않습니다",
                    /* 05 */ "[ 적용 모드 ]  변경사항이 실제로 저장됩니다",
                    /* 06 */ "스캔",
                    /* 07 */ "미리보기 결과 확인",
                    /* 08 */ "AO 텍스처 제거",
                    /* 09 */ "LilToon 마테리얼: {0}개 / AO 존재: {1}개",
                    /* 10 */ "AO 텍스처를 제거할 대상 GameObject",
                    /* 11 */ "하위 계층 오브젝트까지 검색합니다",
                    /* 12 */ "비활성화된 오브젝트도 검색합니다",
                    /* 13 */ "전체 선택",
                    /* 14 */ "전체 해제",
                    /* 15 */ "LilToon 마테리얼을 찾을 수 없습니다.",
                    /* 16 */ "선택",
                    /* 17 */ "AO 텍스처",
                    /* 18 */ "대상 오브젝트를 먼저 지정해주세요.",
                    /* 19 */ "스캔 완료 — LilToon {0}개, AO 보유 {1}개",
                    /* 20 */ "제거할 AO 텍스처가 없습니다.",
                    /* 21 */ "[미리보기] {0}개 마테리얼에서 총 {1}개 AO 텍스처가 제거될 예정입니다.",
                    /* 22 */ "AO 텍스처 제거",
                    /* 23 */ "{0}개 마테리얼에서 AO 텍스처를 제거합니다.\nUndo로 되돌릴 수 있습니다.",
                    /* 24 */ "계속하시겠습니까?",
                    /* 25 */ "제거",
                    /* 26 */ "취소",
                    /* 27 */ "완료 — {0}개 마테리얼에서 {1}개 AO 텍스처를 제거했습니다.",
                };
                break;

            case LanguagePreset.Japanese:
                UI_TEXT = new string[]
                {
                    /* 00 */ "設定",
                    /* 01 */ "対象オブジェクト",
                    /* 02 */ "子オブジェクトを含む",
                    /* 03 */ "非アクティブを含む",
                    /* 04 */ "[ プレビューモード ]  実際には変更されません",
                    /* 05 */ "[ 適用モード ]  変更が保存されます",
                    /* 06 */ "スキャン",
                    /* 07 */ "プレビュー結果を確認",
                    /* 08 */ "AOテクスチャを削除",
                    /* 09 */ "LilToon: {0}個 / AO あり: {1}個",
                    /* 10 */ "AOテクスチャを削除するGameObject",
                    /* 11 */ "子オブジェクトも検索します",
                    /* 12 */ "非アクティブも検索します",
                    /* 13 */ "全て選択",
                    /* 14 */ "全て解除",
                    /* 15 */ "LilToonマテリアルが見つかりません。",
                    /* 16 */ "選択",
                    /* 17 */ "AOテクスチャ",
                    /* 18 */ "対象オブジェクトを先に指定してください。",
                    /* 19 */ "スキャン完了 — LilToon {0}個, AO あり {1}個",
                    /* 20 */ "削除するAOテクスチャがありません。",
                    /* 21 */ "[プレビュー] {0}個のマテリアルから計 {1}個のAOテクスチャが削除される予定です。",
                    /* 22 */ "AOテクスチャの削除",
                    /* 23 */ "{0}個のマテリアルからAOテクスチャを削除します。\nUndoで元に戻せます。",
                    /* 24 */ "続けますか？",
                    /* 25 */ "削除",
                    /* 26 */ "キャンセル",
                    /* 27 */ "完了 — {0}個のマテリアルから {1}個のAOテクスチャを削除しました。",
                };
                break;

            default: // English
                UI_TEXT = new string[]
                {
                    /* 00 */ "Settings",
                    /* 01 */ "Target Object",
                    /* 02 */ "Include Children",
                    /* 03 */ "Include Inactive",
                    /* 04 */ "[ Preview Mode ]  No changes will be saved",
                    /* 05 */ "[ Apply Mode ]  Changes will be written to disk",
                    /* 06 */ "Scan",
                    /* 07 */ "Preview Result",
                    /* 08 */ "Remove AO Textures",
                    /* 09 */ "LilToon: {0} found / With AO: {1}",
                    /* 10 */ "Target GameObject to remove AO textures from",
                    /* 11 */ "Search child objects recursively",
                    /* 12 */ "Search inactive objects too",
                    /* 13 */ "Select All",
                    /* 14 */ "Deselect All",
                    /* 15 */ "No LilToon materials found.",
                    /* 16 */ "Ping",
                    /* 17 */ "AO Textures",
                    /* 18 */ "Please assign a Target Object first.",
                    /* 19 */ "Scan complete — {0} LilToon material(s), {1} with AO texture(s)",
                    /* 20 */ "No AO textures to remove.",
                    /* 21 */ "[Preview] {1} AO texture(s) in {0} material(s) would be removed.",
                    /* 22 */ "Remove AO Textures",
                    /* 23 */ "This will remove AO textures from {0} material(s).\nThis action supports Undo.",
                    /* 24 */ "Do you want to continue?",
                    /* 25 */ "Remove",
                    /* 26 */ "Cancel",
                    /* 27 */ "Done — Removed {1} AO texture(s) from {0} material(s).",
                };
                break;
        }
    }
}

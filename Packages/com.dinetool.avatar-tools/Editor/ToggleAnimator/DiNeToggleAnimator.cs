using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DiNeToggleAnimator : EditorWindow
{
    // ─── DiNe Brand Colors ──────────────────────────────────────
    private static readonly Color ColMint     = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColMintDark = new Color(0.18f, 0.55f, 0.51f);
    private static readonly Color ColDark     = new Color(0.21f, 0.21f, 0.24f);
    private static readonly Color ColDeep     = new Color(0.14f, 0.14f, 0.16f);
    private static readonly Color ColPanel    = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color ColSidebar  = new Color(0.17f, 0.17f, 0.20f);
    private static readonly Color ColOn       = new Color(0.16f, 0.62f, 0.33f);
    private static readonly Color ColOff      = new Color(0.65f, 0.18f, 0.18f);
    private static readonly Color ColUnset    = new Color(0.27f, 0.27f, 0.30f);
    private static readonly Color ColHeaderBg = new Color(0.16f, 0.16f, 0.19f);

    // ─── Language ───────────────────────────────────────────────
    private enum Lang { English, Korean, Japanese }
    private Lang _lang = Lang.Korean;

    // ─── Layout ─────────────────────────────────────────────────
    private const float SIDEBAR_W  = 42f;
    private const float LANG_W     = 28f;   
    private const float LABEL_W    = 190f;
    
    private const float COL_W      = 130f;  
    private const float ROW_H      = 20f;   
    private const float HEADER_H   = 24f;
    
    private const float DEL_W      = 22f;

    // ─── Data Structures ────────────────────────────────────────
    [System.Serializable]
    public struct RowData : System.IEquatable<RowData>
    {
        public string path;
        public string propName;
        public bool IsBlendShape => propName != null && propName.StartsWith("blendShape.");
        
        public string ShortName
        {
            get
            {
                if (string.IsNullOrEmpty(path)) return IsBlendShape ? propName.Substring(11) : "Root";
                int s = path.LastIndexOf('/');
                string p = s >= 0 ? path.Substring(s + 1) : path;
                if (IsBlendShape) return p + " (" + propName.Substring(11) + ")";
                return p;
            }
        }

        public bool Equals(RowData other) => path == other.path && propName == other.propName;
        public override bool Equals(object obj) => obj is RowData other && Equals(other);
        public override int GetHashCode() => (path != null ? path.GetHashCode() : 0) ^ (propName != null ? propName.GetHashCode() : 0);
    }

    // ─── Data ───────────────────────────────────────────────────
    private GameObject          _avatarRoot;
    private List<AnimationClip> _clips = new List<AnimationClip>();
    private List<RowData>       _rows  = new List<RowData>();  
    private List<List<float?>>  _grid  = new List<List<float?>>(); 
    
    // ─── UI State ───────────────────────────────────────────────
    private int     _previewClipIdx = -1;
    private Vector2 _gridScroll;
    private string  _statusMsg  = "";
    private double  _statusTime = -1;
    private bool    _lockSelection = false;

    // ─── Assets ─────────────────────────────────────────────────
    private Font      _titleFont;
    private Texture2D _iconTex;        
    private Texture2D _sidebarLogoTex; 

    // ─── Menu ───────────────────────────────────────────────────
    [MenuItem("DiNe/Toggle Animator", false, 5)]
    public static void ShowWindow()
    {
        var win = GetWindow<DiNeToggleAnimator>();
        win.minSize = new Vector2(500, 440);
    }

    void OnEnable()
    {
        _titleFont = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        _iconTex        = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        _sidebarLogoTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        titleContent = new GUIContent("Toggle Animator", _iconTex);

        Selection.selectionChanged += OnSelectionChange;
        OnSelectionChange(); 
    }

    void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChange;
    }

    private string T(string en, string kr, string jp) =>
        _lang == Lang.Korean ? kr : _lang == Lang.Japanese ? jp : en;

    // ────────────────────────────────────────────────────────────
    //  Selection Sync Logic 
    // ────────────────────────────────────────────────────────────
    private void OnSelectionChange()
    {
        if (_lockSelection) return;

        var newClips = Selection.objects.OfType<AnimationClip>().ToList();
        
        if (!_clips.SequenceEqual(newClips))
        {
            _previewClipIdx = -1;
            _clips = newClips;
            RebuildGrid();
            Repaint();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Data Validation
    // ────────────────────────────────────────────────────────────
    private void ValidateData()
    {
        if (_clips == null) _clips = new List<AnimationClip>();
        if (_rows == null) _rows = new List<RowData>();
        if (_grid == null) _grid = new List<List<float?>>();

        bool needsRebuild = false;

        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrEmpty(_rows[i].propName)) 
            {
                _rows.RemoveAt(i);
                if (i < _grid.Count) _grid.RemoveAt(i);
                needsRebuild = true;
            }
        }

        while (_grid.Count < _rows.Count)
        {
            _grid.Add(new List<float?>(new float?[_clips.Count]));
            needsRebuild = true;
        }
        while (_grid.Count > _rows.Count)
        {
            _grid.RemoveAt(_grid.Count - 1);
        }

        for (int r = 0; r < _grid.Count; r++)
        {
            if (_grid[r] == null)
            {
                _grid[r] = new List<float?>(new float?[_clips.Count]);
                needsRebuild = true;
            }
            while (_grid[r].Count < _clips.Count) { _grid[r].Add(null); needsRebuild = true; }
            while (_grid[r].Count > _clips.Count) { _grid[r].RemoveAt(_grid[r].Count - 1); needsRebuild = true; }
        }

        if (needsRebuild)
        {
            RebuildGrid();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  OnGUI
    // ────────────────────────────────────────────────────────────
    void OnGUI()
    {
        ValidateData();

        var prevBg    = GUI.backgroundColor;
        var prevColor = GUI.color;

        float winH = position.height;
        Rect sidebarRect = new Rect(0, 0, SIDEBAR_W, winH);
        EditorGUI.DrawRect(sidebarRect, ColSidebar);
        EditorGUI.DrawRect(new Rect(SIDEBAR_W - 1, 0, 1, winH), new Color(0.30f, 0.82f, 0.76f, 0.35f));

        if (_sidebarLogoTex != null)
        {
            float iSz = SIDEBAR_W - 8f;
            GUI.DrawTexture(new Rect(4, 5, iSz, iSz), _sidebarLogoTex, ScaleMode.ScaleToFit, true);
        }

        DrawSidebarTitle("Toggle\nAnimator", winH); 

        float langX = SIDEBAR_W;
        EditorGUI.DrawRect(new Rect(langX, 0, LANG_W, winH), new Color(0.19f, 0.19f, 0.22f));
        EditorGUI.DrawRect(new Rect(langX + LANG_W - 1, 0, 1, winH), new Color(0.30f, 0.82f, 0.76f, 0.18f));
        DrawLangButtons(langX, winH, prevBg);

        Rect mainRect = new Rect(SIDEBAR_W + LANG_W, 0, position.width - SIDEBAR_W - LANG_W, winH);
        GUILayout.BeginArea(mainRect);
        GUILayout.BeginVertical();
        GUILayout.Space(6);

        DrawSetupSection(prevBg);
        GUILayout.Space(5);

        if (_clips.Count > 0)
        {
            DrawGrid(prevBg);
            GUILayout.Space(4);
            DrawBottomBar(prevBg);
        }
        else
        {
            DrawEmptyHint(T(
                "Select Animation Clips in the Project window to begin.\n(You can select multiple clips at once)\n\nAfter that, drag GameObjects here to add rows.",
                "프로젝트(Project) 창에서 Animation Clip을 선택하세요.\n(여러 개 동시 선택 가능)\n\n그 뒤, GameObject를 아래 공간에 드래그하여 행 추가.",
                "プロジェクトウィンドウでアニメーションクリップを選択してください。\n(複数選択可能)\n\nその後、GameObjectを下のスペースにドラッグして行を追加します。"
            ));
        }

        GUILayout.Space(2);
        DrawStatusBar();

        HandleGameObjectDrop(); 

        GUILayout.EndVertical();
        GUILayout.EndArea();

        GUI.backgroundColor = prevBg;
        GUI.color           = prevColor;
    }

    private void DrawSidebarTitle(string text, float winH)
    {
        var style = new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            fontSize  = 19,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.50f, 0.50f, 0.54f) },
        };

        float totalHeight = 0;
        float lineSpacing = 18f;
        float paragraphSpacing = 8f;

        foreach (char ch in text)
        {
            if (ch == '\n') totalHeight += paragraphSpacing;
            else totalHeight += lineSpacing;
        }

        float startY = 46f + (winH - 46f - totalHeight) * 0.5f;
        float y = startY;

        foreach (char ch in text)
        {
            if (ch == '\n') { y += paragraphSpacing; continue; }
            GUI.Label(new Rect(0, y, SIDEBAR_W, lineSpacing), ch.ToString(), style);
            y += lineSpacing; 
        }
    }

    private void DrawLangButtons(float x, float winH, Color prevBg)
    {
        Lang[] langs = { Lang.English, Lang.Korean, Lang.Japanese };
        
        string[] labelsEn = { "E\nN\nG", "K\nO\nR", "J\nP\nN" };
        string[] labelsKr = { "영\n어", "한\n국\n어", "일\n본\n어" };
        string[] labelsJp = { "英\n語", "韓\n国\n語", "日\n本\n語" };

        string[] currentLabels = _lang == Lang.Korean ? labelsKr : (_lang == Lang.Japanese ? labelsJp : labelsEn);

        float padding = 4f;
        float btnH = (winH - (padding * 4f)) / 3f;
        float startY = padding; 

        for (int i = 0; i < 3; i++)
        {
            bool active = _lang == langs[i];
            GUI.backgroundColor = active ? ColMint : ColDark;
            
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = active ? ColDeep : new Color(0.55f, 0.55f, 0.58f) },
                hover     = { textColor = active ? ColDeep : Color.white },
                wordWrap  = false 
            };
            
            Rect btnRect = new Rect(x + 2, startY + i * (btnH + padding), LANG_W - 4, btnH);
            
            if (GUI.Button(btnRect, currentLabels[i], style))
            {
                _lang = langs[i];
            }
            
            GUI.backgroundColor = prevBg;
        }
    }

    private void DrawSetupSection(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(T("Avatar Root", "아바타 루트", "アバタールート"), EditorStyles.boldLabel, GUILayout.Width(96));
        EditorGUI.BeginChangeCheck();
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && _previewClipIdx >= 0) PreviewClip(_previewClipIdx);
        
        GUILayout.FlexibleSpace();
        
        GUI.backgroundColor = _lockSelection ? ColMint : ColDark;
        if (GUILayout.Button(T("Lock Selection", "선택 유지 (Lock)", "選択維持 (Lock)"), 
            new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = _lockSelection ? ColDeep : Color.white } }, 
            GUILayout.Width(130), GUILayout.Height(20)))
        {
            _lockSelection = !_lockSelection;
            if (!_lockSelection) OnSelectionChange(); 
        }
        GUI.backgroundColor = prevBg;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ────────────────────────────────────────────────────────────
    //  Grid
    // ────────────────────────────────────────────────────────────
    private void DrawGrid(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll);

        EditorGUILayout.BeginHorizontal(GUILayout.Height(HEADER_H));
        GUILayout.Label("", GUILayout.Width(LABEL_W), GUILayout.Height(HEADER_H));

        for (int c = 0; c < _clips.Count; c++)
        {
            string cname  = _clips[c] != null ? _clips[c].name : $"Clip {c + 1}";
            bool   isPrev = _previewClipIdx == c;

            GUI.backgroundColor = isPrev ? ColMint : ColHeaderBg;
            if (GUILayout.Button(cname,
                new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 10, fontStyle = FontStyle.Bold, wordWrap = false,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = isPrev ? ColDeep : new Color(0.80f, 0.82f, 0.80f) },
                    hover     = { textColor = isPrev ? ColDeep : Color.white },
                },
                GUILayout.Width(COL_W), GUILayout.Height(HEADER_H)))
            {
                if (isPrev) _previewClipIdx = -1;
                else        { _previewClipIdx = c; PreviewClip(c); }
            }
            GUI.backgroundColor = prevBg;
        }
        GUILayout.Space(DEL_W);
        EditorGUILayout.EndHorizontal();

        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 2f, GUILayout.ExpandWidth(true)),
            new Color(0.30f, 0.82f, 0.76f, 0.35f));
        GUILayout.Space(1);

        if (_rows.Count == 0)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                    T("← Drag a GameObject into the window to add rows.",
                      "← GameObject를 창에 끌어다 놓으면 행이 추가됩니다.",
                      "← GameObjectをここにドラッグして行を追加します。"),
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 });
            GUILayout.Space(8);
        }

        var cellBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11, fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white },
        };

        var smallXStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 10, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            padding   = new RectOffset(0, 0, 0, 0), 
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white },
        };

        for (int r = 0; r < _rows.Count; r++)
        {
            Color rowBg = r % 2 == 0 ? new Color(0.19f, 0.19f, 0.21f) : new Color(0.21f, 0.21f, 0.24f);
            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
            EditorGUI.DrawRect(rowRect, rowBg);

            // Row Label
            GUILayout.Label(
                new GUIContent(_rows[r].ShortName, _rows[r].path + "\n" + _rows[r].propName),
                new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 11, alignment = TextAnchor.MiddleLeft,
                    normal    = { textColor = new Color(0.88f, 0.88f, 0.90f) },
                },
                GUILayout.Width(LABEL_W), GUILayout.Height(ROW_H));

            bool isBlendShape = _rows[r].IsBlendShape;

            // Cells
            for (int c = 0; c < _clips.Count; c++)
            {
                float? state = _grid[r][c];

                if (state == null)
                {
                    GUI.backgroundColor = ColUnset;
                    if (GUILayout.Button("—", cellBtnStyle, GUILayout.Width(COL_W), GUILayout.Height(ROW_H)))
                    {
                        _grid[r][c] = isBlendShape ? 100f : 1f; 
                        SaveClip(c);
                        UpdatePreview(r, c);
                    }
                    GUI.backgroundColor = prevBg;
                }
                else
                {
                    if (!isBlendShape)
                    {
                        // 토글 (ON/OFF)
                        bool isOn = state.Value > 0.5f;
                        GUI.backgroundColor = isOn ? ColOn : ColOff;
                        
                        Rect btnRect = GUILayoutUtility.GetRect(new GUIContent(isOn ? "ON" : "OFF"), cellBtnStyle, GUILayout.Width(COL_W), GUILayout.Height(ROW_H));
                        
                        if (Event.current.type == EventType.ContextClick && btnRect.Contains(Event.current.mousePosition))
                        {
                            _grid[r][c] = null;
                            SaveClip(c);
                            UpdatePreview(r, c);
                            Event.current.Use();
                        }
                        else if (GUI.Button(btnRect, isOn ? "ON" : "OFF", cellBtnStyle))
                        {
                            _grid[r][c] = isOn ? 0f : 1f;
                            SaveClip(c);
                            UpdatePreview(r, c);
                        }
                        GUI.backgroundColor = prevBg;
                    }
                    else
                    {
                        // 쉐이프키
                        GUILayout.BeginHorizontal(GUILayout.Width(COL_W));
                        
                        EditorGUI.BeginChangeCheck();
                        float val = EditorGUILayout.FloatField(state.Value, GUILayout.Width(COL_W - 20f), GUILayout.Height(ROW_H));
                        if (EditorGUI.EndChangeCheck())
                        {
                            _grid[r][c] = val;
                            SaveClip(c);
                            UpdatePreview(r, c);
                        }

                        GUI.backgroundColor = new Color(0.42f, 0.12f, 0.12f);
                        if (GUILayout.Button("✕", smallXStyle, GUILayout.Width(16f), GUILayout.Height(ROW_H)))
                        {
                            _grid[r][c] = null;
                            SaveClip(c);
                            UpdatePreview(r, c);
                        }
                        GUI.backgroundColor = prevBg;
                        
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUI.backgroundColor = new Color(0.42f, 0.12f, 0.12f);
            if (GUILayout.Button("✕", smallXStyle, GUILayout.Width(DEL_W), GUILayout.Height(ROW_H)))
            {
                RemoveRow(r);
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
                break;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        EditorGUILayout.EndScrollView(); 

        Rect goDropRect = GUILayoutUtility.GetRect(0, 34f, GUILayout.ExpandWidth(true));
        bool isGoOver = _goDragOver;
        EditorGUI.DrawRect(goDropRect, isGoOver ? new Color(0.20f, 0.55f, 0.50f, 0.25f) : new Color(0.14f, 0.14f, 0.17f));
        DrawBorder(goDropRect, isGoOver ? ColMint : new Color(0.30f, 0.30f, 0.34f), 1);
        GUI.Label(goDropRect,
            T("⊕  Drag GameObject here to add row",
              "⊕  GameObject 를 여기에 드래그해서 행 추가",
              "⊕  GameObjectをドラッグして行追加"),
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11,
                normal    = { textColor = isGoOver ? ColMint : new Color(0.43f, 0.43f, 0.47f) },
            });
        _goDropGuiRect = goDropRect;

        EditorGUILayout.EndVertical();
    }

    private Rect _goDropGuiRect;
    private bool _goDragOver;

    // ────────────────────────────────────────────────────────────
    //  Bottom Bar
    // ────────────────────────────────────────────────────────────
    private void DrawBottomBar(Color prevBg)
    {
        GUI.backgroundColor = ColDark;
        var bs = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11, fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(0.78f, 0.78f, 0.80f) },
            hover     = { textColor = Color.white },
        };
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(T("Fill Missing → OFF", "미지정 → OFF (0)", "未設定→OFF"), bs, GUILayout.Height(26)))
        { FillMissing(0f); SetStatus(T("Filled all unset cells with OFF.", "미지정(—) 셀 전부 0(OFF) 으로 채웠습니다.", "未設定セルをすべて0にしました。")); }
        if (GUILayout.Button(T("Smart Fill", "Smart Fill", "スマートフィル"), bs, GUILayout.Height(26)))
        { SmartFill(); SetStatus(T("Smart Fill: set OFF where ON exists in other clips.", "Smart Fill: 값이 있는 행의 나머지를 0 처리.", "スマートフィル完了。")); }
        if (GUILayout.Button(T("Invert All", "전체 반전", "全て反転"), bs, GUILayout.Height(26)))
        { InvertAll(); SetStatus(T("Inverted all values.", "전체 반전 완료.", "全ての値を反転しました。")); }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
    }

    // ────────────────────────────────────────────────────────────
    //  Status
    // ────────────────────────────────────────────────────────────
    private void DrawStatusBar()
    {
        if (_statusTime < 0 || string.IsNullOrEmpty(_statusMsg)) return;
        double elapsed = EditorApplication.timeSinceStartup - _statusTime;
        if (elapsed > 3.5) { _statusMsg = ""; _statusTime = -1; return; }
        var pc = GUI.color;
        GUI.color = new Color(1, 1, 1, Mathf.Clamp01((float)(3.5 - elapsed)));
        EditorGUILayout.LabelField(_statusMsg, new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal    = { textColor = ColMint },
        });
        GUI.color = pc;
        Repaint();
    }
    private void SetStatus(string m) { _statusMsg = m; _statusTime = EditorApplication.timeSinceStartup; }

    // ────────────────────────────────────────────────────────────
    //  Drag & Drop — GameObjects
    // ────────────────────────────────────────────────────────────
    private void HandleGameObjectDrop()
    {
        var e = Event.current;
        bool hasGo = DragAndDrop.objectReferences.Any(o => o is GameObject);
        if (!hasGo) { _goDragOver = false; return; }

        bool validZone = _clips.Count == 0 || _goDropGuiRect.Contains(e.mousePosition);
        if (!validZone) return;

        _goDragOver = (e.type == EventType.DragUpdated || e.type == EventType.DragPerform);
        if (_goDragOver) Repaint();

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences.OfType<GameObject>())
                if (AddRowForObject(obj)) added++;
            if (added > 0) SetStatus($"오브젝트 {added}개 행 추가됨.");
            _goDragOver = false;
            e.Use();
            Repaint();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Cell Click
    // ────────────────────────────────────────────────────────────
    private void HandleCellClick(int r, int c)
    {
        float? cur  = _grid[r][c];
        
        float? next;
        if (Event.current.button == 1) 
            next = null; 
        else 
        {
            bool isOn = cur.HasValue && cur.Value > 0.5f;
            next = isOn ? 0f : 1f;
        }

        _grid[r][c] = next;

        SaveClip(c);

        if (_previewClipIdx == c) PreviewClip(c);
        else                       ApplyCellToScene(r, next);
        Repaint();
    }

    // ────────────────────────────────────────────────────────────
    //  Scene Sync
    // ────────────────────────────────────────────────────────────
    private void UpdatePreview(int r, int c)
    {
        if (_previewClipIdx == c) PreviewClip(c);
        else ApplyCellToScene(r, _grid[r][c]);
        Repaint();
    }

    private void PreviewClip(int c)
    {
        if (_avatarRoot == null) return;
        for (int r = 0; r < _rows.Count; r++)
        {
            float? state = _grid[r][c];
            if (!state.HasValue) continue;
            ApplyToScene(_rows[r], state.Value, "Toggle Animator Preview");
        }
    }

    private void ApplyCellToScene(int r, float? state)
    {
        if (_avatarRoot == null || !state.HasValue) return;
        ApplyToScene(_rows[r], state.Value, "Toggle Animator");
    }

    private void ApplyToScene(RowData row, float val, string undoMsg)
    {
        var t = _avatarRoot.transform.Find(row.path);
        if (t == null && !string.IsNullOrEmpty(row.path)) return;
        if (t == null && string.IsNullOrEmpty(row.path)) t = _avatarRoot.transform; 

        if (row.IsBlendShape)
        {
            var smr = t.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                string bsName = row.propName.Substring(11);
                int idx = smr.sharedMesh.GetBlendShapeIndex(bsName);
                if (idx >= 0)
                {
                    Undo.RecordObject(smr, undoMsg);
                    smr.SetBlendShapeWeight(idx, val);
                }
            }
        }
        else
        {
            Undo.RecordObject(t.gameObject, undoMsg);
            t.gameObject.SetActive(val > 0.5f);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Data Management
    // ────────────────────────────────────────────────────────────
    private void RebuildGrid()
    {
        var clipBindings = new HashSet<RowData>();
        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "m_IsActive" || b.propertyName.StartsWith("blendShape."))
                {
                    clipBindings.Add(new RowData { path = b.path, propName = b.propertyName });
                }
            }
        }

        foreach (var cb in clipBindings)
        {
            if (!_rows.Contains(cb))
            {
                _rows.Add(cb);
                _grid.Add(new List<float?>(new float?[_clips.Count]));
            }
        }

        for (int r = 0; r < _rows.Count; r++)
        {
            while (_grid[r].Count < _clips.Count) _grid[r].Add(null);
            while (_grid[r].Count > _clips.Count) _grid[r].RemoveAt(_grid[r].Count - 1);
        }

        for (int c = 0; c < _clips.Count; c++)
        {
            var clip = _clips[c];
            if (clip == null) continue;

            var curveDict = new Dictionary<RowData, EditorCurveBinding>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "m_IsActive" || b.propertyName.StartsWith("blendShape."))
                {
                    curveDict[new RowData { path = b.path, propName = b.propertyName }] = b;
                }
            }

            for (int r = 0; r < _rows.Count; r++)
            {
                if (curveDict.TryGetValue(_rows[r], out var b))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve != null && curve.length > 0)
                    {
                        _grid[r][c] = curve.Evaluate(0f);
                    }
                }
            }
        }
    }

    private bool AddRowForObject(GameObject obj)
    {
        string path = _avatarRoot != null
            ? AnimationUtility.CalculateTransformPath(obj.transform, _avatarRoot.transform)
            : obj.name;

        var row = new RowData { path = path, propName = "m_IsActive" };
        if (_rows.Contains(row)) return false;

        int rowIdx = _rows.Count;
        _rows.Add(row);
        _grid.Add(new List<float?>(new float?[_clips.Count]));

        for (int c = 0; c < _clips.Count; c++)
        {
            var clip = _clips[c];
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "m_IsActive" && b.path == path)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve != null && curve.length > 0)
                        _grid[rowIdx][c] = curve.Evaluate(0f);
                    break;
                }
            }
        }

        return true;
    }

    private void RemoveRow(int r)
    {
        if (r < 0 || r >= _rows.Count) return;

        RowData rowToRemove = _rows[r];

        for (int c = 0; c < _clips.Count; c++)
        {
            var clip = _clips[c];
            if (clip == null) continue;

            var binding = EditorCurveBinding.FloatCurve(
                rowToRemove.path, 
                rowToRemove.IsBlendShape ? typeof(SkinnedMeshRenderer) : typeof(GameObject), 
                rowToRemove.propName);

            Undo.RecordObject(clip, "Toggle Animator Auto-Save (Remove Row)");
            AnimationUtility.SetEditorCurve(clip, binding, null); 
            EditorUtility.SetDirty(clip);
        }

        _rows.RemoveAt(r); 
        _grid.RemoveAt(r); 

        SaveAll(); 
    }

    // ────────────────────────────────────────────────────────────
    //  Utility
    // ────────────────────────────────────────────────────────────
    private void FillMissing(float v)
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] == null) _grid[r][c] = v;
        
        SaveAll();
    }

    private void SmartFill()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            if (!_grid[r].Any(v => v != null && v.Value > 0f)) continue;
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] == null || _grid[r][c].Value <= 0f) _grid[r][c] = 0f;
        }

        SaveAll();
    }

    private void InvertAll()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            bool isBlendShape = _rows[r].IsBlendShape;
            for (int c = 0; c < _clips.Count; c++)
            {
                if (_grid[r][c].HasValue)
                {
                    if (isBlendShape) _grid[r][c] = _grid[r][c].Value > 0f ? 0f : 100f;
                    else              _grid[r][c] = _grid[r][c].Value > 0.5f ? 0f : 1f;
                }
            }
        }

        SaveAll();
    }

    // ────────────────────────────────────────────────────────────
    //  Save (Real-time Auto-Save Logic)
    // ────────────────────────────────────────────────────────────
    private void SaveAll()
    {
        for (int c = 0; c < _clips.Count; c++) SaveClipData(c);
        AssetDatabase.SaveAssets(); 
    }

    private void SaveClip(int c)
    {
        SaveClipData(c);
        AssetDatabase.SaveAssets(); 
    }

    private void SaveClipData(int c)
    {
        var clip = _clips[c];
        if (clip == null) return;
        Undo.RecordObject(clip, "Toggle Animator Auto-Save");

        for (int r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            float? st = _grid[r][c];
            
            var binding = EditorCurveBinding.FloatCurve(
                row.path, 
                row.IsBlendShape ? typeof(SkinnedMeshRenderer) : typeof(GameObject), 
                row.propName);

            if (st.HasValue) 
            {
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 0f, st.Value));
            } 
            else 
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        EditorUtility.SetDirty(clip); 
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────
    private void DrawEmptyHint(string msg)
    {
        GUILayout.FlexibleSpace();
        GUILayout.Label(msg, new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 12,
            normal    = { textColor = new Color(0.46f, 0.46f, 0.50f) },
        }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
    }

    private static void DrawBorder(Rect r, Color col, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        r.width, t),       col);
        EditorGUI.DrawRect(new Rect(r.x,        r.yMax - t, r.width, t),       col);
        EditorGUI.DrawRect(new Rect(r.x,        r.y,        t, r.height),      col);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y,        t, r.height),      col);
    }
}
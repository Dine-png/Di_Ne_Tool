using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DiNeToggleAnimator : EditorWindow
{
    // ─── DiNe Brand Colors ──────────────────────────────────────
    private static readonly Color ColMint      = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColMintDark  = new Color(0.20f, 0.58f, 0.54f);
    private static readonly Color ColDark      = new Color(0.21f, 0.21f, 0.24f);
    private static readonly Color ColDeep      = new Color(0.15f, 0.15f, 0.17f);
    private static readonly Color ColPanel     = new Color(0.24f, 0.24f, 0.27f);
    private static readonly Color ColOn        = new Color(0.18f, 0.65f, 0.35f);
    private static readonly Color ColOff       = new Color(0.68f, 0.20f, 0.20f);
    private static readonly Color ColUnset     = new Color(0.28f, 0.28f, 0.31f);
    private static readonly Color ColHeaderBg  = new Color(0.17f, 0.17f, 0.19f);

    // ─── Layout ─────────────────────────────────────────────────
    private const float LABEL_W  = 195f;
    private const float COL_W    = 90f;
    private const float ROW_H    = 26f;
    private const float HEADER_H = 54f;
    private const float DEL_W    = 22f;

    // ─── Data ───────────────────────────────────────────────────
    private GameObject            _avatarRoot;
    private List<AnimationClip>   _clips = new List<AnimationClip>();
    private List<string>          _rows  = new List<string>();   // avatar-relative paths
    private List<List<bool?>>     _grid  = new List<List<bool?>>();
    // _grid[r][c] = null/true/false

    // ─── UI State ───────────────────────────────────────────────
    private int     _previewClipIdx = -1;
    private Vector2 _gridScroll;
    private bool    _isDirty;
    private string  _statusMsg  = "";
    private double  _statusTime = -1;

    // cached styles (built once per Repaint)
    private GUIStyle _styleTitleLabel;
    private GUIStyle _styleSubLabel;
    private GUIStyle _styleCellOn;
    private GUIStyle _styleCellOff;
    private GUIStyle _styleCellUnset;
    private GUIStyle _styleColHeader;
    private GUIStyle _styleColHeaderActive;
    private GUIStyle _styleRowLabel;
    private Font     _titleFont;
    private Texture2D _iconTex;

    // ─── Menu ───────────────────────────────────────────────────
    [MenuItem("DiNe/Toggle Animator", false, 5)]
    public static void ShowWindow()
    {
        var win = GetWindow<DiNeToggleAnimator>();
        win.minSize     = new Vector2(500, 420);
        win.titleContent = new GUIContent("Toggle Animator");
    }

    void OnEnable()
    {
        _titleFont = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        _iconTex   = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
    }

    // ────────────────────────────────────────────────────────────
    //  Style builder
    // ────────────────────────────────────────────────────────────
    private void BuildStyles()
    {
        _styleTitleLabel = new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            fontSize  = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white },
        };
        _styleSubLabel = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.65f, 0.65f, 0.68f) },
        };
        _styleCellOn = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white },
            active    = { textColor = Color.white },
        };
        _styleCellOff   = new GUIStyle(_styleCellOn);
        _styleCellUnset = new GUIStyle(_styleCellOn);

        _styleColHeader = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 10, fontStyle = FontStyle.Bold, wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.78f, 0.78f, 0.80f) },
            hover     = { textColor = Color.white },
        };
        _styleColHeaderActive = new GUIStyle(_styleColHeader)
        {
            normal = { textColor = new Color(0.08f, 0.08f, 0.10f) },
            hover  = { textColor = new Color(0.10f, 0.10f, 0.12f) },
        };
        _styleRowLabel = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = new Color(0.88f, 0.88f, 0.90f) },
        };
    }

    // ────────────────────────────────────────────────────────────
    //  OnGUI
    // ────────────────────────────────────────────────────────────
    void OnGUI()
    {
        BuildStyles();

        var prevBg    = GUI.backgroundColor;
        var prevColor = GUI.color;

        GUI.backgroundColor = ColDeep;
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
        GUI.backgroundColor = prevBg;

        DrawTitleBar(prevBg);
        GUILayout.Space(5);
        DrawSetupSection(prevBg);
        GUILayout.Space(5);

        if (_clips.Count == 0)
        {
            DrawEmptyHint("Add animation clips above, then drag GameObjects into the grid to define toggle rows.");
            HandleDragDrop();
            GUILayout.EndVertical();
            GUI.backgroundColor = prevBg;
            GUI.color = prevColor;
            return;
        }

        DrawGrid(prevBg);
        GUILayout.Space(4);
        DrawBottomBar(prevBg);
        GUILayout.Space(2);
        DrawStatusBar(prevBg);

        GUILayout.EndVertical();
        HandleDragDrop();

        GUI.backgroundColor = prevBg;
        GUI.color = prevColor;
    }

    // ────────────────────────────────────────────────────────────
    //  Title Bar
    // ────────────────────────────────────────────────────────────
    private void DrawTitleBar(Color prevBg)
    {
        GUI.backgroundColor = ColDark;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (_iconTex != null)
            GUILayout.Label(_iconTex, GUILayout.Width(48), GUILayout.Height(48));
        GUILayout.Space(6);
        GUILayout.Label("Toggle Animator", _styleTitleLabel, GUILayout.Height(48));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Label("VRChat 온오프 토글 애니메이션 편집기", _styleSubLabel);
        GUILayout.Space(4);
        EditorGUILayout.EndVertical();
    }

    // ────────────────────────────────────────────────────────────
    //  Setup Section — Avatar + Clip list + Drop zone
    // ────────────────────────────────────────────────────────────
    private void DrawSetupSection(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        // ── Avatar root ──────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatar Root", EditorStyles.boldLabel, GUILayout.Width(90));
        EditorGUI.BeginChangeCheck();
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && _previewClipIdx >= 0)
            PreviewClip(_previewClipIdx);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        // ── Clip list ────────────────────────────────────────────
        EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);

        for (int c = 0; c < _clips.Count; c++)
        {
            EditorGUILayout.BeginHorizontal();

            // Index label (mint)
            var idxStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = ColMint }, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUILayout.Label($"{c + 1}", idxStyle, GUILayout.Width(18), GUILayout.Height(20));

            EditorGUI.BeginChangeCheck();
            var newClip = (AnimationClip)EditorGUILayout.ObjectField(_clips[c], typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                _clips[c] = newClip;
                RebuildGrid();
            }

            // Per-clip save
            bool hasClip = _clips[c] != null;
            EditorGUI.BeginDisabledGroup(!hasClip);
            GUI.backgroundColor = ColMintDark;
            if (GUILayout.Button("↓", GUILayout.Width(26), GUILayout.Height(20)))
            {
                SaveClip(c);
                SetStatus($"저장 완료:  {_clips[c].name}");
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = new Color(0.50f, 0.17f, 0.17f);
            if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(20)))
            {
                RemoveClip(c);
                GUI.backgroundColor = prevBg;
                break;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(4);

        // ── Clip drop zone ───────────────────────────────────────
        Rect dropRect = GUILayoutUtility.GetRect(0, 44f, GUILayout.ExpandWidth(true));

        bool isClipDrag = DragAndDrop.objectReferences.Any(o => o is AnimationClip);
        Color dropBg = isClipDrag ? new Color(0.22f, 0.60f, 0.56f, 0.35f) : new Color(0.18f, 0.18f, 0.21f);
        EditorGUI.DrawRect(dropRect, dropBg);

        // Border
        Color borderCol = isClipDrag ? ColMint : new Color(0.35f, 0.35f, 0.38f);
        DrawBorder(dropRect, borderCol, 1);

        var dropStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = isClipDrag ? ColMint : new Color(0.48f, 0.48f, 0.52f) },
        };
        GUI.Label(dropRect, "⊕  Animation Clip 을 여기에 끌어다 놓으세요  (여러 개 가능)", dropStyle);

        // Handle clip drop
        var e = Event.current;
        if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && dropRect.Contains(e.mousePosition))
        {
            if (DragAndDrop.objectReferences.Any(o => o is AnimationClip))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences.OfType<AnimationClip>())
                    {
                        if (!_clips.Contains(obj))
                        {
                            _clips.Add(obj);
                            foreach (var row in _grid) row.Add(null);
                        }
                    }
                    RebuildGrid();
                    SetStatus($"클립 {DragAndDrop.objectReferences.OfType<AnimationClip>().Count()}개 추가됨");
                    e.Use();
                }
                Repaint();
            }
        }

        GUILayout.Space(4);

        // Add clip button
        GUI.backgroundColor = ColDark;
        if (GUILayout.Button("＋  클립 개별 추가", GUILayout.Height(24)))
        {
            _clips.Add(null);
            foreach (var row in _grid) row.Add(null);
        }
        GUI.backgroundColor = prevBg;

        // Save All (shown only when dirty)
        if (_isDirty)
        {
            GUILayout.Space(3);
            GUI.backgroundColor = ColMint;
            var saveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 12, fontStyle = FontStyle.Bold,
                normal    = { textColor = ColDeep },
                hover     = { textColor = ColDeep },
            };
            if (GUILayout.Button("  ✔  Save All  ", saveStyle, GUILayout.Height(28)))
            {
                SaveAll();
                SetStatus("모든 클립 저장 완료!");
            }
            GUI.backgroundColor = prevBg;
        }

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

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        // ── Column headers ───────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("", GUILayout.Width(LABEL_W), GUILayout.Height(HEADER_H));

        for (int c = 0; c < _clips.Count; c++)
        {
            string cname  = _clips[c] != null ? _clips[c].name : $"Clip {c+1}";
            bool   isPrev = _previewClipIdx == c;

            GUI.backgroundColor = isPrev ? ColMint : ColHeaderBg;
            if (GUILayout.Button(cname,
                isPrev ? _styleColHeaderActive : _styleColHeader,
                GUILayout.Width(COL_W), GUILayout.Height(HEADER_H)))
            {
                if (isPrev) _previewClipIdx = -1;
                else        { _previewClipIdx = c; PreviewClip(c); }
            }
            GUI.backgroundColor = prevBg;
        }
        GUILayout.Space(DEL_W);
        EditorGUILayout.EndHorizontal();

        // Separator
        var sepRect = GUILayoutUtility.GetRect(0, 2f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(sepRect, new Color(0.30f, 0.82f, 0.76f, 0.35f));
        GUILayout.Space(2);

        // ── Data rows ────────────────────────────────────────────
        for (int r = 0; r < _rows.Count; r++)
        {
            Color rowBg = r % 2 == 0
                ? new Color(0.20f, 0.20f, 0.22f)
                : new Color(0.22f, 0.22f, 0.25f);

            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
            EditorGUI.DrawRect(rowRect, rowBg);

            // Row label
            GUILayout.Label(
                new GUIContent(GetShortName(_rows[r]), _rows[r]),
                _styleRowLabel,
                GUILayout.Width(LABEL_W), GUILayout.Height(ROW_H));

            // Cells
            for (int c = 0; c < _clips.Count; c++)
            {
                bool? state = _grid[r][c];
                string lbl;
                Color  bg;
                GUIStyle style;

                if (state == null)
                {
                    lbl   = "—";    bg = ColUnset;  style = _styleCellUnset;
                }
                else if (state == true)
                {
                    lbl   = "ON";   bg = ColOn;     style = _styleCellOn;
                }
                else
                {
                    lbl   = "OFF";  bg = ColOff;    style = _styleCellOff;
                }

                GUI.backgroundColor = bg;
                if (GUILayout.Button(lbl, style, GUILayout.Width(COL_W), GUILayout.Height(ROW_H)))
                    HandleCellClick(r, c);
                GUI.backgroundColor = prevBg;
            }

            // Delete row
            GUI.backgroundColor = new Color(0.42f, 0.14f, 0.14f);
            if (GUILayout.Button("✕", GUILayout.Width(DEL_W), GUILayout.Height(ROW_H)))
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

        // ── GameObject drop zone (grid bottom) ───────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(LABEL_W + 4);

        float dropW = COL_W * _clips.Count + DEL_W;
        Rect goDropRect = GUILayoutUtility.GetRect(dropW, 32f, GUILayout.ExpandWidth(false));

        bool isGoDrag = DragAndDrop.objectReferences.Any(o => o is GameObject);
        Color goBg = isGoDrag ? new Color(0.22f, 0.55f, 0.50f, 0.35f) : new Color(0.16f, 0.16f, 0.18f);
        EditorGUI.DrawRect(goDropRect, goBg);
        DrawBorder(goDropRect, isGoDrag ? ColMint : new Color(0.32f, 0.32f, 0.35f), 1);

        var goDropStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontSize = 11,
            normal    = { textColor = isGoDrag ? ColMint : new Color(0.46f, 0.46f, 0.50f) },
        };
        GUI.Label(goDropRect, "⊕  GameObject 를 여기에 드래그해서 행 추가", goDropStyle);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ────────────────────────────────────────────────────────────
    //  Bottom Bar
    // ────────────────────────────────────────────────────────────
    private void DrawBottomBar(Color prevBg)
    {
        GUI.backgroundColor = ColDark;
        EditorGUILayout.BeginHorizontal();

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11, fontStyle = FontStyle.Bold,
            normal   = { textColor = new Color(0.80f, 0.80f, 0.82f) },
            hover    = { textColor = Color.white },
        };

        if (GUILayout.Button("Fill Missing → OFF", btnStyle, GUILayout.Height(26)))
        {
            FillMissing(false);
            SetStatus("미지정 셀 전부 OFF 로 채웠습니다.");
        }
        if (GUILayout.Button("Smart Fill (exclusive)", btnStyle, GUILayout.Height(26)))
        {
            SmartFill();
            SetStatus("Smart Fill 완료: ON 이 있는 행의 나머지 클립을 OFF 처리.");
        }
        if (GUILayout.Button("Invert All", btnStyle, GUILayout.Height(26)))
        {
            InvertAll();
            SetStatus("전체 ON ↔ OFF 반전 완료.");
        }

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
    }

    // ────────────────────────────────────────────────────────────
    //  Status Bar
    // ────────────────────────────────────────────────────────────
    private void DrawStatusBar(Color prevBg)
    {
        if (_statusTime < 0) return;
        if (EditorApplication.timeSinceStartup - _statusTime > 3.5)
        {
            _statusMsg = ""; _statusTime = -1; return;
        }

        float alpha = Mathf.Clamp01((float)(3.5 - (EditorApplication.timeSinceStartup - _statusTime)));
        GUI.color = new Color(1f, 1f, 1f, alpha);
        EditorGUILayout.LabelField(_statusMsg, new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal    = { textColor = ColMint },
        });
        GUI.color = prevBg;
        Repaint();
    }

    private void SetStatus(string msg)
    {
        _statusMsg  = msg;
        _statusTime = EditorApplication.timeSinceStartup;
    }

    // ────────────────────────────────────────────────────────────
    //  Drag & Drop  (window-wide for GameObjects)
    // ────────────────────────────────────────────────────────────
    private void HandleDragDrop()
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        if (!DragAndDrop.objectReferences.Any(o => o is GameObject)) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences.OfType<GameObject>())
            {
                AddRowForObject(obj);
                added++;
            }
            SetStatus($"오브젝트 {added}개 행 추가됨.");
            e.Use();
        }
        Repaint();
    }

    // ────────────────────────────────────────────────────────────
    //  Cell Click
    // ────────────────────────────────────────────────────────────
    private void HandleCellClick(int r, int c)
    {
        bool? cur = _grid[r][c];
        bool? next;

        if (Event.current.button == 1)  // right-click → unset
            next = null;
        else                             // left-click → null/false→true, true→false
            next = cur != true;

        _grid[r][c] = next;
        _isDirty = true;

        if (_previewClipIdx == c) PreviewClip(c);
        else                       ApplyCellToScene(r, next);
        Repaint();
    }

    // ────────────────────────────────────────────────────────────
    //  Scene Sync
    // ────────────────────────────────────────────────────────────
    private void PreviewClip(int c)
    {
        if (_avatarRoot == null) return;
        for (int r = 0; r < _rows.Count; r++)
        {
            bool? state = _grid[r][c];
            if (!state.HasValue) continue;
            var t = _avatarRoot.transform.Find(_rows[r]);
            if (t != null)
            {
                Undo.RecordObject(t.gameObject, "Toggle Animator Preview");
                t.gameObject.SetActive(state.Value);
            }
        }
    }

    private void ApplyCellToScene(int r, bool? state)
    {
        if (_avatarRoot == null || !state.HasValue) return;
        var t = _avatarRoot.transform.Find(_rows[r]);
        if (t != null)
        {
            Undo.RecordObject(t.gameObject, "Toggle Animator");
            t.gameObject.SetActive(state.Value);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Data Management
    // ────────────────────────────────────────────────────────────
    private void RebuildGrid()
    {
        var newPaths = new HashSet<string>();
        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName == "m_IsActive") newPaths.Add(b.path);
        }

        foreach (var path in newPaths)
        {
            if (!_rows.Contains(path))
            {
                _rows.Add(path);
                _grid.Add(new List<bool?>(new bool?[_clips.Count]));
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
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.propertyName == "m_IsActive")
                .ToDictionary(b => b.path);

            for (int r = 0; r < _rows.Count; r++)
            {
                if (bindings.TryGetValue(_rows[r], out var b))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    _grid[r][c] = curve != null && curve.Evaluate(0f) > 0.5f;
                }
            }
        }
    }

    private void AddRowForObject(GameObject obj)
    {
        string path = _avatarRoot != null
            ? AnimationUtility.CalculateTransformPath(obj.transform, _avatarRoot.transform)
            : obj.name;

        if (_rows.Contains(path)) return;
        _rows.Add(path);
        _grid.Add(new List<bool?>(new bool?[_clips.Count]));
        _isDirty = true;
        Repaint();
    }

    private void RemoveClip(int c)
    {
        if (c < 0 || c >= _clips.Count) return;
        _clips.RemoveAt(c);
        foreach (var row in _grid) if (c < row.Count) row.RemoveAt(c);
        if      (_previewClipIdx == c) _previewClipIdx = -1;
        else if (_previewClipIdx  > c) _previewClipIdx--;
    }

    private void RemoveRow(int r)
    {
        if (r < 0 || r >= _rows.Count) return;
        _rows.RemoveAt(r);
        _grid.RemoveAt(r);
        _isDirty = true;
    }

    // ────────────────────────────────────────────────────────────
    //  Utility Fills
    // ────────────────────────────────────────────────────────────
    private void FillMissing(bool fillValue)
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] == null) _grid[r][c] = fillValue;
        _isDirty = true;
    }

    private void SmartFill()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            if (!_grid[r].Any(v => v == true)) continue;
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] != true) _grid[r][c] = false;
        }
        _isDirty = true;
    }

    private void InvertAll()
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c].HasValue) _grid[r][c] = !_grid[r][c];
        _isDirty = true;
    }

    // ────────────────────────────────────────────────────────────
    //  Save
    // ────────────────────────────────────────────────────────────
    private void SaveAll()
    {
        for (int c = 0; c < _clips.Count; c++) SaveClip(c);
        _isDirty = false;
    }

    private void SaveClip(int c)
    {
        var clip = _clips[c];
        if (clip == null) return;

        Undo.RecordObject(clip, "Toggle Animator Save");

        foreach (var b in AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName == "m_IsActive").ToArray())
            AnimationUtility.SetEditorCurve(clip, b, null);

        for (int r = 0; r < _rows.Count; r++)
        {
            bool? state = _grid[r][c];
            if (!state.HasValue) continue;
            var binding = EditorCurveBinding.FloatCurve(_rows[r], typeof(GameObject), "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, binding,
                AnimationCurve.Constant(0f, 0f, state.Value ? 1f : 0f));
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────
    private static string GetShortName(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    private void DrawEmptyHint(string msg)
    {
        GUILayout.FlexibleSpace();
        GUILayout.Label(msg, new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 12,
            normal    = { textColor = new Color(0.50f, 0.50f, 0.54f) },
        }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }
}

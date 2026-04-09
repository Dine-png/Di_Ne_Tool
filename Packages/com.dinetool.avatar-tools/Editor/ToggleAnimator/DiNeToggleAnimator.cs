using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DiNeToggleAnimator : EditorWindow
{
    // ─── DiNe Brand Colors ──────────────────────────────────────
    private static readonly Color ColMint      = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColMintDark  = new Color(0.18f, 0.55f, 0.51f);
    private static readonly Color ColDark      = new Color(0.21f, 0.21f, 0.24f);
    private static readonly Color ColDeep      = new Color(0.14f, 0.14f, 0.16f);
    private static readonly Color ColPanel     = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color ColSidebar   = new Color(0.17f, 0.17f, 0.20f);
    private static readonly Color ColOn        = new Color(0.16f, 0.62f, 0.33f);
    private static readonly Color ColOff       = new Color(0.65f, 0.18f, 0.18f);
    private static readonly Color ColUnset     = new Color(0.27f, 0.27f, 0.30f);
    private static readonly Color ColHeaderBg  = new Color(0.16f, 0.16f, 0.19f);

    // ─── Layout ─────────────────────────────────────────────────
    private const float SIDEBAR_W = 34f;
    private const float LABEL_W   = 190f;
    private const float COL_W     = 90f;
    private const float ROW_H     = 26f;
    private const float HEADER_H  = 52f;
    private const float DEL_W     = 22f;

    // ─── Data ───────────────────────────────────────────────────
    private GameObject          _avatarRoot;
    private List<AnimationClip> _clips = new List<AnimationClip>();
    private List<string>        _rows  = new List<string>();   // avatar-relative paths
    private List<List<bool?>>   _grid  = new List<List<bool?>>();
    // _grid[r][c] = null(unset) / true(ON) / false(OFF)

    // ─── UI State ───────────────────────────────────────────────
    private int     _previewClipIdx = -1;
    private Vector2 _gridScroll;
    private bool    _isDirty;
    private string  _statusMsg  = "";
    private double  _statusTime = -1;

    // ─── Assets ─────────────────────────────────────────────────
    private Font      _titleFont;
    private Texture2D _iconTex;

    // ─── Menu ───────────────────────────────────────────────────
    [MenuItem("DiNe/Toggle Animator", false, 5)]
    public static void ShowWindow()
    {
        var win = GetWindow<DiNeToggleAnimator>();
        win.minSize      = new Vector2(500, 440);
        win.titleContent = new GUIContent("Toggle Animator");
    }

    void OnEnable()
    {
        _titleFont = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        _iconTex   = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        if (_iconTex != null)
            titleContent = new GUIContent("Toggle Animator", _iconTex);
    }

    // ────────────────────────────────────────────────────────────
    //  OnGUI  –  two-column layout: sidebar | main
    // ────────────────────────────────────────────────────────────
    void OnGUI()
    {
        var prevBg    = GUI.backgroundColor;
        var prevColor = GUI.color;

        // ── Left sidebar ─────────────────────────────────────────
        Rect sidebarRect = new Rect(0, 0, SIDEBAR_W, position.height);
        EditorGUI.DrawRect(sidebarRect, ColSidebar);
        DrawBorder(new Rect(sidebarRect.xMax - 1, 0, 1, position.height), new Color(0.30f, 0.82f, 0.76f, 0.30f), 1);

        // Icon
        if (_iconTex != null)
        {
            float iconSize = SIDEBAR_W - 6f;
            GUI.DrawTexture(new Rect(3, 4, iconSize, iconSize), _iconTex, ScaleMode.ScaleToFit, true);
        }

        // Vertical title
        DrawVerticalLabel("Toggle Animator",
            new Rect(0, SIDEBAR_W + 10f, SIDEBAR_W, position.height - SIDEBAR_W - 10f),
            _titleFont, 13);

        // ── Main content area ────────────────────────────────────
        Rect mainRect = new Rect(SIDEBAR_W, 0, position.width - SIDEBAR_W, position.height);
        GUILayout.BeginArea(mainRect);
        GUILayout.BeginVertical();

        GUILayout.Space(5);
        DrawSetupSection(prevBg);
        GUILayout.Space(5);

        if (_clips.Count == 0)
        {
            DrawEmptyHint("클립 드롭존에 Animation Clip 을 끌어다 놓으세요.\n그 뒤 아래 그리드에 GameObject 를 드래그해 행을 추가합니다.");
        }
        else
        {
            DrawGrid(prevBg);
            GUILayout.Space(4);
            DrawBottomBar(prevBg);
        }

        GUILayout.Space(2);
        DrawStatusBar();
        GUILayout.EndVertical();
        GUILayout.EndArea();

        HandleGameObjectDrop();

        GUI.backgroundColor = prevBg;
        GUI.color           = prevColor;
    }

    // ────────────────────────────────────────────────────────────
    //  Vertical label helper
    // ────────────────────────────────────────────────────────────
    private void DrawVerticalLabel(string text, Rect area, Font font, int fontSize)
    {
        var mat = GUI.matrix;
        Vector2 pivot = new Vector2(area.x + area.width * 0.5f, area.y + area.height * 0.5f);
        GUIUtility.RotateAroundPivot(-90f, pivot);

        var style = new GUIStyle(EditorStyles.label)
        {
            font      = font,
            fontSize  = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.55f, 0.55f, 0.58f) },
        };
        // When rotated -90°, width/height swap visually
        GUI.Label(new Rect(area.x - area.height * 0.5f + area.width * 0.5f,
                           area.y + area.height * 0.5f - area.width * 0.5f,
                           area.height, area.width), text, style);
        GUI.matrix = mat;
    }

    // ────────────────────────────────────────────────────────────
    //  Setup Section
    // ────────────────────────────────────────────────────────────
    private void DrawSetupSection(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        // ── Avatar root ──────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatar Root", EditorStyles.boldLabel, GUILayout.Width(88));
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

            // Index
            GUILayout.Label($"{c + 1}",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = ColMint },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                },
                GUILayout.Width(18), GUILayout.Height(20));

            EditorGUI.BeginChangeCheck();
            var newClip = (AnimationClip)EditorGUILayout.ObjectField(_clips[c], typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                _clips[c] = newClip;
                RebuildGrid();
            }

            // Per-clip save
            EditorGUI.BeginDisabledGroup(_clips[c] == null);
            GUI.backgroundColor = ColMintDark;
            if (GUILayout.Button("↓", GUILayout.Width(26), GUILayout.Height(20)))
            {
                SaveClip(c);
                SetStatus($"저장 완료:  {_clips[c].name}");
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            // Remove clip
            GUI.backgroundColor = new Color(0.50f, 0.16f, 0.16f);
            if (GUILayout.Button("✕", GUILayout.Width(26), GUILayout.Height(20)))
            {
                RemoveClip(c);
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
                break;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(5);

        // ── Clip drop zone ───────────────────────────────────────
        var e = Event.current;
        bool isClipDragging = (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
                              && DragAndDrop.objectReferences.Any(o => o is AnimationClip);

        Rect dropRect = GUILayoutUtility.GetRect(0, 46f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(dropRect, isClipDragging ? new Color(0.20f, 0.58f, 0.54f, 0.25f) : new Color(0.16f, 0.16f, 0.19f));
        DrawBorder(dropRect, isClipDragging ? ColMint : new Color(0.32f, 0.32f, 0.36f), 1);

        GUI.Label(dropRect, "⊕  Animation Clip 을 여기에 끌어다 놓으세요  (여러 개 동시 가능)",
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 11,
                fontStyle = isClipDragging ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = isClipDragging ? ColMint : new Color(0.46f, 0.46f, 0.50f) },
            });

        if (dropRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.DragUpdated && DragAndDrop.objectReferences.Any(o => o is AnimationClip))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.DragPerform && DragAndDrop.objectReferences.Any(o => o is AnimationClip))
            {
                DragAndDrop.AcceptDrag();
                int added = 0;
                foreach (var clip in DragAndDrop.objectReferences.OfType<AnimationClip>())
                {
                    if (!_clips.Contains(clip))
                    {
                        _clips.Add(clip);
                        foreach (var row in _grid) row.Add(null);
                        added++;
                    }
                }
                RebuildGrid();
                SetStatus($"클립 {added}개 추가됨");
                e.Use();
                Repaint();
            }
        }

        GUILayout.Space(4);

        // ── Individual add + Save All ────────────────────────────
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = ColDark;
        if (GUILayout.Button("＋  클립 추가", GUILayout.Height(24), GUILayout.ExpandWidth(true)))
        {
            _clips.Add(null);
            foreach (var row in _grid) row.Add(null);
        }

        if (_isDirty)
        {
            GUI.backgroundColor = ColMint;
            var saveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 11, fontStyle = FontStyle.Bold,
                normal    = { textColor = ColDeep },
                hover     = { textColor = ColDeep },
            };
            if (GUILayout.Button("✔  Save All", saveStyle, GUILayout.Height(24), GUILayout.ExpandWidth(true)))
            {
                SaveAll();
                SetStatus("모든 클립 저장 완료!");
            }
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

        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        // ── Column headers ───────────────────────────────────────
        EditorGUILayout.BeginHorizontal(GUILayout.Height(HEADER_H));
        GUILayout.Label("", GUILayout.Width(LABEL_W), GUILayout.Height(HEADER_H));

        for (int c = 0; c < _clips.Count; c++)
        {
            string cname  = _clips[c] != null ? _clips[c].name : $"Clip {c + 1}";
            bool   isPrev = _previewClipIdx == c;

            GUI.backgroundColor = isPrev ? ColMint : ColHeaderBg;
            var hStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 10, fontStyle = FontStyle.Bold, wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = isPrev ? ColDeep : new Color(0.80f, 0.80f, 0.82f) },
                hover     = { textColor = isPrev ? ColDeep : Color.white },
            };
            if (GUILayout.Button(cname, hStyle, GUILayout.Width(COL_W), GUILayout.Height(HEADER_H)))
            {
                if (isPrev) _previewClipIdx = -1;
                else        { _previewClipIdx = c; PreviewClip(c); }
            }
            GUI.backgroundColor = prevBg;
        }
        GUILayout.Space(DEL_W);
        EditorGUILayout.EndHorizontal();

        // Mint separator
        var sepRect = GUILayoutUtility.GetRect(0, 2f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(sepRect, new Color(0.30f, 0.82f, 0.76f, 0.40f));
        GUILayout.Space(1);

        // ── Data rows ────────────────────────────────────────────
        for (int r = 0; r < _rows.Count; r++)
        {
            Color rowBg = r % 2 == 0 ? new Color(0.19f, 0.19f, 0.22f) : new Color(0.21f, 0.21f, 0.24f);
            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
            EditorGUI.DrawRect(rowRect, rowBg);

            // Label (full path as tooltip)
            GUILayout.Label(
                new GUIContent(GetShortName(_rows[r]), _rows[r]),
                new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 11,
                    alignment = TextAnchor.MiddleLeft,
                    normal    = { textColor = new Color(0.88f, 0.88f, 0.90f) },
                },
                GUILayout.Width(LABEL_W), GUILayout.Height(ROW_H));

            // Cells
            for (int c = 0; c < _clips.Count; c++)
            {
                bool? state = _grid[r][c];
                string lbl; Color bg;

                if      (state == null)  { lbl = "—";   bg = ColUnset; }
                else if (state == true)  { lbl = "ON";  bg = ColOn; }
                else                     { lbl = "OFF"; bg = ColOff; }

                GUI.backgroundColor = bg;
                if (GUILayout.Button(lbl, new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 11, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white },
                    hover     = { textColor = Color.white },
                }, GUILayout.Width(COL_W), GUILayout.Height(ROW_H)))
                {
                    HandleCellClick(r, c);
                }
                GUI.backgroundColor = prevBg;
            }

            // Delete row button
            GUI.backgroundColor = new Color(0.42f, 0.13f, 0.13f);
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

        // ── GameObject drop zone ─────────────────────────────────
        var e = Event.current;
        bool isGoDragging = (e.type == EventType.DragUpdated || e.type == EventType.DragPerform)
                            && DragAndDrop.objectReferences.Any(o => o is GameObject)
                            && !DragAndDrop.objectReferences.Any(o => o is AnimationClip);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(LABEL_W + 4);
        Rect goDropRect = GUILayoutUtility.GetRect(0, 32f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(goDropRect, isGoDragging ? new Color(0.20f, 0.55f, 0.50f, 0.25f) : new Color(0.15f, 0.15f, 0.17f));
        DrawBorder(goDropRect, isGoDragging ? ColMint : new Color(0.30f, 0.30f, 0.34f), 1);
        GUI.Label(goDropRect, "⊕  GameObject 를 여기에 드래그해서 행 추가",
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11,
                normal    = { textColor = isGoDragging ? ColMint : new Color(0.44f, 0.44f, 0.48f) },
            });
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
        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 11, fontStyle = FontStyle.Bold,
            normal    = { textColor = new Color(0.78f, 0.78f, 0.80f) },
            hover     = { textColor = Color.white },
        };

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Fill Missing → OFF", btnStyle, GUILayout.Height(26)))
        {
            FillMissing(false);
            SetStatus("미지정(—) 셀 전부 OFF 로 채웠습니다.");
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
    private void DrawStatusBar()
    {
        if (_statusTime < 0 || string.IsNullOrEmpty(_statusMsg)) return;
        double elapsed = EditorApplication.timeSinceStartup - _statusTime;
        if (elapsed > 3.5) { _statusMsg = ""; _statusTime = -1; return; }

        float alpha = Mathf.Clamp01((float)(3.5 - elapsed));
        var prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        EditorGUILayout.LabelField(_statusMsg,
            new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = ColMint },
            });
        GUI.color = prevColor;
        Repaint();
    }

    private void SetStatus(string msg) { _statusMsg = msg; _statusTime = EditorApplication.timeSinceStartup; }

    // ────────────────────────────────────────────────────────────
    //  Drag & Drop — GameObjects (window-wide)
    // ────────────────────────────────────────────────────────────
    private void HandleGameObjectDrop()
    {
        var e = Event.current;
        if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
        // Only GameObjects, not AnimationClips
        bool hasGo   = DragAndDrop.objectReferences.Any(o => o is GameObject);
        bool hasClip = DragAndDrop.objectReferences.Any(o => o is AnimationClip);
        if (!hasGo || hasClip) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            int added = 0;
            foreach (var obj in DragAndDrop.objectReferences.OfType<GameObject>())
            {
                if (AddRowForObject(obj)) added++;
            }
            if (added > 0) SetStatus($"오브젝트 {added}개 행 추가됨.");
            e.Use();
            Repaint();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Cell Click
    // ────────────────────────────────────────────────────────────
    private void HandleCellClick(int r, int c)
    {
        bool? cur  = _grid[r][c];
        bool? next;

        if (Event.current.button == 1) next = null;          // right-click → unset
        else                            next = cur != true;   // left-click: null/false→ON, ON→OFF

        _grid[r][c] = next;
        _isDirty    = true;

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
        // Collect all m_IsActive paths from clips
        var newPaths = new HashSet<string>();
        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName == "m_IsActive") newPaths.Add(b.path);
        }

        // Add missing rows
        foreach (var path in newPaths)
        {
            if (!_rows.Contains(path))
            {
                _rows.Add(path);
                _grid.Add(new List<bool?>(new bool?[_clips.Count]));
            }
        }

        // Ensure column counts match for all rows
        for (int r = 0; r < _rows.Count; r++)
        {
            while (_grid[r].Count < _clips.Count) _grid[r].Add(null);
            while (_grid[r].Count > _clips.Count) _grid[r].RemoveAt(_grid[r].Count - 1);
        }

        // Read values from clips
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
                // existing manual edits preserved for paths not in this clip
            }
        }
    }

    private bool AddRowForObject(GameObject obj)
    {
        string path = _avatarRoot != null
            ? AnimationUtility.CalculateTransformPath(obj.transform, _avatarRoot.transform)
            : obj.name;

        if (_rows.Contains(path)) return false;
        _rows.Add(path);
        _grid.Add(new List<bool?>(new bool?[_clips.Count]));
        _isDirty = true;
        return true;
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

    /// Each row: where any clip has ON, all other clips in that row become OFF.
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

        // Remove all existing m_IsActive bindings
        foreach (var b in AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName == "m_IsActive").ToArray())
            AnimationUtility.SetEditorCurve(clip, b, null);

        // Write grid data back
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
            normal    = { textColor = new Color(0.48f, 0.48f, 0.52f) },
        }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
    }

    private static void DrawBorder(Rect r, Color col, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x,          r.y,           r.width, t),        col);
        EditorGUI.DrawRect(new Rect(r.x,          r.yMax - t,    r.width, t),        col);
        EditorGUI.DrawRect(new Rect(r.x,          r.y,           t,       r.height), col);
        EditorGUI.DrawRect(new Rect(r.xMax - t,   r.y,           t,       r.height), col);
    }
}

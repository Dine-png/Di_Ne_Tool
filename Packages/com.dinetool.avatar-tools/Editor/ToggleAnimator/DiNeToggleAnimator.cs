using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class DiNeToggleAnimator : EditorWindow
{
    // ─── Brand Colors ───────────────────────────────────────────
    private static readonly Color ColMint   = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColDark   = new Color(0.21f, 0.21f, 0.24f);
    private static readonly Color ColOn     = new Color(0.20f, 0.68f, 0.38f);
    private static readonly Color ColOff    = new Color(0.72f, 0.24f, 0.24f);
    private static readonly Color ColUnset  = new Color(0.30f, 0.30f, 0.33f);
    private static readonly Color ColHeader = new Color(0.18f, 0.18f, 0.20f);

    // ─── Layout Constants ───────────────────────────────────────
    private const float LABEL_W  = 190f;
    private const float COL_W    = 88f;
    private const float ROW_H    = 26f;
    private const float HEADER_H = 52f;
    private const float DEL_W    = 22f;

    // ─── Data ───────────────────────────────────────────────────
    private GameObject        _avatarRoot;
    private List<AnimationClip> _clips = new List<AnimationClip>();

    // _rows[r]   = relative path from avatarRoot
    // _grid[r][c] = null (unset) / true (ON) / false (OFF)
    private List<string>        _rows = new List<string>();
    private List<List<bool?>>   _grid = new List<List<bool?>>();

    // ─── UI State ───────────────────────────────────────────────
    private int     _previewClipIdx = -1;
    private Vector2 _gridScroll;
    private bool    _isDirty;
    private string  _statusMsg  = "";
    private double  _statusTime = -1;

    // ─── Menu ───────────────────────────────────────────────────
    [MenuItem("DiNe/Toggle Animator", false, 5)]
    public static void ShowWindow()
    {
        var win = GetWindow<DiNeToggleAnimator>("Toggle Animator");
        win.minSize = new Vector2(480, 380);
        win.titleContent = new GUIContent("Toggle Animator");
    }

    // ────────────────────────────────────────────────────────────
    //  OnGUI
    // ────────────────────────────────────────────────────────────
    void OnGUI()
    {
        var prevBg = GUI.backgroundColor;

        DrawTopBar(prevBg);
        GUILayout.Space(4);

        if (_clips.Count == 0)
        {
            DrawEmptyHint("Add animation clips above to get started.\nThen drag GameObjects into the grid to define toggle rows.");
            HandleDragDrop();
            GUI.backgroundColor = prevBg;
            return;
        }

        DrawGrid(prevBg);
        GUILayout.Space(4);
        DrawBottomBar(prevBg);

        DrawStatus();
        HandleDragDrop();
        GUI.backgroundColor = prevBg;
    }

    // ────────────────────────────────────────────────────────────
    //  Top bar — title, avatar, clip list
    // ────────────────────────────────────────────────────────────
    private void DrawTopBar(Color prevBg)
    {
        EditorGUILayout.BeginVertical("box");

        // Title row
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Toggle Animator", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
        GUILayout.FlexibleSpace();
        if (_isDirty)
        {
            GUI.backgroundColor = ColMint;
            if (GUILayout.Button("  Save All  ", GUILayout.Height(24)))
            {
                SaveAll();
                SetStatus("Saved!");
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        // Avatar root
        EditorGUI.BeginChangeCheck();
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", _avatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && _previewClipIdx >= 0) PreviewClip(_previewClipIdx);

        GUILayout.Space(4);

        // Clip list
        EditorGUILayout.LabelField("Animation Clips", EditorStyles.miniLabel);
        for (int c = 0; c < _clips.Count; c++)
        {
            EditorGUILayout.BeginHorizontal();

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
            GUI.backgroundColor = new Color(0.25f, 0.55f, 0.50f);
            if (GUILayout.Button("↓", GUILayout.Width(24), GUILayout.Height(18)))
            {
                SaveClip(c);
                SetStatus($"Saved  {_clips[c].name}");
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            // Remove clip
            GUI.backgroundColor = new Color(0.55f, 0.20f, 0.20f);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
            {
                RemoveClip(c);
                GUI.backgroundColor = prevBg;
                break;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }

        // Add clip button
        GUI.backgroundColor = ColDark;
        if (GUILayout.Button("＋  Add Clip", GUILayout.Height(22)))
        {
            _clips.Add(null);
            // add a null column to every row
            foreach (var row in _grid) row.Add(null);
        }
        GUI.backgroundColor = prevBg;

        EditorGUILayout.EndVertical();
    }

    // ────────────────────────────────────────────────────────────
    //  Grid
    // ────────────────────────────────────────────────────────────
    private void DrawGrid(Color prevBg)
    {
        // Header + rows share one horizontal scroll
        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        // ── Column headers ─────────────────────────────────────
        EditorGUILayout.BeginHorizontal();

        // Top-left corner cell
        GUILayout.Label("", GUILayout.Width(LABEL_W), GUILayout.Height(HEADER_H));

        for (int c = 0; c < _clips.Count; c++)
        {
            string cname   = _clips[c] != null ? _clips[c].name : "(none)";
            bool   isPrev  = _previewClipIdx == c;

            GUI.backgroundColor = isPrev ? ColMint : ColHeader;
            var hStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize   = 10,
                fontStyle  = FontStyle.Bold,
                wordWrap   = true,
                alignment  = TextAnchor.MiddleCenter,
                normal     = { textColor = isPrev ? new Color(0.08f, 0.08f, 0.08f) : new Color(0.85f, 0.85f, 0.85f) },
                hover      = { textColor = Color.white },
            };
            if (GUILayout.Button(cname, hStyle, GUILayout.Width(COL_W), GUILayout.Height(HEADER_H)))
            {
                if (isPrev)     { _previewClipIdx = -1; }
                else            { _previewClipIdx = c; PreviewClip(c); }
            }
            GUI.backgroundColor = prevBg;
        }

        GUILayout.Space(DEL_W);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        // ── Data rows ──────────────────────────────────────────
        for (int r = 0; r < _rows.Count; r++)
        {
            bool alternate = (r % 2 == 1);
            if (alternate) EditorGUI.DrawRect(
                EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H)),
                new Color(0f, 0f, 0f, 0.06f));
            else
                EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));

            // Row label (full path as tooltip, short name as text)
            string shortName = GetShortName(_rows[r]);
            var labelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize  = 11,
            };
            GUILayout.Label(new GUIContent(shortName, _rows[r]), labelStyle,
                GUILayout.Width(LABEL_W), GUILayout.Height(ROW_H));

            // Cells
            for (int c = 0; c < _clips.Count; c++)
            {
                bool? state = _grid[r][c];
                string label;
                Color  bg;

                if (state == null)
                {
                    label = "—";
                    bg    = ColUnset;
                }
                else if (state == true)
                {
                    label = "ON";
                    bg    = ColOn;
                }
                else
                {
                    label = "OFF";
                    bg    = ColOff;
                }

                GUI.backgroundColor = bg;
                var cellStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = Color.white },
                    hover     = { textColor = Color.white },
                };

                if (GUILayout.Button(label, cellStyle, GUILayout.Width(COL_W), GUILayout.Height(ROW_H)))
                    HandleCellClick(r, c);

                GUI.backgroundColor = prevBg;
            }

            // Delete row
            GUI.backgroundColor = new Color(0.45f, 0.16f, 0.16f);
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

        // ── Drop zone row ───────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(LABEL_W + 4);
        Rect dropRect = GUILayoutUtility.GetRect(COL_W * _clips.Count + DEL_W, 30f, GUILayout.ExpandWidth(false));
        var dropStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize  = 11,
            normal    = { textColor = new Color(0.5f, 0.5f, 0.55f) },
        };
        GUI.backgroundColor = new Color(0.16f, 0.16f, 0.18f);
        GUI.Box(dropRect, "⊕  Drag GameObject here to add row", dropStyle);
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // ────────────────────────────────────────────────────────────
    //  Bottom bar — utility buttons
    // ────────────────────────────────────────────────────────────
    private void DrawBottomBar(Color prevBg)
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = ColDark;

        if (GUILayout.Button("Fill Missing → OFF", GUILayout.Height(24)))
        {
            FillMissing(false);
            SetStatus("Filled all unset cells with OFF.");
        }

        if (GUILayout.Button("Smart Fill (exclusive)", GUILayout.Height(24)))
        {
            SmartFill();
            SetStatus("Smart fill applied: each ON is exclusive across clips.");
        }

        if (GUILayout.Button("Invert All", GUILayout.Height(24)))
        {
            InvertAll();
            SetStatus("Inverted all ON/OFF values.");
        }

        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();
    }

    // ────────────────────────────────────────────────────────────
    //  Status message
    // ────────────────────────────────────────────────────────────
    private void DrawStatus()
    {
        if (_statusTime < 0) return;
        if (EditorApplication.timeSinceStartup - _statusTime > 3.0) { _statusMsg = ""; _statusTime = -1; return; }
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal    = { textColor = ColMint },
        };
        EditorGUILayout.LabelField(_statusMsg, style);
        Repaint();
    }

    private void SetStatus(string msg) { _statusMsg = msg; _statusTime = EditorApplication.timeSinceStartup; }

    // ────────────────────────────────────────────────────────────
    //  Drag & Drop
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
            foreach (var obj in DragAndDrop.objectReferences.OfType<GameObject>())
                AddRowForObject(obj);
            e.Use();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Cell click
    // ────────────────────────────────────────────────────────────
    private void HandleCellClick(int r, int c)
    {
        bool? cur  = _grid[r][c];
        bool? next;

        // Right-click → unset (null), Left-click → cycle null/false → true, true → false
        if (Event.current.button == 1)
            next = null;
        else
            next = cur != true; // null or false → true;  true → false

        _grid[r][c] = next;
        _isDirty = true;

        // Live scene update
        if (_previewClipIdx == c)
            PreviewClip(c);
        else
            ApplyCellToScene(r, next);

        Repaint();
    }

    // ────────────────────────────────────────────────────────────
    //  Scene sync
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
    //  Data management
    // ────────────────────────────────────────────────────────────
    private void RebuildGrid()
    {
        // Collect all m_IsActive paths from every clip
        var newPaths = new HashSet<string>();
        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
                if (b.propertyName == "m_IsActive") newPaths.Add(b.path);
        }

        // Add paths not yet in _rows
        foreach (var path in newPaths)
        {
            if (!_rows.Contains(path))
            {
                _rows.Add(path);
                _grid.Add(new List<bool?>(new bool?[_clips.Count]));
            }
        }

        // Ensure column counts match
        for (int r = 0; r < _rows.Count; r++)
        {
            while (_grid[r].Count < _clips.Count) _grid[r].Add(null);
            while (_grid[r].Count > _clips.Count) _grid[r].RemoveAt(_grid[r].Count - 1);
        }

        // Read values from clips into grid
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
                // else leave existing value (don't overwrite manual edits)
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
        SetStatus($"Added:  {GetShortName(path)}");
        Repaint();
    }

    private void RemoveClip(int c)
    {
        if (c < 0 || c >= _clips.Count) return;
        _clips.RemoveAt(c);
        foreach (var row in _grid) if (c < row.Count) row.RemoveAt(c);
        if (_previewClipIdx == c) _previewClipIdx = -1;
        else if (_previewClipIdx > c) _previewClipIdx--;
    }

    private void RemoveRow(int r)
    {
        if (r < 0 || r >= _rows.Count) return;
        _rows.RemoveAt(r);
        _grid.RemoveAt(r);
        _isDirty = true;
    }

    // ────────────────────────────────────────────────────────────
    //  Utility fills
    // ────────────────────────────────────────────────────────────
    private void FillMissing(bool fillValue)
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] == null) _grid[r][c] = fillValue;
        _isDirty = true;
    }

    /// Each row: wherever it's ON in one clip, set it OFF in all other clips.
    /// Rows that have no ON anywhere are left alone.
    private void SmartFill()
    {
        for (int r = 0; r < _rows.Count; r++)
        {
            // Check if there's any explicit ON
            bool anyOn = _grid[r].Any(v => v == true);
            if (!anyOn) continue;
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

        // Remove all existing m_IsActive bindings managed by this tool
        foreach (var b in AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName == "m_IsActive").ToArray())
            AnimationUtility.SetEditorCurve(clip, b, null);

        // Write grid data
        for (int r = 0; r < _rows.Count; r++)
        {
            bool? state = _grid[r][c];
            if (!state.HasValue) continue;

            var binding = EditorCurveBinding.FloatCurve(_rows[r], typeof(GameObject), "m_IsActive");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 0f, state.Value ? 1f : 0f));
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
            normal    = { textColor = new Color(0.55f, 0.55f, 0.58f) },
        }, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
    }
}

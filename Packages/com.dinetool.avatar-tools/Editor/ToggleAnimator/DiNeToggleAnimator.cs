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

    // ─── Layout ─────────────────────────────────────────────────
    private const float SIDEBAR_W  = 42f;
    private const float LABEL_W    = 190f;
    private const float COL_W      = 90f;
    private const float ROW_H      = 26f;
    private const float HEADER_H   = 52f;
    private const float DEL_W      = 22f;
    private const int   CLIP_VISIBLE = 3;       // 클립 목록 표시 줄 수

    // ─── Data ───────────────────────────────────────────────────
    private GameObject          _avatarRoot;
    private List<AnimationClip> _clips = new List<AnimationClip>();
    private List<string>        _rows  = new List<string>();  // avatar-relative paths
    private List<List<bool?>>   _grid  = new List<List<bool?>>();
    // _grid[r][c] = null(—) / true(ON) / false(OFF)

    // ─── UI State ───────────────────────────────────────────────
    private int     _previewClipIdx = -1;
    private Vector2 _clipListScroll;
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
        win.minSize = new Vector2(500, 440);
    }

    void OnEnable()
    {
        _titleFont = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        _iconTex   = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        titleContent = new GUIContent("Toggle Animator",
            _iconTex != null ? _iconTex : EditorGUIUtility.IconContent("AnimationClip Icon").image as Texture2D);
    }

    // ────────────────────────────────────────────────────────────
    //  OnGUI
    // ────────────────────────────────────────────────────────────
    void OnGUI()
    {
        var prevBg    = GUI.backgroundColor;
        var prevColor = GUI.color;

        // ── Left sidebar (fixed Rect, drawn on top) ───────────────
        float winH = position.height;
        Rect sidebarRect = new Rect(0, 0, SIDEBAR_W, winH);
        EditorGUI.DrawRect(sidebarRect, ColSidebar);
        // Mint right border
        EditorGUI.DrawRect(new Rect(SIDEBAR_W - 1, 0, 1, winH), new Color(0.30f, 0.82f, 0.76f, 0.35f));

        // Icon
        if (_iconTex != null)
        {
            float iSz = SIDEBAR_W - 8f;
            GUI.DrawTexture(new Rect(4, 5, iSz, iSz), _iconTex, ScaleMode.ScaleToFit, true);
        }

        // Vertical title — character stacking (guaranteed to work)
        DrawSidebarTitle("Toggle\nAnimator", SIDEBAR_W - 8f, SIDEBAR_W + 10f);

        // ── Main area ─────────────────────────────────────────────
        Rect mainRect = new Rect(SIDEBAR_W, 0, position.width - SIDEBAR_W, winH);
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
            DrawEmptyHint("클립 드롭존에 Animation Clip을 끌어다 놓으세요.\n그 뒤 GameObject를 드래그해서 행을 추가합니다.");
        }

        GUILayout.Space(2);
        DrawStatusBar();
        GUILayout.EndVertical();
        GUILayout.EndArea();

        // Drop event handling (after area so coords are window-space)
        HandleClipDrop();
        HandleGameObjectDrop();

        GUI.backgroundColor = prevBg;
        GUI.color           = prevColor;
    }

    // ────────────────────────────────────────────────────────────
    //  Sidebar title — character stacking
    // ────────────────────────────────────────────────────────────
    private void DrawSidebarTitle(string text, float x, float startY)
    {
        var style = new GUIStyle(EditorStyles.label)
        {
            font      = _titleFont,
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            normal    = { textColor = new Color(0.50f, 0.50f, 0.54f) },
        };

        float y = startY;
        foreach (char ch in text)
        {
            if (ch == '\n') { y += 6f; continue; }
            GUI.Label(new Rect(x - SIDEBAR_W + 4, y, SIDEBAR_W - 4, 16f), ch.ToString(), style);
            y += 14f;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Setup Section
    // ────────────────────────────────────────────────────────────
    private void DrawSetupSection(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        // Avatar root
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatar Root", EditorStyles.boldLabel, GUILayout.Width(88));
        EditorGUI.BeginChangeCheck();
        _avatarRoot = (GameObject)EditorGUILayout.ObjectField(_avatarRoot, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck() && _previewClipIdx >= 0) PreviewClip(_previewClipIdx);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);

        // ── Clip list (3 rows visible, rest scrollable) ───────────
        float clipRowH  = 22f;
        float clipAreaH = clipRowH * CLIP_VISIBLE + 4f;
        _clipListScroll = EditorGUILayout.BeginScrollView(_clipListScroll,
            false, false, GUIStyle.none, GUIStyle.none,
            GUIStyle.none, GUILayout.Height(clipAreaH));

        for (int c = 0; c < _clips.Count; c++)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(clipRowH));

            // Index
            GUILayout.Label($"{c + 1}",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = ColMint },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                },
                GUILayout.Width(18), GUILayout.Height(clipRowH));

            EditorGUI.BeginChangeCheck();
            var nc = (AnimationClip)EditorGUILayout.ObjectField(_clips[c], typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck()) { _clips[c] = nc; RebuildGrid(); }

            // Per-clip save
            EditorGUI.BeginDisabledGroup(_clips[c] == null);
            GUI.backgroundColor = ColMintDark;
            if (GUILayout.Button("↓", GUILayout.Width(24), GUILayout.Height(clipRowH)))
            {
                SaveClip(c);
                SetStatus($"저장 완료:  {_clips[c].name}");
            }
            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            // Remove
            GUI.backgroundColor = new Color(0.50f, 0.15f, 0.15f);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(clipRowH)))
            {
                RemoveClip(c);
                GUI.backgroundColor = prevBg;
                EditorGUILayout.EndHorizontal();
                break;
            }
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);

        // ── Clip drop zone ────────────────────────────────────────
        Rect dropRect = GUILayoutUtility.GetRect(0, 44f, GUILayout.ExpandWidth(true));
        // Tag rect for later event handling
        _clipDropRect = GUIUtility.GUIToScreenRect(dropRect); // store for HandleClipDrop

        bool isClipOver = _clipDragOver;
        EditorGUI.DrawRect(dropRect, isClipOver ? new Color(0.20f, 0.58f, 0.54f, 0.25f) : new Color(0.15f, 0.15f, 0.18f));
        DrawBorder(dropRect, isClipOver ? ColMint : new Color(0.32f, 0.32f, 0.36f), 1);
        GUI.Label(dropRect,
            "⊕  Animation Clip 을 여기에 끌어다 놓으세요  (여러 개 동시 가능)",
            new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 11,
                fontStyle = isClipOver ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = isClipOver ? ColMint : new Color(0.45f, 0.45f, 0.50f) },
            });
        _clipDropGuiRect = dropRect; // GUI-space for event check

        GUILayout.Space(4);

        // Add / Save All row
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = ColDark;
        if (GUILayout.Button("＋  클립 추가", GUILayout.Height(24)))
        {
            _clips.Add(null);
            foreach (var row in _grid) row.Add(null);
        }
        if (_isDirty)
        {
            GUI.backgroundColor = ColMint;
            if (GUILayout.Button("✔  Save All",
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11, fontStyle = FontStyle.Bold,
                    normal   = { textColor = ColDeep },
                    hover    = { textColor = ColDeep },
                }, GUILayout.Height(24)))
            {
                SaveAll();
                SetStatus("모든 클립 저장 완료!");
            }
        }
        GUI.backgroundColor = prevBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // Clip drop zone rect in GUI space (set during DrawSetupSection)
    private Rect   _clipDropGuiRect;
    private Rect   _clipDropRect;   // screen space (unused, keep for ref)
    private bool   _clipDragOver;

    // ────────────────────────────────────────────────────────────
    //  Grid
    // ────────────────────────────────────────────────────────────
    private void DrawGrid(Color prevBg)
    {
        GUI.backgroundColor = ColPanel;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = prevBg;

        // ── Column headers (outside scroll so always visible) ─────
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
                    fontSize  = 10, fontStyle = FontStyle.Bold, wordWrap = true,
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

        // Mint separator
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 2f, GUILayout.ExpandWidth(true)),
            new Color(0.30f, 0.82f, 0.76f, 0.35f));
        GUILayout.Space(1);

        // ── Scrollable rows ───────────────────────────────────────
        _gridScroll = EditorGUILayout.BeginScrollView(_gridScroll, GUIStyle.none, GUI.skin.verticalScrollbar);

        if (_rows.Count == 0)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("← GameObject를 창에 끌어다 놓으면 행이 추가됩니다.",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 });
            GUILayout.Space(8);
        }

        for (int r = 0; r < _rows.Count; r++)
        {
            Color rowBg = r % 2 == 0 ? new Color(0.19f, 0.19f, 0.21f) : new Color(0.21f, 0.21f, 0.24f);
            Rect rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
            EditorGUI.DrawRect(rowRect, rowBg);

            // Label
            GUILayout.Label(
                new GUIContent(GetShortName(_rows[r]), _rows[r]),
                new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 11, alignment = TextAnchor.MiddleLeft,
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
                if (GUILayout.Button(lbl,
                    new GUIStyle(GUI.skin.button)
                    {
                        fontSize  = 11, fontStyle = FontStyle.Bold,
                        normal    = { textColor = Color.white },
                        hover     = { textColor = Color.white },
                    },
                    GUILayout.Width(COL_W), GUILayout.Height(ROW_H)))
                {
                    HandleCellClick(r, c);
                }
                GUI.backgroundColor = prevBg;
            }

            // Delete row
            GUI.backgroundColor = new Color(0.42f, 0.12f, 0.12f);
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
        EditorGUILayout.EndScrollView();

        // ── GameObject drop zone (always visible, below scroll) ───
        Rect goDropRect = GUILayoutUtility.GetRect(0, 34f, GUILayout.ExpandWidth(true));
        bool isGoOver = _goDragOver;
        EditorGUI.DrawRect(goDropRect, isGoOver ? new Color(0.20f, 0.55f, 0.50f, 0.25f) : new Color(0.14f, 0.14f, 0.17f));
        DrawBorder(goDropRect, isGoOver ? ColMint : new Color(0.30f, 0.30f, 0.34f), 1);
        GUI.Label(goDropRect,
            "⊕  GameObject 를 여기에 드래그해서 행 추가",
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
        if (GUILayout.Button("Fill Missing → OFF", bs, GUILayout.Height(26)))
        { FillMissing(false); SetStatus("미지정(—) 셀 전부 OFF 로 채웠습니다."); }
        if (GUILayout.Button("Smart Fill", bs, GUILayout.Height(26)))
        { SmartFill(); SetStatus("Smart Fill: ON 있는 행의 나머지를 OFF 처리."); }
        if (GUILayout.Button("Invert All", bs, GUILayout.Height(26)))
        { InvertAll(); SetStatus("전체 ON ↔ OFF 반전 완료."); }
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
    //  Drag & Drop — Clips
    // ────────────────────────────────────────────────────────────
    private void HandleClipDrop()
    {
        var e = Event.current;
        bool hasClip = DragAndDrop.objectReferences.Any(o => o is AnimationClip);

        bool overZone = _clipDropGuiRect.Contains(e.mousePosition);
        _clipDragOver = overZone && hasClip &&
                        (e.type == EventType.DragUpdated || e.type == EventType.DragPerform);
        if (_clipDragOver) Repaint();

        if (!overZone || !hasClip) return;

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
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
            _clipDragOver = false;
            e.Use();
            Repaint();
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Drag & Drop — GameObjects
    // ────────────────────────────────────────────────────────────
    private void HandleGameObjectDrop()
    {
        var e = Event.current;
        bool hasGo   = DragAndDrop.objectReferences.Any(o => o is GameObject);
        bool hasClip = DragAndDrop.objectReferences.Any(o => o is AnimationClip);
        if (!hasGo || hasClip) { _goDragOver = false; return; }

        // Accept anywhere in the window (not just drop zone)
        bool overDropZone = _goDropGuiRect.Contains(e.mousePosition);
        _goDragOver = (e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && hasGo && !hasClip;
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
        bool? cur  = _grid[r][c];
        bool? next = Event.current.button == 1 ? (bool?)null : cur != true;

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
        // Collect all m_IsActive paths from clips
        var clipPaths = new HashSet<string>();
        foreach (var clip in _clips)
        {
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "m_IsActive")
                    clipPaths.Add(b.path);
                // Also add parent paths referenced by blendShape bindings
                else if (b.propertyName.StartsWith("blendShape.") && !string.IsNullOrEmpty(b.path))
                    clipPaths.Add(b.path);
            }
        }

        // Add new paths not yet in _rows
        foreach (var path in clipPaths)
        {
            if (!_rows.Contains(path))
            {
                _rows.Add(path);
                _grid.Add(new List<bool?>(new bool?[_clips.Count]));
            }
        }

        // Sync column count
        for (int r = 0; r < _rows.Count; r++)
        {
            while (_grid[r].Count < _clips.Count) _grid[r].Add(null);
            while (_grid[r].Count > _clips.Count) _grid[r].RemoveAt(_grid[r].Count - 1);
        }

        // Read m_IsActive values from clips
        for (int c = 0; c < _clips.Count; c++)
        {
            var clip = _clips[c];
            if (clip == null) continue;

            var active = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.propertyName == "m_IsActive")
                .ToDictionary(b => b.path);

            for (int r = 0; r < _rows.Count; r++)
            {
                if (active.TryGetValue(_rows[r], out var b))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    _grid[r][c] = curve != null && curve.Evaluate(0f) > 0.5f;
                }
                // else: keep existing manual state
            }
        }
    }

    private bool AddRowForObject(GameObject obj)
    {
        string path = _avatarRoot != null
            ? AnimationUtility.CalculateTransformPath(obj.transform, _avatarRoot.transform)
            : obj.name;

        if (_rows.Contains(path)) return false;

        int rowIdx = _rows.Count;
        _rows.Add(path);
        _grid.Add(new List<bool?>(new bool?[_clips.Count]));

        // Populate values from existing clips for this path
        for (int c = 0; c < _clips.Count; c++)
        {
            var clip = _clips[c];
            if (clip == null) continue;
            foreach (var b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == "m_IsActive" && b.path == path)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    _grid[rowIdx][c] = curve != null && curve.Evaluate(0f) > 0.5f;
                    break;
                }
            }
        }

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
        _rows.RemoveAt(r); _grid.RemoveAt(r); _isDirty = true;
    }

    // ────────────────────────────────────────────────────────────
    //  Utility
    // ────────────────────────────────────────────────────────────
    private void FillMissing(bool v)
    {
        for (int r = 0; r < _rows.Count; r++)
            for (int c = 0; c < _clips.Count; c++)
                if (_grid[r][c] == null) _grid[r][c] = v;
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

        // Remove existing m_IsActive bindings for managed paths
        foreach (var b in AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.propertyName == "m_IsActive").ToArray())
            AnimationUtility.SetEditorCurve(clip, b, null);

        for (int r = 0; r < _rows.Count; r++)
        {
            bool? st = _grid[r][c];
            if (!st.HasValue) continue;
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(_rows[r], typeof(GameObject), "m_IsActive"),
                AnimationCurve.Constant(0f, 0f, st.Value ? 1f : 0f));
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
    }

    // ────────────────────────────────────────────────────────────
    //  Helpers
    // ────────────────────────────────────────────────────────────
    private static string GetShortName(string path)
    {
        int s = path.LastIndexOf('/');
        return s >= 0 ? path.Substring(s + 1) : path;
    }

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

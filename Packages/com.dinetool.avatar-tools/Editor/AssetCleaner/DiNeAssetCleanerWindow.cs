using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiNeTool.AssetCleaner
{
    /// <summary>
    /// 프로젝트의 씬 파일들을 검사해, 선택한 씬들이 의존(GetDependencies)하지 않는
    /// 에셋을 폴더 트리로 보여주고 일괄로 휴지통에 보낸다.
    ///
    /// 안전 원칙: 선택한 씬에서 쓰이는 파일은 후보에 절대 포함되지 않는다.
    /// GetDependencies는 직렬화된 참조를 재귀로 따라가므로 Expression 메뉴 아이콘,
    /// 머티리얼 스왑 애니메이션 등 일반 정리 툴이 놓치는 참조까지 모두 보존된다.
    /// 삭제는 하드 삭제가 아니라 MoveAssetToTrash(OS 휴지통, 복구 가능)로만 한다.
    /// "지금은 안 쓰지만 보존할" 파일은 (1) 종류 필터, (2) 보호 폴더로 후보에서 뺀다.
    /// </summary>
    public class DiNeAssetCleanerWindow : EditorWindow
    {
        // ── DiNeTool 스타일 컬러 ─────────────────────────────────────────────────
        private static readonly Color ColAccent  = new Color(0.30f, 0.82f, 0.76f);
        private static readonly Color ColDanger  = new Color(0.60f, 0.25f, 0.25f);
        private static readonly Color ColWarn    = new Color(0.72f, 0.55f, 0.18f);
        private static readonly Color ColLine    = new Color(0.30f, 0.30f, 0.35f, 0.8f);
        private static readonly Color ColSubText = new Color(0.58f, 0.58f, 0.63f);

        // 코드/씬 등 삭제 후보에서 영구 제외할 확장자 (실수 방지)
        private static readonly HashSet<string> BlockedExt =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".cs", ".asmdef", ".asmref", ".dll", ".unity", ".meta" };

        private static readonly string[] ProtectedFolderSegments =
        {
            "/Resources/",
            "/StreamingAssets/",
            "/Editor Default Resources/",
            "/Gizmos/",
            "/AddressableAssetsData/",
        };

        // ── 언어 ──────────────────────────────────────────────────────────────────
        private enum Lang { English, Korean, Japanese }
        private Lang _lang = Lang.Korean;
        private int  L => (int)_lang;
        private string T(int i) => UI[i][L];
        private string Tf(int i, params object[] a) => string.Format(UI[i][L], a);

        // 종류 필터 (체크된 종류만 삭제 후보로 본다)
        [Flags]
        private enum Cat { Texture = 1, Model = 2, Material = 4, Prefab = 8, Audio = 16, Anim = 32, Preset = 64, Other = 128 }
        private const Cat DefaultCats = Cat.Texture | Cat.Model | Cat.Material | Cat.Prefab | Cat.Audio | Cat.Anim;
        private Cat _includeCats = DefaultCats;
        private bool _includeEmptyFolders = true;
        private readonly List<string> _ignoreFolders = new List<string>(); // 보호 폴더 (이 안은 검사 제외)
        private bool _filterFoldout;

        private Texture2D _windowIcon, _tabIcon;
        private Font      _titleFont;

        // ── 씬 목록 ──────────────────────────────────────────────────────────────
        private class SceneItem { public string Path; public string Name; public bool Selected; }
        private readonly List<SceneItem> _scenes = new List<SceneItem>();
        private Vector2 _sceneScroll;

        // ── 분석 결과 트리 ────────────────────────────────────────────────────────
        private class Node
        {
            public string Name;
            public string Path;        // 에셋 경로 (폴더/파일 공통)
            public bool   IsFile;
            public long   Bytes;       // 파일: 자기 크기 / 폴더: 하위 합계
            public int    FileCount;   // 하위 파일 수
            public Texture Icon;       // 파일 아이콘 캐시 (지연 로드)
            public int    SelVersion = -1; // 선택 카운트 캐시 버전
            public int    SelCount;        // 캐시된 하위 선택 수
            public readonly SortedDictionary<string, Node> Folders =
                new SortedDictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            public readonly List<Node> Files = new List<Node>();
        }

        private bool _analyzed;
        private Node _root;
        private long _totalBytes;
        private int  _totalCount;
        private readonly HashSet<string> _used     = new HashSet<string>(); // 보존 대상 (삭제 금지)
        private readonly HashSet<string> _selected = new HashSet<string>(); // 삭제 선택된 파일
        private readonly HashSet<string> _expanded = new HashSet<string>(); // 펼쳐진 폴더
        private readonly Dictionary<string, long> _sizeByPath = new Dictionary<string, long>(); // 경로→크기(디스크 I/O 회피)
        private Vector2 _treeScroll;
        private string _status = "";
        private bool   _statusWarn;
        private Node   _hoverNode; // 마우스가 올라간 파일 행 (프리뷰용)

        // 선택 상태 메모이즈: 선택이 바뀔 때만 버전을 올려 폴더/삭제바 재계산을 트리거
        private int  _selVersion;
        private int  _selCountCache;
        private long _selBytesCache;
        private int  _selBytesCacheVer = -1;

        // 캐시된 GUI 스타일 / 아이콘
        private GUIStyle _foldoutStyle, _folderStyle, _fileStyle, _metaStyle;
        private GUIStyle _sectionStyle, _miniBtnStyle, _titleStyle, _descStyle, _bigBtnStyle;
        private Texture _sceneIcon, _folderIcon;

        [MenuItem("DiNe/EX/Asset Cleaner", false, 102)]
        public static void Open()
        {
            var win = GetWindow<DiNeAssetCleanerWindow>();
            win.minSize = new Vector2(440, 560);
            win.position = new Rect(win.position.x, win.position.y, 480, 820);
        }

        private void OnEnable()
        {
            _windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
            _tabIcon    = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
            _titleFont  = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
            titleContent = new GUIContent("Cleaner", _tabIcon);
            wantsMouseMove = true; // 호버 프리뷰 갱신용
            LoadPrefs();
            RefreshSceneList();
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseMove) Repaint();
            _hoverNode = null;

            EnsureStyles();

            // 상단 컨트롤은 고정, 결과 트리만 남은 공간을 채우는 전용 스크롤로 둔다.
            DrawHeader();
            DrawLangBar();
            HLine();
            DrawSceneSection();
            GUILayout.Space(2);
            DrawFilterSection();
            GUILayout.Space(4);
            DrawAnalyzeButton();

            if (_analyzed)
            {
                HLine();
                DrawResultToolbar();
                DrawTree();        // 남은 세로 공간을 채움 (자체 스크롤)
                DrawDeleteBar();   // 하단 고정
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusWarn ? MessageType.Warning : MessageType.Info);

            DrawHoverPreview();
        }

        private void DrawHeader()
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prevBg;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            const float iconSize = 72f;
            if (_windowIcon != null)
                GUILayout.Label(_windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
            GUILayout.Space(6);
            GUILayout.Label("Asset Cleaner", _titleStyle, GUILayout.Height(iconSize));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label(T(DESC), _descStyle);
            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void DrawLangBar()
        {
            int next = DrawCustomToolbar(L, new[] { "English", "한국어", "日本語" }, 26);
            if (next != L) { _lang = (Lang)next; SavePrefs(); }
        }

        private int DrawCustomToolbar(int selected, string[] options, float height)
        {
            EditorGUILayout.BeginHorizontal();
            int newSelected = selected;
            for (int i = 0; i < options.Length; i++)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = (i == selected) ? ColAccent : new Color(0.5f, 0.5f, 0.5f, 1f);
                var style = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                    fontSize = 12,
                    normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
                };
                if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
                    newSelected = i;
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndHorizontal();
            return newSelected;
        }

        // ── 1) 씬 선택 ────────────────────────────────────────────────────────────
        private void DrawSceneSection()
        {
            int selCount = _scenes.Count(s => s.Selected);
            SectionLabel(Tf(SCENE_SEL, selCount, _scenes.Count));

            EditorGUILayout.BeginHorizontal();
            if (MiniButton(T(SEL_ALL), ColAccent)) _scenes.ForEach(s => s.Selected = true);
            if (MiniButton(T(DESEL_ALL), ColDanger)) _scenes.ForEach(s => s.Selected = false);
            GUILayout.FlexibleSpace();
            if (MiniButton(T(REFRESH), ColAccent, 140)) RefreshSceneList();
            EditorGUILayout.EndHorizontal();

            if (_scenes.Count == 0)
            {
                EditorGUILayout.HelpBox(T(NO_SCENES), MessageType.Info);
                return;
            }

            float h = Mathf.Clamp(_scenes.Count * 18f + 8f, 72f, 300f);
            _sceneScroll = EditorGUILayout.BeginScrollView(_sceneScroll, "box", GUILayout.Height(h));
            foreach (var s in _scenes)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(16));
                s.Selected = EditorGUILayout.Toggle(s.Selected, GUILayout.Width(16));
                Icon16(_sceneIcon);
                GUILayout.Label(s.Name, _fileStyle, GUILayout.Height(16));
                GUILayout.FlexibleSpace();
                GUILayout.Label(FoldFromAssets(s.Path), _metaStyle);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (selCount > 0 && selCount < _scenes.Count)
                EditorGUILayout.HelpBox(T(PARTIAL), MessageType.Warning);
        }

        // ── 1.5) 종류 필터 + 보호 폴더 ───────────────────────────────────────────
        private void DrawFilterSection()
        {
            _filterFoldout = EditorGUILayout.Foldout(_filterFoldout, T(FILTER_TITLE), true);
            if (!_filterFoldout) return;

            EditorGUILayout.BeginVertical("box");

            GUILayout.Label(T(FILTER_HINT), _metaStyle);
            EditorGUILayout.BeginHorizontal();
            CatToggle(Cat.Texture, CAT_TEX); CatToggle(Cat.Model, CAT_MODEL);
            CatToggle(Cat.Material, CAT_MAT); CatToggle(Cat.Prefab, CAT_PREFAB);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            CatToggle(Cat.Audio, CAT_AUDIO); CatToggle(Cat.Anim, CAT_ANIM);
            CatToggle(Cat.Preset, CAT_PRESET); CatToggle(Cat.Other, CAT_OTHER);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            bool includeEmptyFolders = EditorGUILayout.ToggleLeft(EmptyFoldersLabel(), _includeEmptyFolders, GUILayout.Width(140));
            if (includeEmptyFolders != _includeEmptyFolders)
            {
                _includeEmptyFolders = includeEmptyFolders;
                OnFilterChanged();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            HLine();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(T(IGN_TITLE), _sectionStyle);
            GUILayout.FlexibleSpace();
            if (MiniButton(T(IGN_ADD), ColAccent, 110)) AddIgnoreFolder();
            EditorGUILayout.EndHorizontal();

            if (_ignoreFolders.Count == 0)
            {
                GUILayout.Label(T(IGN_EMPTY), _metaStyle);
            }
            else
            {
                string toRemove = null;
                foreach (var folder in _ignoreFolders)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Height(16));
                    Icon16(_folderIcon);
                    GUILayout.Label(folder, _fileStyle, GUILayout.Height(16));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(24))) toRemove = folder;
                    EditorGUILayout.EndHorizontal();
                }
                if (toRemove != null) { _ignoreFolders.Remove(toRemove); OnFilterChanged(); }
            }

            GUILayout.Space(2);
            GUILayout.Label(T(PROTECT_NOTE), _metaStyle);
            EditorGUILayout.EndVertical();
        }

        private void CatToggle(Cat c, int strId)
        {
            bool on = (_includeCats & c) != 0;
            bool nv = EditorGUILayout.ToggleLeft(T(strId), on, GUILayout.Width(108));
            if (nv != on)
            {
                if (nv) _includeCats |= c; else _includeCats &= ~c;
                OnFilterChanged();
            }
        }

        private string EmptyFoldersLabel()
        {
            switch (_lang)
            {
                case Lang.Korean: return "\uBE48 \uD3F4\uB354";
                case Lang.Japanese: return "\u7A7A\u306E\u30D5\u30A9\u30EB\u30C0";
                default: return "Empty folders";
            }
        }

        private string EmptyFolderMetaLabel()
        {
            switch (_lang)
            {
                case Lang.Korean: return "\uBE48 \uD3F4\uB354";
                case Lang.Japanese: return "\u7A7A";
                default: return "empty";
            }
        }

        private string SafetyNoticeLabel()
        {
            switch (_lang)
            {
                case Lang.Korean:
                    return "\uC774 \uBAA9\uB85D\uC740 '\uC120\uD0DD\uD55C \uC2E0\uC5D0\uC11C \uCC38\uC870\uB418\uC9C0 \uC54A\uB294 \uD6C4\uBCF4'\uC785\uB2C8\uB2E4. Resources, StreamingAssets, Addressables, \uC5D0\uB514\uD130 \uD2B9\uC218 \uD3F4\uB354\uB294 \uC790\uB3D9 \uBCF4\uD638\uD558\uC9C0\uB9CC, \uBB38\uC790\uC5F4/\uC678\uBD80 \uB85C\uB354\uB85C \uC4F0\uB294 \uC790\uC0B0\uC740 \uBCF4\uD638 \uD3F4\uB354\uC5D0 \uCD94\uAC00\uD574\uC8FC\uC138\uC694.";
                default:
                    return "This list means 'not referenced by the selected scenes'. Resources, StreamingAssets, Addressables, and special editor folders are protected automatically, but assets loaded by strings or external systems should be added to protected folders.";
            }
        }

        private string PartialSceneDeleteWarning()
        {
            switch (_lang)
            {
                case Lang.Korean:
                    return "\uC120\uD0DD\uD558\uC9C0 \uC54A\uC740 \uC2E0\uC774 \uC788\uC2B5\uB2C8\uB2E4.\n\uC774 \uC0C1\uD0DC\uC5D0\uC11C\uB294 \uADF8 \uC2E0\uC5D0\uC11C\uB9CC \uC4F0\uB294 \uC790\uC0B0\uB3C4 '\uBBF8\uC0AC\uC6A9' \uD6C4\uBCF4\uB85C \uBCF4\uC77C \uC218 \uC788\uC2B5\uB2C8\uB2E4.\n\n\uADF8\uB798\uB3C4 \uC0AD\uC81C\uB97C \uACC4\uC18D\uD560\uAE4C\uC694?";
                default:
                    return "Some scenes are not selected.\nAssets used only by those scenes may appear as unused candidates.\n\nContinue deleting anyway?";
            }
        }

        private void DrawAnalyzeButton()
        {
            using (new EditorGUI.DisabledScope(!_scenes.Any(s => s.Selected)))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = ColAccent;
                if (GUILayout.Button(T(ANALYZE), _bigBtnStyle, GUILayout.Height(30)))
                    Analyze();
                GUI.backgroundColor = prev;
            }
        }

        // ── 2) 결과 툴바 ──────────────────────────────────────────────────────────
        private void DrawResultToolbar()
        {
            SectionLabel(Tf(SUMMARY, _totalCount, FormatBytes(_totalBytes)));
            EditorGUILayout.HelpBox(SafetyNoticeLabel(), MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (MiniButton(T(PICK_ALL), ColAccent)) { _selected.Clear(); foreach (var f in AllFiles(_root)) _selected.Add(f.Path); _selVersion++; }
            if (MiniButton(T(PICK_NONE), ColDanger)) { _selected.Clear(); _selVersion++; }
            GUILayout.FlexibleSpace();
            if (MiniButton(T(EXPAND), ColAccent)) ExpandAll(true);
            if (MiniButton(T(COLLAPSE), ColAccent)) ExpandAll(false);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTree()
        {
            if (_totalCount == 0)
            {
                EditorGUILayout.HelpBox(T(TREE_EMPTY), MessageType.Info);
                GUILayout.FlexibleSpace();
                return;
            }

            // ExpandHeight: 툴바와 삭제바 사이의 남은 세로 공간을 트리 스크롤이 모두 차지
            // (창이 작아도 최소 높이를 충분히 줘서 리스트가 답답하지 않게)
            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll, "box",
                GUILayout.ExpandHeight(true), GUILayout.MinHeight(360));
            foreach (var folder in _root.Folders.Values) DrawFolder(folder, 0);
            foreach (var file in _root.Files) DrawFile(file, 0);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFolder(Node n, int depth)
        {
            bool expanded = _expanded.Contains(n.Path);
            var (sel, total) = FolderSelection(n);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(16));
            GUILayout.Space(depth * 14);
            if (GUILayout.Button(expanded ? "▾" : "▸", _foldoutStyle, GUILayout.Width(16), GUILayout.Height(16)))
            {
                if (expanded) _expanded.Remove(n.Path); else _expanded.Add(n.Path);
                expanded = !expanded;
            }

            EditorGUI.showMixedValue = sel > 0 && sel < total;
            bool all = total > 0 && sel == total;
            bool nv = EditorGUILayout.Toggle(all, GUILayout.Width(16));
            EditorGUI.showMixedValue = false;
            if (nv != all) SetSubtree(n, nv);

            Icon16(_folderIcon);
            GUILayout.Label(n.Name, _folderStyle, GUILayout.Height(16));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Tf(FILES_N, n.FileCount)}  {FormatBytes(n.Bytes)}", _metaStyle);
            EditorGUILayout.EndHorizontal();

            if (expanded)
            {
                foreach (var c in n.Folders.Values) DrawFolder(c, depth + 1);
                foreach (var f in n.Files) DrawFile(f, depth + 1);
            }
        }

        private void DrawFile(Node f, int depth)
        {
            EditorGUILayout.BeginHorizontal(GUILayout.Height(16));
            GUILayout.Space(depth * 14 + 16);

            bool sel = _selected.Contains(f.Path);
            bool nv = EditorGUILayout.Toggle(sel, GUILayout.Width(16));
            if (nv != sel) ToggleFile(f.Path, nv);

            if (f.Icon == null) f.Icon = f.IsFile ? AssetDatabase.GetCachedIcon(f.Path) : _folderIcon;
            Icon16(f.Icon);
            GUILayout.Label(f.Name, f.IsFile ? _fileStyle : _folderStyle, GUILayout.Height(16));
            GUILayout.FlexibleSpace();
            GUILayout.Label(f.IsFile ? FormatBytes(f.Bytes) : EmptyFolderMetaLabel(), _metaStyle, GUILayout.Width(92));

            if (GUILayout.Button(T(PING), EditorStyles.miniButton, GUILayout.Width(34)))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(f.Path));
            EditorGUILayout.EndHorizontal();

            // 마우스가 올라간 행 기록 (Repaint 시 레이아웃이 확정됨)
            if (Event.current.type == EventType.Repaint &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                _hoverNode = f;
        }

        // ── 3) 삭제 ────────────────────────────────────────────────────────────────
        private void DrawDeleteBar()
        {
            // 선택이 바뀐 경우에만 합계 재계산 (디스크 접근 없이 캐시된 크기 사용)
            if (_selBytesCacheVer != _selVersion)
            {
                long bytes = 0;
                foreach (var p in _selected)
                    if (_sizeByPath.TryGetValue(p, out long b)) bytes += b;
                _selBytesCache = bytes;
                _selCountCache = _selected.Count;
                _selBytesCacheVer = _selVersion;
            }

            using (new EditorGUI.DisabledScope(_selCountCache == 0))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = ColDanger;
                if (GUILayout.Button(Tf(DELETE_BTN, _selCountCache, FormatBytes(_selBytesCache)),
                    _bigBtnStyle, GUILayout.Height(32)))
                    DeleteSelected();
                GUI.backgroundColor = prev;
            }
            GUILayout.Label(T(TRASH_HINT), _metaStyle);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  Logic
        // ══════════════════════════════════════════════════════════════════════════
        private void RefreshSceneList()
        {
            var prev = new HashSet<string>(_scenes.Where(s => s.Selected).Select(s => s.Path));
            _scenes.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsAssetsChildPath(path)) continue; // 패키지 내 씬 제외
                _scenes.Add(new SceneItem
                {
                    Path = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    Selected = prev.Count == 0 || prev.Contains(path), // 첫 로드 시 전체 선택(가장 안전)
                });
            }
            _scenes.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
        }

        // 경로로 현재 열린 씬을 찾는다 (없으면 default).
        private static Scene GetOpenScene(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == path) return s;
            }
            return default;
        }

        // 열린 씬의 비활성 포함 모든 루트 오브젝트가 참조하는 에셋을 보존 대상에 추가한다.
        private void CollectLiveDependencies(Scene scene)
        {
            var roots = scene.GetRootGameObjects(); // 비활성 루트도 포함
            foreach (var dep in EditorUtility.CollectDependencies(roots))
            {
                if (dep == null) continue;
                string p = AssetDatabase.GetAssetPath(dep);
                if (!string.IsNullOrEmpty(p)) _used.Add(p);
            }
        }

        private void RebuildUsedFromSelectedScenes()
        {
            _used.Clear();
            foreach (var scenePath in _scenes.Where(s => s.Selected).Select(s => s.Path))
            {
                _used.Add(scenePath);
                foreach (var dep in AssetDatabase.GetDependencies(scenePath, true))
                    _used.Add(dep);

                var open = GetOpenScene(scenePath);
                if (open.IsValid() && open.isLoaded)
                    CollectLiveDependencies(open);
            }
        }

        private void Analyze()
        {
            var scenes = _scenes.Where(s => s.Selected).Select(s => s.Path).ToArray();
            if (scenes.Length == 0) return;

            _used.Clear();
            _selected.Clear();
            _selVersion++;

            try
            {
                for (int i = 0; i < scenes.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Asset Cleaner",
                        Tf(PROG_DEP, i + 1, scenes.Length), (float)i / scenes.Length);
                    _used.Add(scenes[i]);
                    foreach (var dep in AssetDatabase.GetDependencies(scenes[i], true))
                        _used.Add(dep);

                    // 현재 열려 있는 씬이면 비활성(off) 오브젝트와 미저장 변경까지 포함해
                    // 라이브 의존성도 수집한다. (파일 기준 GetDependencies가 놓치는 경우 보완)
                    var open = GetOpenScene(scenes[i]);
                    if (open.IsValid() && open.isLoaded)
                        CollectLiveDependencies(open);
                }

                EditorUtility.DisplayProgressBar("Asset Cleaner", T(PROG_COLLECT), 0.9f);
                BuildTree();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _analyzed = true;
            ExpandAll(false); // 기본 접힘 — 펼친 행 수를 줄여 렌더링 부하를 낮춘다
            SetStatus(Tf(DONE, _totalCount, FormatBytes(_totalBytes)), false);
        }

        private void BuildTree()
        {
            _root = new Node { Name = "Assets", Path = "Assets", IsFile = false };
            _totalBytes = 0;
            _totalCount = 0;
            _sizeByPath.Clear();

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!IsAssetsChildPath(path)) continue;
                if (_used.Contains(path)) continue;
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (BlockedExt.Contains(Path.GetExtension(path))) continue;
                if (IsAlwaysProtectedPath(path)) continue;
                if (IsAddressableAsset(path)) continue;
                if ((Classify(path) & _includeCats) == 0) continue; // 보호 종류 제외
                if (IsIgnored(path)) continue;                       // 보호 폴더 제외

                long bytes = GetFileSize(path);
                InsertIntoTree(path, bytes);
                _sizeByPath[path] = bytes;
                _totalBytes += bytes;
                _totalCount++;
            }

            if (_includeEmptyFolders)
            {
                foreach (var folder in FindEmptyFolderRoots())
                {
                    if (_used.Contains(folder)) continue;
                    if (IsAlwaysProtectedPath(folder)) continue;
                    if (IsIgnored(folder)) continue;

                    InsertIntoTree(folder, 0, false);
                    _sizeByPath[folder] = 0;
                    _totalCount++;
                }
            }

            ComputeFolderStats(_root);
        }

        private void InsertIntoTree(string assetPath, long bytes)
        {
            InsertIntoTree(assetPath, bytes, true);
        }

        private void InsertIntoTree(string assetPath, long bytes, bool isFile)
        {
            // assetPath = "Assets/A/B/file.png"
            var parts = assetPath.Split('/');
            var node = _root;
            string accum = "Assets";
            for (int i = 1; i < parts.Length - 1; i++)
            {
                accum += "/" + parts[i];
                if (!node.Folders.TryGetValue(parts[i], out var child))
                {
                    child = new Node { Name = parts[i], Path = accum, IsFile = false };
                    node.Folders[parts[i]] = child;
                }
                node = child;
            }
            node.Files.Add(new Node { Name = parts[parts.Length - 1], Path = assetPath, IsFile = isFile, Bytes = bytes });
        }

        private IEnumerable<string> FindEmptyFolderRoots()
        {
            var folders = AssetDatabase.GetAllAssetPaths()
                .Where(p => IsAssetsChildPath(p)
                    && AssetDatabase.IsValidFolder(p)
                    && !IsAlwaysProtectedPath(p)
                    && !IsIgnored(p))
                .OrderBy(p => p.Count(c => c == '/'))
                .ToList();

            var emptyFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in folders)
                if (IsEmptyFolderTree(folder))
                    emptyFolders.Add(folder);

            foreach (var folder in folders)
            {
                if (!emptyFolders.Contains(folder)) continue;
                if (HasEmptyAncestor(folder, emptyFolders)) continue;
                yield return folder;
            }
        }

        private bool IsEmptyFolderTree(string folder)
        {
            if (!IsAssetsChildPath(folder)) return false;
            if (IsAlwaysProtectedPath(folder)) return false;
            if (IsIgnored(folder)) return false;

            string fullPath;
            try { fullPath = Path.GetFullPath(folder); }
            catch { return false; }

            if (!Directory.Exists(fullPath)) return false;

            try
            {
                foreach (var file in Directory.GetFiles(fullPath))
                    if (!string.Equals(Path.GetExtension(file), ".meta", StringComparison.OrdinalIgnoreCase))
                        return false;
            }
            catch { return false; }

            foreach (var subFolder in AssetDatabase.GetSubFolders(folder))
            {
                if (IsAlwaysProtectedPath(subFolder)) return false;
                if (IsIgnored(subFolder)) return false;
                if (!IsEmptyFolderTree(subFolder)) return false;
            }

            return true;
        }

        private static bool HasEmptyAncestor(string folder, HashSet<string> emptyFolders)
        {
            string parent = ParentAssetPath(folder);
            while (!string.IsNullOrEmpty(parent) && !string.Equals(parent, "Assets", StringComparison.Ordinal))
            {
                if (emptyFolders.Contains(parent)) return true;
                parent = ParentAssetPath(parent);
            }
            return false;
        }

        private static string ParentAssetPath(string path)
        {
            int index = path.LastIndexOf('/');
            return index > 0 ? path.Substring(0, index) : "";
        }

        private static (long bytes, int count) ComputeFolderStats(Node n)
        {
            long bytes = 0; int count = 0;
            foreach (var f in n.Files) { bytes += f.Bytes; count++; }
            foreach (var c in n.Folders.Values)
            {
                var (b, cnt) = ComputeFolderStats(c);
                bytes += b; count += cnt;
            }
            n.Bytes = bytes; n.FileCount = count;
            return (bytes, count);
        }

        private void DeleteSelected()
        {
            // 안전 이중 검증: 보존 대상(_used)은 절대 삭제하지 않는다.
            RebuildUsedFromSelectedScenes();
            var paths = _selected
                .Where(IsAssetsChildPath)
                .Where(p => !_used.Contains(p))
                .Where(p => !IsAlwaysProtectedPath(p))
                .Where(p => !IsAddressableAsset(p))
                .Distinct()
                .OrderByDescending(p => p.Count(c => c == '/'))
                .ToList();
            if (paths.Count == 0) { SetStatus(T(NOTHING), true); return; }

            long bytes = paths.Sum(GetFileSize);
            if (_scenes.Any(s => !s.Selected) &&
                !EditorUtility.DisplayDialog("Asset Cleaner", PartialSceneDeleteWarning(), T(DLG_OK), T(DLG_CANCEL)))
                return;

            if (!EditorUtility.DisplayDialog("Asset Cleaner",
                Tf(DLG_BODY, paths.Count, FormatBytes(bytes)), T(DLG_OK), T(DLG_CANCEL)))
                return;

            int ok = 0;
            var failed = new List<string>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var p in paths)
                {
                    if (!IsAssetsChildPath(p))
                    {
                        failed.Add(p);
                        continue;
                    }

                    if (AssetDatabase.MoveAssetToTrash(p)) ok++;
                    else failed.Add(p);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            SetStatus(Tf(DELETED, ok, FormatBytes(bytes)) + (failed.Count > 0 ? Tf(FAIL_SUFFIX, failed.Count) : ""),
                failed.Count > 0);
            Analyze(); // 트리 갱신
        }

        // ── 필터 변경 시 트리만 다시 빌드 (씬 의존성 재계산은 불필요) ───────────────
        private void OnFilterChanged()
        {
            SavePrefs();
            if (_analyzed && _used.Count > 0)
            {
                BuildTree();
                _selected.Clear();
                _selVersion++;
                ExpandAll(false);
            }
        }

        private void AddIgnoreFolder()
        {
            string abs = EditorUtility.OpenFolderPanel(T(IGN_PICK_TITLE), Application.dataPath, "");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = ToAssetRelativePath(abs);
            if (!IsAssetsChildPath(rel)) return;
            if (!AssetDatabase.IsValidFolder(rel)) return;
            if (!_ignoreFolders.Contains(rel)) { _ignoreFolders.Add(rel); _ignoreFolders.Sort(StringComparer.OrdinalIgnoreCase); OnFilterChanged(); }
        }

        private bool IsIgnored(string path)
        {
            foreach (var f in _ignoreFolders)
                if (path.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsAssetsChildPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static bool IsAlwaysProtectedPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            string wrapped = "/" + normalized.Trim('/') + "/";
            foreach (var segment in ProtectedFolderSegments)
                if (wrapped.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static bool IsAddressableAsset(string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return false;

            try
            {
                var settingsType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettingsDefaultObject"))
                    .FirstOrDefault(t => t != null);
                if (settingsType == null) return false;

                var settingsProperty = settingsType.GetProperty("Settings", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var settings = settingsProperty?.GetValue(null, null);
                if (settings == null) return false;

                var findAssetEntry = settings.GetType().GetMethod("FindAssetEntry", new[] { typeof(string) });
                if (findAssetEntry != null)
                    return findAssetEntry.Invoke(settings, new object[] { guid }) != null;

                findAssetEntry = settings.GetType().GetMethod("FindAssetEntry", new[] { typeof(string), typeof(bool) });
                return findAssetEntry?.Invoke(settings, new object[] { guid, true }) != null;
            }
            catch
            {
                return false;
            }
        }

        private static Cat Classify(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": case ".jpg": case ".jpeg": case ".tga": case ".psd":
                case ".tif": case ".tiff": case ".exr": case ".bmp": case ".gif": case ".hdr":
                    return Cat.Texture;
                case ".fbx": case ".obj": case ".blend": case ".dae": case ".3ds": case ".max":
                    return Cat.Model;
                case ".mat": return Cat.Material;
                case ".prefab": return Cat.Prefab;
                case ".wav": case ".mp3": case ".ogg": case ".aif": case ".aiff": case ".flac":
                    return Cat.Audio;
                case ".anim": case ".controller": case ".overridecontroller":
                    return Cat.Anim;
                case ".asset": case ".preset": return Cat.Preset;
                default: return Cat.Other;
            }
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  Persistence (프로젝트별 EditorPrefs)
        // ══════════════════════════════════════════════════════════════════════════
        private static string PK => "DiNe.AssetCleaner." + Application.dataPath.GetHashCode().ToString("X") + ".";

        private void LoadPrefs()
        {
            _lang          = (Lang)EditorPrefs.GetInt(PK + "lang", (int)Lang.Korean);
            _includeCats   = (Cat)EditorPrefs.GetInt(PK + "cats", (int)DefaultCats);
            _includeEmptyFolders = EditorPrefs.GetBool(PK + "emptyFolders", true);
            _ignoreFolders.Clear();
            _ignoreFolders.AddRange(EditorPrefs.GetString(PK + "ignore", "")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt(PK + "lang", (int)_lang);
            EditorPrefs.SetInt(PK + "cats", (int)_includeCats);
            EditorPrefs.SetBool(PK + "emptyFolders", _includeEmptyFolders);
            EditorPrefs.SetString(PK + "ignore", string.Join("\n", _ignoreFolders));
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════════════════════════════════
        private IEnumerable<Node> AllFiles(Node n)
        {
            if (n == null) yield break;
            foreach (var f in n.Files) yield return f;
            foreach (var c in n.Folders.Values)
                foreach (var f in AllFiles(c)) yield return f;
        }

        // 선택 수를 자식 캐시를 이용해 상향식으로 계산하고 버전 단위로 메모이즈한다.
        // (매 프레임 전체 서브트리를 재순회하던 비용 제거)
        private (int sel, int total) FolderSelection(Node n)
        {
            if (n.SelVersion == _selVersion) return (n.SelCount, n.FileCount);
            int sel = 0;
            foreach (var f in n.Files) if (_selected.Contains(f.Path)) sel++;
            foreach (var c in n.Folders.Values) sel += FolderSelection(c).sel;
            n.SelCount = sel;
            n.SelVersion = _selVersion;
            return (sel, n.FileCount);
        }

        private void SetSubtree(Node n, bool on)
        {
            foreach (var f in AllFiles(n))
            {
                if (on) _selected.Add(f.Path);
                else    _selected.Remove(f.Path);
            }
            _selVersion++;
        }

        private void ToggleFile(string path, bool on)
        {
            if (on) _selected.Add(path); else _selected.Remove(path);
            _selVersion++;
        }

        private void ExpandAll(bool on)
        {
            _expanded.Clear();
            if (!on || _root == null) return;
            void Walk(Node n) { foreach (var c in n.Folders.Values) { _expanded.Add(c.Path); Walk(c); } }
            Walk(_root);
        }

        private static long GetFileSize(string assetPath)
        {
            try
            {
                string full = Path.GetFullPath(assetPath);
                return File.Exists(full) ? new FileInfo(full).Length : 0;
            }
            catch { return 0; }
        }

        private static string ToAssetRelativePath(string absolutePath)
        {
            absolutePath = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + absolutePath.Substring(dataPath.Length);
            return absolutePath; // 프로젝트 밖이면 그대로 (IsValidFolder에서 걸러짐)
        }

        private static string FoldFromAssets(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? "";
            return dir.Length > 42 ? "…" + dir.Substring(dir.Length - 41) : dir;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes; int u = 0;
            while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
            return $"{size:0.##} {units[u]}";
        }

        private void SetStatus(string msg, bool warn) { _status = msg; _statusWarn = warn; }

        /// <summary>마우스가 올라간 파일의 에셋 프리뷰를 커서 옆에 떠있게 그린다.</summary>
        private void DrawHoverPreview()
        {
            if (_hoverNode == null || Event.current.type != EventType.Repaint) return;

            var asset = AssetDatabase.LoadMainAssetAtPath(_hoverNode.Path);
            if (asset == null) return;

            Texture preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null)
            {
                // 비동기 생성 중이면 다음 프레임 갱신, 일단 미니 썸네일로 폴백
                if (AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID())) Repaint();
                preview = AssetPreview.GetMiniThumbnail(asset);
            }
            if (preview == null) return;

            const float size = 132f, pad = 6f, gap = 18f;
            Vector2 m = Event.current.mousePosition;

            // 기본은 커서 오른쪽 아래, 창 밖으로 나가면 반대편으로
            var rect = new Rect(m.x + gap, m.y + gap, size, size);
            if (rect.xMax + pad > position.width)  rect.x = m.x - gap - size;
            if (rect.yMax + pad > position.height) rect.y = position.height - size - pad;
            if (rect.x < pad) rect.x = pad;
            if (rect.y < pad) rect.y = pad;

            var bg = new Rect(rect.x - pad, rect.y - pad - 16f, size + pad * 2, size + pad * 2 + 16f);
            EditorGUI.DrawRect(bg, new Color(0.10f, 0.10f, 0.12f, 0.96f));
            EditorGUI.DrawRect(new Rect(bg.x, bg.y, bg.width, 1), ColAccent);
            EditorGUI.DrawRect(new Rect(bg.x, bg.yMax - 1, bg.width, 1), ColAccent);

            GUI.Label(new Rect(bg.x + 4, bg.y + 1, bg.width - 8, 14), _hoverNode.Name, _metaStyle);
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }

        private void SectionLabel(string text) => GUILayout.Label(text, _sectionStyle);

        private bool MiniButton(string label, Color color, float width = 0)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            bool clicked = width > 0
                ? GUILayout.Button(label, _miniBtnStyle, GUILayout.Height(22), GUILayout.Width(width))
                : GUILayout.Button(label, _miniBtnStyle, GUILayout.Height(22));
            GUI.backgroundColor = prev;
            return clicked;
        }

        private static void HLine()
        {
            GUILayout.Space(4);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, ColLine);
            GUILayout.Space(4);
        }

        private void EnsureStyles()
        {
            if (_foldoutStyle != null) return;
            _foldoutStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _folderStyle  = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
            _fileStyle    = new GUIStyle(EditorStyles.label);
            _metaStyle    = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = ColSubText } };
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, normal = { textColor = ColAccent } };
            _miniBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, normal = { textColor = Color.white } };
            _titleStyle   = new GUIStyle(EditorStyles.label)
            { font = _titleFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 36, normal = { textColor = Color.white } };
            _descStyle    = new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };
            _bigBtnStyle  = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _sceneIcon    = EditorGUIUtility.IconContent("SceneAsset Icon").image;
            _folderIcon   = EditorGUIUtility.IconContent("Folder Icon").image;
        }

        /// <summary>아이콘을 16×16 고정 크기로 그려 행이 썸네일 크기로 커지는 것을 막는다.</summary>
        private static void Icon16(Texture icon)
        {
            var r = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
            if (icon != null) GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit);
        }

        // ══════════════════════════════════════════════════════════════════════════
        //  UI 문자열 (English / 한국어 / 日本語)
        // ══════════════════════════════════════════════════════════════════════════
        private const int DESC = 0, SCENE_SEL = 1, SEL_ALL = 2, DESEL_ALL = 3, REFRESH = 4,
            NO_SCENES = 5, PARTIAL = 6, ANALYZE = 7, SUMMARY = 8, PICK_ALL = 9, PICK_NONE = 10,
            EXPAND = 11, COLLAPSE = 12, TREE_EMPTY = 13, PING = 14, DELETE_BTN = 15, TRASH_HINT = 16,
            PROG_DEP = 17, PROG_COLLECT = 18, DONE = 19, DLG_BODY = 20, DLG_OK = 21, DLG_CANCEL = 22,
            NOTHING = 23, DELETED = 24, FAIL_SUFFIX = 25, FILES_N = 26, FILTER_TITLE = 27, FILTER_HINT = 28,
            CAT_TEX = 29, CAT_MODEL = 30, CAT_MAT = 31, CAT_PREFAB = 32, CAT_AUDIO = 33, CAT_ANIM = 34,
            CAT_PRESET = 35, CAT_OTHER = 36, IGN_TITLE = 37, IGN_ADD = 38, IGN_EMPTY = 39,
            IGN_PICK_TITLE = 40, PROTECT_NOTE = 41;

        private static readonly string[][] UI =
        {
            /*DESC*/        new[]{ "Finds and cleans up assets not used by the selected scenes. (Files in use are never deleted.)", "선택한 씬에서 쓰이지 않는 에셋을 찾아 정리합니다. (사용 중인 파일은 절대 지우지 않습니다)", "選択したシーンで使われていないアセットを探して整理します。（使用中のファイルは絶対に削除しません）" },
            /*SCENE_SEL*/  new[]{ "Scenes to scan  ({0} / {1})", "검사할 씬 선택  ({0} / {1})", "対象シーン  ({0} / {1})" },
            /*SEL_ALL*/    new[]{ "Select All", "전체 선택", "全て選択" },
            /*DESEL_ALL*/  new[]{ "Deselect All", "전체 해제", "全て解除" },
            /*REFRESH*/    new[]{ "↺ Refresh scenes", "↺ 씬 목록 새로고침", "↺ シーン更新" },
            /*NO_SCENES*/  new[]{ "No scenes (.unity) found under Assets.", "프로젝트(Assets) 안에서 씬(.unity)을 찾지 못했습니다.", "Assets内にシーン(.unity)が見つかりません。" },
            /*PARTIAL*/    new[]{ "Files used only by unselected scenes also show as 'unused'. Select those scenes too to keep them.", "선택하지 않은 씬에서만 쓰는 파일도 '미사용'으로 표시됩니다. 그 씬을 보존하려면 함께 선택하세요.", "選択していないシーンだけで使うファイルも『未使用』として表示されます。残すにはそのシーンも選択してください。" },
            /*ANALYZE*/    new[]{ "Analyze — find unused assets", "분석 — 미사용 에셋 찾기", "分析 — 未使用アセットを探す" },
            /*SUMMARY*/    new[]{ "Unused: {0} files  ·  total ≈ {1}", "미사용 후보: {0}개  ·  합계 ≈ {1}", "未使用候補: {0}個  ·  合計 ≈ {1}" },
            /*PICK_ALL*/   new[]{ "Check All", "모두 선택", "全てチェック" },
            /*PICK_NONE*/  new[]{ "Uncheck All", "선택 해제", "チェック解除" },
            /*EXPAND*/     new[]{ "Expand", "폴더 펼치기", "展開" },
            /*COLLAPSE*/   new[]{ "Collapse", "폴더 접기", "折りたたみ" },
            /*TREE_EMPTY*/ new[]{ "No unused assets for the selected scenes. Clean!", "선택한 씬 기준으로 미사용 에셋을 찾지 못했습니다. 깔끔하네요!", "選択したシーンに対して未使用アセットはありませんでした。" },
            /*PING*/       new[]{ "Ping", "핑", "Ping" },
            /*DELETE_BTN*/ new[]{ "Delete {0} selected  ({1})  →  Trash", "선택한 {0}개 삭제  ({1})  →  휴지통", "選択した{0}個を削除  ({1})  →  ゴミ箱" },
            /*TRASH_HINT*/ new[]{ "Deleted files go to the OS trash and can be restored.", "삭제된 파일은 OS 휴지통으로 이동하며 복구할 수 있습니다.", "削除したファイルはOSのゴミ箱に移動し、復元できます。" },
            /*PROG_DEP*/   new[]{ "Analyzing scene dependencies… ({0}/{1})", "씬 의존성 분석 중… ({0}/{1})", "シーン依存関係を分析中… ({0}/{1})" },
            /*PROG_COLLECT*/new[]{ "Collecting unused assets…", "미사용 에셋 수집 중…", "未使用アセットを収集中…" },
            /*DONE*/       new[]{ "Done — {0} unused / {1}", "분석 완료 — 미사용 {0}개 / {1}", "完了 — 未使用 {0}個 / {1}" },
            /*DLG_BODY*/   new[]{ "Move {0} files ({1}) to the trash?\nYou can restore them from the OS trash.\n\nContinue?", "{0}개 파일 ({1})을(를) 휴지통으로 보냅니다.\nOS 휴지통에서 복구할 수 있습니다.\n\n계속하시겠습니까?", "{0}個のファイル ({1}) をゴミ箱に移動します。\nOSのゴミ箱から復元できます。\n\n続けますか？" },
            /*DLG_OK*/     new[]{ "Delete", "삭제", "削除" },
            /*DLG_CANCEL*/ new[]{ "Cancel", "취소", "キャンセル" },
            /*NOTHING*/    new[]{ "No files to delete.", "삭제할 파일이 없습니다.", "削除するファイルがありません。" },
            /*DELETED*/    new[]{ "Deleted {0} ({1})", "{0}개 삭제됨 ({1})", "{0}個削除 ({1})" },
            /*FAIL_SUFFIX*/new[]{ " · {0} failed", " · 실패 {0}개", " · 失敗 {0}個" },
            /*FILES_N*/    new[]{ "{0} files", "{0}개", "{0}個" },
            /*FILTER_TITLE*/new[]{ "Target types & protection", "정리 대상 종류 · 보호 설정", "対象の種類・保護設定" },
            /*FILTER_HINT*/new[]{ "Only checked types are treated as deletable candidates.", "체크한 종류만 삭제 후보로 봅니다.", "チェックした種類のみ削除候補にします。" },
            /*CAT_TEX*/    new[]{ "Textures", "텍스처", "テクスチャ" },
            /*CAT_MODEL*/  new[]{ "Models", "모델", "モデル" },
            /*CAT_MAT*/    new[]{ "Materials", "머티리얼", "マテリアル" },
            /*CAT_PREFAB*/ new[]{ "Prefabs", "프리팹", "プレハブ" },
            /*CAT_AUDIO*/  new[]{ "Audio", "오디오", "オーディオ" },
            /*CAT_ANIM*/   new[]{ "Animations", "애니메이션", "アニメ" },
            /*CAT_PRESET*/ new[]{ "Presets/Data (.asset)", "프리셋·데이터(.asset)", "プリセット·データ(.asset)" },
            /*CAT_OTHER*/  new[]{ "Other", "기타", "その他" },
            /*IGN_TITLE*/  new[]{ "Protected folders (excluded from scan)", "보호 폴더 (검사 제외)", "保護フォルダ (検査除外)" },
            /*IGN_ADD*/    new[]{ "+ Add folder", "+ 폴더 추가", "+ フォルダ追加" },
            /*IGN_EMPTY*/  new[]{ "No protected folders.", "등록된 보호 폴더가 없습니다.", "保護フォルダはありません。" },
            /*IGN_PICK_TITLE*/new[]{ "Select a folder to protect", "보호할 폴더 선택", "保護するフォルダを選択" },
            /*PROTECT_NOTE*/new[]{ "Presets and 'Other' are protected by default. Register your reusable library as protected folders.", "프리셋·기타는 기본 보호됩니다. 재사용 라이브러리는 보호 폴더로 등록하세요.", "プリセット·その他は既定で保護されます。再利用ライブラリは保護フォルダに登録してください。" },
        };
    }
}

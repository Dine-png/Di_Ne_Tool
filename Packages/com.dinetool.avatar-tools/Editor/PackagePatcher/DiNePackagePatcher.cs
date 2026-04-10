using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;
using System.Text;

public class DiNePackagePatcher : EditorWindow
{
    [System.Serializable]
    public class PackageItem
    {
        public string SourcePath;
        public string PackagePathInZip;
        public string DisplayName;
        public bool   IsSelected = true;
        public bool   IsFromZip  = false;
        public bool   IsDone     = false;
        public bool   IsFailed   = false;

        public PackageItem(string sourcePath, string displayName, bool isFromZip, string packagePathInZip = null)
        {
            SourcePath       = sourcePath;
            DisplayName      = displayName;
            IsFromZip        = isFromZip;
            PackagePathInZip = packagePathInZip;
        }
    }

    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private string targetFolderName = "_1_Patch";
    private List<PackageItem> foundPackages = new List<PackageItem>();
    private Vector2 packageScrollPos;

    private string statusMessage   = "";
    private bool   isImporting     = false;
    private bool   forceInteractive = false;

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;

    private string[] preImportFolders;
    private int    totalPackagesToImport      = 0;
    private int    currentlyProcessedPackages = 0;
    private string pendingTargetFolderName    = "_1_Patch";
    private PackageItem _currentItem;

    private Queue<(string path, PackageItem item)> importQueue = new Queue<(string, PackageItem)>();
    private static string tempExtractPath = "Temp/DiNePatcher_Extract";

    private static readonly Color ColMint   = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColZip    = new Color(0.40f, 0.75f, 1.00f);
    private static readonly Color ColPkg    = new Color(0.55f, 0.90f, 0.65f);
    private static readonly Color ColDone   = new Color(0.40f, 0.85f, 0.55f);
    private static readonly Color ColFail   = new Color(0.90f, 0.40f, 0.40f);
    private static readonly Color ColSub    = new Color(0.60f, 0.60f, 0.60f);

    [MenuItem("DiNe/EX/Package Patcher", false, 100)]
    public static void ShowWindow()
    {
        var window = GetWindow<DiNePackagePatcher>();
        window.titleContent = new GUIContent("Package Patcher");
        window.minSize = new Vector2(400, 640);
        window.Show();
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        SetLanguage(language);
        isImporting   = false;
        statusMessage = "";
    }

    void OnDisable() { CleanTempFolder(); }

    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ── 타이틀 바 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        var titleStyle = new GUIStyle(EditorStyles.label)
        {
            font      = titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 36,
            normal    = new GUIStyleState { textColor = Color.white }
        };
        float iconSize = 72f;
        if (windowIcon != null) GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Package Patcher", titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.Label(UI_TEXT[13], new GUIStyle(EditorStyles.wordWrappedLabel)
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });
        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 언어 탭 ──
        int curLang = (int)language;
        int newLang = DrawCustomToolbar(curLang, new[] { "English", "한국어", "日本語" }, 28);
        if (newLang != curLang) { language = (LanguagePreset)newLang; SetLanguage(language); Repaint(); }

        GUILayout.Space(5);

        // ── 설정 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(UI_TEXT[1], GUILayout.Width(110));
        targetFolderName = EditorGUILayout.TextField(targetFolderName);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(UI_TEXT[14], GUILayout.Width(180));
        forceInteractive = EditorGUILayout.Toggle(forceInteractive);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 드래그 앤 드롭 영역 ──
        Rect dropArea = GUILayoutUtility.GetRect(0f, 70f, GUILayout.ExpandWidth(true));
        bool isDraggingOver = dropArea.Contains(Event.current.mousePosition)
            && (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform);
        var dropBg = isDraggingOver ? new Color(0.20f, 0.36f, 0.34f) : new Color(0.18f, 0.22f, 0.22f);
        EditorGUI.DrawRect(dropArea, dropBg);
        DrawBorder(dropArea, isDraggingOver ? ColMint : new Color(0.35f, 0.52f, 0.50f), 2);
        GUI.Label(new Rect(dropArea.x, dropArea.y + 10f, dropArea.width, 24f), "📦",
            new GUIStyle(EditorStyles.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isDraggingOver ? ColMint : new Color(0.45f, 0.62f, 0.60f) } });
        GUI.Label(new Rect(dropArea.x, dropArea.y + 34f, dropArea.width, 20f), UI_TEXT[3],
            new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isDraggingOver ? Color.white : ColSub },
                fontStyle = isDraggingOver ? FontStyle.Bold : FontStyle.Normal });
        GUI.Label(new Rect(dropArea.x, dropArea.y + 52f, dropArea.width, 16f), UI_TEXT[15],
            new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.42f, 0.42f, 0.42f) } });
        HandleDragAndDrop(dropArea);

        GUILayout.Space(5);

        // ── 패키지 목록 ──
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        int selCount = foundPackages.Count(p => p.IsSelected);
        EditorGUILayout.LabelField(
            $"{UI_TEXT[4]}  {(foundPackages.Count > 0 ? $"({selCount} / {foundPackages.Count})" : "")}",
            EditorStyles.boldLabel);
        if (foundPackages.Count > 0)
        {
            var prev = GUI.backgroundColor;
            if (GUILayout.Button(UI_TEXT[16], EditorStyles.miniButtonLeft,  GUILayout.Width(38))) { foundPackages.ForEach(p => p.IsSelected = true);  Repaint(); }
            if (GUILayout.Button(UI_TEXT[17], EditorStyles.miniButtonRight, GUILayout.Width(38))) { foundPackages.ForEach(p => p.IsSelected = false); Repaint(); }
            GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(46))) { foundPackages.Clear(); statusMessage = ""; Repaint(); }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(2);

        float listH = Mathf.Clamp(foundPackages.Count * 46f + 8f, 80f, 260f);
        packageScrollPos = EditorGUILayout.BeginScrollView(packageScrollPos, GUILayout.Height(listH));
        if (foundPackages.Count == 0)
        {
            GUILayout.Space(18f);
            EditorGUILayout.LabelField(UI_TEXT[5], new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 });
        }
        else
        {
            int removeIndex = -1;
            for (int i = 0; i < foundPackages.Count; i++)
            {
                var item = foundPackages[i];
                var rowBg = item.IsSelected ? new Color(0.18f, 0.28f, 0.26f) : new Color(0.20f, 0.20f, 0.20f);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = rowBg;
                EditorGUILayout.BeginVertical("box");
                GUI.backgroundColor = prevBg;

                EditorGUILayout.BeginHorizontal();

                item.IsSelected = EditorGUILayout.Toggle(item.IsSelected, GUILayout.Width(16), GUILayout.Height(16));
                GUILayout.Space(2);

                // 타입 뱃지
                string badgeText  = item.IsDone ? "✓" : item.IsFailed ? "✗" : item.IsFromZip ? "ZIP" : "PKG";
                Color  badgeColor = item.IsDone ? ColDone : item.IsFailed ? ColFail : item.IsFromZip ? ColZip : ColPkg;
                GUILayout.Label(badgeText, new GUIStyle(EditorStyles.miniLabel)
                    { fontStyle = FontStyle.Bold, fontSize = 9, alignment = TextAnchor.MiddleCenter,
                      normal = { textColor = badgeColor } }, GUILayout.Width(28));

                // 파일명
                string dispName = item.DisplayName;
                if (item.IsFromZip && dispName.Contains("/")) dispName = Path.GetFileName(dispName);
                Color nameColor = item.IsDone ? ColDone : item.IsFailed ? ColFail : item.IsSelected ? Color.white : ColSub;
                GUILayout.Label(dispName, new GUIStyle(EditorStyles.label)
                    { fontSize = 11, clipping = TextClipping.Clip, normal = { textColor = nameColor } },
                    GUILayout.ExpandWidth(true));

                // 진행 중 표시
                if (isImporting && item == _currentItem)
                    GUILayout.Label("…", new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = ColMint } }, GUILayout.Width(14));
                else
                    GUILayout.Space(14);

                // 삭제
                prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.45f, 0.18f, 0.18f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(18)))
                    removeIndex = i;
                GUI.backgroundColor = prevBg;

                EditorGUILayout.EndHorizontal();

                // ZIP 서브라인
                if (item.IsFromZip && !string.IsNullOrEmpty(item.PackagePathInZip))
                    GUILayout.Label($"  ↳  {Path.GetFileName(item.SourcePath)}",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.40f, 0.55f, 0.70f) } });

                EditorGUILayout.EndVertical();
                GUILayout.Space(1);
            }
            if (removeIndex != -1) { foundPackages.RemoveAt(removeIndex); Repaint(); }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(3);

        // ── 진행 바 ──
        if (isImporting && totalPackagesToImport > 0)
        {
            Rect barBg = GUILayoutUtility.GetRect(0f, 5f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barBg, new Color(0.15f, 0.15f, 0.15f));
            float ratio = (float)currentlyProcessedPackages / totalPackagesToImport;
            EditorGUI.DrawRect(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height), ColMint);
            GUILayout.Space(3);
        }

        // ── 상태 메시지 ──
        if (isImporting)
            statusMessage = $"{UI_TEXT[9]}  ({currentlyProcessedPackages} / {totalPackagesToImport})";

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

        GUILayout.FlexibleSpace();

        // ── 임포트 버튼 ──
        EditorGUI.BeginDisabledGroup(selCount == 0 || isImporting);
        var prevBgBtn = GUI.backgroundColor;
        GUI.backgroundColor = (!isImporting && selCount > 0) ? ColMint : Color.gray;
        string btnLabel = isImporting
            ? $"⏳  {currentlyProcessedPackages} / {totalPackagesToImport}"
            : $"{UI_TEXT[10]}  ({selCount})";
        if (GUILayout.Button(btnLabel, new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold,
              normal = { textColor = Color.white }, hover = { textColor = Color.white } },
            GUILayout.Height(46)))
            StartImport();
        GUI.backgroundColor = prevBgBtn;
        EditorGUI.EndDisabledGroup();
    }

    // ════════════════════════════════════════════════════════════

    private static void DrawBorder(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          r.width, t),        c);
        EditorGUI.DrawRect(new Rect(r.x,          r.yMax - t,   r.width, t),        c);
        EditorGUI.DrawRect(new Rect(r.x,          r.y,          t,       r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t,   r.y,          t,       r.height), c);
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        Event evt = Event.current;
        if (!dropArea.Contains(evt.mousePosition)) return;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (string path in DragAndDrop.paths)
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        string fullPath;
                        try { fullPath = Path.GetFullPath(path); }
                        catch { fullPath = path; }
                        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                            if (Directory.Exists(path) || File.Exists(path))
                                fullPath = path;
                        if (Directory.Exists(fullPath)) AddFromPath(fullPath, true);
                        else ProcessFile(fullPath);
                    }
                    Repaint();
                }
                evt.Use();
                break;
        }
    }

    private void AddFromPath(string path, bool isFolder)
    {
        if (isFolder)
        {
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".unitypackage") || s.EndsWith(".zip"));
            foreach (var file in files) ProcessFile(file);
        }
        else ProcessFile(path);
    }

    private void ProcessFile(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        if (ext == ".zip")
        {
            int[] codepages = { 932, 51949 };
            foreach (int cp in codepages)
            {
                try
                {
                    using (var archive = ZipFile.Open(path, ZipArchiveMode.Read, Encoding.GetEncoding(cp)))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.ToLower().EndsWith(".unitypackage"))
                                if (!foundPackages.Any(p => p.SourcePath == path && p.PackagePathInZip == entry.FullName))
                                    foundPackages.Add(new PackageItem(path, entry.FullName, true, entry.FullName));
                        }
                        return;
                    }
                }
                catch { continue; }
            }
        }
        else if (ext == ".unitypackage")
        {
            if (!foundPackages.Any(p => p.SourcePath == path && !p.IsFromZip))
                foundPackages.Add(new PackageItem(path, Path.GetFileName(path), false));
        }
    }

    private void StartImport()
    {
        var targets = foundPackages.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0) return;

        foreach (var p in foundPackages) { p.IsDone = false; p.IsFailed = false; }

        isImporting   = true;
        statusMessage = UI_TEXT[9];
        CleanTempFolder();
        importQueue.Clear();

        if (!Directory.Exists(tempExtractPath)) Directory.CreateDirectory(tempExtractPath);
        preImportFolders       = AssetDatabase.GetSubFolders("Assets");
        pendingTargetFolderName = targetFolderName;

        foreach (var item in targets)
        {
            string safeFileName = $"SafeImport_{System.Guid.NewGuid().ToString().Substring(0, 8)}.unitypackage";
            string safeTempPath = Path.Combine(tempExtractPath, safeFileName);

            if (item.IsFromZip)
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(item.SourcePath))
                    {
                        var entry = archive.GetEntry(item.PackagePathInZip);
                        if (entry != null)
                        {
                            entry.ExtractToFile(safeTempPath, true);
                            importQueue.Enqueue((Path.GetFullPath(safeTempPath), item));
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[DiNe] ZIP 추출 실패: {item.DisplayName}\n{e.Message}");
                    item.IsFailed = true;
                }
            }
            else
            {
                string fullPath;
                try { fullPath = Path.GetFullPath(item.SourcePath); }
                catch { fullPath = item.SourcePath; }
                if (!File.Exists(fullPath) && File.Exists(item.SourcePath)) fullPath = item.SourcePath;

                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Copy(fullPath, safeTempPath, true);
                        importQueue.Enqueue((Path.GetFullPath(safeTempPath), item));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DiNe] 안전 복사 실패: {item.DisplayName} | {fullPath}\n{e.Message}");
                        item.IsFailed = true;
                    }
                }
                else
                {
                    Debug.LogError($"[DiNe] 파일을 찾을 수 없습니다: {item.DisplayName} | {fullPath}");
                    item.IsFailed = true;
                }
            }
        }

        totalPackagesToImport      = importQueue.Count;
        currentlyProcessedPackages = 0;

        if (totalPackagesToImport == 0)
        {
            isImporting   = false;
            statusMessage = UI_TEXT[19];
            return;
        }

        AssetDatabase.importPackageCompleted -= OnPackageProcessed;
        AssetDatabase.importPackageFailed    -= OnPackageFailed;
        AssetDatabase.importPackageCancelled -= OnPackageProcessed;
        AssetDatabase.importPackageCompleted += OnPackageProcessed;
        AssetDatabase.importPackageFailed    += OnPackageFailed;
        AssetDatabase.importPackageCancelled += OnPackageProcessed;

        ImportNextPackageInQueue();
    }

    private void ImportNextPackageInQueue()
    {
        if (importQueue.Count > 0)
        {
            var (path, item) = importQueue.Dequeue();
            _currentItem = item;
            AssetDatabase.ImportPackage(path, forceInteractive);
        }
        else CheckIfAllFinished();
    }

    private void OnPackageProcessed(string name)
    {
        if (_currentItem != null) { _currentItem.IsDone = true; _currentItem = null; }
        currentlyProcessedPackages++;
        Repaint();
        ImportNextPackageInQueue();
    }

    private void OnPackageFailed(string name, string err)
    {
        Debug.LogError($"[DiNe] 임포트 실패: {name} ({err})");
        if (_currentItem != null) { _currentItem.IsFailed = true; _currentItem = null; }
        currentlyProcessedPackages++;
        Repaint();
        ImportNextPackageInQueue();
    }

    private void CheckIfAllFinished()
    {
        if (currentlyProcessedPackages >= totalPackagesToImport || importQueue.Count == 0)
        {
            AssetDatabase.importPackageCompleted -= OnPackageProcessed;
            AssetDatabase.importPackageFailed    -= OnPackageFailed;
            AssetDatabase.importPackageCancelled -= OnPackageProcessed;
            MoveNewFolders();
            CleanTempFolder();
            isImporting   = false;
            statusMessage = UI_TEXT[11];
            Repaint();
        }
    }

    private void MoveNewFolders()
    {
        string[] postFolders = AssetDatabase.GetSubFolders("Assets");
        var added = postFolders.Except(preImportFolders).ToList();
        if (added.Count > 0)
        {
            string targetPath = "Assets/" + pendingTargetFolderName;
            if (!AssetDatabase.IsValidFolder(targetPath))
                AssetDatabase.CreateFolder("Assets", pendingTargetFolderName);
            foreach (string f in added)
            {
                if (f == targetPath) continue;
                AssetDatabase.MoveAsset(f, targetPath + "/" + Path.GetFileName(f));
            }
        }
    }

    private static void CleanTempFolder()
    {
        try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { }
    }

    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new[]
                {
                    /*  0 */ "설정",
                    /*  1 */ "정리 폴더명",
                    /*  2 */ "소스",
                    /*  3 */ "파일 또는 폴더를 여기에 드래그하세요",
                    /*  4 */ "패키지 목록",
                    /*  5 */ "리스트가 비어 있습니다",
                    /*  6 */ "", /*  7 */ "", /*  8 */ "상태",
                    /*  9 */ "임포트 중...",
                    /* 10 */ "선택 항목 임포트 시작",
                    /* 11 */ "모든 작업 완료!",
                    /* 12 */ "",
                    /* 13 */ "패키지 파일을 드래그하여 설치하고, 한 폴더에 정리하세요.",
                    /* 14 */ "임포트 창 강제 표시 (에러 시 체크)",
                    /* 15 */ ".unitypackage · .zip · 폴더 지원",
                    /* 16 */ "전체",
                    /* 17 */ "없음",
                    /* 18 */ "Clear",
                    /* 19 */ "임포트할 파일을 찾을 수 없습니다. (콘솔 창 확인)",
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new[]
                {
                    /*  0 */ "設定",
                    /*  1 */ "整理フォルダ名",
                    /*  2 */ "ソース",
                    /*  3 */ "ファイルまたはフォルダをここにドラッグ",
                    /*  4 */ "パッケージ一覧",
                    /*  5 */ "リストが空です",
                    /*  6 */ "", /*  7 */ "", /*  8 */ "ステータス",
                    /*  9 */ "インポート中...",
                    /* 10 */ "選択項目をインポート開始",
                    /* 11 */ "全て完了！",
                    /* 12 */ "",
                    /* 13 */ "パッケージファイルをドラッグしてインストールし、一つのフォルダにまとめます。",
                    /* 14 */ "インポートダイアログを強制表示 (エラー時にチェック)",
                    /* 15 */ ".unitypackage · .zip · フォルダ対応",
                    /* 16 */ "全選択",
                    /* 17 */ "解除",
                    /* 18 */ "Clear",
                    /* 19 */ "インポートするファイルが見つかりません。(コンソール確認)",
                };
                break;
            default:
                UI_TEXT = new[]
                {
                    /*  0 */ "Settings",
                    /*  1 */ "Target Folder",
                    /*  2 */ "Source",
                    /*  3 */ "Drag files or folders here",
                    /*  4 */ "Package List",
                    /*  5 */ "List is empty",
                    /*  6 */ "", /*  7 */ "", /*  8 */ "Status",
                    /*  9 */ "Importing...",
                    /* 10 */ "Start Import Selected",
                    /* 11 */ "All Done!",
                    /* 12 */ "",
                    /* 13 */ "Drag and drop packages to install and organize them.",
                    /* 14 */ "Force Import Dialog (check on error)",
                    /* 15 */ ".unitypackage · .zip · folder supported",
                    /* 16 */ "All",
                    /* 17 */ "None",
                    /* 18 */ "Clear",
                    /* 19 */ "No files to import. (Check Console)",
                };
                break;
        }
    }

    // GUIUtility.ExitGUI() 제거 → 언어 변경 정상 동작
    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? ColMint : Color.gray;
            if (GUILayout.Button(options[i], GUILayout.Height(height))) newSelected = i;
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }
}

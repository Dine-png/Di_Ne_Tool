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
        public bool   IsSelected   = true;
        public bool   IsFromZip    = false;
        public bool   IsDone       = false;
        public bool   IsFailed     = false;

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

    private string statusMessage  = "";
    private bool   isImporting    = false;
    private bool   forceInteractive = false;
    private bool   _isDragging    = false;

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;

    private string[] preImportFolders;
    private int totalPackagesToImport     = 0;
    private int currentlyProcessedPackages = 0;
    private string pendingTargetFolderName = "_1_Patch";
    private string _currentImportingName  = "";

    private Queue<(string path, PackageItem item)> importQueue = new Queue<(string, PackageItem)>();

    private static string tempExtractPath = "Temp/DiNePatcher_Extract";

    // ── 색상 팔레트 ──
    private static readonly Color ColMint    = new Color(0.30f, 0.82f, 0.76f);
    private static readonly Color ColCard    = new Color(0.20f, 0.20f, 0.20f);
    private static readonly Color ColCardSel = new Color(0.18f, 0.30f, 0.28f);
    private static readonly Color ColZip     = new Color(0.40f, 0.75f, 1.00f);
    private static readonly Color ColPkg     = new Color(0.55f, 0.90f, 0.65f);
    private static readonly Color ColDone    = new Color(0.40f, 0.85f, 0.55f);
    private static readonly Color ColFail    = new Color(0.90f, 0.40f, 0.40f);
    private static readonly Color ColSub     = new Color(0.60f, 0.60f, 0.60f);
    private static readonly Color ColDanger  = new Color(0.75f, 0.25f, 0.25f);

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
        DrawTitleBar();

        GUILayout.Space(5);

        // ── 언어 탭 ──
        int li = (int)language;
        int ni = DrawCustomToolbar(li, new[] { "English", "한국어", "日本語" }, 28);
        if (ni != li) { language = (LanguagePreset)ni; SetLanguage(language); }

        GUILayout.Space(4);

        // ── 설정 패널 ──
        DrawSettingsPanel();

        GUILayout.Space(6);

        // ── 드래그 앤 드롭 ──
        DrawDropArea();

        GUILayout.Space(6);

        // ── 패키지 목록 ──
        DrawPackageList();

        GUILayout.Space(4);

        // ── 상태 / 임포트 버튼 ──
        DrawBottomSection();
    }

    // ════════════════════════════════════════════════════════════
    //  타이틀 바
    // ════════════════════════════════════════════════════════════
    private void DrawTitleBar()
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

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
    }

    // ════════════════════════════════════════════════════════════
    //  설정 패널
    // ════════════════════════════════════════════════════════════
    private void DrawSettingsPanel()
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        // 정리 폴더명
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(UI_TEXT[1], new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = ColSub }, fontStyle = FontStyle.Bold }, GUILayout.Width(100));
        var fieldStyle = new GUIStyle(EditorStyles.textField) { alignment = TextAnchor.MiddleLeft };
        targetFolderName = EditorGUILayout.TextField(targetFolderName, fieldStyle);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);

        // Force Import Dialog 토글
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(UI_TEXT[14], new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = ColSub }, fontStyle = FontStyle.Bold }, GUILayout.Width(100));
        forceInteractive = EditorGUILayout.Toggle(forceInteractive);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════════
    //  드래그 앤 드롭 영역
    // ════════════════════════════════════════════════════════════
    private void DrawDropArea()
    {
        Event evt = Event.current;

        // 드래그 중인지 감지
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            _isDragging = true;
        else if (evt.type == EventType.DragExited || evt.type == EventType.MouseUp)
            _isDragging = false;

        Rect dropArea = GUILayoutUtility.GetRect(0f, 80f, GUILayout.ExpandWidth(true));

        // 배경
        var bgColor = _isDragging ? new Color(0.20f, 0.38f, 0.36f) : new Color(0.18f, 0.22f, 0.22f);
        EditorGUI.DrawRect(dropArea, bgColor);

        // 점선 테두리 시뮬레이션 (얇은 solid 테두리)
        var borderColor = _isDragging ? ColMint : new Color(0.35f, 0.55f, 0.52f);
        DrawBorder(dropArea, borderColor, 2);

        // 아이콘 + 텍스트
        var iconStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 26,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = _isDragging ? ColMint : new Color(0.45f, 0.65f, 0.62f) }
        };
        var textStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 12,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = _isDragging ? Color.white : ColSub },
            fontStyle = _isDragging ? FontStyle.Bold : FontStyle.Normal
        };
        var subStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.45f, 0.45f, 0.45f) }
        };

        Rect iconRect = new Rect(dropArea.x, dropArea.y + 8f,  dropArea.width, 28f);
        Rect textRect = new Rect(dropArea.x, dropArea.y + 36f, dropArea.width, 20f);
        Rect subRect  = new Rect(dropArea.x, dropArea.y + 54f, dropArea.width, 18f);

        GUI.Label(iconRect, "📦", iconStyle);
        GUI.Label(textRect, UI_TEXT[3], textStyle);
        GUI.Label(subRect,  UI_TEXT[15], subStyle);

        HandleDragAndDrop(dropArea);

        if (_isDragging) Repaint();
    }

    private static void DrawBorder(Rect r, Color c, float t)
    {
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // ════════════════════════════════════════════════════════════
    //  패키지 목록
    // ════════════════════════════════════════════════════════════
    private void DrawPackageList()
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        // 헤더 행
        EditorGUILayout.BeginHorizontal();
        int sel = foundPackages.Count(p => p.IsSelected);
        string listTitle = $"{UI_TEXT[4]}  {(foundPackages.Count > 0 ? $"({sel} / {foundPackages.Count})" : "")}";
        GUILayout.Label(listTitle, new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
        GUILayout.FlexibleSpace();

        if (foundPackages.Count > 0)
        {
            prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.gray;
            if (GUILayout.Button(UI_TEXT[16], EditorStyles.miniButtonLeft,  GUILayout.Width(38))) { foundPackages.ForEach(p => p.IsSelected = true);  GUIUtility.ExitGUI(); }
            if (GUILayout.Button(UI_TEXT[17], EditorStyles.miniButtonRight, GUILayout.Width(38))) { foundPackages.ForEach(p => p.IsSelected = false); GUIUtility.ExitGUI(); }
            GUI.backgroundColor = ColDanger;
            if (GUILayout.Button(UI_TEXT[18], EditorStyles.miniButton, GUILayout.Width(46))) { foundPackages.Clear(); statusMessage = ""; GUIUtility.ExitGUI(); }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(3);

        // 스크롤 목록
        float listH = Mathf.Clamp(foundPackages.Count * 44f + 8f, 80f, 260f);
        packageScrollPos = EditorGUILayout.BeginScrollView(packageScrollPos, GUILayout.Height(listH));

        if (foundPackages.Count == 0)
        {
            GUILayout.Space(20f);
            GUILayout.Label(UI_TEXT[5], new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 });
        }
        else
        {
            int removeIndex = -1;
            for (int i = 0; i < foundPackages.Count; i++)
            {
                var item = foundPackages[i];
                DrawPackageCard(item, i, ref removeIndex);
            }
            if (removeIndex != -1) { foundPackages.RemoveAt(removeIndex); GUIUtility.ExitGUI(); }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawPackageCard(PackageItem item, int idx, ref int removeIdx)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = item.IsSelected ? ColCardSel : ColCard;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = prev;

        EditorGUILayout.BeginHorizontal();

        // 체크박스
        bool newSel = EditorGUILayout.Toggle(item.IsSelected, GUILayout.Width(16), GUILayout.Height(16));
        if (newSel != item.IsSelected) item.IsSelected = newSel;

        GUILayout.Space(2);

        // 타입 뱃지
        string badgeText  = item.IsFromZip ? "ZIP" : "PKG";
        Color  badgeColor = item.IsFromZip ? ColZip : ColPkg;
        if (item.IsDone)   badgeColor = ColDone;
        if (item.IsFailed) badgeColor = ColFail;

        var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold, fontSize = 9,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = badgeColor }
        };
        GUILayout.Label(badgeText, badgeStyle, GUILayout.Width(28));

        // 파일명
        string displayText = item.DisplayName;
        // ZIP 안의 패키지라면 파일명만 뽑아서 표시
        if (item.IsFromZip && displayText.Contains("/"))
            displayText = Path.GetFileName(displayText);

        Color nameColor = item.IsDone   ? ColDone
                        : item.IsFailed ? ColFail
                        : item.IsSelected ? Color.white : ColSub;

        var nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize  = 11,
            clipping  = TextClipping.Clip,
            normal    = { textColor = nameColor }
        };
        GUILayout.Label(displayText, nameStyle, GUILayout.ExpandWidth(true));

        // 상태 아이콘
        if (item.IsDone)
            GUILayout.Label("✓", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDone }, fontStyle = FontStyle.Bold }, GUILayout.Width(16));
        else if (item.IsFailed)
            GUILayout.Label("✗", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColFail }, fontStyle = FontStyle.Bold }, GUILayout.Width(16));
        else if (isImporting && item.IsSelected && !item.IsDone)
            GUILayout.Label("…", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColMint } }, GUILayout.Width(16));
        else
            GUILayout.Space(16);

        // 삭제 버튼
        prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 0.18f, 0.18f);
        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20), GUILayout.Height(18)))
            removeIdx = idx;
        GUI.backgroundColor = prev;

        EditorGUILayout.EndHorizontal();

        // ZIP 경로 서브라인
        if (item.IsFromZip && !string.IsNullOrEmpty(item.PackagePathInZip))
        {
            string srcName = Path.GetFileName(item.SourcePath);
            GUILayout.Label($"  ↳ {srcName}", new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(0.40f, 0.55f, 0.70f) } });
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(2);
    }

    // ════════════════════════════════════════════════════════════
    //  하단 상태 + 버튼
    // ════════════════════════════════════════════════════════════
    private void DrawBottomSection()
    {
        int selectedCount = foundPackages.Count(p => p.IsSelected);

        // 진행 중 상태 텍스트 갱신
        if (isImporting)
            statusMessage = $"{UI_TEXT[9]}  {currentlyProcessedPackages} / {totalPackagesToImport}";

        // 상태 메시지
        if (!string.IsNullOrEmpty(statusMessage))
        {
            bool isDone    = statusMessage.Contains(UI_TEXT[11]);
            bool isError   = statusMessage.Contains("없습니다") || statusMessage.Contains("not found") || statusMessage.Contains("見つかりません");
            Color msgColor = isDone  ? ColDone
                           : isError ? ColFail
                           : ColMint;

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(msgColor.r * 0.3f, msgColor.g * 0.3f, msgColor.b * 0.3f);
            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = prev;
            GUILayout.Label(statusMessage, new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, fontSize = 11, normal = { textColor = msgColor }, fontStyle = FontStyle.Bold });
            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
        }

        // 진행 바
        if (isImporting && totalPackagesToImport > 0)
        {
            Rect barBg = GUILayoutUtility.GetRect(0f, 6f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barBg, new Color(0.15f, 0.15f, 0.15f));
            float ratio = (float)currentlyProcessedPackages / totalPackagesToImport;
            EditorGUI.DrawRect(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height), ColMint);
            GUILayout.Space(4);
        }

        GUILayout.FlexibleSpace();

        // 임포트 버튼
        EditorGUI.BeginDisabledGroup(selectedCount == 0 || isImporting);
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = (!isImporting && selectedCount > 0) ? ColMint : Color.gray;

        string btnText = isImporting
            ? $"  ⏳  {currentlyProcessedPackages} / {totalPackagesToImport}"
            : $"  ▶  {UI_TEXT[10]}  ({selectedCount})";

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
            hover     = { textColor = Color.white }
        };
        if (GUILayout.Button(btnText, btnStyle, GUILayout.Height(46)))
            StartImport();

        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
    }

    // ════════════════════════════════════════════════════════════
    //  드래그 처리
    // ════════════════════════════════════════════════════════════
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
                        {
                            if (Directory.Exists(path) || File.Exists(path))
                                fullPath = path;
                        }

                        if (Directory.Exists(fullPath)) AddFromPath(fullPath, true);
                        else ProcessFile(fullPath);
                    }
                    _isDragging = false;
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
                            {
                                if (!foundPackages.Any(p => p.SourcePath == path && p.PackagePathInZip == entry.FullName))
                                    foundPackages.Add(new PackageItem(path, entry.FullName, true, entry.FullName));
                            }
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

    // ════════════════════════════════════════════════════════════
    //  임포트 로직
    // ════════════════════════════════════════════════════════════
    private void StartImport()
    {
        var targets = foundPackages.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0) return;

        // 상태 초기화
        foreach (var p in foundPackages) { p.IsDone = false; p.IsFailed = false; }

        isImporting   = true;
        statusMessage = UI_TEXT[9];
        CleanTempFolder();
        importQueue.Clear();

        if (!Directory.Exists(tempExtractPath)) Directory.CreateDirectory(tempExtractPath);

        preImportFolders      = AssetDatabase.GetSubFolders("Assets");
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

                if (!File.Exists(fullPath) && File.Exists(item.SourcePath))
                    fullPath = item.SourcePath;

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

        AssetDatabase.importPackageCompleted  -= OnPackageProcessed;
        AssetDatabase.importPackageFailed     -= OnPackageFailed;
        AssetDatabase.importPackageCancelled  -= OnPackageProcessed;
        AssetDatabase.importPackageCompleted  += OnPackageProcessed;
        AssetDatabase.importPackageFailed     += OnPackageFailed;
        AssetDatabase.importPackageCancelled  += OnPackageProcessed;

        ImportNextPackageInQueue();
    }

    private PackageItem _currentItem;

    private void ImportNextPackageInQueue()
    {
        if (importQueue.Count > 0)
        {
            var (path, item) = importQueue.Dequeue();
            _currentItem = item;
            AssetDatabase.ImportPackage(path, forceInteractive);
        }
        else
        {
            CheckIfAllFinished();
        }
    }

    private void OnPackageProcessed(string name)
    {
        if (_currentItem != null) _currentItem.IsDone = true;
        currentlyProcessedPackages++;
        Repaint();
        ImportNextPackageInQueue();
    }

    private void OnPackageFailed(string name, string err)
    {
        Debug.LogError($"[DiNe] 임포트 실패: {name} ({err})");
        if (_currentItem != null) _currentItem.IsFailed = true;
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

    // ════════════════════════════════════════════════════════════
    //  언어
    // ════════════════════════════════════════════════════════════
    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new[]
                {
                    /*  0 */ "설정",
                    /*  1 */ "정리 폴더",
                    /*  2 */ "소스",
                    /*  3 */ "파일 또는 폴더를 여기에 드래그하세요",
                    /*  4 */ "패키지 목록",
                    /*  5 */ "리스트가 비어 있습니다",
                    /*  6 */ "", /*  7 */ "", /*  8 */ "상태",
                    /*  9 */ "임포트 중",
                    /* 10 */ "임포트 시작",
                    /* 11 */ "✓  모든 작업이 완료됐습니다!",
                    /* 12 */ "",
                    /* 13 */ "패키지 파일을 드래그하여 설치하고, 한 폴더에 정리하세요.",
                    /* 14 */ "임포트 창 강제 표시",
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
                    /*  1 */ "整理フォルダ",
                    /*  2 */ "ソース",
                    /*  3 */ "ファイルまたはフォルダをここにドラッグ",
                    /*  4 */ "パッケージ一覧",
                    /*  5 */ "リストが空です",
                    /*  6 */ "", /*  7 */ "", /*  8 */ "ステータス",
                    /*  9 */ "インポート中",
                    /* 10 */ "インポート開始",
                    /* 11 */ "✓  全て完了しました！",
                    /* 12 */ "",
                    /* 13 */ "パッケージファイルをドラッグしてインストールし、一つのフォルダにまとめます。",
                    /* 14 */ "インポートダイアログを強制表示",
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
                    /*  9 */ "Importing",
                    /* 10 */ "Start Import",
                    /* 11 */ "✓  All done!",
                    /* 12 */ "",
                    /* 13 */ "Drag and drop packages to install and organize them.",
                    /* 14 */ "Force Import Dialog",
                    /* 15 */ ".unitypackage · .zip · folder supported",
                    /* 16 */ "All",
                    /* 17 */ "None",
                    /* 18 */ "Clear",
                    /* 19 */ "No files to import. (Check Console)",
                };
                break;
        }
    }

    // ════════════════════════════════════════════════════════════
    //  커스텀 툴바
    // ════════════════════════════════════════════════════════════
    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? ColMint : new Color(0.25f, 0.25f, 0.25f);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontStyle = i == selected ? FontStyle.Bold : FontStyle.Normal,
                normal    = { textColor = i == selected ? Color.white : ColSub },
                hover     = { textColor = Color.white }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height))) { newSelected = i; GUIUtility.ExitGUI(); }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }
}

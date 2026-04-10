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
        public bool IsSelected = true;
        public bool IsFromZip = false;

        public PackageItem(string sourcePath, string displayName, bool isFromZip, string packagePathInZip = null)
        {
            SourcePath = sourcePath;
            DisplayName = displayName;
            IsFromZip = isFromZip;
            PackagePathInZip = packagePathInZip;
        }
    }

    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private string targetFolderName = "_1_Patch";
    private List<PackageItem> foundPackages = new List<PackageItem>();
    private Vector2 packageScrollPos;

    private string statusMessage = "";
    private bool isImporting = false;
    private bool forceInteractive = false; // 수동 창 띄우기 옵션

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font titleFont;

    private string[] preImportFolders;
    private int totalPackagesToImport = 0;
    private int currentlyProcessedPackages = 0;
    private string pendingTargetFolderName = "_1_Patch";
    
    private Queue<string> importQueue = new Queue<string>();
    
    private static string tempExtractPath = "Temp/DiNePatcher_Extract";

    [MenuItem("DiNe/EX/Package Patcher", false, 100)]
    public static void ShowWindow()
    {
        DiNePackagePatcher window = GetWindow<DiNePackagePatcher>();
        window.titleContent = new GUIContent("Package Patcher");
        window.minSize = new Vector2(400, 680);
        window.Show();
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        SetLanguage(language);
        
        isImporting = false;
        statusMessage = "";
    }

    void OnDisable() { CleanTempFolder(); }

    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // --- 타이틀 바 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
        {
            font      = titleFont,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize  = 36,
            normal    = new GUIStyleState() { textColor = Color.white }
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

        int currentLangIndex = (int)language;
        int newLangIndex = DrawCustomToolbar(currentLangIndex, new string[] { "English", "한국어", "日本語" }, 30);
        if (newLangIndex != currentLangIndex) { language = (LanguagePreset)newLangIndex; SetLanguage(language); }

        // --- 설정 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(UI_TEXT[1], GUILayout.Width(110));
        targetFolderName = EditorGUILayout.TextField(targetFolderName);
        EditorGUILayout.EndHorizontal();
        
        // 에러 방지용 대화창 옵션
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("임포트 창 강제 표시 (에러시 체크)", GUILayout.Width(200));
        forceInteractive = EditorGUILayout.Toggle(forceInteractive);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // --- 드래그 앤 드롭 영역 ---
        Rect dropArea = GUILayoutUtility.GetRect(0f, 90f, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, $"\n\n\n{UI_TEXT[3]}", EditorStyles.helpBox);
        HandleDragAndDrop(dropArea);

        GUILayout.Space(5);

        // --- 패키지 목록 ---
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{UI_TEXT[4]} ({foundPackages.Count})", EditorStyles.boldLabel);
        if (foundPackages.Count > 0)
        {
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50))) { foundPackages.Clear(); statusMessage = ""; GUIUtility.ExitGUI(); }
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40))) { foundPackages.ForEach(p => p.IsSelected = true); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(45))) { foundPackages.ForEach(p => p.IsSelected = false); GUIUtility.ExitGUI(); }
        }
        EditorGUILayout.EndHorizontal();

        packageScrollPos = EditorGUILayout.BeginScrollView(packageScrollPos, GUILayout.Height(250));
        if (foundPackages.Count == 0)
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(UI_TEXT[5], EditorStyles.centeredGreyMiniLabel);
            GUILayout.FlexibleSpace();
        }
        else
        {
            int removeIndex = -1;
            for (int i = 0; i < foundPackages.Count; i++)
            {
                var item = foundPackages[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.textArea);
                item.IsSelected = EditorGUILayout.Toggle(item.IsSelected, GUILayout.Width(20));
                string icon = item.IsFromZip ? "📦 " : "📄 ";
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { wordWrap = false, clipping = TextClipping.Clip };
                if (item.IsFromZip) labelStyle.normal.textColor = new Color(0.4f, 0.8f, 1f);
                GUILayout.Label(icon + item.DisplayName, labelStyle);
                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20))) { removeIndex = i; }
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex != -1) { foundPackages.RemoveAt(removeIndex); GUIUtility.ExitGUI(); }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // --- 하단 실행 영역 ---
        var selectedCount = foundPackages.Count(p => p.IsSelected);
        
        if (isImporting) statusMessage = UI_TEXT[9] + $" ({currentlyProcessedPackages}/{totalPackagesToImport})";
        else if (selectedCount > 0 && foundPackages.Count > 0 && statusMessage.Contains("수 없습니다")) statusMessage = "";

        if (!string.IsNullOrEmpty(statusMessage)) EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

        GUILayout.FlexibleSpace();
        
        EditorGUI.BeginDisabledGroup(selectedCount == 0 || isImporting);
        var prevBg = GUI.backgroundColor;
        if (!isImporting && selectedCount > 0) GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f);
        
        if (GUILayout.Button(isImporting ? "Processing..." : UI_TEXT[10], GUILayout.Height(50)))
        {
            StartImport();
        }
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
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

                        // 유니코드/특수문자 경로: Path.GetFullPath가 실패할 수 있으므로 try-catch
                        string fullPath;
                        try { fullPath = Path.GetFullPath(path); }
                        catch { fullPath = path; }

                        // GetFullPath 결과가 실제로 존재하지 않으면 원본 경로도 시도
                        if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                        {
                            if (Directory.Exists(path) || File.Exists(path))
                                fullPath = path;
                        }

                        if (Directory.Exists(fullPath)) AddFromPath(fullPath, true);
                        else ProcessFile(fullPath);
                    }
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
                    using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Read, Encoding.GetEncoding(cp)))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
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

    private void StartImport()
    {
        var targets = foundPackages.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0) return;

        isImporting = true;
        statusMessage = UI_TEXT[9];
        CleanTempFolder();
        importQueue.Clear();

        if (!Directory.Exists(tempExtractPath)) Directory.CreateDirectory(tempExtractPath);

        preImportFolders = AssetDatabase.GetSubFolders("Assets");
        pendingTargetFolderName = targetFolderName;
        
        foreach (var item in targets)
        {
            // 핵심 수정: 특수문자를 모두 제거한 안전한 임시 파일명 생성 (예: SafeImport_1234abcd.unitypackage)
            string safeFileName = $"SafeImport_{System.Guid.NewGuid().ToString().Substring(0,8)}.unitypackage";
            string safeTempPath = Path.Combine(tempExtractPath, safeFileName);

            if (item.IsFromZip)
            {
                try {
                    using (ZipArchive archive = ZipFile.OpenRead(item.SourcePath)) {
                        ZipArchiveEntry entry = archive.GetEntry(item.PackagePathInZip);
                        if (entry != null) {
                            // 안전한 이름으로 추출
                            entry.ExtractToFile(safeTempPath, true);
                            importQueue.Enqueue(Path.GetFullPath(safeTempPath));
                        }
                    }
                } catch (System.Exception e) { 
                    Debug.LogError($"[DiNe] ZIP 추출 실패: {item.DisplayName}\n{e.Message}"); 
                }
            }
            else
            {
                // 유니코드 경로 대응: GetFullPath 실패 시 원본 경로 사용
                string fullPath;
                try { fullPath = Path.GetFullPath(item.SourcePath); }
                catch { fullPath = item.SourcePath; }

                // File.Exists가 특수문자 경로에서 false를 반환하는 경우, 원본 경로도 시도
                if (!File.Exists(fullPath) && File.Exists(item.SourcePath))
                    fullPath = item.SourcePath;

                if (File.Exists(fullPath))
                {
                    try {
                        // 특수문자 에러를 피하기 위해 원본 파일을 안전한 이름으로 임시 폴더에 복사
                        File.Copy(fullPath, safeTempPath, true);
                        importQueue.Enqueue(Path.GetFullPath(safeTempPath));
                    } catch (System.Exception e) {
                        Debug.LogError($"[DiNe] 안전 복사 실패: {item.DisplayName} | 경로: {fullPath}\n{e.Message}");
                    }
                }
                else
                {
                    Debug.LogError($"[DiNe] 파일을 찾을 수 없습니다: {item.DisplayName} | 경로: {fullPath}");
                }
            }
        }

        totalPackagesToImport = importQueue.Count;
        currentlyProcessedPackages = 0;
        
        if (totalPackagesToImport == 0) 
        { 
            isImporting = false; 
            statusMessage = "임포트할 파일을 찾을 수 없습니다. (콘솔 창 확인)"; 
            return; 
        }

        AssetDatabase.importPackageCompleted -= OnPackageProcessed;
        AssetDatabase.importPackageFailed -= OnPackageFailed;
        AssetDatabase.importPackageCancelled -= OnPackageProcessed;

        AssetDatabase.importPackageCompleted += OnPackageProcessed;
        AssetDatabase.importPackageFailed += OnPackageFailed;
        AssetDatabase.importPackageCancelled += OnPackageProcessed;
        
        ImportNextPackageInQueue();
    }

    private void ImportNextPackageInQueue()
    {
        if (importQueue.Count > 0)
        {
            string nextPath = importQueue.Dequeue();
            // forceInteractive가 true이면 유니티 기본 임포트 창을 띄웁니다.
            AssetDatabase.ImportPackage(nextPath, forceInteractive);
        }
        else
        {
            CheckIfAllFinished();
        }
    }

    private void OnPackageProcessed(string name) 
    { 
        currentlyProcessedPackages++;
        ImportNextPackageInQueue(); 
    }
    
    private void OnPackageFailed(string name, string err) 
    { 
        Debug.LogError($"[DiNe] 임포트 실패: {name} ({err})"); 
        currentlyProcessedPackages++; 
        ImportNextPackageInQueue(); 
    }

    private void CheckIfAllFinished()
    {
        if (currentlyProcessedPackages >= totalPackagesToImport || importQueue.Count == 0)
        {
            AssetDatabase.importPackageCompleted -= OnPackageProcessed;
            AssetDatabase.importPackageFailed -= OnPackageFailed;
            AssetDatabase.importPackageCancelled -= OnPackageProcessed;
            
            MoveNewFolders();
            CleanTempFolder();
            
            isImporting = false;
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
            if (!AssetDatabase.IsValidFolder(targetPath)) AssetDatabase.CreateFolder("Assets", pendingTargetFolderName);
            foreach (string f in added) { if (f == targetPath) continue; AssetDatabase.MoveAsset(f, targetPath + "/" + Path.GetFileName(f)); }
        }
    }

    private static void CleanTempFolder() { try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { } }

    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[] { "설정", "정리 폴더명", "소스", "여기에 파일/폴더를 드래그하세요", "패키지 목록", "리스트가 비어 있습니다.", "", "", "상태", "임포트 중...", "선택 항목 임포트 시작", "모든 작업 완료!", "", "패키지 파일을 드래그하여 설치하고, 한 폴더에 정리하세요." };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new string[] { "設定", "整理フォルダ名", "ソース", "ここにファイル/フォルダをドラッグ", "パッケージ一覧", "リストが空です。", "", "", "ステータス", "インポート中...", "選択項目をインポート開始", "全て完了！", "", "パッケージファイルをドラッグしてインストールし、一つのフォルダにまとめます。" };
                break;
            default:
                UI_TEXT = new string[] { "Settings", "Target Folder", "Source", "Drag files/folders here", "Package List", "List is empty.", "", "", "Status", "Importing...", "Start Import Selected", "All Done!", "", "Drag and drop packages to install and organize them." };
                break;
        }
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.3f, 0.8f, 0.7f) : Color.gray;
            if (GUILayout.Button(options[i], GUILayout.Height(height))) { newSelected = i; GUIUtility.ExitGUI(); }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }
}
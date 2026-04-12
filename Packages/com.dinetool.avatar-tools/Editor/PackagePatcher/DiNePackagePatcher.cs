using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;
using System.Text;

[InitializeOnLoad]
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

        public string CachedTempPath; // set when pre-extracted during ProcessFile (e.g. from Bandizip temp)

        public PackageItem(string sourcePath, string displayName, bool isFromZip, string packagePathInZip = null)
        {
            SourcePath       = sourcePath;
            DisplayName      = displayName;
            IsFromZip        = isFromZip;
            PackagePathInZip = packagePathInZip;
        }
    }

    private enum LanguagePreset { English, Korean, Japanese }

    // ── 도메인 리로드 대응 (스크립트 포함 패키지 임포트 시 리로드 발생) ──────────
    // 임포트 중 도메인 리로드가 일어나면 모든 인스턴스 상태와 콜백이 초기화된다.
    // SessionState에 "정리 예정 작업"을 저장하고, [InitializeOnLoad] 정적 생성자로
    // 리로드 직후 자동 재실행한다.
    private const string SESSION_TARGET = "DiNePatcher_PendingTarget";
    private const string SESSION_ROOTS  = "DiNePatcher_PendingRoots";

    static DiNePackagePatcher()
    {
        EditorApplication.delayCall += TryOrganizeAfterReload;
    }

    private static void SavePendingOrganization(string target, IEnumerable<string> roots)
    {
        SessionState.SetString(SESSION_TARGET, target);
        SessionState.SetString(SESSION_ROOTS,  string.Join("\n", roots));
    }

    private static void ClearPendingOrganization()
    {
        SessionState.EraseString(SESSION_TARGET);
        SessionState.EraseString(SESSION_ROOTS);
    }

    private static void TryOrganizeAfterReload()
    {
        string target   = SessionState.GetString(SESSION_TARGET, "");
        string rootsRaw = SessionState.GetString(SESSION_ROOTS,  "");
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(rootsRaw)) return;

        ClearPendingOrganization();
        var roots = rootsRaw.Split('\n').Where(s => !string.IsNullOrEmpty(s)).ToList();
        Debug.Log($"[DiNe] 도메인 리로드 감지 → 정리 재개: [{string.Join(", ", roots)}] → {target}");
        EditorApplication.delayCall += () => StaticMoveNewFolders(target, roots);
    }

    private static void StaticMoveNewFolders(string targetFolderName, List<string> roots)
    {
        string targetPath = "Assets/" + targetFolderName;
        foreach (var root in roots)
        {
            if (root.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[DiNe] 폴더 없음: {root}");
                continue;
            }
            string dest = targetPath + "/" + Path.GetFileName(root);
            if (AssetDatabase.IsValidFolder(dest)) { Debug.Log($"[DiNe] 이미 정리됨: {dest}"); continue; }
            if (!AssetDatabase.IsValidFolder(targetPath))
                AssetDatabase.CreateFolder("Assets", targetFolderName);
            string err = AssetDatabase.MoveAsset(root, dest);
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning($"[DiNe] 폴더 이동 실패: {root} → {dest}\n{err}");
            else
                Debug.Log($"[DiNe] 폴더 이동 완료: {root} → {dest}");
        }
        if (HasOpenInstances<DiNePackagePatcher>())
        {
            var win = GetWindow<DiNePackagePatcher>(false, null, false);
            if (win != null) { win.isImporting = false; win.statusMessage = "완료!"; win.Repaint(); }
        }
    }
    // ──────────────────────────────────────────────────────────────────────────

    private LanguagePreset language = LanguagePreset.Korean;

    private string targetFolderName = "_1_Patch";
    private List<PackageItem> foundPackages = new List<PackageItem>();
    private Vector2 packageScrollPos;

    private string statusMessage   = "";
    private bool   isImporting     = false;

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;

    private string[]        preImportFolders;
    private HashSet<string> predictedRootFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int    totalPackagesToImport      = 0;
    private int    currentlyProcessedPackages = 0;
    private string pendingTargetFolderName    = "_1_Patch";
    private PackageItem _currentItem;

    private Queue<(string path, PackageItem item)> importQueue = new Queue<(string, PackageItem)>();
    private static string tempExtractPath  = "Temp/DiNePatcher_Extract";
    private static string tempCachePath    = "Temp/DiNePatcher_Cache";

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

        // ── 설정 ── (방해되던 수동 임포트 체크박스 제거)
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(UI_TEXT[1], GUILayout.Width(110));
        targetFolderName = EditorGUILayout.TextField(targetFolderName);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ── 파일 직접 선택 버튼 ──
        EditorGUILayout.BeginHorizontal();
        var prevBgBrowse = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.28f, 0.42f, 0.55f);
        if (GUILayout.Button(UI_TEXT[20], GUILayout.Height(26)))
        {
            string picked = EditorUtility.OpenFilePanel(UI_TEXT[20], "", "unitypackage,zip");
            if (!string.IsNullOrEmpty(picked))
            {
                ProcessFile(picked);
                Repaint();
            }
        }
        GUI.backgroundColor = new Color(0.28f, 0.38f, 0.28f);
        if (GUILayout.Button(UI_TEXT[21], GUILayout.Height(26)))
        {
            string pickedDir = EditorUtility.OpenFolderPanel(UI_TEXT[21], "", "");
            if (!string.IsNullOrEmpty(pickedDir))
            {
                AddFromPath(pickedDir, true);
                Repaint();
            }
        }
        GUI.backgroundColor = prevBgBrowse;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

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

        // ── 패키 목록 ──
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

    private static string GetSafeFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return "_1_Patch";
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string clean = new string(folderName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "_1_Patch" : clean;
    }

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
            List<Encoding> encodings = new List<Encoding> { Encoding.UTF8 };
            try { encodings.Add(Encoding.GetEncoding(932)); } catch { }
            try { encodings.Add(Encoding.GetEncoding(51949)); } catch { }

            foreach (var enc in encodings)
            {
                try
                {
                    using (var archive = ZipFile.Open(path, ZipArchiveMode.Read, enc))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.ToLower().EndsWith(".unitypackage"))
                            {
                                if (!foundPackages.Any(p => p.SourcePath == path && p.PackagePathInZip == entry.FullName))
                                {
                                    // ZIP 원본이 반디집 등 임시 경로에 있을 수 있으므로 즉시 캐시에 추출
                                    string cached = TryCacheZipEntry(entry, path);
                                    var item = new PackageItem(path, entry.FullName, true, entry.FullName);
                                    item.CachedTempPath = cached;
                                    foundPackages.Add(item);
                                }
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
            {
                foundPackages.Add(new PackageItem(path, Path.GetFileName(path), false));
            }
        }
    }

    private void StartImport()
    {
        var targets = foundPackages.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0) return;

        foreach (var p in foundPackages) { p.IsDone = false; p.IsFailed = false; }

        isImporting   = true;
        statusMessage = UI_TEXT[9];
        // 이전 임포트 임시 파일만 정리 (캐시 폴더는 유지 - ProcessFile에서 미리 추출한 파일 보존)
        try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { }
        importQueue.Clear();

        if (!Directory.Exists(tempExtractPath)) Directory.CreateDirectory(tempExtractPath);
        preImportFolders = AssetDatabase.GetSubFolders("Assets");
        predictedRootFolders.Clear();

        pendingTargetFolderName = GetSafeFolderName(targetFolderName);

        foreach (var item in targets)
        {
            string safeFileName = $"SafeImport_{System.Guid.NewGuid().ToString().Substring(0, 8)}.unitypackage";
            string safeTempPath = Path.Combine(tempExtractPath, safeFileName);

            if (item.IsFromZip)
            {
                // 이미 ProcessFile 시점에 캐시된 파일이 있으면 그걸 사용
                if (!string.IsNullOrEmpty(item.CachedTempPath) && File.Exists(item.CachedTempPath))
                {
                    try
                    {
                        File.Copy(item.CachedTempPath, safeTempPath, true);
                        importQueue.Enqueue((Path.GetFullPath(safeTempPath), item));
                        foreach (var r in GetPackageRootFolders(safeTempPath)) predictedRootFolders.Add(r);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DiNe] 캐시 복사 실패: {item.DisplayName}\n{e.Message}");
                        item.IsFailed = true;
                    }
                }
                else
                {
                    // 캐시 없음: 원본 ZIP에서 직접 추출 시도 (원본이 아직 존재하는 경우)
                    try
                    {
                        using (var archive = ZipFile.OpenRead(item.SourcePath))
                        {
                            var entry = archive.GetEntry(item.PackagePathInZip);
                            if (entry != null)
                            {
                                entry.ExtractToFile(safeTempPath, true);
                                importQueue.Enqueue((Path.GetFullPath(safeTempPath), item));
                                foreach (var r in GetPackageRootFolders(safeTempPath)) predictedRootFolders.Add(r);
                            }
                            else
                            {
                                Debug.LogError($"[DiNe] ZIP 내 파일을 찾을 수 없음: {item.PackagePathInZip}");
                                item.IsFailed = true;
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DiNe] ZIP 추출 실패: {item.DisplayName}\n{e.Message}");
                        item.IsFailed = true;
                    }
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
                        foreach (var r in GetPackageRootFolders(safeTempPath)) predictedRootFolders.Add(r);
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

        // 도메인 리로드 대비 SessionState에 저장 (스크립트 포함 패키지 대응)
        SavePendingOrganization(pendingTargetFolderName, predictedRootFolders);
        Debug.Log($"[DiNe] StartImport: {totalPackagesToImport}개 큐, 예측 폴더=[{string.Join(", ", predictedRootFolders)}]");

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
            
            // 핵심 수정: 'false'를 강제 입력하여 임포트 창이 절대 뜨지 않고 백그라운드에서 실행되도록 수정!
            AssetDatabase.ImportPackage(path, false);
        }
        else CheckIfAllFinished();
    }

    private void OnPackageProcessed(string name)
    {
        Debug.Log($"[DiNe] importPackageCompleted: {name}");
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
        Debug.Log($"[DiNe] CheckIfAllFinished: {currentlyProcessedPackages}/{totalPackagesToImport}, queue={importQueue.Count}");
        if (currentlyProcessedPackages >= totalPackagesToImport || importQueue.Count == 0)
        {
            AssetDatabase.importPackageCompleted -= OnPackageProcessed;
            AssetDatabase.importPackageFailed    -= OnPackageFailed;
            AssetDatabase.importPackageCancelled -= OnPackageProcessed;

            // importPackageCompleted 직후에는 AssetDatabase가 아직 새 폴더를 반영하지 않은 경우가 있음
            // 첫 번째 delayCall에서 Refresh → 두 번째 delayCall에서 완전히 갱신된 DB로 폴더 이동
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                EditorApplication.delayCall += () =>
                {
                    MoveNewFolders();
                    CleanTempFolder();
                    isImporting   = false;
                    statusMessage = UI_TEXT[11];
                    Repaint();
                };
            };
        }
    }

    private void MoveNewFolders()
    {
        // 정상 경로(콜백)로 실행됨 → 리로드 후 재실행 방지
        ClearPendingOrganization();
        Debug.Log($"[DiNe] MoveNewFolders: 예측 폴더 수={predictedRootFolders.Count} / {string.Join(", ", predictedRootFolders)}");

        if (predictedRootFolders.Count == 0)
        {
            Debug.LogWarning("[DiNe] MoveNewFolders: 이동 대상 없음 — 패키지 파싱 실패 또는 pathname 엔트리 없음");
            return;
        }

        string targetPath = "Assets/" + pendingTargetFolderName;

        foreach (var root in predictedRootFolders)
        {
            if (root.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) continue;

            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogWarning($"[DiNe] '{root}' 폴더 없음 (임포트 후에도 미생성) → 스킵");
                continue;
            }

            string dest = targetPath + "/" + Path.GetFileName(root);

            // 목적지에 이미 같은 이름 폴더가 있으면 스킵 (이미 정리된 상태)
            if (AssetDatabase.IsValidFolder(dest))
            {
                Debug.Log($"[DiNe] '{dest}' 이미 존재 → 스킵");
                continue;
            }

            if (!AssetDatabase.IsValidFolder(targetPath))
                AssetDatabase.CreateFolder("Assets", pendingTargetFolderName);

            string err = AssetDatabase.MoveAsset(root, dest);
            if (!string.IsNullOrEmpty(err))
                Debug.LogWarning($"[DiNe] 폴더 이동 실패: {root} → {dest}\n{err}");
            else
                Debug.Log($"[DiNe] 폴더 이동 완료: {root} → {dest}");
        }
    }

    /// <summary>
    /// .unitypackage(= tgz) 파일을 파싱해 설치될 Assets 루트 폴더 이름 목록 반환.
    /// 임포트 전에 호출하여 정확한 이동 대상을 미리 파악한다.
    /// </summary>
    private static IEnumerable<string> GetPackageRootFolders(string packagePath)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var fs  = File.OpenRead(packagePath);
            using var gz  = new GZipStream(fs, CompressionMode.Decompress);
            var hdr      = new byte[512];
            var skipBuf  = new byte[4096];

            while (TarReadExact(gz, hdr, 512) == 512)
            {
                // end-of-archive: 연속된 제로 블록
                bool allZero = true;
                for (int i = 0; i < 8; i++) if (hdr[i] != 0) { allZero = false; break; }
                if (allZero) break;

                string entryName = Encoding.UTF8.GetString(hdr, 0, 100).TrimEnd('\0');
                string sizeOctal = Encoding.ASCII.GetString(hdr, 124, 12).TrimEnd('\0').Trim();
                long   size      = 0;
                if (!string.IsNullOrEmpty(sizeOctal))
                    try { size = Convert.ToInt64(sizeOctal, 8); } catch { }
                long paddedSize = (size + 511L) / 512 * 512;

                // GUID/pathname 엔트리에서 에셋 경로 읽기
                if (entryName.EndsWith("/pathname") || entryName.EndsWith("\\pathname"))
                {
                    var content = new byte[(int)size];
                    TarReadExact(gz, content, (int)size);
                    string assetPath = Encoding.UTF8.GetString(content).Trim('\0', '\r', '\n', ' ');
                    if (assetPath.StartsWith("Assets/"))
                    {
                        var rel  = assetPath.Substring("Assets/".Length);
                        var idx  = rel.IndexOf('/');
                        var root = idx >= 0 ? rel.Substring(0, idx) : rel;
                        if (!string.IsNullOrEmpty(root)) roots.Add("Assets/" + root);
                    }
                    long pad = paddedSize - size;
                    if (pad > 0) TarSkipBytes(gz, (int)pad, skipBuf);
                }
                else
                {
                    TarSkipBytes(gz, (int)paddedSize, skipBuf);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiNe] 패키지 파싱 실패 (자동 정리 불가): {Path.GetFileName(packagePath)}\n{e.Message}");
        }
        return roots;
    }

    private static int TarReadExact(Stream s, byte[] buf, int count)
    {
        int total = 0;
        while (total < count)
        {
            int r = s.Read(buf, total, count - total);
            if (r <= 0) break;
            total += r;
        }
        return total;
    }

    private static void TarSkipBytes(Stream s, int count, byte[] tmp)
    {
        int rem = count;
        while (rem > 0)
        {
            int r = s.Read(tmp, 0, Math.Min(rem, tmp.Length));
            if (r <= 0) break;
            rem -= r;
        }
    }

    /// <summary>
    /// ZIP 항목을 즉시 캐시 폴더에 추출. 반디집 등 임시 경로 소멸 대비.
    /// 실패 시 null 반환 (원본 ZIP 재시도 fallback).
    /// </summary>
    private static string TryCacheZipEntry(System.IO.Compression.ZipArchiveEntry entry, string sourceZipPath)
    {
        try
        {
            if (!Directory.Exists(tempCachePath)) Directory.CreateDirectory(tempCachePath);
            string safeFileName = $"Cache_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}.unitypackage";
            string cachePath = Path.Combine(tempCachePath, safeFileName);
            entry.ExtractToFile(cachePath, true);
            return cachePath;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DiNe] ZIP 캐시 추출 실패 (임포트 시 재시도): {Path.GetFileName(sourceZipPath)}\n{e.Message}");
            return null;
        }
    }

    private static void CleanTempFolder()
    {
        try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { }
        try { if (Directory.Exists(tempCachePath))   Directory.Delete(tempCachePath,   true); } catch { }
    }

    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new[]
                {
                    /* 0 */ "설정",
                    /* 1 */ "정리 폴더명",
                    /* 2 */ "소스",
                    /* 3 */ "파일 또는 폴더를 여기에 드래그하세요",
                    /* 4 */ "패키지 목록",
                    /* 5 */ "리스트가 비어 있습니다",
                    /* 6 */ "", /* 7 */ "", /* 8 */ "상태",
                    /* 9 */ "임포트 중...",
                    /* 10 */ "선택 항목 임포트 시작",
                    /* 11 */ "모든 작업 완료!",
                    /* 12 */ "",
                    /* 13 */ "패키지 파일을 드래그하여 설치하고, 한 폴더에 정리하세요.",
                    /* 14 */ "임포트 창 강제 표시 (에러 시 체크)", // UI에선 지웠지만 배열 인덱스 유지를 위해 남겨둠
                    /* 15 */ ".unitypackage · .zip · 폴더 지원",
                    /* 16 */ "전체",
                    /* 17 */ "없음",
                    /* 18 */ "Clear",
                    /* 19 */ "임포트할 파일을 찾을 수 없습니다. (콘솔 창 확인)",
                    /* 20 */ "📄  파일 직접 선택",
                    /* 21 */ "📁  폴더 직접 선택",
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new[]
                {
                    /* 0 */ "設定",
                    /* 1 */ "整理フォルダ名",
                    /* 2 */ "ソース",
                    /* 3 */ "ファイルまたはフォルダをここにドラッグ",
                    /* 4 */ "パッケージ一覧",
                    /* 5 */ "リストが空です",
                    /* 6 */ "", /* 7 */ "", /* 8 */ "ステータス",
                    /* 9 */ "インポート中...",
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
                    /* 20 */ "📄  ファイルを直接選択",
                    /* 21 */ "📁  フォルダを直接選択",
                };
                break;
            default:
                UI_TEXT = new[]
                {
                    /* 0 */ "Settings",
                    /* 1 */ "Target Folder",
                    /* 2 */ "Source",
                    /* 3 */ "Drag files or folders here",
                    /* 4 */ "Package List",
                    /* 5 */ "List is empty",
                    /* 6 */ "", /* 7 */ "", /* 8 */ "Status",
                    /* 9 */ "Importing...",
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
                    /* 20 */ "📄  Browse File",
                    /* 21 */ "📁  Browse Folder",
                };
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
            GUI.backgroundColor = (i == selected) ? ColMint : Color.gray;
            if (GUILayout.Button(options[i], GUILayout.Height(height))) newSelected = i;
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }
}
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
        public string Id = Guid.NewGuid().ToString("N");
        public string SourcePath;
        public string PackagePathInZip;
        public string DisplayName;
        public bool   IsSelected = true;
        public bool   IsFromZip  = false;
        public bool   IsDone     = false;
        public bool   IsFailed   = false;

        public string CachedTempPath; // set when pre-extracted during ProcessFile (e.g. from Bandizip temp)
        public string ArchiveLabel = "ZIP"; // 목록 뱃지에 쓰는 압축 종류 (ZIP / RAR / 7Z)
        // rar·7z 또는 압축 안의 압축: 원본 ZIP 을 바로 열어 꺼낼 수 없는 항목.
        // 캐시가 사라지면 원본을 다시 탐색(DiscoverArchive)해서 복구한다.
        public bool   IsExternalArchive = false;
        public string ContainerChain; // 중첩 압축일 때 거쳐 온 안쪽 압축 파일명 ("inner.zip › deep.rar")

        public PackageItem(string sourcePath, string displayName, bool isFromZip, string packagePathInZip = null)
        {
            SourcePath       = sourcePath;
            DisplayName      = displayName;
            IsFromZip        = isFromZip;
            PackagePathInZip = packagePathInZip;
        }
    }

    private enum LanguagePreset { English, Korean, Japanese }

    // ══════════════════════════════════════════════════════════════════════════
    //  임포트 드라이버 (도메인 리로드 내성)
    //
    //  스크립트/셰이더가 든 패키지는 임포트 도중 어셈블리 리로드를 일으킨다.
    //  리로드는 EditorWindow 의 OnDisable 을 호출하고 인스턴스 필드·delayCall·
    //  콜백 구독을 전부 날려버리므로, 진행 상태를 창이 아니라 SessionState 에
    //  두고 [InitializeOnLoad] 정적 드라이버가 이어서 굴린다.
    //  창을 닫아도(OnDestroy) 배치는 그대로 끝까지 진행된다.
    // ══════════════════════════════════════════════════════════════════════════

    private const int PHASE_IMPORT   = 0;
    private const int PHASE_REFRESH  = 1;
    private const int PHASE_ORGANIZE = 2;

    [System.Serializable]
    private class PendingState
    {
        public string       target  = "_1_Patch";
        public List<string> roots   = new List<string>(); // 이동 대상 후보 (Assets/xxx)
        public List<string> before  = new List<string>(); // 임포트 전 Assets 최상위 목록
        public List<string> queue   = new List<string>(); // 남은 임시 패키지 경로
        public List<string> queueId = new List<string>(); // queue 와 1:1 대응하는 PackageItem.Id
        public string inFlight;      // 현재 임포트 중인 임시 경로 (없으면 빈 값)
        public string inFlightId;
        public int    total;
        public int    done;
        public int    phase = PHASE_IMPORT;
    }

    private const string SESSION_STATE = "DiNePatcher_State";

    private static bool   s_driverRunning;
    private static bool   s_callbacksHooked;
    private static double s_settleUntil;
    private static DiNePackagePatcher s_window;

    static DiNePackagePatcher()
    {
        // 리로드 직후에도 남은 작업이 있으면 창 없이 자동으로 이어서 진행한다.
        EditorApplication.delayCall += ResumePendingBatch;
    }

    private static PendingState LoadState()
    {
        string raw = SessionState.GetString(SESSION_STATE, "");
        if (string.IsNullOrEmpty(raw)) return null;
        try { return JsonUtility.FromJson<PendingState>(raw); }
        catch { SessionState.EraseString(SESSION_STATE); return null; }
    }

    private static void SaveState(PendingState st)
    {
        SessionState.SetString(SESSION_STATE, JsonUtility.ToJson(st));
    }

    private static void ClearState()
    {
        SessionState.EraseString(SESSION_STATE);
    }

    private static bool HasPendingBatch() => !string.IsNullOrEmpty(SessionState.GetString(SESSION_STATE, ""));

    private static void ResumePendingBatch()
    {
        var st = LoadState();
        if (st == null) return;

        if (!string.IsNullOrEmpty(st.inFlight))
        {
            // 리로드 시점에 임포트 중이던 항목. ImportPackage 는 리로드보다 먼저
            // 파일 기록을 끝내므로 성공으로 간주하고 다음으로 넘어간다.
            // 여기서 배치를 포기하면 남은 패키지가 통째로 누락된다.
            Debug.Log($"[DiNe] 도메인 리로드 감지 → 임포트 이어서 진행 ({st.done + 1}/{st.total})");
            MarkItem(st.inFlightId, true);
            st.inFlight   = "";
            st.inFlightId = "";
            st.done++;
            SaveState(st);
        }

        StartDriver();
    }

    private static void StartDriver()
    {
        if (s_driverRunning) return;
        s_driverRunning = true;
        s_settleUntil   = EditorApplication.timeSinceStartup + 0.25;
        HookImportCallbacks();
        EditorApplication.update += Drive;
    }

    private static void StopDriver()
    {
        if (!s_driverRunning) return;
        s_driverRunning = false;
        EditorApplication.update -= Drive;
        UnhookImportCallbacks();
    }

    private static void HookImportCallbacks()
    {
        if (s_callbacksHooked) return;
        s_callbacksHooked = true;
        AssetDatabase.importPackageCompleted += OnPackageCompleted;
        AssetDatabase.importPackageFailed    += OnPackageFailed;
        AssetDatabase.importPackageCancelled += OnPackageCancelled;
    }

    private static void UnhookImportCallbacks()
    {
        if (!s_callbacksHooked) return;
        s_callbacksHooked = false;
        AssetDatabase.importPackageCompleted -= OnPackageCompleted;
        AssetDatabase.importPackageFailed    -= OnPackageFailed;
        AssetDatabase.importPackageCancelled -= OnPackageCancelled;
    }

    /// <summary>
    /// 에디터가 에셋 임포트·컴파일로 바쁜 동안에는 다음 ImportPackage 를 걸지 않는다.
    /// 파일이 많은 패키지는 importPackageCompleted 이후에도 AssetDatabase 작업이
    /// 남아 있고, 그 위에 다음 임포트를 얹으면 임포트가 중간에 끊긴다.
    /// </summary>
    private static bool EditorIsBusy()
        => EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode;

    private static void Drive()
    {
        var st = LoadState();
        if (st == null) { StopDriver(); return; }

        if (EditorIsBusy())
        {
            // 바쁜 동안에는 대기 시각을 계속 뒤로 민다 → 완전히 한가해진 뒤 진행.
            s_settleUntil = EditorApplication.timeSinceStartup + 0.4;
            return;
        }
        if (EditorApplication.timeSinceStartup < s_settleUntil) return;
        if (!string.IsNullOrEmpty(st.inFlight)) return; // 콜백 대기 중

        switch (st.phase)
        {
            case PHASE_IMPORT:
                if (st.queue.Count == 0)
                {
                    st.phase = PHASE_REFRESH;
                    SaveState(st);
                    return;
                }
                ImportNext(st);
                return;

            case PHASE_REFRESH:
                st.phase = PHASE_ORGANIZE;
                SaveState(st);
                s_settleUntil = EditorApplication.timeSinceStartup + 0.5;
                try { AssetDatabase.Refresh(); }
                catch (Exception e) { Debug.LogError($"[DiNe] 임포트 후 새로고침 실패\n{e}"); }
                return;

            case PHASE_ORGANIZE:
                RunOrganize(st);
                return;
        }
    }

    private static void ImportNext(PendingState st)
    {
        string path = st.queue[0];
        string id   = st.queueId.Count > 0 ? st.queueId[0] : "";
        st.queue.RemoveAt(0);
        if (st.queueId.Count > 0) st.queueId.RemoveAt(0);

        st.inFlight   = path;
        st.inFlightId = id;
        SaveState(st);
        RepaintWindow();

        try
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("임포트할 임시 패키지 파일이 없습니다.", path);

            AssetDatabase.ImportPackage(path, false);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] 임포트 시작 실패: {Path.GetFileName(path)}\n{e}");
            FinishInFlight(false);
        }
    }

    private static void OnPackageCompleted(string name)
    {
        Debug.Log($"[DiNe] importPackageCompleted: {name}");
        FinishInFlight(true);
    }

    private static void OnPackageFailed(string name, string err)
    {
        Debug.LogError($"[DiNe] 임포트 실패: {name} ({err})");
        FinishInFlight(false);
    }

    private static void OnPackageCancelled(string name)
    {
        Debug.LogWarning($"[DiNe] 임포트 취소됨: {name}");
        FinishInFlight(false);
    }

    private static void FinishInFlight(bool succeeded)
    {
        var st = LoadState();
        if (st == null || string.IsNullOrEmpty(st.inFlight)) return;

        MarkItem(st.inFlightId, succeeded);
        st.inFlight   = "";
        st.inFlightId = "";
        st.done++;
        SaveState(st);

        // 임포트 직후에는 에셋 처리가 계속되므로 최소 대기 시간을 준다.
        s_settleUntil = EditorApplication.timeSinceStartup + 0.4;
        StartDriver();
        RepaintWindow();
    }

    private static void RunOrganize(PendingState st)
    {
        // 스크립트를 옮기면 곧바로 컴파일/리로드가 걸린다. 정리 작업을 다시 타지
        // 않도록 상태를 먼저 지운다.
        ClearState();
        StopDriver();

        var roots = new HashSet<string>(st.roots, StringComparer.OrdinalIgnoreCase);

        // tar 파싱이 완전히 실패한 경우에만 임포트 전후 스냅샷을 안전망으로 쓴다.
        // 정상 파싱된 패키지를 처리하는 동안 사용자가 만든 무관한 에셋까지 이동시키지 않는다.
        if (roots.Count == 0)
        {
            foreach (var added in FindNewTopLevelAssets(st.before, st.target))
                roots.Add(added);
        }

        var organizedFolders = new List<string>();
        try
        {
            foreach (var root in roots)
            {
                try
                {
                    string organized = OrganizeRootFolder(root, st.target);
                    if (!string.IsNullOrEmpty(organized)) organizedFolders.Add(organized);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DiNe] '{root}' 정리 실패\n{e}");
                }
            }

            BadgeFolders(organizedFolders);
        }
        finally
        {
            CleanTempFolder();
            SetWindowStatusDone(st);
        }
    }

    /// <summary>임포트 전 스냅샷에 없던 Assets 최상위 폴더/파일을 찾는다.</summary>
    private static IEnumerable<string> FindNewTopLevelAssets(List<string> before, string targetFolderName)
    {
        var known  = new HashSet<string>(before ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        string tgt = "Assets/" + targetFolderName;

        foreach (var path in EnumerateTopLevelAssets())
        {
            if (known.Contains(path)) continue;
            if (path.Equals(tgt, StringComparison.OrdinalIgnoreCase)) continue;
            Debug.Log($"[DiNe] 패키지 목록에 없던 새 항목 발견 → 함께 정리: {path}");
            yield return path;
        }
    }

    private static List<string> EnumerateTopLevelAssets()
    {
        var list = new List<string>();
        try
        {
            list.AddRange(AssetDatabase.GetSubFolders("Assets"));
            foreach (var f in Directory.GetFiles("Assets"))
            {
                if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(f.Replace('\\', '/'));
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiNe] Assets 최상위 스캔 실패: {e.Message}");
        }
        return list;
    }

    private static void MarkItem(string id, bool succeeded)
    {
        if (s_window == null || string.IsNullOrEmpty(id)) return;
        var item = s_window.foundPackages.FirstOrDefault(p => p.Id == id);
        if (item == null) return;
        item.IsDone   = succeeded;
        item.IsFailed = !succeeded;
    }

    private static void RepaintWindow()
    {
        if (s_window != null) s_window.Repaint();
    }

    private static void SetWindowStatusDone(PendingState st)
    {
        if (s_window == null) return;
        s_window.statusMessage = s_window.UI_TEXT != null ? s_window.UI_TEXT[11] : "완료!";
        s_window.Repaint();
    }
    // ══════════════════════════════════════════════════════════════════════════

    private LanguagePreset language = LanguagePreset.Korean;

    private string targetFolderName = "_1_Patch";
    private List<PackageItem> foundPackages = new List<PackageItem>();
    private Vector2 packageScrollPos;

    private string statusMessage   = "";

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;

    private static string tempExtractPath  = "Temp/DiNePatcher_Extract";
    private static string tempCachePath    = "Temp/DiNePatcher_Cache";
    private bool reloadAssembliesLocked = false;

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
        s_window   = this;
        windowIcon = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe.png");
        tabIcon    = DiNePackageAssets.LoadAsset<Texture2D>("Assets/DiNe_Icon.png");
        titleFont  = DiNePackageAssets.LoadAsset<Font>("DungGeunMo.ttf");
        SetLanguage(language);
        if (!HasPendingBatch()) statusMessage = "";
    }

    void OnDisable()
    {
        // OnDisable 은 창을 닫을 때뿐 아니라 어셈블리 리로드 때도 호출된다.
        // 여기서 큐를 버리거나 임시 파일을 지우면 스크립트가 든 패키지를
        // 임포트할 때마다 배치가 중간에 끊긴다. 정리는 OnDestroy 에서만 한다.
        if (s_window == this) s_window = null;
        ReleaseAssemblyReloadLock();
    }

    void OnDestroy()
    {
        ReleaseAssemblyReloadLock();

        // 배치가 아직 진행 중이면 정적 드라이버가 계속 끝까지 처리한다.
        // 임시 패키지 파일은 드라이버가 끝난 뒤에 지운다.
        if (HasPendingBatch())
        {
            Debug.Log("[DiNe] 창을 닫았지만 남은 패키지 임포트는 백그라운드에서 계속 진행됩니다.");
            return;
        }

        CleanTempFolder();
    }

    void OnGUI()
    {
        bool isImporting = HasPendingBatch();
        var  st          = isImporting ? LoadState() : null;
        int  doneCount   = st?.done  ?? 0;
        int  totalCount  = st?.total ?? 0;

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
            string picked = EditorUtility.OpenFilePanel(UI_TEXT[20], "", "unitypackage,zip,rar,7z");
            if (!string.IsNullOrEmpty(picked))
                QueuePaths(new[] { picked });
        }
        GUI.backgroundColor = new Color(0.28f, 0.38f, 0.28f);
        if (GUILayout.Button(UI_TEXT[21], GUILayout.Height(26)))
        {
            string pickedDir = EditorUtility.OpenFolderPanel(UI_TEXT[21], "", "");
            if (!string.IsNullOrEmpty(pickedDir))
                QueuePaths(new[] { pickedDir });
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
            string inFlightId = st?.inFlightId ?? "";
            int removeIndex = -1;
            for (int i = 0; i < foundPackages.Count; i++)
            {
                var item = foundPackages[i];
                var rowBg = item.IsSelected ? new Color(0.18f, 0.28f, 0.26f) : new Color(0.20f, 0.20f, 0.20f);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = rowBg;
                EditorGUILayout.BeginHorizontal("box");
                GUI.backgroundColor = prevBg;

                // 체크박스 자리만 비워 두고, 칸이 다 그려진 뒤 칸 높이 전체를 클릭 영역으로 쓴다.
                GUILayout.Space(DiNePackageSelectWindow.ToggleColumnWidth);
                EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();

                // 타입 뱃지
                string badgeText  = item.IsDone ? "✓" : item.IsFailed ? "✗" : item.IsFromZip ? item.ArchiveLabel : "PKG";
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
                if (isImporting && item.Id == inFlightId)
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
                    GUILayout.Label($"  ↳  {AppendChain(Path.GetFileName(item.SourcePath), item.ContainerChain)}",
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.40f, 0.55f, 0.70f) } });

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                item.IsSelected = DiNePackageSelectWindow.RowToggle(GUILayoutUtility.GetLastRect(), item.IsSelected);
                GUILayout.Space(1);
            }
            if (removeIndex != -1 && !isImporting) { foundPackages.RemoveAt(removeIndex); Repaint(); }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(3);

        // ── 진행 바 ──
        if (isImporting && totalCount > 0)
        {
            Rect barBg = GUILayoutUtility.GetRect(0f, 5f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barBg, new Color(0.15f, 0.15f, 0.15f));
            float ratio = (float)doneCount / totalCount;
            EditorGUI.DrawRect(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height), ColMint);
            GUILayout.Space(3);
        }

        // ── 상태 메시지 ──
        if (isImporting)
            statusMessage = $"{UI_TEXT[9]}  ({doneCount} / {totalCount})";

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

        GUILayout.FlexibleSpace();

        // ── 임포트 버튼 ──
        EditorGUI.BeginDisabledGroup(selCount == 0 || isImporting);
        var prevBgBtn = GUI.backgroundColor;
        GUI.backgroundColor = (!isImporting && selCount > 0) ? ColMint : Color.gray;
        string btnLabel = isImporting
            ? $"⏳  {doneCount} / {totalCount}"
            : $"{UI_TEXT[10]}  ({selCount})";
        if (GUILayout.Button(btnLabel, new GUIStyle(GUI.skin.button)
            { fontSize = 13, fontStyle = FontStyle.Bold,
              normal = { textColor = Color.white }, hover = { textColor = Color.white } },
            GUILayout.Height(46)))
            StartImport();
        GUI.backgroundColor = prevBgBtn;
        EditorGUI.EndDisabledGroup();

        // 백그라운드 진행 중에는 창이 스스로 갱신되도록 한다.
        if (isImporting) Repaint();
    }

    // ════════════════════════════════════════════════════════════

    private static string GetSafeFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return "_1_Patch";
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string clean = new string(folderName
            .Where(c => !invalidChars.Contains(c) && c != '/' && c != '\\' && c != ':')
            .ToArray())
            .Trim()
            .TrimEnd('.');

        if (string.IsNullOrWhiteSpace(clean) || clean == "." || clean == "..")
            return "_1_Patch";

        // Windows 예약 장치명은 확장자가 붙어도 폴더로 만들 수 없다.
        string stem = clean.Split('.')[0];
        string[] reservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };
        if (reservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            clean = "_" + clean;

        return clean;
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
                    QueuePaths(DragAndDrop.paths);
                }
                evt.Use();
                break;
        }
    }

    private readonly List<string> pendingPaths = new List<string>();
    private bool processingPaths;

    /// <summary>
    /// 경로 처리를 OnGUI 밖(delayCall)으로 미룬다. 압축 안에 패키지가 여러 개면
    /// 모달 선택 창이 뜨는데, OnGUI 도중에 모달을 열면 GUI 레이아웃 스택이 깨진다.
    /// </summary>
    private void QueuePaths(IEnumerable<string> paths)
    {
        var list = (paths ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (list.Count == 0) return;

        pendingPaths.AddRange(list);
        EditorApplication.delayCall += DrainPendingPaths;
    }

    /// <summary>
    /// 쌓인 경로를 한 묶음으로 처리한다. 먼저 모든 압축을 끝까지 탐색해 두고,
    /// 그 다음에 선택 창을 하나씩 차례로 띄운다 → 남은 창 개수를 미리 알 수 있다.
    /// </summary>
    private void DrainPendingPaths()
    {
        if (this == null || processingPaths) return;
        processingPaths = true;
        try
        {
            while (pendingPaths.Count > 0)
            {
                var batch = pendingPaths.ToList();
                pendingPaths.Clear();

                // 1) 폴더를 펼쳐 실제 파일 목록으로
                var files = new List<string>();
                foreach (string path in batch)
                {
                    string fullPath;
                    try { fullPath = Path.GetFullPath(path); }
                    catch { fullPath = path; }

                    if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                        if (Directory.Exists(path) || File.Exists(path))
                            fullPath = path;

                    if (Directory.Exists(fullPath))
                    {
                        try
                        {
                            files.AddRange(Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories)
                                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower())));
                        }
                        catch (Exception e) { Debug.LogWarning($"[DiNe] 폴더 스캔 실패: {fullPath}\n{e.Message}"); }
                    }
                    else files.Add(fullPath);
                }
                files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // 2) 단일 패키지는 바로 넣고, 압축은 전부 탐색만 해 둔다
                var archives = new List<KeyValuePair<string, List<FoundPackage>>>();
                foreach (string file in files)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (DiNeArchiveTools.IsArchive(ext))
                    {
                        var fresh = DiscoverFresh(file);
                        if (fresh != null && fresh.Count > 0)
                            archives.Add(new KeyValuePair<string, List<FoundPackage>>(file, fresh));
                    }
                    else if (ext == ".unitypackage")
                    {
                        if (!foundPackages.Any(p => p.SourcePath == file && !p.IsFromZip))
                            foundPackages.Add(new PackageItem(file, Path.GetFileName(file), false));
                    }
                }
                Repaint();

                // 3) 여러 개가 든 압축은 전부 넣기 전에 선택 창을 하나씩 차례로 띄운다
                int dialogTotal = archives.Count(a => a.Value.Count > 1);
                int dialogIndex = 0;
                foreach (var a in archives)
                {
                    var chosen = a.Value;
                    if (chosen.Count > 1)
                    {
                        dialogIndex++;
                        chosen = AskSelection(a.Key, a.Value, dialogIndex, dialogTotal);
                    }
                    CommitArchive(a.Key, chosen);
                    Repaint();
                }
            }
        }
        finally
        {
            processingPaths = false;
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    private static readonly HashSet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".unitypackage", ".zip", ".rar", ".7z" };

    // ── 압축 파일 탐색 (압축 안의 압축까지 재귀) ─────────────────────────────

    private const int MaxNestDepth = 3; // 압축 안의 압축을 몇 겹까지 따라 들어갈지

    /// <summary>압축 파일 안에서 찾은 .unitypackage 하나.</summary>
    private class FoundPackage
    {
        public string InnerPath;      // 최상위 압축 기준 경로. 중첩이면 "inner.zip/sub/a.unitypackage"
        public string ContainerChain; // 거쳐 온 안쪽 압축 파일명 (표시용). 최상위면 빈 값
        public string CachedPath;     // 외부 도구가 이미 디스크에 풀어 둔 파일
        public string ZipPath;        // 아직 안 풀었을 때: 이 항목을 담고 있는 ZIP (디스크 상 경로)
        public string ZipEntry;
        public int    ZipCodePage;
        public bool   IsDirectZipEntry; // 최상위 ZIP 의 직속 항목 → 캐시가 없어도 원본에서 바로 꺼낼 수 있음
    }

    private static string ArchiveLabelOf(string path)
    {
        switch (Path.GetExtension(path).ToLower())
        {
            case ".rar": return "RAR";
            case ".7z":  return "7Z";
            default:     return "ZIP";
        }
    }

    /// <summary>압축을 끝까지 탐색해, 아직 목록에 없는 패키지만 돌려준다. 없으면 null.</summary>
    private List<FoundPackage> DiscoverFresh(string path)
    {
        var results = new List<FoundPackage>();
        var errors  = new List<string>();
        try { DiscoverArchive(path, "", "", 0, results, errors); }
        finally { EditorUtility.ClearProgressBar(); }

        if (results.Count == 0)
        {
            statusMessage = (errors.Count > 0 && !DiNeArchiveTools.HasAnyTool()) ? UI_TEXT[22] : UI_TEXT[23];
            Debug.LogError($"[DiNe] 압축 파일 안에서 .unitypackage 를 찾지 못했습니다: {Path.GetFileName(path)}" +
                           (errors.Count > 0 ? "\n" + string.Join("\n", errors) : ""));
            return null;
        }
        if (errors.Count > 0)
            Debug.LogWarning($"[DiNe] {Path.GetFileName(path)} 의 일부 압축은 열지 못했습니다.\n" + string.Join("\n", errors));

        // 이미 목록에 있는 항목은 다시 넣지 않는다.
        var fresh = results
            .Where(r => !foundPackages.Any(p => p.IsFromZip && p.SourcePath == path && p.PackagePathInZip == r.InnerPath))
            .OrderBy(r => r.InnerPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var dup in results.Except(fresh)) DeleteCachedFile(dup);
        return fresh;
    }

    /// <summary>선택 창을 띄워 고른 항목만 돌려준다. index/total 은 이번 묶음에서 몇 번째 창인지.</summary>
    private List<FoundPackage> AskSelection(string path, List<FoundPackage> fresh, int index, int total)
    {
        var flags = fresh.Select(_ => true).ToArray();
        string title    = total > 1 ? $"{UI_TEXT[24]}  ({index} / {total})" : UI_TEXT[24];
        string progress = total > 1 ? string.Format(UI_TEXT[28], index, total, total - index) : null;

        bool ok = DiNePackageSelectWindow.Show(
            title,
            string.Format(UI_TEXT[25], Path.GetFileName(path), fresh.Count),
            progress,
            fresh.Select(r => Path.GetFileName(r.InnerPath)).ToArray(),
            fresh.Select(r => r.InnerPath).ToArray(),
            flags, UI_TEXT[16], UI_TEXT[17], UI_TEXT[26], UI_TEXT[27]);

        var chosen = ok ? fresh.Where((r, i) => flags[i]).ToList() : new List<FoundPackage>();
        foreach (var skipped in fresh.Except(chosen)) DeleteCachedFile(skipped);
        return chosen;
    }

    private void CommitArchive(string path, List<FoundPackage> chosen)
    {
        string label = ArchiveLabelOf(path);
        try
        {
            for (int i = 0; i < chosen.Count; i++)
            {
                var found = chosen[i];
                // 대용량 ZIP 은 추출에 시간이 걸리므로 진행률을 보여준다.
                EditorUtility.DisplayProgressBar("Package Patcher",
                    $"{Path.GetFileName(path)} → {Path.GetFileName(found.InnerPath)}", (float)i / chosen.Count);

                // ZIP 원본이 반디집 등 임시 경로에 있을 수 있으므로 즉시 캐시에 추출
                string cached = MaterializeFound(found);
                if (cached == null && !found.IsDirectZipEntry)
                {
                    Debug.LogError($"[DiNe] 압축 해제 실패: {found.InnerPath}");
                    continue;
                }

                foundPackages.Add(new PackageItem(path, found.InnerPath, true, found.InnerPath)
                {
                    CachedTempPath    = cached,
                    ArchiveLabel      = label,
                    IsExternalArchive = !found.IsDirectZipEntry,
                    ContainerChain    = found.ContainerChain,
                });
            }
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void DeleteCachedFile(FoundPackage found)
    {
        if (string.IsNullOrEmpty(found.CachedPath)) return;
        try { if (File.Exists(found.CachedPath)) File.Delete(found.CachedPath); } catch { }
    }

    /// <summary>찾은 항목을 디스크 상의 .unitypackage 로 만든다. 실패 시 null.</summary>
    private static string MaterializeFound(FoundPackage found)
    {
        if (!string.IsNullOrEmpty(found.CachedPath) && File.Exists(found.CachedPath)) return found.CachedPath;
        if (string.IsNullOrEmpty(found.ZipPath)) return null;

        try
        {
            // 항목 이름은 탐색 때 쓴 인코딩으로 열어야 같은 문자열로 나온다.
            using (var archive = ZipFile.Open(found.ZipPath, ZipArchiveMode.Read, Encoding.GetEncoding(found.ZipCodePage)))
            {
                var entry = archive.Entries.FirstOrDefault(e => e.FullName == found.ZipEntry);
                if (entry == null) return null;
                found.CachedPath = TryCacheZipEntry(entry, found.ZipPath);
                return found.CachedPath;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiNe] ZIP 캐시 추출 실패: {Path.GetFileName(found.ZipPath)}\n{e.Message}");
            return null;
        }
    }

    /// <summary>
    /// archivePath 안의 .unitypackage 를 results 에 모은다. 안쪽에 또 압축 파일이
    /// 있으면 캐시 폴더에 풀어서 MaxNestDepth 겹까지 따라 들어간다.
    /// </summary>
    private void DiscoverArchive(string archivePath, string pathPrefix, string chain, int depth,
                                 List<FoundPackage> results, List<string> errors)
    {
        string ext = Path.GetExtension(archivePath).ToLower();
        if (!DiNeArchiveTools.NeedsExternalTool(ext) && DiscoverZip(archivePath, pathPrefix, chain, depth, results, errors))
            return;

        // rar·7z, 그리고 .NET 이 못 여는 ZIP(특수 압축 방식 등)은 외부 도구로 연다.
        DiscoverExternal(archivePath, pathPrefix, chain, depth, results, errors);
    }

    private bool DiscoverZip(string zipPath, string pathPrefix, string chain, int depth,
                             List<FoundPackage> results, List<string> errors)
    {
        List<Encoding> encodings = new List<Encoding> { Encoding.UTF8 };
        try { encodings.Add(Encoding.GetEncoding(932)); } catch { }
        try { encodings.Add(Encoding.GetEncoding(51949)); } catch { }

        foreach (var enc in encodings)
        {
            int resultMark = results.Count;
            var nested = new List<KeyValuePair<string, string>>(); // (풀어 둔 파일, ZIP 내 경로)
            try
            {
                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Read, enc))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // 폴더 항목
                        string entryExt = Path.GetExtension(entry.Name).ToLower();

                        if (entryExt == ".unitypackage")
                        {
                            results.Add(new FoundPackage
                            {
                                InnerPath        = pathPrefix + entry.FullName,
                                ContainerChain   = chain,
                                ZipPath          = zipPath,
                                ZipEntry         = entry.FullName,
                                ZipCodePage      = enc.CodePage,
                                IsDirectZipEntry = depth == 0,
                            });
                        }
                        else if (depth < MaxNestDepth && DiNeArchiveTools.IsArchive(entryExt))
                        {
                            EditorUtility.DisplayProgressBar("Package Patcher",
                                $"{Path.GetFileName(zipPath)} → {entry.Name}", 0.5f);

                            string dir = Path.Combine(tempCachePath, "Nested_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                            Directory.CreateDirectory(dir);
                            // 확장자만 유지한 ASCII 이름으로 풀어 경로 문제를 피한다.
                            string outPath = Path.Combine(dir, "inner" + entryExt);
                            entry.ExtractToFile(outPath, true);
                            nested.Add(new KeyValuePair<string, string>(outPath, entry.FullName));
                        }
                    }
                }
            }
            catch
            {
                // 이 인코딩으로는 못 읽음 → 모은 것을 버리고 다음 인코딩으로.
                results.RemoveRange(resultMark, results.Count - resultMark);
                foreach (var n in nested)
                    try { Directory.Delete(Path.GetDirectoryName(n.Key), true); } catch { }
                continue;
            }

            foreach (var n in nested)
                DiscoverArchive(n.Key, pathPrefix + n.Value + "/", AppendChain(chain, Path.GetFileName(n.Value)),
                                depth + 1, results, errors);
            return true;
        }
        return false;
    }

    /// <summary>
    /// .rar / .7z 는 .NET 이 열지 못하므로 외부 도구로 .unitypackage(와 안쪽 압축)만
    /// 캐시 폴더에 미리 추출한다. 추출 결과가 곧 임포트용 파일이 된다.
    /// </summary>
    private void DiscoverExternal(string archivePath, string pathPrefix, string chain, int depth,
                                  List<FoundPackage> results, List<string> errors)
    {
        if (!DiNeArchiveTools.HasAnyTool())
        {
            errors.Add($"{Path.GetFileName(archivePath)}: {UI_TEXT[22]}");
            return;
        }

        string destRoot = Path.Combine(tempCachePath, "Ext_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        List<string> extracted;
        string error;
        try
        {
            EditorUtility.DisplayProgressBar("Package Patcher",
                $"{ArchiveLabelOf(archivePath)}: {(string.IsNullOrEmpty(chain) ? Path.GetFileName(archivePath) : chain)}", 0.5f);
            Directory.CreateDirectory(destRoot);
            extracted = DiNeArchiveTools.ExtractUnityPackages(archivePath, destRoot, depth < MaxNestDepth, out error);
        }
        catch (Exception e)
        {
            errors.Add($"{Path.GetFileName(archivePath)}: {e.Message}");
            return;
        }

        if (extracted.Count == 0)
        {
            try { Directory.Delete(destRoot, true); } catch { }
            if (!string.IsNullOrEmpty(error)) errors.Add($"{Path.GetFileName(archivePath)}: {error}");
            return;
        }

        foreach (var file in extracted)
        {
            string rel;
            try { rel = file.Substring(destRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { rel = Path.GetFileName(file); }
            // 도구별 임시 하위 폴더(GUID) 한 겹은 표시에서 걷어낸다.
            int slash = rel.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            if (slash >= 0) rel = rel.Substring(slash + 1);
            rel = rel.Replace('\\', '/');

            if (Path.GetExtension(file).ToLower() == ".unitypackage")
            {
                results.Add(new FoundPackage
                {
                    InnerPath      = pathPrefix + rel,
                    ContainerChain = chain,
                    CachedPath     = file,
                });
            }
            else
            {
                DiscoverArchive(file, pathPrefix + rel + "/", AppendChain(chain, Path.GetFileName(file)),
                                depth + 1, results, errors);
            }
        }
    }

    private static string AppendChain(string chain, string name)
        => string.IsNullOrEmpty(chain) ? name : (string.IsNullOrEmpty(name) ? chain : chain + " › " + name);

    private void StartImport()
    {
        if (HasPendingBatch())
        {
            Debug.LogWarning("[DiNe] 이미 임포트가 진행 중입니다.");
            return;
        }

        var targets = foundPackages.Where(p => p.IsSelected).ToList();
        if (targets.Count == 0) return;

        foreach (var p in foundPackages) { p.IsDone = false; p.IsFailed = false; }

        statusMessage = UI_TEXT[9];
        // 이전 임포트 임시 파일만 정리 (캐시 폴더는 유지 - ProcessFile에서 미리 추출한 파일 보존)
        try { if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true); } catch { }
        if (!Directory.Exists(tempExtractPath)) Directory.CreateDirectory(tempExtractPath);

        // 준비 단계(추출/복사/파싱)만 리로드를 잠근다. 실제 임포트 중에는 잠그지
        // 않는다 — 스크립트가 컴파일되지 않은 상태로 뒤 패키지를 임포트하면
        // 커스텀 임포터·셰이더가 필요한 에셋이 반쪽만 들어온다.
        AcquireAssemblyReloadLock();

        var state = new PendingState
        {
            target = GetSafeFolderName(targetFolderName),
            before = EnumerateTopLevelAssets(),
        };
        var predictedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var item = targets[i];
                EditorUtility.DisplayProgressBar("Package Patcher",
                    $"준비 중: {Path.GetFileName(item.DisplayName)}", (float)i / targets.Count);

                string prepared = PreparePackageFile(item);
                if (string.IsNullOrEmpty(prepared)) { item.IsFailed = true; continue; }

                state.queue.Add(prepared);
                state.queueId.Add(item.Id);
                foreach (var r in GetPackageRootFolders(prepared)) predictedRoots.Add(r);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            ReleaseAssemblyReloadLock();
        }

        state.roots.AddRange(predictedRoots);
        state.total = state.queue.Count;

        if (state.total == 0)
        {
            statusMessage = UI_TEXT[19];
            return;
        }

        SaveState(state);
        Debug.Log($"[DiNe] StartImport: {state.total}개 큐, 예측 폴더=[{string.Join(", ", predictedRoots)}]");
        StartDriver();
    }

    /// <summary>선택 항목을 ASCII 임시 경로의 .unitypackage 로 준비한다. 실패 시 null.</summary>
    private string PreparePackageFile(PackageItem item)
    {
        string safeTempPath = Path.Combine(tempExtractPath,
            $"SafeImport_{Guid.NewGuid().ToString("N").Substring(0, 8)}.unitypackage");

        if (item.IsFromZip)
        {
            // ProcessFile 시점에 캐시된 파일이 있으면 재복사 없이 그대로 쓴다.
            if (!string.IsNullOrEmpty(item.CachedTempPath) && File.Exists(item.CachedTempPath))
            {
                try { return Path.GetFullPath(item.CachedTempPath); }
                catch (Exception e)
                {
                    Debug.LogError($"[DiNe] 캐시 경로 확인 실패: {item.DisplayName}\n{e.Message}");
                    return null;
                }
            }

            if (item.IsExternalArchive)
            {
                // 캐시가 지워졌으면 원본 압축을 다시 탐색해서 같은 항목을 찾아 푼다.
                try
                {
                    var results = new List<FoundPackage>();
                    var errors  = new List<string>();
                    DiscoverArchive(item.SourcePath, "", "", 0, results, errors);

                    var hit = results.FirstOrDefault(r => r.InnerPath == item.PackagePathInZip);
                    string file = hit != null ? MaterializeFound(hit) : null;
                    foreach (var other in results) if (other != hit) DeleteCachedFile(other);

                    if (file == null)
                    {
                        Debug.LogError($"[DiNe] {item.ArchiveLabel} 재추출 실패: {item.DisplayName}\n" + string.Join("\n", errors));
                        return null;
                    }
                    item.CachedTempPath = file;
                    return Path.GetFullPath(file);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DiNe] {item.ArchiveLabel} 재추출 실패: {item.DisplayName}\n{e.Message}");
                    return null;
                }
            }

            try
            {
                using (var archive = ZipFile.OpenRead(item.SourcePath))
                {
                    var entry = archive.GetEntry(item.PackagePathInZip)
                        ?? archive.Entries.FirstOrDefault(e =>
                               e.FullName.Equals(item.PackagePathInZip, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                    {
                        Debug.LogError($"[DiNe] ZIP 내 파일을 찾을 수 없음: {item.PackagePathInZip}");
                        return null;
                    }
                    entry.ExtractToFile(safeTempPath, true);
                    return Path.GetFullPath(safeTempPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DiNe] ZIP 추출 실패: {item.DisplayName}\n{e.Message}");
                return null;
            }
        }

        string fullPath;
        try { fullPath = Path.GetFullPath(item.SourcePath); }
        catch { fullPath = item.SourcePath; }
        if (!File.Exists(fullPath) && File.Exists(item.SourcePath)) fullPath = item.SourcePath;

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[DiNe] 파일을 찾을 수 없습니다: {item.DisplayName} | {fullPath}");
            return null;
        }

        try
        {
            File.Copy(fullPath, safeTempPath, true);
            return Path.GetFullPath(safeTempPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DiNe] 안전 복사 실패: {item.DisplayName} | {fullPath}\n{e.Message}");
            return null;
        }
    }

    private void AcquireAssemblyReloadLock()
    {
        if (reloadAssembliesLocked) return;
        EditorApplication.LockReloadAssemblies();
        reloadAssembliesLocked = true;
    }

    private void ReleaseAssemblyReloadLock()
    {
        if (!reloadAssembliesLocked) return;
        reloadAssembliesLocked = false;
        EditorApplication.UnlockReloadAssemblies();
    }

    /// <summary>
    /// 정리(이동/병합)가 끝난 뒤, 최종 폴더(Assets/targetFolderName/&lt;rootName&gt;)에
    /// NEW 뱃지를 단다. 이동은 guid 가 보존되지만 병합 시 원본이 삭제되므로
    /// 최종 위치를 명시적으로 등록해 "넣은 패키지가 안 뜨는" 문제를 막는다.
    /// </summary>
    private static void BadgeFolders(IEnumerable<string> folders)
    {
        var finals = folders.Where(AssetDatabase.IsValidFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (finals.Count > 0) DiNeNewAssetBadge.MarkFolders(finals);
    }

    /// <summary>
    /// 임포트된 루트(root)를 정리 폴더(Assets/targetFolderName) 안으로 옮긴다.
    /// 목적지에 같은 이름의 폴더가 이미 있으면 이동 대신 내용을 병합한다(재귀).
    /// </summary>
    private static string OrganizeRootFolder(string root, string targetFolderName)
    {
        string targetPath = "Assets/" + targetFolderName;
        if (root.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) return targetPath;

        bool isFolder = AssetDatabase.IsValidFolder(root);
        bool isFile   = !isFolder && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(root));

        if (!isFolder && !isFile)
        {
            Debug.LogWarning($"[DiNe] '{root}' 없음 (임포트 후에도 미생성) → 스킵");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(targetPath))
            AssetDatabase.CreateFolder("Assets", targetFolderName);

        string dest = targetPath + "/" + Path.GetFileName(root);

        // Assets 바로 아래에 놓인 단일 파일도 정리 폴더로 함께 옮긴다.
        if (isFile)
        {
            SafeMoveAsset(root, dest);
            return null;
        }

        // 목적지에 같은 이름 폴더가 이미 있으면 → 내용 병합 (재귀)
        if (AssetDatabase.IsValidFolder(dest))
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(root);
            string destGuid   = AssetDatabase.AssetPathToGUID(dest);

            // Two unrelated packages can use the same top-level folder name. Merging
            // those folders and overwriting collisions destroys one side's GUIDs.
            // Keep both folder trees when their folder GUIDs identify different assets.
            if (!string.IsNullOrEmpty(sourceGuid) && !string.IsNullOrEmpty(destGuid) &&
                !sourceGuid.Equals(destGuid, StringComparison.OrdinalIgnoreCase))
            {
                string uniqueDest = AssetDatabase.GenerateUniqueAssetPath(dest);
                string uniqueErr  = AssetDatabase.MoveAsset(root, uniqueDest);
                if (!string.IsNullOrEmpty(uniqueErr))
                {
                    Debug.LogWarning($"[DiNe] GUID 보존 이동 실패: {root} → {uniqueDest}\n{uniqueErr}");
                    return null;
                }

                Debug.LogWarning($"[DiNe] 같은 이름의 서로 다른 패키지 폴더를 발견하여 GUID 보존을 위해 분리했습니다: " +
                                 $"{root} → {uniqueDest}");
                return uniqueDest;
            }

            Debug.Log($"[DiNe] '{dest}' 이미 존재 → 내용 병합");
            MergeFolderInto(root, dest);
            return dest;
        }

        string err = AssetDatabase.MoveAsset(root, dest);
        if (!string.IsNullOrEmpty(err))
        {
            // 이동이 거부되는 흔한 이유(파일 잠금, 임포트 미완료)를 함께 남긴다.
            string validate = AssetDatabase.ValidateMoveAsset(root, dest);
            Debug.LogWarning($"[DiNe] 폴더 이동 실패: {root} → {dest}\n{err}" +
                             (string.IsNullOrEmpty(validate) ? "" : $"\n(validate: {validate})"));
            return null;
        }

        Debug.Log($"[DiNe] 폴더 이동 완료: {root} → {dest}");
        return dest;
    }

    /// <summary>
    /// source 폴더의 내용을 (이미 존재하는) dest 폴더 안으로 합친다.
    /// - 같은 이름의 하위 폴더가 dest에도 있으면 재귀적으로 병합
    /// - 충돌하지 않는 폴더/파일은 그대로 이동
    /// - 같은 GUID의 파일은 업데이트하고, 다른 GUID의 동명 파일은 고유 경로로 분리한다
    /// 병합이 끝나면 비워진 source 폴더를 삭제한다.
    /// </summary>
    private static void MergeFolderInto(string source, string dest)
    {
        // 하위 폴더 처리
        foreach (var sub in AssetDatabase.GetSubFolders(source))
        {
            string childDest = dest + "/" + Path.GetFileName(sub);
            if (AssetDatabase.IsValidFolder(childDest))
            {
                string sourceGuid = AssetDatabase.AssetPathToGUID(sub);
                string destGuid   = AssetDatabase.AssetPathToGUID(childDest);
                if (!string.IsNullOrEmpty(sourceGuid) &&
                    sourceGuid.Equals(destGuid, StringComparison.OrdinalIgnoreCase))
                    MergeFolderInto(sub, childDest);       // 같은 폴더 에셋 → 재귀 병합
                else
                    SafeMoveAsset(sub, childDest);         // 이름만 같은 폴더 → 둘 다 보존
            }
            else
                SafeMoveAsset(sub, childDest);             // 새 폴더 → 통째로 이동
        }

        // 직속 파일 처리 (.meta 는 AssetDatabase 가 자동 처리하므로 스킵)
        foreach (var file in Directory.GetFiles(source))
        {
            string assetSrc = file.Replace('\\', '/');
            if (assetSrc.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string fileDest = dest + "/" + Path.GetFileName(assetSrc);
            SafeMoveAsset(assetSrc, fileDest);
        }

        // 이동 실패가 하나라도 있었다면 source를 지우지 않는다. 실패한 에셋을
        // 함께 삭제하는 것보다 정리되지 않은 폴더를 남기는 편이 안전하다.
        bool hasSubFolders = AssetDatabase.GetSubFolders(source).Length > 0;
        bool hasAssetFiles = Directory.GetFiles(source)
            .Any(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
        if (!hasSubFolders && !hasAssetFiles)
            AssetDatabase.DeleteAsset(source);
        else
            Debug.LogWarning($"[DiNe] 일부 에셋을 이동하지 못해 원본 폴더를 보존합니다: {source}");
    }

    /// <summary>
    /// src 에셋을 dest 로 이동. 충돌 시 GUID가 같으면 업데이트하고, 다르면 둘 다 보존한다.
    /// </summary>
    private static void SafeMoveAsset(string src, string dest)
    {
        bool destinationExists = AssetDatabase.IsValidFolder(dest)
            || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(dest))
            || File.Exists(dest)
            || Directory.Exists(dest);

        if (destinationExists)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(src);
            string destGuid   = AssetDatabase.AssetPathToGUID(dest);

            if (!string.IsNullOrEmpty(sourceGuid) &&
                sourceGuid.Equals(destGuid, StringComparison.OrdinalIgnoreCase))
            {
                // Same logical asset (for example a package update): replacing it keeps
                // the GUID stable for references on both sides.
                if (!AssetDatabase.DeleteAsset(dest))
                {
                    Debug.LogWarning($"[DiNe] 기존 에셋 삭제 실패: {dest}");
                    return;
                }
            }
            else
            {
                // Different assets with the same filename must coexist. Moving the new
                // one to a unique path preserves both GUIDs and therefore prefab,
                // material, texture, animation, and script references.
                string uniqueDest = AssetDatabase.GenerateUniqueAssetPath(dest);
                string uniqueErr = AssetDatabase.MoveAsset(src, uniqueDest);
                if (!string.IsNullOrEmpty(uniqueErr))
                    Debug.LogWarning($"[DiNe] GUID 보존 병합 이동 실패: {src} → {uniqueDest}\n{uniqueErr}");
                else
                    Debug.LogWarning($"[DiNe] 파일명 충돌을 GUID 손실 없이 분리했습니다: {dest} → {uniqueDest}");
                return;
            }
        }

        string err = AssetDatabase.MoveAsset(src, dest);
        if (!string.IsNullOrEmpty(err))
            Debug.LogWarning($"[DiNe] 병합 이동 실패: {src} → {dest}\n{err}");
    }

    /// <summary>
    /// .unitypackage(= tgz) 파일을 파싱해 설치될 Assets 루트 경로 목록 반환.
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
            var skipBuf  = new byte[64 * 1024];

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
                if (size < 0) throw new InvalidDataException($"잘못된 TAR 엔트리 크기: {size}");
                long paddedSize = (size + 511L) / 512 * 512;

                // GUID/pathname 엔트리에서 에셋 경로 읽기
                if (entryName.EndsWith("/pathname") || entryName.EndsWith("\\pathname"))
                {
                    if (size > int.MaxValue)
                    {
                        // pathname is normally only a few bytes. Guarding the cast also
                        // keeps malformed or very large archives from overflowing.
                        TarSkipBytes(gz, paddedSize, skipBuf);
                        continue;
                    }

                    var content = new byte[(int)size];
                    if (TarReadExact(gz, content, (int)size) != (int)size)
                        throw new EndOfStreamException("pathname 엔트리가 중간에 끝났습니다.");

                    // pathname 은 두 줄(신규 경로 / 원본 경로)일 수 있으므로 첫 줄만 쓴다.
                    string raw = Encoding.UTF8.GetString(content);
                    string assetPath = raw.Split('\n')[0].Trim('\0', '\r', '\n', ' ');
                    if (assetPath.StartsWith("Assets/"))
                    {
                        var rel  = assetPath.Substring("Assets/".Length);
                        var idx  = rel.IndexOf('/');
                        var root = idx >= 0 ? rel.Substring(0, idx) : rel;
                        if (!string.IsNullOrEmpty(root)) roots.Add("Assets/" + root);
                    }
                    long pad = paddedSize - size;
                    if (pad > 0) TarSkipBytes(gz, pad, skipBuf);
                }
                else
                {
                    TarSkipBytes(gz, paddedSize, skipBuf);
                }
            }
        }
        catch (Exception e)
        {
            // 파싱이 실패해도 임포트 후 새 폴더 스캔이 정리를 대신 처리한다.
            Debug.LogWarning($"[DiNe] 패키지 파싱 실패 (임포트 후 새 폴더 스캔으로 대체): " +
                             $"{Path.GetFileName(packagePath)}\n{e.Message}");
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

    private static void TarSkipBytes(Stream s, long count, byte[] tmp)
    {
        long rem = count;
        while (rem > 0)
        {
            int request = (int)Math.Min(rem, tmp.Length);
            int r = s.Read(tmp, 0, request);
            if (r <= 0) throw new EndOfStreamException("TAR 엔트리가 중간에 끝났습니다.");
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
        // 진행 중인 배치의 임시 패키지를 지우면 남은 임포트가 전부 실패한다.
        if (HasPendingBatch()) return;
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
                    /* 15 */ ".unitypackage · .zip · .rar · .7z · 폴더 지원",
                    /* 16 */ "전체",
                    /* 17 */ "없음",
                    /* 18 */ "Clear",
                    /* 19 */ "임포트할 파일을 찾을 수 없습니다. (콘솔 창 확인)",
                    /* 20 */ "📄  파일 직접 선택",
                    /* 21 */ "📁  폴더 직접 선택",
                    /* 22 */ ".rar · .7z 를 열려면 7-Zip / WinRAR / Bandizip 이 필요합니다.",
                    /* 23 */ "압축 파일 안에서 .unitypackage 를 찾지 못했습니다. (콘솔 창 확인)",
                    /* 24 */ "넣을 패키지 선택",
                    /* 25 */ "{0} 안에서 패키지 {1}개를 찾았습니다.\n목록에 넣을 항목을 선택하세요.",
                    /* 26 */ "선택 항목 추가",
                    /* 27 */ "취소",
                    /* 28 */ "선택 창 {0} / {1}   ·   이 창 뒤에 {2}개 더 남았습니다",
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
                    /* 15 */ ".unitypackage · .zip · .rar · .7z · フォルダ対応",
                    /* 16 */ "全選択",
                    /* 17 */ "解除",
                    /* 18 */ "Clear",
                    /* 19 */ "インポートするファイルが見つかりません。(コンソール確認)",
                    /* 20 */ "📄  ファイルを直接選択",
                    /* 21 */ "📁  フォルダを直接選択",
                    /* 22 */ ".rar · .7z を開くには 7-Zip / WinRAR / Bandizip が必要です。",
                    /* 23 */ "圧縮ファイル内に .unitypackage が見つかりません。(コンソール確認)",
                    /* 24 */ "追加するパッケージを選択",
                    /* 25 */ "{0} の中にパッケージが {1} 個見つかりました。\nリストに追加する項目を選択してください。",
                    /* 26 */ "選択項目を追加",
                    /* 27 */ "キャンセル",
                    /* 28 */ "選択ウィンドウ {0} / {1}   ·   この後にあと {2} 個あります",
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
                    /* 15 */ ".unitypackage · .zip · .rar · .7z · folder supported",
                    /* 16 */ "All",
                    /* 17 */ "None",
                    /* 18 */ "Clear",
                    /* 19 */ "No files to import. (Check Console)",
                    /* 20 */ "📄  Browse File",
                    /* 21 */ "📁  Browse Folder",
                    /* 22 */ "7-Zip / WinRAR / Bandizip is required to open .rar · .7z files.",
                    /* 23 */ "No .unitypackage found inside the archive. (Check Console)",
                    /* 24 */ "Select Packages to Add",
                    /* 25 */ "Found {1} packages inside {0}.\nChoose which ones to add to the list.",
                    /* 26 */ "Add Selected",
                    /* 27 */ "Cancel",
                    /* 28 */ "Window {0} / {1}   ·   {2} more after this one",
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

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class DiNePackagePatcher : EditorWindow
{
    private enum LanguagePreset { English, Korean, Japanese }
    private LanguagePreset language = LanguagePreset.Korean;

    private string selectedFolderPath = "";
    private string targetFolderName = "_1_Patch";
    private List<string> foundPackages = new List<string>();
    private Vector2 packageScrollPos;

    private string statusMessage = "";
    private bool isImporting = false;

    private string[] UI_TEXT;
    private Texture2D windowIcon;
    private Texture2D tabIcon;
    private Font      titleFont;
    private Texture2D selectedButtonTex;

    // 임포트 추적용 정적 필드
    private static string[] preImportFolders;
    private static int totalPackagesToImport = 0;
    private static int currentlyProcessedPackages = 0;
    private static string pendingTargetFolderName = "_1_Patch";
    private static DiNePackagePatcher activeWindow;

    [MenuItem("DiNe/EX/Package Patcher", false, 100)]
    public static void ShowWindow()
    {
        EditorWindow window = GetWindow<DiNePackagePatcher>();
        window.minSize = new Vector2(300, 350);
        window.position = new Rect(window.position.x, window.position.y, 420, 580);
    }

    void OnEnable()
    {
        windowIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe.png");
        tabIcon    = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dine.tool/Assets/DiNe_Icon.png");
        titleFont  = AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dine.tool/DungGeunMo.ttf");
        titleContent = new GUIContent("Package", tabIcon);
        selectedButtonTex = MakeTex(1, 1, new Color(0.2f, 0.4f, 1f, 1f));
        SetLanguage(language);
        activeWindow = this;
    }

    void OnDisable()
    {
        if (activeWindow == this) activeWindow = null;
    }

    void OnGUI()
    {
        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // ─── 타이틀 바 ───
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
        GUILayout.Label(windowIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUILayout.Space(6);
        GUILayout.Label("Package Patcher", titleStyle, GUILayout.Height(iconSize));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        string desc = "";
        switch (language)
        {
            case LanguagePreset.Korean: desc = "DiNe 툴의 최신 업데이트를 일괄 설치하고, 지정된 폴더로 정리합니다."; break;
            case LanguagePreset.Japanese: desc = "DiNeツールの最新アップデートを一括インストールし、指定フォルダに整理します。"; break;
            default: desc = "Batch install the latest DiNe tool updates and organize them into the specified folder."; break;
        }
        GUILayout.Label(desc, new GUIStyle(EditorStyles.wordWrappedLabel) 
            { alignment = TextAnchor.MiddleCenter, fontSize = 12, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } });

        GUILayout.Space(5);
        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 언어 선택 ───
        int currentLangIndex = (int)language;
        string[] langButtons = { "English", "한국어", "日本語" };
        int newLangIndex = DrawCustomToolbar(currentLangIndex, langButtons, 30);
        if (newLangIndex != currentLangIndex)
        {
            language = (LanguagePreset)newLangIndex;
            SetLanguage(language);
        }
        GUILayout.Space(10);

        // ─── 설정 박스 ───
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[0], EditorStyles.boldLabel); // 설정
        GUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(UI_TEXT[1], GUILayout.Width(130)); // 이동 폴더명
        targetFolderName = EditorGUILayout.TextField(targetFolderName);
        EditorGUILayout.EndHorizontal();

        // 동일 폴더 존재 경고
        if (!string.IsNullOrEmpty(targetFolderName) && AssetDatabase.IsValidFolder("Assets/" + targetFolderName))
        {
            GUILayout.Space(2);
            GUILayout.Label(UI_TEXT[12], new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = new Color(1f, 0.3f, 0.3f) }, fontStyle = FontStyle.Bold, wordWrap = true });
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 폴더 선택 박스 ───
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(UI_TEXT[2], EditorStyles.boldLabel); // 패키지 폴더
        GUILayout.Space(3);

        if (GUILayout.Button(UI_TEXT[3], GUILayout.Height(30))) // 폴더 선택
        {
            string path = EditorUtility.OpenFolderPanel(UI_TEXT[3], selectedFolderPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                selectedFolderPath = path;
                RefreshPackageList();
            }
        }

        if (!string.IsNullOrEmpty(selectedFolderPath))
        {
            GUIStyle pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUILayout.Label(selectedFolderPath, pathStyle);
        }

        GUILayout.Space(5);

        // 패키지 목록
        EditorGUILayout.LabelField(UI_TEXT[4], EditorStyles.boldLabel); // 패키지 목록
        packageScrollPos = EditorGUILayout.BeginScrollView(packageScrollPos, GUILayout.Height(120));

        if (foundPackages.Count == 0)
        {
            GUIStyle emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                wordWrap = true
            };
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(selectedFolderPath) ? UI_TEXT[5] : UI_TEXT[6], emptyStyle);
            GUILayout.FlexibleSpace();
        }
        else
        {
            GUIStyle itemStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            for (int i = 0; i < foundPackages.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // 번호
                GUIStyle numStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                    alignment = TextAnchor.MiddleRight
                };
                GUILayout.Label($"{i + 1}.", numStyle, GUILayout.Width(22));
                GUILayout.Label(Path.GetFileName(foundPackages[i]), itemStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();

        if (foundPackages.Count > 0)
        {
            GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.4f, 0.9f, 0.4f) }
            };
            GUILayout.Label($"{foundPackages.Count} {UI_TEXT[7]}", countStyle); // N개 발견
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(5);

        // ─── 상태 박스 ───
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.BeginVertical("box");
            GUIStyle statusStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                normal = { textColor = isImporting ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.9f, 0.4f) }
            };
            EditorGUILayout.LabelField(UI_TEXT[8], EditorStyles.boldLabel); // 상태
            EditorGUILayout.LabelField(statusMessage, statusStyle);
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        // ─── 임포트 버튼 ───
        EditorGUI.BeginDisabledGroup(foundPackages.Count == 0 || isImporting);
        GUIStyle importBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14
        };
        if (isImporting)
        {
            importBtnStyle.normal.background = selectedButtonTex;
            importBtnStyle.normal.textColor = Color.white;
        }
        var prevBg = GUI.backgroundColor;
        if (!isImporting) GUI.backgroundColor = new Color(0.30f, 0.82f, 0.76f); // Mint Green
        
        if (GUILayout.Button(isImporting ? UI_TEXT[9] : UI_TEXT[10], importBtnStyle, GUILayout.Height(40))) // 임포트 중... / 임포트 시작
        {
            StartImport();
        }
        
        GUI.backgroundColor = prevBg;
        EditorGUI.EndDisabledGroup();
    }

    private void RefreshPackageList()
    {
        foundPackages.Clear();
        if (!string.IsNullOrEmpty(selectedFolderPath) && Directory.Exists(selectedFolderPath))
        {
            foundPackages = Directory.GetFiles(selectedFolderPath, "*.unitypackage").ToList();
            foundPackages.Sort();
        }
        statusMessage = "";
        Repaint();
    }

    private void StartImport()
    {
        if (foundPackages.Count == 0) return;

        isImporting = true;
        statusMessage = UI_TEXT[9]; // 임포트 중...

        preImportFolders = AssetDatabase.GetSubFolders("Assets");
        totalPackagesToImport = foundPackages.Count;
        currentlyProcessedPackages = 0;
        pendingTargetFolderName = targetFolderName;
        activeWindow = this;

        AssetDatabase.importPackageCompleted += OnPackageProcessed;
        AssetDatabase.importPackageCancelled += OnPackageProcessed;
        AssetDatabase.importPackageFailed += OnPackageFailed;

        foreach (string package in foundPackages)
        {
            Debug.Log($"[DiNe Patcher] {Path.GetFileName(package)} 대기열 등록...");
            AssetDatabase.ImportPackage(package, false);
        }

        Repaint();
    }

    private static void OnPackageProcessed(string packageName)
    {
        currentlyProcessedPackages++;
        CheckIfAllFinished();
    }

    private static void OnPackageFailed(string packageName, string errorMessage)
    {
        Debug.LogError($"[DiNe Patcher] {packageName} 임포트 실패: {errorMessage}");
        currentlyProcessedPackages++;
        CheckIfAllFinished();
    }

    private static void CheckIfAllFinished()
    {
        if (currentlyProcessedPackages >= totalPackagesToImport)
        {
            AssetDatabase.importPackageCompleted -= OnPackageProcessed;
            AssetDatabase.importPackageCancelled -= OnPackageProcessed;
            AssetDatabase.importPackageFailed -= OnPackageFailed;

            MoveNewFolders();

            if (activeWindow != null)
            {
                activeWindow.isImporting = false;
                activeWindow.statusMessage = activeWindow.UI_TEXT[11]; // 완료
                activeWindow.Repaint();
            }
        }
    }

    private static void MoveNewFolders()
    {
        string[] postImportFolders = AssetDatabase.GetSubFolders("Assets");
        var newFolders = postImportFolders.Except(preImportFolders).ToList();

        if (newFolders.Count > 0)
        {
            string targetFolderPath = "Assets/" + pendingTargetFolderName;

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                AssetDatabase.CreateFolder("Assets", pendingTargetFolderName);
            }

            foreach (string newFolder in newFolders)
            {
                if (newFolder == targetFolderPath) continue;

                string folderName = newFolder.Substring("Assets/".Length);
                string destinationPath = targetFolderPath + "/" + folderName;

                string error = AssetDatabase.MoveAsset(newFolder, destinationPath);

                if (string.IsNullOrEmpty(error))
                    Debug.Log($"[DiNe Patcher] {folderName} → {pendingTargetFolderName} 이동 완료");
                else
                    Debug.LogError($"[DiNe Patcher] {folderName} 이동 실패: {error}");
            }
        }
        else
        {
            Debug.Log("[DiNe Patcher] 새로 생성된 최상위 폴더가 없습니다. (기존 폴더 덮어쓰기 완료)");
        }

        Debug.Log("[DiNe Patcher] 모든 패키지 임포트 및 자동 정리 작업이 완료되었습니다!");
    }

    private void SetLanguage(LanguagePreset lang)
    {
        switch (lang)
        {
            case LanguagePreset.Korean:
                UI_TEXT = new string[]
                {
                    "설정",                          // 0
                    "임포트 후 이동 폴더명",           // 1
                    "패키지 폴더",                    // 2
                    "폴더 선택",                      // 3
                    "패키지 목록",                    // 4
                    "폴더를 선택하세요.",              // 5
                    "이 폴더에 .unitypackage 파일이 없습니다.", // 6
                    "개 파일 발견",                   // 7
                    "상태",                          // 8
                    "임포트 중...",                   // 9
                    "임포트 및 정리 시작",             // 10
                    "모든 임포트 및 정리 완료!",        // 11
                    "⚠ 동일한 이름의 폴더가 이미 존재합니다. 실행 시 기존 폴더 안에 병합됩니다.", // 12
                };
                break;
            case LanguagePreset.Japanese:
                UI_TEXT = new string[]
                {
                    "設定",
                    "移動先フォルダ名",
                    "パッケージフォルダ",
                    "フォルダを選択",
                    "パッケージ一覧",
                    "フォルダを選択してください。",
                    "このフォルダに .unitypackage がありません。",
                    "個のファイルを検出",
                    "状態",
                    "インポート中...",
                    "インポートして整理を開始",
                    "全インポート・整理が完了しました！",
                    "⚠ 同名のフォルダが既に存在します。実行すると既存フォルダに統合されます。", // 12
                };
                break;
            default: // English
                UI_TEXT = new string[]
                {
                    "Settings",
                    "Target Folder Name",
                    "Package Folder",
                    "Select Folder",
                    "Package List",
                    "Please select a folder.",
                    "No .unitypackage files found in this folder.",
                    "file(s) found",
                    "Status",
                    "Importing...",
                    "Start Import & Organize",
                    "All imports & organization complete!",
                    "⚠ A folder with this name already exists. Files will be merged into it.", // 12
                };
                break;
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private int DrawCustomToolbar(int selected, string[] options, float height)
    {
        EditorGUILayout.BeginHorizontal();
        int newSelected = selected;
        for (int i = 0; i < options.Length; i++)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = (i == selected) ? new Color(0.30f, 0.82f, 0.76f) : new Color(0.5f, 0.5f, 0.5f, 1f);
            GUIStyle style = new GUIStyle(GUI.skin.button) { 
                fontStyle = (i == selected) ? FontStyle.Bold : FontStyle.Normal,
                fontSize = 12,
                normal = { textColor = (i == selected) ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            if (GUILayout.Button(options[i], style, GUILayout.Height(height)))
            {
                newSelected = i;
            }
            GUI.backgroundColor = prevBg;
        }
        EditorGUILayout.EndHorizontal();
        return newSelected;
    }
}

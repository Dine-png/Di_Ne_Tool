#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DiNeNewAssetBadge
{
    private const string DataPath = "UserSettings/DiNeNewAssetBadges.json";

    // 임포트 직후 자동 선택/이동으로 뱃지가 즉시 지워지는 것을 막는 유예 구간.
    // SessionState 에 저장하여 (스크립트 포함 패키지의) 도메인 리로드 후에도 유지된다.
    private const string SuppressKey   = "DiNeNewAssetBadge_SuppressUntil";
    private const double GraceSeconds  = 2.0;

    private static HashSet<string> _knownGuids = new HashSet<string>();
    private static HashSet<string> _newGuids = new HashSet<string>();
    private static GUIStyle _labelStyle;
    private static GUIStyle _listLabelStyle;
    private static string _lastProjectFolder = "";
    private static int _packageImportDepth;
    private static readonly GUIContent _content = new GUIContent("NEW");
    private static readonly Color _badgeFill = new Color(0.08f, 0.16f, 0.15f, 0.95f);
    private static readonly Color _badgeMint = new Color(0.30f, 0.82f, 0.76f, 1f);

    /// <summary>현재 패키지 임포트(.unitypackage)가 진행 중인지. 오작동 방지 게이트.</summary>
    internal static bool IsInPackageImport => _packageImportDepth > 0;

    static DiNeNewAssetBadge()
    {
        Load();

        if (_knownGuids.Count == 0)
            SeedKnownGuids();

        EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;

        AssetDatabase.importPackageStarted   += OnImportStarted;
        AssetDatabase.importPackageCompleted += OnImportEnded;
        AssetDatabase.importPackageCancelled += OnImportEnded;
        AssetDatabase.importPackageFailed    += OnImportFailed;

        // 지난 세션에서 삭제된 에셋의 죽은 guid 정리 (AssetDatabase 준비 후)
        EditorApplication.delayCall += () =>
        {
            if (PruneDead()) { Save(); EditorApplication.RepaintProjectWindow(); }
        };
    }

    private static void OnImportStarted(string name) { _packageImportDepth++; BumpSuppress(); }
    private static void OnImportEnded(string name)   { if (_packageImportDepth > 0) _packageImportDepth--; BumpSuppress(); }
    private static void OnImportFailed(string name, string err) { if (_packageImportDepth > 0) _packageImportDepth--; }

    private static void SeedKnownGuids()
    {
        foreach (var g in AssetDatabase.FindAssets(string.Empty))
            _knownGuids.Add(g);
        Save();
    }

    private static readonly string[] _ignoredExtensions =
    {
        ".cs", ".asmdef", ".asmref", ".rsp", ".dll", ".pdb", ".mdb"
    };

    private static bool IsIgnored(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return false;
        ext = ext.ToLowerInvariant();
        for (int i = 0; i < _ignoredExtensions.Length; i++)
            if (_ignoredExtensions[i] == ext) return true;
        return false;
    }

    /// <summary>
    /// 패키지 임포트로 새로 들어온 에셋 경로들을 받아, 그 중 "진짜 새것"의
    /// 최상위 폴더에만 NEW 를 단다. 이미 알고 있던(=재임포트) guid 는 무시한다.
    /// 패키지 임포트 구간에서만 호출된다(Postprocessor 게이트).
    /// </summary>
    internal static void RegisterImports(IEnumerable<string> paths)
    {
        var candidates = new List<string>();
        foreach (var raw in paths)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var path = raw.Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsIgnored(path)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;

            // Add 가 false 면 이미 알고 있던 에셋 → 재임포트이므로 NEW 아님
            if (!_knownGuids.Add(guid)) continue;
            candidates.Add(path);
        }
        if (candidates.Count == 0) { Save(); return; }

        // 새로 생긴 것 중 최상위만 표시 (부모 폴더가 후보에 있으면 자식은 스킵)
        var candidateSet = new HashSet<string>(candidates);
        bool changed = false;
        foreach (var path in candidates)
        {
            if (HasAncestorIn(path, candidateSet)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (_newGuids.Add(guid)) changed = true;
        }
        Save();
        if (changed)
        {
            BumpSuppress();
            EditorApplication.RepaintProjectWindow();
        }
    }

    /// <summary>
    /// Package Patcher 가 정리(이동/병합)를 끝낸 뒤, 최종 폴더 경로들을 명시적으로
    /// NEW 로 등록한다. 이동은 guid 가 보존돼 RegisterImports 결과가 따라오지만,
    /// 병합 시 원본 폴더가 삭제돼 guid 가 죽으므로 이 경로로 최종 위치를 보장한다.
    /// </summary>
    internal static void MarkFolders(IEnumerable<string> assetPaths)
    {
        bool changed = false;
        foreach (var raw in assetPaths)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            var path = raw.Replace('\\', '/');
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;
            _knownGuids.Add(guid);
            if (_newGuids.Add(guid)) changed = true;
        }
        PruneDead();
        Save();
        if (changed)
        {
            BumpSuppress();
            EditorApplication.RepaintProjectWindow();
        }
    }

    private static bool HasAncestorIn(string path, HashSet<string> set)
    {
        var parent = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(parent))
        {
            var normalized = parent.Replace('\\', '/');
            if (set.Contains(normalized)) return true;
            parent = Path.GetDirectoryName(parent);
        }
        return false;
    }

    private static void OnSelectionChanged()
    {
        if (_newGuids.Count == 0) return;
        if (IsSuppressed()) return;   // 임포트 직후 자동 선택으로 지워지는 것 방지

        bool changed = false;
        foreach (var obj in Selection.objects)
        {
            if (obj == null) continue;
            var path = AssetDatabase.GetAssetPath(obj);
            changed |= ClearPathAndAncestors(path);
        }
        if (changed)
        {
            Save();
            EditorApplication.RepaintProjectWindow();
        }
    }

    /// <summary>선택한 에셋과 그 상위 폴더들의 NEW 를 해제한다.</summary>
    private static bool ClearPathAndAncestors(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        path = path.Replace('\\', '/');
        bool changed = false;
        while (!string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid) && _newGuids.Remove(guid)) changed = true;
            if (string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase)) break;
            var parent = Path.GetDirectoryName(path);
            path = string.IsNullOrEmpty(parent) ? null : parent.Replace('\\', '/');
        }
        return changed;
    }

    private static void OnEditorUpdate()
    {
        if (_newGuids.Count == 0) return;

        var folder = GetCurrentProjectBrowserFolder();
        if (string.IsNullOrEmpty(folder) || folder == _lastProjectFolder) return;

        _lastProjectFolder = folder;      // 현재 위치는 항상 추적
        if (IsSuppressed()) return;       // 단, 유예 구간에는 지우지 않음

        ClearGuidForPath(folder);
    }

    private static void ClearGuidForPath(string path)
    {
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid) || !_newGuids.Remove(guid)) return;

        Save();
        EditorApplication.RepaintProjectWindow();
    }

    private static string GetCurrentProjectBrowserFolder()
    {
        try
        {
            var projectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType == null) return "";

            var projectWindow = Resources.FindObjectsOfTypeAll(projectBrowserType)
                .OfType<EditorWindow>()
                .FirstOrDefault(window => window != null && window.hasFocus);
            if (projectWindow == null) return "";

            var searchFilterField = projectBrowserType.GetField("m_SearchFilter", BindingFlags.Instance | BindingFlags.NonPublic);
            var searchFilter = searchFilterField?.GetValue(projectWindow);
            if (searchFilter == null) return "";

            var foldersField = searchFilter.GetType().GetField("m_Folders", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var folders = foldersField?.GetValue(searchFilter) as string[];
            return folders != null && folders.Length > 0 ? folders[0].Replace('\\', '/') : "";
        }
        catch
        {
            return "";
        }
    }

    private static void OnProjectItemGUI(string guid, Rect rect)
    {
        if (_newGuids.Count == 0) return;
        if (!_newGuids.Contains(guid)) return;

        bool listMode = rect.height <= 20f;
        Rect badgeRect;

        if (listMode)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var label = Path.GetFileNameWithoutExtension(path);
            float iconWidth = rect.height;
            float nameWidth = EditorStyles.label.CalcSize(new GUIContent(label)).x;
            float x = rect.x + iconWidth + 2f + nameWidth + 4f;
            x = Mathf.Min(x, rect.xMax - 18f);
            badgeRect = new Rect(x, rect.y + 2f, 16f, 15f);
            EditorGUI.DrawRect(badgeRect, _badgeFill);
            DrawBorder(badgeRect, _badgeMint, 1f);
            GUI.Label(new Rect(badgeRect.x, badgeRect.y - 1f, badgeRect.width, badgeRect.height), "N", GetListStyle());
            return;
        }
        else
        {
            badgeRect = new Rect(rect.xMax - 48f, rect.y + 1f, 42f, 15f);
        }

        EditorGUI.DrawRect(badgeRect, _badgeFill);
        DrawBorder(badgeRect, _badgeMint, 1f);

        var dotRect = new Rect(badgeRect.x + 5f, badgeRect.y + 5f, 5f, 5f);
        EditorGUI.DrawRect(dotRect, _badgeMint);

        GUI.Label(new Rect(badgeRect.x + 10f, badgeRect.y, badgeRect.width - 10f, badgeRect.height), _content, GetStyle());
    }

    private static GUIStyle GetStyle()
    {
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 8,
                padding = new RectOffset(0, 0, 0, 1)
            };
            _labelStyle.normal.textColor = _badgeMint;
        }
        return _labelStyle;
    }

    private static GUIStyle GetListStyle()
    {
        if (_listLabelStyle == null)
        {
            _listLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _listLabelStyle.normal.textColor = _badgeMint;
        }
        return _listLabelStyle;
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    // ── 유예 구간 (SessionState: 도메인 리로드 생존, 에디터 재시작 시 초기화) ──
    private static void BumpSuppress()
    {
        var until = EditorApplication.timeSinceStartup + GraceSeconds;
        SessionState.SetString(SuppressKey, until.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsSuppressed()
    {
        var s = SessionState.GetString(SuppressKey, "");
        if (string.IsNullOrEmpty(s)) return false;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var until)) return false;
        return EditorApplication.timeSinceStartup < until;
    }

    /// <summary>삭제된 에셋을 가리키는 죽은 guid 를 _newGuids 에서 제거한다.</summary>
    private static bool PruneDead()
    {
        if (_newGuids.Count == 0) return false;
        var dead = _newGuids.Where(g => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g))).ToList();
        foreach (var g in dead) _newGuids.Remove(g);
        return dead.Count > 0;
    }

    [Serializable]
    private class SaveData
    {
        public List<string> known = new List<string>();
        public List<string> news = new List<string>();
    }

    private static void Load()
    {
        try
        {
            if (!File.Exists(DataPath)) return;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(DataPath));
            if (data == null) return;
            _knownGuids = new HashSet<string>(data.known ?? new List<string>());
            _newGuids = new HashSet<string>(data.news ?? new List<string>());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DiNeNewAssetBadge] Load failed: " + e.Message);
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(DataPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new SaveData
            {
                known = _knownGuids.ToList(),
                news = _newGuids.ToList()
            };
            File.WriteAllText(DataPath, JsonUtility.ToJson(data));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DiNeNewAssetBadge] Save failed: " + e.Message);
        }
    }
}

internal class DiNeNewAssetBadgePostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] imported,
        string[] deleted,
        string[] movedTo,
        string[] movedFrom)
    {
        if (imported == null || imported.Length == 0) return;
        // 패키지 임포트 구간에서 들어온 에셋만 NEW 대상.
        // (재컴파일·재임포트·수동 에셋 생성 등은 무시 → 오작동 방지)
        if (!DiNeNewAssetBadge.IsInPackageImport) return;
        DiNeNewAssetBadge.RegisterImports(imported);
    }
}
#endif

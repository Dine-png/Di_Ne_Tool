#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class DiNeNewAssetBadge
{
    private const string DataPath = "UserSettings/DiNeNewAssetBadges.json";

    private static HashSet<string> _knownGuids = new HashSet<string>();
    private static HashSet<string> _newGuids = new HashSet<string>();
    private static GUIStyle _labelStyle;
    private static GUIStyle _listLabelStyle;
    private static string _lastProjectFolder = "";
    private static readonly GUIContent _content = new GUIContent("NEW");
    private static readonly Color _badgeFill = new Color(0.08f, 0.16f, 0.15f, 0.95f);
    private static readonly Color _badgeMint = new Color(0.30f, 0.82f, 0.76f, 1f);

    static DiNeNewAssetBadge()
    {
        Load();

        if (_knownGuids.Count == 0)
            SeedKnownGuids();

        EditorApplication.projectWindowItemOnGUI += OnProjectItemGUI;
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;
    }

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

    internal static void RegisterImports(IEnumerable<string> paths)
    {
        var importedPaths = new List<string>();
        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            if (IsIgnored(path)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) continue;
            importedPaths.Add(path);
            _knownGuids.Add(guid);

            var rootPath = GetFirstAssetsFolder(path);
            var rootGuid = AssetDatabase.AssetPathToGUID(rootPath);
            if (!string.IsNullOrEmpty(rootGuid) && !_knownGuids.Contains(rootGuid))
            {
                _knownGuids.Add(rootGuid);
                importedPaths.Add(rootPath);
            }
        }
        if (importedPaths.Count == 0) return;

        var pathSet = new HashSet<string>(importedPaths);
        bool changed = false;
        foreach (var path in importedPaths)
        {
            if (HasAncestorIn(path, pathSet)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (_newGuids.Add(guid)) changed = true;
        }
        Save();
        if (changed) EditorApplication.RepaintProjectWindow();
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

    private static string GetFirstAssetsFolder(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        path = path.Replace('\\', '/');
        const string prefix = "Assets/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return "";

        var rest = path.Substring(prefix.Length);
        var slash = rest.IndexOf('/');
        if (slash < 0) return "";

        var first = rest.Substring(0, slash);
        return string.IsNullOrEmpty(first) ? "" : prefix + first;
    }

    private static void OnSelectionChanged()
    {
        if (_newGuids.Count == 0) return;

        bool changed = false;
        foreach (var obj in Selection.objects)
        {
            if (obj == null) continue;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (_newGuids.Remove(guid)) changed = true;

            var rootPath = GetFirstAssetsFolder(path);
            var rootGuid = AssetDatabase.AssetPathToGUID(rootPath);
            if (!string.IsNullOrEmpty(rootGuid) && _newGuids.Remove(rootGuid)) changed = true;
        }
        if (changed)
        {
            Save();
            EditorApplication.RepaintProjectWindow();
        }
    }

    private static void OnEditorUpdate()
    {
        if (_newGuids.Count == 0) return;

        var folder = GetCurrentProjectBrowserFolder();
        if (string.IsNullOrEmpty(folder) || folder == _lastProjectFolder) return;

        _lastProjectFolder = folder;
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
        DiNeNewAssetBadge.RegisterImports(imported);
    }
}
#endif

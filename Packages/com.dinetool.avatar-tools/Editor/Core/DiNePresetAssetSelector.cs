#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프로젝트 프리셋 자동 검색, 마지막 선택 복원, 공통 드롭다운 UI를 제공한다.
/// 선택은 에셋 GUID로 기억하고, 에셋이 사라진 경우 저장 경로/이름과 가장 가까운 프리셋을 고른다.
/// </summary>
internal static class DiNePresetAssetSelector
{
    private static readonly Color PopupGray = new Color(0.50f, 0.50f, 0.50f, 1f);

    public static string[] FindPresetPaths<T>() where T : UnityEngine.Object
    {
        return AssetDatabase
            .FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<T>(path) != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static int RestoreSelection(
        string preferenceKey,
        IReadOnlyList<string> paths,
        string preferredPath = null,
        string contextAssetPath = null)
    {
        if (paths == null || paths.Count == 0)
            return -1;

        int exact = IndexOf(paths, preferredPath);
        if (exact >= 0)
        {
            RememberSelection(preferenceKey, paths[exact]);
            return exact;
        }

        string savedGuid = EditorPrefs.GetString(preferenceKey + ".Guid", string.Empty);
        if (!string.IsNullOrEmpty(savedGuid))
        {
            exact = IndexOf(paths, AssetDatabase.GUIDToAssetPath(savedGuid));
            if (exact >= 0)
            {
                RememberSelection(preferenceKey, paths[exact]);
                return exact;
            }
        }

        string savedPath = EditorPrefs.GetString(preferenceKey + ".Path", string.Empty);
        exact = IndexOf(paths, savedPath);
        if (exact >= 0)
        {
            RememberSelection(preferenceKey, paths[exact]);
            return exact;
        }

        string referencePath = !string.IsNullOrEmpty(preferredPath) ? preferredPath : savedPath;
        string referenceName = EditorPrefs.GetString(preferenceKey + ".Name", string.Empty);
        if (string.IsNullOrEmpty(referenceName) && !string.IsNullOrEmpty(referencePath))
            referenceName = Path.GetFileNameWithoutExtension(referencePath);

        int closest = FindClosest(paths, referencePath, referenceName, contextAssetPath);
        RememberSelection(preferenceKey, paths[closest]);
        return closest;
    }

    public static int DrawPopup(
        GUIContent label,
        int selectedIndex,
        IReadOnlyList<string> paths,
        string emptyLabel)
    {
        bool hasPresets = paths != null && paths.Count > 0;
        string[] options = hasPresets ? BuildDisplayNames(paths) : new[] { emptyLabel };
        int safeIndex = hasPresets ? Mathf.Clamp(selectedIndex, 0, paths.Count - 1) : 0;

        var style = new GUIStyle(EditorStyles.popup)
        {
            fixedHeight = 28f,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(10, 26, 4, 4),
        };
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;

        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = PopupGray;
        using (new EditorGUI.DisabledScope(!hasPresets))
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);
            safeIndex = EditorGUILayout.Popup(safeIndex, options, style, GUILayout.Height(28f));
        }
        GUI.backgroundColor = previous;

        return hasPresets ? safeIndex : -1;
    }

    public static void RememberSelection(string preferenceKey, string assetPath)
    {
        if (string.IsNullOrEmpty(preferenceKey) || string.IsNullOrEmpty(assetPath))
            return;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid))
            EditorPrefs.SetString(preferenceKey + ".Guid", guid);
        EditorPrefs.SetString(preferenceKey + ".Path", assetPath);
        EditorPrefs.SetString(preferenceKey + ".Name", Path.GetFileNameWithoutExtension(assetPath));
    }

    private static int FindClosest(
        IReadOnlyList<string> paths,
        string referencePath,
        string referenceName,
        string contextAssetPath)
    {
        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < paths.Count; i++)
        {
            string candidate = paths[i];
            string candidateName = Path.GetFileNameWithoutExtension(candidate);
            int score = 0;

            if (!string.IsNullOrEmpty(referencePath))
                score += CommonDirectoryDepth(candidate, referencePath) * 1000;
            if (!string.IsNullOrEmpty(contextAssetPath))
                score += CommonDirectoryDepth(candidate, contextAssetPath) * 100;
            if (!string.IsNullOrEmpty(referenceName))
                score -= EditDistance(candidateName, referenceName) * 10;

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static string[] BuildDisplayNames(IReadOnlyList<string> paths)
    {
        var duplicateCounts = paths
            .Select(Path.GetFileNameWithoutExtension)
            .GroupBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.CurrentCultureIgnoreCase);

        var labels = new string[paths.Count];
        for (int i = 0; i < paths.Count; i++)
        {
            string name = Path.GetFileNameWithoutExtension(paths[i]);
            if (duplicateCounts[name] <= 1)
            {
                labels[i] = name;
                continue;
            }

            string parent = Path.GetDirectoryName(paths[i])?.Replace('\\', '/');
            labels[i] = string.IsNullOrEmpty(parent) ? name : name + "  ·  " + parent;
        }
        return labels;
    }

    private static int IndexOf(IReadOnlyList<string> paths, string target)
    {
        if (string.IsNullOrEmpty(target)) return -1;
        for (int i = 0; i < paths.Count; i++)
            if (string.Equals(paths[i], target, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private static int CommonDirectoryDepth(string leftPath, string rightPath)
    {
        string left = (Path.GetDirectoryName(leftPath) ?? string.Empty).Replace('\\', '/');
        string right = (Path.GetDirectoryName(rightPath) ?? string.Empty).Replace('\\', '/');
        string[] leftParts = left.Split('/');
        string[] rightParts = right.Split('/');
        int common = 0;
        while (common < leftParts.Length && common < rightParts.Length &&
               string.Equals(leftParts[common], rightParts[common], StringComparison.OrdinalIgnoreCase))
            common++;
        return common;
    }

    private static int EditDistance(string left, string right)
    {
        left = (left ?? string.Empty).ToLowerInvariant();
        right = (right ?? string.Empty).ToLowerInvariant();
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (int j = 0; j <= right.Length; j++) previous[j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= right.Length; j++)
            {
                int substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }
            var swap = previous;
            previous = current;
            current = swap;
        }
        return previous[right.Length];
    }
}
#endif

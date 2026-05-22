#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DiNePackageAssets
{
    private static readonly string[] CandidateRoots =
    {
        "Packages/com.dine.tool",
        "Packages/com.dinetool.avatar-tools",
    };

    private static string _packageRoot;

    public static string PackageRoot => _packageRoot ??= ResolvePackageRoot();

    public static string GetAssetPath(string relativePath)
    {
        return $"{PackageRoot}/{relativePath.TrimStart('/')}";
    }

    public static T LoadAsset<T>(string relativePath) where T : Object
    {
        string normalized = relativePath.TrimStart('/').Replace('\\', '/');
        T asset = AssetDatabase.LoadAssetAtPath<T>(GetAssetPath(normalized));
        if (asset != null)
            return asset;

        string fileName = Path.GetFileNameWithoutExtension(normalized);
        if (!string.IsNullOrEmpty(fileName))
        {
            string typeName = typeof(T) == typeof(Font) ? "t:Font" : $"t:{typeof(T).Name}";
            string[] guids = AssetDatabase.FindAssets($"{fileName} {typeName}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (path.EndsWith("/" + normalized, System.StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(path).Equals(fileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    asset = AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null)
                        return asset;
                }
            }
        }

        return null;
    }

    private static string ResolvePackageRoot()
    {
        foreach (string candidate in CandidateRoots)
        {
            if (AssetDatabase.IsValidFolder(candidate))
                return candidate;
        }

        string[] fontGuids = AssetDatabase.FindAssets("DungGeunMo t:Font");
        foreach (string guid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (path.EndsWith("/DungGeunMo.ttf"))
                return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? CandidateRoots[0];
        }

        string[] asmdefGuids = AssetDatabase.FindAssets("DiNeTool.Runtime t:asmdef");
        foreach (string guid in asmdefGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            const string suffix = "/Runtime/DiNeTool.Runtime.asmdef";
            if (path.EndsWith(suffix))
                return path.Substring(0, path.Length - suffix.Length);
        }

        return CandidateRoots[0];
    }
}
#endif

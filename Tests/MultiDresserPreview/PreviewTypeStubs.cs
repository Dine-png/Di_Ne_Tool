// Isolated test-project dependencies only. These are not shipped in the package.
// The inspector/runtime are production code; NDMF counters prove invalidation calls,
// not the behavior or performance of a real NDMF/Modular Avatar installation.
#if UNITY_EDITOR
using UnityEngine;
public static class DiNeMultiDresserAutoApply
{
    public static void ForceRestoreNow(string reason) { }
}
public static class DiNeIconMaker
{
    public static int Captures;
    public static bool FailCapture;
    public class Settings
    {
        public bool outlineEnabled, forbiddenOverlay, forbiddenBehindObject;
        public Color outlineColor;
        public int outlineSize;
        public float forbiddenOpacity, forbiddenScale;
    }
    public static string GetDefaultIconAssetPath(string name) => "Assets/TestIcons/" + name + ".png";
    public static bool CanOverwriteAsset(string path) => path != null && path.StartsWith("Assets/") && path.EndsWith(".png");
    public static Texture2D GenerateIcon(GameObject target, object linked, string path, Settings settings)
    {
        Captures++;
        if (FailCapture) return null;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        var texture = new Texture2D(4, 4);
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        UnityEditor.AssetDatabase.ImportAsset(path);
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
namespace DiNeScreenSaver
{
    public static class DiNeScreenSaver
    {
        public static void OpenIconEditor(GameObject target, Texture2D texture,
            DiNeMultiDresser dresser, int layer, int button, object callback) { }
        public static void OpenIconEditor(DiNeSmartToggle toggle) { }
        public static Texture2D GenerateConfiguredIcon(GameObject target, Vector2 euler, Vector2 pan,
            float zoom, DiNeIconMaker.Settings settings, string path, bool idle)
            => DiNeIconMaker.GenerateIcon(target, null, path, settings);
    }
}
namespace nadena.dev.ndmf.preview
{
    public static class PropCacheDebug
    {
        public static int Invalidations;
        public static void InvalidateAllCaches() { Invalidations++; }
    }
    public static class ComputeContext
    {
        public static int Flushes;
        public static void FlushInvalidates() { Flushes++; }
    }
}
namespace nadena.dev.ndmf.cs
{
    public sealed class ObjectWatcher
    {
        public static ObjectWatcher Instance { get; } = new ObjectWatcher();
        public readonly TestHierarchy Hierarchy = new TestHierarchy();
    }
    public sealed class TestHierarchy
    {
        public int Notifications;
        public void FireObjectChangeNotification(int instanceId) { Notifications++; }
    }
}
#endif

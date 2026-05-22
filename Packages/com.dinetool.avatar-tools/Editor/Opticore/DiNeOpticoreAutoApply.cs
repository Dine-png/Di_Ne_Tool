#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DiNeOpticoreAutoApply
{
    private const string TempCloneSuffix = " [Opticore Temp]";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    private static void OnBeforeAssemblyReload()
    {
        RestorePlayModeSessions("assembly reload");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ApplyAllForPlayMode();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => RestorePlayModeSessions("play mode ended");
        }
    }

    private static void ApplyAllForPlayMode()
    {
        RestorePlayModeSessions("refresh play mode Opticore session");

        var opticores = UnityEngine.Object.FindObjectsOfType<DiNeOpticore>(true)
            .Where(opticore =>
                opticore != null &&
                opticore.isActiveAndEnabled &&
                opticore.gameObject.activeInHierarchy &&
                !HasParentOpticore(opticore) &&
                opticore.GetComponent<DiNeOpticorePlaySessionMarker>() == null)
            .ToArray();

        foreach (var opticore in opticores)
        {
            try
            {
                CreatePlayModeClone(opticore);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DiNe] Failed to create Opticore play mode clone for '{opticore.name}': {e.Message}\n{e.StackTrace}");
            }
        }
    }

    private static void CreatePlayModeClone(DiNeOpticore opticore)
    {
        if (opticore == null)
            return;

        GameObject source = opticore.gameObject;
        if (source == null)
            return;

        string sessionId = Guid.NewGuid().ToString("N");
        Transform parent = source.transform.parent;

        var clone = UnityEngine.Object.Instantiate(source, parent);
        clone.name = source.name + TempCloneSuffix;
        clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

        var sourceMarker = source.GetComponent<DiNeOpticorePlaySessionMarker>();
        if (sourceMarker == null)
            sourceMarker = source.AddComponent<DiNeOpticorePlaySessionMarker>();
        sourceMarker.SessionId = sessionId;
        sourceMarker.Role = DiNeOpticorePlaySessionMarker.MarkerRole.Source;
        sourceMarker.WasActiveSelf = source.activeSelf;

        var cloneMarker = clone.AddComponent<DiNeOpticorePlaySessionMarker>();
        cloneMarker.SessionId = sessionId;
        cloneMarker.Role = DiNeOpticorePlaySessionMarker.MarkerRole.Clone;
        cloneMarker.WasActiveSelf = true;

        DiNeOpticorePreviewUtility.ApplyOptimizationsInPlace(clone, opticore, true);
        source.SetActive(false);
    }

    private static void RestorePlayModeSessions(string reason)
    {
        var markers = UnityEngine.Object.FindObjectsOfType<DiNeOpticorePlaySessionMarker>(true);
        if (markers == null || markers.Length == 0)
            return;

        var groups = markers
            .Where(marker => marker != null && !string.IsNullOrEmpty(marker.SessionId))
            .GroupBy(marker => marker.SessionId)
            .ToArray();

        int restoredCount = 0;
        foreach (var group in groups)
        {
            var sourceMarker = group.FirstOrDefault(marker => marker != null && marker.Role == DiNeOpticorePlaySessionMarker.MarkerRole.Source);
            var cloneMarker = group.FirstOrDefault(marker => marker != null && marker.Role == DiNeOpticorePlaySessionMarker.MarkerRole.Clone);

            if (sourceMarker != null)
            {
                sourceMarker.gameObject.SetActive(sourceMarker.WasActiveSelf);
                UnityEngine.Object.DestroyImmediate(sourceMarker);
                restoredCount++;
            }

            if (cloneMarker != null)
                UnityEngine.Object.DestroyImmediate(cloneMarker.gameObject);
        }

        if (restoredCount > 0)
            Debug.Log($"[DiNe] Restored Opticore temporary play mode clones after {reason}.");
    }

    private static bool HasParentOpticore(DiNeOpticore opticore)
    {
        if (opticore == null)
            return false;

        Transform current = opticore.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<DiNeOpticore>() != null)
                return true;

            current = current.parent;
        }

        return false;
    }
}

[DisallowMultipleComponent]
public sealed class DiNeOpticorePlaySessionMarker : MonoBehaviour
{
    public enum MarkerRole
    {
        Source,
        Clone,
    }

    [SerializeField] private string _sessionId;
    [SerializeField] private MarkerRole _role;
    [SerializeField] private bool _wasActiveSelf;

    public string SessionId
    {
        get => _sessionId;
        set => _sessionId = value;
    }

    public MarkerRole Role
    {
        get => _role;
        set => _role = value;
    }

    public bool WasActiveSelf
    {
        get => _wasActiveSelf;
        set => _wasActiveSelf = value;
    }
}
#endif

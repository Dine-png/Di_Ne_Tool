#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One reversible object-state preview across both toggle inspectors and locked inspectors.
[InitializeOnLoad]
public static class DiNeTogglePreview
{
    private const string SnapshotKey = "DiNe.TogglePreview.ObjectStates";
    private static readonly Dictionary<GameObject, bool> OriginalStates = new Dictionary<GameObject, bool>();
    private static Editor activeEditor;
    private static int activeSlot = -1;
    private static bool previewOn;
    private static GUIStyle selectedButtonStyle;

    static DiNeTogglePreview()
    {
        Selection.selectionChanged += Clear;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
        Undo.undoRedoPerformed += Clear;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Clear();
        };
        EditorSceneManager.sceneSaving += (scene, path) => Clear();
        EditorSceneManager.sceneClosing += (scene, removingScene) => Clear();
        PrefabStage.prefabSaving += root => Clear();
        EditorApplication.delayCall += () => { if (activeEditor == null) Clear(); };
    }

    public static bool IsActive(Editor editor, int slot)
        => ReferenceEquals(activeEditor, editor) && activeSlot == slot && OriginalStates.Count > 0;

    public static bool Begin(Editor editor, int slot, IEnumerable<GameObject> targets, bool on)
    {
        if (editor == null || targets == null || EditorApplication.isPlayingOrWillChangePlaymode) return false;
        // Restore the other preview before capturing a new baseline, even when targets overlap.
        DiNeMultiSupporter.ClearAllActivePreviews();
        foreach (var go in targets)
            if (go != null && !EditorUtility.IsPersistent(go) && go.scene.IsValid() && !OriginalStates.ContainsKey(go))
                OriginalStates.Add(go, go.activeSelf);
        if (OriginalStates.Count == 0) return false;
        activeEditor = editor;
        activeSlot = slot;
        var snapshot = new StringBuilder();
        foreach (var pair in OriginalStates)
            snapshot.Append(pair.Key.GetInstanceID()).Append(':').Append(pair.Value ? '1' : '0').Append(';');
        SessionState.SetString(SnapshotKey, snapshot.ToString());
        SetState(on);
        return true;
    }

    public static void SetState(bool on)
    {
        if (OriginalStates.Count == 0) return;
        previewOn = on;
        foreach (var go in OriginalStates.Keys.ToArray()) DiNeMultiSupporter.SafeSetActive(go, on);
        DiNeMultiSupporter.ForcePreviewRepaint();
    }

    public static void ClearForOwner(Editor editor)
    {
        if (ReferenceEquals(activeEditor, editor)) Clear();
    }

    public static void Clear()
    {
        bool hadPreview = OriginalStates.Count > 0;
        var states = OriginalStates.ToArray();
        OriginalStates.Clear();
        activeEditor = null;
        activeSlot = -1;
        string snapshot = SessionState.GetString(SnapshotKey, "");
        SessionState.EraseString(SnapshotKey);
        for (int i = states.Length - 1; i >= 0; i--)
            DiNeMultiSupporter.SafeSetActive(states[i].Key, states[i].Value);

        // SessionState survives domain reload, so an interrupted inspector also restores its objects.
        if (states.Length == 0 && snapshot.Length > 0)
        {
            foreach (string entry in snapshot.Split(';'))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int id)) continue;
                var go = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (go == null) continue;
                DiNeMultiSupporter.SafeSetActive(go, parts[1] == "1");
                hadPreview = true;
            }
        }
        if (hadPreview) DiNeMultiSupporter.ForcePreviewRepaint();
    }

    // Only explicit button clicks change object states; Layout/Repaint never updates previews.
    public static void DrawStateControls(Editor editor, int slot)
    {
        if (!IsActive(editor, slot)) return;
        if (selectedButtonStyle == null)
            selectedButtonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        int language = Mathf.Clamp(EditorPrefs.GetInt("DiNeLang", 0), 0, 2);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(language == 1 ? "미리보기 상태" : language == 2 ? "プレビュー状態" : "Preview State", EditorStyles.boldLabel);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = previewOn ? new Color(0.30f, 0.82f, 0.76f) : previous;
            if (GUILayout.Button("ON", previewOn ? selectedButtonStyle : GUI.skin.button, GUILayout.Height(24)) && !previewOn) SetState(true);
            GUI.backgroundColor = !previewOn ? new Color(0.30f, 0.82f, 0.76f) : previous;
            if (GUILayout.Button("OFF", !previewOn ? selectedButtonStyle : GUI.skin.button, GUILayout.Height(24)) && previewOn) SetState(false);
            GUI.backgroundColor = previous;
        }
    }
}
#endif

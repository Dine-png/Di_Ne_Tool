#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DiNeTool.ExpressionEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

// Captures only the isolated test window's GUIView into a render texture.
// No desktop/screen pixel API is used.
public static class ExpressionEditorUiCapture
{
    [Serializable] private sealed class Preferences
    {
        public bool languagePresent, modePresent, mode;
        public int language;
    }
    private const string Manifest = "ExpressionEditorUiCapture/preferences-restore.json";
    private static Preferences previous;
    private static readonly List<string> Results = new List<string>();
    private static ExpressionEditorUiProbeWindow window;
    private static UnityEditor.Editor inspector;
    private static VRCExpressionsMenu menu;
    private static VRCExpressionParameters parameters;
    private static int index, frames;
    private static double started;
    private static string before;

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("ExpressionEditorUiCapture");
            if (File.Exists(Manifest)) { Restore(JsonUtility.FromJson<Preferences>(File.ReadAllText(Manifest))); File.Delete(Manifest); }
            previous = new Preferences {
                languagePresent = EditorPrefs.HasKey("DiNeLang"), language = EditorPrefs.GetInt("DiNeLang", 0),
                modePresent = EditorPrefs.HasKey("DiNeExpressionInspectorEnabled"), mode = EditorPrefs.GetBool("DiNeExpressionInspectorEnabled", true)
            };
            File.WriteAllText(Manifest, JsonUtility.ToJson(previous, true));
            EditorPrefs.SetBool("DiNeExpressionInspectorEnabled", true);
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.hideFlags = HideFlags.DontSave;
            menu.controls = new List<VRCExpressionsMenu.Control> {
                new VRCExpressionsMenu.Control {
                    name = "Movement", type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "Enabled" },
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "Up" }, new VRCExpressionsMenu.Control.Parameter { name = "Right" }, new VRCExpressionsMenu.Control.Parameter { name = "Down" }, new VRCExpressionsMenu.Control.Parameter { name = "Left" } },
                    labels = new[] { new VRCExpressionsMenu.Control.Label { name = "Up" }, new VRCExpressionsMenu.Control.Label { name = "Right" }, new VRCExpressionsMenu.Control.Label { name = "Down" }, new VRCExpressionsMenu.Control.Label { name = "Left" } }
                },
                new VRCExpressionsMenu.Control { name = "Toggle", type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = "Toggle" }, value = 1f }
            };
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.hideFlags = HideFlags.DontSave;
            parameters.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Toggle", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Choice", valueType = VRCExpressionParameters.ValueType.Int, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Amount", valueType = VRCExpressionParameters.ValueType.Float, networkSynced = false }
            };
            window = ScriptableObject.CreateInstance<ExpressionEditorUiProbeWindow>();
            window.ShowUtility(); window.Focus();
            started = EditorApplication.timeSinceStartup;
            Prepare();
            EditorApplication.update += Tick;
        }
        catch (Exception e) { Finish(e); }
    }

    private static void Prepare()
    {
        if (inspector != null) Object.DestroyImmediate(inspector);
        EditorPrefs.SetInt("DiNeLang", index % 3);
        Object target = index < 3 ? (Object)menu : parameters;
        before = EditorJsonUtility.ToJson(target);
        inspector = UnityEditor.Editor.CreateEditor(target, index < 3 ? typeof(DiNeExpressionMenuEditor) : typeof(DiNeExpressionParametersEditor));
        // Host the actual custom inspector's native root just as Unity's Inspector does.
        // A shorter viewport avoids oversized hidden native-window surfaces.
        window.Inspector = null;
        window.rootVisualElement.Clear();
        var root = inspector.CreateInspectorGUI();
        root.Bind(inspector.serializedObject);
        window.rootVisualElement.Add(root);
        window.ContentWidth = 480; window.position = new Rect(40, 40, 480, 850);
        window.Error = null; window.Height = -1; window.Focus(); window.Repaint(); frames = 0;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 60) throw new InvalidOperationException("Isolated UI capture exceeded 60-second bound.");
            if (++frames < 10) { window.Repaint(); return; }
            window.RenderFrame();
            if (window.Error != null) throw window.Error;
            string file = "ExpressionEditorUiCapture/" + (index < 3 ? "menu" : "parameters") + "-" + index % 3 + ".png";
            window.Capture(file, 480, 850);
            if (EditorJsonUtility.ToJson(inspector.target) != before) throw new InvalidOperationException("UI capture mutated its asset.");
            Results.Add("PASS " + Path.GetFullPath(file) + "; native inspector root; viewport=480x850");
            if (++index == 6) Finish(null); else Prepare();
        }
        catch (Exception e) { Finish(e); }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        try
        {
            if (window != null) { window.Inspector = null; window.Close(); }
            if (inspector != null) Object.DestroyImmediate(inspector);
            if (menu != null) Object.DestroyImmediate(menu);
            if (parameters != null) Object.DestroyImmediate(parameters);
            if (previous != null)
            {
                Restore(previous);
                bool restored = EditorPrefs.HasKey("DiNeLang") == previous.languagePresent && (!previous.languagePresent || EditorPrefs.GetInt("DiNeLang") == previous.language) &&
                    EditorPrefs.HasKey("DiNeExpressionInspectorEnabled") == previous.modePresent && (!previous.modePresent || EditorPrefs.GetBool("DiNeExpressionInspectorEnabled") == previous.mode);
                if (!restored) throw new InvalidOperationException("Preference restoration failed; manifest preserved.");
                File.Delete(Manifest);
                Results.Add("PASS global language/inspector preferences restored and verified");
            }
        }
        catch (Exception cleanup) { error = cleanup; }
        if (error != null) Results.Add("FAIL " + error);
        File.WriteAllLines("ExpressionEditorRegression-ui-capture-results.txt", Results);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

    private static void Restore(Preferences preferences)
    {
        if (preferences.languagePresent) EditorPrefs.SetInt("DiNeLang", preferences.language); else EditorPrefs.DeleteKey("DiNeLang");
        if (preferences.modePresent) EditorPrefs.SetBool("DiNeExpressionInspectorEnabled", preferences.mode); else EditorPrefs.DeleteKey("DiNeExpressionInspectorEnabled");
    }
}
#endif

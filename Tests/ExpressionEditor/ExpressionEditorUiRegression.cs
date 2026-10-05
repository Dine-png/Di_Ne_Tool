#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DiNeTool.ExpressionEditor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

// Hosts actual CreateInspectorGUI roots, with real Editor update frames for
// custom IMGUI and official SDK binding, virtualization and layout.
public static class ExpressionEditorUiRegression
{
    private sealed class Scenario { public string Name; public Object Asset; public bool NativeShape; }
    private const string Manifest = "ExpressionEditorUiRegression/preferences-restore.json";
    private static readonly List<Scenario> Scenarios = new List<Scenario>();
    private static readonly List<string> Measurements = new List<string>();
    private static ExpressionEditorUiPreferences previous;
    private static ExpressionEditorUiProbeWindow window;
    private static UnityEditor.Editor inspector;
    private static VisualElement root;
    private static Action<Exception> finished;
    private static int index, frames;
    private static double started;
    private static string before;

    public static void Verify(Action<Exception> onFinished)
    {
        finished = onFinished; index = 0; Measurements.Clear(); Scenarios.Clear();
        try
        {
            previous = ExpressionEditorUiPreferences.SaveWithRecovery(Manifest);
            var emptyMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            emptyMenu.controls = new List<VRCExpressionsMenu.Control>();
            Add("empty-menu", emptyMenu, true);
            var puppetMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            puppetMenu.controls = new List<VRCExpressionsMenu.Control> {
                new VRCExpressionsMenu.Control {
                    name = "Movement", type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "Enabled" },
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "Up" }, new VRCExpressionsMenu.Control.Parameter { name = "Right" }, new VRCExpressionsMenu.Control.Parameter { name = "Down" }, new VRCExpressionsMenu.Control.Parameter { name = "Left" } },
                    labels = new[] { new VRCExpressionsMenu.Control.Label { name = "Up" }, new VRCExpressionsMenu.Control.Label { name = "Right" }, new VRCExpressionsMenu.Control.Label { name = "Down" }, new VRCExpressionsMenu.Control.Label { name = "Left" } }
                },
                new VRCExpressionsMenu.Control { name = "Toggle", type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = "Toggle" }, value = 1f }
            };
            Add("puppet-menu", puppetMenu, true);
            var emptyParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            emptyParameters.isEmpty = true; emptyParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            Add("intentional-empty-parameters", emptyParameters, true);
            var pristineParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            pristineParameters.isEmpty = false; pristineParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            Add("pristine-empty-parameters", pristineParameters, true);
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Toggle", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Choice", valueType = VRCExpressionParameters.ValueType.Int, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Amount", valueType = VRCExpressionParameters.ValueType.Float, networkSynced = false }
            };
            Add("populated-parameters", parameters, true);
            var damagedParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); damagedParameters.parameters = null;
            Add("null-parameter-array", damagedParameters, false);
            var damagedMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            damagedMenu.controls = new List<VRCExpressionsMenu.Control> {
                new VRCExpressionsMenu.Control {
                    name = "Malformed Puppet", type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter(),
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "OnlyOne" } },
                    labels = Array.Empty<VRCExpressionsMenu.Control.Label>()
                }
            };
            Add("malformed-puppet", damagedMenu, false);
            window = ScriptableObject.CreateInstance<ExpressionEditorUiProbeWindow>(); window.ShowUtility();
            started = EditorApplication.timeSinceStartup; Prepare(); EditorApplication.update += Tick;
        }
        catch (Exception error) { Finish(error); }
    }

    private static void Add(string name, Object asset, bool nativeShape)
    {
        asset.hideFlags = HideFlags.DontSave;
        Scenarios.Add(new Scenario { Name = name, Asset = asset, NativeShape = nativeShape });
    }

    private static void Prepare()
    {
        if (inspector != null) Object.DestroyImmediate(inspector);
        var scenario = Scenarios[index / 18];
        int language = index % 3, width = new[] { 320, 480, 700 }[(index / 3) % 3];
        bool enhanced = (index / 9) % 2 == 1;
        EditorPrefs.SetInt("DiNeLang", language); EditorPrefs.SetBool("DiNeExpressionInspectorEnabled", enhanced);
        EditorPrefs.SetBool("DiNeExpressionMenuCompact", false);
        before = EditorJsonUtility.ToJson(scenario.Asset);
        inspector = UnityEditor.Editor.CreateEditor(scenario.Asset,
            scenario.Asset is VRCExpressionsMenu ? typeof(DiNeExpressionMenuEditor) : typeof(DiNeExpressionParametersEditor));
        root = inspector.CreateInspectorGUI();
        if (root == null) throw new InvalidOperationException("Inspector created no root: " + scenario.Name);
        AssertInspectorMode(root, scenario.Asset, enhanced, scenario.NativeShape);
        window.Host(root, inspector.serializedObject, width, 1600); frames = 0;
        if (EditorJsonUtility.ToJson(scenario.Asset) != before)
            throw new InvalidOperationException("Inspector initialization changed its asset: " + scenario.Name);
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180)
                throw new InvalidOperationException("Native Inspector layout verification exceeded 180-second bound.");
            window.Repaint(); if (++frames < 8) return; window.RenderFrame();
            if (window.Error != null) throw window.Error;
            var scenario = Scenarios[index / 18]; float height = root.layout.height;
            if (float.IsNaN(height) || float.IsInfinity(height) || height <= 0)
                throw new InvalidOperationException("Native Inspector measured no content: " + scenario.Name);
            if (EditorJsonUtility.ToJson(scenario.Asset) != before)
                throw new InvalidOperationException("Inspector binding/repaint changed its asset: " + scenario.Name);
            bool enhanced = (index / 9) % 2 == 1;
            AssertInspectorMode(root, scenario.Asset, enhanced, scenario.NativeShape);
            if (enhanced) AssertParityGeometry(inspector);
            // Wide English fixtures exercise the visible row-side buttons through
            // actual IMGUI mouse events. Undo restores each synthetic asset.
            if (enhanced && index % 3 == 0 && (index / 3) % 3 == 2 &&
                (scenario.Name == "puppet-menu" || scenario.Name == "populated-parameters"))
                VerifyRowButtons(scenario.Asset);
            Measurements.Add(scenario.Name + "; mode=" + ((index / 9) % 2 == 1 ? "enhanced" : "SDK-only") +
                "; width=" + new[] { 320, 480, 700 }[(index / 3) % 3] + "; language=" + index % 3 +
                "; height=" + height + "; native-shape=" + scenario.NativeShape +
                (enhanced ? "; " + RenderedRowMetrics(inspector) : ""));
            if (++index == Scenarios.Count * 18) Finish(null); else Prepare();
        }
        catch (Exception error) { Finish(error); }
    }

    public static void AssertNativeShape(VisualElement root, Object asset)
    {
        string[] names = asset is VRCExpressionsMenu
            ? new[] { "ControlsListView", "ControlOptionsContainer" } : new[] { "ParametersListView", "MemoryBar" };
        foreach (string name in names)
        {
            int count = root.Query<VisualElement>(name).ToList().Count;
            // The official menu and nested control-options UXML both use this
            // name. A selected control therefore has two legitimate containers.
            bool valid = name == "ControlOptionsContainer" ? count >= 1 : count == 1;
            if (!valid) throw new InvalidOperationException("Official SDK Inspector element is missing or unexpectedly duplicated: " + name);
        }
    }

    public static void AssertInspectorMode(VisualElement root, Object asset, bool enhanced, bool nativeExpected = true)
    {
        int ownCount = root.Query<IMGUIContainer>("DiNeExpressionInspectorIMGUI").ToList().Count;
        if (ownCount != (enhanced ? 1 : 0))
            throw new InvalidOperationException("SDK+ IMGUI inspector count differs from the selected mode.");
        bool hasNativeList = root.Q("ControlsListView") != null || root.Q("ParametersListView") != null;
        if (enhanced || !nativeExpected)
        {
            if (hasNativeList) throw new InvalidOperationException("Custom or damaged-data mode unexpectedly hosted the SDK list.");
        }
        else AssertNativeShape(root, asset);
        foreach (string oldPanel in new[] { "DiNeMenuActions", "DiNeMenuOptions", "DiNeMergeOptions", "DiNeParameterWarning" })
            if (root.Q(oldPanel) != null) throw new InvalidOperationException("Obsolete optional panel remains in the parity inspector: " + oldPanel);
    }

    private static Dictionary<string, Rect> GetGeometry(UnityEditor.Editor editor)
    {
        string name = editor.target is VRCExpressionsMenu ? "GuiRects" : "Geometry";
        var field = editor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        return (Dictionary<string, Rect>)field.GetValue(editor);
    }

    public static void AssertParityGeometry(UnityEditor.Editor editor)
    {
        var geometry = GetGeometry(editor);
        string[] required = editor.target is VRCExpressionsMenu
            ? new[] { "MenuHistory", "ControlsHeader", "Copy", "Paste", "Duplicate", "MovePlace", "ControlMain" }
            : new[] { "ParameterHeader", "ParameterAdd", "MergeField", "Memory" };
        foreach (string key in required)
            if (!geometry.TryGetValue(key, out Rect rect) || rect.width <= 0 || rect.height <= 0)
                throw new InvalidOperationException("SDK+ inspector omitted a visible UI region: " + key);
        int count = editor.target is VRCExpressionsMenu menu ? menu.controls.Count :
            (editor.target as VRCExpressionParameters).parameters?.Length ?? 0;
        for (int row = 0; row < count; row++)
        {
            string prefix = editor.target is VRCExpressionsMenu ? "Controls" : "Parameter";
            geometry.TryGetValue(prefix + "Row" + row, out Rect rowRect);
            geometry.TryGetValue(prefix + "Delete" + row, out Rect deleteRect);
            // Native ReorderableList row allocation includes spacing. Its
            // visible side button keeps the single-line SDK+ shape.
            if (rowRect.height > 32 || rowRect.height <= 0 || rowRect.width <= 0 ||
                deleteRect.height > 22 || deleteRect.height <= 0 || deleteRect.width <= 0 || deleteRect.x < rowRect.center.x)
                throw new InvalidOperationException("SDK+ row is missing its compact right-side delete button: " + row +
                    "; row=" + rowRect + "; delete=" + deleteRect);
        }
    }

    public static string RenderedRowMetrics(UnityEditor.Editor editor)
    {
        var geometry = GetGeometry(editor);
        string prefix = editor.target is VRCExpressionsMenu ? "Controls" : "Parameter";
        if (!geometry.TryGetValue(prefix + "Row0", out Rect row)) return "row-height=none";
        geometry.TryGetValue(prefix + "Delete0", out Rect delete);
        return "row-height=" + row.height + "; delete-height=" + delete.height;
    }

    private static void VerifyRowButtons(Object asset)
    {
        var geometry = GetGeometry(inspector);
        bool menu = asset is VRCExpressionsMenu;
        int count = menu ? ((VRCExpressionsMenu)asset).controls.Count : ((VRCExpressionParameters)asset).parameters.Length;
        Undo.IncrementCurrentGroup();
        window.Click(geometry[menu ? "ControlsDelete0" : "ParameterDelete0"]);
        int reduced = menu ? ((VRCExpressionsMenu)asset).controls.Count : ((VRCExpressionParameters)asset).parameters.Length;
        if (reduced != count - 1) throw new InvalidOperationException("The actual row-side delete button did not remove its row.");
        Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); inspector.serializedObject.Update(); window.RenderFrame();
        if (EditorJsonUtility.ToJson(asset) != before) throw new InvalidOperationException("Actual row-side delete Undo did not restore its asset.");
        if (!menu)
        {
            geometry = GetGeometry(inspector); Undo.IncrementCurrentGroup(); window.Click(geometry["ParameterAdd"]);
            if (((VRCExpressionParameters)asset).parameters.Length != count + 1)
                throw new InvalidOperationException("The actual parameter footer plus button did not append a row.");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); inspector.serializedObject.Update(); window.RenderFrame();
            if (EditorJsonUtility.ToJson(asset) != before) throw new InvalidOperationException("Actual footer plus Undo did not restore its asset.");
        }
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        try
        {
            if (window != null) window.Close(); if (inspector != null) Object.DestroyImmediate(inspector);
            foreach (var scenario in Scenarios) if (scenario.Asset != null) Object.DestroyImmediate(scenario.Asset);
            if (previous != null) previous.RestoreAndVerify(Manifest);
        }
        catch (Exception cleanup) { error = cleanup; }
        File.WriteAllLines("ExpressionEditorRegression-ui-layout.txt", Measurements);
        var callback = finished; finished = null; callback?.Invoke(error);
    }
}

// A disk manifest protects global preferences if an isolated Editor is interrupted.
[Serializable]
internal sealed class ExpressionEditorUiPreferences
{
    public bool languagePresent, modePresent, mode, compactPresent, compact;
    public int language;

    public static ExpressionEditorUiPreferences SaveWithRecovery(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) JsonUtility.FromJson<ExpressionEditorUiPreferences>(File.ReadAllText(path)).RestoreAndVerify(path);
        var state = new ExpressionEditorUiPreferences {
            languagePresent = EditorPrefs.HasKey("DiNeLang"), language = EditorPrefs.GetInt("DiNeLang", 0),
            modePresent = EditorPrefs.HasKey("DiNeExpressionInspectorEnabled"), mode = EditorPrefs.GetBool("DiNeExpressionInspectorEnabled", true),
            compactPresent = EditorPrefs.HasKey("DiNeExpressionMenuCompact"), compact = EditorPrefs.GetBool("DiNeExpressionMenuCompact", false)
        };
        File.WriteAllText(path, JsonUtility.ToJson(state, true)); return state;
    }

    public void RestoreAndVerify(string path)
    {
        if (languagePresent) EditorPrefs.SetInt("DiNeLang", language); else EditorPrefs.DeleteKey("DiNeLang");
        if (modePresent) EditorPrefs.SetBool("DiNeExpressionInspectorEnabled", mode); else EditorPrefs.DeleteKey("DiNeExpressionInspectorEnabled");
        if (compactPresent) EditorPrefs.SetBool("DiNeExpressionMenuCompact", compact); else EditorPrefs.DeleteKey("DiNeExpressionMenuCompact");
        bool restored = EditorPrefs.HasKey("DiNeLang") == languagePresent && (!languagePresent || EditorPrefs.GetInt("DiNeLang") == language) &&
            EditorPrefs.HasKey("DiNeExpressionInspectorEnabled") == modePresent && (!modePresent || EditorPrefs.GetBool("DiNeExpressionInspectorEnabled") == mode) &&
            EditorPrefs.HasKey("DiNeExpressionMenuCompact") == compactPresent && (!compactPresent || EditorPrefs.GetBool("DiNeExpressionMenuCompact") == compact);
        if (!restored) throw new InvalidOperationException("Preference restoration failed; manifest preserved: " + path);
        File.Delete(path);
    }
}

public sealed class ExpressionEditorUiProbeWindow : EditorWindow
{
    // Retained for the capture harness; native roots are hosted directly.
    public UnityEditor.Editor Inspector;
    public int ContentWidth;
    public float Height;
    public Exception Error;

    public void Host(VisualElement root, SerializedObject serialized, int width, int height)
    {
        Inspector = null; rootVisualElement.Clear(); root.Bind(serialized); rootVisualElement.Add(root);
        ContentWidth = width; position = new Rect(40, 40, width, height); Error = null; Height = -1; Repaint();
    }

    public void RenderFrame()
    {
        var view = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
        var repaint = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("RepaintImmediately", BindingFlags.Public | BindingFlags.Instance);
        repaint.Invoke(view, null);
    }

    public void Click(Rect rect)
    {
        Focus();
        SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = rect.center });
        SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = rect.center });
        RenderFrame();
    }

    public bool Capture(string path, int width, int height)
    {
        var view = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
        var grab = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("GrabPixels", BindingFlags.NonPublic | BindingFlags.Instance);
        var render = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false); var previousRender = RenderTexture.active;
        try
        {
            render.Create(); RenderTexture.active = render; GL.Clear(true, true, Color.magenta);
            grab.Invoke(view, new object[] { render, new Rect(0, 0, width, height) });
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            if (new HashSet<Color32>(image.GetPixels32()).Count < 32)
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, image.EncodeToPNG());
            return true;
        }
        finally
        {
            RenderTexture.active = previousRender; render.Release(); Object.DestroyImmediate(render); Object.DestroyImmediate(image);
        }
    }
}
#endif

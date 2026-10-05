#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DiNeTool.ExpressionEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

// Captures only the isolated test window's GUIView into a render texture.
// No desktop/screen pixel API is used.
public static class ExpressionEditorUiCapture
{
    private const string Manifest = "ExpressionEditorUiCapture/preferences-restore.json";
    private static ExpressionEditorUiPreferences previous;
    private static readonly List<string> Results = new List<string>();
    private static ExpressionEditorUiProbeWindow window;
    private static UnityEditor.Editor inspector;
    private static VRCExpressionsMenu menu;
    private static VRCExpressionParameters parameters;
    private static VRCExpressionParameters menuLookup;
    private static GameObject avatarObject;
    private static VRCAvatarDescriptor avatar;
    private static AnimatorController controller;
    private static string controllerPath, avatarBefore, avatarInitial, controllerBefore;
    private static int index, frames;
    private static double started, prepared;
    private static string before;
    private static string lookupBefore;
    private static VisualElement root;

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("ExpressionEditorUiCapture");
            previous = ExpressionEditorUiPreferences.SaveWithRecovery(Manifest);
            index = 0;
            Results.Clear();
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = "Movement Menu";
            menu.hideFlags = HideFlags.DontSave;
            menu.controls = new List<VRCExpressionsMenu.Control> {
                new VRCExpressionsMenu.Control {
                    name = "Movement", type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "Enabled" }, value = 1f,
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "Up" }, new VRCExpressionsMenu.Control.Parameter { name = "Right" }, new VRCExpressionsMenu.Control.Parameter { name = "Down" }, new VRCExpressionsMenu.Control.Parameter { name = "Left" } },
                    labels = new[] { new VRCExpressionsMenu.Control.Label { name = "Up" }, new VRCExpressionsMenu.Control.Label { name = "Right" }, new VRCExpressionsMenu.Control.Label { name = "Down" }, new VRCExpressionsMenu.Control.Label { name = "Left" } }
                }
            };
            menuLookup = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            menuLookup.name = "Menu Parameters";
            menuLookup.hideFlags = HideFlags.DontSave;
            menuLookup.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Enabled", valueType = VRCExpressionParameters.ValueType.Bool },
                new VRCExpressionParameters.Parameter { name = "Toggle", valueType = VRCExpressionParameters.ValueType.Bool },
                new VRCExpressionParameters.Parameter { name = "Up", valueType = VRCExpressionParameters.ValueType.Float },
                new VRCExpressionParameters.Parameter { name = "Right", valueType = VRCExpressionParameters.ValueType.Float },
                new VRCExpressionParameters.Parameter { name = "Down", valueType = VRCExpressionParameters.ValueType.Float },
                new VRCExpressionParameters.Parameter { name = "Left", valueType = VRCExpressionParameters.ValueType.Float }
            };
            DiNeExpressionUtility.SetMenuParameters(menu, menuLookup);
            lookupBefore = EditorJsonUtility.ToJson(menuLookup);
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.name = "Expression Parameters";
            parameters.hideFlags = HideFlags.DontSave;
            parameters.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Toggle", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Choice", valueType = VRCExpressionParameters.ValueType.Int, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Amount", valueType = VRCExpressionParameters.ValueType.Float, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "NeedsController", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true }
            };
            if (!AssetDatabase.IsValidFolder("Assets/UiCaptureFixtures")) AssetDatabase.CreateFolder("Assets", "UiCaptureFixtures");
            controllerPath = AssetDatabase.GenerateUniqueAssetPath("Assets/UiCaptureFixtures/ReferenceFX.controller");
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (string name in new[] { "Enabled", "Toggle" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Choice", AnimatorControllerParameterType.Int);
            foreach (string name in new[] { "Amount", "Up", "Right", "Down", "Left" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            avatarObject = new GameObject("Reference Avatar");
            avatar = avatarObject.AddComponent<VRCAvatarDescriptor>();
            avatar.customExpressions = true; avatar.expressionsMenu = menu; avatar.expressionParameters = menuLookup;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer {
                type = VRCAvatarDescriptor.AnimLayerType.FX, isDefault = false, animatorController = controller } };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            avatarInitial = EditorJsonUtility.ToJson(avatar); controllerBefore = EditorJsonUtility.ToJson(controller);
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
        bool compact = index >= 12;
        EditorPrefs.SetBool("DiNeExpressionInspectorEnabled", compact || index % 6 >= 3);
        EditorPrefs.SetBool("DiNeExpressionMenuCompact", compact);
        Object target = index < 6 || compact ? (Object)menu : parameters;
        // The only assigned descriptor belongs to this isolated capture fixture.
        // Capture the expected references after choosing its own lookup asset.
        avatar.expressionParameters = target is VRCExpressionsMenu ? menuLookup : parameters;
        avatarBefore = EditorJsonUtility.ToJson(avatar);
        before = EditorJsonUtility.ToJson(target);
        inspector = UnityEditor.Editor.CreateEditor(target, target is VRCExpressionsMenu ? typeof(DiNeExpressionMenuEditor) : typeof(DiNeExpressionParametersEditor));
        if (target is VRCExpressionsMenu && (compact || index % 6 >= 3))
        {
            var list = inspector.GetType().GetField("controlsList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inspector);
            list.GetType().GetProperty("index").SetValue(list, 0);
        }
        // Host the actual custom inspector root just as Unity's Inspector does.
        root = inspector.CreateInspectorGUI();
        ExpressionEditorUiRegression.AssertInspectorMode(root, target, compact || index % 6 >= 3);
        window.Host(root, inspector.serializedObject, 700, 900);
        window.Focus(); window.Repaint(); frames = 0; prepared = EditorApplication.timeSinceStartup;
        if (EditorJsonUtility.ToJson(target) != before) throw new InvalidOperationException("UI initialization mutated its asset.");
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new InvalidOperationException("Isolated UI capture exceeded 120-second bound.");
            if (++frames < 24 || EditorApplication.timeSinceStartup - prepared < 1) { window.Repaint(); return; }
            window.Focus(); window.RenderFrame(); window.RenderFrame();
            if (window.Error != null) throw window.Error;
            bool compact = index >= 12, enhanced = compact || index % 6 >= 3;
            string file = "ExpressionEditorUiCapture/" + (compact ? "menu-compact-" :
                (index < 6 ? "menu" : "parameters") + (enhanced ? "-" : "-sdk-")) + index % 3 + ".png";
            if (!window.Capture(file, 700, 900)) { window.Repaint(); return; }
            ExpressionEditorUiRegression.AssertInspectorMode(root, inspector.target, enhanced);
            if (enhanced) ExpressionEditorUiRegression.AssertParityGeometry(inspector);
            if (EditorJsonUtility.ToJson(inspector.target) != before) throw new InvalidOperationException("UI capture mutated its asset.");
            if (DiNeExpressionUtility.GetMenuParameters(menu) != menuLookup || EditorJsonUtility.ToJson(menuLookup) != lookupBefore)
                throw new InvalidOperationException("UI capture changed its menu parameter lookup fixture.");
            if (EditorJsonUtility.ToJson(avatar) != avatarBefore || EditorJsonUtility.ToJson(controller) != controllerBefore ||
                avatar.expressionsMenu != menu || avatar.baseAnimationLayers[0].animatorController != controller || avatar.baseAnimationLayers[0].isDefault)
                throw new InvalidOperationException("UI capture changed its synthetic avatar/controller references or source data.");
            Results.Add("PASS " + Path.GetFullPath(file) + "; actual inspector root; mode=" +
                (compact ? "SDK+ compact" : enhanced ? "SDK+ full" : "SDK-only reference") + "; viewport=700x900" +
                (enhanced ? "; " + ExpressionEditorUiRegression.RenderedRowMetrics(inspector) : ""));
            if (++index == 15) Finish(null); else Prepare();
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
            if (avatar != null)
            {
                avatar.expressionParameters = menuLookup;
                if (EditorJsonUtility.ToJson(avatar) != avatarInitial || EditorJsonUtility.ToJson(controller) != controllerBefore)
                    throw new InvalidOperationException("Synthetic capture references did not restore; fixture retained for diagnosis.");
                Object.DestroyImmediate(avatarObject);
                if (!string.IsNullOrEmpty(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
                Results.Add("PASS synthetic FX flags/controller/menu/parameter references restored and verified");
            }
            if (menu != null) Object.DestroyImmediate(menu);
            if (parameters != null) Object.DestroyImmediate(parameters);
            if (menuLookup != null) Object.DestroyImmediate(menuLookup);
        }
        catch (Exception cleanup) { error = cleanup; }
        // Restore global settings even when fixture validation intentionally
        // retains synthetic assets for diagnosis.
        try
        {
            if (previous != null)
            {
                previous.RestoreAndVerify(Manifest);
                Results.Add("PASS global language/inspector/compact preferences restored and verified");
            }
        }
        catch (Exception cleanup) { error = cleanup; }
        if (error != null) Results.Add("FAIL " + error);
        File.WriteAllLines("ExpressionEditorRegression-ui-capture-results.txt", Results);
        EditorApplication.Exit(error == null ? 0 : 1);
    }

}
#endif

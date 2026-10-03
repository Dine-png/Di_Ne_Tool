#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DiNeTool.ExpressionEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

// Exercises the actual production IMGUI, never a re-created UI or user project.
public static class ExpressionEditorUiRegression
{
    public static void Verify()
    {
        bool hadLanguage = EditorPrefs.HasKey("DiNeLang");
        int oldLanguage = EditorPrefs.GetInt("DiNeLang", 0);
        var measurements = new List<string>();
        var objects = new List<Object>();
        ExpressionEditorUiProbeWindow window = null;
        try
        {
            var emptyMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); objects.Add(emptyMenu);
            emptyMenu.controls = new List<VRCExpressionsMenu.Control>();
            var puppetMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); objects.Add(puppetMenu);
            puppetMenu.controls = new List<VRCExpressionsMenu.Control> {
                new VRCExpressionsMenu.Control {
                    name = "Movement", type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "Enabled" },
                    subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = "Up" }, new VRCExpressionsMenu.Control.Parameter { name = "Right" }, new VRCExpressionsMenu.Control.Parameter { name = "Down" }, new VRCExpressionsMenu.Control.Parameter { name = "Left" } },
                    labels = new[] { new VRCExpressionsMenu.Control.Label { name = "Up" }, new VRCExpressionsMenu.Control.Label { name = "Right" }, new VRCExpressionsMenu.Control.Label { name = "Down" }, new VRCExpressionsMenu.Control.Label { name = "Left" } }
                },
                new VRCExpressionsMenu.Control { name = "Toggle", type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = "Toggle" }, value = 1f }
            };
            var emptyParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); objects.Add(emptyParameters);
            emptyParameters.isEmpty = true; emptyParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); objects.Add(parameters);
            parameters.parameters = new[] {
                new VRCExpressionParameters.Parameter { name = "Toggle", valueType = VRCExpressionParameters.ValueType.Bool, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Choice", valueType = VRCExpressionParameters.ValueType.Int, saved = true, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = "Amount", valueType = VRCExpressionParameters.ValueType.Float, networkSynced = false }
            };
            var damagedParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>(); objects.Add(damagedParameters);
            damagedParameters.parameters = null;
            window = ScriptableObject.CreateInstance<ExpressionEditorUiProbeWindow>();
            window.ShowUtility();
            window.Focus();
            foreach (var target in objects)
            {
                string before = EditorJsonUtility.ToJson(target);
                var type = target is VRCExpressionsMenu ? typeof(DiNeExpressionMenuEditor) : typeof(DiNeExpressionParametersEditor);
                var inspector = UnityEditor.Editor.CreateEditor(target, type);
                try
                {
                    window.Inspector = inspector;
                    foreach (int width in new[] { 320, 480, 700 })
                    foreach (int language in new[] { 0, 1, 2 })
                    {
                        EditorPrefs.SetInt("DiNeLang", language);
                        window.ContentWidth = width;
                        window.position = new Rect(40, 40, width, 2400);
                        window.Error = null; window.Height = -1;
                        for (int frame = 0; frame < 4; frame++) window.RenderFrame();
                        if (window.Error != null) throw new InvalidOperationException("UI rendering failed for " + target.GetType().Name + ", width=" + width + ", language=" + language, window.Error);
                        if (window.Height <= 0) throw new InvalidOperationException("UI measured no content.");
                        if (EditorJsonUtility.ToJson(target) != before) throw new InvalidOperationException("Inspector repaint changed its asset.");
                        measurements.Add(target.GetType().Name + "; width=" + width + "; language=" + language + "; height=" + window.Height);
                    }
                }
                finally { window.Inspector = null; Object.DestroyImmediate(inspector); }
            }
        }
        finally
        {
            if (window != null) { window.Inspector = null; window.Close(); }
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            if (hadLanguage) EditorPrefs.SetInt("DiNeLang", oldLanguage); else EditorPrefs.DeleteKey("DiNeLang");
            File.WriteAllLines("ExpressionEditorRegression-ui-layout.txt", measurements);
        }
    }
}

public sealed class ExpressionEditorUiProbeWindow : EditorWindow
{
    public UnityEditor.Editor Inspector;
    public int ContentWidth;
    public float Height;
    public Exception Error;

    public void RenderFrame()
    {
        var view = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
        var repaint = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("RepaintImmediately", BindingFlags.Public | BindingFlags.Instance);
        repaint.Invoke(view, null);
    }

    public void Capture(string path, int width, int height)
    {
        var view = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this);
        var grab = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView").GetMethod("GrabPixels", BindingFlags.NonPublic | BindingFlags.Instance);
        var render = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            render.Create(); RenderTexture.active = render; GL.Clear(true, true, Color.magenta);
            grab.Invoke(view, new object[] { render, new Rect(0, 0, width, height) });
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            if (new HashSet<Color32>(image.GetPixels32()).Count < 32)
                throw new InvalidOperationException("Unity offscreen capture yielded no varied GUI pixels: " + path);
        }
        finally
        {
            RenderTexture.active = previous; render.Release();
            Object.DestroyImmediate(render); Object.DestroyImmediate(image);
        }
    }

    private void OnGUI()
    {
        if (Inspector == null || ContentWidth <= 0) return;
        bool previousWide = EditorGUIUtility.wideMode;
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.wideMode = true;
        EditorGUIUtility.labelWidth = ContentWidth * .42f;
        GUILayout.BeginArea(new Rect(0, 0, ContentWidth, 2400));
        try
        {
            Inspector.OnInspectorGUI();
            var marker = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) Height = marker.y;
        }
        catch (Exception e) { Error = e; }
        finally
        {
            GUILayout.EndArea();
            EditorGUIUtility.wideMode = previousWide;
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

// Standalone measurement entry point for the isolated regression project only.
// Copy alongside the regression scripts and execute ToggleLayoutMeasurement.Run.
public static class ToggleLayoutMeasurement
{
    [Serializable]
    public sealed class Row
    {
        public int width;
        public int language;
        public int targetCount;
        public bool preview;
        public bool previewActive;
        public float height;
        public float markerWidth;
        public string error;
    }

    [Serializable]
    private sealed class Report
    {
        public string scope = "DrawSimpleToggleUI, including section header/footer and one independent toggle card";
        public string zeroTargetPreview = "A separate fixture object starts preview to measure preview controls with an empty target list. The normal UI cannot begin a preview without targets.";
        public List<Row> rows = new List<Row>();
    }

    public static void Run()
    {
        var report = new Report();
        int previousLanguage = EditorPrefs.GetInt("DiNeLang", 0);
        var previousSelection = Selection.objects;
        GameObject root = null;
        Editor inspector = null;
        ToggleLayoutMeasurementWindow window = null;
        int failures = 0;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Toggle layout measurement avatar");
            root.AddComponent<VRCAvatarDescriptor>();
            var dresserObject = new GameObject("Multi Dresser");
            dresserObject.transform.SetParent(root.transform, false);
            var dresser = dresserObject.AddComponent<DiNeMultiDresser>();
            dresser.rootTransform = root.transform;
            var first = new GameObject("Stockings");
            first.transform.SetParent(root.transform, false);
            var second = new GameObject("Shoes");
            second.transform.SetParent(root.transform, false);
            second.SetActive(false);
            var toggle = new DiNeMultiDresser.IndependentToggle {
                displayName = "Stockings", parameterName = "DiNe/ST_Stockings", defaultOn = true, saved = true
            };
            dresser.independentToggles.Add(toggle);
            inspector = Editor.CreateEditor(dresser, typeof(DiNeMultiSupporter));
            var method = typeof(DiNeMultiSupporter).GetMethod("DrawSimpleToggleUI", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("DrawSimpleToggleUI was not found");
            window = ScriptableObject.CreateInstance<ToggleLayoutMeasurementWindow>();
            window.Inspector = inspector;
            window.Dresser = dresser;
            window.DrawMethod = method;
            window.Show();

            foreach (int width in new[] { 320, 400, 530, 700 })
            foreach (int language in new[] { 0, 1, 2 })
            foreach (int targets in new[] { 0, 2 })
            foreach (bool preview in new[] { false, true })
            {
                DiNeMultiSupporter.ClearAllActivePreviews();
                toggle.targets.Clear();
                if (targets > 0) { toggle.targets.Add(first); toggle.targets.Add(second); }
                EditorPrefs.SetInt("DiNeLang", language);
                window.ContentWidth = width;
                window.position = new Rect(40, 40, width, 1400);
                window.Error = null;
                window.MeasuredHeight = -1;
                inspector.serializedObject.Update();
                if (preview)
                    DiNeTogglePreview.Begin(inspector, 0, targets == 0 ? new[] { first } : toggle.targets.ToArray(), true);

                // Multiple synchronous Layout/Repaint pairs settle width-dependent wrapping.
                for (int frame = 0; frame < 4; frame++) window.RenderFrame();
                bool active = DiNeTogglePreview.IsActive(inspector, 0);
                var row = new Row {
                    width = width, language = language, targetCount = targets, preview = preview,
                    previewActive = active, height = window.MeasuredHeight,
                    markerWidth = window.MarkerWidth,
                    error = window.Error?.ToString()
                };
                if (row.height < 0 || active != preview || row.error != null) failures++;
                report.rows.Add(row);
            }
        }
        catch (Exception exception)
        {
            failures++;
            report.rows.Add(new Row { error = exception.ToString(), height = -1 });
            Debug.LogException(exception);
        }
        finally
        {
            DiNeMultiSupporter.ClearAllActivePreviews();
            if (window != null) { window.Inspector = null; window.Close(); }
            if (inspector != null) Object.DestroyImmediate(inspector);
            if (root != null) Object.DestroyImmediate(root);
            Selection.objects = previousSelection;
            EditorPrefs.SetInt("DiNeLang", previousLanguage);
            File.WriteAllText("ToggleLayoutMeasurement-results.json", JsonUtility.ToJson(report, true));
        }
        Debug.Log("Toggle layout measurement rows=" + report.rows.Count + ", failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}

// Uses only a production private drawing method; no measurement-only product controls.
public sealed class ToggleLayoutMeasurementWindow : EditorWindow
{
    public Editor Inspector;
    public DiNeMultiDresser Dresser;
    public MethodInfo DrawMethod;
    public int ContentWidth;
    public float MeasuredHeight = -1;
    public float MarkerWidth;
    public Exception Error;

    public void RenderFrame()
    {
        SendEvent(new Event { type = EventType.Layout });
        SendEvent(new Event { type = EventType.Repaint });
    }

    private void OnGUI()
    {
        if (Inspector == null || ContentWidth <= 0) return;
        GUI.changed = false;
        bool previousWideMode = EditorGUIUtility.wideMode;
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.wideMode = true;
        EditorGUIUtility.labelWidth = ContentWidth * 0.45f;
        GUILayout.BeginArea(new Rect(0, 0, ContentWidth, 1400));
        try
        {
            Inspector.serializedObject.Update();
            Rect before = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            DrawMethod.Invoke(Inspector, new object[] { Dresser });
            Inspector.serializedObject.ApplyModifiedProperties();
            Rect after = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                MeasuredHeight = after.y - before.y;
                MarkerWidth = after.width;
            }
        }
        catch (Exception exception) { Error = exception.InnerException ?? exception; }
        finally
        {
            GUILayout.EndArea();
            EditorGUIUtility.wideMode = previousWideMode;
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }
    }
}
#endif

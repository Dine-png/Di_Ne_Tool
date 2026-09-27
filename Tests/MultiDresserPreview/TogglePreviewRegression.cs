#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using nadena.dev.ndmf.preview;
using Object = UnityEngine.Object;

// Runs only in the isolated regression project with real Unity inspectors and scenes.
public static class TogglePreviewRegression
{
    private static void Require(bool condition, string message)
        => MultiDresserPreviewRegression.Require(condition, message);

    private static void SelectDresserTab(Editor inspector, int tab)
    {
        var method = typeof(DiNeMultiSupporter).GetMethod("SelectContentTab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Dresser content-tab selector was not found");
        method.Invoke(inspector, new object[] { tab });
    }

    private static int SelectedDresserTab(Editor inspector)
    {
        var field = typeof(DiNeMultiSupporter).GetField("selectedContentTab",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Dresser selected content-tab field was not found");
        return (int)field.GetValue(inspector);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject root;
        public readonly GameObject first;
        public readonly GameObject second;
        public readonly DiNeSmartToggle smart;
        public readonly DiNeMultiDresser dresser;
        public readonly DiNeMultiDresser.IndependentToggle group;
        public Editor smartEditor;
        public Editor dresserEditor;

        public Fixture()
        {
            root = new GameObject("Toggle preview regression avatar");
            root.AddComponent<VRCAvatarDescriptor>();
            first = Child("Originally On", true);
            second = Child("Originally Off", false);
            smart = Child("Smart Accessory", false).AddComponent<DiNeSmartToggle>();
            smart.DisplayName = "Smart Accessory";
            smart.ParameterName = "PreviewSmart";
            smart.DefaultOn = true;
            smart.Saved = false;
            dresser = Child("Multi Dresser", true).AddComponent<DiNeMultiDresser>();
            dresser.rootTransform = root.transform;
            group = new DiNeMultiDresser.IndependentToggle {
                displayName = "Together", parameterName = "PreviewGroup", defaultOn = false, saved = true
            };
            group.targets.Add(first);
            group.targets.Add(second);
            dresser.independentToggles.Add(group);
            smartEditor = Editor.CreateEditor(smart, typeof(DiNeSmartToggleEditor));
            dresserEditor = Editor.CreateEditor(dresser, typeof(DiNeMultiSupporter));
        }

        private GameObject Child(string name, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.SetActive(active);
            return go;
        }

        public void RequireOriginalStates()
        {
            Require(first.activeSelf && !second.activeSelf && !smart.gameObject.activeSelf,
                "Preview did not restore the mixed original object states");
        }

        public void Dispose()
        {
            DiNeMultiSupporter.ClearAllActivePreviews();
            if (smartEditor != null) Object.DestroyImmediate(smartEditor);
            if (dresserEditor != null) Object.DestroyImmediate(dresserEditor);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    public static void StatesAndRestoration()
    {
        using (var f = new Fixture())
        {
            string smartBefore = EditorJsonUtility.ToJson(f.smart);
            string dresserBefore = EditorJsonUtility.ToJson(f.dresser);
            Require(DiNeTogglePreview.Begin(f.dresserEditor, 0,
                new[] { f.first, null, f.second, f.first }, true), "Grouped preview did not start");
            Require(f.first.activeSelf && f.second.activeSelf, "ON did not enable every grouped target");
            Require(DiNeTogglePreview.IsActive(f.dresserEditor, 0) &&
                !DiNeTogglePreview.IsActive(f.dresserEditor, 1), "Preview owner/slot was not tracked");
            DiNeTogglePreview.SetState(false);
            Require(!f.first.activeSelf && !f.second.activeSelf, "OFF did not disable every grouped target");
            DiNeTogglePreview.ClearForOwner(f.smartEditor);
            Require(DiNeTogglePreview.IsActive(f.dresserEditor, 0), "Unrelated inspector cleared preview");
            DiNeTogglePreview.Clear();
            f.RequireOriginalStates();

            Require(DiNeTogglePreview.Begin(f.smartEditor, 0, new[] { f.smart.gameObject }, true),
                "An initially inactive Smart Toggle did not preview");
            Require(f.smart.gameObject.activeSelf, "Smart preview did not enable its own object");
            DiNeTogglePreview.SetState(false);
            Require(!f.smart.gameObject.activeSelf, "Smart preview OFF did not disable its own object");
            DiNeTogglePreview.Clear();
            f.RequireOriginalStates();
            Require(smartBefore == EditorJsonUtility.ToJson(f.smart), "Smart preview changed saved settings/default ON");
            Require(dresserBefore == EditorJsonUtility.ToJson(f.dresser), "Grouped preview changed saved settings/default ON");
            Require(!DiNeTogglePreview.Begin(f.smartEditor, 0, new GameObject[] { null }, true),
                "Preview started without a valid target");
            int invalidations = PropCacheDebug.Invalidations;
            int flushes = ComputeContext.Flushes;
            DiNeTogglePreview.Clear();
            DiNeMultiSupporter.ClearAllActivePreviews();
            Require(PropCacheDebug.Invalidations == invalidations && ComputeContext.Flushes == flushes,
                "Clearing absent toggle previews invalidated NDMF caches");
        }
        Lifecycle();
    }

    private static void Lifecycle()
    {
        var previousSelection = Selection.objects;
        try
        {
            using (var f = new Fixture())
            {
                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, false);
                // Overlap exercises restoration before the next owner records its baseline.
                DiNeTogglePreview.Begin(f.smartEditor, 0, new[] { f.first, f.smart.gameObject }, true);
                Require(f.first.activeSelf && !f.second.activeSelf && f.smart.gameObject.activeSelf,
                    "Switching owners left the old grouped preview applied");
                DiNeTogglePreview.Clear();
                f.RequireOriginalStates();

                DiNeTogglePreview.Begin(f.smartEditor, 0, new[] { f.smart.gameObject }, true);
                Object.DestroyImmediate(f.smartEditor);
                f.smartEditor = null;
                f.RequireOriginalStates();

                Selection.activeGameObject = f.root;
                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, true);
                Selection.activeGameObject = f.first;
                // executeMethod blocks the next editor update that normally dispatches
                // native selection changes. Dispatch Unity's registered callbacks here,
                // so this still verifies the real subscription rather than calling Clear.
                var selectionCallback = typeof(Selection).GetField("selectionChanged",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as Action;
                Require(selectionCallback != null, "Unity selection callback dispatcher was not available");
                selectionCallback();
                Require(!DiNeTogglePreview.IsActive(f.dresserEditor, 0), "Selection change did not clear preview");
                f.RequireOriginalStates();

                Undo.IncrementCurrentGroup();
                Undo.RecordObject(f.smart, "Toggle preview regression edit");
                string originalName = f.smart.DisplayName;
                f.smart.DisplayName = "Undo this edit";
                Undo.FlushUndoRecordObjects();
                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, false);
                Undo.PerformUndo();
                Require(f.smart.DisplayName == originalName, "Regression Undo edit was not restored");
                Require(!DiNeTogglePreview.IsActive(f.dresserEditor, 0), "Undo did not clear preview");
                f.RequireOriginalStates();

                int previousTab = SelectedDresserTab(f.dresserEditor);
                string settingsBefore = EditorJsonUtility.ToJson(f.dresser);
                SelectDresserTab(f.dresserEditor, 1);
                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, true);
                SelectDresserTab(f.dresserEditor, 0);
                Require(!DiNeTogglePreview.IsActive(f.dresserEditor, 0), "Leaving toggle tab did not clear its preview");
                f.RequireOriginalStates();
                Require(settingsBefore == EditorJsonUtility.ToJson(f.dresser), "Changing tabs changed saved toggle settings");

                SelectDresserTab(f.dresserEditor, 1);
                Object.DestroyImmediate(f.dresserEditor);
                f.dresserEditor = Editor.CreateEditor(f.dresser, typeof(DiNeMultiSupporter));
                Require(SelectedDresserTab(f.dresserEditor) == 1, "Recreated inspector did not remember the toggle tab");
                Require(settingsBefore == EditorJsonUtility.ToJson(f.dresser), "Remembering the active tab changed saved settings");
                SelectDresserTab(f.dresserEditor, previousTab);

                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, true);
                Object.DestroyImmediate(f.dresserEditor);
                f.dresserEditor = null;
                f.RequireOriginalStates();
            }
        }
        finally { Selection.objects = previousSelection; }
    }

    public static void SceneSaveRestoresOriginals()
    {
        string folder = "Assets/TogglePreviewSave_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        // The harness creates a fresh empty scene for each test. Use it directly:
        // Unity refuses additive NewScene while that initial scene is still untitled.
        Scene scene = SceneManager.GetActiveScene();
        try
        {
            string path = folder + "/Preview.unity";
            string rootName;
            using (var f = new Fixture())
            {
                rootName = f.root.name;
                DiNeTogglePreview.Begin(f.dresserEditor, 0, f.group.targets, true);
                Require(EditorSceneManager.SaveScene(scene, path), "Could not save isolated preview scene");
                Require(!DiNeTogglePreview.IsActive(f.dresserEditor, 0), "Saving scene left group preview active");
                f.RequireOriginalStates();

                DiNeTogglePreview.Begin(f.smartEditor, 0, new[] { f.smart.gameObject }, true);
                Require(EditorSceneManager.SaveScene(scene, path), "Could not save Smart preview scene");
                f.RequireOriginalStates();
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene loaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var root = loaded.GetRootGameObjects().Single(go => go.name == rootName);
            Require(root.transform.Find("Originally On").gameObject.activeSelf &&
                !root.transform.Find("Originally Off").gameObject.activeSelf &&
                !root.transform.Find("Smart Accessory").gameObject.activeSelf,
                "Scene asset persisted temporary preview object states");
            var savedSmart = root.GetComponentInChildren<DiNeSmartToggle>(true);
            var savedDresser = root.GetComponentInChildren<DiNeMultiDresser>(true);
            Require(savedSmart.DefaultOn && !savedSmart.Saved &&
                !savedDresser.independentToggles[0].defaultOn && savedDresser.independentToggles[0].saved,
                "Scene save changed configured toggle defaults");
        }
        finally
        {
            DiNeMultiSupporter.ClearAllActivePreviews();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    public static void IdleInspectors()
    {
        using (var f = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<TogglePreviewProbeWindow>();
            int previousLanguage = EditorPrefs.GetInt("DiNeLang", 0);
            try
            {
                window.position = new Rect(50, 50, 530, 1100);
                window.Show();
                foreach (var inspector in new[] { f.smartEditor, f.dresserEditor })
                {
                    if (inspector == f.dresserEditor) SelectDresserTab(inspector, 1);
                    window.Inspector = inspector;
                    window.RenderFrame();
                    var targets = inspector == f.smartEditor ? new[] { f.smart.gameObject } : f.group.targets.ToArray();
                    Require(DiNeTogglePreview.Begin(inspector, 0, targets, true), "Inspector preview did not start");
                    string smartBefore = EditorJsonUtility.ToJson(f.smart);
                    string dresserBefore = EditorJsonUtility.ToJson(f.dresser);
                    int invalidations = PropCacheDebug.Invalidations, flushes = ComputeContext.Flushes;
                    int layouts = window.Layouts, repaints = window.Repaints;
                    foreach (int language in new[] { 0, 1, 2 })
                    {
                        EditorPrefs.SetInt("DiNeLang", language);
                        for (int frame = 0; frame < 4; frame++) window.RenderFrame();
                    }
                    Require(window.Error == null, "Active toggle inspector GUI failed: " + window.Error);
                    Require(window.Layouts >= layouts + 12 && window.Repaints >= repaints + 12,
                        "Active toggle inspector did not receive all Layout/Repaint events");
                    Require(DiNeTogglePreview.IsActive(inspector, 0) && targets.All(go => go.activeSelf),
                        "Idle Layout/Repaint changed or stopped the toggle preview");
                    Require(PropCacheDebug.Invalidations == invalidations && ComputeContext.Flushes == flushes,
                        "Idle toggle inspector invalidated NDMF caches");
                    Require(smartBefore == EditorJsonUtility.ToJson(f.smart) &&
                        dresserBefore == EditorJsonUtility.ToJson(f.dresser), "Idle toggle UI changed saved configuration");
                    DiNeTogglePreview.Clear();
                    f.RequireOriginalStates();
                }
            }
            finally
            {
                EditorPrefs.SetInt("DiNeLang", previousLanguage);
                window.Inspector = null;
                window.Close();
            }
        }
    }
}

// Test-only IMGUI host for existing production inspectors; no additional product UI.
public sealed class TogglePreviewProbeWindow : EditorWindow
{
    public Editor Inspector;
    public Exception Error;
    public int Layouts;
    public int Repaints;

    public void RenderFrame()
    {
        SendEvent(new Event { type = EventType.Layout });
        SendEvent(new Event { type = EventType.Repaint });
    }

    private void OnGUI()
    {
        if (Inspector == null) return;
        if (Event.current.type == EventType.Layout) Layouts++;
        if (Event.current.type == EventType.Repaint) Repaints++;
        GUI.changed = false;
        try { Inspector.OnInspectorGUI(); }
        catch (Exception exception) { Error = exception; }
    }
}
#endif

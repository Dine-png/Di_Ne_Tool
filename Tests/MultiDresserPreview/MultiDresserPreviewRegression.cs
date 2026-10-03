#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;
using nadena.dev.ndmf.preview;

public static class MultiDresserPreviewRegression
{
    private static readonly List<string> Results = new List<string>();
    private static int failures;

    public static void Run()
    {
        Test("Idle preview Layout/Repaint never invalidates NDMF caches", IdleInspector);
        Test("Preview applies and restores objects, blendshapes and materials", RestorePreview);
        Test("Clearing a missing preview does not invalidate NDMF caches", EmptyClear);
        Test("Shape sync preserves values and responds to mesh/target changes", ShapeSyncChanges);
        Test("Shape sync benchmark: 20 outfits, 320 blendshapes, 120 iterations", BenchmarkSync);
        Test("Icons reuse existing assets; explicit regeneration preserves failed captures", SmartToggleRegression.IconReuse);
        Test("Bool toggles appear directly inside dresser/category; original menus stay unchanged", SmartToggleRegression.Placement);
        Test("Full outfit menus paginate while preserving all controls", SmartToggleRegression.Pagination);
        Test("Bool toggle avoids existing Int parameter names", SmartToggleRegression.ParameterCollision);
        Test("Smart Toggle UI supports all languages without idle dirty changes", SmartToggleRegression.Inspector);
        Test("Hierarchy Smart Toggle command is standalone and supports Undo", SmartToggleRegression.ContextMenu);
        Test("One Bool switches multiple objects; separate generators, grouped UI and Undo work", SmartToggleRegression.BatchCreation);
        Test("Grouped and standalone Bool toggles switch before/during/after MMD with 0/1/2/3/5 original FX layers", SmartToggleRegression.MmdSwitching);
        Test("MA preserves grouped and standalone toggle layers even after build layer relocation", SmartToggleRegression.MmdBuildIntent);
        Test("Menu picker recognizes existing nested menus, duplicate names, cycles and configured wardrobes", SmartToggleRegression.MenuDestinations);
        Test("Parameter defaults and custom names avoid Bool, FX and configured dresser collisions", SmartToggleRegression.PrefilledNames);
        Test("Toggle preview switches groups and restores mixed states across owner/lifecycle changes", TogglePreviewRegression.StatesAndRestoration);
        Test("Saving and reopening scenes preserves original toggle object states", TogglePreviewRegression.SceneSaveRestoresOriginals);
        Test("Active toggle inspectors repaint in all languages without cache invalidations or setting changes", TogglePreviewRegression.IdleInspectors);
        Test("Tutorial accepts existing completion and revalidates required queued progress", TutorialRegression.RequiredActions);
        Test("Tutorial start preserves preview; stop and inspector recreation restore and resume safely", TutorialRegression.PreviewAndLifecycle);
        Test("Locked inspectors cannot claim or overwrite another inspector's tutorial session", TutorialRegression.SessionOwnership);
        Test("Real mouse clicks advance completed/optional guidance and keep incomplete requirements", TutorialRegression.BubbleInteraction);
        Test("Required stages incomplete on entry need a real action before progress", TutorialRegression.IncompleteEntryRequiresAction);
        Test("Empty tutorial category creates an all-OFF default only on user action with Undo/Redo", TutorialRegression.EmptyDefaultState);
        Test("Every tutorial stage draws in three languages at narrow/wide widths without avatar mutation", TutorialRegression.IdleInspectors);
        Test("Spotlight geometry preserves target/bubble, GUI state and unrelated input", TutorialRegression.SpotlightFrames);
        Results.Add("Failures: " + failures);
        File.WriteAllLines("MultiDresserPreviewRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Test(string name, Action action)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Undo.ClearAll();
        try { action(); Results.Add("PASS " + name); }
        catch (Exception exception) { failures++; Results.Add("FAIL " + name + ": " + exception); }
        Debug.Log(Results[Results.Count - 1]);
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Invoke(DiNeMultiSupporter inspector, string method, params object[] arguments)
    {
        typeof(DiNeMultiSupporter).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(inspector, arguments);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject root;
        public readonly GameObject baseline;
        public readonly GameObject outfit;
        public readonly GameObject linked;
        public readonly SkinnedMeshRenderer renderer;
        public readonly Mesh mesh;
        public readonly Material originalMaterial;
        public readonly Material previewMaterial;
        public readonly DiNeMultiDresser dresser;
        public readonly DiNeMultiDresser.DresserLayer layer;
        public readonly DiNeMultiSupporter inspector;
        public readonly Action<DiNeMultiDresser, DiNeMultiDresser.DresserLayer> sync;

        public Fixture(int outfitCount = 2, int shapeCount = 3)
        {
            root = new GameObject("Preview regression avatar");
            root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            dresser = root.AddComponent<DiNeMultiDresser>();
            dresser.rootTransform = root.transform;
            layer = new DiNeMultiDresser.DresserLayer { layerName = "Regression" };
            dresser.layers.Add(layer);
            layer.EnsureSize(outfitCount);
            for (int index = 0; index < outfitCount; index++)
            {
                layer.targets[index] = Child(root, "Outfit " + index, index == 0);
                layer.labels[index] = layer.targets[index].name;
            }
            baseline = layer.targets[0]; outfit = layer.targets[1];
            linked = Child(root, "Linked", false);
            layer.linkedObjects[1].objects.Add(linked);
            var body = Child(root, "Body", true);
            renderer = body.AddComponent<SkinnedMeshRenderer>();
            mesh = CreateMesh(shapeCount);
            renderer.sharedMesh = mesh;
            renderer.SetBlendShapeWeight(0, 23);
            renderer.SetBlendShapeWeight(1, 37);
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Require(shader != null, "Built-in regression shader unavailable");
            originalMaterial = new Material(shader) { name = "Original" };
            previewMaterial = new Material(shader) { name = "Preview" };
            renderer.sharedMaterials = new[] { originalMaterial };
            dresser.shapeKeyTargets.Add(body);
            inspector = (DiNeMultiSupporter)Editor.CreateEditor(dresser, typeof(DiNeMultiSupporter));
            sync = (Action<DiNeMultiDresser, DiNeMultiDresser.DresserLayer>)Delegate.CreateDelegate(
                typeof(Action<DiNeMultiDresser, DiNeMultiDresser.DresserLayer>), inspector,
                typeof(DiNeMultiSupporter).GetMethod("SyncShapeKeyData", BindingFlags.NonPublic | BindingFlags.Instance));
            sync(dresser, layer);
            layer.perButtonShapeKeyStates[1].meshShapeKeys[0].shapeKeys[0] =
                new DiNeMultiDresser.ShapeKeyState { name = "Shape 0", value = 80, everRecorded = true };
            layer.perButtonMaterialSwaps[1].entries.Add(new DiNeMultiDresser.MaterialSwapEntry
            {
                renderer = renderer, materials = new List<Material> { previewMaterial }
            });
        }

        public void Apply() { Invoke(inspector, "ApplyPreview", dresser, layer, 1, 0); }
        public void Clear() { Invoke(inspector, "ClearPreview"); }
        public void Dispose()
        {
            if (inspector != null) Object.DestroyImmediate(inspector);
            if (root != null) Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(previewMaterial);
        }
    }

    private static GameObject Child(GameObject root, string name, bool active)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.SetActive(active);
        return child;
    }

    private static Mesh CreateMesh(int shapeCount)
    {
        var mesh = new Mesh { name = "Regression blendshapes" };
        mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
        mesh.triangles = new[] { 0, 1, 2 };
        var deltas = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
        for (int index = 0; index < shapeCount; index++)
            mesh.AddBlendShapeFrame("Shape " + index, 100, deltas, null, null);
        return mesh;
    }

    private static void IdleInspector()
    {
        using (var fixture = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<MultiDresserPreviewTestWindow>();
            try
            {
                window.Inspector = fixture.inspector;
                window.position = new Rect(50, 50, 900, 1100);
                window.Show();
                window.RenderFrame();
                fixture.Apply();
                int invalidations = PropCacheDebug.Invalidations;
                int flushes = ComputeContext.Flushes;
                int layouts = window.Layouts, repaints = window.Repaints;
                for (int frame = 0; frame < 12; frame++) window.RenderFrame();
                Require(window.Layouts >= layouts + 12 && window.Repaints >= repaints + 12,
                    "The actual inspector did not receive all Layout/Repaint events");
                Require(window.Error == null, "Inspector GUI failed: " + window.Error);
                int actual = PropCacheDebug.Invalidations - invalidations;
                Results.Add("METRIC idle 12 frames (Layout=" + (window.Layouts - layouts) +
                    ", Repaint=" + (window.Repaints - repaints) + "): NDMF invalidations=" + actual +
                    ", flushes=" + (ComputeContext.Flushes - flushes));
                Require(actual == 0 && ComputeContext.Flushes == flushes,
                    "Unchanged inspector invalidated NDMF " + actual + " times");
                Require(fixture.outfit.activeSelf && !fixture.baseline.activeSelf && fixture.linked.activeSelf,
                    "Idle repaint changed the preview object state");
                Require(Mathf.Approximately(80, fixture.renderer.GetBlendShapeWeight(0)), "Idle preview shape changed");
            }
            finally { window.Inspector = null; window.Close(); }
        }
    }

    private static void RestorePreview()
    {
        using (var fixture = new Fixture())
        {
            int before = PropCacheDebug.Invalidations;
            fixture.Apply();
            Require(PropCacheDebug.Invalidations > before, "Applying did not invalidate NDMF");
            Require(fixture.outfit.activeSelf && !fixture.baseline.activeSelf && fixture.linked.activeSelf, "Preview objects not applied");
            Require(Mathf.Approximately(80, fixture.renderer.GetBlendShapeWeight(0)), "Preview shape not applied");
            Require(Mathf.Approximately(37, fixture.renderer.GetBlendShapeWeight(1)), "Unmanaged shape changed");
            Require(fixture.renderer.sharedMaterial == fixture.previewMaterial, "Preview material not applied");
            before = PropCacheDebug.Invalidations;
            fixture.Clear();
            Require(PropCacheDebug.Invalidations > before, "Restoring did not invalidate NDMF");
            Require(!fixture.outfit.activeSelf && fixture.baseline.activeSelf && !fixture.linked.activeSelf, "Original object state not restored");
            Require(Mathf.Approximately(23, fixture.renderer.GetBlendShapeWeight(0)), "Original shape not restored");
            Require(Mathf.Approximately(37, fixture.renderer.GetBlendShapeWeight(1)), "Unmanaged shape not preserved");
            Require(fixture.renderer.sharedMaterial == fixture.originalMaterial, "Original material not restored");
        }
    }

    private static void EmptyClear()
    {
        using (var fixture = new Fixture())
        {
            fixture.Clear();
            int before = PropCacheDebug.Invalidations;
            int flushes = ComputeContext.Flushes;
            for (int index = 0; index < 12; index++) fixture.Clear();
            Require(PropCacheDebug.Invalidations == before && ComputeContext.Flushes == flushes,
                "Clearing absent preview repeatedly invalidated NDMF");
        }
    }

    private static void ShapeSyncChanges()
    {
        using (var fixture = new Fixture())
        {
            fixture.sync(fixture.dresser, fixture.layer);
            var keys = fixture.layer.perButtonShapeKeyStates[1].meshShapeKeys[0].shapeKeys;
            Require(keys[0].everRecorded && keys[0].value == 80, "Sync overwrote a recorded value");
            fixture.mesh.AddBlendShapeFrame("Added", 100, new Vector3[3], null, null);
            fixture.sync(fixture.dresser, fixture.layer);
            Require(keys.Count == 4 && keys.Exists(key => key.name == "Added"), "Same mesh new shape was ignored");
            var replacement = CreateMesh(2);
            try
            {
                fixture.renderer.sharedMesh = replacement;
                fixture.sync(fixture.dresser, fixture.layer);
                Require(keys.Count == 2 && keys[0].value == 80 && keys[0].everRecorded, "Mesh swap lost recorded values or retained removed keys");
                fixture.dresser.shapeKeyTargets.Add(null);
                fixture.sync(fixture.dresser, fixture.layer);
                Require(fixture.layer.perButtonShapeKeyStates[0].meshShapeKeys.Count == 2, "New mesh slot was not synchronized");
                fixture.dresser.shapeKeyTargets[0] = null;
                fixture.sync(fixture.dresser, fixture.layer);
                Require(keys.Count == 0, "Missing target left stale shapes");
            }
            finally { fixture.renderer.sharedMesh = fixture.mesh; Object.DestroyImmediate(replacement); }
        }
    }

    private static void BenchmarkSync()
    {
        using (var fixture = new Fixture(20, 320))
        {
            for (int warmup = 0; warmup < 5; warmup++) fixture.sync(fixture.dresser, fixture.layer);
            GC.Collect();
            var watch = Stopwatch.StartNew();
            for (int iteration = 0; iteration < 120; iteration++) fixture.sync(fixture.dresser, fixture.layer);
            watch.Stop();
            Results.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "METRIC SyncShapeKeyData 20x320 x120: total={0:F3} ms; per call={1:F3} ms",
                watch.Elapsed.TotalMilliseconds, watch.Elapsed.TotalMilliseconds / 120));
            Require(fixture.layer.perButtonShapeKeyStates[19].meshShapeKeys[0].shapeKeys.Count == 320,
                "Benchmark fixture was not fully synchronized");
        }
    }
}

// Test-only IMGUI host. It draws the existing production inspector without adding
// any product UI; the Di Ne UI-standard branding/localization checklist is unchanged.
public sealed class MultiDresserPreviewTestWindow : EditorWindow
{
    public DiNeMultiSupporter Inspector;
    public int Layouts;
    public int Repaints;
    public Exception Error;

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
        catch (Exception exception) { Error = exception; throw; }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.ndmf.preview;
using Object = UnityEngine.Object;

// Runs only in the isolated project, using the shipped inspector and actual scene state.
public static class TutorialRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void RunOnly()
    {
        var results = new System.Collections.Generic.List<string>();
        int failures = 0;
        foreach (var test in new[] {
            Tuple.Create("Required scene actions and optional progression", (Action)RequiredActions),
            Tuple.Create("Preview restoration and lifecycle resume", (Action)PreviewAndLifecycle),
            Tuple.Create("Locked inspector tutorial ownership", (Action)SessionOwnership),
            Tuple.Create("Real completed/optional/incomplete bubble mouse input", (Action)BubbleInteraction),
            Tuple.Create("Incomplete required entry needs an actual action", (Action)IncompleteEntryRequiresAction),
            Tuple.Create("Explicit empty default state and Undo/Redo", (Action)EmptyDefaultState),
            Tuple.Create("All tutorial phases/languages/widths remain idle", (Action)IdleInspectors),
            Tuple.Create("Spotlight excludes anchor/bubble, restores GUI state and preserves input", (Action)SpotlightFrames),
            Tuple.Create("Shared tutorial required/optional actions and prerequisite changes", (Action)GuidedTutorialRegression.Progression),
            Tuple.Create("Shared tutorial courses, schemas and locked inspector ownership", (Action)GuidedTutorialRegression.CoursesAndOwnership),
            Tuple.Create("Shared tutorial real bubble mouse input", (Action)GuidedTutorialRegression.BubbleMouseInput),
            Tuple.Create("Shared tutorial scroll coordinates and input passthrough", (Action)GuidedTutorialRegression.NestedScrollSpotlight)
        })
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
            try { test.Item2(); results.Add("PASS " + test.Item1); }
            catch (Exception exception) { failures++; results.Add("FAIL " + test.Item1 + ": " + exception); }
            Debug.Log(results[results.Count - 1]);
        }
        results.Add("Failures: " + failures);
        File.WriteAllLines("MultiDresserTutorialRegression-results.txt", results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static object Step(string name)
    {
        var type = typeof(DiNeMultiSupporter).GetNestedType("TutorialStep", BindingFlags.NonPublic);
        Require(type != null, "Production tutorial step type is missing");
        return Enum.Parse(type, name);
    }

    private static string[] StepNames()
    {
        var type = typeof(DiNeMultiSupporter).GetNestedType("TutorialStep", BindingFlags.NonPublic);
        Require(type != null, "Production tutorial step type is missing");
        return Enum.GetNames(type);
    }

    private static string NextStep(DiNeMultiSupporter inspector)
    {
        string[] names = StepNames();
        int current = Array.IndexOf(names, Field(inspector, "tutorialStep").ToString());
        Require(current >= 0 && current + 1 < names.Length, "Tutorial has no next step");
        return names[current + 1];
    }

    private static void AdvanceTo(DiNeMultiSupporter inspector, string targetStep)
    {
        for (int attempts = 0; attempts < StepNames().Length; attempts++)
        {
            if (Field(inspector, "tutorialStep").ToString() == targetStep) return;
            string next = NextStep(inspector);
            ClickBubble(inspector);
            Expect(inspector, next);
        }
        throw new Exception("Could not reach tutorial step " + targetStep);
    }

    private static object Field(DiNeMultiSupporter inspector, string name)
    {
        var field = typeof(DiNeMultiSupporter).GetField(name, PrivateInstance);
        Require(field != null, "Production tutorial field is missing: " + name);
        return field.GetValue(inspector);
    }

    private static void SetField(DiNeMultiSupporter inspector, string name, object value)
    {
        var field = typeof(DiNeMultiSupporter).GetField(name, PrivateInstance);
        Require(field != null, "Production inspector field is missing: " + name);
        field.SetValue(inspector, value);
    }

    private static void Invoke(DiNeMultiSupporter inspector, string name, params object[] arguments)
    {
        var method = typeof(DiNeMultiSupporter).GetMethod(name, PrivateInstance);
        Require(method != null, "Production tutorial method is missing: " + name);
        try { method.Invoke(inspector, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
    }

    private static void Expect(DiNeMultiSupporter inspector, string step)
    {
        Require((bool)Field(inspector, "tutorialActive"), "Tutorial stopped before " + step);
        Require(Field(inspector, "tutorialStep").ToString() == step,
            "Expected " + step + ", found " + Field(inspector, "tutorialStep"));
    }

    private static void Flush(DiNeMultiSupporter inspector) => Invoke(inspector, "FlushTutorialTransition");
    private static void ClickBubble(DiNeMultiSupporter inspector)
    {
        Invoke(inspector, "AdvanceOptionalTutorial");
        Flush(inspector);
    }
    private static void Notify(DiNeMultiSupporter inspector, string action)
    {
        Invoke(inspector, "NotifyTutorialAction", Step(action));
        Flush(inspector);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string folder;
        public readonly GameObject root, baseline, outfit, linked, foreign;
        public readonly VRCAvatarDescriptor avatar;
        public readonly AnimatorController controller;
        public readonly VRCExpressionsMenu menu;
        public readonly DiNeMultiDresser dresser;
        public readonly DiNeMultiDresser.DresserLayer layer;
        public readonly SkinnedMeshRenderer renderer;
        public readonly Mesh mesh;
        public readonly Material originalMaterial, previewMaterial;
        public DiNeMultiSupporter inspector;

        public Fixture()
        {
            folder = "Assets/TutorialRegression_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/FX.controller");
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(menu, folder + "/Menu.asset");
            root = new GameObject("Tutorial regression avatar");
            avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer {
                type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = controller
            } };
            avatar.expressionsMenu = menu;
            dresser = Child("Multi Dresser", true).AddComponent<DiNeMultiDresser>();
            dresser.rootTransform = root.transform;
            dresser.animatorController = controller;
            dresser.expressionsMenu = menu;
            layer = new DiNeMultiDresser.DresserLayer { layerName = "Clothes" };
            layer.EnsureSize(2);
            baseline = Child("Original clothes", true);
            outfit = Child("Tutorial clothes", false);
            linked = Child("Linked accessory", false);
            foreign = new GameObject("Outside the tutorial avatar");
            layer.targets[0] = baseline;
            layer.targets[1] = outfit;
            layer.labels[0] = "Default";
            layer.labels[1] = "Tutorial clothes";
            layer.linkedObjects[1].objects.Add(linked);
            dresser.layers.Add(layer);
            renderer = Child("Body", true).AddComponent<SkinnedMeshRenderer>();
            mesh = new Mesh { name = "Tutorial regression shape" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("Clothes fitting", 100,
                new[] { Vector3.forward, Vector3.forward, Vector3.forward }, null, null);
            renderer.sharedMesh = mesh;
            renderer.SetBlendShapeWeight(0, 23);
            var shader = Shader.Find("Hidden/InternalErrorShader");
            Require(shader != null, "Built-in tutorial regression shader unavailable");
            originalMaterial = new Material(shader) { name = "Original tutorial material" };
            previewMaterial = new Material(shader) { name = "Temporary tutorial material" };
            renderer.sharedMaterials = new[] { originalMaterial };
            dresser.shapeKeyTargets.Add(renderer.gameObject);
            CreateInspector();
            Invoke(inspector, "SyncShapeKeyData", dresser, layer);
            layer.perButtonShapeKeyStates[1].meshShapeKeys[0].shapeKeys[0] =
                new DiNeMultiDresser.ShapeKeyState { name = "Clothes fitting", value = 80, everRecorded = true };
            layer.perButtonMaterialSwaps[1].entries.Add(new DiNeMultiDresser.MaterialSwapEntry {
                renderer = renderer, materials = new System.Collections.Generic.List<Material> { previewMaterial }
            });
        }

        private GameObject Child(string name, bool active)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.SetActive(active);
            return child;
        }

        public void CreateInspector()
            => inspector = (DiNeMultiSupporter)Editor.CreateEditor(dresser, typeof(DiNeMultiSupporter));
        public void Start() { Invoke(inspector, "StartTutorial"); Expect(inspector, "Welcome"); }
        public void ReachPreview()
        {
            Start();
            AdvanceTo(inspector, "Preview");
        }
        public void Apply(int button = 1) => Invoke(inspector, "ApplyPreview", dresser, layer, button, 0);
        public void RequireOriginalPreviewState()
        {
            Require(baseline.activeSelf && !outfit.activeSelf && !linked.activeSelf,
                "Tutorial did not restore original object activation");
            Require(Mathf.Approximately(renderer.GetBlendShapeWeight(0), 23),
                "Tutorial did not restore original blendshape weight");
            Require(renderer.sharedMaterial == originalMaterial, "Tutorial did not restore original material");
            Require((int)Field(inspector, "previewLayerIndex") < 0 &&
                (int)Field(inspector, "previewButtonIndex") < 0, "Tutorial left wardrobe preview active");
        }
        public void Dispose()
        {
            if (inspector != null) Object.DestroyImmediate(inspector);
            if (root != null) Object.DestroyImmediate(root);
            if (foreign != null) Object.DestroyImmediate(foreign);
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(previewMaterial);
            AssetDatabase.DeleteAsset(folder);
        }
    }

    public static void RequiredActions()
    {
        using (var f = new Fixture())
        {
            string dresserBefore = EditorJsonUtility.ToJson(f.dresser);
            string avatarBefore = EditorJsonUtility.ToJson(f.avatar);
            f.Start();
            Require(dresserBefore == EditorJsonUtility.ToJson(f.dresser) &&
                avatarBefore == EditorJsonUtility.ToJson(f.avatar), "Starting tutorial changed avatar settings");
            ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
            // Existing valid configuration can proceed without reassigning it, but
            // scene validity must survive until the delayed transition commits.
            Invoke(f.inspector, "AdvanceOptionalTutorial");
            f.dresser.rootTransform = null;
            Flush(f.inspector); Expect(f.inspector, "Avatar");
            ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
            f.dresser.rootTransform = f.outfit.transform;
            Notify(f.inspector, "Avatar"); Expect(f.inspector, "Avatar");
            f.dresser.rootTransform = f.root.transform;
            f.dresser.animatorController = null;
            ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
            f.dresser.animatorController = f.controller;
            var originalBindings = f.avatar.baseAnimationLayers;
            f.avatar.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];
            Notify(f.inspector, "Avatar"); Expect(f.inspector, "Avatar");
            f.avatar.baseAnimationLayers = originalBindings;
            ClickBubble(f.inspector); Expect(f.inspector, "Category");

            f.dresser.layers.Clear();
            ClickBubble(f.inspector); Expect(f.inspector, "Category");
            f.dresser.layers.Add(f.layer);
            f.layer.layerName = "  ";
            Notify(f.inspector, "Category"); Expect(f.inspector, "Category");
            f.layer.layerName = "Clothes";
            f.dresser.layers.Add(new DiNeMultiDresser.DresserLayer { layerName = " clothes " });
            ClickBubble(f.inspector); Expect(f.inspector, "Category");
            f.dresser.layers.RemoveAt(1);
            SetField(f.inspector, "selectedLayerIndex", -1);
            Notify(f.inspector, "Category"); Expect(f.inspector, "Category");
            SetField(f.inspector, "selectedLayerIndex", 0);
            Invoke(f.inspector, "AdvanceOptionalTutorial");
            f.layer.layerName = "";
            Flush(f.inspector); Expect(f.inspector, "Category");
            f.layer.layerName = "Clothes";
            ClickBubble(f.inspector); Expect(f.inspector, "DefaultState");
            AdvanceTo(f.inspector, "Outfit");

            f.layer.targets[1] = null;
            ClickBubble(f.inspector); Expect(f.inspector, "Outfit");
            f.layer.targets[1] = f.root;
            Notify(f.inspector, "Outfit"); Expect(f.inspector, "Outfit");
            f.layer.targets[1] = f.foreign;
            ClickBubble(f.inspector); Expect(f.inspector, "Outfit");
            f.layer.targets[1] = f.outfit;
            Invoke(f.inspector, "AdvanceOptionalTutorial");
            f.outfit.transform.SetParent(null);
            Flush(f.inspector); Expect(f.inspector, "Outfit");
            f.outfit.transform.SetParent(f.root.transform);
            ClickBubble(f.inspector); Expect(f.inspector, "Menu");
            AdvanceTo(f.inspector, "Preview");

            ClickBubble(f.inspector); Expect(f.inspector, "Preview");
            SetField(f.inspector, "previewLayerIndex", 0);
            SetField(f.inspector, "previewButtonIndex", 1);
            Notify(f.inspector, "Preview"); Expect(f.inspector, "Preview");
            SetField(f.inspector, "previewLayerIndex", -1);
            SetField(f.inspector, "previewButtonIndex", -1);
            f.Apply(0);
            Notify(f.inspector, "Preview"); Expect(f.inspector, "Preview");
            Invoke(f.inspector, "ClearPreview");
            f.Apply();
            Notify(f.inspector, "Preview"); Expect(f.inspector, "RestorePreview");
            ClickBubble(f.inspector); Expect(f.inspector, "RestorePreview");
            // Merely changing preview indices does not perform scene restoration.
            SetField(f.inspector, "previewLayerIndex", -1);
            SetField(f.inspector, "previewButtonIndex", -1);
            Notify(f.inspector, "RestorePreview"); Expect(f.inspector, "RestorePreview");
            Require(f.outfit.activeSelf && f.linked.activeSelf && f.renderer.sharedMaterial == f.previewMaterial,
                "Spoofing preview indices unexpectedly restored the scene");
            SetField(f.inspector, "previewLayerIndex", 0);
            SetField(f.inspector, "previewButtonIndex", 1);
            Notify(f.inspector, "RestorePreview"); Expect(f.inspector, "RestorePreview");
            Invoke(f.inspector, "ClearPreview");
            f.RequireOriginalPreviewState();
            ClickBubble(f.inspector); Expect(f.inspector, "RestorePreview");
            Notify(f.inspector, "RestorePreview"); Expect(f.inspector, "ShapeKeys");
            AdvanceTo(f.inspector, "Complete");
            ClickBubble(f.inspector);
            Require(!(bool)Field(f.inspector, "tutorialActive"), "Completion bubble did not finish the tutorial");
            Require(dresserBefore == EditorJsonUtility.ToJson(f.dresser) &&
                avatarBefore == EditorJsonUtility.ToJson(f.avatar), "Tutorial progression changed saved settings");
        }
    }

    public static void PreviewAndLifecycle()
    {
        using (var f = new Fixture())
        {
            f.Apply();
            string savedSettings = EditorJsonUtility.ToJson(f.dresser);
            int invalidations = PropCacheDebug.Invalidations;
            f.Start();
            Require(f.outfit.activeSelf && !f.baseline.activeSelf && f.linked.activeSelf &&
                (int)Field(f.inspector, "previewButtonIndex") == 1, "Starting tutorial changed an existing preview");
            Require(PropCacheDebug.Invalidations == invalidations, "Starting tutorial invalidated the existing preview");
            Invoke(f.inspector, "StopTutorial");
            f.RequireOriginalPreviewState();
            f.Apply();
            f.ReachPreview();
            int existingPreviewInvalidations = PropCacheDebug.Invalidations;
            ClickBubble(f.inspector); Expect(f.inspector, "RestorePreview");
            Require(PropCacheDebug.Invalidations == existingPreviewInvalidations &&
                Mathf.Approximately(f.renderer.GetBlendShapeWeight(0), 80) && f.renderer.sharedMaterial == f.previewMaterial,
                "Accepting an already completed preview reapplied or changed it");
            Invoke(f.inspector, "ClearPreview");
            Notify(f.inspector, "RestorePreview"); Expect(f.inspector, "ShapeKeys");
            Invoke(f.inspector, "StopTutorial");
            f.ReachPreview();
            f.Apply();
            Notify(f.inspector, "Preview"); Expect(f.inspector, "RestorePreview");
            Require(Mathf.Approximately(f.renderer.GetBlendShapeWeight(0), 80) &&
                f.renderer.sharedMaterial == f.previewMaterial, "Required tutorial preview did not apply edits");
            Object.DestroyImmediate(f.inspector);
            f.inspector = null;
            f.CreateInspector();
            f.RequireOriginalPreviewState();
            Expect(f.inspector, "Preview");
            ClickBubble(f.inspector); Expect(f.inspector, "Preview");
            Invoke(f.inspector, "StopTutorial");
            Object.DestroyImmediate(f.inspector);
            f.inspector = null;
            f.CreateInspector();
            Require(!(bool)Field(f.inspector, "tutorialActive"), "Stopped tutorial resumed after recreating inspector");
            Require(savedSettings == EditorJsonUtility.ToJson(f.dresser), "Tutorial stop/resume changed saved dresser settings");
        }
    }

    public static void SessionOwnership()
    {
        using (var f = new Fixture())
        {
            var secondary = (DiNeMultiSupporter)Editor.CreateEditor(f.dresser, typeof(DiNeMultiSupporter));
            try
            {
                f.Start();
                ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
                Require(!(bool)Field(secondary, "tutorialActive"), "An idle locked inspector claimed the tutorial");
                Object.DestroyImmediate(secondary);
                secondary = null;
                Object.DestroyImmediate(f.inspector);
                f.inspector = null;
                f.CreateInspector();
                Expect(f.inspector, "Avatar");
                secondary = (DiNeMultiSupporter)Editor.CreateEditor(f.dresser, typeof(DiNeMultiSupporter));
                Require(!(bool)Field(secondary, "tutorialActive"), "A second inspector claimed an active tutorial");
                Object.DestroyImmediate(secondary);
                secondary = null;
                Object.DestroyImmediate(f.inspector);
                f.inspector = null;
                f.CreateInspector();
                Expect(f.inspector, "Avatar");
                Invoke(f.inspector, "StopTutorial");
            }
            finally { if (secondary != null) Object.DestroyImmediate(secondary); }
        }
    }

    public static void BubbleInteraction()
    {
        using (var f = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<TutorialProbeWindow>();
            try
            {
                window.Inspector = f.inspector;
                window.position = new Rect(50, 50, 540, 900);
                window.Show();
                f.dresser.rootTransform = null;
                f.Start();
                window.RenderFrame(); window.RenderFrame();
                Require(DiNeTutorialBubble.LastRect.width > 100 && DiNeTutorialBubble.LastRect.height > 40,
                    "Optional tutorial bubble did not draw a measurable area");
                window.ClickLastBubble();
                Flush(f.inspector); Expect(f.inspector, "Avatar");
                window.RenderFrame(); window.RenderFrame();
                string settings = EditorJsonUtility.ToJson(f.dresser);
                window.ClickLastBubble();
                Flush(f.inspector); Expect(f.inspector, "Avatar");
                Require(settings == EditorJsonUtility.ToJson(f.dresser), "Clicking incomplete guidance changed saved settings");
                f.dresser.rootTransform = f.root.transform;
                window.RenderFrame(); window.RenderFrame();
                window.ClickLastBubble();
                Flush(f.inspector); Expect(f.inspector, "Avatar");
                f.layer.layerName = "";
                Notify(f.inspector, "Avatar"); Expect(f.inspector, "Category");
                window.RenderFrame(); window.RenderFrame();
                Require(DiNeTutorialBubble.LastAnchorRect.height >= 49,
                    "Incomplete category spotlight excluded its confirmation button");
                window.ClickLastBubble();
                Flush(f.inspector); Expect(f.inspector, "Category");
                f.layer.layerName = "Clothes";
                window.RenderFrame(); window.RenderFrame();
                window.ClickLastBubble();
                Flush(f.inspector); Expect(f.inspector, "Category");
                window.RenderFrame(); window.RenderFrame();
                Rect incompleteAnchor = DiNeTutorialBubble.LastAnchorRect;
                window.ClickContentPoint(new Vector2(incompleteAnchor.center.x, incompleteAnchor.yMax - 12));
                Flush(f.inspector); Expect(f.inspector, "DefaultState");
                // A fresh session with a completed avatar allows the same real
                // bubble click without any rebind operation or scene mutation.
                f.Start();
                window.RenderFrame(); window.RenderFrame();
                window.ClickLastBubble(); Flush(f.inspector); Expect(f.inspector, "Avatar");
                window.RenderFrame(); window.RenderFrame();
                settings = EditorJsonUtility.ToJson(f.dresser);
                window.ClickLastBubble(); Flush(f.inspector); Expect(f.inspector, "Category");
                Require(settings == EditorJsonUtility.ToJson(f.dresser), "Clicking completed guidance changed saved settings");
                window.RenderFrame(); window.RenderFrame();
                Require(DiNeTutorialBubble.LastAnchorRect.height < 49,
                    "Completed category spotlight included an unnecessary confirmation action");
                window.ClickLastBubble();
                f.layer.layerName = "";
                Flush(f.inspector); Expect(f.inspector, "Category");
                f.layer.layerName = "Clothes";
                window.RenderFrame(); window.RenderFrame();
                window.ClickLastBubble(); Flush(f.inspector); Expect(f.inspector, "DefaultState");
                Require(window.Error == null, "Actual tutorial bubble click failed: " + window.Error);
                Invoke(f.inspector, "StopTutorial");
            }
            finally { window.Inspector = null; window.Close(); }
        }
    }

    public static void IncompleteEntryRequiresAction()
    {
        using (var f = new Fixture())
        {
            f.dresser.rootTransform = null;
            f.Start(); ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
            f.dresser.rootTransform = f.root.transform;
            ClickBubble(f.inspector); Expect(f.inspector, "Avatar");
            f.layer.layerName = "";
            Notify(f.inspector, "Avatar"); Expect(f.inspector, "Category");
            f.layer.layerName = "Clothes";
            ClickBubble(f.inspector); Expect(f.inspector, "Category");
            Notify(f.inspector, "Category"); Expect(f.inspector, "DefaultState");
            f.layer.targets[1] = null;
            AdvanceTo(f.inspector, "Outfit");
            f.layer.targets[1] = f.outfit;
            ClickBubble(f.inspector); Expect(f.inspector, "Outfit");
            Notify(f.inspector, "Outfit"); Expect(f.inspector, "Menu");
            AdvanceTo(f.inspector, "Preview");
            f.Apply();
            ClickBubble(f.inspector); Expect(f.inspector, "Preview");
            Notify(f.inspector, "Preview"); Expect(f.inspector, "RestorePreview");
            Invoke(f.inspector, "ClearPreview");
            ClickBubble(f.inspector); Expect(f.inspector, "RestorePreview");
            f.RequireOriginalPreviewState();
            Notify(f.inspector, "RestorePreview"); Expect(f.inspector, "ShapeKeys");
            Invoke(f.inspector, "StopTutorial");
        }
    }

    public static void EmptyDefaultState()
    {
        using (var f = new Fixture())
        {
            var empty = new DiNeMultiDresser.DresserLayer { layerName = "Clothes" };
            f.dresser.layers[0] = empty;
            f.Start();
            AdvanceTo(f.inspector, "Outfit");
            var window = ScriptableObject.CreateInstance<TutorialProbeWindow>();
            try
            {
                window.Inspector = f.inspector;
                window.position = new Rect(50, 50, 540, 900);
                window.Show();
                window.RenderFrame(); window.RenderFrame();
                Require(empty.targets.Count == 0, "Idle tutorial created a default outfit without an action");
                string before = EditorJsonUtility.ToJson(f.dresser);
                Undo.ClearAll();
                // Host the actual action alone to measure its button rect without
                // assuming skin margins or manipulating a foreground application.
                window.DrawOnly = () => {
                    f.inspector.serializedObject.Update();
                    Invoke(f.inspector, "DrawTutorialOutfitActions", f.dresser, f.dresser.layers[0]);
                    if (Event.current.type == EventType.Repaint && f.dresser.layers[0].targets.Count == 0)
                        window.ActionRect = GUILayoutUtility.GetLastRect();
                };
                window.RenderFrame(); window.RenderFrame();
                Require(window.ActionRect.width > 100 && window.ActionRect.height >= 24,
                    "All-OFF action did not draw a measurable button");
                window.ClickContentPoint(window.ActionRect.center);
                window.RenderFrame(); Flush(f.inspector);
                Expect(f.inspector, "Outfit");
                Require(f.dresser.layers[0].targets.Count == 1 && f.dresser.layers[0].targets[0] == null,
                    "All-OFF action did not create an empty default slot");
                Require(f.dresser.layers[0].labels.Count == 1 && f.dresser.layers[0].icons.Count == 1 &&
                    f.dresser.layers[0].linkedObjects.Count == 1 &&
                    f.dresser.layers[0].perButtonShapeKeyStates.Count == 1 &&
                    f.dresser.layers[0].perButtonMaterialSwaps.Count == 1, "Default slot left parallel item lists inconsistent");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                f.inspector.serializedObject.Update();
                Require(before == EditorJsonUtility.ToJson(f.dresser), "Undo did not restore the empty category");
                Undo.PerformRedo();
                Require(f.dresser.layers[0].targets.Count == 1 && f.dresser.layers[0].targets[0] == null,
                    "Redo did not restore the explicit all-OFF default slot");
                Require(f.baseline.activeSelf && !f.outfit.activeSelf && !f.linked.activeSelf,
                    "Creating an empty tutorial slot changed scene object activation");
                Require(window.Error == null, "Empty default-state control failed: " + window.Error);
                Invoke(f.inspector, "StopTutorial");
            }
            finally { window.Inspector = null; window.Close(); }
        }
    }

    public static void IdleInspectors()
    {
        using (var f = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<TutorialProbeWindow>();
            int previousLanguage = EditorPrefs.GetInt("DiNeLang", 0);
            try
            {
                window.Inspector = f.inspector;
                window.position = new Rect(50, 50, 540, 900);
                window.Show();
                window.RenderFrame(); // Initialize existing shape-list synchronization before snapshots.
                f.Start();
                foreach (string step in StepNames())
                {
                    DrawStep(window, f, step);
                    if (step == "MaterialRenderer" || step == "MaterialSlots")
                    {
                        var entries = f.layer.perButtonMaterialSwaps[1].entries;
                        var existingEntries = entries.ToArray();
                        try
                        {
                            entries.Clear();
                            DrawStep(window, f, step);
                            Require(DiNeTutorialBubble.LastAnchorRect.width <= 100 &&
                                DiNeTutorialBubble.LastAnchorRect.height <= 30,
                                "Empty material setup did not highlight the Add action at " + step);
                        }
                        finally { entries.AddRange(existingEntries); }
                    }
                    if (step == "Complete") break;
                    string next = NextStep(f.inspector);
                    if (step == "Preview") { f.Apply(); Notify(f.inspector, "Preview"); }
                    else if (step == "RestorePreview") { Invoke(f.inspector, "ClearPreview"); Notify(f.inspector, "RestorePreview"); }
                    else ClickBubble(f.inspector);
                    Expect(f.inspector, next);
                }
                Invoke(f.inspector, "StopTutorial");
                f.RequireOriginalPreviewState();
                window.RenderFrame(); window.RenderFrame();
                Require(!DiNeTutorialBubble.HasOverlay && DiNeTutorialBubble.LastDimmedRects.Length == 0,
                    "An inactive inspector kept the previous spotlight");
            }
            finally
            {
                EditorPrefs.SetInt("DiNeLang", previousLanguage);
                window.Inspector = null;
                window.Close();
            }
        }
    }

    private static void DrawStep(TutorialProbeWindow window, Fixture f, string step)
    {
        Expect(f.inspector, step);
        string dresserBefore = EditorJsonUtility.ToJson(f.dresser), avatarBefore = EditorJsonUtility.ToJson(f.avatar);
        bool baselineBefore = f.baseline.activeSelf, outfitBefore = f.outfit.activeSelf, linkedBefore = f.linked.activeSelf;
        float shapeBefore = f.renderer.GetBlendShapeWeight(0);
        Material materialBefore = f.renderer.sharedMaterial;
        int invalidations = PropCacheDebug.Invalidations, flushes = ComputeContext.Flushes;
        int layouts = window.Layouts, repaints = window.Repaints;
        EditorUtility.ClearDirty(f.dresser);
        EditorUtility.ClearDirty(f.avatar);
        string[] bodies = new string[3];
        foreach (float width in new[] { 360f, 540f, 900f })
        foreach (int language in new[] { 0, 1, 2 })
        {
            window.position = new Rect(50, 50, width, 900);
            EditorPrefs.SetInt("DiNeLang", language);
            object[] copy = { Step(step), null, null };
            Invoke(f.inspector, "GetTutorialCopy", copy);
            Require(!string.IsNullOrWhiteSpace(copy[1] as string) && !string.IsNullOrWhiteSpace(copy[2] as string),
                "Tutorial copy is missing at " + step + ", language " + language);
            bodies[language] = (string)copy[2];
            window.RenderFrame(); window.RenderFrame();
            Flush(f.inspector);
            Expect(f.inspector, step);
            Require(DiNeTutorialBubble.HasOverlay, "Tutorial spotlight did not draw at " + step + ", language " + language + ", width " + width);
            AssertSpotlightGeometry();
        }
        Require(window.Error == null, "Tutorial inspector failed at " + step + ": " + window.Error);
        Require(bodies[0] != bodies[1] && bodies[0] != bodies[2] && bodies[1] != bodies[2],
            "Tutorial body was not translated in all three languages at " + step);
        Require(window.Layouts >= layouts + 18 && window.Repaints >= repaints + 18,
            "Tutorial inspector did not receive all Layout/Repaint events at " + step);
        Require(dresserBefore == EditorJsonUtility.ToJson(f.dresser) && avatarBefore == EditorJsonUtility.ToJson(f.avatar),
            "Idle tutorial drawing changed saved settings at " + step);
        Require(!EditorUtility.IsDirty(f.dresser) && !EditorUtility.IsDirty(f.avatar),
            "Idle tutorial drawing marked avatar settings dirty at " + step);
        Require(f.baseline.activeSelf == baselineBefore && f.outfit.activeSelf == outfitBefore &&
            f.linked.activeSelf == linkedBefore && Mathf.Approximately(f.renderer.GetBlendShapeWeight(0), shapeBefore) &&
            f.renderer.sharedMaterial == materialBefore, "Idle tutorial drawing changed avatar preview at " + step);
        Require(PropCacheDebug.Invalidations == invalidations && ComputeContext.Flushes == flushes,
            "Idle tutorial drawing invalidated NDMF caches at " + step);
    }

    private static float IntersectionArea(Rect first, Rect second)
    {
        return Mathf.Max(0, Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin)) *
            Mathf.Max(0, Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin));
    }

    private static void AssertSpotlightGeometry()
    {
        Require(DiNeTutorialBubble.HasOverlay, "Spotlight has no active overlay");
        Rect bounds = DiNeTutorialBubble.LastOverlayRect, anchor = DiNeTutorialBubble.LastAnchorRect;
        Rect bubble = DiNeTutorialBubble.LastRect;
        Rect[] dimmed = DiNeTutorialBubble.LastDimmedRects;
        Require(bounds.width > 0 && bounds.height > 0 && anchor.width > 0 && anchor.height > 0 &&
            bubble.width > 0 && bubble.height > 0, "Spotlight bounds, anchor or bubble are empty");
        Require(dimmed.Length > 0, "Spotlight did not dim any surrounding controls");
        float area = 0;
        for (int index = 0; index < dimmed.Length; index++)
        {
            Rect rect = dimmed[index];
            Require(rect.width > 0 && rect.height > 0 && rect.xMin >= bounds.xMin - 0.05f &&
                rect.yMin >= bounds.yMin - 0.05f && rect.xMax <= bounds.xMax + 0.05f &&
                rect.yMax <= bounds.yMax + 0.05f, "A dim tile escaped the inspector frame");
            Require(IntersectionArea(rect, anchor) < 0.05f && IntersectionArea(rect, bubble) < 0.05f,
                "Spotlight dimming covered the current control or tutorial bubble");
            for (int previous = 0; previous < index; previous++)
                Require(IntersectionArea(rect, dimmed[previous]) < 0.05f, "Overlapping dim tiles darken controls twice");
            area += rect.width * rect.height;
        }
        Require(area > 0 && area < bounds.width * bounds.height,
            "Spotlight did not preserve a bright region inside its surrounding dim area");
    }

    private struct GuiSnapshot
    {
        public Color color, background, content;
        public bool enabled, changed;
        public Matrix4x4 matrix;
        public int indent;
        public float labelWidth;

        public static GuiSnapshot Take() => new GuiSnapshot {
            color = GUI.color, background = GUI.backgroundColor, content = GUI.contentColor,
            enabled = GUI.enabled, changed = GUI.changed, matrix = GUI.matrix,
            indent = EditorGUI.indentLevel, labelWidth = EditorGUIUtility.labelWidth
        };

        public void Restore()
        {
            GUI.color = color; GUI.backgroundColor = background; GUI.contentColor = content;
            GUI.enabled = enabled; GUI.changed = changed; GUI.matrix = matrix;
            EditorGUI.indentLevel = indent; EditorGUIUtility.labelWidth = labelWidth;
        }

        public void RequireUnchanged(string location)
        {
            Require(GUI.color == color && GUI.backgroundColor == background && GUI.contentColor == content &&
                GUI.enabled == enabled && GUI.changed == changed && GUI.matrix == matrix &&
                EditorGUI.indentLevel == indent && Mathf.Approximately(EditorGUIUtility.labelWidth, labelWidth),
                "Tutorial renderer leaked GUI state after " + location);
        }
    }

    public static void SpotlightFrames()
    {
        using (var f = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<TutorialProbeWindow>();
            try
            {
                window.Inspector = f.inspector;
                window.position = new Rect(50, 50, 540, 900);
                window.Show();
                int clicks = 0;
                window.DrawOnly = () => {
                    DiNeTutorialBubble.BeginFrame();
                    GUILayout.Space(40);
                    Rect anchor = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
                    GUI.Box(anchor, "Current test control");
                    DiNeTutorialBubble.Draw(anchor, "Test instruction", "One short test action.", "Use the current control.", false);
                    GUILayout.Space(24);
                    if (GUILayout.Button("Other test control", GUILayout.Height(24))) clicks++;
                    if (Event.current.type == EventType.Repaint) window.ActionRect = GUILayoutUtility.GetLastRect();
                    GUILayout.Space(80);
                    DiNeTutorialBubble.EndFrame();
                };
                window.RenderFrame(); window.RenderFrame();
                AssertSpotlightGeometry();
                Require(IntersectionArea(window.ActionRect, DiNeTutorialBubble.LastRect) == 0,
                    "Unrelated input control overlapped the bright bubble");
                window.ClickContentPoint(window.ActionRect.center);
                Require(clicks == 1, "Spotlight captured input intended for an unrelated dimmed control");
                window.RenderFrame();
                AssertSpotlightGeometry();

                window.DrawOnly = () => {
                    var original = GuiSnapshot.Take();
                    try
                    {
                        GUI.color = new Color(0.3f, 0.4f, 0.6f, 0.7f);
                        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f, 0.6f);
                        GUI.contentColor = new Color(0.7f, 0.5f, 0.3f, 0.8f);
                        GUI.enabled = false; GUI.changed = true;
                        GUI.matrix = Matrix4x4.Translate(new Vector3(7, 5, 0));
                        EditorGUI.indentLevel = 2; EditorGUIUtility.labelWidth = 77;
                        var expected = GuiSnapshot.Take();
                        DiNeTutorialBubble.BeginFrame();
                        expected.RequireUnchanged("BeginFrame");
                        GUILayout.Space(30);
                        Rect anchor = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
                        DiNeTutorialBubble.Draw(anchor, "Disabled caller", "Keep the caller's drawing state.", "Test state restoration.", false);
                        expected.RequireUnchanged("Draw");
                        GUILayout.Space(80);
                        DiNeTutorialBubble.EndFrame();
                        expected.RequireUnchanged("EndFrame");
                    }
                    finally { original.Restore(); }
                };
                window.RenderFrame(); window.RenderFrame();
                AssertSpotlightGeometry();

                window.DrawOnly = () => {
                    DiNeTutorialBubble.BeginFrame();
                    GUILayout.Space(300);
                    DiNeTutorialBubble.EndFrame();
                };
                window.RenderFrame();
                Require(!DiNeTutorialBubble.HasOverlay && DiNeTutorialBubble.LastDimmedRects.Length == 0 &&
                    DiNeTutorialBubble.LastRect == Rect.zero, "Inactive frame reused another inspector's spotlight");
                window.DrawOnly = () => {
                    DiNeTutorialBubble.BeginFrame();
                    Rect anchor = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
                    DiNeTutorialBubble.Draw(anchor, "Cancelled instruction", "Do not reuse this instruction.", "Cancelled.", false);
                    DiNeTutorialBubble.ClearFrame();
                    DiNeTutorialBubble.EndFrame();
                };
                window.RenderFrame();
                Require(!DiNeTutorialBubble.HasOverlay && DiNeTutorialBubble.LastDimmedRects.Length == 0,
                    "Cleared tutorial frame still painted the cancelled spotlight");
                Require(window.Error == null, "Spotlight GUI failed: " + window.Error);
            }
            finally { window.Inspector = null; window.Close(); }
        }
    }


}

// Test-only host: forwards production drawing through the same scroll behavior
// as the Unity Inspector. It adds no shipped controls or visual styling.
public sealed class TutorialProbeWindow : EditorWindow
{
    public DiNeMultiSupporter Inspector;
    public Exception Error;
    public int Layouts, Repaints;
    public Action DrawOnly;
    public Rect ActionRect;
    private Vector2 scroll;
    private Vector2 contentOrigin;

    public void RenderFrame()
    {
        SendEvent(new Event { type = EventType.Layout });
        SendEvent(new Event { type = EventType.Repaint });
    }

    public void ClickLastBubble()
    {
        // LastRect is in the scroll content coordinates used by the production bubble.
        ClickContentPoint(DiNeTutorialBubble.LastRect.center);
    }

    public void ClickContentPoint(Vector2 contentPoint)
    {
        // SendEvent coordinates include the native tab strip; GUILayout rects
        // use the content GUI origin. Derive that offset from Unity itself.
        Vector2 point = (DrawOnly == null ? contentPoint - scroll : contentPoint) + contentOrigin;
        SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
        SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
    }

    private void OnGUI()
    {
        if (Inspector == null) return;
        if (Event.current.type == EventType.Layout) Layouts++;
        if (Event.current.type == EventType.Repaint) Repaints++;
        contentOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero) - position.position;
        GUI.changed = false;
        if (DrawOnly != null)
        {
            // Keep controls below the EditorWindow tab strip, which intercepts
            // synthetic mouse input near the top of a hosted native window.
            GUILayout.Space(40);
            try { DrawOnly(); }
            catch (Exception exception) { Error = exception; }
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try { Inspector.OnInspectorGUI(); }
        catch (Exception exception) { Error = exception; }
        finally { EditorGUILayout.EndScrollView(); }
    }
}
#endif

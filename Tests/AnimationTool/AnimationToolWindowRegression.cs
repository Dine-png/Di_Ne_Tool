#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

public static partial class AnimationToolRegression
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private sealed class LanguagePreferences : IDisposable
    {
        [Serializable] private sealed class State
        {
            public bool languagePresent;
            public int language;
            public string[] keys, values;
            public bool[] present;
        }
        private readonly bool exists = EditorPrefs.HasKey("DiNeLang");
        private readonly int value = EditorPrefs.GetInt("DiNeLang");
        private readonly Dictionary<string, string> presetValues = new Dictionary<string, string>();
        public LanguagePreferences()
        {
            foreach (string prefix in new[] { "DiNe.AviEditor.ArmaturePreset", "DiNe.AviEditor.MAScalePreset" })
            foreach (string suffix in new[] { ".Guid", ".Path", ".Name" })
            {
                string key = prefix + suffix;
                presetValues[key] = EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : null;
            }
        }
        public void Dispose()
        {
            if (exists) EditorPrefs.SetInt("DiNeLang", value); else EditorPrefs.DeleteKey("DiNeLang");
            foreach (var pair in presetValues)
                if (pair.Value == null) EditorPrefs.DeleteKey(pair.Key); else EditorPrefs.SetString(pair.Key, pair.Value);
            Require(EditorPrefs.HasKey("DiNeLang") == exists && (!exists || EditorPrefs.GetInt("DiNeLang") == value), "Language preference restoration failed");
            foreach (var pair in presetValues)
                Require(EditorPrefs.HasKey(pair.Key) == (pair.Value != null) && (pair.Value == null || EditorPrefs.GetString(pair.Key) == pair.Value), "Preset preference restoration failed");
        }
        public void Persist(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(new State { languagePresent = exists, language = value,
                keys = presetValues.Keys.ToArray(), values = presetValues.Values.Select(v => v ?? "").ToArray(), present = presetValues.Values.Select(v => v != null).ToArray() }, true));
        }
        public static void Recover(string path)
        {
            if (!File.Exists(path)) return;
            State state = JsonUtility.FromJson<State>(File.ReadAllText(path));
            if (state.languagePresent) EditorPrefs.SetInt("DiNeLang", state.language); else EditorPrefs.DeleteKey("DiNeLang");
            for (int i = 0; i < state.keys.Length; i++)
                if (state.present[i]) EditorPrefs.SetString(state.keys[i], state.values[i]); else EditorPrefs.DeleteKey(state.keys[i]);
            File.Delete(path);
        }
    }

    private sealed class AnimationFixture : IDisposable
    {
        public readonly string folder;
        public readonly GameObject root;
        public readonly Transform bone;
        public readonly Mesh mesh;
        public readonly Material green, red;
        public readonly SkinnedMeshRenderer renderer;
        public readonly Animator animator;
        public readonly AnimatorController controller;
        public readonly VRCExpressionsMenu menu;
        public readonly VRCExpressionParameters parameters;
        public readonly VRCAvatarDescriptor descriptor;
        public readonly DiNeMultiDresser dresser;
        public readonly string scenePath;

        public AnimationFixture()
        {
            folder = "Assets/AnimationToolCase_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            root = new GameObject("Synthetic Animation Tool avatar");
            animator = root.AddComponent<Animator>();
            bone = new GameObject("Bone").transform; bone.SetParent(root.transform, false);
            mesh = new Mesh { name = "Synthetic triangle with shapes" };
            mesh.vertices = new[] { new Vector3(-.25f, 0, 0), new Vector3(.25f, 0, 0), new Vector3(0, .5f, 0) };
            // Double-sided synthetic geometry is visible in both body and face camera orientations.
            mesh.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back };
            mesh.bindposes = new[] { Matrix4x4.identity };
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 3).ToArray();
            mesh.AddBlendShapeFrame("Smile", 100, Enumerable.Repeat(new Vector3(.1f, 0, .05f), 3).ToArray(), new Vector3[3], new Vector3[3]);
            mesh.AddBlendShapeFrame("Other", 100, Enumerable.Repeat(new Vector3(0, .1f, 0), 3).ToArray(), new Vector3[3], new Vector3[3]);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, folder + "/OriginalMesh.asset");
            Shader shader = Shader.Find("Unlit/Color"); Require(shader != null, "Unlit/Color shader unavailable");
            green = new Material(shader) { name = "Original green material", color = Color.green };
            red = new Material(shader) { name = "Original red material", color = Color.red };
            AssetDatabase.CreateAsset(green, folder + "/Green.mat"); AssetDatabase.CreateAsset(red, folder + "/Red.mat");
            renderer = NewRenderer("Body"); renderer.SetBlendShapeWeight(0, 23); renderer.SetBlendShapeWeight(1, 37);
            controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/OriginalFX.controller");
            animator.runtimeAnimatorController = controller;
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            AssetDatabase.CreateAsset(menu, folder + "/OriginalMenu.asset"); AssetDatabase.CreateAsset(parameters, folder + "/OriginalParameters.asset");
            descriptor = root.AddComponent<VRCAvatarDescriptor>();
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer
            { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = controller, isDefault = false } };
            descriptor.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];
            descriptor.expressionsMenu = menu; descriptor.expressionParameters = parameters;
            dresser = root.AddComponent<DiNeMultiDresser>(); dresser.animatorController = controller; dresser.expressionsMenu = menu;
            root.AddComponent<AnimationToolPreviewProbe>();
            AssetDatabase.SaveAssets();
            scenePath = folder + "/OriginalScene.unity";
        }

        public SkinnedMeshRenderer NewRenderer(string path, bool duplicateLeaf = false)
        {
            Transform parent = root.transform;
            string[] segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                Transform existing = parent.Find(segments[i]);
                if (existing == null || (i == segments.Length - 1 && duplicateLeaf))
                {
                    existing = new GameObject(segments[i]).transform; existing.SetParent(parent, false);
                }
                parent = existing;
            }
            var result = parent.GetComponent<SkinnedMeshRenderer>();
            if (result == null) result = parent.gameObject.AddComponent<SkinnedMeshRenderer>();
            result.sharedMesh = mesh; result.sharedMaterial = green; result.bones = new[] { bone }; result.rootBone = bone;
            result.updateWhenOffscreen = true;
            return result;
        }

        public void SetNullReferences()
        {
            animator.runtimeAnimatorController = null;
            descriptor.customizeAnimationLayers = false;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer
            { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = null, isDefault = true } };
            descriptor.expressionsMenu = null; descriptor.expressionParameters = null;
            dresser.animatorController = null; dresser.expressionsMenu = null;
        }

        public void SaveScene()
        {
            AssetDatabase.SaveAssets();
            Require(EditorSceneManager.SaveScene(root.scene, scenePath), "Synthetic scene could not be saved");
        }

        public void Dispose()
        {
            if (root) Object.DestroyImmediate(root);
        }
    }

    private sealed class ReferenceSnapshot
    {
        private readonly bool customize;
        private readonly VRCAvatarDescriptor.CustomAnimLayer[] baseLayers, specialLayers;
        private readonly Object fx, menu, parameters, dresserFx, dresserMenu;
        private readonly string[] paths;

        public ReferenceSnapshot(AnimationFixture fixture)
        {
            customize = fixture.descriptor.customizeAnimationLayers;
            baseLayers = (VRCAvatarDescriptor.CustomAnimLayer[])fixture.descriptor.baseAnimationLayers.Clone();
            specialLayers = (VRCAvatarDescriptor.CustomAnimLayer[])fixture.descriptor.specialAnimationLayers.Clone();
            fx = fixture.animator.runtimeAnimatorController; menu = fixture.descriptor.expressionsMenu;
            parameters = fixture.descriptor.expressionParameters;
            dresserFx = fixture.dresser.animatorController; dresserMenu = fixture.dresser.expressionsMenu;
            paths = new[] { fx, menu, parameters, dresserFx, dresserMenu }.Select(Identity).ToArray();
        }

        private static string Identity(Object value)
        {
            if (value == null) return "null";
            string path = AssetDatabase.GetAssetPath(value);
            return path + "|" + AssetDatabase.AssetPathToGUID(path);
        }

        private static void LayersEqual(VRCAvatarDescriptor.CustomAnimLayer[] expected, VRCAvatarDescriptor.CustomAnimLayer[] actual, string label)
        {
            Require(actual != null && expected.Length == actual.Length, label + " count changed");
            for (int i = 0; i < expected.Length; i++)
                Require(expected[i].type == actual[i].type && expected[i].isDefault == actual[i].isDefault &&
                    expected[i].animatorController == actual[i].animatorController && expected[i].mask == actual[i].mask,
                    label + " reference/default flag changed");
        }

        public void Verify(GameObject root)
        {
            var descriptor = root.GetComponent<VRCAvatarDescriptor>(); var dresser = root.GetComponent<DiNeMultiDresser>();
            Require(descriptor != null && dresser != null, "Avatar reference components disappeared");
            Require(descriptor.customizeAnimationLayers == customize, "customizeAnimationLayers changed");
            LayersEqual(baseLayers, descriptor.baseAnimationLayers, "Base animation layers");
            LayersEqual(specialLayers, descriptor.specialAnimationLayers, "Special animation layers");
            var refs = new Object[] { root.GetComponent<Animator>().runtimeAnimatorController, descriptor.expressionsMenu,
                descriptor.expressionParameters, dresser.animatorController, dresser.expressionsMenu };
            Require(paths.SequenceEqual(refs.Select(Identity)), "FX/menu/parameters/dresser asset GUID/path or null state changed");
            Require(refs[0] == fx && refs[1] == menu && refs[2] == parameters && refs[3] == dresserFx && refs[4] == dresserMenu,
                "A source reference was replaced");
            Require(refs.All(reference => reference == null || !AssetDatabase.GetAssetPath(reference).Contains("__Temp")), "Synthetic avatar retains a temporary reference");
        }
    }

    private static AnimationClip PreviewClip(AnimationFixture f)
    {
        var clip = new AnimationClip { name = "Complete preview clip" };
        AnimationUtility.SetEditorCurve(clip, Float("Bone", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1, .25f));
        AnimationUtility.SetEditorCurve(clip, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"), AnimationCurve.Linear(0, 0, 1, 100));
        AnimationUtility.SetEditorCurve(clip, Float("Body", typeof(SkinnedMeshRenderer), "material._Color.r"), AnimationCurve.Linear(0, 0, 1, .8f));
        AnimationUtility.SetObjectReferenceCurve(clip, Material("Body"), new[]
        { new ObjectReferenceKeyframe { time = 0, value = f.green }, new ObjectReferenceKeyframe { time = .25f, value = f.red } });
        AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .25f, functionName = "RegressionSentinel", messageOptions = SendMessageOptions.DontRequireReceiver } });
        return clip;
    }

    private static void DetachedPreview()
    {
        foreach (bool nullReferences in new[] { false, true })
        using (var f = new AnimationFixture())
        {
            if (nullReferences) f.SetNullReferences();
            f.SaveScene();
            var refs = new ReferenceSnapshot(f);
            byte[] savedScene = File.ReadAllBytes(f.scenePath);
            byte[] savedMaterial = File.ReadAllBytes(f.folder + "/Green.mat"), savedSwapMaterial = File.ReadAllBytes(f.folder + "/Red.mat");
            string materialJson = EditorJsonUtility.ToJson(f.green), swapMaterialJson = EditorJsonUtility.ToJson(f.red), meshJson = EditorJsonUtility.ToJson(f.mesh);
            Vector3 bonePosition = f.bone.localPosition, bodyPosition = f.renderer.transform.localPosition;
            int[] rootIds = f.root.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(id => id).ToArray();
            int scenes = SceneManager.sceneCount, previewScenes = EditorSceneManager.previewSceneCount;
            int lifecycle = AnimationToolPreviewProbe.LifecycleCalls, events = AnimationToolPreviewProbe.AnimationEventCalls;
            var clip = PreviewClip(f);
            string clipJson = EditorJsonUtility.ToJson(clip);
            var preview = new DiNeAnimationPreview();
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32); target.Create();
            RenderTexture active = RenderTexture.active;
            int culls = 0; bool isolatedCamera = true;
            Camera.CameraCallback inspect = camera =>
            {
                if (camera.targetTexture != target) return;
                culls++;
                isolatedCamera &= camera.transform.parent != null && camera.scene != f.root.scene;
            };
            Camera.onPreCull += inspect;
            try
            {
                preview.Sample(f.root, clip, .5f);
                Require(preview.Root != null && preview.Root.scene != f.root.scene, "Preview root is missing or lives in source scene");
                Require(preview.Root.GetComponentInChildren<VRCAvatarDescriptor>(true) == null &&
                    preview.Root.GetComponentInChildren<DiNeMultiDresser>(true) == null &&
                    preview.Root.GetComponentInChildren<AnimationToolPreviewProbe>(true) == null, "Preview cloned avatar SDK/dresser/custom behaviours");
                Transform sampledBone = preview.GetTransform(f.bone);
                Require(sampledBone != null && sampledBone != f.bone, "Preview shares source bone transform");
                Equal(.125f, sampledBone.localPosition.x, "Animation transform sampling");
                var sampledBody = preview.GetTransform(f.renderer.transform).GetComponent<SkinnedMeshRenderer>();
                Equal(50, sampledBody.GetBlendShapeWeight(0), "Animation shape sampling");
                Require(sampledBody.sharedMaterial != f.green && sampledBody.sharedMaterial != f.red, "Preview shares editable source or animated replacement material");
                preview.SetShapeWeights(f.renderer, new[] { 65f, 75f });
                Equal(65, sampledBody.GetBlendShapeWeight(0), "Detached expression override");
                preview.Render(target, new Vector3(0, .25f, -1), Quaternion.identity, 45, .01f, 5);
                Require(ReadPixels(target).Any(pixel => pixel.a > 10 && (pixel.r > 20 || pixel.g > 20 || pixel.b > 20)), "Preview rendered no graphics");
                Require(culls > 0 && isolatedCamera, "Preview camera lacks private scene/parent/target contract");
                refs.Verify(f.root);
                Equal(bonePosition, f.bone.localPosition, "Preview changed source bone");
                Equal(bodyPosition, f.renderer.transform.localPosition, "Preview changed source renderer transform");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Preview changed selected source shape");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Preview changed unmentioned source shape");
                Require(materialJson == EditorJsonUtility.ToJson(f.green) && swapMaterialJson == EditorJsonUtility.ToJson(f.red) && meshJson == EditorJsonUtility.ToJson(f.mesh) &&
                    clipJson == EditorJsonUtility.ToJson(clip), "Preview changed source asset data");
                Require(savedMaterial.SequenceEqual(File.ReadAllBytes(f.folder + "/Green.mat")) && savedSwapMaterial.SequenceEqual(File.ReadAllBytes(f.folder + "/Red.mat")), "Preview wrote source/animated replacement material files");
                Require(rootIds.SequenceEqual(f.root.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(id => id)), "Preview added objects to source scene");
                Require(!f.root.scene.isDirty && SceneManager.sceneCount == scenes, "Preview dirtied source scene or added normal scene");
                Require(RenderTexture.active == active, "Preview changed active render target");
                Require(AnimationToolPreviewProbe.LifecycleCalls == lifecycle && AnimationToolPreviewProbe.AnimationEventCalls == events,
                    "Preview invoked source/custom lifecycle or animation event");
            }
            finally
            {
                Camera.onPreCull -= inspect; preview.Dispose(); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(clip);
            }
            Require(EditorSceneManager.previewSceneCount == previewScenes, "Preview scene leaked after Dispose");
            Require(savedScene.SequenceEqual(File.ReadAllBytes(f.scenePath)), "Preview changed saved scene bytes");
            EditorSceneManager.OpenScene(f.scenePath, OpenSceneMode.Single);
            GameObject reopened = GameObject.Find("Synthetic Animation Tool avatar");
            Require(reopened != null, "Saved synthetic avatar missing after reopen");
            refs.Verify(reopened);
            Object.DestroyImmediate(reopened);
        }
    }

    private static Color32[] ReadPixels(RenderTexture target)
    {
        RenderTexture active = RenderTexture.active;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = active; Object.DestroyImmediate(texture); }
    }

    private static void Set(DiNeAnimationTool window, string name, object value)
    {
        FieldInfo field = typeof(DiNeAnimationTool).GetField(name, InstanceFields);
        Require(field != null, "Window API missing field " + name);
        if (field.FieldType.IsEnum && value is int) value = Enum.ToObject(field.FieldType, value);
        field.SetValue(window, value);
    }

    private static T Get<T>(DiNeAnimationTool window, string name)
    {
        FieldInfo field = typeof(DiNeAnimationTool).GetField(name, InstanceFields);
        Require(field != null, "Window API missing field " + name);
        return (T)field.GetValue(window);
    }

    private static void Call(DiNeAnimationTool window, string name, params object[] args)
    {
        MethodInfo method = typeof(DiNeAnimationTool).GetMethod(name, InstanceFields);
        Require(method != null, "Window API missing method " + name);
        try { method.Invoke(window, args); }
        catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
    }

    private static void WindowExpression()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            var clip = new AnimationClip();
            try
            {
                AnimationUtility.SetEditorCurve(clip, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"), AnimationCurve.Constant(0, 1, 10));
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                Set(window, "_exprShapeValues", new[] { 75f, 80f });
                Set(window, "_exprClip", clip); Call(window, "LoadExpressionFromClip");
                var values = Get<float[]>(window, "_exprShapeValues");
                Equal(10, values[0], "Expression clip was not loaded"); Equal(80, values[1], "Expression clip cleared unmentioned value");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Expression loading changed source selected weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Expression loading changed source other weight");
                refs.Verify(f.root);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(clip); }
            refs.Verify(f.root);
        }
    }

    private static void WindowApplyPrefab(AnimationFixture f, ReferenceSnapshot refs)
    {
        string path = f.folder + "/OriginalAvatar.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(f.root, path);
        Require(prefab != null, "Synthetic prefab could not be saved");
        byte[] original = File.ReadAllBytes(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "Synthetic prefab apply instance";
        Transform bone = instance.transform.Find("Bone");
        var renderer = instance.transform.Find("Body").GetComponent<SkinnedMeshRenderer>();
        var clip = PreviewClip(f);
        var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
        try
        {
            f.SaveScene();
            Set(window, "targetAvatarRoot", instance); Call(window, "RefreshAvatar");
            Set(window, "animationClip", clip); Set(window, "clipTime", .5f);
            Call(window, "ApplyBoth"); Undo.FlushUndoRecordObjects();
            Equal(.125f, bone.localPosition.x, "Prefab pose apply");
            Equal(50, renderer.GetBlendShapeWeight(0), "Prefab shape apply");
            Require(instance.scene.isDirty, "Explicit prefab apply did not mark scene dirty");
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instance);
            Require(modifications.Any(m => m.propertyPath == "m_LocalPosition.x"), "Pose prefab override was not recorded");
            Require(modifications.Any(m => m.propertyPath.Contains("m_BlendShapeWeights")), "Shape prefab override was not recorded");
            refs.Verify(instance);
            Undo.PerformUndo();
            Equal(0, bone.localPosition.x, "Prefab apply Undo pose");
            Equal(23, renderer.GetBlendShapeWeight(0), "Prefab apply Undo shape");
            Undo.PerformRedo();
            Equal(.125f, bone.localPosition.x, "Prefab apply Redo pose");
            Equal(50, renderer.GetBlendShapeWeight(0), "Prefab apply Redo shape");
            Call(window, "RestoreToOriginal");
            Equal(0, bone.localPosition.x, "Prefab captured pose restore");
            Equal(23, renderer.GetBlendShapeWeight(0), "Prefab captured shape restore");
            refs.Verify(instance);
        }
        finally { Object.DestroyImmediate(window); Object.DestroyImmediate(clip); }
        f.SaveScene();
        Require(original.SequenceEqual(File.ReadAllBytes(path)), "Prefab apply mutated the original prefab asset");
        EditorSceneManager.OpenScene(f.scenePath, OpenSceneMode.Single);
        GameObject reloaded = GameObject.Find("Synthetic prefab apply instance");
        Require(reloaded != null && PrefabUtility.IsPartOfPrefabInstance(reloaded), "Saved synthetic prefab instance was not preserved");
        Equal(0, reloaded.transform.Find("Bone").localPosition.x, "Saved prefab restored pose");
        Equal(23, reloaded.transform.Find("Body").GetComponent<SkinnedMeshRenderer>().GetBlendShapeWeight(0), "Saved prefab restored shape");
        refs.Verify(reloaded);
    }

    private static void WindowApplyRestore()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var clip = PreviewClip(f);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            Vector3 position = f.bone.localPosition;
            try
            {
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                Set(window, "animationClip", clip); Set(window, "clipTime", .5f);
                Call(window, "ApplyBoth");
                Equal(.125f, f.bone.localPosition.x, "ApplyBoth did not apply sampled transform");
                Equal(50, f.renderer.GetBlendShapeWeight(0), "ApplyBoth did not apply sampled shape");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "ApplyBoth changed unmentioned shape");
                refs.Verify(f.root);
                Call(window, "RestoreToOriginal");
                Equal(position, f.bone.localPosition, "Restore did not restore original transform");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Restore did not restore original shape");
                refs.Verify(f.root);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(clip); }
            refs.Verify(f.root);
            WindowApplyPrefab(f, refs);
        }
    }

    private static void WindowLanguagesAndTabs()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var tool = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            var host = ScriptableObject.CreateInstance<AnimationToolRegressionHost>();
            var clip = PreviewClip(f);
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, kind) =>
            { if (kind == LogType.Exception || kind == LogType.Error || kind == LogType.Assert) errors.Add(message + "\n" + stack); };
            Application.logMessageReceived += capture;
            try
            {
                tool.position = new Rect(0, 0, 440, 850); host.position = new Rect(0, 0, 440, 850);
                Set(tool, "targetAvatarRoot", f.root); Set(tool, "animationClip", clip); Call(tool, "RefreshAvatar");
                Set(tool, "repairClips", new List<AnimationClip> { clip }); Call(tool, "AnalyzeRepairClips");
                host.Tool = tool; host.ShowUtility();
                for (int language = 0; language < 3; language++)
                {
                    EditorPrefs.SetInt("DiNeLang", language);
                    for (int tab = 0; tab < 3; tab++)
                    {
                        Set(tool, "selectedTab", tab);
                        int beforeLayouts = host.Layouts, beforeRepaints = host.Repaints;
                        for (int frame = 0; frame < 3; frame++) host.Render();
                        Require(host.Layouts >= beforeLayouts + 3 && host.Repaints >= beforeRepaints + 3,
                            "GUI did not receive Layout/Repaint for language " + language + " tab " + tab);
                        Require(host.Failure == null, "Window draw failed: " + host.Failure);
                        refs.Verify(f.root);
                    }
                }
                Require(errors.Count == 0, "Localized GUI emitted errors: " + string.Join("\n", errors));
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Window draw changed source shape");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Window draw changed unmentioned source shape");
                Results.Add("METRIC actual Animation Tool GUI: Layout=" + host.Layouts + ", Repaint=" + host.Repaints + ", languages=3, tabs=3");
            }
            finally
            {
                Application.logMessageReceived -= capture; host.Tool = null; host.Close();
                Object.DestroyImmediate(host); Object.DestroyImmediate(tool); Object.DestroyImmediate(clip);
            }
            refs.Verify(f.root);
        }
    }

    private static void ExpressionOverwrite()
    {
        using (var f = new AnimationFixture())
        {
            f.NewRenderer("OtherMesh");
            var refs = new ReferenceSnapshot(f);
            var clip = new AnimationClip { name = "Mixed expression asset", frameRate = 36 };
            var shape = Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var otherShape = Float("OtherMesh", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var pose = Float("Bone", typeof(Transform), "m_LocalPosition.x");
            var materialFloat = Float("Body", typeof(SkinnedMeshRenderer), "material._Color.r");
            AnimationUtility.SetEditorCurve(clip, shape, WeightedCurve(15));
            foreach (var binding in new[] { otherShape, pose, materialFloat }) AnimationUtility.SetEditorCurve(clip, binding, WeightedCurve(42));
            var materialKeys = new[] { new ObjectReferenceKeyframe { time = .5f, value = f.red } };
            AnimationUtility.SetObjectReferenceCurve(clip, Material("Body"), materialKeys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.cycleOffset = .35f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .25f, functionName = "RegressionSentinel", intParameter = 9 } });
            AssetDatabase.CreateAsset(clip, f.folder + "/Mixed.anim"); AssetDatabase.SaveAssets();
            var settingsJson = JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip));
            var preserved = new[] { otherShape, pose, materialFloat }.ToDictionary(b => b, b => AnimationUtility.GetEditorCurve(clip, b));
            var eventsCopy = Object.Instantiate(clip);
            byte[] unrelatedDisk = File.ReadAllBytes(f.folder + "/Red.mat");
            f.red.color = Color.blue; EditorUtility.SetDirty(f.red);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            try
            {
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                Set(window, "_exprClip", clip); Set(window, "_exprShapeValues", new[] { 83f, 0f });
                Set(window, "_includeGestureKeys", false); Call(window, "SaveExpressionClip", true);
                Equal(83, AnimationUtility.GetEditorCurve(clip, shape).Evaluate(0), "Expression overwrite did not save working value");
                foreach (var pair in preserved) EqualCurve(pair.Value, AnimationUtility.GetEditorCurve(clip, pair.Key), "Unrelated expression curve");
                EqualObjects(materialKeys, AnimationUtility.GetObjectReferenceCurve(clip, Material("Body")), "Overwrite object material curves");
                Require(settingsJson == JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip)), "Expression overwrite changed clip settings");
                Equal(36, clip.frameRate, "Expression overwrite changed frame rate"); EqualEvents(eventsCopy, clip);
                Require(unrelatedDisk.SequenceEqual(File.ReadAllBytes(f.folder + "/Red.mat")) && EditorUtility.IsDirty(f.red), "Expression save persisted an unrelated dirty asset");
                refs.Verify(f.root);
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(eventsCopy); }
        }
    }

    private static void NestedFxReplacement()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var a = new AnimationClip { name = "Nested original A" }; var b = new AnimationClip { name = "Nested original B" };
            AnimationUtility.SetEditorCurve(a, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile"), AnimationCurve.Constant(0, 1, 12));
            AnimationUtility.SetEditorCurve(b, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Other"), AnimationCurve.Constant(0, 1, 34));
            AssetDatabase.CreateAsset(a, f.folder + "/A.anim"); AssetDatabase.CreateAsset(b, f.folder + "/B.anim");
            var layers = f.controller.layers; layers[0].name = "Left Hand"; f.controller.layers = layers;
            f.controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
            var outer = new BlendTree { name = "Outer", blendParameter = "Blend" };
            var inner = new BlendTree { name = "Inner", blendParameter = "Blend" };
            AssetDatabase.AddObjectToAsset(outer, f.controller); AssetDatabase.AddObjectToAsset(inner, f.controller);
            inner.AddChild(a, 0); inner.AddChild(b, 1); outer.AddChild(inner, 0); outer.AddChild(b, 1);
            var nested = f.controller.layers[0].stateMachine.AddStateMachine("Nested gesture machine");
            nested.AddState("Nested gesture").motion = outer;
            AssetDatabase.SaveAssets();
            string aJson = EditorJsonUtility.ToJson(a), bJson = EditorJsonUtility.ToJson(b);
            byte[] unrelatedDisk = File.ReadAllBytes(f.folder + "/Red.mat");
            f.red.color = Color.blue; EditorUtility.SetDirty(f.red);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            try
            {
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                Set(window, "_exprShapeValues", new[] { 83f, 0f }); Set(window, "_includeGestureKeys", false);
                var entries = (Array)typeof(DiNeAnimationTool).GetMethod("GetHandLayerClips", InstanceFields).Invoke(window, new object[] { f.controller, "Left Hand" });
                int chosen = -1;
                for (int i = 0; i < entries.Length; i++)
                    if ((AnimationClip)entries.GetValue(i).GetType().GetField("clip", InstanceFields).GetValue(entries.GetValue(i)) == a) chosen = i;
                Require(chosen >= 0 && entries.Length == 3, "Nested blend-tree gesture leaves were not enumerated");
                Undo.ClearAll(); Undo.IncrementCurrentGroup();
                Call(window, "ReplaceClipInFxLayer", f.controller, "Left Hand", chosen); Undo.FlushUndoRecordObjects();
                var replacement = inner.children[0].motion as AnimationClip;
                Require(replacement != null && replacement != a, "Nested leaf was not replaced with a new expression asset");
                Equal(83, AnimationUtility.GetEditorCurve(replacement, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile")).Evaluate(0), "Replacement did not represent working expression");
                Require(inner.children[1].motion == b && outer.children[0].motion == inner && outer.children[1].motion == b, "Replacement changed a neighboring tree/leaf");
                Require(aJson == EditorJsonUtility.ToJson(a) && bJson == EditorJsonUtility.ToJson(b), "Replacement modified original gesture clips");
                Require(unrelatedDisk.SequenceEqual(File.ReadAllBytes(f.folder + "/Red.mat")) && EditorUtility.IsDirty(f.red), "FX replacement persisted an unrelated dirty asset");
                refs.Verify(f.root);
                Undo.PerformUndo(); Require(inner.children[0].motion == a, "Undo did not restore original nested gesture leaf");
                Undo.PerformRedo();
                Require(inner.children[0].motion is AnimationClip && inner.children[0].motion != a, "Redo did not restore expression replacement");
                refs.Verify(f.root);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }

    private static void WindowRepairSave()
    {
        using (var f = new AnimationFixture())
        {
            f.SaveScene(); var refs = new ReferenceSnapshot(f);
            var sourceBinding = Float("OldFace", typeof(SkinnedMeshRenderer), "blendShape.OldSmile");
            var changed = new AnimationClip { name = "Same output name" }; var unchanged = new AnimationClip { name = "Same output name" };
            AnimationUtility.SetEditorCurve(changed, sourceBinding, WeightedCurve(25));
            AnimationUtility.SetEditorCurve(unchanged, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Other"), WeightedCurve(60));
            AssetDatabase.CreateAsset(changed, f.folder + "/Changed.anim"); AssetDatabase.CreateAsset(unchanged, f.folder + "/Unchanged.anim"); AssetDatabase.SaveAssets();
            byte[] sourceBytes = File.ReadAllBytes(f.folder + "/Changed.anim"), savedScene = File.ReadAllBytes(f.scenePath);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            try
            {
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                Set(window, "repairClips", new List<AnimationClip> { changed, unchanged, changed });
                Set(window, "repairMappings", new List<DiNeAnimationRepairCore.BindingMapping> { Map(sourceBinding, false, "Body", "blendShape.Smile") });
                Call(window, "SaveRepairedClipsToFolder", f.folder);
                Require(!Get<bool>(window, "repairStatusError"), "Repair window reported save failure");
                var outputs = Get<List<AnimationClip>>(window, "repairSavedClips");
                string output = Get<string>(window, "repairOutputFolder");
                Require(outputs.Count == 1 && outputs[0] != changed && AssetDatabase.Contains(outputs[0]), "Save copied unchanged/duplicate clips or reused original");
                Require(File.Exists(output + "/MappingTable.json") && File.Exists(output + "/RepairReport.json"), "Save did not write mapping/report artifacts");
                EqualCurve(AnimationUtility.GetEditorCurve(changed, sourceBinding), AnimationUtility.GetEditorCurve(outputs[0], Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile")), "Window saved repair");
                byte[] firstBytes = File.ReadAllBytes(AssetDatabase.GetAssetPath(outputs[0]));
                string firstFolder = output;
                Call(window, "SaveRepairedClipsToFolder", f.folder);
                Require(Get<string>(window, "repairOutputFolder") != firstFolder, "Repeated save reused an output folder");
                Require(firstBytes.SequenceEqual(File.ReadAllBytes(AssetDatabase.GetAssetPath(outputs[0]))) && sourceBytes.SequenceEqual(File.ReadAllBytes(f.folder + "/Changed.anim")), "Repeated save overwrote prior output/source");
                refs.Verify(f.root);
                Require(!f.root.scene.isDirty && savedScene.SequenceEqual(File.ReadAllBytes(f.scenePath)), "Saving copies changed source scene");
            }
            finally { Object.DestroyImmediate(window); }
            EditorSceneManager.OpenScene(f.scenePath, OpenSceneMode.Single);
            var reopened = GameObject.Find("Synthetic Animation Tool avatar"); refs.Verify(reopened); Object.DestroyImmediate(reopened);
        }
    }

    private static void TransientPreviewLifetime()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var source = new AnimationClip();
            var old = Float("Old", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            AnimationUtility.SetEditorCurve(source, old, WeightedCurve(12));
            string before = EditorJsonUtility.ToJson(source);
            var window = ScriptableObject.CreateInstance<DiNeAnimationTool>();
            try
            {
                Set(window, "targetAvatarRoot", f.root); Call(window, "RefreshAvatar");
                var first = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", first);
                Require(Get<AnimationClip>(window, "transientPreviewClip") == first && Get<AnimationClip>(window, "animationClip") == first, "Transient repaired clip was not adopted for preview");
                var second = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", second);
                Require(first == null && Get<AnimationClip>(window, "transientPreviewClip") == second, "Replacing transient preview leaked first clip");
                Call(window, "PreviewClip", source);
                Require(second == null && Get<AnimationClip>(window, "animationClip") == source, "Selecting source did not dispose transient preview");
                var third = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", third); Call(window, "RefreshAvatar");
                Require(third == null && Get<AnimationClip>(window, "transientPreviewClip") == null, "Avatar refresh retained owned preview clip");
                var fourth = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", fourth); Call(window, "ReleaseSessionPreview");
                Require(fourth == null, "Reload/session cleanup leaked transient preview clip");
                var fifth = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", fifth); Call(window, "OnPlayModeChanged", PlayModeStateChange.ExitingEditMode);
                Require(fifth == null, "Play-mode cleanup callback leaked transient preview clip");
                var sixth = DiNeAnimationRepairCore.CreateRepairedClip(source, f.root, new[] { Map(old, false, "Body") });
                Call(window, "PreviewTransientClip", sixth); Object.DestroyImmediate(window); window = null;
                Require(sixth == null, "Closing window leaked transient preview clip");
                Require(before == EditorJsonUtility.ToJson(source), "Transient preview changed original clip"); refs.Verify(f.root);
            }
            finally { if (window != null) Object.DestroyImmediate(window); Object.DestroyImmediate(source); }
        }
    }

    private static void AviLanguagesAndTabs()
    {
        using (var f = new AnimationFixture())
        {
            var refs = new ReferenceSnapshot(f);
            var avi = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            var host = ScriptableObject.CreateInstance<AnimationToolRegressionHost>();
            var mode = typeof(ArmatureScalerEditor).GetNestedType("EditorMode", BindingFlags.NonPublic);
            Require(mode != null && Enum.GetNames(mode).SequenceEqual(new[] { "Armature", "ShapeKeyEditor", "Extra" }), "Removed animation/expression tabs remain in Avi Editor");
            try
            {
                avi.position = new Rect(0, 0, 440, 850); host.position = new Rect(0, 0, 440, 850);
                typeof(ArmatureScalerEditor).GetField("targetAvatarRoot", InstanceFields).SetValue(avi, f.root);
                host.Tool = avi; host.ShowUtility();
                MethodInfo select = typeof(ArmatureScalerEditor).GetMethod("SetMainModeIndex", InstanceFields);
                for (int language = 0; language < 3; language++)
                {
                    EditorPrefs.SetInt("DiNeLang", language);
                    for (int tab = 0; tab < 3; tab++)
                    {
                        select.Invoke(avi, new object[] { tab });
                        int layouts = host.Layouts, repaints = host.Repaints;
                        for (int frame = 0; frame < 3; frame++) host.Render();
                        Require(host.Layouts >= layouts + 3 && host.Repaints >= repaints + 3, "Avi Editor did not receive localized GUI events");
                        Require(host.Failure == null, "Retained Avi Editor tab failed: " + host.Failure);
                        refs.Verify(f.root);
                    }
                }
                Results.Add("METRIC retained Avi Editor GUI: Layout=" + host.Layouts + ", Repaint=" + host.Repaints + ", languages=3, tabs=3");
            }
            finally { host.Tool = null; host.Close(); Object.DestroyImmediate(host); Object.DestroyImmediate(avi); }
            refs.Verify(f.root);
        }
    }
}

public sealed class AnimationToolRegressionHost : EditorWindow
{
    public EditorWindow Tool;
    public int Layouts, Repaints;
    public Exception Failure;
    public void Render()
    {
        SendEvent(new Event { type = EventType.Layout });
        SendEvent(new Event { type = EventType.Repaint });
    }
    private void OnGUI()
    {
        if (!Tool) return;
        if (Event.current.type == EventType.Layout) Layouts++;
        if (Event.current.type == EventType.Repaint) Repaints++;
        try
        {
            Tool.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Tool, null);
        }
        catch (TargetInvocationException exception)
        {
            if (!(exception.InnerException is ExitGUIException)) Failure = exception.InnerException ?? exception;
        }
        catch (ExitGUIException) { }
        catch (Exception exception) { Failure = exception; }
    }
}
#endif

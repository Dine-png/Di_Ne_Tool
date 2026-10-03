#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AviEditorRegression
{
    private static readonly List<string> Results = new List<string>();
    private static int failures;

    public static void Run()
    {
        // Window initialization and picker tests share EditorPrefs with other editors.
        // Restore these keys even when an individual test fails.
        using (var preferences = new PresetPreferences())
        {
            Test("Unified armature preset captures live data and survives asset reimport", UnifiedPresetRoundtrip);
            Test("Full unified preset restores mapped positions without applying MA offsets twice", UnifiedPresetApply);
            Test("Partial unified preset loads leave unrequested data intact", UnifiedPresetPartialApply);
            Test("Unified preset Undo/Redo restores transforms, added MA components and options together", UnifiedPresetUndo);
            Test("MA preset entries skip missing, unknown and null parts", InvalidMAPresetEntries);
            Test("Legacy direct and MA presets remain selectable and load through one picker", LegacyPresetCompatibility);
            Test("Saved scale changes deformation at 0, 50 and 100 and preserves other weights", PersistentScale);
            Test("Japanese key at a late index keeps a saved 70% scale after preview closes", JapaneseSeventyPercentScale);
            Test("Scale preserves multi-frame keys, all UV channels and custom bounds", PreserveMeshData);
            Test("Consecutive edits keep earlier assets intact and support Undo/Redo", ConsecutiveEdits);
            Test("Equal mesh names cannot overwrite another avatar's result", AssetNameCollision);
            Test("Applied mesh is a persistent prefab override after saving and reopening", PrefabOverride);
            Test("Preview renders edited shape in a private scene and releases every temporary object", IsolatedPreview);
            Test("Modify selection switches exclusively and toggles off without changing source weights", WindowShapePreview);
            Test("Replacement preview matches saved deformation with other active source keys", WindowReplacementPreview);
            Test("Head framing ignores oversized culling bounds and finds named eye bones", HeadFraming);
        }
        Results.Add("Failures: " + failures);
        File.WriteAllLines("AviEditorRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Test(string name, Action action)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Undo.ClearAll();
        try { action(); Results.Add("PASS " + name); }
        catch (Exception e) { failures++; Results.Add("FAIL " + name + ": " + e); }
        Debug.Log(Results[Results.Count - 1]);
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void Equal(float expected, float actual, string message)
    {
        Require(Mathf.Abs(expected - actual) < 0.0001f, message + ": expected " + expected + ", actual " + actual);
    }

    private static void Equal(Vector3 expected, Vector3 actual, string message)
    {
        Require((expected - actual).sqrMagnitude < 0.00000001f, message + ": expected " + expected + ", actual " + actual);
    }

    private static void Equal(Quaternion expected, Quaternion actual, string message)
    {
        Require(Mathf.Abs(Quaternion.Dot(expected, actual)) > .999999f,
            message + ": expected " + expected + ", actual " + actual);
    }

    private sealed class PresetPreferences : IDisposable
    {
        private readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public PresetPreferences()
        {
            foreach (string prefix in new[] { "DiNe.AviEditor.ArmaturePreset", "DiNe.AviEditor.MAScalePreset" })
            foreach (string suffix in new[] { ".Guid", ".Path", ".Name" })
            {
                string key = prefix + suffix;
                values.Add(key, EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key) : null);
            }
        }
        public void Dispose()
        {
            foreach (var value in values)
                if (value.Value == null) EditorPrefs.DeleteKey(value.Key);
                else EditorPrefs.SetString(value.Key, value.Value);
        }
    }

    private sealed class ArmatureFixture : IDisposable
    {
        public readonly GameObject root = new GameObject("Synthetic armature avatar");
        public readonly Transform neck;
        public readonly Transform head;
        public readonly Transform unmappedChild;
        public readonly ArmatureScalerEditor window;
        public readonly string folder;
        public ArmatureFixture()
        {
            folder = "Assets/ArmatureCase_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            neck = new GameObject("Neck").transform;
            neck.SetParent(root.transform, false);
            head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            unmappedChild = new GameObject("Unmapped attachment").transform;
            unmappedChild.SetParent(neck, false);
            window = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            Set(window, "targetAvatarRoot", root);
            Set(window, "boneMapping", new Dictionary<HumanBodyBones, Transform>
            {
                { HumanBodyBones.Neck, neck }, { HumanBodyBones.Head, head }
            });
        }
        public nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster AddNeckMA(Vector3 scale, bool adjustChildren)
        {
            var component = neck.gameObject.AddComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>();
            component.Scale = scale;
            SetOption("Neck", adjustChildren);
            return component;
        }
        public void SetOption(string part, bool enabled)
        {
            Type type = typeof(ArmatureScalerEditor).GetNestedType("HumanoidBodyPart", BindingFlags.NonPublic);
            Call(window, "SetMAAdjustChildPositions", Enum.Parse(type, part), enabled);
        }
        public bool Option(string part) => Get<List<string>>(window, "maAdjustChildPositionParts").Contains(part);
        public ArmatureScalerPresetData Capture()
        {
            var preset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            Call(window, "CaptureArmaturePreset", preset);
            return preset;
        }
        public void Apply(ArmatureScalerPresetData preset, bool scale = true, bool rotation = true, bool position = true, bool ma = true)
        {
            Call(window, "ApplyArmaturePreset", preset, scale, rotation, position, ma);
        }
        public void Dispose()
        {
            if (window != null) Object.DestroyImmediate(window);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    private static void UnifiedPresetRoundtrip()
    {
        using (var f = new ArmatureFixture())
        {
            f.neck.localScale = new Vector3(.8f, 1.1f, .9f);
            f.neck.localPosition = new Vector3(.1f, .4f, -.2f);
            f.neck.localRotation = Quaternion.Euler(12, 23, 34);
            f.head.localScale = new Vector3(1.2f, .95f, 1.05f);
            f.head.localPosition = new Vector3(.02f, .17f, .04f);
            f.head.localRotation = Quaternion.Euler(5, 6, 7);
            Vector3 maScale = new Vector3(1.3f, .7f, 1.15f);
            f.AddNeckMA(maScale, true);
            Type partType = typeof(ArmatureScalerEditor).GetNestedType("HumanoidBodyPart", BindingFlags.NonPublic);
            object neckPart = Enum.Parse(partType, "Neck");
            ((System.Collections.IDictionary)Get<object>(f.window, "scaleValues"))[neckPart] = Vector3.one * 9;
            ((System.Collections.IDictionary)Get<object>(f.window, "rotationValues"))[neckPart] = Quaternion.identity;
            ((System.Collections.IDictionary)Get<object>(f.window, "positionValues"))[neckPart] = Vector3.one * 8;
            var preset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            preset.scales.dictionary.Add("Stale", new ArmatureScalerPresetData.SerializableVector3(Vector3.one));
            preset.positions.dictionary.Add("Stale", new ArmatureScalerPresetData.SerializableVector3(Vector3.one));
            preset.rotations.dictionary.Add("Stale", new ArmatureScalerPresetData.SerializableQuaternion(Quaternion.identity));
            preset.maScales.Add(new MAScaleAdjusterPresetData.Entry("Stale", Vector3.one, false));
            Call(f.window, "CaptureArmaturePreset", preset);
            Require(preset.scales.dictionary.Count == 2 && preset.positions.dictionary.Count == 2,
                "Capture retained stale/default unmapped direct entries");
            Require(preset.rotations.dictionary.Count == 1 && !preset.rotations.dictionary.ContainsKey("Head"),
                "Capture did not preserve the existing allowed-rotation policy");
            Equal(f.neck.localScale, preset.scales["Neck"].ToVector3(), "Capture used cached scale");
            Equal(f.neck.localPosition, preset.positions["Neck"].ToVector3(), "Capture used cached position");
            Equal(f.neck.localRotation, preset.rotations["Neck"].ToQuaternion(), "Capture used cached rotation");
            Require(preset.maScales.Count == 1 && preset.maScales[0].part == "Neck" && preset.maScales[0].adjustChildPositions,
                "Capture lost MA component or child-position option");
            string path = f.folder + "/Combined.asset";
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<ArmatureScalerPresetData>(path);
            Equal(f.head.localScale, reloaded.scales["Head"].ToVector3(), "Reimport lost head scale");
            Equal(f.head.localPosition, reloaded.positions["Head"].ToVector3(), "Reimport lost head position");
            Equal(f.neck.localRotation, reloaded.rotations["Neck"].ToQuaternion(), "Reimport lost rotation");
            Require(reloaded.maScales.Count == 1 && reloaded.maScales[0].adjustChildPositions,
                "Reimport lost unified MA data");
            Equal(maScale, reloaded.maScales[0].Scale, "Reimport lost MA scale");
            Object.DestroyImmediate(f.neck.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>());
            Call(f.window, "CaptureArmaturePreset", reloaded);
            Require(reloaded.maScales.Count == 0, "Overwriting capture retained an obsolete MA component");
        }
    }

    private static void UnifiedPresetApply()
    {
        using (var f = new ArmatureFixture())
        {
            f.neck.localScale = new Vector3(.9f, 1.1f, 1.2f);
            f.neck.localPosition = new Vector3(.1f, .2f, .3f);
            f.neck.localRotation = Quaternion.Euler(9, 18, 27);
            f.head.localScale = new Vector3(1.05f, .8f, .95f);
            f.head.localPosition = new Vector3(.04f, .19f, .02f);
            var ma = f.AddNeckMA(new Vector3(2, 1.5f, .5f), true);
            var preset = f.Capture();
            try
            {
                f.neck.localScale = Vector3.one * .5f;
                f.neck.localPosition = Vector3.one * .7f;
                f.neck.localRotation = Quaternion.identity;
                f.head.localScale = Vector3.one * .6f;
                f.head.localPosition = Vector3.one;
                f.unmappedChild.localPosition = new Vector3(1, 2, 3);
                ma.Scale = Vector3.one;
                f.SetOption("Neck", false);
                f.Apply(preset);
                Equal(preset.scales["Neck"].ToVector3(), f.neck.localScale, "Full apply neck scale");
                Equal(preset.positions["Neck"].ToVector3(), f.neck.localPosition, "Full apply neck position");
                Equal(preset.rotations["Neck"].ToQuaternion(), f.neck.localRotation, "Full apply neck rotation");
                Equal(preset.scales["Head"].ToVector3(), f.head.localScale, "Full apply head scale");
                Equal(preset.positions["Head"].ToVector3(), f.head.localPosition, "MA offset applied twice to mapped head");
                Equal(new Vector3(2, 3, 1.5f), f.unmappedChild.localPosition, "Unmapped child lost requested MA adjustment");
                Require(f.Option("Neck"), "Full apply did not restore child-position option");
                Equal(preset.maScales[0].Scale, ma.Scale, "Full apply MA scale");
                f.Apply(preset);
                Equal(preset.positions["Head"].ToVector3(), f.head.localPosition, "Repeated full apply changed mapped position");
                Equal(new Vector3(2, 3, 1.5f), f.unmappedChild.localPosition, "Repeated full apply scaled child position again");
            }
            finally { Object.DestroyImmediate(preset); }
        }
    }

    private static void UnifiedPresetPartialApply()
    {
        using (var f = new ArmatureFixture())
        {
            f.neck.localScale = new Vector3(.7f, .8f, .9f);
            f.neck.localRotation = Quaternion.Euler(10, 20, 30);
            f.neck.localPosition = new Vector3(.2f, .3f, .4f);
            f.head.localPosition = new Vector3(.01f, .18f, .03f);
            var ma = f.AddNeckMA(new Vector3(1.1f, 1.2f, 1.3f), false);
            var preset = f.Capture();
            try
            {
                for (int mode = 0; mode < 4; mode++)
                {
                    Vector3 beforeScale = Vector3.one * .55f;
                    Vector3 beforePosition = new Vector3(.8f, .7f, .6f);
                    Quaternion beforeRotation = Quaternion.Euler(40, 50, 60);
                    Vector3 beforeMA = Vector3.one * .85f;
                    f.neck.localScale = beforeScale;
                    f.neck.localPosition = beforePosition;
                    f.neck.localRotation = beforeRotation;
                    f.head.localPosition = beforePosition;
                    ma.Scale = beforeMA;
                    f.SetOption("Neck", true);
                    f.Apply(preset, mode == 0, mode == 1, mode == 2, mode == 3);
                    Equal(mode == 0 ? preset.scales["Neck"].ToVector3() : beforeScale, f.neck.localScale,
                        "Partial mode " + mode + " changed wrong direct scale");
                    Equal(mode == 1 ? preset.rotations["Neck"].ToQuaternion() : beforeRotation, f.neck.localRotation,
                        "Partial mode " + mode + " changed wrong rotation");
                    Equal(mode == 2 ? preset.positions["Neck"].ToVector3() : beforePosition, f.neck.localPosition,
                        "Partial mode " + mode + " changed wrong position");
                    Equal(mode == 2 ? preset.positions["Head"].ToVector3() : beforePosition, f.head.localPosition,
                        "Partial mode " + mode + " changed wrong child position");
                    Equal(mode == 3 ? preset.maScales[0].Scale : beforeMA, ma.Scale,
                        "Partial mode " + mode + " changed wrong MA scale");
                    Require(f.Option("Neck") == (mode != 3), "Partial mode changed unrequested MA option");
                }
            }
            finally { Object.DestroyImmediate(preset); }
        }
    }

    private static void UnifiedPresetUndo()
    {
        using (var f = new ArmatureFixture())
        {
            var preset = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            preset.scales["Neck"] = new ArmatureScalerPresetData.SerializableVector3(new Vector3(.8f, 1.2f, .9f));
            preset.positions["Neck"] = new ArmatureScalerPresetData.SerializableVector3(new Vector3(.1f, .2f, .3f));
            preset.positions["Head"] = new ArmatureScalerPresetData.SerializableVector3(new Vector3(.02f, .17f, .04f));
            preset.rotations["Neck"] = new ArmatureScalerPresetData.SerializableQuaternion(Quaternion.Euler(11, 22, 33));
            preset.maScales.Add(new MAScaleAdjusterPresetData.Entry("Neck", new Vector3(1.4f, .8f, 1.1f), true));
            Vector3 beforeHead = new Vector3(.05f, .2f, -.03f);
            f.head.localPosition = beforeHead;
            Undo.ClearAll();
            try
            {
                f.Apply(preset);
                Undo.FlushUndoRecordObjects();
                Require(f.neck.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>() != null && f.Option("Neck"),
                    "Apply did not add MA component and option");
                Undo.PerformUndo();
                Equal(Vector3.one, f.neck.localScale, "One Undo did not restore direct scale");
                Equal(Vector3.zero, f.neck.localPosition, "One Undo did not restore direct position");
                Equal(Quaternion.identity, f.neck.localRotation, "One Undo did not restore rotation");
                Equal(beforeHead, f.head.localPosition, "One Undo did not restore child position");
                Require(f.neck.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>() == null && !f.Option("Neck"),
                    "One Undo did not remove added component and restore option");
                Undo.PerformRedo();
                Equal(preset.scales["Neck"].ToVector3(), f.neck.localScale, "Redo direct scale");
                Equal(preset.positions["Neck"].ToVector3(), f.neck.localPosition, "Redo direct position");
                Equal(preset.rotations["Neck"].ToQuaternion(), f.neck.localRotation, "Redo rotation");
                Equal(preset.positions["Head"].ToVector3(), f.head.localPosition, "Redo child position");
                var ma = f.neck.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>();
                Require(ma != null && f.Option("Neck"), "Redo did not restore component and option");
                Equal(preset.maScales[0].Scale, ma.Scale, "Redo MA scale");
            }
            finally { Object.DestroyImmediate(preset); }
        }
    }

    private static void InvalidMAPresetEntries()
    {
        using (var f = new ArmatureFixture())
        {
            var entries = new List<MAScaleAdjusterPresetData.Entry>
            {
                null,
                new MAScaleAdjusterPresetData.Entry(null, Vector3.one * 2, true),
                new MAScaleAdjusterPresetData.Entry("None", Vector3.one * 2, true),
                new MAScaleAdjusterPresetData.Entry("UnknownPart", Vector3.one * 2, true),
                new MAScaleAdjusterPresetData.Entry("999", Vector3.one * 2, true),
                new MAScaleAdjusterPresetData.Entry("LeftFoot", Vector3.one * 2, true),
                new MAScaleAdjusterPresetData.Entry("Head", new Vector3(.8f, .9f, 1.1f), false)
            };
            int skipped = (int)typeof(ArmatureScalerEditor).GetMethod("ApplyMAPresetEntries", PrivateInstance)
                .Invoke(f.window, new object[] { entries });
            Require(skipped == 6, "Invalid or missing MA entries were not reported as skipped");
            Require(f.neck.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>() == null && !f.Option("None"),
                "Invalid entry added component or option");
            Equal(entries[6].Scale, f.head.GetComponent<nadena.dev.modular_avatar.core.ModularAvatarScaleAdjuster>().Scale,
                "Valid entry was not applied after skipped entries");
        }
    }

    private static void LegacyPresetCompatibility()
    {
        using (var f = new ArmatureFixture())
        {
            var oldDirect = ScriptableObject.CreateInstance<ArmatureScalerPresetData>();
            // Older presets serialized only the original dictionaries.
            JsonUtility.FromJsonOverwrite("{\"scales\":{\"keys\":[\"Neck\"],\"values\":[{\"x\":0.8,\"y\":0.9,\"z\":1.1}]}," +
                "\"rotations\":{\"keys\":[\"Neck\"],\"values\":[{\"x\":0,\"y\":0,\"z\":0,\"w\":1}]}," +
                "\"positions\":{\"keys\":[\"Neck\"],\"values\":[{\"x\":0.1,\"y\":0.2,\"z\":0.3}]}}", oldDirect);
            oldDirect.scales.OnAfterDeserialize();
            oldDirect.rotations.OnAfterDeserialize();
            oldDirect.positions.OnAfterDeserialize();
            Require(oldDirect.maScales == null || oldDirect.maScales.Count == 0, "Legacy direct preset invented MA data");
            string directPath = f.folder + "/LegacyDirect.asset";
            AssetDatabase.CreateAsset(oldDirect, directPath);
            var oldMA = ScriptableObject.CreateInstance<MAScaleAdjusterPresetData>();
            // Schema 1 omitted adjustChildPositions; the old behavior must remain false.
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":1,\"entries\":[{\"part\":\"Neck\",\"x\":1.2,\"y\":0.7,\"z\":1.4}]}", oldMA);
            string maPath = f.folder + "/LegacyMA.asset";
            AssetDatabase.CreateAsset(oldMA, maPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(directPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(maPath, ImportAssetOptions.ForceUpdate);
            Require(oldMA.SchemaVersion == 1 && !oldMA.entries[0].adjustChildPositions,
                "Legacy MA schema/default child behavior changed");
            var ma = f.AddNeckMA(new Vector3(.95f, 1.05f, 1.15f), true);
            Vector3 originalMA = ma.Scale;
            Call(f.window, "RefreshPresetList", directPath);
            string[] paths = Get<string[]>(f.window, "presetFiles");
            Require(paths.Contains(directPath) && paths.Contains(maPath), "Unified picker excluded a legacy preset type");
            Set(f.window, "selectedPresetIndex", Array.IndexOf(paths, directPath));
            Call(f.window, "LoadSelectedPreset", true, true, true, true);
            Equal(new Vector3(.8f, .9f, 1.1f), f.neck.localScale, "Legacy direct scale did not load");
            Equal(new Vector3(.1f, .2f, .3f), f.neck.localPosition, "Legacy direct position did not load");
            Equal(originalMA, ma.Scale, "Legacy direct load changed existing MA component");
            Require(f.Option("Neck"), "Legacy direct load cleared MA option");
            Vector3 scaleBeforeMA = f.neck.localScale;
            Vector3 positionBeforeMA = f.neck.localPosition;
            Vector3 headBeforeMA = new Vector3(.03f, .18f, .02f);
            f.head.localPosition = headBeforeMA;
            Call(f.window, "RefreshPresetList", maPath);
            paths = Get<string[]>(f.window, "presetFiles");
            Set(f.window, "selectedPresetIndex", Array.IndexOf(paths, maPath));
            Call(f.window, "LoadSelectedPreset", false, false, false, true);
            Equal(oldMA.entries[0].Scale, ma.Scale, "Legacy MA preset did not load through unified picker");
            Equal(scaleBeforeMA, f.neck.localScale, "Legacy MA preset replaced direct scale");
            Equal(positionBeforeMA, f.neck.localPosition, "Legacy MA preset replaced direct position");
            Equal(headBeforeMA, f.head.localPosition, "Schema 1 MA load adjusted child positions");
            Require(!f.Option("Neck"), "Schema 1 child option did not retain the old false default");
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject root;
        public readonly SkinnedMeshRenderer renderer;
        public readonly Mesh mesh;
        public readonly string folder;
        public Fixture(string meshName = "Regression Face")
        {
            folder = "Assets/Case_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            root = new GameObject("Avatar");
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bone = new GameObject("Head");
            bone.transform.SetParent(root.transform, false);
            mesh = CreateMesh(meshName);
            AssetDatabase.CreateAsset(mesh, folder + "/Source.asset");
            renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = new[] { bone.transform };
            renderer.rootBone = bone.transform;
            renderer.updateWhenOffscreen = true;
            renderer.SetBlendShapeWeight(0, 23f);
            renderer.SetBlendShapeWeight(1, 37f);
        }
        public void Dispose() { if (root != null) Object.DestroyImmediate(root); }
    }

    private static Mesh CreateMesh(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { new Vector3(-.1f, 0, 0), new Vector3(.1f, 0, 0), new Vector3(0, .2f, 0) };
        mesh.normals = Enumerable.Repeat(Vector3.back, 3).ToArray();
        mesh.tangents = Enumerable.Repeat(new Vector4(1, 0, 0, -1), 3).ToArray();
        mesh.colors = new[] { Color.red, Color.green, Color.blue };
        mesh.triangles = new[] { 0, 2, 1 };
        for (int channel = 0; channel < 8; channel++)
            mesh.SetUVs(channel, new List<Vector4> { new Vector4(channel, 1, 2, 3), new Vector4(1, channel, 2, 3), new Vector4(1, 2, channel, 3) });
        mesh.bindposes = new[] { Matrix4x4.identity };
        mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 3).ToArray();
        mesh.bounds = new Bounds(new Vector3(.01f, .07f, 0), new Vector3(2, 3, 4));
        AddFrame(mesh, "Smile", 50, new Vector3(.03f, 0, .01f));
        AddFrame(mesh, "Smile", 100, new Vector3(.1f, 0, .04f));
        AddFrame(mesh, "Blink", 100, new Vector3(0, -.02f, 0));
        return mesh;
    }

    private static void AddFrame(Mesh mesh, string name, float weight, Vector3 delta)
    {
        mesh.AddBlendShapeFrame(name, weight, Enumerable.Repeat(delta, 3).ToArray(),
            Enumerable.Repeat(delta * .3f, 3).ToArray(), Enumerable.Repeat(delta * .2f, 3).ToArray());
    }

    private static Vector3[] Bake(SkinnedMeshRenderer renderer, float weight, int index = 0)
    {
        renderer.SetBlendShapeWeight(index, weight);
        var baked = new Mesh();
        try { renderer.BakeMesh(baked); return baked.vertices; }
        finally { Object.DestroyImmediate(baked); }
    }

    private static void Scale(SkinnedMeshRenderer renderer, float factor)
    {
        Undo.IncrementCurrentGroup();
        Require(DiNeShapeKeyEditorCore.ModifyShapeKeyScale(renderer, 0, factor, out string error), error);
        Undo.FlushUndoRecordObjects();
    }

    private static void PersistentScale()
    {
        using (var f = new Fixture())
        {
            var baseline = new[] { 0f, 50f, 100f }.ToDictionary(w => w, w => Bake(f.renderer, w));
            Scale(f.renderer, 2f);
            Equal(37f, f.renderer.GetBlendShapeWeight(1), "Unselected key weight changed");
            string path = AssetDatabase.GetAssetPath(f.renderer.sharedMesh);
            Require(!string.IsNullOrEmpty(path), "Result was not saved as an asset");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            f.renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            foreach (float weight in new[] { 0f, 50f, 100f })
            {
                Vector3[] actual = Bake(f.renderer, weight);
                for (int vertex = 0; vertex < actual.Length; vertex++)
                    Equal(baseline[0][vertex] + (baseline[weight][vertex] - baseline[0][vertex]) * 2,
                        actual[vertex], "Saved deformation at " + weight + "/vertex " + vertex);
            }
            f.renderer.sharedMesh = f.mesh;
            Equal(baseline[100][0], Bake(f.renderer, 100)[0], "Original mesh was changed");
        }
    }

    private static void PreserveMeshData()
    {
        using (var f = new Fixture())
        {
            Scale(f.renderer, .5f);
            Mesh actual = f.renderer.sharedMesh;
            Require(actual.blendShapeCount == 2 && actual.GetBlendShapeFrameCount(0) == 2, "Keys/frames were lost");
            for (int shape = 0; shape < f.mesh.blendShapeCount; shape++)
            for (int frame = 0; frame < f.mesh.GetBlendShapeFrameCount(shape); frame++)
            {
                Equal(f.mesh.GetBlendShapeFrameWeight(shape, frame), actual.GetBlendShapeFrameWeight(shape, frame), "Frame weight changed");
                var before = new[] { new Vector3[3], new Vector3[3], new Vector3[3] };
                var after = new[] { new Vector3[3], new Vector3[3], new Vector3[3] };
                f.mesh.GetBlendShapeFrameVertices(shape, frame, before[0], before[1], before[2]);
                actual.GetBlendShapeFrameVertices(shape, frame, after[0], after[1], after[2]);
                for (int channel = 0; channel < 3; channel++)
                for (int vertex = 0; vertex < 3; vertex++)
                    Equal(before[channel][vertex] * (shape == 0 ? .5f : 1f), after[channel][vertex], "Shape delta changed unexpectedly");
            }
            for (int channel = 0; channel < 8; channel++)
            {
                var before = new List<Vector4>(); var after = new List<Vector4>();
                f.mesh.GetUVs(channel, before); actual.GetUVs(channel, after);
                Require(before.SequenceEqual(after), "UV channel " + channel + " changed");
            }
            Require(f.mesh.bounds == actual.bounds, "Custom bounds changed");
            Require(f.mesh.vertices.SequenceEqual(actual.vertices) && f.mesh.triangles.SequenceEqual(actual.triangles), "Base geometry changed");
            Require(f.mesh.colors.SequenceEqual(actual.colors) && f.mesh.bindposes.SequenceEqual(actual.bindposes), "Colors/bindposes changed");
        }
    }

    private static void JapaneseSeventyPercentScale()
    {
        using (var f = new Fixture("Japanese Shape Fixture"))
        {
            // Generated data only: reproduce a long shape list with an MMD divider and
            // a late Japanese key, without including any proprietary avatar asset.
            for (int i = 2; i < 687; i++) AddFrame(f.mesh, "補助_" + i, 100, Vector3.zero);
            AddFrame(f.mesh, "------ MMD ------", 100, Vector3.zero);
            AddFrame(f.mesh, "笑い", 100, new Vector3(.025f, .05f, .012f));
            for (int i = 689; i < 737; i++) AddFrame(f.mesh, "追加_" + i, 100, Vector3.zero);
            EditorUtility.SetDirty(f.mesh);
            AssetDatabase.SaveAssetIfDirty(f.mesh);
            int index = f.mesh.GetBlendShapeIndex("笑い");
            Require(index == 688, "Fixture key index changed");
            var baseline = new[] { 0f, 50f, 100f }.ToDictionary(w => w, w => Bake(f.renderer, w, index));
            Require((baseline[100][0] - baseline[0][0]).sqrMagnitude > .0001f, "Selected key has no positional deformation");
            f.renderer.SetBlendShapeWeight(index, 17);
            var window = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            string savedPath;
            try
            {
                Set(window, "targetAvatarRoot", f.root); Set(window, "_skeSmr", f.renderer);
                Call(window, "SkeResetTarget");
                Set(window, "_skeSubMode", 1); Set(window, "_skeModifySubMode", 0);
                Set(window, "_skeModifyIndex", index); Set(window, "_skeModifyScale", 70f);
                Call(window, "SkeApplyModifyPreview", index);
                Mesh preview = Get<Mesh>(window, "_skePreviewMesh");
                Require(preview != null && !AssetDatabase.Contains(preview), "70% preview mesh missing");
                var delta = new Vector3[3];
                preview.GetBlendShapeFrameVertices(index, 0, delta, null, null);
                Equal(new Vector3(.025f, .05f, .012f) * .7f, delta[0], "70% preview deformation");
                Equal(17, f.renderer.GetBlendShapeWeight(index), "Preview changed selected source weight");

                // Exercise persisted core scaling and the window's post-Apply cleanup.
                // The batch harness does not synthesize a GUILayout button click.
                Require(DiNeShapeKeyEditorCore.ModifyShapeKeyScale(f.renderer, index, .7f, out string error), error);
                Call(window, "SkeMeshApplied");
                Equal(100, Get<float>(window, "_skeModifyScale"), "Completed scale was not reset for the next edit");
                Require(preview == null, "Post-Apply temporary preview was not released");
                savedPath = AssetDatabase.GetAssetPath(f.renderer.sharedMesh);
                Equal(17, f.renderer.GetBlendShapeWeight(index), "Apply changed selected source weight");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Apply changed unrelated active Smile");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Apply changed unrelated active Blink");
            }
            finally { Object.DestroyImmediate(window); }

            AssetDatabase.ImportAsset(savedPath, ImportAssetOptions.ForceUpdate);
            f.renderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(savedPath);
            Require(f.renderer.sharedMesh.GetBlendShapeIndex("笑い") == index, "Saved Japanese key name/index changed");
            foreach (float weight in new[] { 0f, 50f, 100f })
            {
                Vector3[] actual = Bake(f.renderer, weight, index);
                for (int vertex = 0; vertex < actual.Length; vertex++)
                    Equal(baseline[0][vertex] + (baseline[weight][vertex] - baseline[0][vertex]) * .7f,
                        actual[vertex], "Closed-window saved 70% deformation at " + weight);
            }
            f.renderer.sharedMesh = f.mesh;
            Equal(baseline[100][0], Bake(f.renderer, 100, index)[0], "Japanese source mesh was modified");
        }
    }

    private static void ConsecutiveEdits()
    {
        using (var f = new Fixture())
        {
            Vector3 initial = Bake(f.renderer, 100)[0];
            Vector3 zero = Bake(f.renderer, 0)[0];
            Scale(f.renderer, 2);
            Mesh first = f.renderer.sharedMesh;
            string firstPath = AssetDatabase.GetAssetPath(first);
            Scale(f.renderer, .5f);
            Mesh second = f.renderer.sharedMesh;
            Require(AssetDatabase.GetAssetPath(second) != firstPath, "Second edit overwrote first saved asset");
            Equal(initial, Bake(f.renderer, 100)[0], "Consecutive scale result");
            Undo.PerformUndo();
            Require(f.renderer.sharedMesh == first, "Undo did not restore previous mesh");
            Equal(zero + (initial - zero) * 2, Bake(f.renderer, 100)[0], "Undo restored mesh with overwritten data");
            Undo.PerformRedo();
            Require(f.renderer.sharedMesh == second, "Redo did not restore result mesh");
            Equal(initial, Bake(f.renderer, 100)[0], "Redo deformation");
        }
    }

    private static void AssetNameCollision()
    {
        using (var a = new Fixture("Same Name"))
        using (var b = new Fixture("Same Name"))
        {
            Scale(a.renderer, 2);
            Mesh first = a.renderer.sharedMesh;
            Vector3 firstValue = Bake(a.renderer, 100)[0];
            Scale(b.renderer, .5f);
            Require(first != b.renderer.sharedMesh, "Equal names share the same saved asset");
            Equal(firstValue, Bake(a.renderer, 100)[0], "Second avatar altered first avatar");
        }
    }

    private static void PrefabOverride()
    {
        using (var f = new Fixture())
        {
            string prefabPath = f.folder + "/Avatar.prefab";
            PrefabUtility.SaveAsPrefabAsset(f.root, prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            Scale(renderer, 1.5f);
            string savedMeshPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            var modifications = PrefabUtility.GetPropertyModifications(instance);
            Require(modifications != null && modifications.Any(p => p.propertyPath == "m_Mesh" && p.objectReference == renderer.sharedMesh),
                "Mesh assignment is not recorded as a prefab override");
            Object.DestroyImmediate(f.root);
            string scenePath = f.folder + "/Saved.unity";
            Require(EditorSceneManager.SaveScene(instance.scene, scenePath), "Scene could not be saved");
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            renderer = Object.FindObjectOfType<SkinnedMeshRenderer>();
            Require(renderer != null && AssetDatabase.GetAssetPath(renderer.sharedMesh) == savedMeshPath, "Mesh override did not survive reopening");
            Equal(37f, renderer.GetBlendShapeWeight(1), "Other key weight did not survive reopening");
        }
    }

    private static Color32[] ReadPixels(RenderTexture target)
    {
        RenderTexture previous = RenderTexture.active;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            return pixels.GetPixels32();
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
    }

    private static void IsolatedPreview()
    {
        using (var f = new Fixture())
        {
            var green = new Material(Shader.Find("Unlit/Color")) { color = Color.green };
            var red = new Material(Shader.Find("Unlit/Color")) { color = Color.red };
            f.renderer.sharedMaterial = green;
            f.renderer.gameObject.layer = 8;
            Require(f.root.AddComponent<AviEditorPreviewProbe>() != null, "Fixture lifecycle probe did not attach");
            var excluded = GameObject.CreatePrimitive(PrimitiveType.Cube);
            excluded.name = "User scene must stay excluded";
            excluded.transform.position = new Vector3(0, .1f, .2f);
            excluded.transform.localScale = new Vector3(1, 1, .1f);
            excluded.GetComponent<Renderer>().sharedMaterial = red;
            var rootIds = f.root.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(id => id).ToArray();
            EditorSceneManager.SaveScene(f.root.scene, f.folder + "/Preview.unity");
            int behaviourCalls = AviEditorPreviewProbe.LifecycleCalls;
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            int previewSceneCount = EditorSceneManager.previewSceneCount;
            var target = new RenderTexture(256, 256, 24);
            target.Create();
            var activeBefore = RenderTexture.active;
            var preview = new DiNeAviHeadPreview();
            Mesh scaled = DiNeShapeKeyEditorCore.BuildScaledMesh(f.mesh, 0, 2);
            int previewCullCallbacks = 0;
            bool previewCameraIsIsolated = true;
            bool ndmfWouldHookCamera = false;
            Camera.CameraCallback inspectPreviewCamera = camera =>
            {
                if (camera.name != "__DiNeHeadPreviewCamera__") return;
                previewCullCallbacks++;
                Transform parent = camera.transform.parent;
                previewCameraIsIsolated &= parent != null &&
                    parent.gameObject.scene == camera.gameObject.scene &&
                    EditorSceneManager.IsPreviewScene(camera.gameObject.scene) &&
                    camera.scene == camera.gameObject.scene && camera.targetTexture == target;
                // Contract from the installed NDMF ProxyManager.ShouldHookCamera:
                // parentless and screen cameras may substitute cached avatar proxies.
                ndmfWouldHookCamera |=
                    (camera.name == "TempCamera" && camera.targetTexture?.name == "ThumbnailCapture") ||
                    parent == null || camera.targetTexture == null;
            };
            Camera.onPreCull += inspectPreviewCamera;
            try
            {
                Action<IReadOnlyDictionary<int, float>, Mesh> render = (weights, mesh) => preview.Render(f.root, f.renderer,
                    weights, mesh, target, new Vector3(0, .1f, -1), Quaternion.identity, 35, .01f, 3f);
                render(null, null);
                Color32[] initial = ReadPixels(target);
                Require(initial.Count(p => p.g > 100 && p.r < 20) > 50, "Preview contains no avatar pixels");
                Require(initial.All(p => p.r < 20), "Preview camera rendered unrelated red scene geometry");
                render(new Dictionary<int, float> { { 0, 100 }, { 1, 85 } }, scaled);
                Color32[] edited = ReadPixels(target);
                Require(initial.Where((p, i) => !p.Equals(edited[i])).Count() > 50, "Shape overrides/scaled mesh did not change preview pixels");
                foreach (GameObject alternateRoot in new[] { null, excluded })
                {
                    preview.Render(alternateRoot, f.renderer, null, null, target,
                        new Vector3(0, .1f, -1), Quaternion.identity, 35, .01f, 3f);
                    Require(initial.SequenceEqual(ReadPixels(target)), "Direct renderer selection lost avatar or rendered unrelated root");
                }
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Preview changed selected source weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Preview changed other source weight");
                Require(f.renderer.sharedMesh == f.mesh && f.renderer.sharedMaterial == green, "Preview changed source mesh/material");
                Require(f.renderer.gameObject.layer == 8 && f.renderer.gameObject.activeSelf && f.renderer.enabled, "Preview changed source visibility");
                Require(rootIds.SequenceEqual(f.root.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(id => id)), "Preview added user scene roots");
                Require(!f.root.scene.isDirty && UnityEngine.SceneManagement.SceneManager.sceneCount == sceneCount, "Preview dirtied or added normal scenes");
                Require(RenderTexture.active == activeBefore, "Preview did not restore active render target");
                Require(AviEditorPreviewProbe.LifecycleCalls == behaviourCalls, "Preview cloned or disabled avatar behaviours");
                Require(previewCullCallbacks > 0 && previewCameraIsIsolated, "Rendering camera lacks its private parent/scene/target contract");
                Require(!ndmfWouldHookCamera, "Rendering camera is eligible for NDMF cached proxy substitution");
            }
            finally
            {
                Camera.onPreCull -= inspectPreviewCamera;
                preview.Dispose();
                Object.DestroyImmediate(scaled);
                target.Release(); Object.DestroyImmediate(target);
                Object.DestroyImmediate(green); Object.DestroyImmediate(red); Object.DestroyImmediate(excluded);
            }
            Require(EditorSceneManager.previewSceneCount == previewSceneCount, "Preview scene leaked after disposal");
            Require(!Resources.FindObjectsOfTypeAll<GameObject>().Any(o => o.name.StartsWith("__DiNeHeadPreview")), "Preview object leaked after disposal");
        }
    }

    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(ArmatureScalerEditor window, string name, object value)
    {
        typeof(ArmatureScalerEditor).GetField(name, PrivateInstance).SetValue(window, value);
    }
    private static T Get<T>(ArmatureScalerEditor window, string name)
    {
        return (T)typeof(ArmatureScalerEditor).GetField(name, PrivateInstance).GetValue(window);
    }
    private static void Call(ArmatureScalerEditor window, string name, params object[] args)
    {
        typeof(ArmatureScalerEditor).GetMethod(name, PrivateInstance).Invoke(window, args);
    }

    private static void HeadFraming()
    {
        using (var f = new Fixture())
        {
            f.renderer.bones[0].localPosition = Vector3.up;
            var window = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            try
            {
                Set(window, "targetAvatarRoot", f.root);
                object[] before = { f.renderer, Vector3.zero, Vector3.zero, 0f };
                var framing = typeof(ArmatureScalerEditor).GetMethod("ComputeHeadFraming", PrivateInstance);
                framing.Invoke(window, before);
                f.renderer.localBounds = new Bounds(Vector3.up, Vector3.one * 100);
                object[] after = { f.renderer, Vector3.zero, Vector3.zero, 0f };
                framing.Invoke(window, after);
                Equal((Vector3)before[1], (Vector3)after[1], "Culling bounds shifted focus");
                Equal((float)before[3], (float)after[3], "Culling bounds changed zoom");

                var left = new GameObject("Eye_L");
                var right = new GameObject("Eye_R");
                left.transform.SetParent(f.renderer.bones[0], false);
                right.transform.SetParent(f.renderer.bones[0], false);
                left.transform.localPosition = new Vector3(-.03f, .04f, .03f);
                right.transform.localPosition = new Vector3(.03f, .04f, .03f);
                framing.Invoke(window, after);
                Equal((left.transform.position + right.transform.position) * .5f,
                    (Vector3)after[1], "Named eyes did not center focus");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Framing changed source weight");

                f.root.transform.localScale = Vector3.one * .7f;
                var actual = DiNeAvatarHeadFraming.GetBounds(f.renderer);
                var mesh = new Mesh();
                try
                {
                    f.renderer.BakeMesh(mesh, false);
                    var expected = new Bounds(f.renderer.transform.TransformPoint(mesh.vertices[0]), Vector3.zero);
                    foreach (var vertex in mesh.vertices) expected.Encapsulate(f.renderer.transform.TransformPoint(vertex));
                    Equal(expected.center, actual.center, "Scaled avatar geometry center");
                    Equal(expected.size, actual.size, "Scaled avatar geometry size");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            finally { Object.DestroyImmediate(window); }
        }
    }

    private static void WindowShapePreview()
    {
        foreach (float originalA in new[] { 0f, 23f })
        using (var f = new Fixture())
        {
            f.renderer.SetBlendShapeWeight(0, originalA);
            var window = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            try
            {
                Set(window, "targetAvatarRoot", f.root); Set(window, "_skeSmr", f.renderer);
                Call(window, "SkeResetTarget");
                Set(window, "_skeSubMode", 1); Set(window, "_skeModifySubMode", 0);
                // Index 0 is the initial target but has no active preview yet.
                Call(window, "SkeSelectModifyTarget", 0);
                var weights = Get<Dictionary<int, float>>(window, "_skePreviewWeights");
                Require(weights.Count == 1 && weights[0] == 100, "First click did not activate A exclusively");
                Set(window, "_skeModifyScale", 200f);
                Call(window, "SkeUpdateScalePreviewMesh");
                Mesh previewMesh = Get<Mesh>(window, "_skePreviewMesh");
                Require(previewMesh != null && previewMesh != f.mesh && !AssetDatabase.Contains(previewMesh), "Scale preview was not temporary");
                var delta = new Vector3[3]; previewMesh.GetBlendShapeFrameVertices(0, 1, delta, null, null);
                Equal(new Vector3(.2f, 0, .08f), delta[0], "Scale preview did not scale mesh data");
                Call(window, "SkeSelectModifyTarget", 1);
                Require(weights.Count == 1 && weights[1] == 100 && !weights.ContainsKey(0), "Selecting B retained an override on A");
                Equal(originalA, f.renderer.GetBlendShapeWeight(0), "Switching to B changed A's original weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Switching to B changed B's original weight");
                Require(previewMesh == null, "Previous scale preview mesh leaked");
                Equal(100, Get<float>(window, "_skeModifyScale"), "Switching target retained the previous scale");
                weights[1] = 42f;
                Set(window, "_skeModifyScale", 150f);
                Call(window, "SkeUpdateScalePreviewMesh");
                Equal(42, weights[1], "Changing scale reset the chosen preview weight");
                Mesh scaledB = Get<Mesh>(window, "_skePreviewMesh");
                Require(scaledB != null, "B scale preview missing");
                Call(window, "SkeSelectModifyTarget", 1);
                Require(weights.Count == 0 && Get<Mesh>(window, "_skePreviewMesh") == null && scaledB == null,
                    "Clicking selected B did not clear overrides and dispose its scale preview");
                Equal(100, Get<float>(window, "_skeModifyScale"), "Toggling off retained the preview scale");
                Equal(originalA, f.renderer.GetBlendShapeWeight(0), "Toggling off changed A's original weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Toggling off changed B's nonzero original weight");
                Call(window, "SkeSelectModifyTarget", 1);
                Require(weights.Count == 1 && weights[1] == 100, "Clicking B again did not reactivate it");
                Call(window, "SkeSelectModifyTarget", 0);
                Require(weights.Count == 1 && weights[0] == 100 && !weights.ContainsKey(1), "Switching back to A retained B");
                weights[0] = 0;
                Call(window, "SkeSelectModifyTarget", 0);
                Require(weights.Count == 0, "A preview set to zero did not toggle off");

                Set(window, "_skeSubMode", 0);
                var mix = Get<System.Collections.IList>(window, "_skeMixEntries");
                Type entryType = typeof(ArmatureScalerEditor).GetNestedType("DiNeSkeMixEntry", BindingFlags.NonPublic);
                foreach (float weight in new[] { 40f, 60f })
                {
                    object entry = Activator.CreateInstance(entryType);
                    entryType.GetField("index").SetValue(entry, 0);
                    entryType.GetField("weight").SetValue(entry, weight);
                    mix.Add(entry);
                }
                Call(window, "SkeApplyMixPreview", mix);
                Equal(100, weights[0], "Duplicate mix keys did not accumulate");
                Require(!weights.ContainsKey(1), "Unmentioned mix key overrides source baseline");
                Equal(originalA, f.renderer.GetBlendShapeWeight(0), "Window changed source selected weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Window changed source other weight");
                Require(f.renderer.sharedMesh == f.mesh, "Window replaced source mesh before Apply");
                Call(window, "SkeRestoreAndClearPreview");
                Require(weights.Count == 0 && Get<Mesh>(window, "_skePreviewMesh") == null, "Restore did not release preview state");
                Equal(100, Get<float>(window, "_skeModifyScale"), "Restore did not reset scale");
            }
            finally { Object.DestroyImmediate(window); }
            Equal(originalA, f.renderer.GetBlendShapeWeight(0), "Closing window changed source selected weight");
            Equal(37, f.renderer.GetBlendShapeWeight(1), "Closing window changed source other weight");
        }
    }


    private static void WindowReplacementPreview()
    {
        using (var f = new Fixture())
        {
            var window = ScriptableObject.CreateInstance<ArmatureScalerEditor>();
            var comparison = new GameObject("Detached deformation comparison");
            try
            {
                Set(window, "targetAvatarRoot", f.root); Set(window, "_skeSmr", f.renderer);
                Call(window, "SkeResetTarget");
                Set(window, "_skeSubMode", 1); Set(window, "_skeModifySubMode", 1); Set(window, "_skeModifyIndex", 0);
                var mix = Get<System.Collections.IList>(window, "_skeModifyMixEntries");
                Type entryType = typeof(ArmatureScalerEditor).GetNestedType("DiNeSkeMixEntry", BindingFlags.NonPublic);
                object entry = Activator.CreateInstance(entryType);
                entryType.GetField("index").SetValue(entry, 1);
                entryType.GetField("weight").SetValue(entry, 50f);
                mix.Add(entry);
                Call(window, "SkeApplyMixPreview", mix);
                Mesh previewMesh = Get<Mesh>(window, "_skePreviewMesh");
                var weights = Get<Dictionary<int, float>>(window, "_skePreviewWeights");
                Require(previewMesh != null && previewMesh != f.mesh, "Replacement did not create a temporary mesh");
                Require(weights.Count == 1 && weights[0] == 100, "Replacement preview overrides contributing source keys");
                Require(f.renderer.sharedMesh == f.mesh, "Replacement preview assigned source mesh");
                Equal(23, f.renderer.GetBlendShapeWeight(0), "Replacement preview changed target source weight");
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Replacement preview changed contributor source weight");

                var detached = comparison.AddComponent<SkinnedMeshRenderer>();
                detached.sharedMesh = previewMesh; detached.bones = f.renderer.bones; detached.rootBone = f.renderer.rootBone;
                detached.SetBlendShapeWeight(1, f.renderer.GetBlendShapeWeight(1));
                var expected = new[] { 0f, 50f, 100f }.ToDictionary(w => w, w => Bake(detached, w));
                Require(DiNeShapeKeyEditorCore.ReplaceShapeKeyWithMix(f.renderer, 0,
                    new List<(int, float)> { (1, 50f) }, out string error), error);
                Equal(37, f.renderer.GetBlendShapeWeight(1), "Apply changed contributor source weight");
                foreach (float weight in new[] { 0f, 50f, 100f })
                {
                    Vector3[] actual = Bake(f.renderer, weight);
                    for (int vertex = 0; vertex < actual.Length; vertex++)
                        Equal(expected[weight][vertex], actual[vertex], "Replacement preview/Apply differ at " + weight);
                }
            }
            finally { Object.DestroyImmediate(window); Object.DestroyImmediate(comparison); }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

public static class MultiDresserRegression
{
    private static readonly List<string> Results = new List<string>();
    private static int failures;

    public static void Run()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 5 })
        {
            int originalCount = count;
            Test("Preserve original FX and protect new dresser, original count " + count,
                () => Structure(originalCount));
        }
        Test("Regeneration and a second dresser do not accumulate padding", Regenerate);
        Test("Old generated clothing at index 1 is protected on regeneration", Legacy);
        Test("No padding for an unconfigured dresser", Empty);
        Test("Clothing, body shape and material survive disabling FX 1 and 2", Animate);
        Test("Clothing and Schoolbag switch independently during MMD, clothing first", () => AnimateSchoolbag(false));
        Test("Clothing and Schoolbag switch independently during MMD, Schoolbag first", () => AnimateSchoolbag(true));
        Results.Add("Failures: " + failures);
        File.WriteAllLines("MultiDresserRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Test(string name, Action action)
    {
        try { action(); Results.Add("PASS " + name); }
        catch (Exception e) { failures++; Results.Add("FAIL " + name + ": " + e); }
        Debug.Log(Results[Results.Count - 1]);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject root, first, second;
        public readonly DiNeMultiDresser dresser;
        public readonly AnimatorController controller;
        public readonly string folder;
        public Fixture(int count)
        {
            folder = "Assets/Case_" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/FX.controller");
            while (controller.layers.Length > 0) controller.RemoveLayer(0);
            for (int i = 0; i < count; i++) controller.AddLayer("Original " + i);
            root = new GameObject("Avatar");
            first = new GameObject("First"); first.transform.SetParent(root.transform);
            second = new GameObject("Second"); second.transform.SetParent(root.transform);
            second.SetActive(false);
            dresser = root.AddComponent<DiNeMultiDresser>();
            dresser.rootTransform = root.transform;
            dresser.animatorController = controller;
            var data = new DiNeMultiDresser.DresserLayer { layerName = "Clothing" };
            data.EnsureSize(2);
            data.targets[0] = first;
            data.targets[1] = second;
            dresser.layers.Add(data);
        }
        public void Generate(bool clean = true) { dresser.Generate(folder + "/Generated", clean); }
        public void Dispose() { Object.DestroyImmediate(root); }
    }

    private static void Structure(int originalCount)
    {
        using (var f = new Fixture(originalCount))
        {
            var original = f.controller.layers;
            for (int i = 0; i < original.Length; i++)
            {
                original[i].defaultWeight = .25f;
                original[i].blendingMode = AnimatorLayerBlendingMode.Additive;
            }
            f.controller.layers = original;
            f.Generate();
            var actual = f.controller.layers;
            Require(actual.Length == Math.Max(3, originalCount) + 1, "Unexpected layer count");
            for (int i = 0; i < original.Length; i++)
            {
                Require(actual[i].name == original[i].name && actual[i].stateMachine == original[i].stateMachine,
                    "Original layer moved or replaced: " + i);
                Require(actual[i].defaultWeight == .25f && actual[i].blendingMode == AnimatorLayerBlendingMode.Additive,
                    "Original layer settings changed: " + i);
            }
            for (int i = originalCount; i < 3; i++)
                Require(actual[i].stateMachine.states.Length == 0 && actual[i].stateMachine.behaviours.Length == 0,
                    "Reserved layer has animation or behavior");
            int clothing = Array.FindIndex(actual, l => l.name == "DiNe Clothing");
            Require(clothing >= 3 && actual[clothing].defaultWeight == 1f, "Clothing layer is not protected and enabled");
            // Unity returns layer copies. Earlier generated layers must retain weight
            // when a subsequent AddLayer refreshes the array.
            f.dresser.layers.Add(new DiNeMultiDresser.DresserLayer { layerName = "Accessories" });
            f.dresser.layers[1].EnsureSize(2);
            f.dresser.layers[1].targets[0] = f.first;
            f.dresser.layers[1].targets[1] = f.second;
            f.Generate();
            Require(f.controller.layers.Where(l => l.name == "DiNe Clothing" || l.name == "DiNe Accessories")
                .All(l => l.defaultWeight == 1f), "A generated layer lost its default weight");
        }
    }

    private static void Regenerate()
    {
        using (var f = new Fixture(1))
        {
            f.Generate();
            string[] expected = f.controller.layers.Select(l => l.name).ToArray();
            f.Generate();
            Require(expected.SequenceEqual(f.controller.layers.Select(l => l.name)), "Regeneration accumulated layers");
            f.Generate(false);
            Require(expected.SequenceEqual(f.controller.layers.Select(l => l.name)), "Incremental update accumulated layers");
            f.dresser.layers[0].layerName = "Accessories";
            f.Generate(false);
            Require(f.controller.layers.Length == 5 && f.controller.layers[4].name == "DiNe Accessories",
                "Second dresser did not reuse reserved slots");
            f.dresser.DeleteAllGeneratedData();
            Require(f.controller.layers.Length == 1 && f.controller.layers[0].name == "Original 0",
                "Cleanup failed to remove generated padding");
        }
    }

    private static void Legacy()
    {
        using (var f = new Fixture(1))
        {
            f.controller.AddLayer("DiNe Clothing");
            f.Generate();
            Require(Array.FindIndex(f.controller.layers, l => l.name == "DiNe Clothing") >= 3,
                "Legacy generated layer remained in an MMD slot");
        }
    }

    private static void Empty()
    {
        using (var f = new Fixture(1))
        {
            f.dresser.layers.Clear();
            f.Generate();
            Require(f.controller.layers.Length == 1, "Empty dresser added reserved layers");
        }
    }

    private static void AnimateSchoolbag(bool schoolbagFirst)
    {
        using (var f = new Fixture(1))
        {
            var schoolbag = new GameObject("Schoolbag"); schoolbag.transform.SetParent(f.root.transform);
            var strap = new GameObject("SchoolbagStrap"); strap.transform.SetParent(f.root.transform);
            // Match an accessory that starts enabled in the scene and has an empty
            // default (Off) slot, separate from the clothing selection.
            var accessory = new DiNeMultiDresser.DresserLayer { layerName = "Schoolbag" };
            accessory.EnsureSize(2);
            accessory.targets[1] = schoolbag;
            accessory.linkedObjects[1].objects.Add(strap);
            f.dresser.layers.Insert(schoolbagFirst ? 0 : 1, accessory);
            f.Generate();

            var animator = f.root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("MMD Schoolbag regression");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, f.controller);
                var output = AnimationPlayableOutput.Create(graph, "FX", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                Action<int, int, string> select = (clothing, bag, phase) =>
                {
                    playable.SetInteger("DiNe/MultiDresser/Clothing", clothing);
                    playable.SetInteger("DiNe/MultiDresser/Schoolbag", bag);
                    for (int i = 0; i < 5; i++) graph.Evaluate(1f / 60f);
                    Require(schoolbag.activeSelf == (bag == 1), phase + ": Schoolbag on/off changed/lost");
                    Require(strap.activeSelf == (bag == 1), phase + ": linked strap on/off changed/lost");
                    Require(f.first.activeSelf == (clothing == 0) && f.second.activeSelf == (clothing == 1),
                        phase + ": clothing selection changed/lost");
                };

                select(0, 0, "Before MMD");
                select(1, 0, "Before MMD");
                select(1, 1, "Before MMD");
                select(0, 1, "Before MMD");
                for (int layer = 1; layer <= 2; layer++) playable.SetLayerWeight(layer, 0f);
                select(0, 1, "Enter MMD");
                // Exercise the accessory first in one case so an unprotected
                // controller reports the bag failure independently of clothing.
                if (schoolbagFirst) select(0, 0, "During MMD");
                select(1, 1, "During MMD");
                select(1, 0, "During MMD");
                select(0, 0, "During MMD");
                select(0, 1, "During MMD");
                for (int layer = 1; layer <= 2; layer++) playable.SetLayerWeight(layer, 1f);
                select(0, 1, "Exit MMD");
                select(1, 0, "After MMD");
            }
            finally { graph.Destroy(); }
        }
    }

    private static void Animate()
    {
        using (var f = new Fixture(1))
        {
            var body = new GameObject("Body"); body.transform.SetParent(f.root.transform);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            mesh.AddBlendShapeFrame("Shrink", 100, new[] { Vector3.one, Vector3.one, Vector3.one }, new Vector3[3], new Vector3[3]);
            renderer.sharedMesh = mesh;
            var firstMaterial = new Material(Shader.Find("Standard"));
            var secondMaterial = new Material(firstMaterial);
            renderer.sharedMaterial = firstMaterial;
            f.dresser.shapeKeyTargets.Add(body);
            var data = f.dresser.layers[0];
            for (int i = 0; i < 2; i++)
            {
                var keys = new DiNeMultiDresser.ShapeKeyList();
                keys.shapeKeys.Add(new DiNeMultiDresser.ShapeKeyState { name = "Shrink", value = i * 100, everRecorded = true });
                data.perButtonShapeKeyStates[i].meshShapeKeys.Add(keys);
                data.perButtonMaterialSwaps[i].entries.Add(new DiNeMultiDresser.MaterialSwapEntry
                { renderer = renderer, materials = new List<Material> { i == 0 ? firstMaterial : secondMaterial } });
            }
            // Persist references used by the production animation assets.
            AssetDatabase.CreateAsset(mesh, f.folder + "/Body.asset");
            AssetDatabase.CreateAsset(firstMaterial, f.folder + "/First.mat");
            AssetDatabase.CreateAsset(secondMaterial, f.folder + "/Second.mat");
            f.Generate();
            var animator = f.root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create("MMD regression");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, f.controller);
                var output = AnimationPlayableOutput.Create(graph, "FX", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                Action<int> select = index =>
                {
                    playable.SetInteger("DiNe/MultiDresser/Clothing", index);
                    for (int i = 0; i < 5; i++) graph.Evaluate(1f / 60f);
                    Require(f.first.activeSelf == (index == 0) && f.second.activeSelf == (index == 1), "Clothing visibility changed/lost");
                    Require(Mathf.Abs(renderer.GetBlendShapeWeight(0) - index * 100) < .01f, "Body shape changed/lost");
                    Require(renderer.sharedMaterial == (index == 0 ? firstMaterial : secondMaterial), "Material changed/lost");
                };
                select(1);
                for (int layer = 1; layer <= 2 && layer < playable.GetLayerCount(); layer++)
                    playable.SetLayerWeight(layer, 0f);
                select(1);
                select(0);
                select(1);
                for (int layer = 1; layer <= 2 && layer < playable.GetLayerCount(); layer++)
                    playable.SetLayerWeight(layer, 1f);
                select(0);
            }
            finally { graph.Destroy(); }
        }
    }
}
#endif

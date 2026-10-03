#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Mapping = DiNeAnimationRepairCore.BindingMapping;
using Status = DiNeAnimationRepairCore.BindingStatus;

public static partial class AnimationToolRegression
{
    private static readonly List<string> Results = new List<string>();
    private static int failures;
    private static string reportPath = "AnimationToolRegression-results.txt";

    public static void Run() => RunSuite(true);

    public static void RunRealSdk() => RunSuite(false);

    public static void RunNarrowUiCapture()
    {
        RecoverUiPreferences();
        reportPath = "AnimationToolUiGeometry-results.txt";
        BeginUiCapture(error =>
        {
            if (error == null) Results.Add("PASS narrow-window geometry capture");
            else { failures++; Results.Add("FAIL narrow-window geometry capture: " + error); }
            CompleteRun();
        });
        CloseUiWindow(uiAnimation); uiAnimation = null;
        uiCase = 18;
        PrepareUiCase();
    }

    private static void RunSuite(bool captureUi)
    {
        RecoverUiPreferences();
        using (var preferences = new LanguagePreferences())
        {
            Test("Renamed path, blendshape and material curves retain keys/events/settings and source assets", RepairPreservesData);
            Test("Cross mappings swap complete float and object curves without overwriting either source", CrossMappings);
            Test("Many-to-one and untouched destination collisions block copy generation", CollisionProtection);
            Test("Root bindings and distinct same-name branches work; identical sibling paths remain ambiguous", RootAndDuplicatePaths);
            Test("Missing components, shapes, slots and unverified destinations remain explicit", InvalidDestinationDiagnostics);
            Test("Float/object curve-kind mismatches are blocked in manual and imported mappings", IncompatibleCurveKinds);
            Test("Controller traversal includes nested motions and effective overrides only", ControllerClipCollection);
            Test("Mapping profiles preserve empty paths, Unicode, curve kind and disabled rows", MappingProfileRoundtrip);
            Test("Saved repaired outputs remain independent after later repairs and asset reimport", IndependentOutputs);
            Test("Detached preview samples graphics without changing source transforms/assets/avatar references", DetachedPreview);
            Test("Animation Tool expression loading keeps unmentioned values and source references", WindowExpression);
            Test("Animation Tool Apply/Restore preserves avatar asset references", WindowApplyRestore);
            Test("Expression overwrite preserves other meshes, transform/material curves, events and settings", ExpressionOverwrite);
            Test("Nested FX blend-tree leaf replacement changes one slot and supports Undo/Redo", NestedFxReplacement);
            Test("Repair window saves only changed copies plus reports without assigning avatar references", WindowRepairSave);
            Test("Transient repaired preview clips are owned and disposed on replacement, refresh and close", TransientPreviewLifetime);
            Test("All three Animation Tool tabs process EN/KO/JP Layout/Repaint without errors", WindowLanguagesAndTabs);
            Test("Avi Editor retains only Armature, ShapeKey and Extra tabs in EN/KO/JP", AviLanguagesAndTabs);
        }
        if (!captureUi)
        {
            Results.Add("METRIC SDK descriptor assembly: " + typeof(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor).Assembly.GetName().Name);
            Results.Add("METRIC SDK menu assembly: " + typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu).Assembly.GetName().Name);
            Results.Add("METRIC SDK parameters assembly: " + typeof(VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters).Assembly.GetName().Name);
            Results.Add("METRIC UI captures: skipped in DLL-copy SDK environment; actual OnGUI Layout/Repaint checks ran synchronously");
            Results.Add("METRIC language/preset preferences restored and verified");
            CompleteRun();
            return;
        }
        BeginUiCapture(error =>
        {
            if (error == null) Results.Add("PASS 22 actual GUIView captures: Animation Tool and retained Avi Editor tabs in EN/KO/JP, including 420px EN/JP and KO lower controls");
            else { failures++; Results.Add("FAIL actual GUIView captures: " + error); }
            CompleteRun();
        });
    }

    private static void CompleteRun()
    {
        Results.Add("Failures: " + failures);
        File.WriteAllLines(reportPath, Results);
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

    internal static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void Equal(float expected, float actual, string message)
        => Require(Mathf.Abs(expected - actual) < .0001f, message + ": expected " + expected + ", actual " + actual);

    private static void Equal(Vector3 expected, Vector3 actual, string message)
        => Require((expected - actual).sqrMagnitude < .00000001f, message + ": expected " + expected + ", actual " + actual);

    private static EditorCurveBinding Float(string path, Type type, string property)
        => EditorCurveBinding.FloatCurve(path, type, property);

    private static EditorCurveBinding Material(string path, int slot = 0)
        => EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), "m_Materials.Array.data[" + slot + "]");

    private static AnimationCurve WeightedCurve(float value)
    {
        return new AnimationCurve(
            new Keyframe(.125f, value, -2.5f, 3.75f, .2f, .35f) { weightedMode = WeightedMode.Both },
            new Keyframe(.875f, value + 11, 7.5f, -1.25f, .45f, .3f) { weightedMode = WeightedMode.Both })
        { preWrapMode = WrapMode.PingPong, postWrapMode = WrapMode.Loop };
    }

    private static void EqualCurve(AnimationCurve expected, AnimationCurve actual, string message)
    {
        Require(expected != null && actual != null && expected.length == actual.length, message + " key count");
        Require(expected.preWrapMode == actual.preWrapMode && expected.postWrapMode == actual.postWrapMode, message + " wrapping");
        for (int i = 0; i < expected.length; i++)
        {
            Keyframe left = expected.keys[i], right = actual.keys[i];
            Equal(left.time, right.time, message + " time " + i);
            Equal(left.value, right.value, message + " value " + i);
            Equal(left.inTangent, right.inTangent, message + " in tangent " + i);
            Equal(left.outTangent, right.outTangent, message + " out tangent " + i);
            Equal(left.inWeight, right.inWeight, message + " in weight " + i);
            Equal(left.outWeight, right.outWeight, message + " out weight " + i);
            Require(left.weightedMode == right.weightedMode, message + " weighted mode " + i);
        }
    }

    private static void EqualObjects(ObjectReferenceKeyframe[] expected, ObjectReferenceKeyframe[] actual, string message)
    {
        Require(actual != null && expected.Length == actual.Length, message + " count");
        for (int i = 0; i < expected.Length; i++)
        {
            Equal(expected[i].time, actual[i].time, message + " time " + i);
            Require(expected[i].value == actual[i].value, message + " asset " + i);
        }
    }

    private static void EqualEvents(AnimationClip expected, AnimationClip actual)
    {
        AnimationEvent[] left = AnimationUtility.GetAnimationEvents(expected), right = AnimationUtility.GetAnimationEvents(actual);
        Require(left.Length == right.Length, "Animation event count changed");
        for (int i = 0; i < left.Length; i++)
        {
            Equal(left[i].time, right[i].time, "Event time");
            Equal(left[i].floatParameter, right[i].floatParameter, "Event float");
            Require(left[i].functionName == right[i].functionName && left[i].stringParameter == right[i].stringParameter &&
                left[i].intParameter == right[i].intParameter && left[i].objectReferenceParameter == right[i].objectReferenceParameter &&
                left[i].messageOptions == right[i].messageOptions, "Animation event payload changed");
        }
    }

    private static Mapping Map(EditorCurveBinding binding, bool objects, string path, string property = null)
        => DiNeAnimationRepairCore.MappingFor(binding, objects, path, property ?? binding.propertyName);

    private static void ExpectBlocked(AnimationClip clip, GameObject root, IEnumerable<Mapping> mappings)
    {
        string original = EditorJsonUtility.ToJson(clip);
        bool rejected = false;
        try { Object.DestroyImmediate(DiNeAnimationRepairCore.CreateRepairedClip(clip, root, mappings)); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Invalid or conflicting repair generated a copy");
        Require(original == EditorJsonUtility.ToJson(clip), "Rejected repair changed the source clip");
    }

    private static void RepairPreservesData()
    {
        using (var f = new AnimationFixture())
        {
            f.renderer.gameObject.name = "NewFace";
            var shape = Float("OldFace", typeof(SkinnedMeshRenderer), "blendShape.OldSmile");
            var material = Material("OldFace");
            var pose = Float("OldBone", typeof(Transform), "m_LocalPosition.x");
            var untouched = Float("Body", typeof(GameObject), "m_IsActive");
            var clip = new AnimationClip { name = "Original complete clip", frameRate = 48, wrapMode = WrapMode.PingPong };
            AnimationUtility.SetEditorCurve(clip, shape, WeightedCurve(25));
            AnimationUtility.SetEditorCurve(clip, pose, WeightedCurve(.5f));
            AnimationUtility.SetEditorCurve(clip, untouched, AnimationCurve.Constant(0, 1, 1));
            var objects = new[] { new ObjectReferenceKeyframe { time = .1f, value = f.green }, new ObjectReferenceKeyframe { time = .6f, value = f.red } };
            AnimationUtility.SetObjectReferenceCurve(clip, material, objects);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true; settings.keepOriginalPositionY = true;
            settings.keepOriginalOrientation = true; settings.orientationOffsetY = 37; settings.cycleOffset = .25f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = .25f, functionName = "RegressionSentinel",
                stringParameter = "한글・日本語", intParameter = 7, floatParameter = 1.25f, objectReferenceParameter = f.red,
                messageOptions = SendMessageOptions.DontRequireReceiver } });
            string path = f.folder + "/Original.anim";
            AssetDatabase.CreateAsset(clip, path); AssetDatabase.SaveAssets();
            string sourceJson = EditorJsonUtility.ToJson(clip);
            byte[] sourceBytes = File.ReadAllBytes(path);
            var mappings = new[] { Map(shape, false, "NewFace", "blendShape.Smile"), Map(material, true, "NewFace"), Map(pose, false, "Bone") };
            var plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip, clip }, mappings);
            Require(plan.CanSave && plan.ChangedBindingCount == 3 && plan.ChangedClipCount == 1, "Complete repair plan did not deduplicate the input clip");
            AnimationClip result = DiNeAnimationRepairCore.CreateRepairedClip(clip, f.root, mappings);
            try
            {
                Require(result != clip && !AssetDatabase.Contains(result), "Repair reused a source/persistent clip");
                EqualCurve(AnimationUtility.GetEditorCurve(clip, shape), AnimationUtility.GetEditorCurve(result, Float("NewFace", typeof(SkinnedMeshRenderer), "blendShape.Smile")), "Renamed shape");
                EqualCurve(AnimationUtility.GetEditorCurve(clip, pose), AnimationUtility.GetEditorCurve(result, Float("Bone", typeof(Transform), "m_LocalPosition.x")), "Renamed transform");
                EqualObjects(objects, AnimationUtility.GetObjectReferenceCurve(result, Material("NewFace")), "Material swap");
                EqualCurve(AnimationUtility.GetEditorCurve(clip, untouched), AnimationUtility.GetEditorCurve(result, untouched), "Untouched curve");
                Require(AnimationUtility.GetEditorCurve(result, shape) == null && AnimationUtility.GetObjectReferenceCurve(result, material) == null, "Old paths survived the rewrite");
                Require(JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(clip)) == JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(result)), "Clip settings changed");
                Equal(clip.frameRate, result.frameRate, "Clip frame rate");
                Require(clip.wrapMode == result.wrapMode && clip.legacy == result.legacy, "Playback flags changed");
                EqualEvents(clip, result);
                Require(sourceJson == EditorJsonUtility.ToJson(clip) && sourceBytes.SequenceEqual(File.ReadAllBytes(path)), "Repair changed original serialized data or disk asset");
            }
            finally { Object.DestroyImmediate(result); }
        }
    }

    private static void CrossMappings()
    {
        using (var f = new AnimationFixture())
        {
            var other = f.NewRenderer("Other");
            var a = Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var b = Float("Other", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var ma = Material("Body"); var mb = Material("Other");
            var clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(clip, a, WeightedCurve(15));
            AnimationUtility.SetEditorCurve(clip, b, WeightedCurve(70));
            var ka = new[] { new ObjectReferenceKeyframe { time = 0, value = f.green } };
            var kb = new[] { new ObjectReferenceKeyframe { time = .25f, value = f.red } };
            AnimationUtility.SetObjectReferenceCurve(clip, ma, ka); AnimationUtility.SetObjectReferenceCurve(clip, mb, kb);
            var mappings = new[] { Map(a, false, "Other"), Map(b, false, "Body"), Map(ma, true, "Other"), Map(mb, true, "Body") };
            string before = EditorJsonUtility.ToJson(clip);
            AnimationClip result = DiNeAnimationRepairCore.CreateRepairedClip(clip, f.root, mappings);
            try
            {
                EqualCurve(AnimationUtility.GetEditorCurve(clip, a), AnimationUtility.GetEditorCurve(result, b), "A to B");
                EqualCurve(AnimationUtility.GetEditorCurve(clip, b), AnimationUtility.GetEditorCurve(result, a), "B to A");
                EqualObjects(ka, AnimationUtility.GetObjectReferenceCurve(result, mb), "Material A to B");
                EqualObjects(kb, AnimationUtility.GetObjectReferenceCurve(result, ma), "Material B to A");
                Require(before == EditorJsonUtility.ToJson(clip), "Cross-map changed original clip");
            }
            finally { Object.DestroyImmediate(result); Object.DestroyImmediate(clip); }
        }
    }

    private static void CollisionProtection()
    {
        using (var f = new AnimationFixture())
        {
            f.NewRenderer("Other"); f.NewRenderer("Third");
            var a = Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var b = Float("Other", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var c = Float("Third", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var clip = new AnimationClip();
            try
            {
                AnimationUtility.SetEditorCurve(clip, a, WeightedCurve(10)); AnimationUtility.SetEditorCurve(clip, b, WeightedCurve(20));
                var intoUntouched = new[] { Map(a, false, "Other") };
                var plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, intoUntouched);
                Require(!plan.CanSave && plan.Collisions.Count == 1 && plan.Collisions[0].Sources.Count == 2, "Untouched destination overwrite was accepted");
                ExpectBlocked(clip, f.root, intoUntouched);
                AnimationUtility.SetEditorCurve(clip, c, WeightedCurve(30));
                var merge = new[] { Map(a, false, "Other"), Map(c, false, "Other") };
                plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, merge);
                Require(!plan.CanSave && plan.Collisions.Count == 1 && plan.Collisions[0].Sources.Count == 3, "Many-to-one collision was accepted");
                ExpectBlocked(clip, f.root, merge);
                var conflictingRows = new[] { Map(a, false, "Other"), Map(a, false, "Third") };
                plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, conflictingRows);
                Require(!plan.CanSave && plan.InvalidMappings.Any(p => p.DuplicateMapping), "Conflicting mappings for one source were accepted");
                ExpectBlocked(clip, f.root, conflictingRows);
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }

    private static void RootAndDuplicatePaths()
    {
        using (var f = new AnimationFixture())
        {
            var rootBinding = Float("", typeof(Transform), "m_LocalPosition.x");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, rootBinding) == Status.Valid, "Empty root path was rejected");
            f.NewRenderer("Left/Face"); f.NewRenderer("Right/Face");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Left/Face", typeof(SkinnedMeshRenderer), "blendShape.Smile")) == Status.Valid,
                "Distinct same-name child branches were treated as ambiguous");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Right/Face", typeof(SkinnedMeshRenderer), "blendShape.Smile")) == Status.Valid,
                "Second distinct same-name branch was rejected");
            f.NewRenderer("Duplicate"); f.NewRenderer("Duplicate", true);
            var duplicate = Float("Duplicate", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, duplicate) == Status.AmbiguousPath, "Identical sibling path was silently matched");
            var clip = new AnimationClip();
            try
            {
                var missing = Float("Gone", typeof(Transform), "m_LocalPosition.x");
                AnimationUtility.SetEditorCurve(clip, missing, WeightedCurve(3));
                var copy = DiNeAnimationRepairCore.CreateRepairedClip(clip, f.root, new[] { Map(missing, false, "") });
                try { EqualCurve(AnimationUtility.GetEditorCurve(clip, missing), AnimationUtility.GetEditorCurve(copy, rootBinding), "Repair to root"); }
                finally { Object.DestroyImmediate(copy); }
                AnimationUtility.SetEditorCurve(clip, duplicate, WeightedCurve(45));
                var group = DiNeAnimationRepairCore.Analyze(f.root, new[] { clip, clip }).Single(g => g.Binding.path == "Duplicate");
                Require(group.Status == Status.AmbiguousPath && group.Clips.Count == 1, "Ambiguous analysis group lost status or duplicated clips");
                ExpectBlocked(clip, f.root, new[] { Map(missing, false, "Duplicate") });
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }

    private static void InvalidDestinationDiagnostics()
    {
        using (var f = new AnimationFixture())
        {
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Missing", typeof(Transform), "m_LocalPosition.x")) == Status.MissingPath, "Missing path status");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Bone", typeof(SkinnedMeshRenderer), "blendShape.Smile")) == Status.MissingComponent, "Missing component status");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Gone")) == Status.MissingBlendShape, "Missing shape status");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Material("Body", 3)) == Status.MissingMaterialSlot, "Missing material slot status");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("Bone", typeof(Transform), "unknown.nonSerializedProperty")) == Status.UnverifiedProperty, "Unknown property claimed verified");
            Require(DiNeAnimationRepairCore.ValidateBinding(f.root, Float("", typeof(Animator), "Nonexistent muscle property")) == Status.UnverifiedHumanoid, "Unknown humanoid property claimed verified");
            var source = Float("Old", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var clip = new AnimationClip();
            try
            {
                AnimationUtility.SetEditorCurve(clip, source, WeightedCurve(10));
                var mapping = Map(source, false, "Body", "blendShape.Gone");
                var plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, new[] { mapping });
                Require(!plan.CanSave && plan.InvalidMappings.Count == 1 && plan.Remaining.Any(g => g.Status == Status.MissingBlendShape), "Invalid mapped shape was accepted");
                ExpectBlocked(clip, f.root, new[] { mapping });
                mapping.Enabled = false;
                plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, new[] { mapping });
                Require(!plan.CanSave && plan.ChangedBindingCount == 0 && plan.Remaining.Single().Status == Status.MissingPath, "Disabled row unexpectedly changed a curve");
            }
            finally { Object.DestroyImmediate(clip); }
        }
    }

    private static void ControllerClipCollection()
    {
        using (var f = new AnimationFixture())
        {
            var a = new AnimationClip { name = "A" }; var b = new AnimationClip { name = "B" }; var replacement = new AnimationClip { name = "Override" };
            foreach (var clip in new[] { a, b, replacement }) AssetDatabase.CreateAsset(clip, f.folder + "/" + clip.name + ".anim");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(f.folder + "/Nested.controller");
            controller.AddParameter("Blend", AnimatorControllerParameterType.Float);
            var child = controller.layers[0].stateMachine.AddStateMachine("Nested machine");
            var tree = new BlendTree { name = "Nested tree", blendParameter = "Blend" };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(a, 0); tree.AddChild(b, 1); child.AddState("Blend state").motion = tree;
            controller.layers[0].stateMachine.AddState("Duplicate A").motion = a;
            AssetDatabase.SaveAssets();
            var clips = DiNeAnimationRepairCore.CollectClips(controller);
            Require(clips.Count == 2 && clips.Contains(a) && clips.Contains(b), "Nested controller clips missing or duplicated");
            var overrides = new AnimatorOverrideController(controller);
            try
            {
                overrides.ApplyOverrides(new List<KeyValuePair<AnimationClip, AnimationClip>> { new KeyValuePair<AnimationClip, AnimationClip>(a, replacement) });
                clips = DiNeAnimationRepairCore.CollectClips(overrides);
                Require(clips.Count == 2 && clips.Contains(replacement) && clips.Contains(b) && !clips.Contains(a), "Override collector reintroduced replaced originals");
                Require(DiNeAnimationRepairCore.CollectClips(null).Count == 0, "Null controller did not yield empty clips");
            }
            finally { Object.DestroyImmediate(overrides); }
        }
    }

    private static void IncompatibleCurveKinds()
    {
        using (var f = new AnimationFixture())
        {
            var shape = Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var material = Material("Body");
            foreach (bool objectCurve in new[] { false, true })
            {
                var source = objectCurve ? material : shape;
                var destination = objectCurve ? shape : material;
                var clip = new AnimationClip();
                try
                {
                    if (objectCurve) AnimationUtility.SetObjectReferenceCurve(clip, source, new[] { new ObjectReferenceKeyframe { time = 0, value = f.green } });
                    else AnimationUtility.SetEditorCurve(clip, source, WeightedCurve(20));
                    var manual = new[] { Map(source, objectCurve, destination.path, destination.propertyName) };
                    foreach (var mappings in new IEnumerable<Mapping>[] { manual, DiNeAnimationRepairCore.FromProfileJson(DiNeAnimationRepairCore.ToProfileJson(manual)) })
                    {
                        var plan = DiNeAnimationRepairCore.Plan(f.root, new[] { clip }, mappings);
                        Require(!plan.CanSave && plan.InvalidMappings.Count > 0, "Curve-kind mismatch was accepted");
                        Require(plan.InvalidMappings[0].Status == Status.IncompatibleCurveType, "Curve-kind mismatch lacks explicit incompatible status");
                        ExpectBlocked(clip, f.root, mappings);
                    }
                }
                finally { Object.DestroyImmediate(clip); }
            }
        }
    }

    private static void MappingProfileRoundtrip()
    {
        var root = Float("", typeof(Transform), "m_LocalPosition.x");
        var material = Material("몸/表情");
        var rows = new[] { Map(root, false, "새 루트/Body"), Map(material, true, "", "m_Materials.Array.data[2]") };
        rows[1].Enabled = false;
        var result = DiNeAnimationRepairCore.FromProfileJson(DiNeAnimationRepairCore.ToProfileJson(rows));
        Require(result.Count == 2 && result[0].SourcePath == "" && result[0].TargetPath == "새 루트/Body", "Profile lost root or Unicode path");
        Require(result[1].SourcePath == "몸/表情" && result[1].IsObjectReference && !result[1].Enabled && result[1].TargetPath == "", "Profile lost object-reference kind or disabled/root state");
        Require(result[1].TypeName == material.type.AssemblyQualifiedName && result[1].TargetProperty == "m_Materials.Array.data[2]", "Profile lost type or target property");
        bool blocked = false;
        try { DiNeAnimationRepairCore.FromProfileJson("{\"Version\":99,\"Mappings\":[]}"); }
        catch (ArgumentException) { blocked = true; }
        Require(blocked, "Unsupported profile version was accepted");
    }

    private static void IndependentOutputs()
    {
        using (var f = new AnimationFixture())
        {
            f.NewRenderer("Other");
            var old = Float("Old", typeof(SkinnedMeshRenderer), "blendShape.Smile");
            var clip = new AnimationClip { name = "Immutable original" };
            AnimationUtility.SetEditorCurve(clip, old, WeightedCurve(15));
            AssetDatabase.CreateAsset(clip, f.folder + "/Immutable.anim");
            var first = DiNeAnimationRepairCore.CreateRepairedClip(clip, f.root, new[] { Map(old, false, "Body") });
            AssetDatabase.CreateAsset(first, f.folder + "/First.anim"); AssetDatabase.SaveAssets();
            string original = EditorJsonUtility.ToJson(clip), saved = EditorJsonUtility.ToJson(first);
            byte[] disk = File.ReadAllBytes(f.folder + "/First.anim");
            var second = DiNeAnimationRepairCore.CreateRepairedClip(clip, f.root, new[] { Map(old, false, "Other") });
            try
            {
                AnimationUtility.SetEditorCurve(second, Float("Other", typeof(SkinnedMeshRenderer), "blendShape.Smile"), AnimationCurve.Constant(0, 1, 99));
                Require(original == EditorJsonUtility.ToJson(clip) && saved == EditorJsonUtility.ToJson(first), "Later repair shares mutable curves with an earlier asset");
                AssetDatabase.SaveAssets();
                Require(disk.SequenceEqual(File.ReadAllBytes(f.folder + "/First.anim")), "Later repair rewrote first output file");
                AssetDatabase.ImportAsset(f.folder + "/First.anim", ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<AnimationClip>(f.folder + "/First.anim");
                EqualCurve(AnimationUtility.GetEditorCurve(clip, old), AnimationUtility.GetEditorCurve(reloaded, Float("Body", typeof(SkinnedMeshRenderer), "blendShape.Smile")), "Saved output reimport");
            }
            finally { Object.DestroyImmediate(second); }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DiNeTool.ExtraModifier.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public static class ExtraModifierTransplantRegression
{
    private static readonly List<string> Results = new List<string>();
    private static int failures;
    private static int tests;
    private static string caseFolder;

    public static void Run()
    {
        Test("Analyze is read-only and describes the same work as Execute", AnalysisReadOnly);
        Test("Target static/skinned geometry, materials, bones and target-only references survive", PreserveTargetGeometry);
        Test("Independent collision meshes are retained and rendered collision meshes use revised geometry", CollisionMeshes);
        Test("Animator keeps the revised Avatar and warns when the revised rig has no Avatar", AnimatorAvatars);
        Test("Multiple typed components, nested arrays and external references use destination objects", ComponentReferences);
        Test("Native ParentConstraint sources, offsets and collider values survive serialization", NativeComponents);
        Test("New inactive nonmesh helpers inherit source local transforms under unchanged target bones", RestoreObjectsAndTransforms);
        Test("Matched RectTransforms retain their complete target layout and object settings", KeepMatchedTransforms);
        Test("Direct target retains placement and root identity across different scene parents", TargetRootIdentity);
        Test("Removed mesh hosts are never recreated and dangling references are cleared", RemovedMeshReferences);
        Test("Missing mesh ancestors do not recreate hidden nonmesh descendant chains", RemovedMeshAncestor);
        Test("Equal sibling names map by exact ordinal path when counts agree", DuplicateSiblingPaths);
        Test("Duplicate sibling count mismatch cannot reuse an arbitrary target", DuplicateCountMismatch);
        Test("Unique moved bone names recover a target bone without duplicate creation", UniqueBoneFallback);
        Test("Unique moved renderer hosts receive nonmesh components without replacing geometry", UniqueRendererHostFallback);
        Test("Rotated nonuniform target wrapper and moved bones stay unchanged while new helpers copy local TRS", ChangedParentShear);
        Test("Synchronous copied-component callbacks cannot change existing target objects", CallbackPreservation);
        Test("New helper lifecycle callbacks cannot replace its original prefab local transforms", CreatedHelperCallbacks);
        Test("Imported-like helper quaternion setter roundoff preserves orientation and rejects real rotation changes", HelperQuaternionRoundoff);
        Test("A helper callback quaternion sign flip remains the same copied local rotation", HelperQuaternionSign);
        Test("Auto-added collider requirements reuse the copied dependency", RequiredCollider);
        Test("Recursive RequireComponent chains cannot resurrect target mesh components", RequiredMeshExclusion);
        Test("Cyclic SerializeReference graphs terminate and remap every internal reference", CyclicManagedReferences);
        Test("Prefab asset target is rejected and linked scene target keeps persistent overrides", PrefabAssetInputs);
        Test("Imported model asset target is rejected and linked scene instance receives components", ModelAssetTarget);
        Test("A direct transplant is one Undo/Redo transaction restoring the same target", UndoRedo);
        Test("A real incompatible component-add failure rolls back the target without destroying it", Rollback);
        Test("Invalid input analysis/execution creates no scene object", InvalidInputs);
        if (typeof(DiNePrefabTransplantUtility).Assembly.GetType("DiNeTool.ExtraModifier.Editor.DiNeExtraModifierWindow") != null)
        {
            Test("Actual window text and every emitted diagnostic support all three shared languages", WindowLocalization);
            Test("Actual window queues one captured transfer and cancels safely outside IMGUI", WindowExecutionQueue);
        }
        var sdkSuite = typeof(DiNePrefabTransplantUtility).Assembly.GetType("ExtraModifierTransplantSdkRegression");
        if (sdkSuite != null)
            sdkSuite.GetMethod("RegisterTests", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { new Action<string, Action>(Test) });
        Results.Add("Tests: " + tests);
        Results.Add("Failures: " + failures);
        File.WriteAllLines("ExtraModifierTransplantRegression-results.txt", Results);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void Test(string name, Action action)
    {
        tests++;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Undo.ClearAll();
        caseFolder = "Assets/Case_" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(caseFolder);
        AssetDatabase.Refresh();
        try { action(); Results.Add("PASS " + name); }
        catch (Exception e) { failures++; Results.Add("FAIL " + name + ": " + e); }
        finally { DiNeTransplantProbe.ForbiddenSourceRoot = null; DiNeTransplantMutationProbe.Armed = false; DiNeTransplantMutationProbe.ActiveOnly = false; DiNeTransplantMutationProbe.TargetRoot = null; DiNeTransplantHelperMutationProbe.Armed = false; DiNeTransplantQuaternionProbe.Armed = false; }
        Debug.Log(Results[Results.Count - 1]);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(Vector3 expected, Vector3 actual, string message)
    {
        Require((expected - actual).sqrMagnitude < 0.00000001f, message + ": expected " + expected + ", actual " + actual);
    }

    private static void Equal(Quaternion expected, Quaternion actual, string message)
    {
        Require(Quaternion.Angle(expected, actual) < .001f, message + ": expected " + expected + ", actual " + actual);
    }

    private static void Equal(Matrix4x4 expected, Matrix4x4 actual, string message)
    {
        for (int i = 0; i < 16; i++)
            Require(Mathf.Abs(expected[i] - actual[i]) < .0001f, message + " matrix element " + i + ": expected " + expected[i] + ", actual " + actual[i]);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var result = new GameObject(name);
        result.transform.SetParent(parent.transform, false);
        return result;
    }

    private static GameObject Find(GameObject root, string path)
    {
        var transform = root.transform.Find(path);
        Require(transform != null, "Missing destination path: " + path);
        return transform.gameObject;
    }

    public sealed class ExistingTargetState
    {
        public Transform Transform;
        public int InstanceId, SceneHandle, Sibling, Layer;
        public string Name, Tag;
        public Transform Parent;
        public bool Active;
        public StaticEditorFlags StaticFlags;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public Matrix4x4 World;
        public bool IsRect;
        public Vector2 AnchorMin, AnchorMax, Pivot, SizeDelta;
        public Vector3 AnchoredPosition;
        public string SerializedTransform;
    }

    public static List<ExistingTargetState> CaptureExistingTarget(GameObject root)
    {
        var states = new List<ExistingTargetState>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var host = transform.gameObject;
            var state = new ExistingTargetState { Transform = transform, InstanceId = transform.GetInstanceID(), SceneHandle = host.scene.handle,
                Sibling = transform.GetSiblingIndex(), Layer = host.layer, Name = host.name, Tag = host.tag, Parent = transform.parent, Active = host.activeSelf,
                StaticFlags = GameObjectUtility.GetStaticEditorFlags(host), Position = transform.localPosition, Scale = transform.localScale,
                Rotation = transform.localRotation, World = transform.localToWorldMatrix };
            var rect = transform as RectTransform;
            if (rect != null)
            {
                state.IsRect = true; state.AnchorMin = rect.anchorMin; state.AnchorMax = rect.anchorMax; state.Pivot = rect.pivot;
                state.SizeDelta = rect.sizeDelta; state.AnchoredPosition = rect.anchoredPosition3D;
            }
            state.SerializedTransform = TransformDataWithoutChildren(transform);
            states.Add(state);
        }
        return states;
    }

    public static void RequireExistingTargetUnchanged(List<ExistingTargetState> states)
    {
        foreach (var state in states)
        {
            var transform = state.Transform;
            Require(transform != null && transform.GetInstanceID() == state.InstanceId, "Existing target transform was destroyed or replaced: " + state.Name);
            var host = transform.gameObject;
            Require(host.name == state.Name && host.scene.handle == state.SceneHandle && transform.parent == state.Parent && transform.GetSiblingIndex() == state.Sibling && host.activeSelf == state.Active && host.layer == state.Layer && host.tag == state.Tag && GameObjectUtility.GetStaticEditorFlags(host) == state.StaticFlags,
                "Existing target object identity/settings changed: " + state.Name);
            Require(transform.localPosition.Equals(state.Position) && transform.localRotation.Equals(state.Rotation) && transform.localScale.Equals(state.Scale) && transform.localToWorldMatrix.Equals(state.World),
                "Existing target local/world transform changed: " + state.Name);
            Require(TransformDataWithoutChildren(transform) == state.SerializedTransform, "Existing target serialized Transform data changed: " + state.Name + "; Before=" + state.SerializedTransform + "; After=" + TransformDataWithoutChildren(transform));
            if (state.IsRect)
            {
                var rect = transform as RectTransform;
                Require(rect != null && rect.anchorMin.Equals(state.AnchorMin) && rect.anchorMax.Equals(state.AnchorMax) && rect.pivot.Equals(state.Pivot) && rect.sizeDelta.Equals(state.SizeDelta) && rect.anchoredPosition3D.Equals(state.AnchoredPosition),
                    "Existing target RectTransform layout changed: " + state.Name);
            }
        }
    }

    private static string TransformDataWithoutChildren(Transform transform)
    {
        // Appending newly restored helper children is allowed; every other
        // serialized transform field (including inspector-only fields) is kept.
        return Regex.Replace(EditorJsonUtility.ToJson(transform), "\"m_Children\"\\s*:\\s*\\[[^\\]]*\\]\\s*,?", string.Empty);
    }

    private static DiNePrefabTransplantUtility.Report Execute(GameObject source, GameObject target)
    {
        var sourceBefore = Snapshot(source);
        var targetStates = CaptureExistingTarget(target);
        string name = target.name; var parent = target.transform.parent; int sibling = target.transform.GetSiblingIndex();
        bool active = target.activeSelf; int roots = target.scene.rootCount; int instanceId = target.GetInstanceID();
        var report = DiNePrefabTransplantUtility.Execute(source, target);
        Require(report.Succeeded && report.ResultRoot != null, report.Error ?? "Missing result root");
        Require(report.ResultRoot == target && target.GetInstanceID() == instanceId && report.ResultRoot != source, "Execute did not mutate the exact passed target");
        Require(target.name == name && target.transform.parent == parent && target.transform.GetSiblingIndex() == sibling && target.activeSelf == active,
            "Target root name, parent, sibling or active state changed");
        Require(target.scene.rootCount == roots, "Execute created an unrequested scene root");
        RequireExistingTargetUnchanged(targetStates);
        RequireSourceUnchanged(source, sourceBefore);
        return report;
    }

    private static void LocalTransform(Transform expected, Transform actual, string message)
    {
        Equal(expected.localPosition, actual.localPosition, message + " position");
        Equal(expected.localRotation, actual.localRotation, message + " rotation");
        Equal(expected.localScale, actual.localScale, message + " scale");
    }

    private static void SetTransform(Transform transform, float seed)
    {
        transform.localPosition = new Vector3(seed, seed + 1, -seed);
        transform.localRotation = Quaternion.Euler(seed * 7, seed * 11, seed * 3);
        transform.localScale = new Vector3(1 + seed * .1f, 1 + seed * .2f, 1 + seed * .3f);
    }

    private static Mesh MeshAsset(string name, float size)
    {
        var mesh = new Mesh { name = name };
        mesh.vertices = new[] { Vector3.zero, Vector3.right * size, Vector3.up * size };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.bindposes = new[] { Matrix4x4.identity };
        mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 3).ToArray();
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, caseFolder + "/" + name + ".asset");
        return mesh;
    }

    private static Material MaterialAsset(string name, Color color)
    {
        var shader = Shader.Find("Hidden/InternalErrorShader");
        Require(shader != null, "Built-in test shader unavailable");
        var material = new Material(shader) { name = name, color = color };
        AssetDatabase.CreateAsset(material, caseFolder + "/" + name + ".mat");
        return material;
    }

    private static string Snapshot(GameObject root)
    {
        var rows = new List<string>();
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            rows.Add(transform.gameObject.GetInstanceID() + ":" + EditorJsonUtility.ToJson(transform.gameObject));
            foreach (var component in transform.GetComponents<Component>())
                rows.Add(component == null ? "Missing Script" : component.GetInstanceID() + ":" + EditorJsonUtility.ToJson(component));
        }
        return string.Join("\n", rows);
    }

    private static void RequireInputsUnchanged(GameObject source, GameObject target, string sourceBefore, string targetBefore)
    {
        Require(Snapshot(source) == sourceBefore, "Source input changed");
        Require(Snapshot(target) == targetBefore, "Target input changed");
    }

    private static void RequireSourceUnchanged(GameObject source, string sourceBefore)
    {
        Require(source != null && Snapshot(source) == sourceBefore, "Source input changed");
    }

    private static void AnalysisReadOnly()
    {
        var source = new GameObject("Source");
        var target = new GameObject("Target");
        Child(source, "Bone").AddComponent<BoxCollider>().center = Vector3.one;
        Child(target, "Bone");
        Child(source, "Extra").AddComponent<DiNeTransplantProbe>().marker = 81;
        EditorSceneManager.SaveScene(source.scene, caseFolder + "/Inputs.unity");
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        var roots = source.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(i => i).ToArray();
        int group = Undo.GetCurrentGroup();
        var plan = DiNePrefabTransplantUtility.Analyze(source, target);
        Require(plan.Succeeded && plan.ResultRoot == null, "Analyze did not return a read-only plan");
        Require(plan.CreatedObjects == 1 && plan.CopiedComponents == 2, "Analyze component/object totals are incorrect");
        Require(!source.scene.isDirty && Undo.GetCurrentGroup() == group, "Analyze dirtied the scene or Undo group");
        Require(roots.SequenceEqual(source.scene.GetRootGameObjects().Select(o => o.GetInstanceID()).OrderBy(i => i)), "Analyze changed scene roots");
        RequireInputsUnchanged(source, target, sourceBefore, targetBefore);
        var result = Execute(source, target);
        Require(plan.MatchedObjects == result.MatchedObjects && plan.CreatedObjects == result.CreatedObjects && plan.CopiedComponents == result.CopiedComponents, "Analyze differs from completed work");
        RequireSourceUnchanged(source, sourceBefore);
    }

    private static void PreserveTargetGeometry()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var sourceBone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        var oldMesh = MeshAsset("OldMesh", 1); var newMesh = MeshAsset("NewMesh", 2);
        var oldMaterial = MaterialAsset("OldMaterial", Color.red); var newMaterial = MaterialAsset("NewMaterial", Color.green);
        var sourceStatic = Child(source, "Static"); var targetStatic = Child(target, "Static");
        sourceStatic.AddComponent<MeshFilter>().sharedMesh = oldMesh;
        sourceStatic.AddComponent<MeshRenderer>().sharedMaterial = oldMaterial;
        var filter = targetStatic.AddComponent<MeshFilter>(); filter.sharedMesh = newMesh;
        var renderer = targetStatic.AddComponent<MeshRenderer>(); renderer.sharedMaterial = newMaterial; renderer.enabled = false;
        var sourceSkin = Child(source, "Skin").AddComponent<SkinnedMeshRenderer>();
        sourceSkin.sharedMesh = oldMesh; sourceSkin.sharedMaterial = oldMaterial; sourceSkin.bones = new[] { sourceBone.transform }; sourceSkin.rootBone = sourceBone.transform;
        var targetSkin = Child(target, "Skin").AddComponent<SkinnedMeshRenderer>();
        targetSkin.sharedMesh = newMesh; targetSkin.sharedMaterial = newMaterial; targetSkin.bones = new[] { targetBone.transform }; targetSkin.rootBone = targetBone.transform;
        targetSkin.localBounds = new Bounds(Vector3.one, Vector3.one * 7);
        sourceStatic.AddComponent<DiNeTransplantProbe>().marker = 67;
        var targetOnly = Child(target, "TargetOnly").AddComponent<DiNeTransplantProbe>();
        targetOnly.bone = targetBone.transform; targetOnly.sibling = targetSkin; targetOnly.mesh = newMesh;
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        string staticGeometryBefore = EditorJsonUtility.ToJson(filter) + EditorJsonUtility.ToJson(renderer);
        string skinGeometryBefore = EditorJsonUtility.ToJson(targetSkin);
        var root = Execute(source, target).ResultRoot;
        var actualStatic = Find(root, "Static"); var actualSkin = Find(root, "Skin").GetComponent<SkinnedMeshRenderer>();
        Require(actualStatic.GetComponent<MeshFilter>().sharedMesh == newMesh, "Target static mesh changed");
        var actualRenderer = actualStatic.GetComponent<MeshRenderer>();
        Require(actualRenderer.sharedMaterial == newMaterial && !actualRenderer.enabled, "Target renderer settings changed");
        Require(actualSkin.sharedMesh == newMesh && actualSkin.sharedMaterial == newMaterial && actualSkin.localBounds == targetSkin.localBounds, "Target skinned renderer data changed");
        Require(actualSkin.rootBone == Find(root, "Bone").transform && actualSkin.bones.Single() == Find(root, "Bone").transform, "Target bones refer to input hierarchy");
        Require(actualStatic.GetComponent<DiNeTransplantProbe>().marker == 67, "Nonmesh component on a mesh host was lost");
        var actualOnly = Find(root, "TargetOnly").GetComponent<DiNeTransplantProbe>();
        Require(actualOnly.bone == Find(root, "Bone").transform && actualOnly.sibling == actualSkin && actualOnly.mesh == newMesh, "Target-only references were changed");
        Require(staticGeometryBefore == EditorJsonUtility.ToJson(filter) + EditorJsonUtility.ToJson(renderer) && skinGeometryBefore == EditorJsonUtility.ToJson(targetSkin), "Target renderer serialized geometry data changed");
        RequireSourceUnchanged(source, sourceBefore);
    }

    private static void ComponentReferences()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var external = new GameObject("External scene object");
        var bone = Child(source, "Bone"); Child(target, "Bone");
        var oldBody = Child(source, "Body"); var newBody = Child(target, "Body");
        var first = oldBody.AddComponent<DiNeTransplantProbe>(); var second = oldBody.AddComponent<DiNeTransplantProbe>();
        newBody.AddComponent<DiNeTransplantProbe>().marker = -1;
        first.marker = 11; first.text = "한국어 日本語"; first.enabled = false; second.marker = 22;
        var collider = oldBody.AddComponent<BoxCollider>();
        first.owner = oldBody; first.bone = bone.transform; first.sibling = second; first.external = external;
        first.items = new Object[] { first, second, collider, bone, bone.transform, external };
        first.groups = new List<DiNeTransplantReferenceGroup> {
            new DiNeTransplantReferenceGroup { owner = bone, transform = bone.transform, component = collider, items = new Object[] { second, bone, external } }
        };
        second.sibling = first;
        UnityEventTools.AddObjectPersistentListener(first.onObject, second.CaptureObject, bone);
        first.onObject.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        DiNeTransplantProbe.ForbiddenSourceRoot = source.transform;
        DiNeTransplantProbe.TransientSourceReferenceObservations = 0;
        var root = Execute(source, target).ResultRoot; var body = Find(root, "Body"); var probes = body.GetComponents<DiNeTransplantProbe>();
        DiNeTransplantProbe.ForbiddenSourceRoot = null;
        Require(DiNeTransplantProbe.TransientSourceReferenceObservations == 0, "OnValidate observed transient references into the source hierarchy");
        Require(probes.Length == 2 && probes[0].marker == 11 && probes[1].marker == 22, "Same-type component order/count/values changed");
        var actual = probes[0]; var actualBone = Find(root, "Bone"); var actualCollider = body.GetComponent<BoxCollider>();
        Require(!actual.enabled && actual.text == first.text, "MonoBehaviour serialized values changed");
        Require(actual.owner == body && actual.bone == actualBone.transform && actual.sibling == probes[1] && probes[1].sibling == actual, "Typed/cyclic sibling references were not remapped");
        Require(actual.external == external, "External scene reference changed");
        Require(actual.items.SequenceEqual(new Object[] { actual, probes[1], actualCollider, actualBone, actualBone.transform, external }), "Object array references were not remapped");
        var group = actual.groups.Single();
        Require(group.owner == actualBone && group.transform == actualBone.transform && group.component == actualCollider, "Nested typed references were not remapped");
        Require(group.items.SequenceEqual(new Object[] { probes[1], actualBone, external }), "Nested array references were not remapped");
        Require(actual.onObject.GetPersistentEventCount() == 1 && actual.onObject.GetPersistentTarget(0) == probes[1], "UnityEvent persistent callback target was not remapped");
        actual.onObject.Invoke(null);
        Require(probes[1].captured == actualBone && second.captured == null, "UnityEvent persistent object argument still targets the source hierarchy");
        string scenePath = caseFolder + "/SerializedReferences.unity";
        Object.DestroyImmediate(source);
        EditorSceneManager.SaveScene(root.scene, scenePath);
        EditorSceneManager.OpenScene(scenePath);
        var restoredRoot = Object.FindObjectsOfType<DiNeTransplantProbe>(true).Single(p => p.marker == 11).transform.root.gameObject;
        var restored = Find(restoredRoot, "Body").GetComponents<DiNeTransplantProbe>();
        Require(restored[0].sibling == restored[1] && restored[0].groups[0].component == restored[0].GetComponent<BoxCollider>(), "References did not survive actual scene save/reload");
    }

    private static void CollisionMeshes()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var oldMesh = MeshAsset("OldRendered", 1); var newMesh = MeshAsset("NewRendered", 2);
        var independent = MeshAsset("IndependentCollision", .25f); var orphan = MeshAsset("RemovedRendered", 3); var retained = MeshAsset("ExistingCollision", 4);
        var oldBody = Child(source, "Body"); var newBody = Child(target, "Body");
        oldBody.AddComponent<MeshFilter>().sharedMesh = oldMesh; oldBody.AddComponent<MeshRenderer>();
        newBody.AddComponent<MeshFilter>().sharedMesh = newMesh; newBody.AddComponent<MeshRenderer>();
        var removed = Child(source, "RemovedBody"); removed.AddComponent<MeshFilter>().sharedMesh = orphan; removed.AddComponent<MeshRenderer>();
        var oldColliders = Child(source, "Colliders"); var newColliders = Child(target, "Colliders");
        foreach (var mesh in new[] { independent, oldMesh, orphan, orphan }) oldColliders.AddComponent<MeshCollider>().sharedMesh = mesh;
        newColliders.AddComponent<MeshCollider>(); newColliders.AddComponent<MeshCollider>(); newColliders.AddComponent<MeshCollider>().sharedMesh = retained;
        var report = Execute(source, target); var actual = Find(report.ResultRoot, "Colliders").GetComponents<MeshCollider>();
        Require(actual.Length == 4, "Collision mesh component count changed");
        Require(actual[0].sharedMesh == independent, "Independent collision-only mesh was cleared");
        Require(actual[1].sharedMesh == newMesh, "Rendered collision mesh was not remapped to the edited model");
        Require(actual[2].sharedMesh == retained, "Ambiguous/missing rendered mesh replaced an existing target collision mesh");
        Require(actual[3].sharedMesh == null, "Removed rendered mesh was copied into a new collision component");
        Require(report.Diagnostics.Any(d => d.Code == "MeshReferenceRetargeted") && report.Diagnostics.Any(d => d.Code == "MeshReferenceExcluded"), "Collision mesh migration diagnostics missing");
    }

    private static void AnimatorAvatars()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        Child(source, "Rig"); Child(target, "Rig");
        var oldAvatar = AvatarBuilder.BuildGenericAvatar(source, ""); var newAvatar = AvatarBuilder.BuildGenericAvatar(target, "");
        Require(oldAvatar != null && newAvatar != null, "Generic test Avatar generation failed");
        AssetDatabase.CreateAsset(oldAvatar, caseFolder + "/OldAvatar.asset"); AssetDatabase.CreateAsset(newAvatar, caseFolder + "/NewAvatar.asset");
        var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(caseFolder + "/Controller.controller");
        var original = source.AddComponent<Animator>(); original.avatar = oldAvatar; original.runtimeAnimatorController = controller; original.applyRootMotion = true;
        var revised = target.AddComponent<Animator>(); revised.avatar = newAvatar;
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        var report = Execute(source, target); var actual = report.ResultRoot.GetComponent<Animator>();
        Require(actual.avatar == newAvatar && actual.runtimeAnimatorController == controller && actual.applyRootMotion, "Animator Avatar or functionality was not transferred correctly");
        Require(report.Diagnostics.Any(d => d.Code == "AnimatorAvatarPreserved"), "Revised Avatar preservation diagnostic missing");
        RequireSourceUnchanged(source, sourceBefore);
        revised.avatar = null;
        var plan = DiNePrefabTransplantUtility.Analyze(source, target);
        var missing = Execute(source, target);
        Require(missing.ResultRoot.GetComponent<Animator>().avatar == null, "Original incompatible rig Avatar was copied");
        Require(plan.Diagnostics.Any(d => d.Code == "AnimatorAvatarMissing" && d.Severity == DiNePrefabTransplantUtility.DiagnosticSeverity.Warning) &&
            missing.Diagnostics.Any(d => d.Code == "AnimatorAvatarMissing" && d.Severity == DiNePrefabTransplantUtility.DiagnosticSeverity.Warning), "Missing revised Avatar did not warn during both analysis and execution");
    }

    private static void NativeComponents()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var a = Child(source, "A"); var b = Child(source, "B"); Child(target, "A"); Child(target, "B");
        var host = Child(source, "Host"); Child(target, "Host");
        SetTransform(target.transform, 6); SetTransform(Find(target, "A").transform, 3); SetTransform(Find(target, "B").transform, 4); SetTransform(Find(target, "Host").transform, 8);
        var collider = host.AddComponent<SphereCollider>(); collider.center = new Vector3(.1f, .2f, .3f); collider.radius = .61f; collider.isTrigger = true;
        var constraint = host.AddComponent<ParentConstraint>();
        constraint.AddSource(new ConstraintSource { sourceTransform = a.transform, weight = .4f });
        constraint.AddSource(new ConstraintSource { sourceTransform = b.transform, weight = .6f });
        constraint.SetTranslationOffset(0, new Vector3(1, 2, 3)); constraint.SetRotationOffset(1, new Vector3(10, 20, 30));
        constraint.translationAxis = Axis.X | Axis.Z; constraint.rotationAxis = Axis.Y; constraint.weight = .37f; constraint.constraintActive = true; constraint.locked = true;
        var root = Execute(source, target).ResultRoot; var resultHost = Find(root, "Host");
        var actualCollider = resultHost.GetComponent<SphereCollider>(); var actual = resultHost.GetComponent<ParentConstraint>();
        Equal(collider.center, actualCollider.center, "Collider center");
        Require(Mathf.Abs(actualCollider.radius - .61f) < .00001f && actualCollider.isTrigger, "Collider values changed");
        Require(actual.sourceCount == 2 && actual.GetSource(0).sourceTransform == Find(root, "A").transform && actual.GetSource(1).sourceTransform == Find(root, "B").transform, "Native constraint sources were not remapped");
        Require(Mathf.Abs(actual.GetSource(0).weight - .4f) < .00001f && Mathf.Abs(actual.weight - .37f) < .00001f, "Constraint weights changed");
        Equal(constraint.GetTranslationOffset(0), actual.GetTranslationOffset(0), "Constraint translation offset");
        Equal(constraint.GetRotationOffset(1), actual.GetRotationOffset(1), "Constraint rotation offset");
        Require(actual.translationAxis == constraint.translationAxis && actual.rotationAxis == constraint.rotationAxis && actual.constraintActive && actual.locked, "Constraint settings changed");
    }

    private static void RestoreObjectsAndTransforms()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        SetTransform(source.transform, 3); SetTransform(target.transform, 9);
        var sourceBone = Child(source, "Bone"); var targetBone = Child(target, "Bone"); SetTransform(sourceBone.transform, 2); SetTransform(targetBone.transform, 8);
        var extra = Child(sourceBone, "Extra"); SetTransform(extra.transform, 4); extra.SetActive(false); extra.layer = 7;
        extra.tag = "EditorOnly"; GameObjectUtility.SetStaticEditorFlags(extra, StaticEditorFlags.OccluderStatic);
        sourceBone.layer = 3; sourceBone.tag = "EditorOnly"; targetBone.layer = 9; targetBone.tag = "Player";
        GameObjectUtility.SetStaticEditorFlags(sourceBone, StaticEditorFlags.OccluderStatic); GameObjectUtility.SetStaticEditorFlags(targetBone, StaticEditorFlags.BatchingStatic);
        var leaf = Child(extra, "Leaf"); SetTransform(leaf.transform, 5); leaf.AddComponent<BoxCollider>();
        var report = Execute(source, target); var root = report.ResultRoot;
        var actualExtra = Find(root, "Bone/Extra");
        LocalTransform(extra.transform, actualExtra.transform, "Created inactive parent");
        LocalTransform(leaf.transform, Find(root, "Bone/Extra/Leaf").transform, "Created leaf");
        Require(actualExtra.transform.parent == targetBone.transform && !actualExtra.activeSelf && actualExtra.layer == 7 && actualExtra.tag == "EditorOnly" && GameObjectUtility.GetStaticEditorFlags(actualExtra) == StaticEditorFlags.OccluderStatic && Find(root, "Bone/Extra/Leaf").GetComponent<BoxCollider>() != null, "Created helper parent/object values/components were lost");
        Require(report.CreatedObjects == 2, "Wrong created object count");
    }

    private static void KeepMatchedTransforms()
    {
        var source = new GameObject("Source", typeof(RectTransform)); var target = new GameObject("Target", typeof(RectTransform));
        var sourcePanel = new GameObject("Panel", typeof(RectTransform)); sourcePanel.transform.SetParent(source.transform, false);
        var targetPanel = new GameObject("Panel", typeof(RectTransform)); targetPanel.transform.SetParent(target.transform, false);
        var targetRect = (RectTransform)targetPanel.transform;
        targetRect.anchorMin = new Vector2(.13f, .27f); targetRect.anchorMax = new Vector2(.72f, .83f); targetRect.pivot = new Vector2(.36f, .42f);
        targetRect.sizeDelta = new Vector2(321, 123); targetRect.anchoredPosition3D = new Vector3(37, 49, 11);
        SetTransform(source.transform, 3); SetTransform(target.transform, 9); SetTransform(sourcePanel.transform, 2); SetTransform(targetPanel.transform, 8);
        targetPanel.layer = 11; targetPanel.tag = "Player"; targetPanel.SetActive(false); GameObjectUtility.SetStaticEditorFlags(targetPanel, StaticEditorFlags.BatchingStatic);
        sourcePanel.layer = 4; sourcePanel.tag = "EditorOnly";
        sourcePanel.AddComponent<DiNeTransplantProbe>().marker = 92;
        var extra = new GameObject("Extra", typeof(RectTransform)); extra.transform.SetParent(sourcePanel.transform, false);
        var extraRect = (RectTransform)extra.transform;
        extraRect.anchorMin = new Vector2(.12f, .23f); extraRect.anchorMax = new Vector2(.78f, .89f); extraRect.pivot = new Vector2(.34f, .56f); extraRect.sizeDelta = new Vector2(45, 67);
        SetTransform(extra.transform, 4); extraRect.anchoredPosition3D = extraRect.anchoredPosition3D; extra.AddComponent<BoxCollider>();
        ((RectTransform)source.transform).anchoredPosition3D = ((RectTransform)source.transform).anchoredPosition3D;
        ((RectTransform)sourcePanel.transform).anchoredPosition3D = ((RectTransform)sourcePanel.transform).anchoredPosition3D;
        var root = Execute(source, target).ResultRoot;
        Require(Find(root, "Panel").GetComponent<DiNeTransplantProbe>().marker == 92 && Find(root, "Panel/Extra").GetComponent<BoxCollider>() != null, "Components/helpers did not copy while preserving target UI layout");
        LocalTransform(extra.transform, Find(root, "Panel/Extra").transform, "Created RectTransform helper under different target parent layout");
    }

    private static void TargetRootIdentity()
    {
        var sourceParent = new GameObject("SourceParent"); var targetParent = new GameObject("TargetParent");
        sourceParent.transform.position = new Vector3(1, 2, 3); sourceParent.transform.rotation = Quaternion.Euler(0, 20, 0); sourceParent.transform.localScale = Vector3.one * 1.5f;
        targetParent.transform.position = new Vector3(8, 0, 1); targetParent.transform.rotation = Quaternion.Euler(13, 30, 17); targetParent.transform.localScale = new Vector3(2, 3, .7f);
        var source = Child(sourceParent, "Source"); Child(targetParent, "Before"); var target = Child(targetParent, "ChosenTarget"); Child(targetParent, "After");
        SetTransform(source.transform, 3); SetTransform(target.transform, 8); target.SetActive(false);
        source.tag = "EditorOnly"; source.layer = 4; target.tag = "Player"; target.layer = 12; GameObjectUtility.SetStaticEditorFlags(target, StaticEditorFlags.BatchingStatic);
        var extra = Child(source, "Extra"); SetTransform(extra.transform, 4); extra.AddComponent<BoxCollider>();
        Matrix4x4 before = target.transform.localToWorldMatrix; string targetBefore = Snapshot(target); int sibling = target.transform.GetSiblingIndex();
        var report = Execute(source, target);
        Equal(before, report.ResultRoot.transform.localToWorldMatrix, "Target placement across different parents");
        LocalTransform(extra.transform, Find(target, "Extra").transform, "Created helper local transform across different scene parents");
        Require(target.transform.GetSiblingIndex() == sibling && !target.activeSelf, "Chosen inactive target root identity changed");
        Undo.PerformUndo();
        Require(Snapshot(target) == targetBefore, "Undo did not restore the unchanged target and its added helper");
        Undo.PerformRedo(); Equal(before, target.transform.localToWorldMatrix, "Redo changed the target root world placement");
    }

    private static void RemovedMeshReferences()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var removed = Child(source, "Removed"); removed.AddComponent<MeshFilter>().sharedMesh = MeshAsset("RemovedMesh", 1); var renderer = removed.AddComponent<MeshRenderer>();
        var removedProbe = removed.AddComponent<DiNeTransplantProbe>();
        var holder = Child(source, "Holder").AddComponent<DiNeTransplantProbe>(); Child(target, "Holder");
        holder.owner = removed; holder.bone = removed.transform; holder.sibling = removedProbe;
        holder.items = new Object[] { removed, removed.transform, renderer, removedProbe };
        var report = Execute(source, target); var root = report.ResultRoot; var actual = Find(root, "Holder").GetComponent<DiNeTransplantProbe>();
        Require(root.transform.Find("Removed") == null && root.GetComponentsInChildren<Renderer>(true).Length == 0, "A removed mesh host was recreated");
        Require(actual.owner == null && actual.bone == null && actual.sibling == null && actual.items.All(o => o == null), "Dangling source mesh subtree references survived");
        Require(report.SkippedMeshObjects >= 1 && report.ClearedReferences >= 7, "Excluded mesh/reference diagnostics were not counted");
    }

    private static void RemovedMeshAncestor()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var removed = Child(source, "Removed"); removed.AddComponent<SkinnedMeshRenderer>();
        Child(Child(removed, "Hidden"), "Leaf").AddComponent<BoxCollider>();
        var report = Execute(source, target);
        Require(report.ResultRoot.transform.childCount == 0, "A descendant of an unmapped mesh ancestor was recreated");
        Require(report.Diagnostics.Any(d => d.Code == "UnmappedParent"), "Skipped descendant explanation missing");
    }

    private static void DuplicateSiblingPaths()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        for (int i = 0; i < 2; i++)
        {
            var oldBone = Child(source, "Joint"); var newBone = Child(target, "Joint");
            oldBone.AddComponent<DiNeTransplantProbe>().marker = i + 10; SetTransform(oldBone.transform, i + 1); SetTransform(newBone.transform, i + 8);
        }
        var root = Execute(source, target).ResultRoot;
        Require(root.transform.childCount == 2, "Duplicate sibling paths created extra objects");
        for (int i = 0; i < 2; i++)
        {
            Require(root.transform.GetChild(i).GetComponent<DiNeTransplantProbe>().marker == i + 10, "Duplicate sibling component mapped to the wrong ordinal");
        }
    }

    private static void DuplicateCountMismatch()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        Child(source, "Joint").AddComponent<DiNeTransplantProbe>().marker = 10;
        Child(source, "Joint").AddComponent<DiNeTransplantProbe>().marker = 20;
        Child(target, "Joint").AddComponent<DiNeTransplantProbe>().marker = 99;
        var report = Execute(source, target); var root = report.ResultRoot;
        Require(root.transform.childCount == 1 && Find(root, "Joint").GetComponent<DiNeTransplantProbe>().marker == 99, "An ambiguous duplicate overwrote or added a target branch");
        Require(report.Diagnostics.Any(d => d.Code.IndexOf("Ambiguous", StringComparison.OrdinalIgnoreCase) >= 0), "Ambiguous sibling diagnostic missing");
    }

    private static void UniqueBoneFallback()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var oldRig = Child(source, "OldRig"); var newRig = Child(target, "NewRig");
        oldRig.transform.localPosition = new Vector3(1, 2, 3); oldRig.transform.localRotation = Quaternion.Euler(0, 20, 0); oldRig.transform.localScale = Vector3.one * .5f;
        newRig.transform.localPosition = new Vector3(9, 0, 0); newRig.transform.localRotation = Quaternion.Euler(0, 25, 0); newRig.transform.localScale = Vector3.one * 2;
        var sourceBone = Child(oldRig, "UniqueBone"); sourceBone.AddComponent<DiNeTransplantProbe>().marker = 71;
        var targetBone = Child(newRig, "UniqueBone");
        SetTransform(sourceBone.transform, 4); SetTransform(targetBone.transform, 9);
        var root = Execute(source, target).ResultRoot;
        var mapped = Find(root, "NewRig/UniqueBone");
        Require(mapped.GetComponent<DiNeTransplantProbe>().marker == 71, "Unique moved bone was not recovered");
        Require(root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "UniqueBone") == 1, "Unique fallback duplicated a bone");
        Require(mapped == targetBone && mapped.transform.parent == newRig.transform, "Moved target bone was reparented or replaced");
    }

    private static void RequiredCollider()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var host = Child(source, "Host"); Child(target, "Host");
        var probe = host.AddComponent<DiNeTransplantRequiresCollider>(); probe.marker = 14; probe.colliderReference = host.GetComponent<BoxCollider>(); probe.colliderReference.size = Vector3.one * 3;
        var actualHost = Find(Execute(source, target).ResultRoot, "Host"); var actual = actualHost.GetComponent<DiNeTransplantRequiresCollider>();
        Require(actualHost.GetComponents<BoxCollider>().Length == 1 && actual.marker == 14, "Required dependency was duplicated or component lost");
        Require(actual.colliderReference == actualHost.GetComponent<BoxCollider>(), "Required collider reference points to the source");
        Equal(Vector3.one * 3, actual.colliderReference.size, "Required collider serialized values");
    }

    private static void UniqueRendererHostFallback()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var oldHost = Child(Child(source, "OldMeshes"), "UniqueGarment"); var targetHost = Child(Child(target, "NewMeshes"), "UniqueGarment");
        var oldMesh = MeshAsset("OldGarment", 1); var editedMesh = MeshAsset("EditedGarment", 2);
        oldHost.AddComponent<MeshFilter>().sharedMesh = oldMesh; oldHost.AddComponent<MeshRenderer>();
        targetHost.AddComponent<MeshFilter>().sharedMesh = editedMesh; var targetRenderer = targetHost.AddComponent<MeshRenderer>(); targetRenderer.enabled = false;
        SetTransform(oldHost.transform, 2); SetTransform(targetHost.transform, 9);
        var originalProbe = oldHost.AddComponent<DiNeTransplantProbe>(); originalProbe.marker = 72; originalProbe.owner = oldHost; originalProbe.sibling = oldHost.AddComponent<BoxCollider>();
        var report = Execute(source, target); var actual = targetHost.GetComponent<DiNeTransplantProbe>();
        Require(actual != null && actual.marker == 72 && actual.owner == targetHost && actual.sibling == targetHost.GetComponent<BoxCollider>(), "Nonmesh functionality on a unique moved renderer host was not transferred");
        Require(targetHost.GetComponent<MeshFilter>().sharedMesh == editedMesh && targetHost.GetComponent<MeshRenderer>() == targetRenderer && !targetRenderer.enabled, "Renderer fallback replaced target geometry");
        Require(target.GetComponentsInChildren<Renderer>(true).Length == 1 && report.Diagnostics.Any(d => d.Code == "NameMatched"), "Moved renderer host was duplicated or fallback not diagnosed");
    }

    private static void ChangedParentShear()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var sourceBone = Child(Child(source, "OldRig"), "UniqueBone");
        sourceBone.transform.localPosition = new Vector3(1, 2, 3); sourceBone.transform.localRotation = Quaternion.Euler(0, 47, 0);
        var wrapper = Child(target, "NewRig"); wrapper.transform.localPosition = new Vector3(5, 1, 0);
        wrapper.transform.localRotation = Quaternion.Euler(0, 30, 0); wrapper.transform.localScale = new Vector3(2, 1, 3);
        var targetBone = Child(wrapper, "UniqueBone"); SetTransform(targetBone.transform, 7);
        var helper = Child(sourceBone, "Helper"); SetTransform(helper.transform, 3); helper.AddComponent<BoxCollider>();
        var leaf = Child(helper, "Leaf"); SetTransform(leaf.transform, 4); leaf.SetActive(false);
        var report = Execute(source, target); var actual = Find(report.ResultRoot, "NewRig/UniqueBone");
        Require(actual == targetBone && actual.transform.parent == wrapper.transform, "Rotated nonuniform target hierarchy changed");
        LocalTransform(helper.transform, Find(actual, "Helper").transform, "New helper under rotated nonuniform wrapper");
        LocalTransform(leaf.transform, Find(actual, "Helper/Leaf").transform, "New helper leaf local transform");
        Require(Find(actual, "Helper").GetComponent<BoxCollider>() != null && !Find(actual, "Helper/Leaf").activeSelf, "New subtree functionality/active state was lost");
    }

    private static void CallbackPreservation()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        SetTransform(source.transform, 3); SetTransform(target.transform, 9);
        Child(source, "Bone"); var targetBone = Child(target, "Bone"); SetTransform(targetBone.transform, 7);
        var targetOnly = Child(target, "TargetOnly"); SetTransform(targetOnly.transform, 5);
        var panel = new GameObject("TargetOnlyUI", typeof(RectTransform)); panel.transform.SetParent(target.transform, false);
        var rect = (RectTransform)panel.transform; rect.anchorMin = new Vector2(.1f, .2f); rect.anchorMax = new Vector2(.8f, .9f); rect.sizeDelta = new Vector2(121, 232);
        target.layer = 9; target.tag = "Player"; targetOnly.layer = 11; targetOnly.SetActive(false);
        var original = source.AddComponent<DiNeTransplantMutationProbe>(); original.marker = 515;
        var targetBefore = Snapshot(target); var sourceBefore = Snapshot(source); var targetStates = CaptureExistingTarget(target);
        DiNeTransplantMutationProbe.TargetRoot = target;
        DiNeTransplantMutationProbe.Objects = new[] { target, targetBone, targetOnly, panel };
        DiNeTransplantMutationProbe.Observations = 0; DiNeTransplantMutationProbe.Mutated = false; DiNeTransplantMutationProbe.ActiveOnly = false; DiNeTransplantMutationProbe.Armed = true;
        try { Execute(source, target); }
        finally { DiNeTransplantMutationProbe.Armed = false; }
        Require(DiNeTransplantMutationProbe.Observations > 0 && target.GetComponent<DiNeTransplantMutationProbe>().marker == 515, "Real callbacks were not invoked or copied config was lost");
        RequireExistingTargetUnchanged(targetStates);
        Undo.PerformUndo(); Require(Snapshot(target) == targetBefore && Snapshot(source) == sourceBefore, "Callback preservation Undo changed existing inputs");
        Undo.PerformRedo(); RequireExistingTargetUnchanged(targetStates); Require(Snapshot(source) == sourceBefore, "Callback preservation Redo changed source");
        Require(target.GetComponent<DiNeTransplantMutationProbe>().marker == 515, "Callback probe configuration was lost on Redo");

        var template = new GameObject("PrefabCallbackTarget"); SetTransform(template.transform, 4);
        Child(template, "Bone"); Child(template, "TargetOnly").SetActive(false);
        var asset = PrefabUtility.SaveAsPrefabAsset(template, caseFolder + "/CallbackTarget.prefab"); Object.DestroyImmediate(template);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        var instanceBefore = Snapshot(instance); var instanceStates = CaptureExistingTarget(instance); string instanceName = instance.name;
        string overridesBefore = ActiveOverrides(instance);
        DiNeTransplantMutationProbe.TargetRoot = instance; DiNeTransplantMutationProbe.Objects = new[] { instance, Find(instance, "Bone"), Find(instance, "TargetOnly") };
        DiNeTransplantMutationProbe.Mutated = false; DiNeTransplantMutationProbe.ActiveOnly = true; DiNeTransplantMutationProbe.Armed = true;
        try { Execute(source, instance); }
        finally { DiNeTransplantMutationProbe.Armed = false; DiNeTransplantMutationProbe.ActiveOnly = false; }
        Require(DiNeTransplantMutationProbe.Mutated, "Active-only prefab callback never ran");
        RequireExistingTargetUnchanged(instanceStates);
        string overridesAfter = ActiveOverrides(instance);
        Undo.PerformUndo(); Require(Snapshot(instance) == instanceBefore && Snapshot(source) == sourceBefore, "Active-only prefab callback Undo failed");
        Undo.PerformRedo(); RequireExistingTargetUnchanged(instanceStates);
        string overridesRedo = ActiveOverrides(instance);
        string scenePath = caseFolder + "/CallbackOverrides.unity";
        EditorSceneManager.SaveScene(instance.scene, scenePath); EditorSceneManager.OpenScene(scenePath);
        var reloadedProbe = Object.FindObjectsOfType<DiNeTransplantMutationProbe>(true).SingleOrDefault(p => p.gameObject.name == instanceName);
        var reloaded = reloadedProbe != null ? reloadedProbe.gameObject : null;
        Require(reloaded != null && PrefabUtility.IsPartOfPrefabInstance(reloaded) && reloaded.activeSelf && Find(reloaded, "Bone").activeSelf && !Find(reloaded, "TargetOnly").activeSelf,
            "Stale prefab active overrides returned after callback repair and scene reload; Defaults=root:true,Bone:true,TargetOnly:false; Before=" + overridesBefore + "; After=" + overridesAfter + "; Redo=" + overridesRedo + "; Reload=" + (reloaded == null ? "Missing" : ActiveOverrides(reloaded) + "; Actual=root:" + reloaded.activeSelf + ",Bone:" + Find(reloaded, "Bone").activeSelf + ",TargetOnly:" + Find(reloaded, "TargetOnly").activeSelf));
        Require(reloaded.GetComponent<DiNeTransplantMutationProbe>().marker == 515, "Prefab callback configuration did not persist");
    }

    private static string ActiveOverrides(GameObject instance)
    {
        return string.Join(",", (PrefabUtility.GetPropertyModifications(instance) ?? new PropertyModification[0])
            .Where(p => p.propertyPath == "m_IsActive").Select(p => (p.target != null ? p.target.name : "null") + ":" + p.value));
    }

    private static void CreatedHelperCallbacks()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var sourceBone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        SetTransform(sourceBone.transform, 2); SetTransform(targetBone.transform, 7);
        var helper = Child(sourceBone, "Helper"); var probe = helper.AddComponent<DiNeTransplantHelperMutationProbe>(); probe.marker = 616;
        SetTransform(helper.transform, 4); helper.layer = 11; helper.tag = "EditorOnly";
        DiNeTransplantHelperMutationProbe.SourceRoot = source; DiNeTransplantHelperMutationProbe.Observations = 0; DiNeTransplantHelperMutationProbe.Armed = true;
        GameObject actual;
        try { actual = Find(Execute(source, target).ResultRoot, "Bone/Helper"); }
        finally { DiNeTransplantHelperMutationProbe.Armed = false; }
        Require(DiNeTransplantHelperMutationProbe.Observations > 0, "New helper Reset/OnValidate/OnEnable fixture did not run");
        LocalTransform(helper.transform, actual.transform, "Created helper after own lifecycle callbacks");
        Require(actual.transform.parent == targetBone.transform && actual.GetComponent<DiNeTransplantHelperMutationProbe>().marker == 616 && actual.GetComponent<DiNeTransplantHelperMutationProbe>().enabled && actual.activeSelf && actual.layer == 11 && actual.tag == "EditorOnly", "Created helper parent or copied functional configuration changed");
    }

    private static void HelperQuaternionRoundoff()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var bone = Child(source, "Bone"); var targetBone = Child(target, "Bone"); SetTransform(targetBone.transform, 7);
        source.AddComponent<DiNeTransplantMutationProbe>().marker = 919;
        var helper = Child(bone, "Option_C_R"); helper.AddComponent<DiNeTransplantProbe>().marker = 717;
        helper.transform.localPosition = new Vector3(.049511235f, .11228181f, -.08664975f);
        helper.transform.localScale = new Vector3(.923092f, .9845361f, .96655154f);
        var scratch = new GameObject("Actual setter precision inspection");
        bool observedRoundoff = false;
        int reproducedCase = -1;
        for (int i = 0; i < 128 && !observedRoundoff; i++)
        {
            var rotation = i == 0 ? new Quaternion(.3933636f, .2968083f, .42958885f, -.75671893f) : Quaternion.Euler(12.34567f + i * 1.2345f, 67.89123f - i * .4567f, 98.76543f + i * .8912f);
            float drift = i == 0 ? 1f : i % 2 == 0 ? 1.0000002f : .9999998f;
            WriteRawRotation(helper.transform, new Quaternion(rotation.x * drift, rotation.y * drift, rotation.z * drift, rotation.w * drift));
            scratch.transform.localPosition = helper.transform.localPosition;
            scratch.transform.localScale = helper.transform.localScale;
            scratch.transform.localRotation = helper.transform.localRotation;
            observedRoundoff = !helper.transform.localRotation.Equals(scratch.transform.localRotation);
            if (observedRoundoff) reproducedCase = i;
        }
        Require(observedRoundoff && reproducedCase == 0, "Actual reported Option_C_R quaternion did not reproduce Unity normalization roundoff");
        Results.Add("INFO Option_C_R actual raw quaternion=" + helper.transform.localRotation.ToString("R") + "; Unity setter=" + scratch.transform.localRotation.ToString("R") + "; Quaternion.Equals=False");
        var matches = typeof(DiNePrefabTransplantUtility).GetMethod("LocalTransformMatches", BindingFlags.Static | BindingFlags.NonPublic);
        Require((bool)matches.Invoke(null, new object[] { helper.transform, scratch.transform }), "Equivalent normalized rotation was rejected");
        scratch.transform.localRotation = Quaternion.AngleAxis(.01f, Vector3.up) * helper.transform.localRotation;
        Require(!(bool)matches.Invoke(null, new object[] { helper.transform, scratch.transform }), "A genuine .01-degree rotation change was accepted as roundoff");
        Object.DestroyImmediate(scratch);
        WriteRawRotation(targetBone.transform, helper.transform.localRotation);
        DiNeTransplantMutationProbe.TargetRoot = target; DiNeTransplantMutationProbe.Objects = new[] { targetBone };
        DiNeTransplantMutationProbe.Mutated = false; DiNeTransplantMutationProbe.ActiveOnly = false; DiNeTransplantMutationProbe.Armed = true;
        GameObject actual;
        try { actual = Find(Execute(source, target).ResultRoot, "Bone/Option_C_R"); }
        finally { DiNeTransplantMutationProbe.Armed = false; }
        Require(DiNeTransplantMutationProbe.Mutated, "Raw imported target callback restoration fixture did not run");
        Require(actual.transform.localPosition.Equals(helper.transform.localPosition) && actual.transform.localScale.Equals(helper.transform.localScale), "Quaternion tolerance weakened helper position/scale preservation");
        Equal(helper.transform.localRotation.normalized * Vector3.forward, actual.transform.localRotation.normalized * Vector3.forward, "Copied helper forward orientation");
        Equal(helper.transform.localRotation.normalized * Vector3.up, actual.transform.localRotation.normalized * Vector3.up, "Copied helper up orientation");
        Require(actual.GetComponent<DiNeTransplantProbe>().marker == 717, "Precision fix lost copied helper functionality");
    }

    public static void WriteRawRotation(Transform transform, Quaternion rotation)
    {
        using (var serialized = new SerializedObject(transform))
        {
            var property = serialized.FindProperty("m_LocalRotation");
            property.FindPropertyRelative("x").floatValue = rotation.x;
            property.FindPropertyRelative("y").floatValue = rotation.y;
            property.FindPropertyRelative("z").floatValue = rotation.z;
            property.FindPropertyRelative("w").floatValue = rotation.w;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void HelperQuaternionSign()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var helper = Child(source, "SignedHelper"); helper.AddComponent<DiNeTransplantQuaternionProbe>().marker = 818;
        SetTransform(helper.transform, 4); SetTransform(target.transform, 9);
        DiNeTransplantQuaternionProbe.SourceRoot = source; DiNeTransplantQuaternionProbe.Observations = 0; DiNeTransplantQuaternionProbe.Armed = true;
        GameObject actual;
        try { actual = Find(Execute(source, target).ResultRoot, "SignedHelper"); }
        finally { DiNeTransplantQuaternionProbe.Armed = false; }
        Require(DiNeTransplantQuaternionProbe.Observations > 0, "Actual quaternion sign callback was not invoked");
        Require(Quaternion.Dot(helper.transform.localRotation.normalized, actual.transform.localRotation.normalized) < -.99999f, "Fixture did not retain the equivalent opposite-sign quaternion");
        Require(actual.transform.localPosition.Equals(helper.transform.localPosition) && actual.transform.localScale.Equals(helper.transform.localScale) && actual.GetComponent<DiNeTransplantQuaternionProbe>().marker == 818, "Equivalent quaternion changed helper placement or copied configuration");
        Equal(helper.transform.localRotation.normalized * Vector3.forward, actual.transform.localRotation.normalized * Vector3.forward, "Opposite-sign quaternion forward orientation");
        Equal(helper.transform.localRotation.normalized * Vector3.up, actual.transform.localRotation.normalized * Vector3.up, "Opposite-sign quaternion up orientation");
    }

    private static void RequiredMeshExclusion()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var host = Child(source, "Host"); Child(target, "Host");
        host.AddComponent<DiNeTransplantRequiresMeshIndirect>().marker = 18;
        var report = Execute(source, target); var actualHost = Find(report.ResultRoot, "Host");
        Require(actualHost.GetComponent<Renderer>() == null && actualHost.GetComponent<MeshFilter>() == null, "RequireComponent resurrected excluded mesh components");
        Require(actualHost.GetComponent<DiNeTransplantRequiresMesh>() == null && actualHost.GetComponent<DiNeTransplantRequiresMeshIndirect>() == null, "An unsafe recursive mesh dependency was copied");
        Require(report.SkippedComponents >= 4, "Excluded recursive component dependencies not counted");
    }

    private static void CyclicManagedReferences()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var bone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        var probe = source.AddComponent<DiNeTransplantProbe>();
        var first = new DiNeTransplantManagedLink { reference = bone }; var second = new DiNeTransplantManagedLink { reference = bone.transform };
        first.bounds = new Bounds(new Vector3(1, 2, 3), new Vector3(4, 5, 6)); first.SetPrivateValue(42);
        first.curve = AnimationCurve.EaseInOut(0, 1, 2, 3); first.curve.preWrapMode = WrapMode.Loop;
        first.gradient = new Gradient(); first.gradient.SetKeys(new[] { new GradientColorKey(Color.red, 0), new GradientColorKey(Color.blue, 1) }, new[] { new GradientAlphaKey(.25f, 0), new GradientAlphaKey(.75f, 1) });
        first.next = second; second.next = first; probe.managed = first; probe.managedAlias = first;
        Require(ReferenceEquals(probe.managed.next.next, probe.managed) && probe.managed.reference == bone && probe.managed.next.reference == bone.transform, "Source managed-cycle fixture is invalid");
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        DiNeTransplantProbe.ForbiddenSourceRoot = source.transform; DiNeTransplantProbe.TransientSourceReferenceObservations = 0;
        var root = Execute(source, target).ResultRoot; var actualProbe = root.GetComponent<DiNeTransplantProbe>(); var actual = actualProbe.managed;
        DiNeTransplantProbe.ForbiddenSourceRoot = null;
        Require(DiNeTransplantProbe.TransientSourceReferenceObservations == 0, "OnValidate observed transient source references inside a managed graph");
        Require(ReferenceEquals(probe.managed.next.next, probe.managed) && probe.managed.reference == bone && probe.managed.next.reference == bone.transform,
            "Copying a managed graph modified the source references. Destination=" + EditorJsonUtility.ToJson(actualProbe));
        RequireSourceUnchanged(source, sourceBefore);
        Require(actual != null && actual.next != null && ReferenceEquals(actual.next.next, actual),
            "Managed cycle topology changed. Source=" + EditorJsonUtility.ToJson(probe) + "; Destination=" + EditorJsonUtility.ToJson(actualProbe));
        Require(actual.reference == Find(root, "Bone") && actual.next.reference == Find(root, "Bone").transform, "References inside a managed cycle were not remapped");
        Require(ReferenceEquals(actualProbe.managedAlias, actual), "Shared references across top-level managed fields were duplicated");
        Require(actual.PrivateValue == 42 && actual.bounds == first.bounds, "Managed private serialized data or native value fields changed");
        Require(actual.curve != first.curve && Mathf.Abs(actual.curve.Evaluate(.75f) - first.curve.Evaluate(.75f)) < .00001f && actual.curve.preWrapMode == WrapMode.Loop, "Managed AnimationCurve was lost or shared with the source");
        Require(actual.gradient != first.gradient && actual.gradient.Evaluate(.5f) == first.gradient.Evaluate(.5f), "Managed Gradient was lost or shared with the source");
    }

    private static void PrefabAssetInputs()
    {
        var source = new GameObject("SourcePrefab"); var target = new GameObject("TargetPrefab");
        Child(source, "Bone").AddComponent<BoxCollider>(); Child(target, "Bone");
        var extra = Child(source, "Extra").AddComponent<DiNeTransplantProbe>(); extra.bone = source.transform.Find("Bone");
        var nested = new GameObject("Nested"); Child(nested, "NestedBone");
        var nestedAsset = PrefabUtility.SaveAsPrefabAsset(nested, caseFolder + "/Nested.prefab"); Object.DestroyImmediate(nested);
        var nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(nestedAsset); nestedInstance.transform.SetParent(target.transform, false);
        var sourceAsset = PrefabUtility.SaveAsPrefabAsset(source, caseFolder + "/Source.prefab"); var targetAsset = PrefabUtility.SaveAsPrefabAsset(target, caseFolder + "/Target.prefab");
        Object.DestroyImmediate(source); Object.DestroyImmediate(target);
        AssetDatabase.SaveAssets();
        var sourceBytes = File.ReadAllBytes(caseFolder + "/Source.prefab"); var targetBytes = File.ReadAllBytes(caseFolder + "/Target.prefab");
        var sourceBefore = Snapshot(sourceAsset); var targetBefore = Snapshot(targetAsset);
        RequireAssetTargetRejected(sourceAsset, targetAsset);
        var targetInstance = (GameObject)PrefabUtility.InstantiatePrefab(targetAsset);
        var instanceBefore = Snapshot(targetInstance);
        Undo.ClearAll();
        var root = Execute(sourceAsset, targetInstance).ResultRoot;
        Require(root == targetInstance && PrefabUtility.IsPartOfPrefabInstance(root) && PrefabUtility.IsPartOfPrefabInstance(Find(root, "Nested")), "Direct target or nested prefab instance linkage was lost");
        Require(Find(root, "Extra").GetComponent<DiNeTransplantProbe>().bone == Find(root, "Bone").transform, "Prefab asset internal reference was not remapped");
        Require(PrefabUtility.HasPrefabInstanceAnyOverrides(root, false), "Transplant was not recorded as prefab overrides");
        RequireInputsUnchanged(sourceAsset, targetAsset, sourceBefore, targetBefore);
        Undo.PerformUndo();
        Require(targetInstance != null && Snapshot(targetInstance) == instanceBefore && PrefabUtility.IsPartOfPrefabInstance(targetInstance), "Prefab Undo did not restore the original linked target");
        Undo.PerformRedo();
        Require(Find(targetInstance, "Extra").GetComponent<DiNeTransplantProbe>().bone == Find(targetInstance, "Bone").transform, "Prefab Redo restored stale internal references");
        string scenePath = caseFolder + "/PrefabOverrides.unity";
        EditorSceneManager.SaveScene(root.scene, scenePath);
        EditorSceneManager.OpenScene(scenePath);
        var reloaded = Object.FindObjectsOfType<DiNeTransplantProbe>(true).Single(p => p.bone != null).transform.root.gameObject;
        Require(PrefabUtility.IsPartOfPrefabInstance(reloaded) && Find(reloaded, "Bone").GetComponent<BoxCollider>() != null &&
            Find(reloaded, "Extra").GetComponent<DiNeTransplantProbe>().bone == Find(reloaded, "Bone").transform, "Direct prefab overrides did not survive scene reload");
        AssetDatabase.SaveAssets();
        Require(File.ReadAllBytes(caseFolder + "/Source.prefab").SequenceEqual(sourceBytes) && File.ReadAllBytes(caseFolder + "/Target.prefab").SequenceEqual(targetBytes), "Input prefab assets were written");
    }

    private static void ModelAssetTarget()
    {
        string modelPath = caseFolder + "/NewModel.obj";
        File.WriteAllText(modelPath, "o ModelMesh\nv 0 0 0\nv 2 0 0\nv 0 2 0\nvn 0 0 1\nf 1//1 2//1 3//1\n");
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Require(model != null && PrefabUtility.GetPrefabAssetType(model) == PrefabAssetType.Model, "Generated fixture was not imported as a real model prefab");
        var source = Object.Instantiate(model); source.name = "OldModel";
        var meshHost = source.GetComponentInChildren<MeshFilter>().gameObject; meshHost.AddComponent<BoxCollider>().center = Vector3.one;
        Child(source, "Addon").AddComponent<DiNeTransplantProbe>().marker = 35;
        var modelBefore = Snapshot(model); var sourceBefore = Snapshot(source); var bytes = File.ReadAllBytes(modelPath);
        var targetMesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
        RequireAssetTargetRejected(source, model);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var root = Execute(source, instance).ResultRoot;
        Require(root.GetComponentInChildren<MeshFilter>().sharedMesh == targetMesh, "Imported target mesh was replaced");
        Require(root.GetComponentInChildren<BoxCollider>() != null && Find(root, "Addon").GetComponent<DiNeTransplantProbe>().marker == 35, "Model transplant components/objects missing");
        Require(root == instance && PrefabUtility.IsPartOfPrefabInstance(root) && PrefabUtility.GetCorrespondingObjectFromSource(root) == model, "Model scene instance linkage was lost");
        RequireInputsUnchanged(source, model, sourceBefore, modelBefore);
        Require(File.ReadAllBytes(modelPath).SequenceEqual(bytes), "Model asset file changed");
    }

    private static void RequireAssetTargetRejected(GameObject source, GameObject targetAsset)
    {
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(targetAsset);
        int roots = EditorSceneManager.GetActiveScene().rootCount;
        foreach (var report in new[] { DiNePrefabTransplantUtility.Analyze(source, targetAsset), DiNePrefabTransplantUtility.Execute(source, targetAsset) })
            Require(!report.Succeeded && report.ResultRoot == null && report.Diagnostics.Any(d => d.Code == "TargetAssetInput" && d.Severity == DiNePrefabTransplantUtility.DiagnosticSeverity.Error),
                "Asset target was not rejected with the scene-instance diagnostic");
        RequireInputsUnchanged(source, targetAsset, sourceBefore, targetBefore);
        Require(EditorSceneManager.GetActiveScene().rootCount == roots, "Asset target rejection created a clone");
    }

    private static void UndoRedo()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        var bone = Child(source, "Bone"); var targetBone = Child(target, "Bone");
        var probe = Child(source, "Addon").AddComponent<DiNeTransplantProbe>(); probe.marker = 909; probe.bone = bone.transform;
        var targetCollider = targetBone.AddComponent<BoxCollider>(); targetCollider.center = Vector3.one * 9;
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        Undo.ClearAll(); var root = Execute(source, target).ResultRoot; Undo.FlushUndoRecordObjects();
        string targetAfter = Snapshot(target); int targetId = target.GetInstanceID();
        Require(source.scene.rootCount == 2 && root == target, "A direct transplant created a result root");
        Undo.PerformUndo();
        Require(root == target && target != null && target.GetInstanceID() == targetId && source.scene.rootCount == 2, "One Undo destroyed or replaced the chosen target");
        RequireInputsUnchanged(source, target, sourceBefore, targetBefore);
        Undo.PerformRedo();
        var restored = Object.FindObjectsOfType<DiNeTransplantProbe>(true).Where(p => p.marker == 909 && p.transform.root != source.transform).ToArray();
        Require(restored.Length == 1 && source.scene.rootCount == 2 && target.GetInstanceID() == targetId && Snapshot(target) == targetAfter, "One Redo did not restore the complete direct transplant");
        var actual = restored[0];
        Require(actual.bone == actual.transform.root.Find("Bone"), "Redo restored a stale source bone reference");
        RequireSourceUnchanged(source, sourceBefore);
    }

    private static void Rollback()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        Child(source, "FirstAdded").AddComponent<BoxCollider>();
        Child(source, "Incompatible").AddComponent<DiNeTransplantExclusiveSource>();
        Child(target, "Incompatible").AddComponent<DiNeTransplantExclusiveTarget>();
        EditorSceneManager.SaveScene(target.scene, caseFolder + "/BeforeRollback.unity");
        Require(!target.scene.isDirty, "Rollback fixture did not start from a clean scene");
        var sourceBefore = Snapshot(source); var targetBefore = Snapshot(target);
        Undo.ClearAll();
        var report = DiNePrefabTransplantUtility.Execute(source, target);
        Require(!report.Succeeded && report.ResultRoot == null && !string.IsNullOrEmpty(report.Error), "Real component-add failure was reported as success");
        Require(source.scene.rootCount == 2, "Failed transaction left a partial result hierarchy");
        RequireInputsUnchanged(source, target, sourceBefore, targetBefore);
        Results.Add("INFO Rollback restored both inputs; scene remains dirty: " + target.scene.isDirty + " (conservative dirty-state policy)");
        Undo.PerformUndo();
        Require(source != null && target != null && source.scene.rootCount == 2, "Rolled-back operation left an Undo record affecting inputs");
    }

    private static void InvalidInputs()
    {
        var source = new GameObject("Source"); var target = new GameObject("Target");
        int roots = source.scene.rootCount;
        foreach (var report in new[] { DiNePrefabTransplantUtility.Analyze(null, target), DiNePrefabTransplantUtility.Execute(source, null), DiNePrefabTransplantUtility.Execute(source, source) })
            Require(!report.Succeeded && report.ResultRoot == null && !string.IsNullOrEmpty(report.Error), "Invalid inputs were accepted");
        Require(source.scene.rootCount == roots && source != null && target != null, "Invalid inputs mutated scene objects");
    }

    private static void WindowExecutionQueue()
    {
        var type = typeof(DiNePrefabTransplantUtility).Assembly.GetType("DiNeTool.ExtraModifier.Editor.DiNeExtraModifierWindow");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var window = ScriptableObject.CreateInstance(type);
        var originalSelection = Selection.activeObject;
        var source = new GameObject("Source"); var target = new GameObject("Target"); SetTransform(target.transform, 7);
        source.AddComponent<DiNeTransplantProbe>().marker = 939;
        Child(source, "Helper").AddComponent<BoxCollider>();
        var otherSource = new GameObject("OtherSource"); var otherTarget = new GameObject("OtherTarget");
        string sourceBefore = Snapshot(source), targetBefore = Snapshot(target), otherBefore = Snapshot(otherTarget);
        var states = CaptureExistingTarget(target);
        var queue = type.GetMethod("QueueTransplantExecution", flags); var cancel = type.GetMethod("CancelQueuedTransplant", flags);
        try
        {
            type.GetField("transplantSource", flags).SetValue(window, source);
            type.GetField("transplantTarget", flags).SetValue(window, target);
            queue.Invoke(window, null); queue.Invoke(window, null);
            var callbacks = (EditorApplication.delayCall?.GetInvocationList() ?? new Delegate[0]).Where(d => ReferenceEquals(d.Target, window) && d.Method.Name == "ExecuteQueuedTransplant").ToArray();
            Require(callbacks.Length == 1 && (bool)type.GetField("transplantExecutionPending", flags).GetValue(window), "Actual window did not enqueue exactly one transfer");
            Require(Snapshot(source) == sourceBefore && Snapshot(target) == targetBefore && type.GetField("transplantReport", flags).GetValue(window) == null && Selection.activeObject == originalSelection,
                "Queue changed objects, report or selection during the drawing event");
            type.GetField("transplantSource", flags).SetValue(window, otherSource);
            type.GetField("transplantTarget", flags).SetValue(window, otherTarget);
            // Invoke only this actual queued delegate, outside an OnGUI frame;
            // leave unrelated global editor callbacks registered and untouched.
            callbacks[0].DynamicInvoke();
            var report = (DiNePrefabTransplantUtility.Report)type.GetField("transplantReport", flags).GetValue(window);
            Require(report.Succeeded && report.ResultRoot == target && target.GetComponent<DiNeTransplantProbe>().marker == 939 && Find(target, "Helper").GetComponent<BoxCollider>() != null,
                "Deferred action ignored captured source/target or lost copied configuration");
            RequireExistingTargetUnchanged(states);
            Require(Snapshot(source) == sourceBefore && Snapshot(otherTarget) == otherBefore && Selection.activeGameObject == target && !(bool)type.GetField("transplantExecutionPending", flags).GetValue(window), "Deferred action changed source/other target or left its pending state");
            Require(!(EditorApplication.delayCall?.GetInvocationList() ?? new Delegate[0]).Any(d => ReferenceEquals(d.Target, window) && d.Method.Name == "ExecuteQueuedTransplant"), "Executed action stayed subscribed");

            source.GetComponent<DiNeTransplantProbe>().marker = 940;
            type.GetField("transplantSource", flags).SetValue(window, source); type.GetField("transplantTarget", flags).SetValue(window, target);
            string after = Snapshot(target);
            foreach (string cancellation in new[] { "OnDisable", "OnTransplantUndoRedo" })
            {
                queue.Invoke(window, null);
                var pending = (EditorApplication.delayCall?.GetInvocationList() ?? new Delegate[0]).Single(d => ReferenceEquals(d.Target, window) && d.Method.Name == "ExecuteQueuedTransplant");
                type.GetMethod(cancellation, flags).Invoke(window, null);
                Require(!(bool)type.GetField("transplantExecutionPending", flags).GetValue(window) && !(EditorApplication.delayCall?.GetInvocationList() ?? new Delegate[0]).Any(d => ReferenceEquals(d.Target, window) && d.Method.Name == "ExecuteQueuedTransplant"), "Window lifecycle did not cancel its queued action: " + cancellation);
                pending.DynamicInvoke();
                Require(Snapshot(target) == after, "A cancelled/stale action still modified its target: " + cancellation);
            }
            string panel = File.ReadAllText("Assets/Editor/ExtraModifierTransplantRegression/DiNeExtraModifierWindow.Transplant.cs");
            int start = panel.IndexOf("private void DrawTransplantTab", StringComparison.Ordinal), end = panel.IndexOf("private GameObject DrawTransplantInput", start, StringComparison.Ordinal);
            string draw = panel.Substring(start, end - start);
            Require(draw.Contains("QueueTransplantExecution()") && !draw.Contains("DiNePrefabTransplantUtility.Execute("), "Actual IMGUI draw still executes the transfer directly");
        }
        finally { cancel.Invoke(window, null); Object.DestroyImmediate(window); Selection.activeObject = originalSelection; }
    }

    private static void WindowLocalization()
    {
        var type = typeof(DiNePrefabTransplantUtility).Assembly.GetType("DiNeTool.ExtraModifier.Editor.DiNeExtraModifierWindow");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var dictionary = (Dictionary<string, string[]>)type.GetField("TransplantText", flags).GetValue(null);
        Require(dictionary.Count > 30, "Actual transplant localization catalog unavailable");
        foreach (var entry in dictionary)
        {
            Require(entry.Value.Length == 3 && entry.Value.All(s => !string.IsNullOrWhiteSpace(s)), "Incomplete English/Korean/Japanese text: " + entry.Key);
            var formats = entry.Value.Select(s => string.Join(",", Regex.Matches(s, @"\{\d+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v))).ToArray();
            Require(formats.Distinct().Count() == 1, "Localization format placeholders differ: " + entry.Key);
        }
        var originalText = (string[][])type.GetField("UiText", flags).GetValue(null);
        Require(originalText.All(row => row.Length == 3 && row.All(s => !string.IsNullOrWhiteSpace(s))), "Existing window text became incomplete");
        Require((string)type.GetField("LanguagePrefKey", flags).GetRawConstantValue() == "DiNeLang", "Window does not use shared Di Ne language preference");
        Require(type.GetField("transplantCopyTransforms", BindingFlags.Instance | BindingFlags.NonPublic) == null, "Obsolete transform-copy option still exists");
        const string scripts = "Assets/Editor/ExtraModifierTransplantRegression/";
        string core = File.ReadAllText(scripts + "DiNePrefabTransplantUtility.cs");
        foreach (Match match in Regex.Matches(core, "(?:Add|Fail)\\(\\s*report,\\s*\"(?<code>[^\"]+)\""))
            Require(dictionary.ContainsKey(match.Groups["code"].Value), "Unlocalized emitted diagnostic: " + match.Groups["code"].Value);
        string panel = File.ReadAllText(scripts + "DiNeExtraModifierWindow.Transplant.cs");
        Require(!panel.Contains("UseSelection") && !panel.Contains("transplantCopyTransforms") && !dictionary.ContainsKey("MatchedTransformsKept") && !dictionary.ContainsKey("TransformShear"), "Removed selection/transform controls or diagnostics remain in the actual panel");
        foreach (Match match in Regex.Matches(panel, "TT\\(\"(?<key>[^\"]+)\"\\)"))
            Require(dictionary.ContainsKey(match.Groups["key"].Value), "Unlocalized panel key: " + match.Groups["key"].Value);
    }
}
#endif

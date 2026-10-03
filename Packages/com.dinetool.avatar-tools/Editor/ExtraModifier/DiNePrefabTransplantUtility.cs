using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DiNeTool.ExtraModifier.Editor
{
    /// <summary>
    /// Restores prefab functionality directly on the assigned revised scene object.
    /// The source is read-only; all target changes belong to a single undo transaction.
    /// Renderer data belongs to the revised model; all other serialized component references
    /// are rebound only after the complete destination hierarchy and components exist.
    /// </summary>
    internal static class DiNePrefabTransplantUtility
    {
        public enum DiagnosticSeverity { Info, Warning, Error }

        public sealed class Diagnostic
        {
            public DiagnosticSeverity Severity;
            public string Code;
            public string Path;
            public string Message;
        }

        public sealed class Report
        {
            public GameObject ResultRoot;
            public string Error;
            public int MatchedObjects;
            public int CreatedObjects;
            public int SkippedMeshObjects;
            public int SkippedObjects;
            public int CopiedComponents;
            public int SkippedComponents;
            public int RemappedReferences;
            public int ClearedReferences;
            public int ExternalReferences;
            public int MissingScripts;
            public readonly List<Diagnostic> Diagnostics = new List<Diagnostic>();

            public bool Succeeded => string.IsNullOrEmpty(Error);
            public bool HasWarnings => WarningCount > 0;
            public int WarningCount
            {
                get
                {
                    int count = 0;
                    foreach (var diagnostic in Diagnostics)
                        if (diagnostic.Severity == DiagnosticSeverity.Warning) count++;
                    return count;
                }
            }
        }

        private sealed class Node
        {
            public Transform Source;
            public Transform Target;
            public Transform Destination;
            public Node Parent;
            public string Key;
            public string Path;
            public bool Create;
            public bool Ambiguous;
            public bool Available => Target != null || Create;
        }

        private sealed class ComponentCopy
        {
            public Node Node;
            public Component Source;
            public Component ExistingTarget;
            public Component Destination;
            public Type Type;
            public int Ordinal;
        }

        private sealed class Plan
        {
            public GameObject Source;
            public GameObject Target;
            public Scene Scene;
            public readonly List<Node> Nodes = new List<Node>();
            public readonly List<ComponentCopy> Components = new List<ComponentCopy>();
            public readonly Dictionary<Transform, Node> SourceNodes = new Dictionary<Transform, Node>();
            public readonly Dictionary<Component, ComponentCopy> SourceComponents = new Dictionary<Component, ComponentCopy>();
            public readonly Dictionary<Mesh, Mesh> Meshes = new Dictionary<Mesh, Mesh>();
            public readonly HashSet<Mesh> AmbiguousMeshes = new HashSet<Mesh>();
            public readonly HashSet<Mesh> RenderedMeshes = new HashSet<Mesh>();
        }

        private sealed class ManagedGraphCarrier : ScriptableObject
        {
            [SerializeReference] public object[] Roots;
        }

        private sealed class TargetState
        {
            public GameObject Object;
            public Transform Transform;
            public Transform Parent;
            public bool HadParent;
            public int SiblingIndex;
            public Scene Scene;
            public string Name;
            public bool Active;
            public int Layer;
            public string Tag;
            public StaticEditorFlags StaticFlags;
            public HideFlags ObjectHideFlags;
            public HideFlags TransformHideFlags;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Matrix4x4 WorldMatrix;
            public bool IsRect;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 SizeDelta;
            public Vector3 AnchoredPosition;
        }

        private const string UndoName = "Transplant prefab functionality";

        public static Report Analyze(GameObject source, GameObject target)
        {
            var report = new Report();
            try
            {
                var plan = BuildPlan(source, target, report);
                if (plan != null)
                    foreach (var component in plan.Components)
                        InspectReferences(plan, component, null, null, report);
            }
            catch (Exception exception)
            {
                Fail(report, "AnalysisFailed", string.Empty, exception.Message);
                Debug.LogException(exception, source);
            }
            return report;
        }

        public static Report Execute(GameObject source, GameObject target)
        {
            var report = new Report();
            Plan plan;
            try
            {
                // Rebuild every time: a previous preview may no longer match the scene or FBX.
                plan = BuildPlan(source, target, report);
            }
            catch (Exception exception)
            {
                Fail(report, "AnalysisFailed", string.Empty, exception.Message);
                Debug.LogException(exception, source);
                return report;
            }
            if (plan == null) return report;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            try
            {
                Undo.RegisterFullObjectHierarchyUndo(target, UndoName);
                var originalTargetStates = CaptureTargetStates(target);
                var originalMeshComponents = new HashSet<Component>();
                foreach (var transform in target.GetComponentsInChildren<Transform>(true))
                    foreach (var component in transform.GetComponents<Component>())
                        if (component != null && IsMeshComponent(component.GetType())) originalMeshComponents.Add(component);
                // Existing objects keep their state throughout the transfer. Only new helpers
                // stay inactive until their component data and references are ready.
                foreach (var node in plan.Nodes)
                {
                    if (node.Target != null)
                    {
                        node.Destination = node.Target;
                    }
                    else if (node.Create)
                    {
                        var created = node.Source is RectTransform
                            ? new GameObject(node.Source.name, typeof(RectTransform))
                            : new GameObject(node.Source.name);
                        created.SetActive(false);
                        Undo.RegisterCreatedObjectUndo(created, UndoName);
                        Undo.SetTransformParent(created.transform, node.Parent.Destination, UndoName);
                        node.Destination = created.transform;
                    }
                }

                foreach (var node in plan.Nodes)
                {
                    if (!node.Create) continue;
                    CopyTransform(node.Source, node.Destination);
                    CopyObjectSettings(node.Source.gameObject, node.Destination.gameObject);
                }

                // Add everything before copying: PhysBone collider arrays, constraints and
                // arbitrary scripts may refer forward to components later in the hierarchy.
                foreach (var component in plan.Components)
                {
                    var matches = ExactComponents(component.Node.Destination.gameObject, component.Type);
                    while (matches.Count <= component.Ordinal)
                    {
                        int previousCount = matches.Count;
                        var added = Undo.AddComponent(component.Node.Destination.gameObject, component.Type);
                        if (added == null)
                            throw new InvalidOperationException("Could not add " + component.Type.FullName + " at " + component.Node.Path);
                        // Native dependencies need not have managed RequireComponent metadata.
                        foreach (var dependency in component.Node.Destination.GetComponents<Component>())
                            if (dependency != null && IsMeshComponent(dependency.GetType()) && !originalMeshComponents.Contains(dependency))
                                throw new InvalidOperationException("Adding " + component.Type.FullName + " implicitly created a renderer or MeshFilter at " + component.Node.Path);
                        matches = ExactComponents(component.Node.Destination.gameObject, component.Type);
                        if (matches.Count <= previousCount)
                            throw new InvalidOperationException("Could not create the required component occurrence at " + component.Node.Path);
                    }
                    component.Destination = matches[component.Ordinal];
                }
                RestoreTargetStates(originalTargetStates);
                VerifyMeshComponents(target, originalMeshComponents);

                var objectMap = BuildObjectMap(plan);
                foreach (var component in plan.Components)
                {
                    // Keep later path-based remapping in the original destination hierarchy
                    // even when an earlier component's validation changed an existing object.
                    RestoreTargetStates(originalTargetStates);
                    var preservedMeshes = CaptureMeshes(component.Destination);
                    Undo.RecordObject(component.Destination, UndoName);
                    using (var sourceData = new SerializedObject(component.Source))
                    using (var destinationData = new SerializedObject(component.Destination))
                    {
                        sourceData.Update();
                        destinationData.Update();
                        var field = sourceData.GetIterator();
                        bool firstField = true;
                        while (field.Next(firstField))
                        {
                            firstField = false;
                            if (IsStructuralProperty(field.propertyPath) || field.propertyPath == "m_ObjectHideFlags" ||
                                (component.Source is Animator && field.propertyPath == "m_Avatar")) continue;
                            destinationData.CopyFromSerializedProperty(field);
                        }
                        CopyManagedGraphs(sourceData, destinationData);
                        // Apply once after rebinding the buffered data. OnValidate must never
                        // see transient references into the original prefab from our copy step.
                        InspectReferences(plan, component, objectMap, preservedMeshes, report, destinationData);
                    }
                    EditorUtility.SetDirty(component.Destination);
                    RecordPrefabOverrides(component.Destination);
                }

                foreach (var node in plan.Nodes)
                    if (node.Create && node.Destination.gameObject.activeSelf != node.Source.gameObject.activeSelf)
                    {
                        Undo.RecordObject(node.Destination.gameObject, UndoName);
                        node.Destination.gameObject.SetActive(node.Source.gameObject.activeSelf);
                    }
                RestoreTargetStates(originalTargetStates);
                foreach (var node in plan.Nodes)
                    if (node.Create)
                    {
                        if (node.Destination == null)
                            throw new InvalidOperationException("A component callback removed a newly added helper: " + node.Path);
                        if (node.Destination.parent != node.Parent.Destination)
                            Undo.SetTransformParent(node.Destination, node.Parent.Destination, UndoName);
                        if (!LocalTransformMatches(node.Source, node.Destination))
                            CopyTransform(node.Source, node.Destination);
                    }
                // ExecuteAlways/OnValidate/OnEnable implementations can move other objects.
                // Repair only values actually changed by those callbacks, including target-only
                // bones, and reject the transaction if the original state cannot be preserved.
                RestoreTargetStates(originalTargetStates);
                VerifyTargetStates(originalTargetStates);
                foreach (var node in plan.Nodes)
                    if (node.Create && (node.Destination == null || node.Destination.parent != node.Parent.Destination ||
                        !LocalTransformMatches(node.Source, node.Destination)))
                        throw new InvalidOperationException("Could not preserve the original local placement of a new helper: " +
                            node.Path + ". " + DescribeLocalPlacement(node));
                VerifyMeshComponents(target, originalMeshComponents);
                foreach (var node in plan.Nodes)
                    if (node.Create)
                    {
                        RecordPrefabOverrides(node.Destination);
                        RecordPrefabOverrides(node.Destination.gameObject);
                    }
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(plan.Scene);
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(undoGroup);
                report.ResultRoot = target;
            }
            catch (Exception exception)
            {
                string error = exception.Message;
                try { Undo.RevertAllDownToGroup(undoGroup); }
                catch (Exception rollbackException) { error += " (Undo rollback: " + rollbackException.Message + ")"; }
                Fail(report, "ExecutionFailed", string.Empty, error);
                Debug.LogException(exception, source);
            }
            finally
            {
                Undo.IncrementCurrentGroup();
            }
            return report;
        }

        private static Plan BuildPlan(GameObject source, GameObject target, Report report)
        {
            if (source == null) { Fail(report, "MissingSource", string.Empty, "Assign the original prefab or scene object."); return null; }
            if (target == null) { Fail(report, "MissingTarget", string.Empty, "Assign the revised model or scene object."); return null; }
            if (EditorUtility.IsPersistent(target))
            {
                Fail(report, "TargetAssetInput", target.name, "Drag the revised FBX or prefab into the Hierarchy and assign that scene instance as the target.");
                return null;
            }
            if (source == target) { Fail(report, "SameInput", source.name, "Source and target must be different objects."); return null; }
            if (source.transform.IsChildOf(target.transform) || target.transform.IsChildOf(source.transform))
            {
                Fail(report, "NestedInputs", source.name, "Source and target must not contain each other.");
                return null;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail(report, "Playing", string.Empty, "Transplant functionality in Edit Mode.");
                return null;
            }
            if (PrefabStageUtility.GetPrefabStage(source) != null || PrefabStageUtility.GetPrefabStage(target) != null)
            {
                Fail(report, "PrefabStageInput", string.Empty, "Use a prefab asset or a scene instance outside Prefab Mode.");
                return null;
            }
            if ((!EditorUtility.IsPersistent(source) && !UsableScene(source.scene)) ||
                (!EditorUtility.IsPersistent(target) && !UsableScene(target.scene)))
            {
                Fail(report, "InvalidScene", string.Empty, "An input belongs to a preview or unloaded scene.");
                return null;
            }

            var scene = target.scene;
            if (!UsableScene(scene))
            {
                Fail(report, "InvalidScene", string.Empty, "Assign a revised target in a regular loaded scene.");
                return null;
            }

            var plan = new Plan { Source = source, Target = target, Scene = scene };
            var sourceTransforms = source.GetComponentsInChildren<Transform>(true);
            var targetTransforms = target.GetComponentsInChildren<Transform>(true);
            var targetPaths = IndexHierarchy(target.transform);
            var sourceNames = IndexNames(sourceTransforms);
            var targetNames = IndexNames(targetTransforms);
            var used = new HashSet<Transform>();

            foreach (var transform in sourceTransforms)
            {
                var node = new Node
                {
                    Source = transform,
                    Key = GetKey(transform, source.transform),
                    Path = DisplayPath(transform, source.transform)
                };
                plan.Nodes.Add(node);
                plan.SourceNodes.Add(transform, node);
                if (transform == source.transform)
                {
                    node.Target = target.transform;
                }
                else
                {
                    node.Parent = plan.SourceNodes[transform.parent];
                    bool ambiguous = node.Parent.Ambiguous;
                    Transform match = null;
                    if (!ambiguous && node.Parent.Target != null)
                        match = MatchChild(transform, node.Parent.Target, out ambiguous);
                    if (match == null && !ambiguous && targetPaths.TryGetValue(node.Key, out var exact))
                    {
                        match = MatchChild(transform, exact.parent, out ambiguous);
                        if (match != exact) match = null;
                    }
                    if (ambiguous)
                    {
                        Add(report, node.Parent.Ambiguous ? "UnmappedParent" : "DuplicatePathAmbiguous", node.Path,
                            node.Parent.Ambiguous ? "The original parent was ambiguous; its child cannot be matched safely."
                                : "Duplicate sibling names differ between the original and revised hierarchy.");
                    }
                    else if (match == null && targetNames.TryGetValue(transform.name, out var candidates))
                    {
                        if (sourceNames[transform.name].Count == 1 && candidates.Count == 1 && !used.Contains(candidates[0]))
                        {
                            match = candidates[0];
                            Add(report, "NameMatched", node.Path, "Matched the only object of this name at a different hierarchy path.");
                        }
                        else
                        {
                            ambiguous = true;
                            Add(report, "AmbiguousName", node.Path, "The name occurs more than once, so a different hierarchy path cannot be matched safely.");
                        }
                    }
                    if (match != null && !used.Contains(match)) node.Target = match;
                    else if (ambiguous || (match != null && used.Contains(match)))
                    {
                        node.Ambiguous = true;
                        if (!ambiguous) Add(report, "AmbiguousName", node.Path, "This target object is already assigned to another source object.");
                        report.SkippedObjects++;
                    }
                    else if (HasMesh(transform.gameObject))
                    {
                        report.SkippedMeshObjects++;
                        Add(report, "MissingMeshObject", node.Path, "The revised model has no matching mesh object; the old mesh host is not recreated.");
                    }
                    else if (!node.Parent.Available)
                    {
                        report.SkippedObjects++;
                        Add(report, "UnmappedParent", node.Path, "The original parent was skipped; its missing child cannot be recreated safely.");
                    }
                    else
                    {
                        node.Create = true;
                        report.CreatedObjects++;
                    }
                }

                if (node.Target != null)
                {
                    used.Add(node.Target);
                    report.MatchedObjects++;
                    if (transform.GetType() != node.Target.GetType())
                        Add(report, "TransformTypeMismatch", node.Path, "Transform types differ; the revised model's transform type is retained.");
                }
            }

            foreach (var node in plan.Nodes)
            {
                var ordinals = new Dictionary<Type, int>();
                foreach (var component in node.Source.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        report.MissingScripts++;
                        Add(report, "MissingScript", node.Path, "A missing script cannot be copied.");
                        continue;
                    }
                    if (component is Transform) continue;
                    Type type = component.GetType();
                    ordinals.TryGetValue(type, out int ordinal);
                    ordinals[type] = ordinal + 1;
                    if (!node.Available || IsMeshComponent(type)) { report.SkippedComponents++; continue; }

                    var existing = node.Target != null ? ExactComponent(node.Target.gameObject, type, ordinal) : null;
                    if (existing == null && RequiresMissingMeshComponent(type, node.Target != null ? node.Target.gameObject : null, new HashSet<Type>()))
                    {
                        report.SkippedComponents++;
                        Add(report, "UnsafeRequiredComponent", node.Path + " (" + type.Name + ")", "Adding this component would implicitly create an old renderer or MeshFilter.");
                        continue;
                    }
                    if (existing == null && RequiresDifferentTransform(type, node.Target != null ? node.Target.GetType() : node.Source.GetType(), new HashSet<Type>()))
                    {
                        report.SkippedComponents++;
                        Add(report, "UnsafeRequiredTransform", node.Path + " (" + type.Name + ")", "Adding this component would require replacing the revised model's transform type.");
                        continue;
                    }
                    var operation = new ComponentCopy { Node = node, Source = component, ExistingTarget = existing, Type = type, Ordinal = ordinal };
                    plan.Components.Add(operation);
                    plan.SourceComponents.Add(component, operation);
                    report.CopiedComponents++;
                }
            }

            BuildMeshMap(plan);
            return plan;
        }

        private static void InspectReferences(Plan plan, ComponentCopy operation, Dictionary<Object, Object> objectMap,
            Dictionary<string, Mesh> preservedMeshes, Report report, SerializedObject bufferedData = null)
        {
            bool execute = objectMap != null;
            var serialized = bufferedData ?? new SerializedObject(execute ? operation.Destination : operation.Source);
            try
            {
                if (bufferedData == null) serialized.Update();
                var iterator = serialized.GetIterator();
                var managedIds = new HashSet<long>();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = iterator.propertyType != SerializedPropertyType.String;
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                        enterChildren = managedIds.Add(iterator.managedReferenceId);
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference || IsStructuralProperty(iterator.propertyPath)) continue;
                    if (operation.Source is Animator && iterator.propertyPath == "m_Avatar") continue;

                    var original = iterator.objectReferenceValue;
                    if (original == null) continue;
                    string path = operation.Node.Path + " (" + operation.Type.Name + ")." + iterator.propertyPath;
                    Object replacement = original;
                    if (original is Mesh mesh && plan.RenderedMeshes.Contains(mesh))
                    {
                        if (plan.Meshes.TryGetValue(mesh, out var revisedMesh))
                        {
                            replacement = revisedMesh;
                            report.RemappedReferences++;
                            Add(report, "MeshReferenceRetargeted", path, "Rebound the original mesh reference to its unique revised mesh.", DiagnosticSeverity.Info);
                        }
                        else
                        {
                            replacement = PreservedMesh(operation, iterator.propertyPath, preservedMeshes);
                            if (replacement == null) report.ClearedReferences++; else report.RemappedReferences++;
                            Add(report, "MeshReferenceExcluded", path, replacement == null
                                ? "The old mesh reference has no unique revised counterpart and is cleared."
                                : "The old mesh reference is omitted; the target component's existing mesh is retained.");
                        }
                    }
                    else if (BelongsTo(original, plan.Source.transform))
                    {
                        if (execute)
                        {
                            if (!objectMap.TryGetValue(original, out replacement)) replacement = null;
                        }
                        else
                        {
                            replacement = PlannedCounterpart(plan, original);
                        }
                        if (replacement == null)
                        {
                            report.ClearedReferences++;
                            Add(report, "MissingInternalReference", path, "The original internal object or component was skipped; the reference is cleared.");
                        }
                        else report.RemappedReferences++;
                    }
                    else if (BelongsTo(original, plan.Target.transform))
                    {
                        if (execute && !objectMap.TryGetValue(original, out replacement)) replacement = null;
                        if (replacement == null) report.ClearedReferences++; else report.RemappedReferences++;
                    }
                    else
                    {
                        report.ExternalReferences++;
                        Add(report, "ExternalReference", path, "The reference outside the original hierarchy is retained: " + original.name,
                            EditorUtility.IsPersistent(original) ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning);
                    }

                    if (execute && replacement != original) iterator.objectReferenceValue = replacement;
                }
                RepairAvatarObjectPaths(plan, operation, serialized, objectMap, report);
                if (execute) serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                if (bufferedData == null) serialized.Dispose();
            }

            var sourceAnimator = operation.Source as Animator;
            if (sourceAnimator != null)
            {
                var destinationAnimator = execute ? operation.Destination as Animator : operation.ExistingTarget as Animator;
                if (sourceAnimator.avatar != (destinationAnimator != null ? destinationAnimator.avatar : null))
                    Add(report, destinationAnimator != null && destinationAnimator.avatar != null ? "AnimatorAvatarPreserved" : "AnimatorAvatarMissing",
                        operation.Node.Path, destinationAnimator != null && destinationAnimator.avatar != null
                            ? "The revised model's Animator Avatar is retained instead of the original rig Avatar."
                            : "The revised model has no Animator Avatar. Configure its Rig import settings if a humanoid rig is required.",
                        destinationAnimator != null && destinationAnimator.avatar != null ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning);
            }
        }

        private static void RepairAvatarObjectPaths(Plan plan, ComponentCopy operation, SerializedObject destination,
            Dictionary<Object, Object> objectMap, Report report)
        {
            // Only recognize Modular Avatar's own runtime component/value type pair. Other
            // string paths (animation bindings, BoneProxy subPath, user scripts) are untouched.
            var assembly = operation.Source.GetType().Assembly;
            var referenceType = assembly.GetType("nadena.dev.modular_avatar.core.AvatarObjectReference", false);
            var runtimeType = assembly.GetType("nadena.dev.modular_avatar.core.RuntimeUtil", false);
            if (referenceType == null || runtimeType == null) return;
            var findAvatar = runtimeType.GetMethod("FindAvatarTransformInParents", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Transform) }, null);
            if (findAvatar == null) return;
            bool execute = objectMap != null;
            Transform sourceAvatar = findAvatar.Invoke(null, new object[] { operation.Source.transform }) as Transform;
            Transform destinationAvatar = findAvatar.Invoke(null, new object[] { execute ? operation.Node.Destination : plan.Target.transform }) as Transform;
            using (var source = new SerializedObject(operation.Source))
            {
                source.Update();
                var iterator = destination.GetIterator();
                var visited = new HashSet<long>();
                bool children = true;
                while (iterator.Next(children))
                {
                    children = iterator.propertyType != SerializedPropertyType.String;
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference) children = visited.Add(iterator.managedReferenceId);
                    if (iterator.propertyType != SerializedPropertyType.Generic || iterator.type != referenceType.Name) continue;
                    var original = source.FindProperty(iterator.propertyPath);
                    var originalPath = original?.FindPropertyRelative("referencePath");
                    var originalObject = original?.FindPropertyRelative("targetObject");
                    var targetPath = iterator.FindPropertyRelative("referencePath");
                    var targetObject = iterator.FindPropertyRelative("targetObject");
                    if (originalPath == null || originalPath.propertyType != SerializedPropertyType.String ||
                        originalObject == null || originalObject.propertyType != SerializedPropertyType.ObjectReference || targetPath == null || targetObject == null) continue;

                    string path = originalPath.stringValue;
                    // MA treats an empty path as an intentionally unset reference, even if
                    // a stale direct object is serialized. Preserve that inactive state.
                    if (string.IsNullOrEmpty(path)) continue;
                    var resolved = originalObject.objectReferenceValue as GameObject;
                    if (resolved != null && sourceAvatar != null && !resolved.transform.IsChildOf(sourceAvatar)) resolved = null;
                    bool internalPath = false;
                    if (resolved == null)
                    {
                        if (sourceAvatar != null)
                        {
                            resolved = path == "$$$AVATAR_ROOT$$$" ? sourceAvatar.gameObject : FindUniquePath(sourceAvatar, path)?.gameObject;
                            string prefix = RelativeNamePath(sourceAvatar, plan.Source.transform);
                            internalPath = prefix != null && (prefix.Length == 0 || path == prefix || path.StartsWith(prefix + "/", StringComparison.Ordinal));
                        }
                        else if (path == plan.Source.name || path.StartsWith(plan.Source.name + "/", StringComparison.Ordinal))
                        {
                            internalPath = true;
                            string localPath = path == plan.Source.name ? string.Empty : path.Substring(plan.Source.name.Length + 1);
                            resolved = FindUniquePath(plan.Source.transform, localPath)?.gameObject;
                        }
                    }
                    // External avatar references retain their paths and generic object refs.
                    if (resolved != null && !BelongsTo(resolved, plan.Source.transform)) continue;
                    if (resolved == null && !internalPath) continue;
                    string location = operation.Node.Path + " (" + operation.Type.Name + ")." + iterator.propertyPath;
                    GameObject counterpart = null;
                    if (resolved != null)
                    {
                        if (execute)
                        {
                            if (objectMap.TryGetValue(resolved, out var mapped)) counterpart = mapped as GameObject;
                        }
                        else counterpart = PlannedCounterpart(plan, resolved) as GameObject;
                    }
                    if (counterpart == null)
                    {
                        if (execute) { targetPath.stringValue = string.Empty; targetObject.objectReferenceValue = null; }
                        report.ClearedReferences++;
                        Add(report, "AvatarObjectPathUnresolved", location, "An internal Modular Avatar object path is missing or ambiguous and is cleared instead of pointing back to the original.");
                        continue;
                    }

                    if (execute)
                    {
                        targetObject.objectReferenceValue = counterpart;
                        if (destinationAvatar != null)
                        {
                            string revisedPath = RelativeNamePath(destinationAvatar, counterpart.transform);
                            if (revisedPath != null) targetPath.stringValue = revisedPath.Length == 0 ? "$$$AVATAR_ROOT$$$" : revisedPath;
                        }
                        else
                        {
                            string relative = RelativeNamePath(plan.Target.transform, counterpart.transform);
                            if (relative != null) targetPath.stringValue = plan.Target.name + (relative.Length == 0 ? string.Empty : "/" + relative);
                        }
                    }
                    report.RemappedReferences++;
                    Add(report, "AvatarObjectPathRemapped", location, "The internal Modular Avatar reference now points to the assigned target's matching object.", DiagnosticSeverity.Info);
                    if (destinationAvatar == null)
                        Add(report, "AvatarObjectPathOutsideAvatar", location, "The target has no avatar ancestor. The object reference and target-root path are rebound; check the path after placing it under an avatar.");
                }
            }
        }

        private static Transform FindUniquePath(Transform root, string path)
        {
            if (path.Length == 0) return root;
            var current = root;
            foreach (string segment in path.Split('/'))
            {
                Transform found = null;
                foreach (Transform child in current)
                    if (child.name == segment)
                    {
                        if (found != null) return null;
                        found = child;
                    }
                if (found == null) return null;
                current = found;
            }
            return current;
        }

        private static string RelativeNamePath(Transform root, Transform child)
        {
            var names = new List<string>();
            while (child != null && child != root) { names.Add(child.name); child = child.parent; }
            if (child != root) return null;
            names.Reverse();
            return string.Join("/", names);
        }

        private static void CopyManagedGraphs(SerializedObject source, SerializedObject destination)
        {
            // Unity 2022.3 CopyFromSerializedProperty can truncate a cyclic managed graph's
            // edge back to its copied root. A single temporary host lets Unity itself clone
            // every supported serialized type, preserving cycles and cross-field aliases.
            var paths = new List<string>();
            var roots = new List<object>();
            var iterator = source.GetIterator();
            bool children = true;
            while (iterator.Next(children))
            {
                children = iterator.propertyType != SerializedPropertyType.String;
                if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                paths.Add(iterator.propertyPath);
                roots.Add(iterator.managedReferenceValue);
                // The temporary host clones the complete graph. Reassigning nested nodes
                // separately could break their identity; only collect each outer root.
                children = false;
            }
            if (paths.Count == 0) return;
            ManagedGraphCarrier carrier = null;
            ManagedGraphCarrier cloned = null;
            try
            {
                carrier = ScriptableObject.CreateInstance<ManagedGraphCarrier>();
                carrier.hideFlags = HideFlags.HideAndDontSave;
                carrier.Roots = roots.ToArray();
                cloned = Object.Instantiate(carrier);
                cloned.hideFlags = HideFlags.HideAndDontSave;
                for (int i = 0; i < paths.Count; i++)
                {
                    var property = destination.FindProperty(paths[i]);
                    if (property != null) property.managedReferenceValue = cloned.Roots[i];
                }
            }
            finally
            {
                if (cloned != null) Object.DestroyImmediate(cloned);
                if (carrier != null) Object.DestroyImmediate(carrier);
            }
        }

        private static Object PlannedCounterpart(Plan plan, Object original)
        {
            var transform = ObjectTransform(original);
            if (transform == null || !plan.SourceNodes.TryGetValue(transform, out var node) || !node.Available) return null;
            if (original is GameObject) return node.Target != null ? (Object)node.Target.gameObject : original;
            if (original is Transform)
            {
                if (original is RectTransform && node.Target != null && !(node.Target is RectTransform)) return null;
                return node.Target != null ? (Object)node.Target : original;
            }
            var component = original as Component;
            if (component == null) return null;
            if (plan.SourceComponents.TryGetValue(component, out var copied))
                return copied.ExistingTarget != null ? (Object)copied.ExistingTarget : original;
            return node.Target != null ? ExactComponent(node.Target.gameObject, component.GetType(), ComponentOrdinal(component)) : null;
        }

        private static Dictionary<Object, Object> BuildObjectMap(Plan plan)
        {
            var map = new Dictionary<Object, Object>();
            foreach (var targetTransform in plan.Target.GetComponentsInChildren<Transform>(true))
            {
                map[targetTransform] = targetTransform;
                map[targetTransform.gameObject] = targetTransform.gameObject;
                foreach (var component in targetTransform.GetComponents<Component>())
                    if (component != null)
                        map[component] = component;
            }
            foreach (var node in plan.Nodes)
            {
                if (node.Destination == null) continue;
                map[node.Source.gameObject] = node.Destination.gameObject;
                if (!(node.Source is RectTransform) || node.Destination is RectTransform)
                    map[node.Source] = node.Destination;
                foreach (var component in node.Source.GetComponents<Component>())
                {
                    if (component == null || component is Transform || plan.SourceComponents.ContainsKey(component)) continue;
                    var counterpart = ExactComponent(node.Destination.gameObject, component.GetType(), ComponentOrdinal(component));
                    if (counterpart != null) map[component] = counterpart;
                }
            }
            foreach (var operation in plan.Components) map[operation.Source] = operation.Destination;
            return map;
        }

        private static void RecordPrefabOverrides(Object value)
        {
            if (value != null && PrefabUtility.IsPartOfPrefabInstance(value))
                PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }

        private static void VerifyMeshComponents(GameObject root, HashSet<Component> expected)
        {
            var actual = new HashSet<Component>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null || !IsMeshComponent(component.GetType())) continue;
                    if (!expected.Contains(component))
                        throw new InvalidOperationException("A component callback created a renderer or MeshFilter in the target.");
                    actual.Add(component);
                }
            foreach (var component in expected)
                if (component == null || !actual.Contains(component))
                    throw new InvalidOperationException("A component callback removed a revised model renderer or MeshFilter.");
        }

        private static void CopyTransform(Transform source, Transform destination)
        {
            Undo.RecordObject(destination, UndoName);
            if (source is RectTransform sourceRect && destination is RectTransform destinationRect)
            {
                destinationRect.anchorMin = sourceRect.anchorMin;
                destinationRect.anchorMax = sourceRect.anchorMax;
                destinationRect.pivot = sourceRect.pivot;
                destinationRect.sizeDelta = sourceRect.sizeDelta;
                destinationRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            }
            // Added helpers use source-local placement under the preserved destination parent.
            // Rect anchors can derive a different local position when that parent's size differs.
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        private static bool LocalTransformMatches(Transform source, Transform destination) =>
            source.localPosition.Equals(destination.localPosition) && RotationsMatch(source.localRotation, destination.localRotation) &&
            source.localScale.Equals(destination.localScale);

        private static bool RotationsMatch(Quaternion expected, Quaternion actual)
        {
            // Transform setters normalize imported serialized rotations. Equivalent q/-q or
            // a few normalization rounding bits must not be mistaken for a moved helper.
            // Existing target state is still checked exactly by TargetTransformMatches.
            double expectedLength = Math.Sqrt((double)expected.x * expected.x + (double)expected.y * expected.y +
                (double)expected.z * expected.z + (double)expected.w * expected.w);
            double actualLength = Math.Sqrt((double)actual.x * actual.x + (double)actual.y * actual.y +
                (double)actual.z * actual.z + (double)actual.w * actual.w);
            if (expectedLength <= 0d || actualLength <= 0d || double.IsNaN(expectedLength) || double.IsNaN(actualLength) ||
                double.IsInfinity(expectedLength) || double.IsInfinity(actualLength)) return false;
            if (expected.Equals(actual)) return true;
            double dot = ((double)expected.x * actual.x + (double)expected.y * actual.y +
                (double)expected.z * actual.z + (double)expected.w * actual.w) / (expectedLength * actualLength);
            double sign = dot < 0d ? -1d : 1d;
            double x = expected.x / expectedLength - sign * actual.x / actualLength;
            double y = expected.y / expectedLength - sign * actual.y / actualLength;
            double z = expected.z / expectedLength - sign * actual.z / actualLength;
            double w = expected.w / expectedLength - sign * actual.w / actualLength;
            // A normalized quaternion difference of 1e-6 is at most about 0.000115 degrees.
            // Double arithmetic avoids the coarse angle resolution of a float dot product.
            return x * x + y * y + z * z + w * w <= 1e-12d;
        }

        private static string DescribeLocalPlacement(Node node)
        {
            if (node.Destination == null) return "The newly added helper no longer exists.";
            if (node.Destination.parent != node.Parent.Destination) return "The helper's parent differs from its mapped destination parent.";
            return "Expected position " + node.Source.localPosition.ToString("R") + ", rotation " + node.Source.localRotation.ToString("R") +
                ", scale " + node.Source.localScale.ToString("R") + "; actual position " + node.Destination.localPosition.ToString("R") +
                ", rotation " + node.Destination.localRotation.ToString("R") + ", scale " + node.Destination.localScale.ToString("R") + ".";
        }

        private static void CopyObjectSettings(GameObject source, GameObject destination)
        {
            Undo.RecordObject(destination, UndoName);
            destination.layer = source.layer;
            destination.tag = source.tag;
            GameObjectUtility.SetStaticEditorFlags(destination, GameObjectUtility.GetStaticEditorFlags(source));
            destination.hideFlags &= ~HideFlags.NotEditable;
            EditorUtility.SetDirty(destination);
        }

        private static List<TargetState> CaptureTargetStates(GameObject root)
        {
            var states = new List<TargetState>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var gameObject = transform.gameObject;
                var state = new TargetState
                {
                    Object = gameObject,
                    Transform = transform,
                    Parent = transform.parent,
                    HadParent = transform.parent != null,
                    SiblingIndex = transform.GetSiblingIndex(),
                    Scene = gameObject.scene,
                    Name = gameObject.name,
                    Active = gameObject.activeSelf,
                    Layer = gameObject.layer,
                    Tag = gameObject.tag,
                    StaticFlags = GameObjectUtility.GetStaticEditorFlags(gameObject),
                    ObjectHideFlags = gameObject.hideFlags,
                    TransformHideFlags = transform.hideFlags,
                    Position = transform.localPosition,
                    Rotation = transform.localRotation,
                    Scale = transform.localScale,
                    WorldMatrix = transform.localToWorldMatrix
                };
                if (transform is RectTransform rect)
                {
                    state.IsRect = true;
                    state.AnchorMin = rect.anchorMin;
                    state.AnchorMax = rect.anchorMax;
                    state.Pivot = rect.pivot;
                    state.SizeDelta = rect.sizeDelta;
                    state.AnchoredPosition = rect.anchoredPosition3D;
                }
                states.Add(state);
            }
            return states;
        }

        private static void RequireTargetStateObjects(TargetState state)
        {
            if (state.Object == null || state.Transform == null || state.Object.transform != state.Transform ||
                state.Object.scene != state.Scene || (state.HadParent && state.Parent == null))
                throw new InvalidOperationException("A component callback destroyed or replaced an existing target object: " + state.Name);
        }

        private static void RestoreTargetStates(List<TargetState> states)
        {
            var changed = new HashSet<Object>();
            // Restore activation before transforms: OnEnable can move other bones synchronously.
            // No existing object is deactivated by the transfer itself.
            foreach (var state in states)
            {
                RequireTargetStateObjects(state);
                if (state.Object.activeSelf != state.Active)
                {
                    Undo.RecordObject(state.Object, UndoName);
                    state.Object.SetActive(state.Active);
                    changed.Add(state.Object);
                }
            }
            foreach (var state in states)
            {
                RequireTargetStateObjects(state);
                if (state.Transform.parent != state.Parent)
                {
                    Undo.SetTransformParent(state.Transform, state.Parent, UndoName);
                    changed.Add(state.Transform);
                }
            }
            foreach (var state in states)
            {
                RequireTargetStateObjects(state);
                if (state.Transform.GetSiblingIndex() != state.SiblingIndex)
                {
                    Undo.RecordObject(state.Transform, UndoName);
                    state.Transform.SetSiblingIndex(state.SiblingIndex);
                    changed.Add(state.Transform);
                }
                var gameObject = state.Object;
                if (gameObject.name != state.Name || gameObject.layer != state.Layer || gameObject.tag != state.Tag ||
                    gameObject.hideFlags != state.ObjectHideFlags || GameObjectUtility.GetStaticEditorFlags(gameObject) != state.StaticFlags)
                {
                    Undo.RecordObject(gameObject, UndoName);
                    if (gameObject.name != state.Name) gameObject.name = state.Name;
                    if (gameObject.layer != state.Layer) gameObject.layer = state.Layer;
                    if (gameObject.tag != state.Tag) gameObject.tag = state.Tag;
                    if (gameObject.hideFlags != state.ObjectHideFlags) gameObject.hideFlags = state.ObjectHideFlags;
                    if (GameObjectUtility.GetStaticEditorFlags(gameObject) != state.StaticFlags)
                        GameObjectUtility.SetStaticEditorFlags(gameObject, state.StaticFlags);
                    changed.Add(gameObject);
                }
                if (state.Transform.hideFlags != state.TransformHideFlags)
                {
                    Undo.RecordObject(state.Transform, UndoName);
                    state.Transform.hideFlags = state.TransformHideFlags;
                    changed.Add(state.Transform);
                }
            }
            foreach (var state in states)
            {
                RequireTargetStateObjects(state);
                if (TargetTransformMatches(state)) continue;
                Undo.RecordObject(state.Transform, UndoName);
                using (var serialized = new SerializedObject(state.Transform))
                {
                    serialized.Update();
                    serialized.FindProperty("m_LocalPosition").vector3Value = state.Position;
                    serialized.FindProperty("m_LocalRotation").quaternionValue = state.Rotation;
                    serialized.FindProperty("m_LocalScale").vector3Value = state.Scale;
                    if (state.IsRect)
                    {
                        serialized.FindProperty("m_AnchorMin").vector2Value = state.AnchorMin;
                        serialized.FindProperty("m_AnchorMax").vector2Value = state.AnchorMax;
                        serialized.FindProperty("m_Pivot").vector2Value = state.Pivot;
                        serialized.FindProperty("m_SizeDelta").vector2Value = state.SizeDelta;
                        serialized.FindProperty("m_AnchoredPosition").vector2Value = state.AnchoredPosition;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                changed.Add(state.Transform);
            }
            foreach (var value in changed) RecordPrefabOverrides(value);
        }

        private static bool TargetTransformMatches(TargetState state)
        {
            if (!state.Transform.localPosition.Equals(state.Position) || !state.Transform.localRotation.Equals(state.Rotation) ||
                !state.Transform.localScale.Equals(state.Scale)) return false;
            if (!state.IsRect) return true;
            var rect = state.Transform as RectTransform;
            return rect != null && rect.anchorMin.Equals(state.AnchorMin) && rect.anchorMax.Equals(state.AnchorMax) &&
                rect.pivot.Equals(state.Pivot) && rect.sizeDelta.Equals(state.SizeDelta) && rect.anchoredPosition3D.Equals(state.AnchoredPosition);
        }

        private static void VerifyTargetStates(List<TargetState> states)
        {
            foreach (var state in states)
            {
                RequireTargetStateObjects(state);
                var gameObject = state.Object;
                if (state.Transform.parent != state.Parent || state.Transform.GetSiblingIndex() != state.SiblingIndex ||
                    gameObject.name != state.Name || gameObject.activeSelf != state.Active || gameObject.layer != state.Layer ||
                    gameObject.tag != state.Tag || gameObject.hideFlags != state.ObjectHideFlags ||
                    GameObjectUtility.GetStaticEditorFlags(gameObject) != state.StaticFlags ||
                    state.Transform.hideFlags != state.TransformHideFlags || !TargetTransformMatches(state) ||
                    !state.Transform.localToWorldMatrix.Equals(state.WorldMatrix))
                    throw new InvalidOperationException("A component callback prevented preservation of an existing target object: " + state.Name);
            }
        }

        private static void BuildMeshMap(Plan plan)
        {
            foreach (var node in plan.Nodes)
            {
                foreach (var component in node.Source.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var renderedMesh = component is SkinnedMeshRenderer sourceSkin ? sourceSkin.sharedMesh
                        : component is MeshFilter sourceFilter ? sourceFilter.sharedMesh : null;
                    if (renderedMesh != null) plan.RenderedMeshes.Add(renderedMesh);
                    if (node.Target == null) continue;
                    var target = ExactComponent(node.Target.gameObject, component.GetType(), ComponentOrdinal(component));
                    if (component is SkinnedMeshRenderer skin && target is SkinnedMeshRenderer targetSkin)
                        AddMeshPair(plan, skin.sharedMesh, targetSkin.sharedMesh);
                    else if (component is MeshFilter filter && target is MeshFilter targetFilter)
                        AddMeshPair(plan, filter.sharedMesh, targetFilter.sharedMesh);
                }
            }
        }

        private static void AddMeshPair(Plan plan, Mesh source, Mesh target)
        {
            if (source == null || target == null || plan.AmbiguousMeshes.Contains(source)) return;
            if (plan.Meshes.TryGetValue(source, out var existing) && existing != target)
            {
                plan.Meshes.Remove(source);
                plan.AmbiguousMeshes.Add(source);
            }
            else plan.Meshes[source] = target;
        }

        private static Dictionary<string, Mesh> CaptureMeshes(Component component)
        {
            var meshes = new Dictionary<string, Mesh>();
            using (var serialized = new SerializedObject(component))
            {
                var iterator = serialized.GetIterator();
                var visited = new HashSet<long>();
                bool children = true;
                while (iterator.Next(children))
                {
                    children = iterator.propertyType != SerializedPropertyType.String;
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference) children = visited.Add(iterator.managedReferenceId);
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is Mesh mesh)
                        meshes[iterator.propertyPath] = mesh;
                }
            }
            return meshes;
        }

        private static Mesh PreservedMesh(ComponentCopy operation, string path, Dictionary<string, Mesh> preserved)
        {
            if (preserved != null) return preserved.TryGetValue(path, out var mesh) ? mesh : null;
            if (operation.ExistingTarget == null) return null;
            using (var serialized = new SerializedObject(operation.ExistingTarget))
                return serialized.FindProperty(path)?.objectReferenceValue as Mesh;
        }

        private static bool RequiresMissingMeshComponent(Type type, GameObject target, HashSet<Type> visited)
        {
            if (!visited.Add(type)) return false;
            // These native dependencies are not consistently exposed as managed attributes.
            if (type == typeof(ParticleSystem) && (target == null || target.GetComponent<ParticleSystemRenderer>() == null)) return true;
            if (type == typeof(Cloth) && (target == null || target.GetComponent<SkinnedMeshRenderer>() == null)) return true;
            foreach (RequireComponent requirement in type.GetCustomAttributes(typeof(RequireComponent), true))
            {
                var types = new[] { requirement.m_Type0, requirement.m_Type1, requirement.m_Type2 };
                foreach (var required in types)
                {
                    if (required == null || (target != null && target.GetComponent(required) != null)) continue;
                    if (IsMeshComponent(required) || RequiresMissingMeshComponent(required, target, visited)) return true;
                }
            }
            return false;
        }

        private static bool RequiresDifferentTransform(Type type, Type destinationTransformType, HashSet<Type> visited)
        {
            if (!visited.Add(type)) return false;
            foreach (RequireComponent requirement in type.GetCustomAttributes(typeof(RequireComponent), true))
                foreach (var required in new[] { requirement.m_Type0, requirement.m_Type1, requirement.m_Type2 })
                {
                    if (required == null) continue;
                    if (typeof(Transform).IsAssignableFrom(required) && !required.IsAssignableFrom(destinationTransformType)) return true;
                    if (RequiresDifferentTransform(required, destinationTransformType, visited)) return true;
                }
            return false;
        }

        private static bool IsMeshComponent(Type type) => typeof(Renderer).IsAssignableFrom(type) || typeof(MeshFilter).IsAssignableFrom(type);
        private static bool HasMesh(GameObject gameObject) => gameObject.GetComponent<Renderer>() != null || gameObject.GetComponent<MeshFilter>() != null;
        private static bool UsableScene(Scene scene) => scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);
        private static bool IsStructuralProperty(string path) => path == "m_GameObject" || path == "m_Script" || path == "m_CorrespondingSourceObject" || path == "m_PrefabInstance" || path == "m_PrefabAsset";

        private static Transform ObjectTransform(Object value) => value is GameObject gameObject ? gameObject.transform : (value as Component)?.transform;
        private static bool BelongsTo(Object value, Transform root)
        {
            var transform = ObjectTransform(value);
            return transform != null && (transform == root || transform.IsChildOf(root));
        }

        private static List<Component> ExactComponents(GameObject gameObject, Type type)
        {
            var result = new List<Component>();
            foreach (var component in gameObject.GetComponents(type))
                if (component != null && component.GetType() == type) result.Add(component);
            return result;
        }

        private static Component ExactComponent(GameObject gameObject, Type type, int ordinal)
        {
            var components = ExactComponents(gameObject, type);
            return ordinal < components.Count ? components[ordinal] : null;
        }

        private static int ComponentOrdinal(Component component)
        {
            int ordinal = 0;
            foreach (var other in component.gameObject.GetComponents(component.GetType()))
            {
                if (other == component) return ordinal;
                if (other != null && other.GetType() == component.GetType()) ordinal++;
            }
            return ordinal;
        }

        private static Dictionary<string, List<Transform>> IndexNames(Transform[] transforms)
        {
            var result = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
            foreach (var transform in transforms)
            {
                if (!result.TryGetValue(transform.name, out var entries)) result.Add(transform.name, entries = new List<Transform>());
                entries.Add(transform);
            }
            return result;
        }

        private static Dictionary<string, Transform> IndexHierarchy(Transform root)
        {
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) result.Add(GetKey(transform, root), transform);
            return result;
        }

        private static Transform MatchChild(Transform source, Transform parent, out bool ambiguous)
        {
            ambiguous = false;
            if (parent == null) return null;
            var candidates = new List<Transform>();
            foreach (Transform child in parent) if (child.name == source.name) candidates.Add(child);
            int sourceCount = 0;
            int ordinal = 0;
            foreach (Transform sibling in source.parent)
                if (sibling.name == source.name)
                {
                    if (sibling == source) ordinal = sourceCount;
                    sourceCount++;
                }
            if (candidates.Count > 0 && candidates.Count != sourceCount && (candidates.Count > 1 || sourceCount > 1))
            {
                ambiguous = true;
                return null;
            }
            return ordinal < candidates.Count ? candidates[ordinal] : null;
        }

        // Length-prefixed names also handle slashes and literal ordinal markers in object names.
        private static string GetKey(Transform transform, Transform root)
        {
            if (transform == root) return string.Empty;
            var segments = new List<string>();
            while (transform != null && transform != root)
            {
                int ordinal = 0;
                if (transform.parent != null)
                    foreach (Transform sibling in transform.parent)
                    {
                        if (sibling == transform) break;
                        if (sibling.name == transform.name) ordinal++;
                    }
                segments.Add(transform.name.Length + ":" + transform.name + ":" + ordinal);
                transform = transform.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string DisplayPath(Transform transform, Transform root)
        {
            var names = new List<string>();
            while (transform != null)
            {
                int ordinal = 0;
                int count = 0;
                if (transform.parent != null && transform != root)
                    foreach (Transform sibling in transform.parent)
                        if (sibling.name == transform.name)
                        {
                            if (sibling == transform) ordinal = count;
                            count++;
                        }
                names.Add(transform.name + (count > 1 ? " [" + (ordinal + 1) + "]" : string.Empty));
                if (transform == root) break;
                transform = transform.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private static void Add(Report report, string code, string path, string message, DiagnosticSeverity severity = DiagnosticSeverity.Warning)
        {
            report.Diagnostics.Add(new Diagnostic { Severity = severity, Code = code, Path = path, Message = message });
        }

        private static void Fail(Report report, string code, string path, string message)
        {
            report.Error = message;
            Add(report, code, path, message, DiagnosticSeverity.Error);
        }
    }
}
